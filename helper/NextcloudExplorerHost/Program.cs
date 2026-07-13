using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextcloudExplorerOpen;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--configure", StringComparer.OrdinalIgnoreCase))
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ConfigurationForm());
            return 0;
        }

        if (args.Contains("--remove-user-data", StringComparer.OrdinalIgnoreCase))
        {
            ConfigurationStore.DeleteUserData();
            return 0;
        }

        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTest.Run(Console.OpenStandardOutput());
        }

        if (args.Contains("--status-json", StringComparer.OrdinalIgnoreCase))
        {
            byte[] status = JsonSerializer.SerializeToUtf8Bytes(HostActions.GetStatus(), JsonOptions.Default);
            using Stream output = Console.OpenStandardOutput();
            output.Write(status);
            output.Flush();
            return 0;
        }

        return RunNativeHost();
    }

    private static int RunNativeHost()
    {
        using Stream input = Console.OpenStandardInput();
        using Stream output = Console.OpenStandardOutput();

        try
        {
            string? json = NativeMessaging.ReadMessage(input);
            if (json is null)
            {
                return 0;
            }

            HostRequest? request = JsonSerializer.Deserialize<HostRequest>(json, JsonOptions.Default);
            if (request is null)
            {
                throw new AppException("invalid_request", "Die Anfrage ist leer oder ungueltig.");
            }

            NativeMessaging.WriteMessage(output, HostActions.Handle(request));
            return 0;
        }
        catch (AppException ex)
        {
            NativeMessaging.WriteMessage(output, HostResponse.Failure(ex.Code, ex.Message));
            return 1;
        }
        catch (Exception ex)
        {
            NativeMessaging.WriteMessage(output, HostResponse.Failure(
                "unexpected_error",
                $"Der Windows-Helper konnte die Aktion nicht ausfuehren: {ex.Message}"));
            return 1;
        }
    }
}

internal static class HostActions
{
    public static HostResponse Handle(HostRequest request)
    {
        return request.Action switch
        {
            "getStatus" => GetStatus(),
            "configure" => Configure(),
            "openFolder" => OpenFolder(request),
            _ => throw new AppException("invalid_action", "Unbekannte Helper-Aktion.")
        };
    }

    public static HostResponse GetStatus()
    {
        bool configured = ConfigurationStore.TryLoad(out AppConfiguration? configuration, out _);
        bool credentialPresent = configured && configuration is not null &&
            CredentialStore.Exists(configuration.CredentialTarget);

        return new HostResponse(
            Ok: true,
            Code: null,
            Error: null,
            Configured: configured,
            ServerUrl: configured ? configuration!.ServerBaseUrl : null,
            Username: configured ? configuration!.Username : null,
            CredentialPresent: credentialPresent,
            WebClientRunning: WebClientService.IsRunning(),
            WebClientAutomatic: WebClientService.IsAutomatic());
    }

    private static HostResponse Configure()
    {
        using var form = new ConfigurationForm();
        form.ShowDialog();
        return GetStatus();
    }

    private static HostResponse OpenFolder(HostRequest request)
    {
        if (!ConfigurationStore.TryLoad(out AppConfiguration? configuration, out string? configurationError) ||
            configuration is null)
        {
            throw new AppException(
                "not_configured",
                configurationError ?? "Die Windows-Einrichtung fehlt. Oeffne die Einstellungen der Erweiterung.");
        }

        NextcloudAddress.ValidatePageUrl(configuration, request.PageUrl);

        if (!WebClientService.IsRunning())
        {
            throw new AppException(
                "webclient_not_running",
                "Der Windows-WebClient ist nicht gestartet. Oeffne die Einstellungen und waehle 'Einrichtung reparieren'.");
        }

        string uncPath = WebDavConnection.EnsureAccessible(configuration, request.FolderPath);
        ExplorerLauncher.Open(uncPath);

        return HostResponse.Success();
    }
}

internal sealed record HostRequest(
    [property: JsonPropertyName("action")] string? Action,
    [property: JsonPropertyName("webdavBaseUrl")] string? LegacyWebdavBaseUrl,
    [property: JsonPropertyName("folderPath")] string? FolderPath,
    [property: JsonPropertyName("itemPath")] string? ItemPath,
    [property: JsonPropertyName("itemType")] string? ItemType,
    [property: JsonPropertyName("pageUrl")] string? PageUrl);

internal sealed record HostResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("configured")] bool? Configured,
    [property: JsonPropertyName("serverUrl")] string? ServerUrl,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("credentialPresent")] bool? CredentialPresent,
    [property: JsonPropertyName("webClientRunning")] bool? WebClientRunning,
    [property: JsonPropertyName("webClientAutomatic")] bool? WebClientAutomatic)
{
    public static HostResponse Success() => new(true, null, null, null, null, null, null, null, null);

    public static HostResponse Failure(string code, string message) =>
        new(false, code, message, null, null, null, null, null, null);
}

internal sealed class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}

internal static class NativeMessaging
{
    private const int MaxMessageBytes = 1024 * 1024;

    public static string? ReadMessage(Stream input)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        if (!ReadExactly(input, lengthBytes))
        {
            return null;
        }

        uint length = BitConverter.ToUInt32(lengthBytes);
        if (length == 0)
        {
            return "";
        }

        if (length > MaxMessageBytes)
        {
            throw new AppException("message_too_large", "Die Native-Messaging-Nachricht ist zu gross.");
        }

        byte[] buffer = new byte[length];
        if (!ReadExactly(input, buffer))
        {
            throw new EndOfStreamException("Die Native-Messaging-Nachricht wurde unvollstaendig gelesen.");
        }

        return Encoding.UTF8.GetString(buffer);
    }

    public static void WriteMessage<T>(Stream output, T message)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions.Default);
        byte[] length = BitConverter.GetBytes((uint)payload.Length);
        output.Write(length, 0, length.Length);
        output.Write(payload, 0, payload.Length);
        output.Flush();
    }

    private static bool ReadExactly(Stream input, Span<byte> target)
    {
        int offset = 0;
        while (offset < target.Length)
        {
            int read = input.Read(target[offset..]);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}

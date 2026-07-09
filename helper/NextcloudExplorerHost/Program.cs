using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    return SelfTest.Run();
}

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
        throw new InvalidOperationException("Die Anfrage ist leer oder ungueltig.");
    }

    if (!string.Equals(request.Action, "openFolder", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Unbekannte Helper-Aktion.");
    }

    string uncPath = WebDavPathConverter.ToUncPath(request.WebdavBaseUrl, request.FolderPath);
    WebClientService.EnsureRunning();
    WebDavPathVerifier.EnsureAccessible(uncPath);
    ExplorerLauncher.Open(uncPath);

    NativeMessaging.WriteMessage(output, new HostResponse(true, uncPath, null));
    return 0;
}
catch (Exception ex)
{
    NativeMessaging.WriteMessage(output, new HostResponse(false, null, ex.Message));
    return 1;
}

internal sealed record HostRequest(
    [property: JsonPropertyName("action")] string? Action,
    [property: JsonPropertyName("webdavBaseUrl")] string? WebdavBaseUrl,
    [property: JsonPropertyName("folderPath")] string? FolderPath,
    [property: JsonPropertyName("itemPath")] string? ItemPath,
    [property: JsonPropertyName("itemType")] string? ItemType,
    [property: JsonPropertyName("pageUrl")] string? PageUrl);

internal sealed record HostResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("uncPath")] string? UncPath,
    [property: JsonPropertyName("error")] string? Error);

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

internal static class NativeMessaging
{
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

        if (length > 1024 * 1024)
        {
            throw new InvalidOperationException("Die Native-Messaging-Nachricht ist zu gross.");
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

internal static class WebDavPathConverter
{
    public static string ToUncPath(string? webdavBaseUrl, string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(webdavBaseUrl))
        {
            throw new InvalidOperationException("Die WebDAV-Basis-URL fehlt.");
        }

        if (!Uri.TryCreate(webdavBaseUrl.Trim(), UriKind.Absolute, out Uri? baseUri))
        {
            throw new InvalidOperationException("Die WebDAV-Basis-URL ist ungueltig.");
        }

        if (baseUri.Scheme is not ("https" or "http"))
        {
            throw new InvalidOperationException("Nur http(s)-WebDAV-URLs werden unterstuetzt.");
        }

        string combinedPath = CombinePaths(baseUri.AbsolutePath, NormalizeFolderPath(folderPath));
        string hostPart = BuildUncHostPart(baseUri);
        string windowsPath = ToWindowsPath(combinedPath);

        return string.IsNullOrEmpty(windowsPath)
            ? $@"\\{hostPart}\DavWWWRoot"
            : $@"\\{hostPart}\DavWWWRoot\{windowsPath}";
    }

    private static string BuildUncHostPart(Uri uri)
    {
        bool ssl = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        bool defaultPort = uri.IsDefaultPort || (ssl && uri.Port == 443) || (!ssl && uri.Port == 80);

        if (ssl && defaultPort)
        {
            return $"{uri.Host}@SSL";
        }

        if (ssl)
        {
            return $"{uri.Host}@SSL@{uri.Port}";
        }

        return defaultPort ? uri.Host : $"{uri.Host}@{uri.Port}";
    }

    private static string CombinePaths(string basePath, string folderPath)
    {
        string left = NormalizeFolderPath(basePath);
        string right = NormalizeFolderPath(folderPath);

        if (right == "/")
        {
            return left;
        }

        return $"{left.TrimEnd('/')}/{right.TrimStart('/')}";
    }

    private static string NormalizeFolderPath(string? value)
    {
        string path = string.IsNullOrWhiteSpace(value) ? "/" : value.Trim().Replace('\\', '/');
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        while (path.Contains("//", StringComparison.Ordinal))
        {
            path = path.Replace("//", "/", StringComparison.Ordinal);
        }

        if (path.Length > 1)
        {
            path = path.TrimEnd('/');
        }

        return path;
    }

    private static string ToWindowsPath(string urlPath)
    {
        string[] parts = NormalizeFolderPath(urlPath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .Where(part => part.Length > 0)
            .ToArray();

        return string.Join('\\', parts);
    }
}

internal static class WebClientService
{
    public static void EnsureRunning()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (IsRunning())
        {
            return;
        }

        RunSc("start WebClient");

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Thread.Sleep(250);
            if (IsRunning())
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "Der Windows-Dienst WebClient ist nicht gestartet. Starte PowerShell als Administrator und fuehre aus: Start-Service WebClient");
    }

    private static bool IsRunning()
    {
        string output = RunSc("query WebClient");
        return output.Contains("STATE", StringComparison.OrdinalIgnoreCase) &&
            output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }

    private static string RunSc(string arguments)
    {
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("sc.exe konnte nicht gestartet werden.");

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return output + Environment.NewLine + error;
    }
}

internal static class WebDavPathVerifier
{
    public static void EnsureAccessible(string uncPath)
    {
        try
        {
            if (!Directory.Exists(uncPath))
            {
                throw new InvalidOperationException(
                    "Der WebDAV-Ordner ist fuer Windows nicht erreichbar. Pruefe die Windows-Anmeldeinformationen fuer Nextcloud.");
            }

            using IEnumerator<string> enumerator = Directory.EnumerateFileSystemEntries(uncPath).GetEnumerator();
            _ = enumerator.MoveNext();
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "Zugriff verweigert. Windows braucht passende Nextcloud-WebDAV-Anmeldedaten. Verwende den Nextcloud-Benutzernamen und ein App-Passwort.");
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Der WebDAV-Ordner konnte nicht geoeffnet werden: {ex.Message}");
        }
    }
}

internal static class ExplorerLauncher
{
    public static void Open(string uncPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Der Helper kann explorer.exe nur unter Windows starten.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = uncPath,
            UseShellExecute = true
        });
    }
}

internal static class SelfTest
{
    public static int Run()
    {
        var cases = new Dictionary<string, string>
        {
            [
                WebDavPathConverter.ToUncPath(
                    "https://cloud.example.com/remote.php/dav/files/USERNAME/",
                    "/Dokumente/Rechnungen/")
            ] = @"\\cloud.example.com@SSL\DavWWWRoot\remote.php\dav\files\USERNAME\Dokumente\Rechnungen",
            [
                WebDavPathConverter.ToUncPath(
                    "https://cloud.example.test:8443/remote.php/dav/files/demo",
                    "A B/C%23D")
            ] = @"\\cloud.example.test@SSL@8443\DavWWWRoot\remote.php\dav\files\demo\A B\C#D"
        };

        foreach (KeyValuePair<string, string> testCase in cases)
        {
            if (!string.Equals(testCase.Key, testCase.Value, StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Self-test failed.");
                Console.Error.WriteLine($"Expected: {testCase.Value}");
                Console.Error.WriteLine($"Actual:   {testCase.Key}");
                return 1;
            }
        }

        Console.WriteLine("Self-test passed.");
        return 0;
    }
}

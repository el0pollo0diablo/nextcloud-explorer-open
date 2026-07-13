using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextcloudExplorerOpen;

internal sealed record AppConfiguration(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("serverBaseUrl")] string ServerBaseUrl,
    [property: JsonPropertyName("username")] string Username)
{
    public const int CurrentVersion = 1;

    [JsonIgnore]
    public Uri ServerUri => new(ServerBaseUrl, UriKind.Absolute);

    [JsonIgnore]
    public string CredentialTarget => AppIdentity.BuildCredentialTarget(ServerBaseUrl, Username);

    [JsonIgnore]
    public Uri WebDavUri => new(
        ServerUri,
        $"remote.php/dav/files/{Uri.EscapeDataString(Username)}/");

    public static AppConfiguration Create(string serverUrl, string username)
    {
        string normalizedUrl = NextcloudAddress.NormalizeServerBase(serverUrl);
        string normalizedUsername = username.Trim();

        if (normalizedUsername.Length is < 1 or > 128)
        {
            throw new AppException("invalid_username", "Der Nextcloud-Benutzername ist ungueltig.");
        }

        if (normalizedUsername.Any(char.IsControl) || normalizedUsername.Contains('/') || normalizedUsername.Contains('\\'))
        {
            throw new AppException("invalid_username", "Der Nextcloud-Benutzername enthaelt ungueltige Zeichen.");
        }

        return new AppConfiguration(CurrentVersion, normalizedUrl, normalizedUsername);
    }

    public void Validate()
    {
        if (Version != CurrentVersion)
        {
            throw new AppException("unsupported_config", "Die lokale Konfiguration hat eine nicht unterstuetzte Version.");
        }

        AppConfiguration normalized = Create(ServerBaseUrl, Username);
        if (!string.Equals(normalized.ServerBaseUrl, ServerBaseUrl, StringComparison.Ordinal) ||
            !string.Equals(normalized.Username, Username, StringComparison.Ordinal))
        {
            throw new AppException("invalid_config", "Die lokale Konfiguration ist ungueltig.");
        }
    }
}

internal static class AppIdentity
{
    public const string CredentialPrefix = "NextcloudExplorerOpen:";

    public static string BuildCredentialTarget(string serverBaseUrl, string username)
    {
        byte[] input = Encoding.UTF8.GetBytes(serverBaseUrl + "\n" + username);
        try
        {
            return CredentialPrefix + Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }
}

internal static class ConfigurationStore
{
    private const string FileName = "config.json";

    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NextcloudExplorerOpen");

    public static string FilePath => Path.Combine(DirectoryPath, FileName);

    public static bool TryLoad(out AppConfiguration? configuration, out string? error)
    {
        configuration = null;
        error = null;

        if (!File.Exists(FilePath))
        {
            error = "Die Windows-Einrichtung wurde noch nicht abgeschlossen.";
            return false;
        }

        try
        {
            string json = File.ReadAllText(FilePath, Encoding.UTF8);
            configuration = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions.Default);
            if (configuration is null)
            {
                throw new AppException("invalid_config", "Die lokale Konfiguration ist leer.");
            }

            configuration.Validate();
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or AppException)
        {
            configuration = null;
            error = $"Die lokale Konfiguration konnte nicht gelesen werden: {ex.Message}";
            return false;
        }
    }

    public static void Save(AppConfiguration configuration)
    {
        configuration.Validate();
        Directory.CreateDirectory(DirectoryPath);

        string temporaryPath = FilePath + ".tmp";
        string json = JsonSerializer.Serialize(configuration, JsonOptions.Default);

        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
            }

            File.Move(temporaryPath, FilePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static void DeleteUserData()
    {
        if (TryLoad(out AppConfiguration? configuration, out _) && configuration is not null)
        {
            WebDavConnection.Disconnect(configuration);
            CredentialStore.Delete(configuration.CredentialTarget);
        }

        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }

        if (Directory.Exists(DirectoryPath) && !Directory.EnumerateFileSystemEntries(DirectoryPath).Any())
        {
            Directory.Delete(DirectoryPath);
        }
    }
}

internal static class NextcloudAddress
{
    private static readonly string[] KnownRouteMarkers =
    [
        "/index.php/apps/files",
        "/apps/files",
        "/remote.php/dav/files/"
    ];

    public static string NormalizeServerBase(string rawUrl)
    {
        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out Uri? uri))
        {
            throw new AppException("invalid_server_url", "Bitte eine gueltige Nextcloud-Adresse eintragen.");
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException(
                "https_required",
                "Aus Sicherheitsgruenden werden nur verschluesselte HTTPS-Verbindungen unterstuetzt.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new AppException("invalid_server_url", "Die Nextcloud-Adresse ist ungueltig.");
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new AppException("invalid_server_url", "Die Nextcloud-Adresse darf keine Abfrage oder Sprungmarke enthalten.");
        }

        string path = uri.AbsolutePath.TrimEnd('/');
        foreach (string marker in KnownRouteMarkers)
        {
            int index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                path = path[..index];
                break;
            }
        }

        ValidateUrlPath(path);

        var builder = new UriBuilder(uri)
        {
            Scheme = Uri.UriSchemeHttps,
            Path = path.TrimEnd('/') + "/",
            Query = "",
            Fragment = "",
            UserName = "",
            Password = ""
        };

        return builder.Uri.AbsoluteUri;
    }

    public static void ValidatePageUrl(AppConfiguration configuration, string? pageUrl)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri? pageUri))
        {
            throw new AppException("invalid_page", "Die aktuelle Nextcloud-Seite konnte nicht sicher erkannt werden.");
        }

        Uri serverUri = configuration.ServerUri;
        bool sameOrigin = pageUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            pageUri.IdnHost.Equals(serverUri.IdnHost, StringComparison.OrdinalIgnoreCase) &&
            pageUri.Port == serverUri.Port &&
            string.IsNullOrEmpty(pageUri.UserInfo);

        if (!sameOrigin)
        {
            throw new AppException(
                "server_mismatch",
                "Die aktuelle Seite gehoert nicht zu der lokal eingerichteten Nextcloud-Adresse.");
        }

        string basePath = serverUri.AbsolutePath.TrimEnd('/');
        string pagePath = pageUri.AbsolutePath;
        string requiredPrefix = basePath + "/";
        if (!pagePath.StartsWith(requiredPrefix, StringComparison.Ordinal))
        {
            throw new AppException("server_mismatch", "Der Nextcloud-Unterordner stimmt nicht mit der Einrichtung ueberein.");
        }

        string relativePath = pagePath[requiredPrefix.Length..];
        bool filesRoute = IsRoute(relativePath, "index.php/apps/files") ||
            IsRoute(relativePath, "apps/files");
        if (!filesRoute)
        {
            throw new AppException("invalid_page", "Die Aktion ist nur innerhalb von Nextcloud Files erlaubt.");
        }
    }

    private static bool IsRoute(string path, string route)
    {
        return path.Equals(route, StringComparison.Ordinal) ||
            path.StartsWith(route + "/", StringComparison.Ordinal);
    }

    private static void ValidateUrlPath(string escapedPath)
    {
        foreach (string escapedSegment in escapedPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            string segment = Uri.UnescapeDataString(escapedSegment);
            if (segment is "." or ".." || segment.Contains('/') || segment.Contains('\\'))
            {
                throw new AppException("invalid_server_url", "Die Nextcloud-Adresse enthaelt einen ungueltigen Pfad.");
            }
        }
    }
}

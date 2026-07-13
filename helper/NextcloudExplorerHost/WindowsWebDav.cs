using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace NextcloudExplorerOpen;

internal static class WebDavPathConverter
{
    private static readonly char[] InvalidWindowsPathCharacters = ['<', '>', ':', '"', '|', '?', '*'];
    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string ToUncPath(AppConfiguration configuration, string? folderPath)
    {
        Uri webDavUri = configuration.WebDavUri;
        string hostPart = BuildUncHostPart(webDavUri);

        List<string> segments = DecodeUriSegments(webDavUri.AbsolutePath);
        segments.AddRange(ValidateFolderSegments(folderPath));

        return segments.Count == 0
            ? $@"\\{hostPart}\DavWWWRoot"
            : $@"\\{hostPart}\DavWWWRoot\{string.Join('\\', segments)}";
    }

    public static string ToUncRoot(AppConfiguration configuration) => ToUncPath(configuration, "/");

    private static string BuildUncHostPart(Uri uri)
    {
        bool defaultPort = uri.IsDefaultPort || uri.Port == 443;
        return defaultPort ? $"{uri.IdnHost}@SSL" : $"{uri.IdnHost}@SSL@{uri.Port}";
    }

    private static List<string> DecodeUriSegments(string escapedPath)
    {
        var segments = new List<string>();
        foreach (string escapedSegment in escapedPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            string segment = Uri.UnescapeDataString(escapedSegment);
            ValidateWindowsSegment(segment);
            segments.Add(segment);
        }

        return segments;
    }

    private static IEnumerable<string> ValidateFolderSegments(string? folderPath)
    {
        string path = string.IsNullOrWhiteSpace(folderPath) ? "/" : folderPath.Trim();
        if (path.Length > 4096 || path.Contains('\\') || path.Any(character => character == '\0'))
        {
            throw new AppException("invalid_folder_path", "Der Nextcloud-Ordnerpfad ist ungueltig.");
        }

        foreach (string segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            ValidateWindowsSegment(segment);
            yield return segment;
        }
    }

    private static void ValidateWindowsSegment(string segment)
    {
        if (segment.Length is < 1 or > 255 || segment is "." or ".." ||
            segment.Any(char.IsControl) || segment.IndexOfAny(InvalidWindowsPathCharacters) >= 0 ||
            segment.Contains('/') || segment.Contains('\\') ||
            segment.EndsWith(' ') || segment.EndsWith('.'))
        {
            throw new AppException(
                "unsupported_folder_name",
                "Der Ordnername enthaelt Zeichen, die Windows WebDAV nicht sicher oeffnen kann.");
        }

        string baseName = segment.Split('.')[0];
        if (ReservedWindowsNames.Contains(baseName))
        {
            throw new AppException(
                "unsupported_folder_name",
                "Der Ordnername ist unter Windows reserviert und kann nicht geoeffnet werden.");
        }
    }
}

internal static class WebDavConnection
{
    private const int ErrorSessionCredentialConflict = 1219;
    private const int ErrorNotConnected = 2250;

    public static string EnsureAccessible(AppConfiguration configuration, string? folderPath)
    {
        string root = WebDavPathConverter.ToUncRoot(configuration);
        string target = WebDavPathConverter.ToUncPath(configuration, folderPath);

        if (!WebClientService.IsRunning())
        {
            throw new AppException(
                "webclient_not_running",
                "Der Windows-WebClient ist nicht gestartet. Oeffne die Einstellungen und repariere die Einrichtung.");
        }

        if (!CanEnumerate(root))
        {
            using CredentialSecret secret = CredentialStore.Read(configuration.CredentialTarget);
            if (!string.Equals(secret.Username, configuration.Username, StringComparison.Ordinal))
            {
                throw new AppException("credential_invalid", "Der gespeicherte Benutzername stimmt nicht mit der Einrichtung ueberein.");
            }

            int result = Connect(root, configuration.Username, secret.PasswordPointer);
            if (result != 0 && result != ErrorSessionCredentialConflict)
            {
                throw CreateConnectionException(result);
            }

            if (!CanEnumerate(root))
            {
                if (result == ErrorSessionCredentialConflict)
                {
                    throw new AppException(
                        "credential_conflict",
                        "Windows verwendet bereits andere Zugangsdaten fuer diesen Server. Trenne die alte WebDAV-Verbindung und versuche es erneut.");
                }

                throw new AppException(
                    "webdav_unreachable",
                    "Windows konnte den Nextcloud-WebDAV-Ordner trotz gueltiger Einrichtung nicht erreichen.");
            }
        }

        if (!Directory.Exists(target))
        {
            throw new AppException("folder_not_found", "Der ausgewaehlte Nextcloud-Ordner wurde unter Windows nicht gefunden.");
        }

        return target;
    }

    public static void VerifyRoot(AppConfiguration configuration)
    {
        _ = EnsureAccessible(configuration, "/");
    }

    public static void Disconnect(AppConfiguration configuration)
    {
        string root = WebDavPathConverter.ToUncRoot(configuration);
        int result = WNetCancelConnection2(root, 0, true);
        if (result is 0 or ErrorNotConnected)
        {
            return;
        }

        // Credential removal must still proceed if Windows has already released
        // the transient WebDAV session under a provider-specific name.
    }

    private static bool CanEnumerate(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return false;
            }

            using IEnumerator<string> enumerator = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
            _ = enumerator.MoveNext();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static int Connect(string remotePath, string username, IntPtr passwordPointer)
    {
        var resource = new NativeNetworkResource
        {
            Type = 1,
            RemoteName = remotePath
        };

        return WNetAddConnection2(ref resource, passwordPointer, username, 0);
    }

    private static AppException CreateConnectionException(int error)
    {
        string details = new Win32Exception(error).Message;
        return error switch
        {
            5 or 86 or 1326 => new AppException(
                "authentication_failed",
                "Windows hat die Nextcloud-Anmeldung abgelehnt. Pruefe Benutzername und App-Passwort."),
            53 or 67 or 1203 => new AppException(
                "server_unreachable",
                "Der Nextcloud-WebDAV-Server ist fuer Windows nicht erreichbar."),
            _ => new AppException("webdav_connection_failed", $"Die Windows-WebDAV-Verbindung ist fehlgeschlagen: {details}")
        };
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeNetworkResource
    {
        public uint Scope;
        public uint Type;
        public uint DisplayType;
        public uint Usage;
        public string? LocalName;
        public string? RemoteName;
        public string? Comment;
        public string? Provider;
    }

    [DllImport("mpr.dll", EntryPoint = "WNetAddConnection2W", CharSet = CharSet.Unicode)]
    private static extern int WNetAddConnection2(
        ref NativeNetworkResource networkResource,
        IntPtr password,
        string username,
        uint flags);

    [DllImport("mpr.dll", EntryPoint = "WNetCancelConnection2W", CharSet = CharSet.Unicode)]
    private static extern int WNetCancelConnection2(string name, uint flags, bool force);
}

internal static class NextcloudCredentialVerifier
{
    public static async Task VerifyAsync(AppConfiguration configuration, string password, CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            Credentials = new NetworkCredential(configuration.Username, password),
            PreAuthenticate = true
        };

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), configuration.WebDavUri);
        request.Headers.TryAddWithoutValidation("Depth", "0");

        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        int status = (int)response.StatusCode;
        if (status is 200 or 207)
        {
            return;
        }

        if (status is 301 or 302 or 303 or 307 or 308)
        {
            throw new AppException(
                "redirect_rejected",
                "Die Nextcloud-Adresse leitet weiter. Trage die endgueltige HTTPS-Adresse ein.");
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new AppException(
                "authentication_failed",
                "Nextcloud hat Benutzername oder App-Passwort abgelehnt.");
        }

        throw new AppException(
            "webdav_test_failed",
            $"Nextcloud hat beim Verbindungstest mit HTTP-Status {status} geantwortet.");
    }
}

internal static class WebClientService
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceQueryConfig = 0x0001;
    private const uint ServiceQueryStatus = 0x0004;
    private const uint ServiceRunning = 0x00000004;
    private const uint ServiceAutoStart = 0x00000002;
    private const int ErrorInsufficientBuffer = 122;

    public static bool IsRunning()
    {
        return TryQuery(out NativeServiceStatus status, out _) && status.CurrentState == ServiceRunning;
    }

    public static bool IsAutomatic()
    {
        return TryQuery(out _, out uint startType) && startType == ServiceAutoStart;
    }

    public static void Repair()
    {
        string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string commandProcessor = Path.Combine(systemDirectory, "cmd.exe");
        string serviceControl = Path.Combine(systemDirectory, "sc.exe");
        string command = $"\"{serviceControl}\" config WebClient start= auto >nul && " +
            $"(\"{serviceControl}\" start WebClient >nul 2>&1 || \"{serviceControl}\" query WebClient >nul 2>&1)";

        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = commandProcessor,
            Arguments = $"/d /s /c \"{command}\"",
            WorkingDirectory = systemDirectory,
            Verb = "runas",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        }) ?? throw new AppException("service_repair_failed", "Die Windows-Dienstreparatur konnte nicht gestartet werden.");

        process.WaitForExit();
        if (process.ExitCode != 0 || !IsRunning() || !IsAutomatic())
        {
            throw new AppException(
                "service_repair_failed",
                "Der Windows-WebClient konnte nicht automatisch eingerichtet werden.");
        }
    }

    private static bool TryQuery(out NativeServiceStatus status, out uint startType)
    {
        status = default;
        startType = 0;

        IntPtr manager = OpenSCManager(null, null, ScManagerConnect);
        if (manager == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            IntPtr service = OpenService(manager, "WebClient", ServiceQueryStatus | ServiceQueryConfig);
            if (service == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                if (!QueryServiceStatus(service, out status))
                {
                    return false;
                }

                _ = QueryServiceConfig(service, IntPtr.Zero, 0, out uint bytesNeeded);
                if (bytesNeeded == 0 || Marshal.GetLastWin32Error() != ErrorInsufficientBuffer)
                {
                    return false;
                }

                IntPtr buffer = Marshal.AllocHGlobal(checked((int)bytesNeeded));
                try
                {
                    if (!QueryServiceConfig(service, buffer, bytesNeeded, out _))
                    {
                        return false;
                    }

                    NativeQueryServiceConfig configuration = Marshal.PtrToStructure<NativeQueryServiceConfig>(buffer);
                    startType = configuration.StartType;
                    return true;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeServiceStatus
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeQueryServiceConfig
    {
        public uint ServiceType;
        public uint StartType;
        public uint ErrorControl;
        public IntPtr BinaryPathName;
        public IntPtr LoadOrderGroup;
        public uint TagId;
        public IntPtr Dependencies;
        public IntPtr ServiceStartName;
        public IntPtr DisplayName;
    }

    [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr serviceManager, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatus(IntPtr service, out NativeServiceStatus serviceStatus);

    [DllImport("advapi32.dll", EntryPoint = "QueryServiceConfigW", SetLastError = true)]
    private static extern bool QueryServiceConfig(IntPtr service, IntPtr serviceConfig, uint bufferSize, out uint bytesNeeded);

    [DllImport("advapi32.dll")]
    private static extern bool CloseServiceHandle(IntPtr serviceHandle);
}

internal static class ExplorerLauncher
{
    public static void Open(string uncPath)
    {
        if (!uncPath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            throw new AppException("unsafe_path", "Der Windows-Pfad wurde aus Sicherheitsgruenden abgelehnt.");
        }

        _ = Process.Start(new ProcessStartInfo
        {
            FileName = uncPath,
            UseShellExecute = true
        });
    }
}

using System.Text;
using System.Text.Json;

namespace NextcloudExplorerOpen;

internal static class SelfTest
{
    public static int Run(Stream output)
    {
        using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };

        try
        {
            AppConfiguration rootConfiguration = AppConfiguration.Create(
                "https://cloud.example.com/index.php/apps/files/",
                "demo");

            AssertEqual("https://cloud.example.com/", rootConfiguration.ServerBaseUrl, "server normalization");
            AssertEqual(
                @"\\cloud.example.com@SSL\DavWWWRoot\remote.php\dav\files\demo\Dokumente\Rechnungen",
                WebDavPathConverter.ToUncPath(rootConfiguration, "/Dokumente/Rechnungen"),
                "UNC conversion");

            AppConfiguration subPathConfiguration = AppConfiguration.Create(
                "https://cloud.example.com/nextcloud/apps/files/",
                "demo user");

            AssertEqual("https://cloud.example.com/nextcloud/", subPathConfiguration.ServerBaseUrl, "subpath normalization");
            AssertEqual(
                @"\\cloud.example.com@SSL\DavWWWRoot\nextcloud\remote.php\dav\files\demo user\A B\C#D",
                WebDavPathConverter.ToUncPath(subPathConfiguration, "/A B/C#D"),
                "encoded path conversion");

            NextcloudAddress.ValidatePageUrl(
                subPathConfiguration,
                "https://cloud.example.com/nextcloud/index.php/apps/files/?dir=/Dokumente");

            ExpectAppException(
                () => AppConfiguration.Create("http://cloud.example.com/", "demo"),
                "https_required");
            ExpectAppException(
                () => AppConfiguration.Create("https://demo:secret@cloud.example.com/", "demo"),
                "invalid_server_url");
            ExpectAppException(
                () => NextcloudAddress.ValidatePageUrl(
                    rootConfiguration,
                    "https://cloud.example.com.evil.test/index.php/apps/files/"),
                "server_mismatch");
            ExpectAppException(
                () => WebDavPathConverter.ToUncPath(rootConfiguration, "/Dokumente/../Privat"),
                "unsupported_folder_name");
            ExpectAppException(
                () => WebDavPathConverter.ToUncPath(rootConfiguration, "/Dokumente\\Privat"),
                "invalid_folder_path");

            string target = rootConfiguration.CredentialTarget;
            Assert(target.StartsWith(AppIdentity.CredentialPrefix, StringComparison.Ordinal), "credential target prefix");
            Assert(!target.Contains("cloud.example.com", StringComparison.OrdinalIgnoreCase), "credential target hides server name");
            Assert(!target.Contains("demo", StringComparison.OrdinalIgnoreCase), "credential target hides username");

            string serialized = JsonSerializer.Serialize(rootConfiguration, JsonOptions.Default);
            Assert(!serialized.Contains("password", StringComparison.OrdinalIgnoreCase), "configuration contains no password");

            using var nativeMessage = new MemoryStream();
            NativeMessaging.WriteMessage(nativeMessage, HostResponse.Success());
            nativeMessage.Position = 0;
            string? roundTrip = NativeMessaging.ReadMessage(nativeMessage);
            Assert(roundTrip is not null && roundTrip.Contains("\"ok\": true", StringComparison.Ordinal), "native messaging framing");

            writer.WriteLine("Self-test passed.");
            return 0;
        }
        catch (Exception ex)
        {
            writer.WriteLine($"Self-test failed: {ex.Message}");
            return 1;
        }
    }

    private static void ExpectAppException(Action action, string code)
    {
        try
        {
            action();
        }
        catch (AppException ex) when (ex.Code == code)
        {
            return;
        }

        throw new InvalidOperationException($"Expected AppException '{code}'.");
    }

    private static void AssertEqual(string expected, string actual, string name)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{name}: expected '{expected}', got '{actual}'.");
        }
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException(name);
        }
    }
}

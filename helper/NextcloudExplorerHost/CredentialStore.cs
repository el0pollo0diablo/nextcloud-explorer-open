using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace NextcloudExplorerOpen;

internal static class CredentialStore
{
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int MaxSecretBytes = 1024;

    public static bool Exists(string target)
    {
        if (CredRead(target, CredTypeGeneric, 0, out IntPtr pointer))
        {
            CredFree(pointer);
            return true;
        }

        int error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound)
        {
            return false;
        }

        throw new Win32Exception(error, "Der Windows-Anmeldeinformationsspeicher konnte nicht gelesen werden.");
    }

    public static CredentialSecret Read(string target)
    {
        if (!CredRead(target, CredTypeGeneric, 0, out IntPtr pointer))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                throw new AppException(
                    "credential_missing",
                    "Das App-Passwort fehlt. Oeffne die Einstellungen und fuehre die Einrichtung erneut aus.");
            }

            throw new Win32Exception(error, "Das App-Passwort konnte nicht aus dem Windows-Anmeldeinformationsspeicher gelesen werden.");
        }

        try
        {
            NativeCredential credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            if (credential.CredentialBlobSize == 0 || credential.CredentialBlobSize > MaxSecretBytes ||
                credential.CredentialBlobSize % 2 != 0)
            {
                throw new AppException("credential_invalid", "Das gespeicherte App-Passwort ist ungueltig.");
            }

            string username = Marshal.PtrToStringUni(credential.UserName) ?? "";
            return CredentialSecret.FromNativeBlob(
                username,
                credential.CredentialBlob,
                checked((int)credential.CredentialBlobSize));
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public static void Write(string target, string username, string password)
    {
        using CredentialSecret secret = CredentialSecret.FromPassword(username, password);
        Write(target, secret);
    }

    public static void Write(string target, CredentialSecret secret)
    {
        IntPtr targetPointer = IntPtr.Zero;
        IntPtr usernamePointer = IntPtr.Zero;

        try
        {
            targetPointer = Marshal.StringToCoTaskMemUni(target);
            usernamePointer = Marshal.StringToCoTaskMemUni(secret.Username);

            var credential = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = targetPointer,
                CredentialBlobSize = checked((uint)secret.SecretByteCount),
                CredentialBlob = secret.PasswordPointer,
                Persist = CredPersistLocalMachine,
                UserName = usernamePointer
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Das App-Passwort konnte nicht im Windows-Anmeldeinformationsspeicher gespeichert werden.");
            }
        }
        finally
        {
            if (targetPointer != IntPtr.Zero)
            {
                Marshal.ZeroFreeCoTaskMemUnicode(targetPointer);
            }

            if (usernamePointer != IntPtr.Zero)
            {
                Marshal.ZeroFreeCoTaskMemUnicode(usernamePointer);
            }
        }
    }

    public static void Delete(string target)
    {
        if (CredDelete(target, CredTypeGeneric, 0))
        {
            return;
        }

        int error = Marshal.GetLastWin32Error();
        if (error != ErrorNotFound)
        {
            throw new Win32Exception(error, "Das gespeicherte App-Passwort konnte nicht entfernt werden.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credentialPointer);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}

internal sealed class CredentialSecret : IDisposable
{
    private int _bufferByteCount;

    private CredentialSecret(string username, IntPtr passwordPointer, int secretByteCount, int bufferByteCount)
    {
        Username = username;
        PasswordPointer = passwordPointer;
        SecretByteCount = secretByteCount;
        _bufferByteCount = bufferByteCount;
    }

    public string Username { get; }

    public IntPtr PasswordPointer { get; private set; }

    public int SecretByteCount { get; }

    ~CredentialSecret()
    {
        Dispose();
    }

    public static CredentialSecret FromPassword(string username, string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 512 || password.Any(char.IsControl))
        {
            throw new AppException("invalid_password", "Das App-Passwort ist leer oder ungueltig.");
        }

        byte[] bytes = Encoding.Unicode.GetBytes(password);
        try
        {
            return FromBytes(username, bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public static CredentialSecret FromNativeBlob(string username, IntPtr source, int byteCount)
    {
        byte[] bytes = new byte[byteCount];
        try
        {
            Marshal.Copy(source, bytes, 0, byteCount);
            return FromBytes(username, bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static CredentialSecret FromBytes(string username, byte[] bytes)
    {
        int bufferByteCount = checked(bytes.Length + 2);
        IntPtr pointer = Marshal.AllocHGlobal(bufferByteCount);

        try
        {
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            Marshal.WriteInt16(pointer, bytes.Length, 0);
            return new CredentialSecret(username, pointer, bytes.Length, bufferByteCount);
        }
        catch
        {
            ZeroAndFree(pointer, bufferByteCount);
            throw;
        }
    }

    public void Dispose()
    {
        if (PasswordPointer == IntPtr.Zero)
        {
            return;
        }

        ZeroAndFree(PasswordPointer, _bufferByteCount);
        PasswordPointer = IntPtr.Zero;
        _bufferByteCount = 0;
        GC.SuppressFinalize(this);
    }

    private static void ZeroAndFree(IntPtr pointer, int byteCount)
    {
        if (pointer == IntPtr.Zero)
        {
            return;
        }

        for (int index = 0; index < byteCount; index++)
        {
            Marshal.WriteByte(pointer, index, 0);
        }
        Marshal.FreeHGlobal(pointer);
    }
}

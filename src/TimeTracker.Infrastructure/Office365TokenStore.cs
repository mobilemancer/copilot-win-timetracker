using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using TimeTracker.Domain;

namespace TimeTracker.Infrastructure;

[SupportedOSPlatform("windows")]
public sealed class Office365TokenStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly AppDataPaths _paths;

    public Office365TokenStore(AppDataPaths paths)
    {
        _paths = paths;
    }

    public Office365TokenRecord? Load(Office365AccountSettings account)
    {
        var path = GetPath(account);
        if (!File.Exists(path))
        {
            return null;
        }

        var protectedBytes = File.ReadAllBytes(path);
        var bytes = WindowsDataProtector.Unprotect(protectedBytes);
        return JsonSerializer.Deserialize<Office365TokenRecord>(bytes, JsonOptions);
    }

    public void Save(Office365AccountSettings account, Office365TokenRecord record)
    {
        Directory.CreateDirectory(_paths.TokenCacheDirectory);
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
        var protectedBytes = WindowsDataProtector.Protect(jsonBytes);
        File.WriteAllBytes(GetPath(account), protectedBytes);
    }

    private string GetPath(Office365AccountSettings account)
    {
        Directory.CreateDirectory(_paths.TokenCacheDirectory);
        var rawKey = $"{account.TenantId}\u001F{account.ClientId}\u001F{account.DisplayName}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
        return Path.Combine(_paths.TokenCacheDirectory, $"{hash}.json.bin");
    }
}

internal static class WindowsDataProtector
{
    public static byte[] Protect(byte[] data)
    {
        return Transform(data, protect: true);
    }

    public static byte[] Unprotect(byte[] data)
    {
        return Transform(data, protect: false);
    }

    private static byte[] Transform(byte[] data, bool protect)
    {
        if (data.Length == 0)
        {
            return [];
        }

        var input = new DataBlob();
        var output = new DataBlob();

        try
        {
            input.pbData = Marshal.AllocHGlobal(data.Length);
            input.cbData = data.Length;
            Marshal.Copy(data, 0, input.pbData, data.Length);

            var success = protect
                ? CryptProtectData(ref input, string.Empty, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref output);

            if (!success)
            {
                throw new InvalidOperationException("Windows data protection operation failed.");
            }

            var bytes = new byte[output.cbData];
            Marshal.Copy(output.pbData, bytes, 0, output.cbData);
            return bytes;
        }
        finally
        {
            if (input.pbData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(input.pbData);
            }

            if (output.pbData != IntPtr.Zero)
            {
                LocalFree(output.pbData);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn,
        string szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DataBlob pDataOut);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn,
        IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DataBlob pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);
}

public sealed class Office365TokenRecord
{
    public string AccessToken { get; set; } = string.Empty;

    public string? RefreshToken { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}

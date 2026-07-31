using System.Security.Cryptography;
using System.Text;
using Microsoft.JSInterop;

namespace WebApp.Services;

/// <summary>
/// Encrypts/decrypts the master password for storage in the browser's localStorage,
/// using a random per-device AES key that never leaves the device.
/// </summary>
public class MasterPasswordProtector
{
    private const string DeviceKeyStorageKey = "pwdgen.deviceKey";
    private const string MasterPasswordStorageKey = "pwdgen.masterPassword";

    private readonly IJSRuntime _jsRuntime;

    public MasterPasswordProtector(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task SaveAsync(string plainTextMasterPassword)
    {
        var key = await GetOrCreateDeviceKeyAsync();

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainTextMasterPassword);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var payload = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, aes.IV.Length, cipherBytes.Length);

        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", MasterPasswordStorageKey, Convert.ToBase64String(payload));
    }

    public async Task<string?> LoadAsync()
    {
        var stored = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", MasterPasswordStorageKey);
        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }

        try
        {
            var payload = Convert.FromBase64String(stored);
            var key = await GetOrCreateDeviceKeyAsync();

            using var aes = Aes.Create();
            aes.Key = key;

            var iv = new byte[16];
            var cipherBytes = new byte[payload.Length - iv.Length];
            Buffer.BlockCopy(payload, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(payload, iv.Length, cipherBytes, 0, cipherBytes.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            // Corrupted or tampered storage entry - treat as if nothing was saved.
            await ClearAsync();
            return null;
        }
    }

    public async Task ClearAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", MasterPasswordStorageKey);
    }

    private async Task<byte[]> GetOrCreateDeviceKeyAsync()
    {
        var stored = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", DeviceKeyStorageKey);
        if (!string.IsNullOrEmpty(stored))
        {
            return Convert.FromBase64String(stored);
        }

        var key = RandomNumberGenerator.GetBytes(32);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", DeviceKeyStorageKey, Convert.ToBase64String(key));
        return key;
    }
}

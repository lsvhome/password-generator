using Blazor.SubtleCrypto;
using Microsoft.JSInterop;
using WebApp.Services;

namespace WebApp.Tests;

/// <summary>
/// In-memory stand-in for the browser's localStorage, implementing just enough of
/// IJSRuntime to exercise MasterPasswordProtector without a real JS environment.
/// </summary>
public class FakeLocalStorageJsRuntime : IJSRuntime
{
    private readonly Dictionary<string, string> _store = new();

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        switch (identifier)
        {
            case "localStorage.getItem":
                {
                    var key = (string)args![0]!;
                    _store.TryGetValue(key, out var value);
                    return ValueTask.FromResult((TValue)(object?)value!);
                }
            case "localStorage.setItem":
                {
                    var key = (string)args![0]!;
                    var value = (string)args![1]!;
                    _store[key] = value;
                    return ValueTask.FromResult(default(TValue)!);
                }
            case "localStorage.removeItem":
                {
                    var key = (string)args![0]!;
                    _store.Remove(key);
                    return ValueTask.FromResult(default(TValue)!);
                }
            default:
                throw new NotSupportedException($"Unexpected JS invocation: {identifier}");
        }
    }

    public void SetRawValue(string key, string value) => _store[key] = value;

    public bool TryGetRawValue(string key, out string? value) => _store.TryGetValue(key, out value);
}

/// <summary>
/// Stand-in for Blazor.SubtleCrypto's ICryptoService (which itself relies on JSInterop
/// into the browser's Web Crypto API, unavailable in a plain xUnit test host). Mimics the
/// "dynamic key" behavior described in the library's docs: every EncryptAsync call gets a
/// fresh random key, and DecryptAsync(CryptoInput) only succeeds when given back the exact
/// matching key and untampered ciphertext.
/// </summary>
public class FakeCryptoService : ICryptoService
{
    public Task<CryptoResult> EncryptAsync(string text)
    {
        var key = Guid.NewGuid().ToString("N");
        var ciphertext = Encode(text, key);
        return Task.FromResult(new CryptoResult
        {
            Status = true,
            Origin = text,
            Value = ciphertext,
            Secret = new Secret { Key = key, IV = "fake-iv" }
        });
    }

    public Task<string> DecryptAsync(CryptoInput input)
    {
        var plainText = Decode(input.Value, input.Key);
        return Task.FromResult(plainText);
    }

    private static string Encode(string plainText, string key)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(key + ":" + plainText));

    private static string Decode(string ciphertext, string key)
    {
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(ciphertext));
        var prefix = key + ":";
        if (!decoded.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Key does not match ciphertext.");
        }

        return decoded[prefix.Length..];
    }

    public Task<CryptoResult> EncryptAsync(object obj) => throw new NotSupportedException();
    public Task<List<CryptoResult>> EncryptListAsync(List<string> list) => throw new NotSupportedException();
    public Task<List<CryptoResult>> EncryptListAsync<T>(List<T> list) => throw new NotSupportedException();
    public Task<string> DecryptAsync(string text) => throw new NotSupportedException();
    public Task<T> DecryptAsync<T>(string text) => throw new NotSupportedException();
    public Task<T> DecryptAsync<T>(CryptoInput input) => throw new NotSupportedException();
    public Task<List<string>> DecryptListAsync(List<string> list) => throw new NotSupportedException();
    public Task<List<T>> DecryptListAsync<T>(List<string> list) => throw new NotSupportedException();
    public Task<List<string>> DecryptListAsync(List<CryptoInput> list) => throw new NotSupportedException();
    public Task<List<T>> DecryptListAsync<T>(List<CryptoInput> list) => throw new NotSupportedException();
}

public class MasterPasswordProtectorTests
{
    private static (MasterPasswordProtector Sut, FakeLocalStorageJsRuntime JsRuntime) CreateSut()
    {
        var jsRuntime = new FakeLocalStorageJsRuntime();
        var sut = new MasterPasswordProtector(new FakeCryptoService(), jsRuntime);
        return (sut, jsRuntime);
    }

    [Fact]
    public async Task LoadAsync_WhenNothingStored_ReturnsNull()
    {
        var (sut, _) = CreateSut();

        var result = await sut.LoadAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsThePlainTextMasterPassword()
    {
        var (sut, _) = CreateSut();

        await sut.SaveAsync("correct-horse-battery-staple");
        var result = await sut.LoadAsync();

        Assert.Equal("correct-horse-battery-staple", result);
    }

    [Fact]
    public async Task SaveAsync_StoresCiphertextAndKeySeparately()
    {
        var (sut, jsRuntime) = CreateSut();

        await sut.SaveAsync("my-password");

        Assert.True(jsRuntime.TryGetRawValue("pwdgen.masterPassword", out var ciphertext));
        Assert.True(jsRuntime.TryGetRawValue("pwdgen.masterPasswordKey", out var key));
        Assert.False(string.IsNullOrEmpty(ciphertext));
        Assert.False(string.IsNullOrEmpty(key));
    }

    [Fact]
    public async Task SaveAsync_GeneratesAFreshKeyOnEverySave()
    {
        var (sut, jsRuntime) = CreateSut();

        await sut.SaveAsync("first-password");
        jsRuntime.TryGetRawValue("pwdgen.masterPasswordKey", out var keyAfterFirstSave);

        await sut.SaveAsync("second-password");
        jsRuntime.TryGetRawValue("pwdgen.masterPasswordKey", out var keyAfterSecondSave);

        Assert.NotEqual(keyAfterFirstSave, keyAfterSecondSave);

        var result = await sut.LoadAsync();
        Assert.Equal("second-password", result);
    }

    [Fact]
    public async Task ClearAsync_RemovesStoredValues_SoSubsequentLoadReturnsNull()
    {
        var (sut, _) = CreateSut();

        await sut.SaveAsync("to-be-cleared");
        await sut.ClearAsync();
        var result = await sut.LoadAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_WithOnlyCiphertextStored_ReturnsNull()
    {
        var (sut, jsRuntime) = CreateSut();
        jsRuntime.SetRawValue("pwdgen.masterPassword", "some-ciphertext");

        var result = await sut.LoadAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_WithOnlyKeyStored_ReturnsNull()
    {
        var (sut, jsRuntime) = CreateSut();
        jsRuntime.SetRawValue("pwdgen.masterPasswordKey", "some-key");

        var result = await sut.LoadAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_WithCorruptedCiphertext_ReturnsNullAndClearsStorage()
    {
        var (sut, jsRuntime) = CreateSut();

        await sut.SaveAsync("tamper-me");
        jsRuntime.SetRawValue("pwdgen.masterPassword", "not-valid-base64!!!");

        var result = await sut.LoadAsync();

        Assert.Null(result);
        Assert.False(jsRuntime.TryGetRawValue("pwdgen.masterPassword", out _));
        Assert.False(jsRuntime.TryGetRawValue("pwdgen.masterPasswordKey", out _));
    }

    [Fact]
    public async Task LoadAsync_WithMismatchedKey_ReturnsNullAndClearsStorage()
    {
        var (sut, jsRuntime) = CreateSut();

        await sut.SaveAsync("wrong-key-scenario");
        jsRuntime.SetRawValue("pwdgen.masterPasswordKey", "not-the-real-key");

        var result = await sut.LoadAsync();

        Assert.Null(result);
        Assert.False(jsRuntime.TryGetRawValue("pwdgen.masterPassword", out _));
        Assert.False(jsRuntime.TryGetRawValue("pwdgen.masterPasswordKey", out _));
    }
}

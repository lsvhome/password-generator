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

public class MasterPasswordProtectorTests
{
    [Fact]
    public async Task LoadAsync_WhenNothingStored_ReturnsNull()
    {
        var jsRuntime = new FakeLocalStorageJsRuntime();
        var sut = new MasterPasswordProtector(jsRuntime);

        var result = await sut.LoadAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsThePlainTextMasterPassword()
    {
        var jsRuntime = new FakeLocalStorageJsRuntime();
        var sut = new MasterPasswordProtector(jsRuntime);

        await sut.SaveAsync("correct-horse-battery-staple");
        var result = await sut.LoadAsync();

        Assert.Equal("correct-horse-battery-staple", result);
    }

    [Fact]
    public async Task SaveAsync_ReusesTheSameDeviceKeyAcrossMultipleCalls()
    {
        var jsRuntime = new FakeLocalStorageJsRuntime();
        var sut = new MasterPasswordProtector(jsRuntime);

        await sut.SaveAsync("first-password");
        jsRuntime.TryGetRawValue("pwdgen.deviceKey", out var deviceKeyAfterFirstSave);

        await sut.SaveAsync("second-password");
        jsRuntime.TryGetRawValue("pwdgen.deviceKey", out var deviceKeyAfterSecondSave);

        Assert.NotNull(deviceKeyAfterFirstSave);
        Assert.Equal(deviceKeyAfterFirstSave, deviceKeyAfterSecondSave);

        var result = await sut.LoadAsync();
        Assert.Equal("second-password", result);
    }

    [Fact]
    public async Task ClearAsync_RemovesStoredValue_SoSubsequentLoadReturnsNull()
    {
        var jsRuntime = new FakeLocalStorageJsRuntime();
        var sut = new MasterPasswordProtector(jsRuntime);

        await sut.SaveAsync("to-be-cleared");
        await sut.ClearAsync();
        var result = await sut.LoadAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_WithInvalidBase64Payload_ReturnsNullAndClearsStorage()
    {
        var jsRuntime = new FakeLocalStorageJsRuntime();
        var sut = new MasterPasswordProtector(jsRuntime);
        jsRuntime.SetRawValue("pwdgen.masterPassword", "not-valid-base64!!!");

        var result = await sut.LoadAsync();

        Assert.Null(result);
        Assert.False(jsRuntime.TryGetRawValue("pwdgen.masterPassword", out _));
    }

    [Fact]
    public async Task LoadAsync_WithTamperedCiphertext_ReturnsNullAndClearsStorage()
    {
        var jsRuntime = new FakeLocalStorageJsRuntime();
        var sut = new MasterPasswordProtector(jsRuntime);

        await sut.SaveAsync("tamper-me");
        jsRuntime.TryGetRawValue("pwdgen.masterPassword", out var storedPayload);
        var tamperedBytes = Convert.FromBase64String(storedPayload!);
        // Flip a byte inside the ciphertext (after the 16-byte IV) so decryption fails padding validation.
        tamperedBytes[^1] ^= 0xFF;
        jsRuntime.SetRawValue("pwdgen.masterPassword", Convert.ToBase64String(tamperedBytes));

        var result = await sut.LoadAsync();

        Assert.Null(result);
        Assert.False(jsRuntime.TryGetRawValue("pwdgen.masterPassword", out _));
    }
}

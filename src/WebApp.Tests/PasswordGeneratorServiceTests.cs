using WebApp.Services;

namespace WebApp.Tests;

public class PasswordGeneratorServiceNormalizeHostnameTests
{
    private readonly PasswordGeneratorService _sut = new();

    [Theory]
    [InlineData("example.com", "example.com")]
    [InlineData("http://example.com", "example.com")]
    [InlineData("https://example.com", "example.com")]
    [InlineData("https://example.com/path", "example.com")]
    [InlineData("https://example.com/path?query=1", "example.com")]
    [InlineData("https://example.com/path?query=1#fragment", "example.com")]
    [InlineData("https://example.com:8080", "example.com")]
    [InlineData("example.com:8080", "example.com")]
    [InlineData("HTTPS://EXAMPLE.COM", "example.com")]
    [InlineData("  example.com  ", "example.com")]
    [InlineData("www.example.com", "www.example.com")]
    public void NormalizeHostname_ReturnsExpectedHostname(string input, string expected)
    {
        var result = _sut.NormalizeHostname(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeHostname_EmptyOrWhitespaceInput_ReturnsEmptyString(string? input)
    {
        var result = _sut.NormalizeHostname(input);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void NormalizeHostname_MalformedInput_ReturnsEmptyString()
    {
        var result = _sut.NormalizeHostname("://not-a-valid-url");

        Assert.Equal(string.Empty, result);
    }
}

public class PasswordGeneratorServiceGeneratePasswordTests
{
    private readonly PasswordGeneratorService _sut = new();
    private const PasswordCharsetOptions AllCharsets =
        PasswordCharsetOptions.Lowercase | PasswordCharsetOptions.Digits | PasswordCharsetOptions.Uppercase | PasswordCharsetOptions.Symbols;

    [Theory]
    [InlineData(null, "example.com")]
    [InlineData("", "example.com")]
    [InlineData("master", null)]
    [InlineData("master", "")]
    public void GeneratePassword_EmptyMasterPasswordOrHostname_ReturnsEmptyString(string? masterPassword, string? hostname)
    {
        var result = _sut.GeneratePassword(masterPassword!, hostname!, PasswordHashAlgorithm.SHA256, 16, AllCharsets);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GeneratePassword_NoCharsetOptions_ReturnsEmptyString()
    {
        var result = _sut.GeneratePassword("master", "example.com", PasswordHashAlgorithm.SHA256, 16, PasswordCharsetOptions.None);

        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(16, 16)]
    [InlineData(32, 32)]
    [InlineData(1, 4)]
    [InlineData(0, 4)]
    [InlineData(-5, 4)]
    [InlineData(64, 32)]
    [InlineData(1000, 32)]
    public void GeneratePassword_LengthIsClampedBetween4And32(int requestedLength, int expectedLength)
    {
        var result = _sut.GeneratePassword("master", "example.com", PasswordHashAlgorithm.SHA256, requestedLength, AllCharsets);

        Assert.Equal(expectedLength, result.Length);
    }

    [Fact]
    public void GeneratePassword_SameInputs_ProducesIdenticalOutput()
    {
        var first = _sut.GeneratePassword("master", "example.com", PasswordHashAlgorithm.SHA256, 16, AllCharsets);
        var second = new PasswordGeneratorService().GeneratePassword("master", "example.com", PasswordHashAlgorithm.SHA256, 16, AllCharsets);

        Assert.Equal(first, second);
    }

    [Fact]
    public void GeneratePassword_DifferentMasterPassword_ProducesDifferentOutput()
    {
        var first = _sut.GeneratePassword("master1", "example.com", PasswordHashAlgorithm.SHA256, 16, AllCharsets);
        var second = _sut.GeneratePassword("master2", "example.com", PasswordHashAlgorithm.SHA256, 16, AllCharsets);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GeneratePassword_DifferentHostname_ProducesDifferentOutput()
    {
        var first = _sut.GeneratePassword("master", "example.com", PasswordHashAlgorithm.SHA256, 16, AllCharsets);
        var second = _sut.GeneratePassword("master", "other.com", PasswordHashAlgorithm.SHA256, 16, AllCharsets);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GeneratePassword_DifferentAlgorithm_ProducesDifferentOutput()
    {
        var first = _sut.GeneratePassword("master", "example.com", PasswordHashAlgorithm.SHA256, 16, AllCharsets);
        var second = _sut.GeneratePassword("master", "example.com", PasswordHashAlgorithm.SHA512, 16, AllCharsets);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GeneratePassword_DifferentCharsetOptions_ProducesDifferentOutput()
    {
        var first = _sut.GeneratePassword("master", "example.com", PasswordHashAlgorithm.SHA256, 16, PasswordCharsetOptions.Lowercase);
        var second = _sut.GeneratePassword("master", "example.com", PasswordHashAlgorithm.SHA256, 16, PasswordCharsetOptions.Digits);

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(PasswordCharsetOptions.Lowercase, "abcdefghijklmnopqrstuvwxyz")]
    [InlineData(PasswordCharsetOptions.Digits, "0123456789")]
    [InlineData(PasswordCharsetOptions.Uppercase, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")]
    [InlineData(PasswordCharsetOptions.Symbols, "!@#$%^&*()-_=+[]{}")]
    [InlineData(PasswordCharsetOptions.Lowercase | PasswordCharsetOptions.Digits, "abcdefghijklmnopqrstuvwxyz0123456789")]
    [InlineData(AllCharsets, "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()-_=+[]{}")]
    public void GeneratePassword_OnlyUsesCharactersFromSelectedCharset(PasswordCharsetOptions options, string allowedCharset)
    {
        var result = _sut.GeneratePassword("master", "example.com", PasswordHashAlgorithm.SHA256, 32, options);

        Assert.Equal(32, result.Length);
        Assert.All(result, c => Assert.Contains(c, allowedCharset));
    }

    [Theory]
    [InlineData(PasswordHashAlgorithm.MD5)]
    [InlineData(PasswordHashAlgorithm.SHA1)]
    [InlineData(PasswordHashAlgorithm.SHA256)]
    [InlineData(PasswordHashAlgorithm.SHA384)]
    [InlineData(PasswordHashAlgorithm.SHA512)]
    public void GeneratePassword_SupportedAlgorithms_ProduceDeterministicOutputOfRequestedLength(PasswordHashAlgorithm algorithm)
    {
        var first = _sut.GeneratePassword("master", "example.com", algorithm, 20, AllCharsets);
        var second = _sut.GeneratePassword("master", "example.com", algorithm, 20, AllCharsets);

        Assert.Equal(20, first.Length);
        Assert.Equal(first, second);
    }

    [Fact]
    public void GeneratePassword_UnsupportedAlgorithm_ThrowsArgumentOutOfRangeException()
    {
        var invalidAlgorithm = (PasswordHashAlgorithm)999;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _sut.GeneratePassword("master", "example.com", invalidAlgorithm, 16, AllCharsets));
    }
}

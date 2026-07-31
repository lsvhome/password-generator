using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Pages;
using WebApp.Services;

namespace WebApp.Tests;

/// <summary>
/// Component tests for the Home page, driving the master password / site URL inputs
/// and asserting on the derived hostname and generated password.
/// bUnit's default (Loose) JSInterop stub makes MasterPasswordProtector's localStorage
/// calls no-ops, so no saved master password is loaded on initialization.
/// </summary>
public class HomeComponentTests : BunitContext
{
    public HomeComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new PasswordGeneratorService());
        Services.AddSingleton(sp => new MasterPasswordProtector(new FakeCryptoService(), JSInterop.JSRuntime));
    }

    [Fact]
    public void Initially_GeneratedPasswordIsEmpty()
    {
        var cut = Render<Home>();

        var passwordInput = cut.Find("#generatedPassword");
        Assert.Equal(string.Empty, passwordInput.GetAttribute("value"));
    }

    [Fact]
    public void OnlyMasterPassword_WithoutSiteUrl_GeneratedPasswordStaysEmpty()
    {
        var cut = Render<Home>();

        cut.Find("#masterPassword").Input("mymasterpassword");

        var passwordInput = cut.Find("#generatedPassword");
        Assert.Equal(string.Empty, passwordInput.GetAttribute("value"));
    }

    [Fact]
    public void SiteUrlInput_NormalizesToHostname()
    {
        var cut = Render<Home>();

        cut.Find("#siteUrl").Input("https://Example.com/some/path?x=1");

        var hostnameDiv = cut.Find(".form-control-plaintext.fw-bold");
        Assert.Equal("example.com", hostnameDiv.TextContent);
    }

    [Fact]
    public void MasterPasswordAndSiteUrlBothSet_GeneratesNonEmptyPassword()
    {
        var cut = Render<Home>();

        cut.Find("#masterPassword").Input("mymasterpassword");
        cut.Find("#siteUrl").Input("https://example.com");

        var passwordInput = cut.Find("#generatedPassword");
        var value = passwordInput.GetAttribute("value");
        Assert.False(string.IsNullOrEmpty(value));
    }

    [Fact]
    public void MasterPasswordInput_StripsNonAlphanumericCharacters()
    {
        var cut = Render<Home>();

        cut.Find("#masterPassword").Input("my pass!word-123");
        cut.Find("#siteUrl").Input("https://example.com");

        var expected = new PasswordGeneratorService().GeneratePassword(
            "mypassword123", "example.com", PasswordHashAlgorithm.SHA256, 16,
            PasswordCharsetOptions.Lowercase | PasswordCharsetOptions.Digits | PasswordCharsetOptions.Uppercase);

        var passwordInput = cut.Find("#generatedPassword");
        Assert.Equal(expected, passwordInput.GetAttribute("value"));
    }

    [Fact]
    public void InvalidSiteUrl_ShowsInvalidUrlMessage()
    {
        var cut = Render<Home>();

        cut.Find("#siteUrl").Input("://not-a-valid-url");

        Assert.Contains("Invalid site URL.", cut.Markup);
    }

    [Fact]
    public void TogglingAllCharsetOptionsOff_ShowsSelectAtLeastOneMessage()
    {
        var cut = Render<Home>();

        cut.Find("#chkLowercase").Change(false);
        cut.Find("#chkDigits").Change(false);
        cut.Find("#chkUppercase").Change(false);

        Assert.Contains("Select at least one character type.", cut.Markup);
    }

    [Fact]
    public void CopyToClipboard_IsDisabled_WhenNoPasswordGenerated()
    {
        var cut = Render<Home>();

        var copyButton = cut.Find("button.btn-outline-secondary");
        Assert.True(copyButton.HasAttribute("disabled"));
    }

    [Fact]
    public void CopyToClipboard_InvokesClipboardWriteText_WhenPasswordPresent()
    {
        var cut = Render<Home>();
        cut.Find("#masterPassword").Input("mymasterpassword");
        cut.Find("#siteUrl").Input("https://example.com");

        var invocation = JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
        invocation.SetVoidResult();

        var copyButton = cut.Find("button.btn-outline-secondary");
        copyButton.Click();

        Assert.Single(invocation.Invocations);
        cut.WaitForAssertion(() => Assert.Contains("text-success", cut.Markup));
    }
}

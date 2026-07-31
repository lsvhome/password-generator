using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WebApp.Services;

namespace WebApp.Pages;

public partial class Home : ComponentBase
{
    private static readonly Regex NonAlphanumeric = new("[^a-zA-Z0-9]", RegexOptions.Compiled);

    private PasswordHashAlgorithm SelectedAlgorithm { get; set; } = PasswordHashAlgorithm.SHA256;
    private int PasswordLength { get; set; } = 16;

    private bool UseLowercase { get; set; } = true;
    private bool UseDigits { get; set; } = true;
    private bool UseUppercase { get; set; } = true;
    private bool UseSymbols { get; set; }

    private string MasterPassword { get; set; } = string.Empty;
    private bool SaveMasterPassword { get; set; }

    private string SiteUrl { get; set; } = string.Empty;
    private string SiteHostname { get; set; } = string.Empty;

    private string GeneratedPassword { get; set; } = string.Empty;
    private bool JustCopied { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var savedMasterPassword = await Protector.LoadAsync();
        if (savedMasterPassword is not null)
        {
            MasterPassword = savedMasterPassword;
            SaveMasterPassword = true;
        }

        Recalculate();
    }

    private async Task OnMasterPasswordInput(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString() ?? string.Empty;
        MasterPassword = NonAlphanumeric.Replace(raw, string.Empty);

        Recalculate();
        await PersistMasterPasswordAsync();
    }

    private void OnSiteUrlInput(ChangeEventArgs e)
    {
        SiteUrl = e.Value?.ToString() ?? string.Empty;
        SiteHostname = Generator.NormalizeHostname(SiteUrl);

        Recalculate();
    }

    private async Task OnSavePreferenceChanged()
    {
        await PersistMasterPasswordAsync();
    }

    private void Recalculate()
    {
        JustCopied = false;

        if (string.IsNullOrEmpty(MasterPassword) || string.IsNullOrEmpty(SiteHostname))
        {
            GeneratedPassword = string.Empty;
            return;
        }

        var options = PasswordCharsetOptions.None;
        if (UseLowercase) options |= PasswordCharsetOptions.Lowercase;
        if (UseDigits) options |= PasswordCharsetOptions.Digits;
        if (UseUppercase) options |= PasswordCharsetOptions.Uppercase;
        if (UseSymbols) options |= PasswordCharsetOptions.Symbols;

        GeneratedPassword = Generator.GeneratePassword(MasterPassword, SiteHostname, SelectedAlgorithm, PasswordLength, options);
    }

    private async Task PersistMasterPasswordAsync()
    {
        if (SaveMasterPassword && !string.IsNullOrEmpty(MasterPassword))
        {
            await Protector.SaveAsync(MasterPassword);
        }
        else
        {
            await Protector.ClearAsync();
        }
    }

    private async Task CopyToClipboard()
    {
        if (string.IsNullOrEmpty(GeneratedPassword))
        {
            return;
        }

        await JS.InvokeVoidAsync("navigator.clipboard.writeText", GeneratedPassword);
        JustCopied = true;
        StateHasChanged();

        _ = ResetCopiedIndicatorAsync();
    }

    private async Task ResetCopiedIndicatorAsync()
    {
        await Task.Delay(2000);
        JustCopied = false;
        StateHasChanged();
    }
}

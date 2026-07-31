using Blazor.SubtleCrypto;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebApp;
using WebApp.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<PasswordGeneratorService>();
// No global Key is configured: SubtleCrypto generates a fresh AES-GCM key/IV per encryption,
// which MasterPasswordProtector stores alongside the ciphertext.
builder.Services.AddSubtleCrypto();
builder.Services.AddScoped<MasterPasswordProtector>();

await builder.Build().RunAsync();

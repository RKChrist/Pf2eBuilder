using Fluxor;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Options;
using Pf2e.Client;
using Pf2e.Client.Api;
using Pf2e.Client.Catalog;
using Pf2e.Client.State;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

RuleCatalog.EnsureComplete();

var api = builder.Configuration.GetSection(ApiOptions.Section).Get<ApiOptions>()
          ?? throw new InvalidOperationException(
              $"wwwroot/appsettings.json is missing its '{ApiOptions.Section}' section.");

// An empty BaseUrl means the API is wherever this page came from. That is what makes one
// ngrok tunnel enough: the API serves the client, so phones and desktops share an origin
// and there is no CORS to configure. A value here overrides it for the two-process dev run.
if (string.IsNullOrWhiteSpace(api.BaseUrl))
{
    api.BaseUrl = builder.HostEnvironment.BaseAddress;
}

builder.Services.AddSingleton(Options.Create(api));
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(api.BaseUrl) });
builder.Services.AddScoped<RulesApi>();
builder.Services.AddScoped<TrackerApi>();
builder.Services.AddScoped<CampaignHub>();
builder.Services.AddScoped<CampaignMemory>();
builder.Services.AddFluxor(options => options.ScanAssemblies(typeof(App).Assembly));

await builder.Build().RunAsync();

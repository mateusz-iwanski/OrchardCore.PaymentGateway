using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Settings;
using Refit;
using OrchardCore.PaymentGateway.Providers.Przelewy24.Clients;
using OrchardCore.PaymentGateway.Providers.Przelewy24.Services;
using OrchardCore.PaymentGateway.Providers.Przelewy24.Drivers;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace OrchardCore.PaymentGateway
{
    public class Startup : StartupBase
    {
        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient("Przelewy24")
                .ConfigureHttpClient((sp, client) =>
                {
                    var cfg = sp.GetRequiredService<IConfiguration>();
                    var baseUrl = cfg["Przelewy24:BaseUrl"] ?? "https://sandbox.przelewy24.pl/api/v1/";
                    if (!baseUrl.EndsWith('/')) baseUrl += '/';
                    client.BaseAddress = new Uri(baseUrl);
                })
                .AddHttpMessageHandler<Przelewy24LoggingHandler>();

            services.AddRefitClient<IPrzelewy24Api>()
                .ConfigureHttpClient((sp, client) =>
                {
                    var cfg = sp.GetRequiredService<IConfiguration>();
                    var baseUrl = cfg["Przelewy24:BaseUrl"] ?? "https://sandbox.przelewy24.pl/api/v1/";
                    if (!baseUrl.EndsWith('/')) baseUrl += '/';
                    client.BaseAddress = new Uri(baseUrl);
                });

            services.AddScoped<IDisplayDriver<ISite>, Przelewy24SettingsDisplayDriver>();
            services.AddSingleton<IPrzelewy24SignatureProvider, Przelewy24SignatureProvider_Default>();
            services.AddTransient<Przelewy24LoggingHandler>();
            services.AddTransient<Przelewy24Service>();

            services.AddScoped<INavigationProvider, AdminMenu>();
            services.AddScoped<IPermissionProvider, Permissions>();
        }

        public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        {
            routes.MapAreaControllerRoute(
                name: "Home",
                areaName: "ApiClientPrzelewy24",
                pattern: "Home/Index",
                defaults: new { controller = "Home", action = "Index" }
            );
        }
    }
}


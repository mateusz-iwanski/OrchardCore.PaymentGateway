using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using Refit;
using ApiClientPrzelewy24.Services;
using ApiClientPrzelewy24.Clients;

namespace ApiClientPrzelewy24
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

            services.AddSingleton<IPrzelewy24SignatureProvider, Przelewy24SignatureProvider_Default>();
            services.AddTransient<Przelewy24LoggingHandler>();
            services.AddTransient<Przelewy24Service>();
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


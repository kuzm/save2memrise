﻿using System;
using System.IO;
using System.Linq;
using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.AspNetCore.Health;
using App.Metrics.Formatters.Prometheus;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Formatting.Compact;

namespace Save2Memrise.Services.Public.API
{
    public class Program
    {
        public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables(prefix: "SAVE2MEMRISE_")
            .Build();

        public static void Main(string[] args)
        {
            var version = Configuration.GetValue<string>("AppVersion");

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .WriteTo.Console(new CompactJsonFormatter())
                .CreateLogger()
                .ForContext("AppVersion", version);

            try
            {
                Log.Information("Starting web host");
                BuildWebHost(args).Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IWebHost BuildWebHost(string[] args) 
        {
            var metrics = AppMetrics.CreateDefaultBuilder()
                .Configuration.Configure(options =>
                {
                    options.Enabled = true;
                    options.DefaultContextLabel = "s2m";
                    options.WithGlobalTags((tags, envInfo) =>
                    {
                        tags.Add("app_version", envInfo.EntryAssemblyVersion);
                    });
                })
                .OutputMetrics.AsPrometheusPlainText()
                .Build();

            return WebHost.CreateDefaultBuilder(args)
                .ConfigureAppHealthHostingConfiguration(options => 
                {
                    options.HealthEndpoint = "/_system/health";
                    options.PingEndpoint = "/_system/ping";
                })
                .UseHealth()
                .ConfigureAppMetricsHostingConfiguration(options => 
                {
                    options.MetricsTextEndpoint = "/_system/metrics";
                })
                .ConfigureMetrics(metrics)
                .UseMetrics(options =>
                {
                    options.EndpointOptions = endpointsOptions =>
                    {
                        endpointsOptions.MetricsEndpointEnabled = false;
                        endpointsOptions.MetricsTextEndpointEnabled = true;
                        endpointsOptions.EnvironmentInfoEndpointEnabled = true;
                        endpointsOptions.MetricsTextEndpointOutputFormatter 
                            = metrics.OutputMetricsFormatters.OfType<MetricsPrometheusTextOutputFormatter>().First();
                    };
                })
                .UseMetricsWebTracking(options => 
                {
                    options.ApdexTrackingEnabled = false;
                    options.OAuth2TrackingEnabled = false;
                })
                .UseStartup<Startup>()
                .UseConfiguration(Configuration)
                .UseSerilog()
                .UseUrls("http://*:8080")
                .Build();
        }
    }
}

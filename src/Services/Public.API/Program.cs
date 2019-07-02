using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using App.Metrics.AspNetCore.Health;
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

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureAppHealthHostingConfiguration(options => 
                {
                    options.HealthEndpoint = "/_system/health";
                    options.PingEndpoint = "/_system/ping";
                })
                .UseHealth()
                .UseStartup<Startup>()
                .UseConfiguration(Configuration)
                .UseSerilog()
                .UseUrls("http://*:8080")
                .Build();
    }
}

using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace Tests.Common
{
    public class ConfigHelper
    {
        private static readonly Lazy<IConfiguration> _config = new Lazy<IConfiguration>(() => 
            new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("testsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"testsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .AddJsonFile("testsettings.local.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables(prefix: "SAVE2MEMRISE_")
                .Build()
            );

        public static IConfiguration Config => _config.Value;

        public static T Get<T>(string section) where T: new()
        {
            var obj = new T();
            Config.GetSection(section).Bind(obj);
            return obj;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NSpec;
using System.IO;
using NSpec.Api;
using NSpec.Domain; // SpecFinder
using NSpec.Domain.Formatters; // ConsoleFormatter
using Serilog;
using Microsoft.Extensions.Configuration;
using Tests.Common;

namespace Save2Memrise.Services.Public.API.IntegrationTests
{
    class Program 
    {
        static void Main(string[] args)
        {
            var config = ConfigHelper.Config;

            //TODO Load config
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}{Properties}{NewLine}"
                )
                .CreateLogger();

            var types = typeof(Program).Assembly.GetTypes();
            var finder = new SpecFinder(types, "");
            var tagsFilter = new Tags().Parse("");
            var builder = new ContextBuilder(finder, tagsFilter, new DefaultConventions());
            var runner = new ContextRunner(tagsFilter, new ConsoleFormatter(), false);
            var results = runner.Run(builder.Contexts().Build());
 
            if(results.Failures().Count() > 0)
            {
                Environment.Exit(1);
            }
        }
    }
}
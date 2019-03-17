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

namespace Save2Memrise.Services.Public.API.UnitTests
{
    class Program 
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
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
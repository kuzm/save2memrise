using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;

namespace Save2Memrise.Services.Public.API.Middleware
{
    class AppVersionMiddleware
    {
        readonly string _appVersion;
        readonly RequestDelegate _next;

        public AppVersionMiddleware(ILogger logger, IConfiguration configuration, RequestDelegate next)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (configuration == null) 
                throw new ArgumentNullException(nameof(configuration));

            _next = next ?? throw new ArgumentNullException(nameof(next));
        
            _appVersion = configuration.GetValue<string>("AppVersion") ?? "";

            logger.ForContext<AppVersionMiddleware>()
                .Information("AppVersion: {RetrievedAppVersion}", _appVersion);
        }

        // ReSharper disable once UnusedMember.Global
        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));

            httpContext.Response?.Headers.Add("X-AppVersion", _appVersion);
            await _next(httpContext);
        }
    }
}
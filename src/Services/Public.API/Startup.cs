﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Save2Memrise.Services.Public.API.Middleware;

namespace Save2Memrise.Services.Public.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IHostingEnvironment env)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<Serilog.ILogger>(Serilog.Log.Logger);
            
            services.AddCors(options => 
                options.AddPolicy("AllowAll", p => p
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader()));     
        
            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddMetrics();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors("AllowAll");

            app.UseMiddleware<AppVersionMiddleware>();
            app.UseMiddleware<SerilogMiddleware>();
            
            app.UseMvc();
        }
    }
}

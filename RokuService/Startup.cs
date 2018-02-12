using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RokuService.Services;
using yozepi.Roku;

namespace RokuService
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //services.AddLogging(builder =>
            //{
            //    builder.AddConsole(config => config.IncludeScopes = true)
            //        .AddDebug();
            //});

            services.AddSingleton<RokuManager>((sp) =>
            {
                var mgr = new RokuManager(new RokuDiscovery(), sp.GetService<ILogger<RokuManager>>());
                var junk = mgr.ListRokusAsync(true).Result;
                return mgr;
            });
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            //loggerFactory.AddConsole(LogLevel.Trace);
            //loggerFactory.AddDebug();
            app.UseMvc();
        }
    }
}

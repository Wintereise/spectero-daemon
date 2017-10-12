﻿using System.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectero.daemon.Libraries.Config;
using NLog.Extensions.Logging;
using NLog.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ServiceStack.OrmLite;
using Spectero.daemon.Libraries.Core.Authenticator;
using Spectero.daemon.Libraries.Core.Statistics;
using Spectero.daemon.Libraries.Services;
using Spectero.daemon.Migrations;

namespace Spectero.daemon
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        private IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddMemoryCache();
            
            var appConfig = Configuration.GetSection("Daemon");
            services.Configure<AppConfig>(appConfig);

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddSingleton<IDbConnection>(c => 
                new OrmLiteConnectionFactory(appConfig["DatabaseFile"], SqliteDialect.Provider).Open()
            );
            
            services.AddSingleton<IStatistician, Statistician>();

            services.AddSingleton<IAuthenticator, Authenticator>();

            services.AddSingleton<IMigration, Initialize>();
            
            services.AddSingleton<IServiceManager, ServiceManager>();
                        
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env,
            ILoggerFactory loggerFactory, IMigration migration)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            app.UseMvc();

            loggerFactory.AddNLog();
            loggerFactory.ConfigureNLog("nlog.config");
            app.AddNLogWeb();
            
            migration.Up();
        }
    }
}

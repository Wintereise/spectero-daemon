﻿using System;
using System.Data;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NLog.Extensions.Logging;
using NLog.Web;
using RazorLight;
using ServiceStack.OrmLite;
using Spectero.daemon.Libraries.Config;
using Spectero.daemon.Libraries.Core.Authenticator;
using Spectero.daemon.Libraries.Core.Crypto;
using Spectero.daemon.Libraries.Core.HTTP.Middlewares;
using Spectero.daemon.Libraries.Core.Identity;
using Spectero.daemon.Libraries.Core.Statistics;
using Spectero.daemon.Libraries.Services;
using Spectero.daemon.Migrations;
using Spectero.daemon.Models;

namespace Spectero.daemon
{
    public class Startup
    {
        private readonly string _currentDirectory = System.IO.Directory.GetCurrentDirectory();

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", false, true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true)
                .AddJsonFile("hosting.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        private IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            var appConfig = Configuration.GetSection("Daemon");
            var serviceProvider = services.BuildServiceProvider();

            services.Configure<AppConfig>(appConfig);

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddSingleton(c =>
                InitializeDbConnection(appConfig["DatabaseFile"], SqliteDialect.Provider)
            );

            services.AddSingleton<IStatistician, Statistician>();

            services.AddSingleton<IAuthenticator, Authenticator>();

            services.AddSingleton<IIdentityProvider, IdentityProvider>();

            services.AddSingleton<ICryptoService, CryptoService>();

            services.AddSingleton<IMigration, Initialize>();

            services.AddSingleton<IServiceConfigManager, ServiceConfigManager>();

            services.AddSingleton<IServiceManager, ServiceManager>();

            services.AddSingleton<IRazorLightEngine>(c =>
                new EngineFactory()
                    .ForFileSystem(System.IO.Path.Combine(_currentDirectory, appConfig["TemplateDirectory"]))
            );

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = false,
                        ValidateIssuer = false,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey =
                            serviceProvider.GetService<ICryptoService>().GetJWTSigningKey()
                    };
                });


            services.AddCors(options =>
            {
                // TODO: Lock down this policy in production
                options.AddPolicy("DefaultCORSPolicy",
                    builder => builder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials());
            });

            services.AddMvc();
            services.AddMemoryCache();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env,
            ILoggerFactory loggerFactory, IMigration migration)
        {
            var appConfig = Configuration.GetSection("Daemon");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseCors("DefaultCORSPolicy");
                app.UseInterceptOptions(); // Return 200/OK with correct CORS to allow preflight requests, giant hack.
            }

            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(
                    Path.Combine(_currentDirectory, appConfig["WebRoot"]))
            });

            app.UseAddRequestIdHeader();
            app.UseMvc();
            
            loggerFactory.AddNLog();
            loggerFactory.ConfigureNLog("nlog.config");
            app.AddNLogWeb();

            migration.Up();
        }

        private IDbConnection InitializeDbConnection(string connectionString, IOrmLiteDialectProvider provider)
        {
            // Validate that the DB connection can actually be used.
            // If not, attempt to fix it (for SQLite and corrupt files.)
            // Other providers not implemented (and are not possibly fixable for us anyway due to 3rd party daemons being involved)
            OrmLiteConnectionFactory factory = new OrmLiteConnectionFactory(connectionString, provider);
            IDbConnection databaseContext = null;

            try
            {
                databaseContext = factory.Open();
                databaseContext.TableExists<User>();
            }
            catch (SqliteException e)
            {
                // Message=SQLite Error 26: 'file is encrypted or is not a database'. most likely.
                // If we got here, our local database is corrupt.
                // Why Console.Writeline? Because the logging context is not initialized yet <_<'

                Console.WriteLine("Error: " + e.Message);
                databaseContext?.Close();

                // Move the corrupt DB file into db.sqlite.corrupt to aid recovery if needed.
                File.Copy(connectionString, connectionString + ".corrupt");

                // Create a new empty DB file for the schema to be initialized into
                // Dirty hack to ensure that the file's resource is actually released by the time ORMLite tries to open it
                using (var resource = File.Create(connectionString))
                {
                    Console.WriteLine("Error Recovery: Executing automatic DB schema creation after saving the corrupt DB into db.sqlite.corrupt");
                }

                databaseContext = factory.Open();
            }


            return databaseContext;

        }
    }
}
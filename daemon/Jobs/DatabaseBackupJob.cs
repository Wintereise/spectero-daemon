﻿using System;
using System.Data;
using System.IO;
using Hangfire;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestSharp;
using ServiceStack.OrmLite;
using Spectero.daemon.Libraries.CloudConnect;
using Spectero.daemon.Libraries.Config;
using Spectero.daemon.Libraries.Core.Identity;

namespace Spectero.daemon.Jobs
{
    public class DatabaseBackupJob
    {
        // Class Dependencies
        private readonly IRestClient _restClient;
        private readonly IDbConnection _db;
        private readonly IIdentityProvider _identityProvider;
        private readonly ILogger<FetchCloudEngagementsJob> _logger;
        private readonly IMemoryCache _cache;
        private readonly AppConfig _config;
        private readonly ICloudHandler _cloudHandler;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="db"></param>
        /// <param name="restClient"></param>
        /// <param name="identityProvider"></param>
        /// <param name="logger"></param>
        /// <param name="cache"></param>
        /// <param name="configMonitor"></param>
        /// <param name="cloudHandler"></param>
        public DatabaseBackupJob(IDbConnection db, IRestClient restClient,
            IIdentityProvider identityProvider, ILogger<FetchCloudEngagementsJob> logger,
            IMemoryCache cache, IOptionsMonitor<AppConfig> configMonitor,
            ICloudHandler cloudHandler)
        {
            _restClient = restClient;
            _db = db;
            _identityProvider = identityProvider;
            _logger = logger;
            _cache = cache;
            _config = configMonitor.CurrentValue;
            _cloudHandler = cloudHandler;

            logger.LogDebug("FCEJ: init successful, dependencies processed.");
        }

        /// <summary>
        /// The cron time that the schedule should run.
        /// Currently configured for every night at midnight.
        /// </summary>
        /// <returns></returns>
        public string GetSchedule() => "0 0 * * *";

        /// <summary>
        /// Determine if this job is enabled.
        /// </summary>
        /// <returns></returns>
        public bool IsEnabled() => _cloudHandler.IsConnected().Result;


        /// <summary>
        /// The actual job function that will be executed.
        /// </summary>
        /// <exception cref="???"></exception>
        [AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
        public void Perform()
        {
            // Check to make sure this job is enabled.
            if (!IsEnabled())
            {
                _logger.LogError("FCEJ: Job enabled, but matching criterion does not match. This should not happen, silently going back to sleep.");
                return;
            }
            
            // Validate that the existing foldders exist.
            if (!RootBackupDirectoryExists()) CreateRootBackupDirectory();
            if (!DatedBackupDirectoryExists()) CreateDatedBackupDirectory();
            
            // Save the database into the dated directory.
            // TODO: implement this.
            
        }

        /// <summary>
        /// Pointer to the root directory that backups should occur in.
        /// In the event that we write something else inside the daemon that needs easy access to find the path, we can call this static function.
        /// </summary>
        public static string RootBackupDirectory => Path.Combine(Program.GetAssemblyLocation(), "Backups");
        
        /// <summary>
        /// Pointer to the directory that today's backup should be saved to.
        /// In the event that we write something else inside the daemon that needs easy access to find the path, we can call this static function.
        /// </summary>
        public static string DatedBackupDirectory => Path.Combine(RootBackupDirectory, DateTime.Now.ToString("yyyy-MM-dd"));

        /// <summary>
        /// Utility function to create the root backup directory.
        /// </summary>
        public void CreateRootBackupDirectory() => Directory.CreateDirectory(RootBackupDirectory);

        /// <summary>
        /// Utility function to check if the root backup directory exists.
        /// </summary>
        /// <returns></returns>
        public bool RootBackupDirectoryExists() => Directory.Exists(RootBackupDirectory);
        
        /// <summary>
        /// Utility function to create the Dated backup directory.
        /// </summary>
        public void CreateDatedBackupDirectory() => Directory.CreateDirectory(DatedBackupDirectory);

        /// <summary>
        /// Utility function to check if the Dated backup directory exists.
        /// </summary>
        /// <returns></returns>
        public bool DatedBackupDirectoryExists() => Directory.Exists(DatedBackupDirectory);
    }
}
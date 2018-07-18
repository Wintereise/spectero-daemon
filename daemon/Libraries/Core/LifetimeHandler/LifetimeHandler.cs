﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectero.daemon.Libraries.Config;
using Spectero.daemon.Libraries.Core.ProcessRunner;
using Spectero.daemon.Libraries.Services;

namespace Spectero.daemon.Libraries.Core.LifetimeHandler
{
    public class LifetimeHandler : ILifetimeHandler
    {
        private readonly ILogger<ILifetimeHandler> _logger;
        private readonly AppConfig _config;
        private readonly IServiceManager _serviceManager;
        private readonly IProcessRunner _processRunner;
        
        public LifetimeHandler(IOptionsMonitor<AppConfig> configurationMonitor, ILogger<ILifetimeHandler> logger,
            IServiceManager serviceManager, IProcessRunner processRunner)
        {
            _config = configurationMonitor.CurrentValue;
            _logger = logger;
            _serviceManager = serviceManager;
            _processRunner = processRunner;
        }

        public void OnStarted()
        {
            _logger.LogDebug("Processing events that are registered for ApplicationStarted");
            
            // Remove the filesystem marker that signifies ongoing startup
            if(! Utility.ManageStartupMarker(true))
                _logger.LogWarning($"An attempt was made to remove the startup marker ({Utility.GetCurrentStartupMarker()}), but it could not be found.");
            else
                _logger.LogDebug($"Startup marker ({Utility.GetCurrentStartupMarker()}) has been de-activated successfully.");
        }

        public void OnStopping()
        {
            _logger.LogDebug("Processing events that are registered for ApplicationStopping");
            _serviceManager.StopServices();
        }

        public void OnStopped()
        {
            _logger.LogDebug("Processing events that are registered for ApplicationStopped");
            _processRunner.TerminateAllTrackedCommands();
        }
    }
}
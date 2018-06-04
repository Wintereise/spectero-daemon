﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;

namespace Spectero.daemon.Libraries.Core.ProcessRunner
{
    public class CommandLogger
    {
        /// <summary>
        /// Attach the command to all available stream readers.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="commandHolder"></param>
        public static void LatchQuickly(ILogger<ProcessRunner> logger, CommandHolder commandHolder)
        {
            // Start the standard stream reader.
            new Thread(() => { Standard(logger, commandHolder); }).Start();

            // Start the error stream reader.
            new Thread(() => { Error(logger, commandHolder); }).Start();
        }

        /// <summary>
        /// Attach the command to the standard stream reader.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="commandHolder"></param>
        public static async void Standard(ILogger<ProcessRunner> logger, CommandHolder commandHolder)
        {
            string line;
            while ((line = await commandHolder.Command.StandardOutput.ReadLineAsync().ConfigureAwait(false)) != null)
                logger.LogInformation(line);
        }

        /// <summary>
        /// Attach the command to the error stream reader.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="commandHolder"></param>
        public static async void Error(ILogger<ProcessRunner> logger, CommandHolder commandHolder)
        {
            string line;
            while ((line = await commandHolder.Command.StandardError.ReadLineAsync().ConfigureAwait(false)) != null)
                logger.LogError(line);
        }
    }
}
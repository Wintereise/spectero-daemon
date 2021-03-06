﻿/*
    Spectero Daemon - Daemon Component to the Spectero Solution
    Copyright (C)  2017 Spectero, Inc.

    Spectero Daemon is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Spectero Daemon is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.
    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://github.com/ProjectSpectero/daemon/blob/master/LICENSE>.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Spectero.daemon.Jobs;

namespace Spectero.daemon.Libraries.Config
{
    public class AppConfig
    {
        public string BlockedRedirectUri { get; set; }
        public string DatabaseDir { get; set; }
        public double AuthCacheMinutes { get; set; }
        public bool LocalSubnetBanEnabled { get; set; }
        public Dictionary<string, Dictionary<string, string>> Defaults { get; set; }
        public int PasswordCostLowerThreshold { get; set; }
        public int JWTTokenExpiryInMinutes { get; set; }
        public int JWTRefreshTokenDelta { get; set; }
        public int PasswordCostCalculationIterations { get; set; }
        public string PasswordCostCalculationTestTarget { get; set; }
        public double PasswordCostTimeThreshold { get; set; }

        public static bool isWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool isLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static bool isMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static bool isUnix => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public bool RespectEndpointToOutgoingMapping { get; set; }
        public bool BindToUnbound { get; set; }
        public string LoggingConfig { get; set; }
        public string DefaultOutgoingIPResolver { get; set; }

        public bool InMemoryAuth { get; set; }
        public int InMemoryAuthCacheMinutes { get; set; }
        public bool AutoStartServices { get; set; }
        public bool LogCommonProxyEngineErrors { get; set; }
        public bool IgnoreRFC1918 { get; set; }
        public bool HaltStartupIfServiceInitFails { get; set; }
        public BackupConfiguration Backups { get; set; }

        public static string ApiBaseUri
        {
            // Mostly hardcoded, a discovery service is not planned for the MVP
            get
            {
                switch (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.ToLower())
                {
                    case "local":
                        return $"http://homestead.marketplace/v1/";
                    case "development":
                    case "staging":
                        return $"https://dev.spectero.com/v1/";
                    default:
                        return $"https://api.spectero.com/v1/";
                }
            }
        }

        public static string CloudConnectDefaultAuthKey => "cloud";
        public static string version => "0.1-alpha";

        /// <summary>
        /// Simple function to detect the presence of OpenVZ hosts.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static bool IsOpenVZContainer()
        {
            return (File.Exists("/proc/user_beancounters") || Directory.Exists("/proc/bc/"));
        }
    }
}
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
using System.Diagnostics;
using System.Linq;
using Spectero.daemon.Libraries.Config;

namespace Spectero.daemon.Libraries.APM
{
    public class Apm
    {
        private readonly ISystemEnvironment _operatingSystemEnvironment;

        /// <summary>
        /// APM Constructor
        /// 
        /// The constructor determines which operating system environment is needed.
        /// Environments follow a interface, and will have the same core functions.
        /// </summary>
        public Apm()
        {
            if (AppConfig.isWindows)
            {
                _operatingSystemEnvironment = new WindowsEnvironment();
            }
            else if (AppConfig.isLinux)
            {
                _operatingSystemEnvironment = new LinuxEnvironment();
            }
            else if (AppConfig.isMac)
            {
                _operatingSystemEnvironment = new MacEnvironment();
            }
        }

        /// <summary>
        /// Get information about the memory on the system in the form of a dictionary.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> GetMemoryDetails()
        {
            // Purge any cached proc information.
            _operatingSystemEnvironment.PurgeCachedInformation();

            // Get Physical Memory
            var physicalMemoryObjects = new Dictionary<string, object>()
            {
                {"Used", _operatingSystemEnvironment.GetPhysicalMemoryUsed()},
                {"Free", _operatingSystemEnvironment.GetPhysicalMemoryFree()},
                {"Total", _operatingSystemEnvironment.GetPhysicalMemoryTotal()}
            };

            // Returned the compiled object.
            return new Dictionary<string, object>()
            {
                {"Physical", physicalMemoryObjects},
            };
        }

        /// <summary>
        /// Get infomration about the CPU in the form of a dictionary.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> GetCpuDetails()
        {
            // Purge any cached proc information.
            _operatingSystemEnvironment.PurgeCachedInformation();

            // Return the compiled object.
            return new Dictionary<string, object>()
            {
                {"Model", _operatingSystemEnvironment.GetCpuName()},
                {"Cores", _operatingSystemEnvironment.GetCpuCoreCount()},
                {"Threads", _operatingSystemEnvironment.GetCpuThreadCount()},
                {"Cache Size", _operatingSystemEnvironment.GetCpuCacheSize()},
                // TODO: Enable after MVP + Discussion
                // {"Utilization", GetUtilizationDetails() }
            };
        }

        /// <summary>
        /// Get infoirmation about the environment in the form of a dictionary.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> GetEnvironmentDetails()
        {
            return new Dictionary<string, object>()
            {
                {"Hostname", Environment.MachineName},
                {"OS Version", Environment.OSVersion},
                {"64-Bits", Is64Bits()},
                {"Virtualization", GetVirtualization()}
            };
        }

        /// <summary>
        /// Provide the type of virtualization environment we are in.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, bool> GetVirtualization()
        {
            return new Dictionary<string, bool>()
            {
                {"OpenVZ", AppConfig.IsOpenVZContainer()},
            };
        }

        /// <summary>
        /// Get all environment information in the form of a dictionary.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> GetAllDetails()
        {
            return new Dictionary<string, object>()
            {
                {"CPU", GetCpuDetails()},
                {"Memory", GetMemoryDetails()},
                {"Environment", GetEnvironmentDetails()}
            };
        }

        /// <summary>
        /// Create a combined dictionary of processor utilizations for the spectero daemon and the system.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> GetUtilizationDetails()
        {
            return new Dictionary<string, object>()
            {
                {"Spectero Daemon", GetSpecteroProcessUtilization()},
                {"Total", GetTotalProcessUtilization()}
            };
        }

        /// <summary>
        /// Get the percentage of the processor that spectero daemon itself uses.
        /// </summary>
        /// <returns></returns>
        public double GetSpecteroProcessUtilization()
        {
            var currentProcess = Process.GetCurrentProcess();
            return (currentProcess.TotalProcessorTime.TotalMilliseconds * 100) / (DateTime.Now - currentProcess.StartTime).TotalMilliseconds;
        }

        /// <summary>
        /// Get the total processor utilization percentage.
        ///
        /// Note: Linq is awesome!
        /// </summary>
        /// <returns></returns>
        public double GetTotalProcessUtilization()
        {
            return Process.GetProcesses().Sum(currentProcess =>
                (currentProcess.TotalProcessorTime.TotalMilliseconds * 100) / (DateTime.Now - currentProcess.StartTime).TotalMilliseconds);
        }

        /// <summary>
        /// Determine if the system is running in 64 bit mode.
        /// </summary>
        /// <returns></returns>
        public bool Is64Bits()
        {
            return Environment.Is64BitOperatingSystem;
        }

        /// <summary>
        /// Get the instance of the operating system environment handler.
        ///
        /// This is extendable: Apm.SystemEnvironment().GetCpuDetails();
        /// </summary>
        /// <returns></returns>
        public ISystemEnvironment SystemEnvironment() => _operatingSystemEnvironment;
    }
}
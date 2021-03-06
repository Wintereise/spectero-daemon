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
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.X509;
using ServiceStack.OrmLite;
using Spectero.daemon.Libraries.Config;
using Spectero.daemon.Libraries.Core;
using Spectero.daemon.Libraries.Core.Authenticator;
using Spectero.daemon.Libraries.Core.Constants;
using Spectero.daemon.Libraries.Core.Crypto;
using Spectero.daemon.Libraries.Core.Identity;
using Spectero.daemon.Models;

namespace Spectero.daemon.Migrations
{
    public class Initialize : IMigration
    {
        private readonly AppConfig _config;
        private readonly ICryptoService _cryptoService;
        private readonly IDbConnection _db;
        private readonly IIdentityProvider _identityProvider;
        private readonly ILogger<Initialize> _logger;

        private readonly string _firstRunConfigName;


        public Initialize(IOptionsMonitor<AppConfig> config, ILogger<Initialize> logger,
            IDbConnection db, IIdentityProvider identityProvider,
            ICryptoService cryptoService)
        {
            _config = config.CurrentValue;
            _logger = logger;
            _db = db;
            _identityProvider = identityProvider;
            _cryptoService = cryptoService;

            _firstRunConfigName = ".firstrun";
        }

        public void Up()
        {
            var instanceId = Guid.NewGuid().ToString();
            long viablePasswordCost = _config.PasswordCostLowerThreshold;

            var specteroCertKey = "";
            X509Certificate2 specteroCertificate = null;
            X509Certificate2 ca = null;

            var localIPs = Utility.GetLocalIPs(_config.IgnoreRFC1918);
                

            if (!_db.TableExists<Configuration>())
            {
                _db.CreateTable<Configuration>();
                _logger.LogDebug("Firstrun: Creating Configurations table and inserting default values");

                // Identity
                _db.Insert(new Configuration
                {
                    Key = ConfigKeys.SystemIdentity,
                    Value = instanceId
                });
                
                // Schema version
                _db.Insert(new Configuration
                {
                    Key = ConfigKeys.SchemaVersion,
                    Value = AppConfig.version
                });

                // Cloud Connectivity
                _db.Insert(new Configuration
                {
                    Key = ConfigKeys.CloudConnectStatus,
                    Value = false.ToString()
                });

                // HTTP proxy
                var httpSkeleton = Defaults.HTTP.Value;
                var proposedListeners = localIPs.Select(ip => Tuple.Create(ip.ToString(), 10240))
                    .ToList();
                if (proposedListeners.Count > 0)
                    httpSkeleton.listeners = proposedListeners;

                _db.Insert(new Configuration
                {
                    Key = ConfigKeys.HttpConfig,
                    Value = JsonConvert.SerializeObject(httpSkeleton)
                });

                // Password Hashing
                _logger.LogDebug("Firstrun: Calculating optimal password hashing cost.");
                viablePasswordCost = AuthUtils.GenerateViableCost(_config.PasswordCostCalculationTestTarget,
                    _config.PasswordCostCalculationIterations,
                    _config.PasswordCostTimeThreshold, _config.PasswordCostLowerThreshold);
                _logger.LogDebug($"Firstrun: Determined {viablePasswordCost} to be the optimal password hashing cost.");
                _db.Insert(new Configuration
                {
                    Key = ConfigKeys.PasswordHashingCost,
                    Value = viablePasswordCost.ToString()
                });

                // JWT security Key
                _logger.LogDebug("Firstrun: Generating JWT security key.");
                _db.Insert(new Configuration
                {
                    Key = ConfigKeys.JWTSymmetricSecurityKey,
                    Value = PasswordUtils.GeneratePassword(48, 8)
                });

                // Crypto
                // 48 characters len with 12 non-alpha-num characters
                // Ought to be good enough for everyone. -- The IPv4 working group, 1996
                var caPassword = PasswordUtils.GeneratePassword(48, 8);
                var serverPassword = PasswordUtils.GeneratePassword(48, 8);
                
                ca = _cryptoService.CreateCertificateAuthorityCertificate($"CN={instanceId}.ca.instance.spectero.io",
                    null, null, caPassword);
                
                var serverCertificate = _cryptoService.IssueCertificate($"CN={instanceId}.instance.spectero.io", ca, null, 
                    new[] { KeyPurposeID.AnyExtendedKeyUsage, KeyPurposeID.IdKPServerAuth }, serverPassword,
                    new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment ));

                specteroCertKey = PasswordUtils.GeneratePassword(48, 8);
                specteroCertificate = _cryptoService.IssueCertificate(
                    "CN=" + "spectero", ca, null,
                    new[] {KeyPurposeID.IdKPClientAuth}, specteroCertKey);
                    

                _db.Insert(new Configuration
                {
                    Key = ConfigKeys.CeritificationAuthorityPassword,
                    Value = caPassword
                });

                _db.Insert(new Configuration
                {
                    Key = ConfigKeys.ServerCertificatePassword,
                    Value = serverPassword
                });

                _db.Insert(new Configuration
                {
                    Key = ConfigKeys.CertificationAuthority,
                    Value = Convert.ToBase64String(_cryptoService.GetCertificateBytes(ca, caPassword))
                });

                _db.Insert(new Configuration
                {
                    Key = ConfigKeys.ServerCertificate,
                    Value = Convert.ToBase64String(_cryptoService.GetCertificateBytes(serverCertificate, serverPassword))
                });

                _db.Insert(new Configuration
                {
                    Key = ConfigKeys.ServerPFXChain,
                    Value = Convert.ToBase64String(_cryptoService.ExportCertificateChain(serverCertificate, ca)) // Yes, passwordless. Somewhat intentionally, as this is mostly consumed by 3rd party apps.
                });

                // OpenVPN defaults

                _db.Insert(new Configuration
                {
                    Key = ConfigKeys.OpenVPNListeners,
                    Value = JsonConvert.SerializeObject(Defaults.OpenVPNListeners)

                });

                _db.Insert(new Configuration
                {
                    Key = ConfigKeys.OpenVPNBaseConfig,
                    Value = JsonConvert.SerializeObject(Defaults.OpenVPN.Value)
                });

            }

            if (!_db.TableExists<User>())
            {
                _logger.LogDebug("Firstrun: Creating Users table");
                var password = PasswordUtils.GeneratePassword(16, 8);
                _db.CreateTable<User>();
                _db.Insert(new User
                {
                    AuthKey = "spectero",
                    Roles = new List<User.Role>
                    {
                        User.Role.SuperAdmin
                    },
                    FullName = "Spectero Administrator",
                    EmailAddress = "changeme@example.com",
                    Password = BCrypt.Net.BCrypt.HashPassword(password, (int) viablePasswordCost),
                    Cert = specteroCertificate != null && ca != null ? Convert.ToBase64String(_cryptoService.ExportCertificateChain(specteroCertificate, ca, specteroCertKey)) : "",
                    CertKey = specteroCertKey,
                    Source = User.SourceTypes.Local,
                    CreatedDate = DateTime.Now
                });

                using (var tw = new StreamWriter(_firstRunConfigName, false))
                {
                    tw.WriteLine("username: spectero");
                    tw.WriteLine($"password: {password}");
                }
            }


            if (!_db.TableExists<Statistic>())
            {
                _logger.LogDebug("Firstrun: Creating Statistics table");
                _db.CreateTable<Statistic>();

                _logger.LogInformation("Firstrun: Initialization complete.");
            }
        }

        public void Down()
        {
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.IO;
using NClap.Metadata;
using Spectero.daemon.CLI.Requests;

namespace Spectero.daemon.CLI.Commands
{
    public class OpenVPNInlineFileAuthentication : BaseJob
    {
        [PositionalArgument(
            ArgumentFlags.Required,
            Position = 0,
            Description = "The service scope being requested from the system for the user. Usually one of { HTTPProxy | OpenVPN | SSHTunnel | ShadowSOCKS }"
        )]
        private string Scope { get; set; }

        [PositionalArgument(
            ArgumentFlags.Required,
            Position = 1,
            Description = "The name of the file with authentication data."
        )]
        private string Filename { get; set; }

        public override CommandResult Execute()
        {
            string[] configFileObject;
            string pluckedUsername;
            string pluckedPassword;

            // Attempt to read the config.
            try
            {
                // Read.
                configFileObject = File.ReadAllLines(Filename);

                // Pluck the data in order
                pluckedUsername = configFileObject[0];
                pluckedPassword = configFileObject[1];
            }
            catch (Exception e)
            {
                throw InvalidConfigurationFilePath(e.ToString());
            }

            // Build the request
            var request = new AuthenticationRequest(ServiceProvider);
            var body = new Dictionary<string, object>
            {
                {"username", pluckedUsername},
                {"password", pluckedPassword},
                {"serviceScope", Scope}
            };

            // Run it.
            return HandleRequest(null, request, body);
        }

        private Exception InvalidConfigurationFilePath(string msg) => new Exception(msg);
    }
}
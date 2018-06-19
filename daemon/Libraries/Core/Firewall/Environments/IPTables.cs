﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Medallion.Shell;
using Microsoft.Extensions.Logging;
using Spectero.daemon.Libraries.Core.Firewall.Rule;
using Spectero.daemon.Libraries.Core.ProcessRunner;

namespace Spectero.daemon.Libraries.Core.Firewall.Environments
{
    public class IPTables : IFirewallEnvironment
    {
        // Interface to the logger.
        private readonly ILogger<object> _logger;

        // List of active firewall commands.
        private List<NetworkRule> _rules;

        // Templates.
        private const string SNatTemplate = "-t nat POSTROUTING -p TCP -o {interface} -J SNAT --to {address}";
        private const string MasqueradeTemplate = "POSTROUTING -S {network} -o {interface} -J MASQUERADE";

        /// <summary>
        /// Initialize the logger from the firewall handler.
        /// </summary>
        /// <param name="logger"></param>
        public IPTables(ILogger<object> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Add a rule to track.
        /// </summary>
        /// <param name="networkRule"></param>
        /// <exception cref="Exception"></exception>
        public void AddRule(NetworkRule networkRule)
        {
            // Todo: write a function to do this once
            var processOptions = NetworkBuilder.BuildProcessOptions("iptables", true);

            switch (networkRule.Type)
            {
                // MASQUERADE
                case NetworkRuleType.Masquerade:
                    processOptions.Arguments = ("-A " + NetworkBuilder.BuildTemplate(MasqueradeTemplate, networkRule)).Split(" ");
                    break;

                // SNAT
                case NetworkRuleType.SourceNetworkAddressTranslation:
                    processOptions.Arguments = ("-A " + NetworkBuilder.BuildTemplate(SNatTemplate, networkRule)).Split(" ");
                    break;

                // Unhandled Exception
                default:
                    throw FirewallExceptions.UnhandledNetworkRuleException();
            }

            //TODO: Implement Process Execution

            // Track the rule.
            _rules.Add(networkRule);
        }

        /// <summary>
        /// Delete a rule from the tracked objects.
        /// </summary>
        /// <param name="networkRule"></param>
        /// <exception cref="Exception"></exception>
        public void DeleteRule(NetworkRule networkRule)
        {
            var processOptions = NetworkBuilder.BuildProcessOptions("iptables", true);

            switch (networkRule.Type)
            {
                // MASQUERADE
                case NetworkRuleType.Masquerade:
                    processOptions.Arguments = ("-D " + NetworkBuilder.BuildTemplate(MasqueradeTemplate, networkRule)).Split(" ");
                    break;

                // SNAT
                case NetworkRuleType.SourceNetworkAddressTranslation:
                    processOptions.Arguments = ("-D " + NetworkBuilder.BuildTemplate(SNatTemplate, networkRule)).Split(" ");
                    break;

                // Unhandled Exception
                default:
                    throw FirewallExceptions.UnhandledNetworkRuleException();
            }

            //TODO: Implement Process Execution

            // Forget the rule.
            _rules.Remove(networkRule);
        }

        // ANYTHING BEYOND THIS POINT WILL BE REFACTORED.

        public NetworkRule Masquerade(string network, string networkInterface)
        {
            // Define the rule
            var rule = new NetworkRule()
            {
                Type = NetworkRuleType.Masquerade,
                Network = network,
                Interface = network
            };

            // Add the rule safely.
            AddRule(rule);

            // Return the rule if the user wants it.
            return rule;
        }

        public void DisableMasquerade(NetworkRule networkRule)
        {
            // Check if we have the right rule.
            if (networkRule.Type != NetworkRuleType.Masquerade)
                throw FirewallExceptions.NetworkRuleMismatchException();

            // Delete the rule
            DeleteRule(networkRule);
        }

        public NetworkRule SourceNetworkAddressTranslation(string network, string networkInterface)
        {
            // Define the rule
            var rule = new NetworkRule()
            {
                Type = NetworkRuleType.Masquerade,
                Network = network,
                Interface = network,
                Protocol = NetworkRuleProtocol.Tcp
            };

            // Add the rule safely.
            AddRule(rule);

            // Return the rule if the user wants it.
            return rule;
        }

        public void DisableSourceNetworkAddressTranslation(NetworkRule networkRule)
        {
            // Check if we have the right rule.
            if (networkRule.Type != NetworkRuleType.SourceNetworkAddressTranslation)
                throw FirewallExceptions.NetworkRuleMismatchException();

            // Safely delete the rule.
            DeleteRule(networkRule);
        }

        public InterfaceInformation GetDefaultInterface()
        {
            var cmd = Command.Run("ip", "r g 8.8.8.8");
            var splitShellResponse = cmd.StandardOutput.GetLines().ToList()[0].Split(" ");

            return new InterfaceInformation()
            {
                Name = splitShellResponse[4],
                Address = splitShellResponse[6]
            };
        }

        /// <summary>
        /// Simple getter function to return the list of rules.
        /// </summary>
        /// <returns></returns>
        public List<NetworkRule> GetNetworkRules() => _rules;
    }
}
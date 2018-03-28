﻿using NClap.Metadata;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectero.daemon.CLI.Requests;
using System;

namespace Spectero.daemon.CLI.Commands
{
    public class GetSystemHeartbeat : BaseJob
    {
        public override CommandResult Execute()
        {
            var request = new GetSystemHeartbeatRequest(ServiceProvider);

            var response = request.Perform();

            string json = JsonConvert.SerializeObject(response);

            string jsonFormatted = JValue.Parse(json).ToString(Formatting.Indented);

            Console.WriteLine(jsonFormatted);

            return CommandResult.Success;
        }
    }
}
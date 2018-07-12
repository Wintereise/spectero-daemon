﻿using System;
using System.Collections.Generic;
using NClap.Metadata;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectero.daemon.CLI.Requests;
using Spectero.daemon.Libraries.Core.HTTP;

namespace Spectero.daemon.CLI.Commands
{
    public abstract class BaseJob : SynchronousCommand
    {
        protected readonly IServiceProvider ServiceProvider 
            = Startup.GetServiceProvider();

        protected static CommandResult HandleRequest(Action<APIResponse> action, IRequest request,
            Dictionary<string, object> requestBody = null)
        {
            try
            {
                var response = request.Perform(requestBody);
                DisplayResult(response);

                action?.Invoke(response);

                return CommandResult.Success;
            }
            catch (Exception e)
            {
                if (! AppConfig.Debug)
                    Console.WriteLine("Failed :(: " + e.Message);
                else
                    Console.WriteLine(e);

                return CommandResult.RuntimeFailure;
            }
        }

        private static void DisplayResult(APIResponse response)
        {
            if (response.Errors != null && response.Errors?.Count != 0)
            {
                Console.WriteLine("Something went wrong :(:");
                foreach (var error in response.Errors)
                {
                    Console.WriteLine(error.Key + ":" + error.Value);
                }
            }
            else
            {
                var json = JsonConvert.SerializeObject(response.Result);
                var jsonFormatted = JToken.Parse(json).ToString(Formatting.Indented);

                Console.WriteLine(jsonFormatted);
            }
        }
    }
}
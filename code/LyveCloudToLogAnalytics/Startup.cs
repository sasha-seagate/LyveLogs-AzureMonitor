using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

[assembly: FunctionsStartup(typeof(LyveCloudToLogAnalytics.Startup))]
namespace LyveCloudToLogAnalytics
{
    public class Startup : FunctionsStartup
    {
        // override configure method
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // set up Azure Log Analytics client
            builder.Services.AddSingleton<IAzureLogAnalyticsClient>(svc =>
            {
                var client = new AzureLogAnalyticsClient(
                  Environment.GetEnvironmentVariable("LogAnalyticsWorkspaceID"),
                  Environment.GetEnvironmentVariable("LogAnalyticsKey"));
                client.TimeStampField = Environment.GetEnvironmentVariable("LogAnalyticsTimestampField");
                return client;
            });
        }
    }
}

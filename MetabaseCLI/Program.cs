using System;
using System.IO;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MetabaseCLI.Entities;
using System.CommandLine.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Serilog;
using Serilog.Core;

namespace MetabaseCLI
{
    class Program
    {

        static int Main(string[] args)
        {
            var host = Host
                .CreateDefaultBuilder()
                .ConfigureAppConfiguration(conf => conf.AddInMemoryCollection())
                .ConfigureServices(ConfigureServices)
                .UseSerilog()
                .Build();
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(host.Services.GetRequiredService<IConfiguration>())
                .MinimumLevel.ControlledBy(host.Services.GetRequiredService<LoggingLevelSwitch>())
                .CreateLogger();
            return host.Services
                .GetService<MetabaseCLIBuilder>()?
                .Build()
                .Invoke(args)??1;
        }

        static void ConfigureServices(IServiceCollection services)
        {
            Assembly
                .GetEntryAssembly()?
                .GetTypesAssignableFrom<EntityFactory>()
                .ToList()
                .ForEach(t => services.AddSingleton(typeof(EntityFactory), t));
            Assembly
                .GetEntryAssembly()?
                .GetTypesAssignableFrom<ICommandBuilder>()
                .Where(t => t != typeof(EntityFactory))
                .ToList()
                .ForEach(t => services.AddSingleton(typeof(ICommandBuilder), t));
            services
                .AddSingleton<CollectionFactory>()
                .AddSingleton<MetabaseCLIBuilder>()
                .AddSingleton<SessionCredentials>()
                .AddSingleton<LoggingLevelSwitch>()
                .AddSingleton<Session>();
        }
    }
}

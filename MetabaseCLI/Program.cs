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

namespace MetabaseCLI
{
    class Program
    {
        private static readonly Option ServerOption = new Option<string>(
            aliases: new string[] {"--server", "-s"},
            getDefaultValue: () => Environment.GetEnvironmentVariable("MB_SERVER")??"",
            description: "The url of the metabase server"
        ) {IsRequired = true};
        private static readonly Option UserNameOption = new Option<string>(
            aliases: new string[] {"--user", "-u"},
            getDefaultValue: () => Environment.GetEnvironmentVariable("MB_USERNAME")??"",
            description: "The username used on server calls"
        ) {IsRequired = true};

        private static readonly Option PasswordOption = new Option<string>(
            aliases: new string[] {"--password", "-p"},
            getDefaultValue: () => Environment.GetEnvironmentVariable("MB_PASSWORD")??"",
            description: "The password used on server calls"
        ) {IsRequired = true};
        static Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand(
                "Simple communication system between local files and a given metabase server."
            )
            {
                ServerOption,
                UserNameOption,
                PasswordOption
            };
            
            var factories = new  List<EntityFactory>{
                new CollectionFactory(),
                new CardFactory(),
                new DashboardFactory(),
                new PulseFactory()
            };

            rootCommand
                .AppendCommand(CommandBuilder.BuildAuthCommand())
                .AppendFactoriesCommand(factories)
                .AppendCommand(CommandBuilder.BuildPullCommand(
                    (CollectionFactory)(factories.First()), factories.Skip(1)
                ))
                .AppendCommand(CommandBuilder.BuildPushCommand(
                    (CollectionFactory)(factories.First()), factories.Skip(1)
                ));
            return rootCommand.InvokeAsync(args);
        }
    }
}

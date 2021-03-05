using System.Linq;


using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using MetabaseCLI.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Serilog.Core;

namespace MetabaseCLI
{

    internal class MetabaseCLIBuilder
    {
        public IEnumerable<EntityFactory> Factories { get; private set; }
        public CollectionFactory CollectionFactory { get; private set; }
        public ILoggerFactory LoggerFactory { get; private set; }
        public LoggingLevelSwitch LoggingLevelSwitch { get; private set; }
        public IEnumerable<ICommandBuilder> CommandBuilders { get; private set; }
        public SessionCredentials SessionCredentials { get; private set; }

        public MetabaseCLIBuilder(
            IEnumerable<EntityFactory> factories,
            IEnumerable<ICommandBuilder> commandBuilders,
            CollectionFactory collectionFactory,
            LoggingLevelSwitch loggingLevelSwitch,
            SessionCredentials sessionCredentials,
            ILoggerFactory loggerFactory
        )
        {
            Factories = factories;
            CollectionFactory = collectionFactory;
            LoggingLevelSwitch = loggingLevelSwitch;
            LoggerFactory = loggerFactory;
            CommandBuilders = commandBuilders;
            SessionCredentials = sessionCredentials;
        }

        public RootCommand Build()
        {
            var builder = new CommandLineBuilder(
                new RootCommand(
                    "Simple communication system between local files and a given metabase server")
                )
                .AddOption(ArgumentBuilder.SetServerOption(SessionCredentials))
                .AddOption(ArgumentBuilder.SetUserNameOption(SessionCredentials))
                .AddOption(ArgumentBuilder.SetPasswordOption(SessionCredentials))
                .AddOption(ArgumentBuilder.SetVerbosityOption(LoggingLevelSwitch));
            CommandBuilders.ToList().ForEach(b =>  builder.AddCommand(b.Build()));
            return (RootCommand)builder.Command;
        }
    }

}
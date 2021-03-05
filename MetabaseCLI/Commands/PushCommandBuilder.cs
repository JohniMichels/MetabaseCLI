

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MetabaseCLI.Entities;
using Microsoft.Extensions.Logging;

namespace MetabaseCLI
{
    public class PushCommandBuilder : ICommandBuilder
    {

        public IEnumerable<EntityFactory> Factories { get; private set;}

        public CollectionFactory CollectionFactory { get; private set; }
        public ILogger Logger { get; set; }

        public PushCommandBuilder(
            IEnumerable<EntityFactory> factories,
            CollectionFactory collectionFactory,
            ILogger<PushCommandBuilder> logger)
        {
            Factories = factories;
            CollectionFactory = collectionFactory;
            Logger = logger;
        }
        
        public Command Build()
        {
            var pushCommand = new Command(
                "push",
                $"Pushes {string.Join(", ", Factories.Select(c => c.Name + "s"))} and " +
                "collections from specified directory into the server"
            )
            {
                Handler =
                    CommandHandler.Create(
                    (
                        bool includePersonal,
                        bool includeArchived,
                        DirectoryInfo localPath,
                        int? customRoot,
                        Regex excludePattern
                    ) =>
                        CommandBuilder.ExecutePush(
                            CollectionFactory,
                            Factories.Where(f => f.GetType() != typeof(CollectionFactory)),
                            includePersonal,
                            includeArchived,
                            localPath,
                            customRoot, 
                            excludePattern,
                            Logger
                        ) 
                )
            }.AddCollectionFilteringOptions()
            .AddInternalPathArgument();
            return pushCommand;
        }
    }
}
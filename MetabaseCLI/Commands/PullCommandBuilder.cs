

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
    public class PullCommandBuilder : ICommandBuilder
    {

        public IEnumerable<EntityFactory> Factories { get; private set;}

        public CollectionFactory CollectionFactory { get; private set; }

        public ILogger Logger { get; private set; }
        public PullCommandBuilder(
            IEnumerable<EntityFactory> factories,
            CollectionFactory collectionFactory,
            ILogger<PullCommandBuilder> logger)
        {
            Factories = factories;
            CollectionFactory = collectionFactory;
            Logger = logger;
        }
        
        public Command Build()
        {
            var pullCommand = new Command(
                "pull",
                $"Pulls {string.Join(", ", Factories.Select(c => c.Name + "s"))} and " +
                "collections from the server into the specified directory using the server collection structure"
            )
            {
                Handler =
                    CommandHandler.Create(
                    async (
                        bool includePersonal,
                        bool includeArchived,
                        DirectoryInfo localPath,
                        int? customRoot,
                        Regex excludePattern
                    ) =>
                        await Logger.SafeHandleCommand(
                            "pull",
                            () => CommandBuilder.ExecutePull(
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
                )
            }.AddCollectionFilteringOptions()
            .AddInternalPathArgument();
            return pullCommand;
        }
    }
}
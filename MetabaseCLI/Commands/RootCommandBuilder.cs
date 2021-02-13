

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MetabaseCLI.Entities;
using Newtonsoft.Json;

namespace MetabaseCLI
{
    public static partial class CommandBuilder
    {
        internal static Command BuildAuthCommand()
        {
            return new Command(
                "auth",
                "Gets a X-Metabase-Session token for requests to the server."
            )
            {
                Handler = CommandHandler.Create(
                    async (Session session, IConsole console) =>
                        console.Out.WriteLine(await session.InvalidateSession())
                )
            };
        }

        private static IDictionary<int, string> IdToPath(
            CollectionFactory collectionFactory,
            int? customRoot,
            bool includePersonal,
            bool includeArchived,
            Regex excludePattern,
            Session session
        )
        {
            var serverRequest = collectionFactory.Get(session)
                .Select(c => (
                    location: (string)((c.TryGetValue("location", out var l) ? l : "").TrimEnd('/') + $"/{c["id"]}") + "/",
                    id: (int?)(int.TryParse(c["id"].ToString(), out int i) ? i : null),
                    owner: (c.TryGetValue("personal_owner_id", out var o) ? (int?)o : null),
                    name: (string)(c["name"]),
                    archived: (bool)(c.TryGetValue("archived", out var a) ? a : false)
                    )
                ).ToListObservable();
            var collections = serverRequest
                .Where(c => c.id.HasValue)
                .ToDictionary(c => c.id!.Value, c => c);
            return collections
                .Select(
                    kv => (
                        id: kv.Key,
                        path: "/" + string.Join('/', kv.Value.location.Split('/', StringSplitOptions.RemoveEmptyEntries)
                            .Select(a => collections[int.Parse(a)].name.Replace("/", "_\\_") )) + "/",
                        isPersonalOwned: kv.Value.location.Split('/', StringSplitOptions.RemoveEmptyEntries)
                            .Any(a => collections[int.Parse(a)].owner.HasValue),
                        isArchived: kv.Value.archived,
                        location: kv.Value.location
                    )
                )
                .Where(c => !customRoot.HasValue || c.location.Contains($"/{customRoot}/") || c.id == customRoot.Value)
                .Where(c => customRoot.HasValue || includePersonal || !c.isPersonalOwned)
                .Where(c => includeArchived || !c.isArchived)
                .Where(c => !excludePattern.IsMatch(c.path))
                .ToDictionary(item => item.id, item => item.path);
        }

        private async static Task ExecutePull(
            CollectionFactory collectionFactory,
            IEnumerable<EntityFactory> targetFactories,
            Session session,
            bool includePersonal,
            bool includeArchived,
            DirectoryInfo localPath,
            int? customRoot,
            Regex excludePattern,
            IConsole console
        )
        {
            localPath.Create();
            var basePath = localPath.FullName;
            localPath.Delete(true);
            var idPaths = IdToPath(collectionFactory, customRoot, includePersonal, includeArchived, excludePattern, session);
            idPaths.Values.Select(
                f => basePath.TrimEnd('/') + f
            ).ToList().ForEach(f => Directory.CreateDirectory(f));
            await targetFactories.Select(
                f => f.Get(session).Select(i => (type: f.Name, item: i))
                ).ToObservable().Merge()
                .Where(item =>
                    (!customRoot.HasValue && !((int?)(item.item["collection_id"])).HasValue) ||
                    idPaths.ContainsKey((int?)(item.item["collection_id"]) ?? -1))
                .Where(item => includeArchived || !(bool)(item.item["archived"]))
                .Select(item =>
                    {
                        var itemId = (int)(item.item["id"]);
                        var itemName = ((string)(item.item["name"])).Replace("/", "_\\_");
                        var itemCollectionId = (int?)(item.item["collection_id"]);
                        var targetFolder = (
                                idPaths.ContainsKey(itemCollectionId ?? -1) ?
                                basePath.TrimEnd('/') + idPaths[itemCollectionId ?? -1] :
                                basePath
                            ).TrimEnd('/');
                        var targetPath = $"{targetFolder}/{itemId}.{itemName}.mb{item.type}";
                        return (path: targetPath, item: item.item);
                    }
                )
                .Where(item => !excludePattern.IsMatch(item.path))
                .Do(
                    item =>
                    {
                        using (var f = File.CreateText(item.path))
                        {
                            f.WriteLine(
                                JsonConvert.SerializeObject(item.item, Formatting.Indented)
                            );
                        }
                    }
                );
        }

        internal static Command BuildPullCommand(
            CollectionFactory collectionFactory,
            IEnumerable<EntityFactory> targetFactories
        )
        {
            var pullCommand = new Command(
                "pull",
                $"Pulls {string.Join(", ", targetFactories.Select(c => c.Name + "s"))} and " +
                "collections from the server into the specified directory using the server collection structure"
            )
            {
                Handler =
                    CommandHandler.Create(
                    (
                        Session session,
                        bool includePersonal,
                        bool includeArchived,
                        DirectoryInfo localPath,
                        int? customRoot,
                        Regex excludePattern,
                        IConsole console
                    ) =>
                        ExecutePull(
                            collectionFactory,
                            targetFactories,
                            session,
                            includePersonal,
                            includeArchived,
                            localPath,
                            customRoot, 
                            excludePattern, 
                            console
                        ) 
                )
            }.AddCollectionFilteringOptions()
            .AddInternalPathArgument();
            return pullCommand;
        }

        private static void ExecutePush(
            CollectionFactory collectionFactory,
            IEnumerable<EntityFactory> targetFactories,
            Session session,
            bool includePersonal,
            bool includeArchived,
            DirectoryInfo localPath,
            int? customRoot,
            Regex excludePattern,
            IConsole console
        )
        { 
            var basePath = localPath.FullName;
            var idPaths = IdToPath(collectionFactory, customRoot, includePersonal, includeArchived, excludePattern, session);
            var pathId = idPaths.Reverse();
            var localPaths = localPath.GetDirectories("*", SearchOption.AllDirectories).Select(d => d.FullName);

        }

        internal static Command BuildPushCommand(
            CollectionFactory collectionFactory,
            IEnumerable<EntityFactory> targetFactories
        )
        {
            var pushCommand = new Command(
                "push",
                $"Pushes {string.Join(", ", targetFactories.Select(c => c.Name + "s"))} and " +
                "collections from the specified directory into the server"
            )
            {
                Handler = 
                    CommandHandler.Create(
                        (
                            (
                                Session session,
                                bool includePersonal,
                                bool includeArchived,
                                DirectoryInfo localPath,
                                int? customRoot,
                                Regex excludePattern,
                                IConsole console
                            ) =>
                            {
                                ExecutePush(
                                    collectionFactory,
                                    targetFactories,
                                    session,
                                    includePersonal,
                                    includeArchived,
                                    localPath,
                                    customRoot,
                                    excludePattern,
                                    console
                                );
                            }
                        )
                    )
            }.AddCollectionFilteringOptions()
            .AddInternalPathArgument();

            return pushCommand;
        }
    }
}

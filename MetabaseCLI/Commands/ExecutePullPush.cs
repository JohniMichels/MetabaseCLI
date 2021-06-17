

using System;
using System.Collections.Concurrent;
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace MetabaseCLI
{
    public static partial class CommandBuilder
    {

        private static IDictionary<int, string> IdToPath(
            CollectionFactory collectionFactory,
            int? customRoot,
            bool includePersonal,
            bool includeArchived,
            Regex excludePattern
        )
        {
            var serverRequest = collectionFactory.Get(includeArchivedItems: includeArchived)
                .Select(c => (
                    location: (string)((c.TryGetValue("location", out var l) ? l??"" : "").TrimEnd('/') + $"/{c["id"]}") + "/",
                    id: (int?)(int.TryParse((c["id"]??"").ToString(), out int i) ? i : null),
                    owner: c.TryGetValue("personal_owner_id", out var o) ? (int?)o : null,
                    name: (string)(c["name"]??"untitled"),
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
                        kv.Value.location
                    )
                )
                .Where(c => !customRoot.HasValue || c.location.Contains($"/{customRoot}/") || c.id == customRoot.Value)
                .Where(c => customRoot.HasValue || includePersonal || !c.isPersonalOwned)
                .Where(c => !excludePattern.IsMatch(c.path))
                .ToDictionary(item => item.id, item => item.path);
        }

        internal async static Task ExecutePull(
            CollectionFactory collectionFactory,
            IEnumerable<EntityFactory> targetFactories,
            bool includePersonal,
            bool includeArchived,
            DirectoryInfo localPath,
            int? customRoot,
            Regex excludePattern,
            ILogger logger
        )
        {
            logger.LogTrace("Clearing contents inside {LocalPath}", localPath.FullName);
            localPath.Create();
            var basePath = localPath.FullName;
            localPath.Delete(true);

            logger.LogTrace("Retrieving server collection structure");
            var idPaths = IdToPath(
                collectionFactory, customRoot, includePersonal,
                includeArchived, excludePattern);
            logger.LogDebug("Server structure is {@CollectionStructure}", idPaths);

            logger.LogTrace("Starting local replication");
            idPaths.Values.Distinct().Select(
                f => basePath.TrimEnd('/') + f
            ).ToList().ForEach(f => {
                Directory.CreateDirectory(f);
                logger.LogTrace("Created local directory {NewDirectory}", f);
            });
            _ = await targetFactories.Select(
                f => f.Get(includeArchivedItems: includeArchived).Select(i => (type: f.Name, item: i))
                ).ToObservable().Merge()
                .Where(item =>
                    (!customRoot.HasValue && !((int?)item.item["collection_id"]).HasValue) ||
                    idPaths.ContainsKey((int?)item.item["collection_id"] ?? -1))
                .Select(item =>
                    {
                        var itemId = (int)item.item["id"];
                        var itemName = ((string)(item.item["name"] ?? "untitled")).Replace("/", "_\\_");
                        var itemCollectionId = (int?)item.item["collection_id"];
                        var targetFolder = (
                                idPaths.ContainsKey(itemCollectionId ?? -1) ?
                                basePath.TrimEnd('/') + idPaths[itemCollectionId ?? -1] :
                                basePath
                            ).TrimEnd('/');
                        var targetPath = $"{targetFolder}/{itemId}.{itemName}.mb{item.type}";
                        return (path: targetPath, item.item);
                    }
                )
                .Where(item => !excludePattern.IsMatch(item.path))
                .Do(item =>
                    {
                        logger.LogTrace(
                            "Writing to {LocalPath} {EntityType} {EntityId}",
                            item.path,
                            item.path.Split('.').Last().Replace("mb", ""),
                            (int)item.item["id"]
                            );
                        using var f = File.CreateText(item.path);
                        f.WriteLine(
                            JsonConvert.SerializeObject(item.item, Formatting.Indented)
                        );
                    }
                );
        }

        internal static async Task ExecutePush(
            CollectionFactory collectionFactory,
            IEnumerable<EntityFactory> targetFactories,
            bool includePersonal,
            bool includeArchived,
            DirectoryInfo localPath,
            int? customRoot,
            Regex excludePattern,
            ILogger logger
        )
        {
            var basePath = localPath.FullName;
            logger.LogTrace("Retrieving server collection structure");
            var idPaths = IdToPath(collectionFactory, customRoot, includePersonal, includeArchived, excludePattern);
            logger.LogDebug("Server structure is {@CollectionStructure}", idPaths);
            var pathId = idPaths.Reverse();
            logger.LogDebug("Reverse server structure is {@CollectionStructure}", pathId);
            var localPaths = localPath
                .GetDirectories("*", SearchOption.AllDirectories)
                .Select(d => d.FullName.Replace(basePath,"") + "/");
            logger.LogDebug("Local paths is currently {@LocalPaths}", localPaths);

            var targetFactoriesFiles = targetFactories
                .Select(f => (file: $"mb{f.Name}", factory: f))
                .ToDictionary(i => i.file, i => i.factory);
            
            var mbFilePattern = new Regex(
                "\\.(" +
                targetFactoriesFiles.Keys.Join(")|(") +
                ")"
            );
            var factoryIds = targetFactories
                .ToDictionary(
                    f => f,
                    f => new ConcurrentBag<int>()
                )!;

            var padLock = new object();
            var createCollectionsRequest = localPaths
                .OrderBy(p => p)
                .Where(p => !pathId.ContainsKey(p))
                .GroupBy(p => p.Where(c => c == '/').Count())
                .OrderBy(g => g.Key)
                .Select(g => g
                    .ToObservable()
                    .Do(p => {
                        logger.LogInformation($"Creating collection {p}");
                    })
                    .Select(p => collectionFactory.Create(
                        new Dictionary<string, dynamic?>()
                        {
                            {"name", p.Split('/', StringSplitOptions.RemoveEmptyEntries).Last().Replace("_\\_", "/")},
                            {"parent_id", pathId.TryGetValue("/" + p.Split("/", StringSplitOptions.RemoveEmptyEntries).SkipLast(1).Join("/") + "/", out var parentId) ? parentId.First() : null},
                            {"color", "#999999"}
                        }.RemoveParentWhitespace())
                        .Do(r => 
                        {
                            logger.LogInformation($"Created collection {p} with id {r["id"]}");
                            lock (padLock)
                            {
                                pathId.Add(p, (int)r["id"]);
                            }
                        }))
                    .Merge())
                .ToObservable()
                .Concat();
            var archiveNonDefinedPathsRequest = idPaths
                .Values
                .Where(p => !localPaths.Contains(p))
                .SelectMany(p => pathId.TryGetValue(p, out var i) ? i : throw new ArgumentException("The required archive path does not exist"))
                .Select(id => collectionFactory.Archive(id))
                .ToObservable().Merge();

            var archiveMultipleDefinedRequest = pathId
                .Values
                .SelectMany(i => i.Skip(1))
                .Select(id => collectionFactory.Archive(id))
                .ToObservable().Merge();

            var archiveCollectionsRequest = archiveMultipleDefinedRequest
                .Concat(archiveNonDefinedPathsRequest);

            var collectionsRequest = archiveCollectionsRequest
                .Merge(createCollectionsRequest);

            var upsertRequest = localPath
                .GetFiles("*.mb?*", SearchOption.AllDirectories)
                .Where(p => mbFilePattern.IsMatch(p.FullName))
                .Select(f =>
                    {
                        IDictionary<string, dynamic?> content = ArgumentBuilder
                            .ParseContent("", f);
                        var factory = targetFactoriesFiles[f.FullName.Split(".").Last()];
                        int? position = pathId.TryGetValue(
                            (f.Directory?.FullName.Replace(basePath, "")??"") + "/",
                            out var p) ?
                            p.First() :
                            null;
                        content = factory.AtPosition(
                            content,
                            position
                        );
                        return (content.ContainsKey("id") ?
                            factory.Update(content, (int)content["id"]) :
                            factory.Create(content))
                            .Select(r => (factory, entity: r));
                    }
                )
                .ToObservable()
                .Merge()
                .Do(item => factoryIds[item.factory].Add((int)item.entity["id"]));

            var archiveRequest = targetFactories.ToObservable()
                .SelectMany(f => f.Get().Select(entity => (factory: f, entity)))
                .Where(item => 
                    pathId.Values.SelectMany(v => v).Contains(((int?)item.entity[item.factory.CollectionField])??-1)
                    || (item.entity[item.factory.CollectionField] == customRoot)
                )
                .Where(item => !factoryIds[item.factory].Contains((int)item.entity["id"]))
                .SelectMany(item => item.factory.Archive((int)item.entity["id"]));

            var entitiesRequest = upsertRequest
                .Select(item => item.entity)
                .Concat(archiveRequest);

            var entireRequest = collectionsRequest.Concat(entitiesRequest);

            await entireRequest;

        }

    }
}

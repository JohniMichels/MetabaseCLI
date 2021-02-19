
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace MetabaseCLI
{
    public static class Utils
    {
        public static IDictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> source
        )
        where TKey: notnull 
        => source.ToDictionary(kv => kv.Key, kv => kv.Value);

        public static IDictionary<TKey, TValue> FilterKeys<TKey, TValue>(
            this IDictionary<TKey, TValue> source,
            IEnumerable<TKey> whiteList
        )
        where TKey: notnull
        => source.Where(kv => whiteList.Contains(kv.Key)).ToDictionary();

        internal static IDictionary<TKey, TValue> Merge<TKey, TValue>(
            this IDictionary<TKey, TValue> source,
            IDictionary<TKey, TValue> target
        )
        where TKey: notnull
        {
            return source.Keys.Union(target.Keys)
                .ToDictionary(k => k, k => target.ContainsKey(k)? target[k]: source[k]);
        }

        public static RootCommand AppendFactoriesCommand(
            this RootCommand baseCommand, 
            IEnumerable<EntityFactory> factories
        )
        {
            factories.ToList().ForEach(f => baseCommand.Add(f.GenerateCommand()));
            return baseCommand;
        }

        public static T AppendCommand<T>(
            this T baseCommand,
            Command command
        )
        where T: Command
        {
            baseCommand.AddCommand(command);
            return baseCommand;
        }

        public static T ChoiceBetweenValidate<T>(
            this T command,
            params Symbol[] items
        )
        where T: Command => 
            command.ChooseNFrom(1, 1, items);

        public static T MutuallyExclusiveValidate<T>(
            this T command,
            params Symbol[] items
        )
        where T : Command =>
            command.ChooseNFrom(0, 1, items);

        public static T ChooseNFrom<T>(
            this T command,
            int minItems,
            int maxItems,
            params Symbol[] items
        )
        where T : Command
        {
            command.AddValidator(r =>
                (
                    items.Select(i =>
                        i switch {
                            IArgument arg => (SymbolResult?)r.FindResultFor(arg),
                            IOption opt => (SymbolResult?)r.FindResultFor(opt),
                            ICommand com => (SymbolResult?)r.FindResultFor(com),
                            _ => throw new ArgumentException("Only arguments, options and commands are allowed")
                        }).Where(s => r.Children.Contains(s)).Count()
                ) switch {
                    int v when ((v >= minItems) && (v <= maxItems)) => null,
                    _ => "Use either " +
                        string.Join(", ", items.SkipLast(1).Select(t => t.Name)) +
                        " or " + items.Last().Name
                }
            );
            return command;
        }

        internal static IDictionary<string, dynamic?> RemoveParentWhitespace(this IDictionary<string, dynamic?> entity)
        {
            if (entity.TryGetValue("parent_id", out var parentId) && (parentId == null || ((int)parentId == 0)))
            {
                entity.Remove("parent_id");
            }
            return entity;
        }

        internal static string Join(this IEnumerable<string> source, string separator)
            => string.Join(separator, source);

        internal static IDictionary<TValue, TKey> Reverse<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary
        )
        where TValue : notnull
        {
            return dictionary.ToDictionary(kv => kv.Value, kv => kv.Key);
        }

        internal static IDictionary<TKey, TValue> RemoveNullValues<TKey, TValue>(
            this IDictionary<TKey, TValue?> dictionary
        )
        where TKey : notnull
        => dictionary.Where(kv => kv.Value == null).ToDictionary(kv => kv.Key, kv => kv.Value!);
    }
}
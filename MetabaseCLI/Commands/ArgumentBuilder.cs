

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace MetabaseCLI
{
    public static partial class ArgumentBuilder
    {

        internal static T AddIdArgument<T>(this T command, string entity, IArgumentArity arity)
        where T : Command
        {
            var arg = new Argument<int>(
                "id",
                $"The {entity} server id"
            )
            {
                Arity = arity
            };
            command.Add(arg);
            return command;
        }

        internal static T AddFileAndStringContent<T>(
            this T command,
            string entity,
            bool useFileAsOption = false)
        where T : Command
        {
            var stringContent = CreateStringContentOption(entity);
            Symbol fileContent = CreateFileContent(entity, useFileAsOption);
            command.Add(stringContent);
            command.Add(fileContent);
            command.ChoiceBetweenValidate(fileContent, stringContent);
            return command;
        }

        internal static T AddInternalPathArgument<T>(
            this T command,
            IArgumentArity? arity = null
        )
        where T : Command
        {
            command.Add(
                new Argument<DirectoryInfo>(
                    "local-path",
                    () => new DirectoryInfo(Directory.GetCurrentDirectory()),
                    "Target path to clone server structure"
                )
                {
                    Arity = arity??ArgumentArity.ExactlyOne
                }.LegalFilePathsOnly()
            );
            return command;
        }

        private static Symbol CreateFileContent(string entity, bool useOption)
        {
            Symbol result = useOption ?
                new Argument<FileInfo>("file-content", $"A file that has the json content of the {entity}")
                {
                    Arity = ArgumentArity.ZeroOrOne
                }.ExistingOnly() :
                new Option<FileInfo>(
                    new[] { "-f", "--file-content" },
                    $"A file that has the json content of the {entity}"
                )
                {
                    IsRequired = false
                }.ExistingOnly();
            string? ValidateFile(SymbolResult r) =>
                ((
                    new JsonSerializer().Deserialize(
                        new FileInfo(r.Tokens.First().Value).OpenText(),
                        typeof(IDictionary<string, dynamic>)) is IDictionary<string, dynamic> content
                )
                && content.Count() > 0
                ) ?
                null :
                $"The content in {r.Tokens.First().Value} is not a valid {entity}";

            (result switch
            {
                Option opt => (Action)(() => opt.AddValidator(ValidateFile)),
                Argument arg => (Action)(() => arg.AddValidator(ValidateFile)),
                _ => (Action)(() => throw new ArgumentException("Only valid for arguments and options"))
            })();
            return result;
        }

        private static Option CreateStringContentOption(string entity)
        {
            var result = new Option<string>(
                new [] {"-c", "--string-content"},
                $"A string that has the json content of the {entity}"
            ) {
                IsRequired = false
            };
            result.AddValidator(r => (
                JsonConvert.DeserializeObject<IDictionary<string, dynamic>>(r.Tokens.First().Value)
                is IDictionary<string, dynamic> content
                && content.Count() > 0 ?
                null :
                $"The given string is not a valid {entity}"
            ));
            return result;
        }
        internal static IDictionary<string, dynamic> ParseContent(string content, FileInfo file)
        {
            return !string.IsNullOrWhiteSpace(content) ?
                JsonConvert.DeserializeObject<IDictionary<string, dynamic>>(content) :
                (IDictionary<string, dynamic>?)(
                    new JsonSerializer().Deserialize(
                        file.OpenText(),
                        typeof(IDictionary<string, dynamic>)
                    )
                )!;
        }

        internal static T AddCollectionFilteringOptions<T>(this T command)
        where T : Command
        {
            var includePersonalCollectionsOption =
                new Option<bool>(
                    aliases: new[] {"-p","--include-personal"},
                    description: "Include personal collections on the pull, this option is ignored " +
                    "when the custom root collection option is used"
                );
            var includeArchived =
                new Option<bool>(
                    aliases: new[] {"-a", "--include-archived" },
                    description: "Include archived collections on the pull"
                );
            var customRootCollectionOption =
                new Option<int?>(
                    aliases: new[] {"-r", "--custom-root" },
                    description: "Defines a custom server id as root collection for the pull"
                );
            var filterPatternOption =
                new Option<string>(
                    aliases: new[] {"-e", "--exclude-pattern" },
                    description: "Exclude all entities path/name matching this regex pattern",
                    getDefaultValue: () => "^$"
                )
                {
                    IsRequired = false
                };
            command.Add(includePersonalCollectionsOption);
            command.Add(includeArchived);
            command.Add(customRootCollectionOption);
            command.Add(filterPatternOption);
            command.MutuallyExclusiveValidate(
                includePersonalCollectionsOption,
                customRootCollectionOption
                );
            return command;
        }
    }
}

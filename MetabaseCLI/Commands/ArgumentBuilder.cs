

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog.Core;
using Serilog.Events;

namespace MetabaseCLI
{
    public static partial class ArgumentBuilder
    {

        internal static Option<string> SetServerOption(SessionCredentials sessionCredentials) =>
            SetValueOption(sessionCredentials, "Server", "url");
        internal static Option<string> SetUserNameOption(SessionCredentials sessionCredentials) =>
            SetValueOption(sessionCredentials, "UserName");

        internal static Option SetPasswordOption(SessionCredentials sessionCredentials) =>
            SetValueOption(sessionCredentials, "Password");

        private static Option<string> SetValueOption(SessionCredentials sessionCredentials, string name, string? customDescription = null) =>
            new(
                aliases: new string[] { "-" + name.First().ToString().ToLower(), "--" + name.ToLower() },
                description: $"The {customDescription ?? name.ToLower()} used on server calls",
                parseArgument: result =>
                {
                    var value = result.Tokens[0].Value;
                    value = string.IsNullOrWhiteSpace(value) ?
                        Environment.GetEnvironmentVariable($"MB_{name.ToUpper()}") ?? "" :
                        value;
                    sessionCredentials[name] = value;
                    return value;
                }
            ){IsRequired = true};


        internal static Option SetVerbosityOption(LoggingLevelSwitch loggingLevelSwitch) 
        => new Option<int>(
            aliases: new[] { "-v", "--verbose" },
            description: "Defines the verbosity of the logger",
            parseArgument: result =>
            {
                var minLevel = (LogEventLevel)Math.Max((int)LogEventLevel.Error - int.Parse(result.Tokens[0].Value), 0);
                loggingLevelSwitch.MinimumLevel = minLevel;
                return int.Parse(result.Tokens[0].Value);
            }
        )
        {
            AllowMultipleArgumentsPerToken = true
        };

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
                        new FileInfo(r.Tokens[0].Value).OpenText(),
                        typeof(IDictionary<string, dynamic?>)) is IDictionary<string, dynamic?> content
                )
                && content.Count > 0
                ) ?
                null :
                $"The content in {r.Tokens[0].Value} is not a valid {entity}";

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
                JsonConvert.DeserializeObject<IDictionary<string, dynamic?>>(r.Tokens[0].Value)
                is IDictionary<string, dynamic?> content
                && content.Count > 0 ?
                null :
                $"The given string is not a valid {entity}"
            ));
            return result;
        }
        internal static IDictionary<string, dynamic?> ParseContent(string content, FileInfo file)
        {
            return JsonConvert.DeserializeObject<IDictionary<string, dynamic?>>(
                !string.IsNullOrEmpty(content) ?
                content :
                file.OpenText().ReadToEnd()
            );
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

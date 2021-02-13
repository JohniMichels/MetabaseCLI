using System.Threading;
using System.Linq.Expressions;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Binding;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using System.CommandLine.IO;
using System.IO;
using MetabaseCLI.Entities;

namespace MetabaseCLI
{
    public static class EntityFactoryExtensions
    {

        public static Command GenerateCommand(
            this EntityFactory factory
        )
        {
            var entityCommand = new Command(
                factory.Name, $"Base command for {factory.Name} operations"
            );
            entityCommand.AddCommand(factory.BuildGetCommand());
            entityCommand.AddCommand(factory.BuildCreateCommand());
            entityCommand.AddCommand(factory.BuildUpdateCommand());
            entityCommand.AddCommand(factory.BuildDeleteCommand());
            entityCommand.AddCommand(factory.BuildArchiveCommand());
            return entityCommand;
        }

    }
}
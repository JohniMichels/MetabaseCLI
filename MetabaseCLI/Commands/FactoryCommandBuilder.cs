

using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Reactive.Linq;
using Newtonsoft.Json;

namespace MetabaseCLI
{
    public static partial class CommandBuilder
    {
        internal static Command BuildGetCommand(this EntityFactory factory)
        {
            return new Command(
                "get", 
                $"Get a {factory.Name} from the server when id is given, or all if id is not given"
            )
            {
                Handler = CommandHandler.Create(
                    async (Session session, int? id, IConsole console) =>
                        console.Out.Write(
                            JsonConvert.SerializeObject(
                                !id.HasValue ?
                                factory.Get(session).ToListObservable() :
                                await factory.Get(session, id.Value),
                                Formatting.Indented
                            )
                        )
                    )
            }.AddIdArgument(
                factory.Name,
                ArgumentArity.ZeroOrOne
            );
        }

        internal static Command BuildCreateCommand(this EntityFactory factory)
        {
            return new Command(
                "create",
                $"Creates a {factory.Name}"
            )
            {
                Handler = CommandHandler.Create(
                    async (Session session, string stringContent, FileInfo fileContent, IConsole console) =>
                        await factory.Create(
                            session,
                            ArgumentBuilder.ParseContent(stringContent, fileContent))
                )
            }.AddFileAndStringContent(
                factory.Name
            );
        }

        internal static Command BuildUpdateCommand(this EntityFactory factory)
        {
            return new Command(
                "update",
                $"Updates the {factory.Name} with the given id"
            )
            {
                Handler = CommandHandler.Create(
                    async (Session session, string stringContent, FileInfo fileContent, int id, IConsole console) =>
                        await factory.Update(
                            session,
                            ArgumentBuilder.ParseContent(stringContent, fileContent),
                            id)
                )
            }.AddIdArgument(
                factory.Name,
                ArgumentArity.ExactlyOne
            ).AddFileAndStringContent(
                factory.Name,
                true
            );
        }

        internal static Command BuildDeleteCommand(this EntityFactory factory)
        {
            return new Command(
                "delete",
                $"Deletes the {factory.Name} with the given id if not already deleted"
            )
            {
                Handler = CommandHandler.Create(
                    async (Session session, int id, IConsole console) =>
                        await factory.Delete(session, id)
                )
            }.AddIdArgument(
                factory.Name,
                ArgumentArity.ExactlyOne
            );
        }

        internal static Command BuildArchiveCommand(this EntityFactory factory)
        {
            return new Command(
                "archive",
                $"Archives the {factory.Name} with the given id if not already archived"
            )
            {
                Handler = CommandHandler.Create(
                    async (Session session, int id, IConsole console) =>
                        await factory.Archive(session, id)
                )
            }.AddIdArgument(
                factory.Name,
                ArgumentArity.ExactlyOne
            );
        }
    }
}

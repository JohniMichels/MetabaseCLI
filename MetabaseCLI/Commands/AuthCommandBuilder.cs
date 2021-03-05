

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using Microsoft.Extensions.Logging;
using System.Reactive.Linq;
using System;

namespace MetabaseCLI
{
    public class AuthCommandBuilder : ICommandBuilder
    {

        public ILogger Logger { get; set; }
        public Session Session { get; set; }
        public AuthCommandBuilder(
            ILogger<AuthCommandBuilder> logger,
            Session session)
        {
            Logger = logger;
            Session = session;
        }
        public Command Build()
        {
            return new Command(
                "auth",
                "Gets a X-Metabase-Session token for requests to the server."
            )
            {
                Handler = CommandHandler.Create(
                    async(IConsole console) =>
                    {
                        return await Logger.SafeHandleCommand(
                            "auth",
                            async () => console.Out.WriteLine(
                                await Session.InvalidateSession())
                        );
                    }
                )
            };
        }
    }
}
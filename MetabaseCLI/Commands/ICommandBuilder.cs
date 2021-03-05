

using System.CommandLine;

namespace MetabaseCLI
{
    public interface ICommandBuilder
    {
        Command Build();

    }
}
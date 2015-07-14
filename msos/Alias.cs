using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb(".newalias", HelpText =
        "Creates a new command alias. The alias itself can accept parameters specified as " + 
        "$1, $2, ... in the command body. For example: .newalias dt !dumpheap --stat --type $1")]
    class CreateAlias : ICommand
    {
        [Value(0, Required = true)]
        public string AliasName { get; set; }

        [Value(1, Required = true, Min = 1)]
        public IEnumerable<string> AliasCommand { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (context.Aliases.ContainsKey(AliasName))
            {
                context.WriteError("The specified alias already exists. Clear it first with .rmalias.");
                return;
            }
            context.Aliases.Add(AliasName, String.Join(" ", AliasCommand.ToArray()));
        }
    }

    [Verb("%", HelpText = "Executes an existing command alias.")]
    class ExecuteAlias : ICommand
    {
        [Value(0, Required = true)]
        public string AliasName { get; set; }

        [Value(1, Required = false)]
        public IEnumerable<string> AliasParameters { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            string aliasCommand;
            if (!context.Aliases.TryGetValue(AliasName, out aliasCommand))
            {
                context.WriteError("Unknown alias '{0}'", AliasName);
                return;
            }
            int index = 1;
            foreach (var paramValue in AliasParameters)
            {
                aliasCommand = aliasCommand.Replace("$" + index, paramValue);
            }
            context.WriteInfo("Alias '{0}' expanded to '{1}'", AliasName, aliasCommand);
            context.ExecuteOneCommand(aliasCommand);
        }
    }

    [Verb(".rmalias", HelpText = "Removes the specified alias.")]
    class RemoveAlias : ICommand
    {
        [Value(0, Required = true)]
        public string AliasName { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (!context.Aliases.Remove(AliasName))
            {
                context.WriteError("Unknown alias '{0}'", AliasName);
                return;
            }
        }
    }

    [Verb(".clearalias", HelpText = "Removes all registered aliases.")]
    class RemoveAllAliases : ICommand
    {
        [Option("temporary", HelpText = "Remove only temporary aliases created by hyperlink output.")]
        public bool TemporaryOnly { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (TemporaryOnly)
            {
                context.RemoveTemporaryAliases();
            }
            else
            {
                context.Aliases.Clear();
            }
        }
    }


    [Verb(".listalias", HelpText = "Lists all the aliases.")]
    class ListAliases : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            if (context.Aliases.Count == 0)
            {
                context.WriteLine("There are no aliases. Use .newalias to define some.");
                return;
            }

            context.WriteLine("{0,-20} {1}", "Name", "Command");
            foreach (var aliasAndCommand in context.Aliases)
            {
                context.WriteLine("{0,-20} {1}", aliasAndCommand.Key, aliasAndCommand.Value);
            }
        }
    }
}

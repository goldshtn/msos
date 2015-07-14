using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!DumpDomain", HelpText = "Displays the application domains in the current target.")]
    class DumpDomain : ICommand
    {
        [Value(0, HelpText = "The id of the application domain to display.")]
        public int Id { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            IEnumerable<ClrAppDomain> appDomains = context.Runtime.AppDomains;
            if (Id != 0)
            {
                appDomains = appDomains.Where(ad => ad.Id == Id);
            }

            context.WriteLine("{0,-4} {1,-40} {2,-10} {3}", "Id", "Name", "# Modules", "Application Base");
            foreach (var appDomain in appDomains)
            {
                context.WriteLine("{0,-4} {1,-40} {2,-10} {3}",
                    appDomain.Id, appDomain.Name.TrimEndToLength(40),
                    appDomain.Modules.Count, appDomain.ApplicationBase);

                if (Id != 0)
                {
                    foreach (var module in appDomain.Modules)
                    {
                        context.WriteLine("  {0}", module.FileName);
                    }
                }
            }
        }
    }
}

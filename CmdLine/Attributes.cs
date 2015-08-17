using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CmdLine
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class VerbAttribute : Attribute
    {
        public VerbAttribute(string verb)
        {
            Verb = verb;
        }

        public string HelpText { get; set; }
        
        internal string Verb { get; private set; }
    }

    public class AttributeBase : Attribute
    {
        public string HelpText { get; set; }
        public bool Required { get; set; }
        public object Default { get; set; }

        // For numeric types only
        public bool Hexadecimal { get; set; }
        public object Min { get; set; }
        public object Max { get; set; }

        internal bool Satisfied { get; set; }
        internal PropertyInfo TargetProperty { get; set; }

        public string Description
        {
            get
            {
                string required = Required ? "required" : "optional";
                string range = "";
                if (Min != null)
                {
                    range += " min: " + Min;
                }
                if (Max != null)
                {
                    range += " max: " + Max;
                }
                if (Default != null)
                {
                    range += " default: " + Default;
                }
                if (TargetProperty.PropertyType.IsEnum)
                {
                    range += " values:";
                    foreach (string name in Enum.GetNames(TargetProperty.PropertyType))
                        range += " " + name;
                }
                return String.Format("{0} ({1} {2} {3}{4})", HelpText, TargetProperty.PropertyType.Name, TargetProperty.Name, required, range);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class OptionAttribute : AttributeBase
    {
        public OptionAttribute(char shortOption)
        {
            Option = shortOption.ToString();
        }

        public OptionAttribute(string longOption)
        {
            Option = longOption;
        }

        public string MutuallyExclusiveSet { get; set; }

        internal bool IsShortOption { get { return Option.Length == 1; } }
        internal string Option { get; private set; }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ValueAttribute : AttributeBase
    {
        public ValueAttribute(int index)
        {
            Index = index;
        }

        internal int Index { get; private set; }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RestOfInputAttribute : Attribute
    {
        public string HelpText { get; set; }
        public bool Required { get; set; }

        internal PropertyInfo TargetProperty { get; set; }

        public string Description
        {
            get
            {
                return String.Format("{0} ({1} {2})", HelpText, TargetProperty.Name, Required ? "required" : "optional");
            }
        }
    }
}

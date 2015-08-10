using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmdLine.Tests
{
    public class OptionAndRest
    {
        [Option('a')]
        public int Option { get; set; }

        [RestOfInput]
        public string Rest { get; set; }
    }

    public class StructuredOptions
    {
        [Option('a', Required = true)]
        public string Option1 { get; set; }

        [Option("boo")]
        public int Option2 { get; set; }

        [Option("coo")]
        public bool Option3 { get; set; }
    }

    public class OptionsAndValues
    {
        [Option('a', HelpText = "This is an option.")]
        public string Option { get; set; }

        [Value(0, Required = true, HelpText = "This is the first value. It's a very important value so it has a very long description that is going to span multiple lines and go on and on and on.")]
        public int Value1 { get; set; }

        [Value(1, HelpText = "This is the second value.")]
        public double Value2 { get; set; }
    }

    public class MutuallyExclusive
    {
        [Option('a', MutuallyExclusiveSet = "all")]
        public string Option1 { get; set; }

        [Option('b', MutuallyExclusiveSet = "all")]
        public string Option2 { get; set; }
    }

    [Verb("verb1", HelpText = "This is the first verb.")]
    public class Verb1
    {
        [Option('a')]
        public string Input1 { get; set; }
    }

    [Verb("verb2", HelpText = "This is the second verb.")]
    public class Verb2
    {
        [Option("foo")]
        public int Input2 { get; set; }
    }

    public class HexArg
    {
        [Value(0, Hexadecimal = true)]
        public long Value { get; set; }
    }

    public class Bounded
    {
        [Option('a', Min = -8, Max = 14)]
        public int Bounded1 { get; set; }

        [Option('b', Min = 887ul)]
        public ulong Bounded2 { get; set; }

        [Option('c', Max = (short)-9)]
        public short Bounded3 { get; set; }
    }

    public class BadBounds
    {
        [Value(0, Min = 13, Max = 0)]
        public int Bounded { get; set; }
    }

    public class BadOptions
    {
        [Option("same")]
        public string Option1 { get; set; }

        [Option("same")]
        public string Option2 { get; set; }
    }

    public class BadValues
    {
        [Value(0)]
        public int Value1 { get; set; }

        [Value(2)]
        public int Value2 { get; set; }
    }
}

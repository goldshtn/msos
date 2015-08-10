using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace CmdLine.Tests
{
    [TestClass]
    public class ParserTests
    {
        private CmdLineParser _parser;

        [TestInitialize]
        public void TestInitialize()
        {
            _parser = new CmdLineParser();
        }

        [TestMethod]
        public void ParseOneClass_OptionsAndRest()
        {
            ParseResult<OptionAndRest> result = _parser.Parse<OptionAndRest>("-a 123 alpha beta gamma");
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(123, result.Value.Option);
            Assert.AreEqual("alpha beta gamma", result.Value.Rest);
        }

        [TestMethod]
        public void ParseOneClass_MutuallyExclusiveOptions()
        {
            ParseResult<MutuallyExclusive> result = _parser.Parse<MutuallyExclusive>("-a alpha -b beta");
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void ParseOneClass_Options()
        {
            ParseResult<StructuredOptions> result = _parser.Parse<StructuredOptions>("-a alpha --boo 123 --coo");
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual("alpha", result.Value.Option1);
            Assert.AreEqual(123, result.Value.Option2);
            Assert.IsTrue(result.Value.Option3);
        }

        [TestMethod]
        public void ParseOneClass_OptionsAndValues()
        {
            ParseResult<OptionsAndValues> result = _parser.Parse<OptionsAndValues>("123 -a alpha 456.43");
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(123, result.Value.Value1);
            Assert.AreEqual("alpha", result.Value.Option);
            Assert.AreEqual(456.43, result.Value.Value2);
        }

        [ExpectedException(typeof(ParserException))]
        [TestMethod]
        public void ParserAttribute_BadBounds()
        {
            _parser.Parse<BadBounds>("");
        }

        [ExpectedException(typeof(ParserException))]
        [TestMethod]
        public void ParserAttribute_BadOptions()
        {
            _parser.Parse<BadOptions>("");
        }

        [ExpectedException(typeof(ParserException))]
        [TestMethod]
        public void ParserAttribute_BadValues()
        {
            _parser.Parse<BadValues>("");
        }

        [TestMethod]
        public void ParserCanTellNegativeNumberFromOption()
        {
            ParseResult<OptionsAndValues> result = _parser.Parse<OptionsAndValues>("-423");
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(-423, result.Value.Value1);
        }

        [TestMethod]
        public void ParseMinOK()
        {
            ParseResult<Bounded> result = _parser.Parse<Bounded>("-b 12481942");
            Assert.IsTrue(result.Success);
        }

        [TestMethod]
        public void ParseMinBad()
        {
            ParseResult<Bounded> result = _parser.Parse<Bounded>("-b 100");
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void ParseMaxOK()
        {
            ParseResult<Bounded> result = _parser.Parse<Bounded>("-c -12");
            Assert.IsTrue(result.Success);
        }

        [TestMethod]
        public void ParseMaxBad()
        {
            ParseResult<Bounded> result = _parser.Parse<Bounded>("-c -2");
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void ParseHexFormat()
        {
            ParseResult<HexArg> result = _parser.Parse<HexArg>("10ac78ee");
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(279738606, result.Value.Value);
        }
        
        [TestMethod]
        public void ParserHelp()
        {
            ParseResult<object> result = _parser.Parse(new[] { typeof(Verb1) }, "help");
            Assert.IsTrue(result.Success);
            Assert.IsNull(result.Value);
        }

        [TestMethod]
        public void ParserUsageOneType()
        {
            Trace.WriteLine(_parser.Usage<OptionsAndValues>());
        }

        [TestMethod]
        public void ParserUsageVerbs()
        {
            Trace.WriteLine(_parser.Usage(new[] { typeof(Verb1), typeof(Verb2) }));
        }

        [TestMethod]
        public void ParseError_WrongType1()
        {
            ParseResult<OptionAndRest> result = _parser.Parse<OptionAndRest>("-a blah");
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void ParseError_WrongType2()
        {
            ParseResult<OptionAndRest> result = _parser.Parse<OptionAndRest>("-a 42.13");
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void ParseError_UnrecognizedOption()
        {
            ParseResult<OptionAndRest> result = _parser.Parse<OptionAndRest>("--boo 123");
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void ParseError_MissingRequiredOption()
        {
            ParseResult<StructuredOptions> result = _parser.Parse<StructuredOptions>("--boo 123");
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void ParseError_MissingRequiredValue()
        {
            ParseResult<OptionsAndValues> result = _parser.Parse<OptionsAndValues>("-a alpha");
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void ParseError_MalformedInput()
        {
            ParseResult<object> result = _parser.Parse(new[] { typeof(Verb1) }, "-- blah");
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void ParseError_VerbNotFound()
        {
            ParseResult<object> result = _parser.Parse(new[] { typeof(Verb1) }, "blah");
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void ParseError_NotAVerb()
        {
            ParseResult<object> result = _parser.Parse(new[] { typeof(Verb1) }, "--alpha");
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void ParseVerbs()
        {
            ParseResult<object> result = _parser.Parse(new[] { typeof(Verb1), typeof(Verb2) }, "verb1 -a blah");
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Value);
            Assert.IsInstanceOfType(result.Value, typeof(Verb1));
            Assert.AreEqual("blah", ((Verb1)result.Value).Input1);
        }
    }
}

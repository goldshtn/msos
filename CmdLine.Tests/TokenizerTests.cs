using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmdLine.Tests
{
    [TestClass]
    public class TokenizerTests
    {
        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void TokenizeNull()
        {
            new Tokenizer(null);
        }

        [TestMethod]
        public void TokenizeEmpty()
        {
            var tokenizer = new Tokenizer("");
            Assert.IsNull(tokenizer.NextToken);
            Assert.AreEqual(0, tokenizer.RestOfInput.Length);
        }

        [TestMethod]
        public void TokenizeOneString()
        {
            var tokenizer = new Tokenizer("hello");
            var token = tokenizer.NextToken;
            Assert.IsNotNull(token);
            Assert.AreEqual(TokenKind.Value, token.Kind);
            Assert.AreEqual("hello", token.Value);
            Assert.AreEqual("", tokenizer.RestOfInput);
        }

        [TestMethod]
        public void TokenizeTwoStrings()
        {
            var tokenizer = new Tokenizer("hello world");
            var token = tokenizer.NextToken;
            Assert.IsNotNull(token);
            Assert.AreEqual(TokenKind.Value, token.Kind);
            Assert.AreEqual("hello", token.Value);
            Assert.AreEqual("world", tokenizer.RestOfInput);
            token = tokenizer.NextToken;
            Assert.IsNotNull(token);
            Assert.AreEqual(TokenKind.Value, token.Kind);
            Assert.AreEqual("world", token.Value);
            Assert.AreEqual("", tokenizer.RestOfInput);
        }

        [TestMethod]
        public void TokenizeOneShortOption()
        {
            var tokenizer = new Tokenizer("-a 123");
            AssertTokenEqual(TokenKind.ShortOption, "a", tokenizer.NextToken);
            AssertTokenEqual(TokenKind.Value, "123", tokenizer.NextToken);
            Assert.IsTrue(tokenizer.AtEnd);
        }

        [TestMethod]
        public void TokenizeOneLongOption()
        {
            var tokenizer = new Tokenizer("--alpha 123");
            AssertTokenEqual(TokenKind.LongOption, "alpha", tokenizer.NextToken);
            AssertTokenEqual(TokenKind.Value, "123", tokenizer.NextToken);
            Assert.IsTrue(tokenizer.AtEnd);
        }

        [TestMethod]
        public void TokenizeMixOfOptions()
        {
            var tokenizer = new Tokenizer(" moo -a   --beta foo -c    123 -d --epsilon --zeta goo  ");
            AssertTokenEqual(TokenKind.Value, "moo", tokenizer.NextToken);
            AssertTokenEqual(TokenKind.ShortOption, "a", tokenizer.NextToken);
            AssertTokenEqual(TokenKind.LongOption, "beta", tokenizer.NextToken);
            AssertTokenEqual(TokenKind.Value, "foo", tokenizer.NextToken);
            AssertTokenEqual(TokenKind.ShortOption, "c", tokenizer.NextToken);
            AssertTokenEqual(TokenKind.Value, "123", tokenizer.NextToken);
            AssertTokenEqual(TokenKind.ShortOption, "d", tokenizer.NextToken);
            AssertTokenEqual(TokenKind.LongOption, "epsilon", tokenizer.NextToken);
            AssertTokenEqual(TokenKind.LongOption, "zeta", tokenizer.NextToken);
            AssertTokenEqual(TokenKind.Value, "goo", tokenizer.NextToken);
            Assert.IsTrue(tokenizer.AtEnd);
        }

        [TestMethod]
        public void TokenizeErrorShortOptionNoName()
        {
            var tokenizer = new Tokenizer("- foo goo");
            Assert.AreEqual(TokenKind.Error, tokenizer.NextToken.Kind);
        }

        [TestMethod]
        public void TokenizeErrorLongOptionNoName()
        {
            var tokenizer = new Tokenizer("-- foo goo");
            Assert.AreEqual(TokenKind.Error, tokenizer.NextToken.Kind);
        }

        [TestMethod]
        public void TokenizeErrorShortOptionLongName()
        {
            var tokenizer = new Tokenizer("-foo goo");
            Assert.AreEqual(TokenKind.Error, tokenizer.NextToken.Kind);
        }

        [TestMethod]
        public void TokenizePosition()
        {
            var tokenizer = new Tokenizer("hello");
            Assert.AreEqual(5, tokenizer.NextToken.EndPosition);
        }

        [TestMethod]
        public void TokenizeNegativeNumber()
        {
            var tokenizer = new Tokenizer("-12");
            AssertTokenEqual(TokenKind.Value, "-12", tokenizer.NextToken);
        }

        [TestMethod]
        public void TokenizeQuotedOption()
        {
            var tokenizer = new Tokenizer("-c \"foo; goo; moo\"");
            AssertTokenEqual(TokenKind.ShortOption, "c", tokenizer.NextToken);
            AssertTokenEqual(TokenKind.Value, "foo; goo; moo", tokenizer.NextToken);
        }

        private void AssertTokenEqual(TokenKind expectedKind, string expectedValue, Token token)
        {
            Assert.IsNotNull(token);
            Assert.AreEqual(expectedKind, token.Kind);
            Assert.AreEqual(expectedValue, token.Value);
        }
    }
}

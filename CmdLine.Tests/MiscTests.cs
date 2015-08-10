using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmdLine.Tests
{
    using CmdLine;

    [TestClass]
    public class MiscTests
    {
        [TestMethod]
        public void SplitOneLine()
        {
            Assert.AreEqual("hello world", "hello world".SplitToLines(50));
        }

        [TestMethod]
        public void SplitMultipleLines()
        {
            Assert.AreEqual("hello " + Environment.NewLine + "world", "hello world".SplitToLines(6));
        }

        [TestMethod]
        public void SplitOnWordBoundary()
        {
            Assert.AreEqual("hello " + Environment.NewLine + "world", "hello world".SplitToLines(10));
        }

        [TestMethod]
        public void SplitOnWordBoundaryEvenIfSpills()
        {
            Assert.AreEqual("helloworld", "helloworld".SplitToLines(7));
        }

        [TestMethod]
        public void SplitWithPrepad()
        {
            Assert.AreEqual("hello " + Environment.NewLine + "   world",
                "hello world".SplitToLines(6, 3));
        }

    }
}

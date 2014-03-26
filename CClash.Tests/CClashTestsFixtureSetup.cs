using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace CClash.Tests {
    [SetUpFixture]
    public class CClashTestsFixtureSetup 
    {
        public static string InitialDir = null;

        [SetUp]
        public void Init() {
            if (InitialDir == null)
                InitialDir = Environment.CurrentDirectory;
            Environment.CurrentDirectory = "c:\\";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CClash.Tests
{
    [TestFixture]
    public class MessageTests
    {
        void BytesCloneTest<T>(T original)
            where T : CClashMessage, new()
        {
            var txt = original.Serialize();
            
            var m2 = new T();
            m2.Deserialize(txt);
            var txt2 = m2.Serialize();

            Assert.AreEqual(txt, txt2);
        }

        [Test]
        public void CClastResponseSerializationDefault()
        {
            var m = new CClashResponse();
            BytesCloneTest(m);
        }

        [Test]
        public void CClastResponseSerialization()
        {
            var m = new CClashResponse();
            m.exitcode = 1;
            m.supported = true;
            m.stderr = ":::hello\nworld\n";
            m.stdout = ":foo\r\nbar\r\n";
            BytesCloneTest(m);
        }

        [Test]
        public void CClashRequestSerializationDefault()
        {
            var m = new CClashRequest();
            BytesCloneTest(m);
        }

        [Test]
        public void CClashRequestSerialization()
        {
            var m = new CClashRequest();
            m.compiler = "c:\\foo\\bar.exe";
            m.cmd = Command.Run;
            m.argv = new List<string>(Environment.GetCommandLineArgs());
            m.envs = new Dictionary<string, string>();

            var env = Environment.GetEnvironmentVariables();

            foreach (string e in env.Keys)
            {
                m.envs[e] = (string)(env[e]);
            }

            BytesCloneTest(m);
        }
    }
}

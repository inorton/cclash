using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;
using NUnit.Framework;
using System.IO;

namespace CClash.Tests
{
    [TestFixture]
    public class NetworkTest
    {
        const string cachedirprefix = "nettest.cachedir";
        string cachedir = null;
        string workdir = null;
        public void ServerThread(object o)
        {
            var srv = o as CClashServerBase;
            if (srv != null)
            {
                srv.Listen(cachedir);
            }
            Console.Error.WriteLine("server thread exiting");
        }

        CClashServerBase server = null;
        Thread serverThread = null;

        [SetUp]
        public void Start()
        {
            if (cachedir == null)
            {
                cachedir = Path.Combine(Environment.CurrentDirectory, cachedirprefix);
            }

            if (workdir == null)
            {
                workdir = Environment.CurrentDirectory;
            }

            if (server != null)
                server.Stop();
            if (serverThread != null)
                serverThread.Join();
        }

        [Test]
        public void ServerNoOp()
        {
            CompilerTest.SetEnvs();

            var srv = new CClashPipeServer();
            server = srv;

            serverThread = new Thread(new ParameterizedThreadStart(ServerThread));
            serverThread.Start(srv);

            Thread.Sleep(1000);
            
            var req = new CClashRequest();
            req.envs = new Dictionary<string, string>();
            req.cmd = Command.NoOp;
            req.argv = new List<string> { DateTime.Now.ToString() };
            using (var cl = new CClashServerClient(cachedir, false))
            {
                var resp = cl.Transact(req);
                Assert.AreEqual(req.argv[0], resp.stdout);
                Assert.IsNotNull(resp);
            }
        }

        [Test]
        [TestCase(1)]
        [TestCase(500)]
        public void ServerCompile(int times)
        {
            CompilerTest.SetEnvs();

            var srv = new CClashPipeServer(); 
            ThreadPool.QueueUserWorkItem(ServerThread, srv);

            
            var req = new CClashRequest();
            req.envs = new Dictionary<string, string>();
            var envs = Environment.GetEnvironmentVariables();
            foreach (string e in envs.Keys)
            {
                req.envs[e] = envs[e].ToString();
            }
            req.compiler = CompilerTest.CompilerPath;
            req.workdir = workdir;
            req.argv = new List<string> { "/c", "test-sources\\hello.c", "/I", "test-sources\\inc with spaces", "/D", "a_hash_define" };
            req.cmd = Command.Run;

            for (int i = 0; i < times; i++)
            {
                using (var cl = new CClashServerClient(cachedir, false))
                {
                    var resp = cl.Transact(req);
                    Assert.IsNotNull(resp);
                    Assert.AreEqual(0, resp.exitcode);
                }
            }
        }
    }
}

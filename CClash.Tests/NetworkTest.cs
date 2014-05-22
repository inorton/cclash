using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;
using NUnit.Framework;
using System.IO;

using CClash;

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
                if (!System.IO.Directory.Exists(cachedir))
                    System.IO.Directory.CreateDirectory(cachedir);
                srv.Listen(cachedir);
            }
            Console.Error.WriteLine("server thread exiting");
        }

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

        }

        [TearDown]
        public void Stop()
        {
            if (serverThread != null)
                serverThread.Join();
        }

        [Test]
        public void PipeServerNoOp()
        {
            CompilerTest.SetEnvs();
            cachedir += "pipe";
            Environment.SetEnvironmentVariable("CCLASH_DIR", cachedir);
            using (var srv = new CClashPipeServer())
            {
                serverThread = new Thread(new ParameterizedThreadStart(ServerThread));
                serverThread.Start(srv);

                Thread.Sleep(1000);

                var req = new CClashRequest();
                req.envs = new Dictionary<string, string>();
                req.cmd = Command.NoOp;
                req.argv = new List<string> { DateTime.Now.ToString() };
                using (var cl = new CClashPipeServerClient())
                {
                    cl.Init(cachedir, false);
                    var resp = cl.Transact(req);
                    Assert.AreEqual(req.argv[0], resp.stdout);
                    Assert.IsNotNull(resp);
                }
            }
        }

        [Test]
        public void InetServerNoOp()
        {
            CompilerTest.SetEnvs();
            cachedir += "inet";
            Environment.SetEnvironmentVariable("CCLASH_DIR", cachedir);
            using (var srv = new CClashInetServer())
            {

                serverThread = new Thread(new ParameterizedThreadStart(ServerThread));
                serverThread.Start(srv);

                Thread.Sleep(1000);

                var req = new CClashRequest();
                req.envs = new Dictionary<string, string>();
                req.cmd = Command.NoOp;
                req.argv = new List<string> { DateTime.Now.ToString() };
                using (var cl = new CClashInetServerClient())
                {
                    cl.Init(cachedir, false);
                    var resp = cl.Transact(req);
                    Assert.AreEqual(req.argv[0], resp.stdout);
                    Assert.IsNotNull(resp);
                }
            }
        }



        [Test]
        [TestCase(1,"inet")]
        [TestCase(500,"inet")]
        public void ServerCompile(int times, string srvmode)
        {
            CompilerTest.SetEnvs();
            cachedir += srvmode;
            Environment.SetEnvironmentVariable("CCLASH_DIR", cachedir);
            Environment.SetEnvironmentVariable("CCLASH_SERVER", srvmode);
            cachedir = cachedirprefix + "." + srvmode;
            CClashServerBase srv;
            if (Settings.InetServiceMode)
            {
                srv = new CClashInetServer();
            }
            else
            {
                srv = new CClashPipeServer();
            }
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
            var argv = new List<string> { "/c", "test-sources\\hello.c", "/I", "test-sources\\inc with spaces", "/D", "a_hash_define" };
            req.cmd = Command.Run;

            Environment.SetEnvironmentVariable("CCLASH_HASH_NO_OBJECTARG", "1");

            for (int i = 0; i < times; i++)
            {
                using ( var cl = new CClashServerClientFactory().GetClient())
                {
                    cl.Init(cachedir, false);

                    var fo = string.Format("/Fo{0}.obj", Guid.NewGuid().ToString().Substring(0, 4));
                    req.argv = new List<string>(argv);
                    req.argv.Add(fo);
                    var resp = cl.Transact(req);
                    Assert.IsNotNull(resp);
                    Assert.AreEqual(0, resp.exitcode);
                }
            }
        }
    }
}

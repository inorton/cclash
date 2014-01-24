using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using System.IO;
namespace CClash.Tests
{
    [TestFixture]
    public class InetSettingsTest
    {

        [Test]
        [Repeat(10)]
        public void InetPortGetSet()
        {
            var cdir = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString().Substring(0, 6));
            var rng = new System.Security.Cryptography.RNGCryptoServiceProvider();
            var bb = new byte[2];
            rng.GetNonZeroBytes(bb);
            int port = (bb[0] | (bb[1] * 2));

            Assert.IsTrue(port > 0);

            Settings.SetServicePort(cdir, port);

            int gport = -1;

            Settings.GetServicePort(cdir, out gport);

            Assert.AreEqual(port, gport);

        }
    }
}

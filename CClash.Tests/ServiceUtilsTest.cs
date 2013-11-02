using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Web.Script.Serialization;

namespace CClash.Tests
{
    [TestFixture]
    public class ServiceUtilsTest
    {
        [Test]
        [Repeat(1000)]
        public void EnvironmentToJson()
        {
            var jss = new JavaScriptSerializer();
            var ed = Environment.GetEnvironmentVariables();
            var txt = jss.Serialize(ed);
            var dd = jss.Deserialize(txt, typeof(Dictionary<string, string>)) as Dictionary<string, string>;

            foreach (string n in ed.Keys)
            {
                Assert.IsTrue(dd.ContainsKey(n));
                Assert.AreEqual(ed[n], dd[n]);
            }

        }
    }
}

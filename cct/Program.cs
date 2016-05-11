using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;

using CClash;

namespace CClash.Tool
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length > 0)
                Settings.CacheDirectory =  Path.GetFullPath(args[0]);
            
            while (true)
            {
                CClashResponse resp = null;
                try
                {
                    var cc = new CClashServerClient(Settings.CacheDirectory);
                    resp = cc.Transact(new CClashRequest() { cmd = Command.GetStats });
                }
                catch (CClashServerNotReadyException)
                {
                    Console.Error.Write(".");
                }
                catch (Exception)
                {
                    Console.Error.Write("e");
                }

                if (resp != null)
                {
                    Console.Clear();
                    Console.Out.WriteLine(DateTime.Now.ToString("s"));
                    Console.Out.WriteLine(resp.stdout);
                }
                
                
                Thread.Sleep(1000);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public sealed class Logging
    {
        public static void Emit(string fmt, params object[] args)
        {
            if (Settings.DebugEnabled)
            {
                for (int i = 0; i < 4; i++)
                {
                    try
                    {
                        File.AppendAllLines(Settings.DebugFile, new string[] { string.Format(fmt, args) });
                        return;
                    }
                    catch {}
                }
            }
        }
    }
}

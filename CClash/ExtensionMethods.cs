using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public static class ExtensionMethods
    {

        public static void WriteLine(this StringBuilder sb, string fmt, params object[] args)
        {
            sb.AppendFormat(fmt, args);
            sb.AppendLine();
        }
    }
}

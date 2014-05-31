using System;
using System.Text;

namespace CClash
{
    public static class ExtensionMethods
    {

        public static void WriteLine(this StringBuilder sb, string fmt, params object[] args)
        {
            sb.AppendFormat(fmt, args);
            sb.AppendLine();
        }

        public static TimeSpan Age(this DateTime dt)
        {
            return DateTime.Now.Subtract(dt);
        }
    }
}

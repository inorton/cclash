using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public class CClashErrorException : Exception
    {
        public CClashErrorException(string fmt, params object[] args)
            : base(string.Format(fmt, args))
        {
        }
    }
}

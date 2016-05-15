using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CClash
{
    public class FileCacheBase : IDisposable
    {
        public string FolderPath { get; protected set; }

        Mutex mtx = null;

        protected void SetupLocks()
        {
            mtx = new Mutex(false, "cclash_mtx_" + FolderPath.ToLower().GetHashCode());
        }

        public void WaitOne()
        {
            Logging.Emit("WaitOne {0}", FolderPath);
            if (!mtx.WaitOne()) throw new InvalidProgramException("mutex lock failed " + mtx.ToString());
        }

        public void ReleaseMutex()
        {
            Logging.Emit("ReleaseMutex {0}", FolderPath);
            mtx.ReleaseMutex();
        }

        public virtual void Dispose()
        {
            if (mtx != null)
            {
                mtx.Dispose();
            }
        }
    }
}

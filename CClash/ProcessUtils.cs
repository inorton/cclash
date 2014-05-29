using System;
using System.Collections.Generic;
using System.Management;
using System.Diagnostics;
namespace CClash
{
    public class ProcessUtils
    {
        public string GetParentProcessName(int childpid)
        {
            var query = string.Format("SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {0}", childpid);
            var search = new ManagementObjectSearcher("root\\CIMV2", query);
            var results = search.Get().GetEnumerator();
            if (!results.MoveNext()) return null;
            var queryObj = results.Current;
            uint parentId = (uint)queryObj["ParentProcessId"];
            if (parentId > 0)
            {
                try
                {
                    var parent = Process.GetProcessById((int)parentId);
                    return parent.ProcessName;
                }
                catch (InvalidOperationException)
                {
                }
            }
            return null;
        }
    }
}

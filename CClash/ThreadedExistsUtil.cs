using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CClash
{
    public class ThreadedExistsUtil
    {
        List<string> filesAdded = new List<string>();

        object lck = new object();

        int threads = Environment.ProcessorCount-1;

        sealed class ExistsData
        {
            public int start;
            public int count;
            public List<string> checkList;
            public List<string> addedList;
            public bool quit = false;
        }

        void CheckExistsThread(object data)
        {
            var ed = data as ExistsData;
            for (int i = ed.start; i < (ed.start + ed.count) && (i < ed.checkList.Count); i++)
            {
                if (ed.quit) return;

                if (!FileUtils.FileMissing(ed.checkList[i]))
                {
                    lock (lck)
                    {
                        ed.addedList.Add(ed.checkList[i]);
                    }
                    ed.quit = true;
                    return;
                }
            }
        }

        public string CheckNotCreated(IEnumerable<string> files)
        {
            var cl = new List<string>(files);
            var al = new List<string>();
            
            var tl = new List<Thread>();

            for (int i = 1; i < threads; i++)
            {
                var d = new ExistsData();
                d.checkList = cl;
                d.addedList = al;
                d.count = d.checkList.Count / threads;
                d.start = i * d.count;
                var t = new Thread(CheckExistsThread);
                t.Start(d);
                tl.Add(t);
            }
            var ed = new ExistsData();
            ed.checkList = cl;
            ed.addedList = al;
            ed.count = ed.checkList.Count / threads;
            CheckExistsThread(ed);
 
            if ( tl.Count > 0 )
                foreach (var t in tl) t.Join();
            return al.FirstOrDefault();
        }

    }
}

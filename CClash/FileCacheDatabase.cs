using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using System.Threading;

namespace CClash
{
    public class FileCacheDatabase : FileCacheBase, IFileCacheStore
    {
        const string DBFile = "cache.sqlite3";
        const string DBSchema = "v5";
        const int MaxConnections = 8;

        /* sqlite connections are not thread-safe! */
        List<SQLiteConnection> connections = new List<SQLiteConnection>();

        void MakeConnections()
        {
            while (connections.Count < MaxConnections)
            {
                var conn = OpenConnection();
                connections.Add(conn);
            }
        }

        SQLiteConnection GetConnection()
        {
            SQLiteConnection conn;
            lock (connections)
            {
                MakeConnections();
                conn = connections.First();
                connections.RemoveAt(0);
            }
            return conn;
        }

        void ReturnConnection(SQLiteConnection conn)
        {
            ThreadPool.QueueUserWorkItem( (x) => {
                conn.Close();
            } );
        }
        
        string ReadTextMetafile(string metafile)
        {
            try
            {
                return File.ReadAllText(Path.Combine(FolderPath, metafile));
            }
            catch {
                return String.Empty;
            }
        }

        void WriteTextMatafile(string metafile, string content)
        {
            File.WriteAllText(Path.Combine(FolderPath, metafile), content);
        }

        public void Open(string folderPath)
        {
            FolderPath = folderPath;
            bool created = false;
            base.SetupLocks();

            while (true)
            {
                try
                {
                    if (ReadTextMetafile(CacheInfo.F_CacheType) != "sqlite")
                    {
                        if (Directory.Exists(FolderPath))
                            Directory.Delete(FolderPath, true);
                    }
                    if (ReadTextMetafile(CacheInfo.F_CacheSchema) != DBSchema)
                    {
                        if (Directory.Exists(FolderPath))
                            Directory.Delete(FolderPath, true);
                    }
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(100);
                }
                break;
            }

            if (!Directory.Exists(FolderPath))
            {
                created = true;
                Directory.CreateDirectory(FolderPath);
            }

            var conn = OpenConnection();

            if (created)
            {
                WriteTextMatafile(CacheInfo.F_CacheType, "sqlite");
                WriteTextMatafile(CacheInfo.F_CacheSchema, DBSchema);
                InitTables(conn);
            }       
        }

        private SQLiteConnection OpenConnection()
        {
            var cs = new SQLiteConnectionStringBuilder();
            cs.DataSource = Path.Combine(FolderPath, DBFile);
            cs.BusyTimeout = 1000;
            cs.Version = 3;
            var conn = new SQLiteConnection(cs.ToString());
            conn.Open();            
            new SQLiteCommand("PRAGMA synchronous = OFF", conn).ExecuteNonQuery();
            new SQLiteCommand("PRAGMA journal_mode = WAL", conn).ExecuteNonQuery();
            return conn;
        }

        void InitTables(SQLiteConnection conn)
        {
            using (var txn = conn.BeginTransaction())
            {
                var schema = @" 
CREATE TABLE IF NOT EXISTS cachedata
(
  hashkey TEXT NOT NULL,
  filename TEXT NOT NULL,
  filedata BLOB,
  CONSTRAINT hashitem PRIMARY KEY ( hashkey, filename )
)";
                var cmd = new SQLiteCommand(schema, conn, txn);                
                cmd.ExecuteNonQuery();
                txn.Commit();
            }
        }

        public event FileCacheStoreAddedHandler Added;

        public event FileCacheStoreRemovedHandler Removed;

        public bool CacheEntryChecksInMemory
        {
            get;
            set;
        }

        public void ClearLocked()
        {
            var conn = GetConnection();
            try
            {    
                using (var txn = conn.BeginTransaction())
                {
                    var sql = @"DELETE FROM cachedata";
                    var cmd = new SQLiteCommand(sql, conn, txn);
                    cmd.ExecuteNonQuery();
                    txn.Commit();
                }
            }
            finally
            {
                ReturnConnection(conn);
            }
        }

        public void EnsureKey(string key)
        {
        }

        Stream GetFileData(string key, string filename)
        {            
            var sql = @"SELECT filedata FROM cachedata WHERE hashkey = @hk AND filename = @fn";
            var conn = GetConnection();
            try
            {
                var cmd = new SQLiteCommand(sql, conn);
                AddSQLiteParams(cmd, key, filename);
                var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return reader.GetStream(0);
                }
            }
            finally
            {
                ReturnConnection(conn);
            }

            throw new System.IO.FileNotFoundException(
                String.Format("cachedata does not contain {0} in {1}", filename, key), filename);
        }

        public Stream OpenFileStream(string key, string filename, System.IO.FileMode mode, System.IO.FileAccess access)
        {
            switch (access)
            {
                case FileAccess.Read:
                    return GetFileData(key, filename);                    
                case FileAccess.Write:
                    switch (mode)
                    {
                        case FileMode.Open:
                            throw new FileNotFoundException();
                        case FileMode.Append:
                            throw new InvalidOperationException();

                        default:
                            var ms = new FileCacheDatabaseWriteStream();
                            ms.HashKey = key;
                            ms.Filename = filename;
                            ms.Cache = this;
                            return ms;
                    }
                default:
                    throw new InvalidOperationException();
            }
            throw new NotImplementedException();
        }

        void AddSQLiteParams(SQLiteCommand cmd, string key, string filename)
        {
            cmd.Parameters.Add(new SQLiteParameter("@hk", key));
            if (filename != null)
                cmd.Parameters.Add(new SQLiteParameter("@fn", filename));
        }


        public bool ContainsEntry(string key, string filename)
        {
            var sql = @"SELECT COUNT(filename) FROM cachedata WHERE hashkey = @hk AND filename = @fn";
            var conn = GetConnection();
            var cmd = new SQLiteCommand(sql, conn);
            AddSQLiteParams(cmd, key, filename);
            var output = (long) cmd.ExecuteScalar();
            ReturnConnection(conn);
            return output == 1;
        }

        public void Remove(string key)
        {
            var conn = GetConnection();
            using (var txn = conn.BeginTransaction())
            {
                var sql = @"DELETE FROM cachedata WHERE hashkey = @hk";
                var cmd = new SQLiteCommand(sql, conn, txn);
                AddSQLiteParams(cmd, key, null);
                cmd.ExecuteNonQuery();
                txn.Commit();
            }
            ReturnConnection(conn);
        }

        public void AddEntry(string key)
        {
        }

        public void AddFile(string key, string filePath, string contentName)
        {
            using (var fs = new FileStream(filePath, FileMode.Open))
            {
                if (fs.Length > Int32.MaxValue)
                    throw new InvalidDataException("file impossibly huge!");
                ReplaceBinaryFileContent(key, filePath, fs);
            }
        }

        public void AddTextFileContent(string key, string filename, string content)
        {
            using (var ms = new MemoryStream(content.Length))
            {
                var sw = new StreamWriter(ms);
                sw.Write(content);
                sw.Flush();                
                ReplaceBinaryFileContent(key, filename, ms);
            }
        }

        public void ReplaceBinaryFileContent(string key, string filename, Stream readfrom)
        {
            var conn = GetConnection();
            using (var txn = conn.BeginTransaction())
            {
                var sql = @"REPLACE INTO cachedata (hashkey, filename, filedata) VALUES (@hk, @fn, @dat)";
                var cmd = new SQLiteCommand(sql, conn, txn);
                AddSQLiteParams(cmd, key, filename);
                var data = new SQLiteParameter("@dat", System.Data.DbType.Binary, (int)readfrom.Length);
                readfrom.Seek(0, SeekOrigin.Begin);
                var savebuf = new byte[readfrom.Length];
                readfrom.Read(savebuf, 0, savebuf.Length);               
                data.Value = savebuf;
                cmd.Parameters.Add(data);
                cmd.ExecuteNonQuery();
                txn.Commit();
            }
            ReturnConnection(conn);
        }

        public override void Dispose()
        {
            base.Dispose();
            lock (connections)
            {
                foreach (var conn in connections)
                {
                    conn.Close();
                }
                connections.Clear();
            }
        }
    }
}

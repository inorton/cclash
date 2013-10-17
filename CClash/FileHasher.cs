using System;
using System.IO;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Security.Cryptography;


namespace CClash
{
    public class FileHasher
    {
        public string DigestFile(string filepath)
        {
            var ha = new MD5CryptoServiceProvider();
            using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
            {
                var h = ha.ComputeHash(fs);
                return new SoapHexBinary(h).ToString();
            }
        }
    }
}

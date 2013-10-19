using System;
using System.IO;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Security.Cryptography;
using System.Text.RegularExpressions;


namespace CClash
{
    public enum DataHashResult
    {
        Ok,
        ContainsTimeOrDate,
        FileNotFound,
        AccessDenied,
    }

    public class DataHash
    {
        public DataHashResult Result { get; set; }
        public string Hash { get; set; }
    }

    public class HashUtil
    {
        static Regex FindDateTime = new Regex("__(TIM|DAT)E__");
        static MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();

        public DataHash DigestString(string input)
        {
            var rv = new DataHash()
            {
                Result = DataHashResult.Ok,
                Hash = new SoapHexBinary( md5.ComputeHash( System.Text.Encoding.Unicode.GetBytes( input ) ) ).ToString()
            };
            return rv;
        }


        public DataHash DigestFile(string filepath)
        {
            var rv = new DataHash() {
                Result = DataHashResult.FileNotFound
            };

            using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
            {
                var txt = File.ReadAllText(filepath);
                if (FindDateTime.IsMatch(txt))
                {
                    rv.Result = DataHashResult.ContainsTimeOrDate;
                }
                else
                {
                    rv.Hash = new SoapHexBinary(md5.ComputeHash(System.Text.Encoding.Unicode.GetBytes(txt))).ToString();
                    rv.Result = DataHashResult.Ok;
                }

            }

            return rv;
        }
    }
}

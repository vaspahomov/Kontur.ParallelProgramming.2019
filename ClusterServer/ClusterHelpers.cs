using System;
using System.Security.Cryptography;
using System.Text;

namespace Cluster
{
    internal static class ClusterHelpers
    {
        private static readonly Encoding encoding = Encoding.UTF8;
        private static readonly byte[] key = Encoding.UTF8.GetBytes("Контур.Шпора");

        public static byte[] GetBase64HashBytes(string query)
        {
            using (var hasher = new HMACMD5(key))
            {
                var hash = Convert.ToBase64String(hasher.ComputeHash(encoding.GetBytes(query ?? "")));
                return encoding.GetBytes(hash);
            }
        }
    }
}
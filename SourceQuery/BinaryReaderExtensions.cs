using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceQuery
{
    public static class BinaryReaderExtensions
    {
        public static string ReadAnsiString(this BinaryReader br)
        {
            var stringBytes = new List<byte>();
            byte charByte;
            while ((charByte = br.ReadByte()) != 0)
            {
                stringBytes.Add(charByte);
            }
            return Encoding.ASCII.GetString(stringBytes.ToArray());
        }
    }
}

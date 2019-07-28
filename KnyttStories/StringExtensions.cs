using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KnyttStories
{
    public static class StringExtensions
    {

        public static string ReadNullTerminatedString(this System.IO.BinaryReader stream)
        {
            var str = "";
            char ch;
            while ((ch = stream.ReadChar()) != 0)
                str += ch;
            return str;
        }

        public static string SafeSubstring(this string text, int start, int length)
        {
            return text.Length <= start ? ""
                : text.Length - start <= length ? text.Substring(start)
                : text.Substring(start, length);
        }
    }
}

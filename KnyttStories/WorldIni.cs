using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace KnyttStories
{
    public class WorldIni
    {
        //Windows has a built in INI reader/writer.
        //I really don't want to use 3rd party libraries in such a simple project.
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string section, string key, string Default, StringBuilder returnedString, int size, string filePath);

        private readonly string _iniPath;
       
        /// <summary>
        /// The folder that contains the World.ini file. 
        /// </summary>
        /// <param name="mapPath"></param>
        public WorldIni(string mapPath)
        {
            var path = Path.Combine(mapPath, "World.ini");
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                throw new FileLoadException($"Could not find World.ini in ${mapPath}");
            }
            _iniPath = path;
        }

        /// <summary>
        /// Read an int value found within a section of the ini file.
        /// </summary>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public int? ReadInt(string section, string key)
        {
            var stringBuilder = new StringBuilder(256);
            GetPrivateProfileString(section, key, "", stringBuilder, 255, _iniPath);
            var result = stringBuilder.ToString();
            if (string.IsNullOrWhiteSpace(result))
            {
                return null;
            }
            return int.Parse(result);
        }

        /// <summary>
        /// Read a string value found within a section of the ini file.
        /// </summary>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public string Read(string section, string key)
        {
            var returnedString = new StringBuilder(256);
            GetPrivateProfileString(section, key, "", returnedString, 255, _iniPath);
            return returnedString.ToString();
        }
    }
}

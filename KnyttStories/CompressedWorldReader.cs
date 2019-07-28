using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KnyttStories
{
    /// <summary>
    /// This class allows you to read compressed Knytt Stories levels (.knytt.bin)
    /// Once loaded, you can inspect the contents of the archive, and extract files to a destination of your choice.
    /// After files have been extracted, you can use the WorldMap class to view level information.
    /// </summary>
    public class CompressedWorldReader
    {
        /// <summary>
        /// The path to the .knytt.bin file.
        /// </summary>
        private readonly string _knyttBinPath;

        /// <summary>
        /// Basic dictionary for storing files, indexed  by filename.
        /// </summary>
        private readonly Dictionary<string, byte[]> _files;

        /// <summary>
        /// The name of the level found inside the .knytt.bin 
        /// </summary>
        public string RootDirectory { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="knyttBinPath"></param>
        public CompressedWorldReader(string knyttBinPath)
        {
            _knyttBinPath = knyttBinPath;
            if (!File.Exists(_knyttBinPath))
                throw new FileNotFoundException($"Unable to locate compressed World file: {_knyttBinPath}");
            RootDirectory = string.Empty;
            _files = new Dictionary<string, byte[]>();
        }
        /// <summary>
        /// Saves a file contained within the .knytt.bin to disk.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="outputDirectory"></param>
        /// <returns></returns>
        public bool SaveFile(string fileName, string outputDirectory = "")
        {
            if (!_files.ContainsKey(fileName))
            {
                return false;
            }
            outputDirectory = Path.Combine(Path.Combine(outputDirectory, RootDirectory), fileName);
            var fileInfo = new FileInfo(outputDirectory);
            if (fileInfo.Directory != null && !fileInfo.Directory.Exists) fileInfo.Directory.Create();
            using (var file = File.Create(fileInfo.FullName))
            {
                file.Write(_files[fileName], 0, GetFileSize(fileName));
                file.Flush();
                return true;
            }
        }

        /// <summary>
        /// Retrieve a file directly from .knytt.bin without saving to disk.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public byte[] GetFile(string fileName)
        {
            return !_files.ContainsKey(fileName) ? null : _files[fileName];
        }

        /// <summary>
        /// Retrieve the size of a file in .knytt.bin without saving to disk.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public int GetFileSize(string fileName)
        {
            return !_files.ContainsKey(fileName) ? -1 : _files[fileName].Length;
        }

        /// <summary>
        /// Retrieve a list of all files in the .knytt.bin 
        /// </summary>
        /// <param name="regexFilter"></param>
        /// <returns></returns>
        public List<string> GetFileNames(string regexFilter = "")
        {
            if (string.IsNullOrWhiteSpace(regexFilter)) return _files.Keys.ToList();
            var matchedFiles = new List<string>();
            foreach (var key in _files.Keys)
            {
                if (Regex.IsMatch(key, regexFilter))
                {
                    matchedFiles.Add(key);
                }
            }
            return matchedFiles;
        }

        /// <summary>
        /// Proceed with opening and parsing a .knytt.bin file.
        /// </summary>
        /// <returns></returns>
        public uint Open()
        {
            using (var fileStream = File.OpenRead(_knyttBinPath))
            using (var binaryReader = new BinaryReader(fileStream))
            {
                var header = binaryReader.ReadChars(2);
                if (!header.SequenceEqual(new[] { 'N', 'F' }))
                {
                    throw new InvalidOperationException("Not a valid compressed World file (.knytt.bin) -- missing NF header.");
                }

                //get the level name.
                while (binaryReader.PeekChar() != 0)
                {
                    RootDirectory += binaryReader.ReadChar();
                }
                //this doesn't match the final files I find, so that is confusing.
                var fileCount = binaryReader.ReadUInt32();

                while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                {
                    //skip ['N', 'F']
                    binaryReader.BaseStream.Seek(2, SeekOrigin.Current);

                    var filePath = string.Empty;
                    char fileChr;
                    //build a file name
                    while ((fileChr = binaryReader.ReadChar()) != 0)
                    {
                        filePath += (fileChr == '\\') ? '/' : fileChr;
                    }
                    var sizeData = binaryReader.ReadBytes(4);

                    //what in the fuck
                    var fileSize = sizeData[0] + sizeData[1] * 256 + sizeData[2] * 65536 + sizeData[3] * 16777216;

                    //read and store the file.
                    _files[filePath] = binaryReader.ReadBytes(fileSize);
                }
                return (uint)_files.Count;
            }
        }
    }
}
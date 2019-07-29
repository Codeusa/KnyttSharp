using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KnyttStories
{
    class Program
    {
        static void Main(string[] args)
        {

            var knyttBinFile = args[0];
            var outputFolder = args[1];
            var knyttStoriesData = args[2];

            var worldReader = new CompressedWorldReader(knyttBinFile);
            if (worldReader.Open() > 0)
            {
                Console.WriteLine($"Opened {worldReader.RootDirectory} successfully.");
                foreach (var file in worldReader.GetFileNames())
                {
                    if (worldReader.SaveFile(file, outputFolder))
                    {
                        Console.WriteLine($"Saving {file} ({worldReader.GetFileSize(file)} bytes)");
                    }
                }
                Console.WriteLine("All files extracted.");
                var worldMap = new WorldMap(Path.Combine(outputFolder, worldReader.RootDirectory), knyttStoriesData);
                if (worldMap.Load())
                {
                    Console.WriteLine($"Loaded: {worldMap.Name} by {worldMap.Author}");
                    Console.WriteLine(worldMap.Bounds.ToString());
                    Console.WriteLine($"Rendering world...");
                    using (var worldRender = worldMap.Draw())
                    {
                        if (worldRender != null)
                        {
                            worldRender.Save(Path.Combine(outputFolder, $"{worldReader.RootDirectory}_{DateTime.Now:yyyy-dd-M--HH-mm-ss}.png"));
                            Console.WriteLine($"World render saved to {outputFolder}");
                        }
                    }
                }
            }
            Console.WriteLine("Press enter to close...");
            Console.Read();
        }
    }
}

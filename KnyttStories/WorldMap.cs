using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace KnyttStories
{
    public enum Settings : byte
    {
        TileSetA,
        TileSetB,
        AmbianceA,
        AmbianceB,
        Music,
        Gradient
    }

    /// <summary>
    /// A basic store for screen information.
    /// </summary>
    internal class Screen
    {
        public int X { get; set; }

        public int Y { get; set; }

        public Dictionary<byte, byte[]> Layers { get; set; }

        public Dictionary<Settings, byte> Settings { get; set; }

        public override string ToString()
        {
            return $"x{X}y{Y}";
        }
    }

    /// <summary>
    /// This class allows you to read a Map.bin file which represents a Knytt Stories world.
    /// Once loaded, you can then render the actual level and all of its contents to an image.
    /// </summary>
    public class WorldMap
    {
        private const int MapWidth = 25;
        private const int MapHeight = 10;
        private const int TileWidth = 24;
        private const int TileHeight = 24;
        private const int LayerWidth = MapWidth * TileWidth;
        private const int LayerHeight = MapHeight * TileHeight;

        private static readonly Color TransparentColor = Color.FromArgb(255, 0, 255);
        private readonly string _dataFolder;
        private readonly string _mapPath;

        private readonly Dictionary<int, Screen> _screens;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mapPath">The path to the folder containing Map.bin</param>
        /// <param name="dataFolder">The path to your primary Knytt Stories Data folder.</param>
        public WorldMap(string mapPath, string dataFolder)
        {
            _mapPath = mapPath;
            _dataFolder = dataFolder;
            _screens = new Dictionary<int, Screen>();
        }

        /// <summary>
        /// The Calculated game world grid.
        /// </summary>
        public Rectangle Bounds { get; private set; }

        /// <summary>
        /// Decompresses a GZIP file.
        /// </summary>
        /// <param name="memoryStream"></param>
        /// <param name="mapFile"></param>
        private static void DecompressMap(Stream memoryStream, string mapFile)
        {
            using (var fileStream = File.OpenRead(mapFile))
            {
                //not a valid gzip file.
                if (fileStream.ReadByte() != 31 || fileStream.ReadByte() != 139) return;
                fileStream.Seek(0, SeekOrigin.Begin);
                using (var decompressionStream = new GZipStream(fileStream, CompressionMode.Decompress))
                {
                    decompressionStream.CopyTo(memoryStream);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                }
            }
        }

        /// <summary>
        /// Generates a unique ID for a screen based on an X and Y coordinate pair. 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private static int GetScreenId(int x, int y)
        {
            var screenId = 23;
            screenId = screenId * 37 + x;
            screenId = screenId * 37 + y;
            return screenId;
        }

        /// <summary>
        /// Calculates the rectangle bounds of the world grid.
        /// </summary>
        private void CalculateWorldBounds()
        {
            var boundsLeft = _screens.Values.Min(r => r.X);
            var boundsTop = _screens.Values.Min(r => r.Y);
            var boundsRight = _screens.Values.Max(r => r.X);
            var boundsBottom = _screens.Values.Max(r => r.Y);
            Bounds = new Rectangle(boundsLeft, boundsTop, boundsRight - boundsLeft + 1, boundsBottom - boundsTop + 1);
        }
        /// <summary>
        /// Renders all the screens found within the world grid.
        /// TODO try actually placing Screens as their correct x/y coords?
        /// </summary>
        /// <param name="removeDebugObjects">Removes otherwise useless objects from the render.</param>
        /// <param name="removeGhost"></param>
        /// <returns>The full rendered world map.</returns>
        public Bitmap Draw(bool removeDebugObjects = true, bool removeGhost = true)
        {
            var worldSize = new Size((Bounds.Right - Bounds.Left + 1) * LayerWidth, LayerHeight);
            var worldRender = new Bitmap(worldSize.Width, worldSize.Height, PixelFormat.Format32bppPArgb);
            using (var canvas = Graphics.FromImage(worldRender))
            {
                canvas.PixelOffsetMode = PixelOffsetMode.HighQuality;
                canvas.InterpolationMode = InterpolationMode.HighQualityBilinear;
                canvas.SmoothingMode = SmoothingMode.HighQuality;
                canvas.Clear(TransparentColor);
                var offset = 0;
                foreach (var screen in _screens.Values)
                {
                    canvas.DrawImage(RenderScreen(screen.X, screen.Y, removeDebugObjects, removeGhost), offset, 0,
                        LayerWidth, LayerHeight);
                    offset += LayerWidth;
                }
            }

            return worldRender;
        }

        /// <summary>
        /// Game worlds in Knytt Stories are composed of screens placed on a grid.
        /// Calling this will access one of those screens, rendering all of its layers (gradients, landscape, sprites.)
        /// </summary>
        /// <param name="x">The X coordinate of the screen on the world grid.</param>
        /// <param name="y">The X coordinate of the screen on the world grid.</param>
        /// <param name="removeDebugObjects">Removes otherwise useless objects from the render.</param>
        /// <param name="removeGhost"></param>
        /// <returns>The rendered screen as a bitmap.</returns>
        public Bitmap RenderScreen(int x, int y, bool removeDebugObjects = true, bool removeGhost = true)
        {
            var screenId = GetScreenId(x, y);
            if (!_screens.ContainsKey(screenId))
            {
                Console.WriteLine($"There is not {x}/{y} screen");
                return null;
            }
            var screenRender = new Bitmap(LayerWidth, LayerHeight, PixelFormat.Format32bppPArgb);
            using (var canvas = Graphics.FromImage(screenRender))
            {
                canvas.PixelOffsetMode = PixelOffsetMode.HighQuality;
                canvas.InterpolationMode = InterpolationMode.HighQualityBilinear;
                canvas.SmoothingMode = SmoothingMode.HighQuality;

                var gradientId = _screens[screenId].Settings[Settings.Gradient];
                using (var gradient =
                    new Bitmap(Image.FromFile(GetResourcePath($"Gradients/Gradient{gradientId}.png"))))
                {
                    gradient.MakeTransparent(TransparentColor);
                    //draw gradients
                    for (var i = 0; i < Math.Ceiling(1000.0 / gradient.Width); i++)
                        canvas.DrawImage(gradient, i * gradient.Width, 0, gradient.Width, LayerHeight);
                }

                var tileAId = _screens[screenId].Settings[Settings.TileSetA];
                var tileBId = _screens[screenId].Settings[Settings.TileSetB];

                using (var tileA = new Bitmap(Image.FromFile(GetResourcePath($"Tilesets/Tileset{tileAId}.png"))))
                using (var tileB = new Bitmap(Image.FromFile(GetResourcePath($"Tilesets/Tileset{tileBId}.png"))))
                {
                    tileA.MakeTransparent();
                    tileB.MakeTransparent();

                    //Draw our initial tile layers.
                    for (byte layer = 0; layer < 4; layer++)
                    for (var i = 0; i < 250; i++)
                    {
                        var tile = GetTile(screenId, layer, i);
                        if (tile % 128 > 0)
                        {
                            var tileSet = Math.Floor((double) (tile / 128)) == 0 ? tileA : tileB;
                            var tileX = i % 25 * 24;
                            var tileY = (int) Math.Floor((double) (i / MapWidth)) * TileHeight;
                            var sourceX = tile % 128 % 16 * 24;
                            var sourceY = (int) Math.Floor((double) (tile % 128 / 16)) * TileHeight;


                            canvas.DrawImage(tileSet, new Rectangle(tileX, tileY, TileHeight, TileHeight), sourceX,
                                sourceY, TileHeight, TileHeight,
                                GraphicsUnit.Pixel);
                        }
                    }
                }

                //Here is our object/sprite layers.
                for (byte layer = 4; layer < 8; layer++)
                for (var i = 0; i < 250; i++)
                {
                    var objectId = GetTile(screenId, layer, i);
                    if (objectId > 0)
                    {
                        var objectBank = GetTile(screenId, layer, i + 250);

                        //custom Objects
                        if (objectBank == 255)
                        {
                            Console.WriteLine("Skipping custom object for now");
                            continue;
                        }

                        var drawObject = true;
                        if (removeDebugObjects)
                        {
                            //System objects
                            if (objectBank == 0 &&
                                (objectId == 2 || objectId >= 11 && objectId <= 20 || objectId >= 25))
                            {
                                drawObject = false;
                            }
                            // Ghost [X] Wall
                            else if (objectBank == 12 && objectId == 17)
                            {
                                drawObject = false;
                            }
                            //Invisible
                            else if (objectBank == 16)
                            {
                                drawObject = false;
                            }
                            //Fly A B
                            else if (objectBank == 2 && (objectId == 3 || objectId == 4))
                            {
                                drawObject = false;
                            }
                            //Decoration
                            else if (objectBank == 8 && objectId >= 15 && objectId <= 17)
                            {
                                drawObject = false;
                            }
                            //Nature FX
                            else if (objectBank == 7 &&
                                     (objectId == 1 || objectId == 10 || objectId == 12 || objectId == 14 ||
                                      objectId == 16 || objectId == 3 || objectId == 6 || objectId == 8))
                            {
                                drawObject = false;
                            }
                            //Ghosts
                            else if (objectBank == 12 && removeGhost)
                            {
                                drawObject = false;
                            }
                            //Robots
                            else if (objectBank == 13 && (objectId == 7 || objectId == 10))
                            {
                                drawObject = false;
                            }
                            //Robots (redirection)
                            else if (objectBank == 13)
                            {
                                //Lasers
                                if (objectId == 8 || objectId == 11) objectId++;
                            }
                            //Objects & Areas (redirection)
                            else if (objectBank == 15)
                            {
                                //Password switches
                                if (objectId >= 14 && objectId <= 21)
                                    objectId = 13;
                                //Disappearing blocks
                                else if (objectId >= 8 && objectId <= 11)
                                    objectId -= 7;
                                //Blue blocks
                                else if (objectId == 6)
                                    drawObject = false;
                                else if (objectId == 7) objectId = 6;
                            }
                            //Traps (redirections)
                            else if (objectBank == 6 && objectId == 6)
                            {
                                objectBank = 8;
                            }
                        }

                        if (!drawObject) continue;
                        using (var worldObject =
                            new Bitmap(GetResourcePath($"Objects/Bank{objectBank}/Object{objectId}.png")))
                        {
                            worldObject.MakeTransparent(Color.FromArgb(255, 0, 255));
                            var objectX = i % 25 * TileHeight;
                            var objectY = (int) Math.Floor((double) (i / 25)) * TileHeight;
                            canvas.DrawImage(worldObject, objectX, objectY, worldObject.Width,
                                worldObject.Height);
                        }
                    }
                }
            }

            return screenRender;
        }

        /// <summary>
        /// Retrieves a tile from a screens layer.
        /// </summary>
        /// <param name="screenId"></param>
        /// <param name="layer"></param>
        /// <param name="tile"></param>
        /// <returns></returns>
        private int GetTile(int screenId, byte layer, int tile)
        {
            return _screens[screenId].Layers[layer][tile];
        }

        /// <summary>
        /// Look's in the "Data" folder for a resource.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        private string GetResourcePath(string resourceName)
        {
            var dataBasedPath = Path.Combine(_dataFolder, resourceName);
            if (File.Exists(dataBasedPath)) return dataBasedPath;
            Console.WriteLine(dataBasedPath);
            return null;
        }

        /// <summary>
        /// Decompresses a Knytt Stories "Map.bin" and loads the contents.
        /// </summary>
        /// <param name="mapFile"></param>
        /// <returns></returns>
        public bool Load(string mapFile = "Map.bin")
        {
            mapFile = Path.Combine(_mapPath, mapFile);
            if (!File.Exists(mapFile)) return false;

            using (var memoryStream = new MemoryStream())
            using (var binaryReader = new BinaryReader(memoryStream))
            {
                DecompressMap(memoryStream, mapFile);
                if (binaryReader.PeekChar() != 'x')
                {
                    Console.WriteLine("Not it chief");
                    return false;
                }

                while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                {
                    var screenCoords = binaryReader.ReadNullTerminatedString().Substring(1).Split('y')
                        .Select(n => Convert.ToInt32(n)).ToArray();

                    var screen = new Screen
                    {
                        X = screenCoords[0],
                        Y = screenCoords[1]
                    };
                    var screenData =
                        binaryReader.ReadBytes(binaryReader.ReadInt32()); //reads the screen size, then the data.
                    screen.Settings = new Dictionary<Settings, byte>();
                    for (int i = 0, offset = 3000; i < Enum.GetNames(typeof(Settings)).Length; i++, offset++)
                        screen.Settings[(Settings) i] = screenData[offset];
                    screen.Layers = new Dictionary<byte, byte[]>
                    {
                        {0, screenData.Take(250).ToArray()},
                        {1, screenData.Skip(250).Take(250).ToArray()},
                        {2, screenData.Skip(500).Take(250).ToArray()},
                        {3, screenData.Skip(750).Take(250).ToArray()},
                        {4, screenData.Skip(1000).Take(500).ToArray()},
                        {5, screenData.Skip(1500).Take(500).ToArray()},
                        {6, screenData.Skip(2000).Take(500).ToArray()},
                        {7, screenData.Skip(2500).Take(500).ToArray()}
                    };
                    _screens[GetScreenId(screen.X, screen.Y)] = screen;
                }
            }
            CalculateWorldBounds();
            return true;
        }
    }
}
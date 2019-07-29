using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
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
    ///     A basic model for screen information.
    /// </summary>
    internal class Screen
    {
        public int X { get; set; }

        public int Y { get; set; }

        public Dictionary<byte, byte[]> Layers { get; set; }

        public Dictionary<Settings, byte> Settings { get; set; }

        public override string ToString()
        {
            return $"X={X} Y={Y}";
        }
    }


    /// <summary>
    ///     This class allows you to read a Map.bin file which represents a Knytt Stories world.
    ///     Once loaded, you can then render the actual level and all of its contents to an image.
    /// </summary>
    public class WorldMap
    {
        private const int MapWidth = 25;
        private const int MapHeight = 10;
        private const int TileWidth = 24;
        private const int TileHeight = 24;
        private const int TileSetRow = 16;
        private const int LayerWidth = MapWidth * TileWidth;
        private const int LayerHeight = MapHeight * TileHeight;

        internal static readonly Color TransparentColor = Color.FromArgb(255, 0, 255);

        private readonly string _dataFolder;

        /// <summary>
        ///     An instance that allows us to read values from World.ini
        /// </summary>
        private readonly WorldIni _ini;

        private readonly string _mapPath;

        /// <summary>
        ///     A collection of all the screens found in Map.bin
        /// </summary>
        private readonly Dictionary<int, Screen> _screens;


        /// <summary>
        /// </summary>
        /// <param name="mapPath">The path to the folder containing Map.bin</param>
        /// <param name="dataFolder">The path to your primary Knytt Stories Data folder.</param>
        public WorldMap(string mapPath, string dataFolder)
        {
            _mapPath = mapPath;
            _dataFolder = dataFolder;
            _screens = new Dictionary<int, Screen>();
            _ini = new WorldIni(_mapPath);
        }

        /// <summary>
        ///     The Calculated game world grid.
        /// </summary>
        public Rectangle Bounds { get; private set; }

        /// <summary>
        ///     The name of the world.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        ///     The original author of the world.
        /// </summary>
        public string Author { get; private set; }

        /// <summary>
        ///     The described size of the world.
        /// </summary>
        public string Size { get; private set; }

        /// <summary>
        ///     A description attached to the world.
        /// </summary>
        public string Description { get; private set; }


        /// <summary>
        ///     Decompresses a Knytt Stories "Map.bin" and loads the contents.
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
            ReadWorldMetadata();
            return true;
        }

        /// <summary>
        ///     Reads metadata from the [World] section of the World.ini file
        /// </summary>
        private void ReadWorldMetadata()
        {
            Name = _ini.Read("World", "Name");
            Author = _ini.Read("World", "Author");
            Description = _ini.Read("World", "Description");
            Size = _ini.Read("World", "Size");
        }


        /// <summary>
        ///     Decompresses a GZIP file.
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
        ///     Generates a unique ID for a screen based on an X and Y coordinate pair.
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
        ///     Calculates the rectangle bounds of the world grid.
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
        ///     Renders all the screens found within the world grid.
        ///     TODO try actually placing Screens as their correct x/y coords?
        /// </summary>
        /// <param name="removeDebugObjects">Removes otherwise useless objects from the render.</param>
        /// <param name="removeGhost"></param>
        /// <param name="withCoords">Draws the X & Y coordinates of each screen.</param>
        /// <returns>The full rendered world map.</returns>
        public Bitmap Draw(bool removeDebugObjects = true, bool removeGhost = true, bool withCoords = false)
        {
            var columns = 6;
            var totalRows = (int) Math.Ceiling(_screens.Count / (double) columns);
            var totalWidth = columns * LayerWidth + (columns - 1);
            var totalHeight = totalRows * LayerHeight + (totalRows - 1);
            var worldRender = new Bitmap(totalWidth, totalHeight, PixelFormat.Format32bppPArgb);
            using (var canvas = Graphics.FromImage(worldRender))
            {
                canvas.PixelOffsetMode = PixelOffsetMode.HighQuality;
                canvas.InterpolationMode = InterpolationMode.HighQualityBilinear;
                canvas.SmoothingMode = SmoothingMode.HighQuality;
                canvas.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                canvas.CompositingQuality = CompositingQuality.HighQuality;
                canvas.Clear(TransparentColor);
                var i = 0;
                var row = 0;
                foreach (var screen in _screens.Values)
                {
                    if (i > 0 && i % columns == 0) row++;
                    var column = i % columns;
                    var destinationX = column * LayerWidth;
                    var destinationY = row * LayerHeight;
                    using (var screenRender = RenderScreen(screen.X, screen.Y, removeDebugObjects, removeGhost, withCoords))
                    {
                        canvas.DrawImage(screenRender, destinationX, destinationY, LayerWidth, LayerHeight);
                        i++;
                    }
                }
            }
            worldRender.MakeTransparent(TransparentColor);
            return worldRender;
        }

        /// <summary>
        ///     Game worlds in Knytt Stories are composed of screens placed on a grid.
        ///     Calling this will access one of those screens, rendering all of its layers (gradients, landscape, sprites.)
        /// </summary>
        /// <param name="x">The X coordinate of the screen on the world grid.</param>
        /// <param name="y">The X coordinate of the screen on the world grid.</param>
        /// <param name="removeDebugObjects">Removes otherwise useless objects from the render.</param>
        /// <param name="removeGhost"></param>
        /// <param name="withCoords">Adds the X & Y coordinates to the rendered screen.</param>
        /// <returns>The rendered screen as a bitmap.</returns>
        public Bitmap RenderScreen(int x, int y, bool removeDebugObjects = true, bool removeGhost = true,
            bool withCoords = false)
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
                canvas.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                canvas.CompositingQuality = CompositingQuality.HighQuality;

                var gradientId = _screens[screenId].Settings[Settings.Gradient];
                using (var gradient = new Bitmap(Image.FromFile(GetResourcePath($"Gradients/Gradient{gradientId}.png"))))
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
                    tileA.MakeTransparent(TransparentColor);
                    tileB.MakeTransparent(TransparentColor);

                    //Draw our initial tile layers.
                    for (byte layer = 0; layer < 4; layer++)
                    for (var i = 0; i < 250; i++)
                    {
                        var tile = GetTile(screenId, layer, i);
                        if (tile % 128 > 0)
                        {
                            var tileSet = Math.Floor((double) (tile / 128)) == 0 ? tileA : tileB;
                            var tileX = i % MapWidth * TileWidth;
                            var tileY = (int) Math.Floor(i / (double) MapWidth) * TileHeight;
                            var sourceX = tile % 128 % 16 * TileWidth;
                            var sourceY = (int) Math.Floor(tile % 128 / (double) TileSetRow) * TileHeight;
                            var tileBounds = new Rectangle(tileX, tileY, TileHeight, TileHeight);

                            canvas.DrawImage(tileSet, tileBounds, sourceX,
                                sourceY, TileHeight, TileHeight,
                                GraphicsUnit.Pixel);
                        }
                    }
                }

                //Here is our object/sprite layers.
                for (byte layer = 4; layer < 8; layer++)
                for (var i = 0; i < 250; i++)
                {
                    var gameObjectId = GetTile(screenId, layer, i);
                    if (gameObjectId <= 0) continue;
                    var objectBankId = GetTile(screenId, layer, i + 250);
                    //custom Objects
                    if (objectBankId == 255)
                    {
                        var customSection = $"Custom Object {gameObjectId}";
                        var customImage = _ini.Read(customSection, "Image");
                        if (string.IsNullOrWhiteSpace(customImage))
                        {
                            Console.WriteLine(
                                $"Unable to locate information for Object ID {gameObjectId} in World Custom Objects");
                            continue;
                        }

                        var customTileWidth = _ini.ReadInt(customSection, "Tile Width") ?? TileWidth;
                        var customTileHeight = _ini.ReadInt(customSection, "Tile Height") ?? TileHeight;
                        var customOffsetX = _ini.ReadInt(customSection, "Offset X") ?? 0;
                        var customOffsetY = _ini.ReadInt(customSection, "Offset Y") ?? 0;
                        var frame = _ini.ReadInt(customSection, "Init AnimFrom") ?? 0;

                        using (var customGameObject =
                            new Bitmap(GetCustomResourcePath($"Custom Objects/{customImage}")))
                        {

                            customGameObject.MakeTransparent(TransparentColor);
                            var sourceX = frame %
                                          (customGameObject.Width / customTileWidth) *
                                          customTileWidth;

                            var sourceY = (int) Math.Floor(frame /
                                                           (customGameObject.Width /
                                                            (double) customTileWidth)) *
                                          customTileHeight;

                            var destinationX = i % MapWidth * TileHeight + 12 -
                                               Math.Floor((double) customTileWidth / 2) +
                                               customOffsetX;

                            var destinationY = Math.Floor((double) i / MapWidth) * TileHeight + 12 -
                                               Math.Floor((double) customTileHeight / 2) +
                                               customOffsetY;

                            var customObjectBounds = new Rectangle((int) destinationX, (int) destinationY,
                                customTileWidth,
                                customTileHeight);

                            canvas.DrawImage(customGameObject, customObjectBounds, sourceX,
                                sourceY, customTileWidth, customTileHeight,
                                GraphicsUnit.Pixel);
                        }

                        continue;
                    }
                    if (!CanDraw(ref objectBankId, ref gameObjectId, removeGhost)) continue;
                    using (var worldObject =
                        new Bitmap(GetResourcePath($"Objects/Bank{objectBankId}/Object{gameObjectId}.png")))
                    {
                        worldObject.MakeTransparent(TransparentColor);
                        var objectX = i % MapWidth * TileHeight;
                        var objectY = (int) Math.Floor((double) (i / MapWidth)) * TileHeight;
                        canvas.DrawImage(worldObject, objectX, objectY, worldObject.Width,
                            worldObject.Height);
                    }
                }
                if (withCoords)
                {
                    var rect = new Rectangle(0, 0, LayerWidth, LayerHeight);
                    using (var gp = new GraphicsPath())
                    using (var outline = new Pen(Color.Black, 2)
                        {LineJoin = LineJoin.Round})
                    using (var sf = new StringFormat())
                    using (var foreBrush = new SolidBrush(Color.White))
                    {
                        gp.AddString($"Screen Coords: X={x} Y={y}", FontFamily.GenericMonospace,
                            (int) FontStyle.Regular,
                            16, rect, sf);
                        canvas.ScaleTransform(1.3f, 1.35f);
                        canvas.SmoothingMode = SmoothingMode.HighQuality;
                        canvas.DrawPath(outline, gp);
                        canvas.FillPath(foreBrush, gp);
                    }
                }
            }
            return screenRender;
        }

        /// <summary>
        ///     Retrieves a tile from a screens layer.
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
        ///     Looks in the supplied map folder for Custom Objects.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        private string GetCustomResourcePath(string resourceName)
        {
            var customPath = Path.Combine(_mapPath, resourceName);
            if (File.Exists(customPath)) return customPath;
            Console.WriteLine(customPath);
            return null;
        }

        /// <summary>
        ///     Look's in the "Data" folder for a resource.
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
        /// Hard coded values to handle filter out certain objects, or banks entirely. 
        /// </summary>
        /// <param name="objectBankId"></param>
        /// <param name="gameObjectId"></param>
        /// <param name="removeGhost"></param>
        /// <returns></returns>
        private static bool CanDraw(ref int objectBankId, ref int gameObjectId, bool removeGhost)
        {
            if (objectBankId == 0)
            {
                //System objects
                if ((gameObjectId == 2 || gameObjectId >= 11 && gameObjectId <= 20 || gameObjectId >= 25))
                {
                    return false;
                }
            }
            if (objectBankId == 12)
            {
                // Ghost [X] Wall / Ghost
                if (gameObjectId == 17 || removeGhost)
                {
                    return false;
                }
            }
            //Invisible
            if (objectBankId == 16)
            {
                return false;
            }
            if (objectBankId == 2)
            {
                //Fly A B
                if ((gameObjectId == 3 || gameObjectId == 4))
                {
                    return false;
                }
            }
            if (objectBankId == 8)
            {
                //Decoration
                if (gameObjectId >= 15 && gameObjectId <= 17)
                {
                    return false;
                }
            }
            if (objectBankId == 7)
            {
                //Nature FX
                if (gameObjectId
                    == 1
                    || gameObjectId == 10
                    || gameObjectId == 12
                    || gameObjectId == 14
                    || gameObjectId == 16
                    || gameObjectId == 3
                    || gameObjectId == 6
                    || gameObjectId == 8)
                {
                    return false;
                }
            }
            if (objectBankId == 13)
            {
                //Robots
                if ((gameObjectId == 7 || gameObjectId == 10))
                {
                    return false;
                }
                //Robots (redirection) -> Lasers
                if (gameObjectId == 8 || gameObjectId == 11)
                {
                    gameObjectId++;
                    return true;
                }
            }
            //Objects & Areas (redirection)
            if (objectBankId == 15)
            {
                //Password switches
                if (gameObjectId >= 14 && gameObjectId <= 21)
                {
                    gameObjectId = 13;
                    return true;
                }
                //Disappearing blocks
                if (gameObjectId >= 8 && gameObjectId <= 11)
                {
                    gameObjectId -= 7;
                    return true;
                }
                //Blue blocks
                if (gameObjectId == 6)
                {
                    return false;
                }
                //Red blocks
                if (gameObjectId == 7)
                {
                    gameObjectId = 6;
                    return true;
                }
            }
            //Traps (redirections)
            if (objectBankId == 6)
            {
                if (gameObjectId == 6)
                {
                    objectBankId = 8;
                    return true;
                }
            }
            return true;
        }
    }
}
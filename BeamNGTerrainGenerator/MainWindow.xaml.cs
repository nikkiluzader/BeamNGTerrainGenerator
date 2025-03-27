using System;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using BeamNGTerrainGenerator.Services;
using OSGeo.GDAL;
using Newtonsoft.Json;
using System.Windows.Controls;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using System.IO;
using System.Windows.Media.Imaging;
using HelixToolkit.Wpf;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.Processing;

namespace BeamNGTerrainGenerator
{
    public partial class MainWindow : Window
    {
        private DataRetrievalService _dataService;
        private float[,] _latestHeightmap;
        private int _selectedResolution;
        private Image<Rgba32> satelliteImage;

        public MainWindow()
        {
            InitializeComponent();
            GdalConfiguration.ConfigureGdal();
            heightScaleSlider.ValueChanged += HeightScaleSlider_ValueChanged;
            ResolutionComboBox.SelectionChanged += ResolutionComboBox_SelectionChanged;

            _dataService = new DataRetrievalService();

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            txtLatitude.Text = "36.056595";
            txtLongitude.Text = "-112.125092";
            ResolutionComboBox.SelectedIndex = 2; // Default 1024

            await webViewMap.EnsureCoreWebView2Async();

            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string htmlPath = System.IO.Path.Combine(appDirectory, "Web", "map.html");
            string htmlUri = new Uri(htmlPath).AbsoluteUri;

            webViewMap.Source = new Uri(htmlUri);

            webViewMap.WebMessageReceived += WebViewMap_WebMessageReceived;
        }

        private void WebViewMap_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            dynamic message = JsonConvert.DeserializeObject(e.WebMessageAsJson);

            double latitude = message.latitude;
            double longitude = message.longitude;

            txtLatitude.Text = latitude.ToString("F6");
            txtLongitude.Text = longitude.ToString("F6");

            satelliteCoordsText.Text = $"Satellite Center:\nLat: {latitude:F6}, Lon: {longitude:F6}";
        }

        private BitmapImage ConvertToBitmapImage(Image<Rgba32> image)
        {
            using var ms = new MemoryStream();
            image.Save(ms, new PngEncoder());
            ms.Seek(0, SeekOrigin.Begin);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }

        private void RenderHeightmap3D(float[,] heightmap, double verticalScale)
        {
            var meshBuilder = new MeshBuilder();
            int rows = heightmap.GetLength(0);
            int cols = heightmap.GetLength(1);

            double horizontalScale = 1.0;

            for (int y = 0; y < rows - 1; y++)
            {
                for (int x = 0; x < cols - 1; x++)
                {
                    var p00 = new Point3D(x * horizontalScale, y * horizontalScale, heightmap[y, x] * verticalScale);
                    var p10 = new Point3D((x + 1) * horizontalScale, y * horizontalScale, heightmap[y, x + 1] * verticalScale);
                    var p01 = new Point3D(x * horizontalScale, (y + 1) * horizontalScale, heightmap[y + 1, x] * verticalScale);
                    var p11 = new Point3D((x + 1) * horizontalScale, (y + 1) * horizontalScale, heightmap[y + 1, x + 1] * verticalScale);

                    meshBuilder.AddQuad(p00, p10, p11, p01);
                }
            }

            var mesh = meshBuilder.ToMesh();
            var material = Materials.Green;

            terrainViewport.Children.Clear();
            terrainViewport.Children.Add(new DefaultLights());
            terrainViewport.Children.Add(new ModelVisual3D { Content = new GeometryModel3D(mesh, material) });

            terrainViewport.Camera.Position = new Point3D(cols / 2, -rows, rows / 2);
            terrainViewport.Camera.LookDirection = new Vector3D(0, rows, -rows / 2);
            terrainViewport.Camera.UpDirection = new Vector3D(0, 0, 1);
            terrainViewport.Camera.FarPlaneDistance = 10000;
        }

        private void HeightScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_latestHeightmap != null)
            {
                RenderHeightmap3D(_latestHeightmap, heightScaleSlider.Value);
            }
        }

        private void ResolutionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResolutionComboBox.SelectedItem is ComboBoxItem item)
            {
                _selectedResolution = int.Parse(item.Content.ToString());
                if (HighResWarningText != null)
                    HighResWarningText.Visibility = (_selectedResolution >= 4096) ? Visibility.Visible : Visibility.Collapsed;

                if (webViewMap.CoreWebView2 != null)
                {
                    webViewMap.CoreWebView2.PostWebMessageAsJson($"{{\"areaSize\": {_selectedResolution}}}");
                }
            }
        }

        private List<Rgba32> ExtractPalette(Image<Rgba32> image, int colorCount = 16)
        {
            var colorFrequency = new Dictionary<Rgba32, int>();

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var color = image[x, y];
                    if (colorFrequency.ContainsKey(color))
                        colorFrequency[color]++;
                    else
                        colorFrequency[color] = 1;
                }
            }

            return colorFrequency
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => kvp.Key)
                .Take(colorCount)
                .ToList();
        }

        private void GenerateMaterialsFromPalette(List<Rgba32> palette, string outputDir)
        {
            var materials = new Dictionary<string, object>();
            var terrainDir = Path.Combine(outputDir, "art", "terrain");
            Directory.CreateDirectory(terrainDir);

            for (int i = 0; i < palette.Count; i++)
            {
                var color = palette[i];
                var materialName = $"generated_{i}";
                var basecolorFilename = $"{materialName}_basecolor.png";
                var basecolorPath = Path.Combine(terrainDir, basecolorFilename);

                // 🖼 Create a simple swatch (16x16) and fill with this color
                using (var swatch = new Image<Rgba32>(16, 16))
                {
                    swatch.Mutate(ctx => ctx.BackgroundColor(color));
                    swatch.Save(basecolorPath);
                }

                // 🟫 Roughness heuristic: darker colors = rougher surface
                float brightness = (color.R + color.G + color.B) / 3f / 255f;
                float roughness = Math.Clamp(1.0f - brightness, 0.2f, 1.0f);

                // 🧱 Build JSON entry
                var materialEntry = new Dictionary<string, object>
                {
                    ["name"] = materialName,
                    ["maps"] = new Dictionary<string, object>
                    {
                        ["baseColor"] = $"art/terrain/{basecolorFilename}"
                    },
                    ["params"] = new Dictionary<string, object>
                    {
                        ["roughness"] = roughness
                    }
                };

                materials[materialName] = materialEntry;
            }

            // 🧾 Save materials.json
            var materialJsonPath = Path.Combine(outputDir, "materials.json");
            File.WriteAllText(materialJsonPath, JsonConvert.SerializeObject(materials, Formatting.Indented));
        }

        private ushort[,] NormalizeHeightmap(float[,] heightmap)
        {
            int width = heightmap.GetLength(1);
            int height = heightmap.GetLength(0);

            float min = float.MaxValue, max = float.MinValue;
            foreach (float val in heightmap)
            {
                if (val < min) min = val;
                if (val > max) max = val;
            }

            var normalized = new ushort[height, width];

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float norm = (heightmap[y, x] - min) / (max - min);
                    normalized[y, x] = (ushort)(norm * 65535);
                }

            return normalized;
        }

        private void WriteTerFile(string path, ushort[,] heightmap, byte[,] layerMap, List<string> materialNames)
        {
            using var fs = new FileStream(path, FileMode.Create);
            using var bw = new BinaryWriter(fs);

            int width = heightmap.GetLength(1);
            int height = heightmap.GetLength(0);

            // 1. Header
            bw.Write((byte)9); // Version
            bw.Write((UInt32)width); // Terrain size (must be square)

            // Terrain settings (can be tuned later)
            bw.Write(1024.0f); // terrainSize
            bw.Write(1.0f);    // squareSize
            bw.Write(255.0f);  // heightScale
            bw.Write((UInt32)materialNames.Count); // number of materials

            // 2. Heightmap data
            for (int y = height - 1; y >= 0; y--) // BeamNG = bottom to top!
                for (int x = 0; x < width; x++)
                    bw.Write(heightmap[y, x]);

            // 3. Layer map
            for (int y = height - 1; y >= 0; y--)
                for (int x = 0; x < width; x++)
                    bw.Write(layerMap[y, x]);

            // 4. Layer texture data (zeroed for now)
            for (int i = 0; i < width * height; i++)
                bw.Write((byte)0);

            // 5. Material names
            bw.Write((UInt32)materialNames.Count);
            foreach (var name in materialNames)
            {
                byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(name);
                bw.Write((byte)nameBytes.Length);
                bw.Write(nameBytes);
            }
        }


        private byte[,] GenerateLayerMap(Image<Rgba32> image, List<Rgba32> palette)
        {
            int width = image.Width;
            int height = image.Height;
            byte[,] layerMap = new byte[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var pixel = image[x, y];
                    int closestIndex = 0;
                    double closestDistance = double.MaxValue;

                    for (int i = 0; i < palette.Count; i++)
                    {
                        var pal = palette[i];
                        double dist = Math.Pow(pixel.R - pal.R, 2) +
                                      Math.Pow(pixel.G - pal.G, 2) +
                                      Math.Pow(pixel.B - pal.B, 2);

                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            closestIndex = i;
                        }
                    }

                    layerMap[y, x] = (byte)closestIndex;
                }
            }

            return layerMap;
        }

        private void WriteJsonFiles(string exportDir, string mapName, int resolution, List<string> materialNames)
        {
            string terrainJsonPath = Path.Combine(exportDir, $"{mapName}.terrain.json");
            string mainLevelJsonPath = Path.Combine(exportDir, "main.level.json");

            // 🧱 .terrain.json
            var terrainJson = new
            {
                name = mapName,
                terrainAsset = $"{mapName}.ter",
                size = new[] { resolution, resolution },
                squareSize = 1,
                materials = materialNames
            };

            // 🌍 main.level.json
            var mainJson = new
            {
                name = mapName,
                objects = new[]
                {
            new {
                className = "TerrainBlock",
                data = $"{mapName}.terrain.json",
                position = new[] { 0, 0, 0 }
            }
        }
            };

            File.WriteAllText(terrainJsonPath, JsonConvert.SerializeObject(terrainJson, Formatting.Indented));
            File.WriteAllText(mainLevelJsonPath, JsonConvert.SerializeObject(mainJson, Formatting.Indented));
        }






        private async void btnUpdateMap_Click(object sender, RoutedEventArgs e)
        {
            double lat = double.Parse(txtLatitude.Text);
            double lon = double.Parse(txtLongitude.Text);

            string openTopoKey = "";
            string mapboxToken = "";

            var demData = await _dataService.FetchDEMAsync(lat, lon, _selectedResolution, openTopoKey);
            var imageryData = await _dataService.FetchSatelliteImageryAsync(lat, lon, _selectedResolution, mapboxToken);

            if (demData != null && imageryData != null)
            {
                var demDataset = _dataService.LoadDemDataset(demData);
                _latestHeightmap = _dataService.GetHeightmapFromDataset(demDataset, _selectedResolution);
                RenderHeightmap3D(_latestHeightmap, heightScaleSlider.Value);
                terrainCoordsText.Text = $"Terrain Center:\nLat: {lat:F6}, Lon: {lon:F6}";

                satelliteImage = _dataService.LoadSatelliteImage(imageryData);
                satellitePreview.Source = ConvertToBitmapImage(satelliteImage);

                // ✅ Extract palette colors for material generation step
                var palette = ExtractPalette(satelliteImage);
                // TODO: use this in material generation logic
                // GenerateMaterialsFromPalette(palette, @"C:\Temp\BeamNGExportTest");

                _dataService.DisposeDataset(demDataset);
            }
            else
            {
                MessageBox.Show("Error retrieving data from APIs.");
            }
        }

        private void btnExport_Click(object sender, RoutedEventArgs e) 
        {
            string exportDir = @"C:\Temp\BeamNGExportTest"; // Later this can be dynamic

            // 1. Normalize the heightmap
            ushort[,] normalizedHeights = NormalizeHeightmap(_latestHeightmap);

            // 2. Regenerate palette from satellite image if not cached
            var palette = ExtractPalette(satelliteImage); // Reuse satelliteImage from earlier step

            // 3. Generate layer map
            byte[,] layerMap = GenerateLayerMap(satelliteImage, palette);

            // 4. Create list of material names
            var materialNames = palette.Select((c, i) => $"generated_{i}").ToList();

            // 5. Write .ter file
            string terPath = Path.Combine(exportDir, "YourMap.ter");
            WriteTerFile(terPath, normalizedHeights, layerMap, materialNames);

            // 6. Generate materials and textures
            GenerateMaterialsFromPalette(palette, exportDir);

            // 7. Write JSON files
            WriteJsonFiles(exportDir, "YourMap", _selectedResolution, materialNames);
        }

    }
}
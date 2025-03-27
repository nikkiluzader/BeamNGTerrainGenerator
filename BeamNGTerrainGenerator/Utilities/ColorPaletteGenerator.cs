using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace BeamNGTerrainGenerator.Utilities
{
    class ColorPaletteGenerator
    {
        public static List<Color> GeneratePalette(string imagePath, int paletteSize)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Image file not found.", imagePath);

            if (paletteSize <= 0)
                throw new ArgumentException("Palette size must be greater than zero.");

            using var bitmap = new Bitmap(imagePath);
            var pixels = new List<Color>();

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var color = bitmap.GetPixel(x, y);
                    if (color.A > 128) // skip highly transparent pixels
                        pixels.Add(color);
                }
            }

            var pixelVectors = pixels.Select(c => new double[] { c.R, c.G, c.B }).ToList();
            var clusters = KMeansCluster(pixelVectors, paletteSize);

            return clusters.Select(c => Color.FromArgb((int)c[0], (int)c[1], (int)c[2])).ToList();
        }

        private static List<double[]> KMeansCluster(List<double[]> data, int k, int maxIterations = 100)
        {
            var random = new Random();
            var centroids = data.OrderBy(_ => random.Next()).Take(k).Select(p => (double[])p.Clone()).ToList();
            List<int> labels = new List<int>(new int[data.Count]);

            for (int iter = 0; iter < maxIterations; iter++)
            {
                for (int i = 0; i < data.Count; i++)
                {
                    double minDist = double.MaxValue;
                    for (int j = 0; j < k; j++)
                    {
                        var dist = Distance(data[i], centroids[j]);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            labels[i] = j;
                        }
                    }
                }

                var newCentroids = new List<double[]>(new double[k][]);
                var counts = new int[k];

                for (int i = 0; i < k; i++)
                    newCentroids[i] = new double[3];

                for (int i = 0; i < data.Count; i++)
                {
                    int cluster = labels[i];
                    for (int d = 0; d < 3; d++)
                        newCentroids[cluster][d] += data[i][d];
                    counts[cluster]++;
                }

                for (int i = 0; i < k; i++)
                    if (counts[i] > 0)
                        for (int d = 0; d < 3; d++)
                            newCentroids[i][d] /= counts[i];

                centroids = newCentroids;
            }

            return centroids;
        }

        private static double Distance(double[] a, double[] b)
        {
            double sum = 0;
            for (int i = 0; i < a.Length; i++)
                sum += Math.Pow(a[i] - b[i], 2);
            return Math.Sqrt(sum);
        }

        // 🧪 Test method
        public static void TestPalette(string imagePath)
        {
            var palette = GeneratePalette(imagePath, 16);
            Console.WriteLine("Generated Color Palette (16 colors):");
            foreach (var color in palette)
                Console.WriteLine($"RGB({color.R}, {color.G}, {color.B})");
        }
    }
}

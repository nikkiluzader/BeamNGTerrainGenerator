using System.Net.Http;
using OSGeo.GDAL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;

namespace BeamNGTerrainGenerator.Services
{
    public class DataRetrievalService
    {
        private readonly HttpClient _httpClient;

        public DataRetrievalService()
        {
            _httpClient = new HttpClient();
        }

        public Image<Rgba32> LoadSatelliteImage(byte[] imageData)
        {
            using var ms = new MemoryStream(imageData);
            return Image.Load<Rgba32>(ms);
        }

        // Fetch DEM from OpenTopography with 20% extra margin
        public async Task<byte[]> FetchDEMAsync(double latitude, double longitude, int resolution, string apiKey)
        {
            double halfSizeMeters = resolution / 2.0 * 1.2;
            double latDeg = halfSizeMeters / 111320.0;
            double lonDeg = halfSizeMeters / (111320.0 * Math.Cos(latitude * Math.PI / 180.0));

            double minLat = latitude - latDeg;
            double maxLat = latitude + latDeg;
            double minLon = longitude - lonDeg;
            double maxLon = longitude + lonDeg;

            string demUrl = $"https://portal.opentopography.org/API/globaldem?demtype=SRTMGL1&west={minLon}&south={minLat}&east={maxLon}&north={maxLat}&outputFormat=GTiff&API_Key={apiKey}";

            var response = await _httpClient.GetAsync(demUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        // Fetch Satellite Imagery from Mapbox with extra margin
        public async Task<byte[]> FetchSatelliteImageryAsync(double latitude, double longitude, int resolution, string mapboxToken)
        {
            double halfSizeMeters = resolution / 2.0 * 1.2;
            double latDeg = halfSizeMeters / 111320.0;
            double lonDeg = halfSizeMeters / (111320.0 * Math.Cos(latitude * Math.PI / 180.0));

            double minLat = latitude - latDeg;
            double maxLat = latitude + latDeg;
            double minLon = longitude - lonDeg;
            double maxLon = longitude + lonDeg;

            string bbox = $"[{minLon},{minLat},{maxLon},{maxLat}]";
            int imgSize = (int)Math.Ceiling(resolution * 1.2);
            imgSize = Math.Min(imgSize, 1280);

            string imageryUrl = $"https://api.mapbox.com/styles/v1/mapbox/satellite-v9/static/{bbox}/{imgSize}x{imgSize}?access_token={mapboxToken}";

            var response = await _httpClient.GetAsync(imageryUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        public Dataset LoadDemDataset(byte[] demData)
        {
            GdalConfiguration.ConfigureGdal();

            string tempDemFile = Path.Combine(Path.GetTempPath(), $"temp_dem_{Guid.NewGuid()}.tif");
            File.WriteAllBytes(tempDemFile, demData);

            return Gdal.Open(tempDemFile, Access.GA_ReadOnly);
        }

        public void DisposeDataset(Dataset dataset)
        {
            string filePath = dataset.GetDescription();
            dataset.Dispose();

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        public float[,] GetHeightmapFromDataset(Dataset demDataset, int finalResolution)
        {
            Band rasterBand = demDataset.GetRasterBand(1);

            int width = rasterBand.XSize;
            int height = rasterBand.YSize;

            float[] elevationData = new float[width * height];
            rasterBand.ReadRaster(0, 0, width, height, elevationData, width, height, 0, 0);

            // Calculate scale factor (30m to 1m)
            float scaleFactor = 30.0f;
            float[,] heightmap = new float[finalResolution, finalResolution];

            // Calculate the center offset to maintain the same center point
            float offsetX = (width - finalResolution / scaleFactor) / 2;
            float offsetY = (height - finalResolution / scaleFactor) / 2;

            // Bicubic interpolation
            for (int row = 0; row < finalResolution; row++)
            {
                for (int col = 0; col < finalResolution; col++)
                {
                    // Convert to source coordinates (30m resolution)
                    float srcX = col / scaleFactor + offsetX;
                    float srcY = row / scaleFactor + offsetY;

                    // Get the four surrounding points
                    int x0 = (int)Math.Floor(srcX);
                    int y0 = (int)Math.Floor(srcY);
                    int x1 = Math.Min(x0 + 1, width - 1);
                    int y1 = Math.Min(y0 + 1, height - 1);

                    // Calculate fractional parts
                    float fx = srcX - x0;
                    float fy = srcY - y0;

                    // Get the 16 surrounding points for bicubic interpolation
                    float[,] points = new float[4, 4];
                    for (int i = -1; i <= 2; i++)
                    {
                        for (int j = -1; j <= 2; j++)
                        {
                            int x = Math.Clamp(x0 + i, 0, width - 1);
                            int y = Math.Clamp(y0 + j, 0, height - 1);
                            points[i + 1, j + 1] = elevationData[y * width + x];
                        }
                    }

                    // Perform bicubic interpolation
                    heightmap[row, col] = BicubicInterpolate(points, fx, fy);
                }
            }

            return heightmap;
        }

        private float BicubicInterpolate(float[,] p, float x, float y)
        {
            float[] arr = new float[4];
            arr[0] = CubicInterpolate(p[0, 0], p[1, 0], p[2, 0], p[3, 0], x);
            arr[1] = CubicInterpolate(p[0, 1], p[1, 1], p[2, 1], p[3, 1], x);
            arr[2] = CubicInterpolate(p[0, 2], p[1, 2], p[2, 2], p[3, 2], x);
            arr[3] = CubicInterpolate(p[0, 3], p[1, 3], p[2, 3], p[3, 3], x);
            return CubicInterpolate(arr[0], arr[1], arr[2], arr[3], y);
        }

        private float CubicInterpolate(float p0, float p1, float p2, float p3, float x)
        {
            return p1 + 0.5f * x * (p2 - p0 + x * (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3 + x * (3.0f * (p1 - p2) + p3 - p0)));
        }
    }
}

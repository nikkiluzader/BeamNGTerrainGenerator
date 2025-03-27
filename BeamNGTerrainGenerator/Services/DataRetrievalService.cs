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
        public async Task<byte[]?> FetchDEMAsync(double latitude, double longitude, int resolution, string apiKey)
        {
            double halfSizeMeters = resolution / 2.0 * 1.2;
            double latDeg = halfSizeMeters / 111320.0;
            double lonDeg = halfSizeMeters / (111320.0 * Math.Cos(latitude * Math.PI / 180.0));

            double minLat = latitude - latDeg;
            double maxLat = latitude + latDeg;
            double minLon = longitude - lonDeg;
            double maxLon = longitude + lonDeg;

            string demUrl = $"https://portal.opentopography.org/API/usgsdem?datasetName=USGS1m&west={minLon}&south={minLat}&east={maxLon}&north={maxLat}&outputFormat=GTiff&API_Key={apiKey}";

            try
            {
                var response = await _httpClient.GetAsync(demUrl);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEM fetch error: {ex.Message}");
                return null;
            }
        }

        // Fetch Satellite Imagery from Mapbox with extra margin
        public async Task<byte[]?> FetchSatelliteImageryAsync(double latitude, double longitude, int resolution, string mapboxToken)
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

            try
            {
                var response = await _httpClient.GetAsync(imageryUrl);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Imagery fetch error: {ex.Message}");
                return null;
            }
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

            int offsetX = (width - finalResolution) / 2;
            int offsetY = (height - finalResolution) / 2;

            float[,] heightmap = new float[finalResolution, finalResolution];
            for (int row = 0; row < finalResolution; row++)
                for (int col = 0; col < finalResolution; col++)
                    heightmap[row, col] = elevationData[(row + offsetY) * width + (col + offsetX)];

            return heightmap;
        }
    }
}
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using System.IO;

namespace BeamNGTerrainGenerator.Services
{
    public static class GdalConfiguration
    {
        private static bool _configured = false;

        public static void ConfigureGdal()
        {
            if (_configured) return;

            // Get GDAL native folder path
            var executingDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            // Set GDAL environment paths
            Gdal.SetConfigOption("GDAL_DATA", Path.Combine(executingDirectory, "gdal-data"));
            Gdal.SetConfigOption("GDAL_DRIVER_PATH", Path.Combine(executingDirectory, "gdalplugins"));
            Osr.SetPROJSearchPaths(new[] { Path.Combine(executingDirectory, "projlib") });

            // Register all drivers
            Gdal.AllRegister();
            Ogr.RegisterAll();

            _configured = true;
        }
    }
}

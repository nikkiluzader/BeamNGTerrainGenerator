
# 🚀 BeamNG Terrain Generator

## Install Required Development Tools

To develop and run this C# BeamNG terrain generation tool:

1. **Visual Studio 2022**  
   - Install the **“.NET Desktop Development”** workload during setup.

2. **.NET 8 SDK**  
   - Download if not automatically installed by Visual Studio.

---

## 🌐 Install NuGet Dependencies

Install the following packages via NuGet Package Manager:

| Package | Purpose |
|--------|---------|
| `GDAL` / `GDAL.Native` | GIS processing, DEM reprojection, GeoTIFF parsing |
| `ProjNet` | Coordinate system transformations |
| `SixLabors.ImageSharp` | Image manipulation and texture export |
| `Newtonsoft.Json` | JSON parsing and serialization |
| `HelixToolkit.Core` | 3D preview rendering (terrain mesh, materials) |
| `Microsoft.Web.WebView2` | Embeds an interactive web map UI using Mapbox |
| *(Built-in)* `System.Net.Http` | API calls (e.g., OpenTopography, Mapbox) |

---

## 🔑 API Keys

You’ll need API tokens for the following services:

- **Mapbox**  
  - Sign up at [Mapbox](https://www.mapbox.com/)  
  - Retrieve your **access token** for satellite imagery

- **OpenTopography**  
  - Sign up at [OpenTopography](https://opentopography.org/)  
  - Retrieve your **API key** to download elevation data

---

## ⚙ Dev Environment Summary

- ✅ Visual Studio 2022
- ✅ .NET 8 SDK
- ✅ NuGet: `GDAL`, `GDAL.Native`, `ProjNet`, `Newtonsoft.Json`, `ImageSharp`, `HelixToolkit.Core`, `WebView2`
- ✅ APIs: OpenTopography, Mapbox

---

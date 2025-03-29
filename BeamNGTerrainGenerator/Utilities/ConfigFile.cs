using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grille.BeamNG.IO.Text;
using Grille.BeamNG.SceneTree;

namespace BeamNGTerrainGenerator.Utilities;

internal class ConfigFile : JsonDictWrapper
{
    public JsonDictProperty<string> LevelsPath { get; }
    public JsonDictProperty<string> ApiKeyOpenTopography { get; }
    public JsonDictProperty<string> ApiKeyMapbox { get; }
    public JsonDictProperty<string> PathDefaultHeightmap { get; }
    public JsonDictProperty<string> PathDefaultColormap { get; }

    public JsonDictProperty<JsonDict> TemplateMaterial { get; }

    public ConfigFile(JsonDict dict) : base(dict)
    {
        LevelsPath = new(this, "LevelsPath");
        ApiKeyOpenTopography = new(this, "ApiKeyOpenTopography");
        ApiKeyMapbox = new(this, "ApiKeyMapbox");
        PathDefaultHeightmap = new(this, "PathDefaultHeightmap");
        PathDefaultColormap = new(this, "PathDefaultColormap");
        TemplateMaterial = new(this, "TemplateMaterial");
    }

    public static ConfigFile LoadDefault()
    {
        try
        {
            const string filename = "config.json";
            if (File.Exists(filename))
            {
                using var stream = File.OpenRead(filename);
                var dict = JsonDictSerializer.Deserialize(stream);
                return new ConfigFile(dict);
            }
        }
        catch (Exception e)
        {
            ExceptionBox.Show(e);
        }
        return new ConfigFile(new JsonDict());
    }
}

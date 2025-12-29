using System.Text.Json;

namespace PolarDrive.Data.Constants;

public enum FuelType
{
    Electric,
}

public class VehicleVariant
{
    public string[] Trims { get; set; } = [];
    public string[] Colors { get; set; } = [];
    public FuelType FuelType { get; set; }
}

public class VehicleOptionsRoot
{
    public Dictionary<string, BrandOptions> Options { get; set; } = new();
}

public class BrandOptions
{
    public Dictionary<string, ModelOptions> Models { get; set; } = new();
}

public class ModelOptions
{
    public string FuelType { get; set; } = "Electric";
    public string[] Trims { get; set; } = [];
    public string[] Colors { get; set; } = [];
}

public static class VehicleConstants
{
    private static Dictionary<string, Dictionary<string, VehicleVariant>>? _options;
    private static readonly object _lock = new();
    private static string _configPath = "/app/config/vehicle-options.json";

    // Permette di configurare il path del file (utile per testing o ambienti diversi)
    public static void SetConfigPath(string path)
    {
        _configPath = path;
        _options = null; // Reset cache
    }

    public static Dictionary<string, Dictionary<string, VehicleVariant>> Options
    {
        get
        {
            if (_options == null)
            {
                lock (_lock)
                {
                    if (_options == null)
                    {
                        _options = LoadOptionsFromFile();
                    }
                }
            }
            return _options;
        }
    }

    private static Dictionary<string, Dictionary<string, VehicleVariant>> LoadOptionsFromFile()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                Console.WriteLine($"WARNING: Vehicle options file not found at {_configPath}, using fallback defaults");
                return GetFallbackOptions();
            }

            var jsonString = File.ReadAllText(_configPath);
            var root = JsonSerializer.Deserialize<VehicleOptionsRoot>(jsonString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (root?.Options == null)
            {
                Console.WriteLine("WARNING: Invalid vehicle options JSON structure, using fallback defaults");
                return GetFallbackOptions();
            }

            var result = new Dictionary<string, Dictionary<string, VehicleVariant>>();

            foreach (var brand in root.Options)
            {
                result[brand.Key] = new Dictionary<string, VehicleVariant>();

                foreach (var model in brand.Value.Models)
                {
                    result[brand.Key][model.Key] = new VehicleVariant
                    {
                        FuelType = Enum.Parse<FuelType>(model.Value.FuelType),
                        Trims = model.Value.Trims,
                        Colors = model.Value.Colors
                    };
                }
            }

            Console.WriteLine($"Successfully loaded vehicle options from {_configPath}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR loading vehicle options from {_configPath}: {ex.Message}");
            return GetFallbackOptions();
        }
    }

    private static Dictionary<string, Dictionary<string, VehicleVariant>> GetFallbackOptions()
    {
        // Fallback defaults in case file is not available
        return new()
        {
            ["Tesla"] = new Dictionary<string, VehicleVariant>
            {
                ["Model 3"] = new VehicleVariant
                {
                    FuelType = FuelType.Electric,
                    Trims = ["Trazione posteriore"],
                    Colors = ["Bianco Perla"]
                }
            }
        };
    }

    // Metodo per ricaricare le opzioni (utile per reload senza restart)
    public static void ReloadOptions()
    {
        lock (_lock)
        {
            _options = LoadOptionsFromFile();
        }
    }

    public static string[] ValidBrands => [.. Options.Keys];

    public static string[] ValidFuelTypes => Enum.GetNames<FuelType>();

    public static string[] ValidModels =>
        [.. Options.SelectMany(b => b.Value.Keys).Distinct()];

    public static string[] ValidTrims =>
        [.. Options.SelectMany(b => b.Value.Values.SelectMany(v => v.Trims)).Distinct()];

    public static string[] ValidColors =>
        [.. Options.SelectMany(b => b.Value.Values.SelectMany(v => v.Colors)).Distinct()];
}

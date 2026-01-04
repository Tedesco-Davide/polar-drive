using System.Text.Json;

namespace PolarDrive.Data.Constants;

#region JSON Model Classes

public class VehicleVariant
{
    // Allestimenti disponibili per il modello
    public string[] Trims { get; set; } = [];

    // Colori disponibili per il modello
    public string[] Colors { get; set; } = [];

    // Tipo di alimentazione (electric, hybrid, etc.)
    public string FuelType { get; set; } = "";
}

public class BrandApiConfig
{
    // URL base delle API del brand
    public string BaseUrl { get; set; } = "";

    // Endpoint per ottenere lista veicoli
    public string VehicleListEndpoint { get; set; } = "";

    // Endpoint per ottenere dati veicolo
    public string VehicleDataEndpoint { get; set; } = "";

    // Endpoint per health check API
    public string HealthEndpoint { get; set; } = "";

    // URL autorizzazione OAuth
    public string OauthAuthorizeUrl { get; set; } = "";

    // URL per ottenere token OAuth
    public string OauthTokenUrl { get; set; } = "";
}

public class BrandConfig
{
    // Identificativo lowercase del brand (es. "tesla")
    public string BrandKey { get; set; } = "";

    // Configurazione API del brand
    public BrandApiConfig Api { get; set; } = new();

    // Dizionario modelli disponibili per il brand
    public Dictionary<string, VehicleVariant> Models { get; set; } = new();
}

public class VehicleOptionsRoot
{
    // Root object del JSON, contiene tutti i brand
    public Dictionary<string, BrandOptions> Options { get; set; } = new();
}

public class BrandOptions
{
    // Identificativo lowercase del brand
    public string BrandKey { get; set; } = "";

    // Configurazione API (opzionale)
    public BrandApiConfigJson? Api { get; set; }

    // Modelli disponibili per il brand
    public Dictionary<string, ModelOptions> Models { get; set; } = new();
}

public class BrandApiConfigJson
{
    // URL base delle API del brand
    public string BaseUrl { get; set; } = "";

    // Endpoint per ottenere lista veicoli
    public string VehicleListEndpoint { get; set; } = "";

    // Endpoint per ottenere dati veicolo
    public string VehicleDataEndpoint { get; set; } = "";

    // Endpoint per health check API
    public string HealthEndpoint { get; set; } = "";

    // URL autorizzazione OAuth
    public string OauthAuthorizeUrl { get; set; } = "";

    // URL per ottenere token OAuth
    public string OauthTokenUrl { get; set; } = "";
}

public class ModelOptions
{
    // Tipo di alimentazione (electric, hybrid, etc.)
    public string FuelType { get; set; } = "";

    // Allestimenti disponibili per il modello
    public string[] Trims { get; set; } = [];

    // Colori disponibili per il modello
    public string[] Colors { get; set; } = [];
}

#endregion

/// <summary>
/// Servizio per caricare configurazioni veicoli da vehicle-options.json con supporto reload.
/// Contiene brand, modelli, allestimenti, colori e configurazioni API per ogni brand supportato.
/// </summary>
public static class VehicleConstants
{
    /// <summary>
    /// Brand keys (lowercase identifiers) - costanti compile-time per switch/case
    /// </summary>
    public static class VehicleBrand
    {
        public const string TESLA = "tesla";
        // Aggiungere qui altri brand quando supportati
    }

    // Cache delle configurazioni brand caricate
    private static Dictionary<string, BrandConfig>? _brandConfigs;

    // Lock per thread-safety nel caricamento
    private static readonly Lock _lock = new();

    // Path del file JSON di configurazione
    private static readonly string _configPath = "/app/config/vehicle-options.json";

    /// <summary>
    /// Ottiene le configurazioni brand (lazy loading con cache thread-safe)
    /// </summary>
    public static Dictionary<string, BrandConfig> BrandConfigs
    {
        get
        {
            if (_brandConfigs == null)
            {
                lock (_lock)
                {
                    _brandConfigs ??= LoadBrandConfigsFromFile();
                }
            }
            return _brandConfigs;
        }
    }

    private static Dictionary<string, BrandConfig> LoadBrandConfigsFromFile()
    {
        if (!File.Exists(_configPath))
            throw new FileNotFoundException($"Vehicle options file not found at {_configPath}");

        var jsonString = File.ReadAllText(_configPath);
        var root = JsonSerializer.Deserialize<VehicleOptionsRoot>(jsonString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (root?.Options == null)
            throw new InvalidOperationException($"Invalid vehicle options JSON structure in {_configPath}");

        var result = new Dictionary<string, BrandConfig>();

        foreach (var brand in root.Options)
        {
            var brandConfig = new BrandConfig
            {
                BrandKey = brand.Value.BrandKey ?? brand.Key.ToLowerInvariant(),
                Api = brand.Value.Api != null ? new BrandApiConfig
                {
                    BaseUrl = brand.Value.Api.BaseUrl,
                    VehicleListEndpoint = brand.Value.Api.VehicleListEndpoint,
                    VehicleDataEndpoint = brand.Value.Api.VehicleDataEndpoint,
                    HealthEndpoint = brand.Value.Api.HealthEndpoint,
                    OauthAuthorizeUrl = brand.Value.Api.OauthAuthorizeUrl,
                    OauthTokenUrl = brand.Value.Api.OauthTokenUrl
                } : new BrandApiConfig(),
                Models = []
            };

            foreach (var model in brand.Value.Models)
            {
                brandConfig.Models[model.Key] = new VehicleVariant
                {
                    FuelType = model.Value.FuelType,
                    Trims = model.Value.Trims,
                    Colors = model.Value.Colors
                };
            }

            result[brand.Key] = brandConfig;
        }

        Console.WriteLine($"Successfully loaded vehicle options from {_configPath}");
        return result;
    }

    // === Proprietà per validazione ===

    // Lista di tutti i brand validi configurati
    public static string[] ValidBrands => [.. BrandConfigs.Keys];

    // Lista di tutti i modelli validi (aggregati da tutti i brand)
    public static string[] ValidModels =>
        [.. BrandConfigs.SelectMany(b => b.Value.Models.Keys).Distinct()];
}

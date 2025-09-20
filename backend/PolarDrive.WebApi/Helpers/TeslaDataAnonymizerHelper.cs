using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

public static class TeslaDataAnonymizerHelper
{
    private static readonly Regex VinRegex = new(@"\b[A-HJ-NPR-Z0-9]{17}\b", RegexOptions.IgnoreCase);
    private static readonly Regex EmailRegex = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
    
    /// <summary>
    /// Anonimizza i dati Tesla prima del salvataggio per compliance Fleet API
    /// </summary>
    public static string AnonymizeVehicleData(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return rawJson;

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var anonymizedJson = AnonymizeJsonElement(document.RootElement);
            return JsonSerializer.Serialize(anonymizedJson, new JsonSerializerOptions 
            { 
                WriteIndented = false 
            });
        }
        catch
        {
            // Se il parsing fallisce, applica anonimizzazione via regex
            return AnonymizeViaRegex(rawJson);
        }
    }

    private static object? AnonymizeJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>();
                foreach (var property in element.EnumerateObject())
                {
                    var key = property.Name.ToLowerInvariant();
                    var value = property.Value;

                    // Anonimizza campi specifici
                    if (IsVinField(key))
                    {
                        dict[property.Name] = AnonymizeVin(value.GetString() ?? string.Empty);
                    }
                    else if (IsLocationField(key))
                    {
                        dict[property.Name] = AnonymizeCoordinate(value);
                    }
                    else if (IsEmailField(key))
                    {
                        dict[property.Name] = AnonymizeEmail(value.GetString() ?? string.Empty);
                    }
                    else if (IsAddressField(key))
                    {
                        dict[property.Name] = AnonymizeAddress(value.GetString() ?? string.Empty);
                    }
                    else
                    {
                        dict[property.Name] = AnonymizeJsonElement(value);
                    }
                }
                return dict;

            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(AnonymizeJsonElement(item));
                }
                return list;

            case JsonValueKind.String:
                var stringValue = element.GetString();
                return AnonymizeStringValue(stringValue ?? string.Empty);

            default:
                return GetJsonValue(element);
        }
    }

    private static bool IsVinField(string fieldName)
    {
        return fieldName.Contains("vin") || fieldName.Contains("vehicle_id");
    }

    private static bool IsLocationField(string fieldName)
    {
        return fieldName.Contains("latitude") || fieldName.Contains("longitude") || 
               fieldName.Contains("lat") || fieldName.Contains("lon");
    }

    private static bool IsEmailField(string fieldName)
    {
        return fieldName.Contains("email") || fieldName.Contains("mail");
    }

    private static bool IsAddressField(string fieldName)
    {
        return fieldName.Contains("address") || fieldName.Contains("location_name") || 
               fieldName.Contains("site_name") || fieldName.Contains("home_address");
    }

    private static string AnonymizeVin(string vin)
    {
        if (string.IsNullOrEmpty(vin) || vin.Length < 4)
            return vin;

        // Mantiene solo gli ultimi 4 caratteri
        return string.Concat("***************", vin.AsSpan(vin.Length - 4));
    }

    private static object? AnonymizeCoordinate(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            var coordinate = element.GetDecimal();
            // Riduce precisione a 2 decimali (circa 1km di precisione)
            return Math.Round(coordinate, 2);
        }
        return GetJsonValue(element);
    }

    private static string AnonymizeEmail(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            return email;

        // Sostituisce con hash della prima parte + dominio generico
        var parts = email.Split('@');
        var hash = CreateShortHash(parts[0]);
        return $"user_{hash}@domain.com";
    }

    private static string AnonymizeAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
            return address;

        // Estrae solo città/regione se riconoscibile, altrimenti "Unknown Location"
        if (address.Contains("Milano") || address.Contains("Milan"))
            return "Milano, IT";
        if (address.Contains("Roma") || address.Contains("Rome"))
            return "Roma, IT";
        if (address.Contains("Torino") || address.Contains("Turin"))
            return "Torino, IT";
        
        return "Location, IT";
    }

    private static string AnonymizeStringValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Anonimizza VIN se trovato nella stringa
        if (VinRegex.IsMatch(value))
        {
            value = VinRegex.Replace(value, match => AnonymizeVin(match.Value));
        }

        // Anonimizza email se trovata nella stringa
        if (EmailRegex.IsMatch(value))
        {
            value = EmailRegex.Replace(value, match => AnonymizeEmail(match.Value));
        }

        return value;
    }

    private static string AnonymizeViaRegex(string json)
    {
        // Fallback per JSON non parsabili
        json = VinRegex.Replace(json, match => AnonymizeVin(match.Value));
        json = EmailRegex.Replace(json, match => AnonymizeEmail(match.Value));
        return json;
    }

    private static string CreateShortHash(string input)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..6]; // Primi 6 caratteri dell'hash
    }

    private static object? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }
}
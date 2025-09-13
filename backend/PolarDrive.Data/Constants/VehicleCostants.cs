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

public static class VehicleConstants
{
    public static readonly Dictionary<string, Dictionary<string, VehicleVariant>> Options =
        new()
        {
            ["Tesla"] = new Dictionary<string, VehicleVariant>
            {
                ["Model 3"] = new VehicleVariant
                {
                    FuelType = FuelType.Electric,
                    Trims =
                        [
                            "Trazione posteriore",
                            "Long Range a trazione posteriore",
                            "Long Range a trazione integrale",
                            "Performance a trazione integrale"
                        ],
                    Colors =
                        [
                            "Bianco Perla",
                            "Blu Oceano",
                            "Nero Diamante",
                            "Grigio Stealth",
                            "Ultra Rosso",
                            "Argento Mercurio"
                        ]
                },
                ["Model Y"] = new VehicleVariant
                {
                    FuelType = FuelType.Electric,
                    Trims =
                        [
                            "Trazione posteriore",
                            "Long Range a trazione posteriore",
                            "Long Range a trazione integrale",
                            "Performance a trazione integrale"
                        ],
                    Colors =
                        [
                            "Bianco Perla",
                            "Blu Oceano",
                            "Nero Diamante",
                            "Grigio Stealth",
                            "Ultra Rosso",
                            "Argento Mercurio"
                        ]
                },
                ["Model S"] = new VehicleVariant
                {
                    FuelType = FuelType.Electric,
                    Trims =
                        [
                            "Trazione integrale",
                            "Plaid",
                        ],
                    Colors =
                        [
                            "Bianco Perla",
                            "Blu Frost",
                            "Nero Diamante",
                            "Grigio Stealth",
                            "Ultra Rosso",
                            "Argento Lunare"
                        ]
                },
                ["Model X"] = new VehicleVariant
                {
                    FuelType = FuelType.Electric,
                    Trims =
                        [
                            "Trazione integrale",
                            "Plaid",
                        ],
                    Colors =
                        [
                            "Bianco Perla",
                            "Blu Frost",
                            "Nero Diamante",
                            "Grigio Stealth",
                            "Ultra Rosso",
                            "Argento Lunare"
                        ]
                }
            }
        };

    public static readonly string[] ValidBrands = [.. Options.Keys];

    public static readonly string[] ValidFuelTypes = Enum.GetNames<FuelType>();

    public static readonly string[] ValidModels =
        [.. Options.SelectMany(b => b.Value.Keys).Distinct()];

    public static readonly string[] ValidTrims =
        [.. Options.SelectMany(b => b.Value.Values.SelectMany(v => v.Trims)).Distinct()];

    public static readonly string[] ValidColors =
        [.. Options.SelectMany(b => b.Value.Values.SelectMany(v => v.Colors)).Distinct()];
}

namespace PolarDrive.Data.Constants;

public enum FuelType
{
    Electric,
    Combustion
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
                    Trims = ["Long Range"],
                    Colors = ["Ultra Red"],
                    FuelType = FuelType.Electric
                }
            },
            ["Polestar"] = new Dictionary<string, VehicleVariant>
            {
                ["Polestar 4"] = new VehicleVariant
                {
                    Trims = ["Long range Single motor"],
                    Colors = ["Snow"],
                    FuelType = FuelType.Electric
                }
            },
            ["Porsche"] = new Dictionary<string, VehicleVariant>
            {
                ["718 Cayman"] = new VehicleVariant
                {
                    Trims = ["GT4RS"],
                    Colors = ["Racing Yellow"],
                    FuelType = FuelType.Combustion
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

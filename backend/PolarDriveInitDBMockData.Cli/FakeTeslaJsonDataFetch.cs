using System.Text.Json;

namespace PolarDriveInitDBMockData.Cli;

public static class FakeTeslaJsonDataFetch
{
    public static string GenerateRawVehicleJson(DateTime ts, Random random)
    {
        var json = new
        {
            response = new
            {
                data = new[]
                {
                    GenerateChargingHistory(ts, random)
                }
            }
        };

        return JsonSerializer.Serialize(json);
    }

    private static object GenerateChargingHistory(DateTime ts, Random random)
    {
        return new
        {
            sessionId = 100000 + random.Next(1000, 9999),
            vin = "5YJJ6677544845943",
            siteLocationName = "Napoli - Tesla Supercharger",
            chargeStartDateTime = ts.AddMinutes(-45).ToString("o"),
            chargeStopDateTime = ts.AddMinutes(-15).ToString("o"),
            unlatchDateTime = ts.ToString("o"),
            countryCode = "IT",
            billingType = "IMMEDIATE",
            vehicleMakeType = "TSLA",
            fees = new object[]
            {
                new {
                    sessionFeeId = 1,
                    feeType = "CHARGING",
                    currencyCode = "EUR",
                    pricingType = "PAYMENT",
                    rateBase = 0.48,
                    rateTier1 = 0,
                    rateTier2 = 0,
                    rateTier3 = (decimal?)null,
                    rateTier4 = (decimal?)null,
                    usageBase = 35,
                    usageTier1 = 0,
                    usageTier2 = 25,
                    usageTier3 = (decimal?)null,
                    usageTier4 = (decimal?)null,
                    totalBase = 16.8,
                    totalTier1 = 0,
                    totalTier2 = 0,
                    totalTier3 = 0,
                    totalTier4 = 0,
                    totalDue = 16.8,
                    netDue = 16.8,
                    uom = "kwh",
                    isPaid = true,
                    status = "PAID"
                },
                new {
                    sessionFeeId = 2,
                    feeType = "PARKING",
                    currencyCode = "EUR",
                    pricingType = "NO_CHARGE",
                    rateBase = 0.0,
                    rateTier1 = 0,
                    rateTier2 = 0,
                    rateTier3 = (decimal?)null,
                    rateTier4 = (decimal?)null,
                    usageBase = 0,
                    usageTier1 = 0,
                    usageTier2 = 0,
                    usageTier3 = (decimal?)null,
                    usageTier4 = (decimal?)null,
                    totalBase = 0,
                    totalTier1 = 0,
                    totalTier2 = 0,
                    totalTier3 = 0,
                    totalTier4 = 0,
                    totalDue = 0,
                    netDue = 0,
                    uom = "min",
                    isPaid = true,
                    status = "PAID"
                }
            },
            invoices = new[]
            {
                new {
                    fileName = "INV-2025-12345.pdf",
                    contentId = "file-abcde-12345",
                    invoiceType = "IMMEDIATE"
                }
            }
        };
    }
}
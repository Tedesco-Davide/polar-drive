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
                data = new[] {
                    new { type = "charging_history", content = GenerateChargingHistory(ts, random) },
                    new { type = "energy_endpoints", content = GenerateEnergyEndpoints(ts, random) }
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

    public static object GenerateEnergyEndpoints(DateTime ts, Random random)
    {
        return new
        {
            backup = new
            {
                response = new
                {
                    code = 201,
                    message = "Updated"
                }
            },
            backup_history = new
            {
                response = new
                {
                    events = new[]
                    {
                        new { timestamp = ts.AddDays(-2).ToString("o"), duration = 3600 },
                        new { timestamp = ts.AddDays(-1).ToString("o"), duration = 7200 }
                    },
                    total_events = 2
                }
            },
            charge_history = new
            {
                response = new
                {
                    charge_history = new[]
                    {
                        new
                        {
                            charge_start_time = new { seconds = new DateTimeOffset(ts.AddHours(-3)).ToUnixTimeSeconds() },
                            charge_duration = new { seconds = 12000 },
                            energy_added_wh = 25000
                        }
                    }
                }
            },
            energy_history = new
            {
                response = new
                {
                    period = "day",
                    time_series = new[]
                    {
                        new
                        {
                            timestamp = ts.ToString("o"),
                            solar_energy_exported = 70940,
                            generator_energy_exported = 0,
                            grid_energy_imported = 521,
                            grid_services_energy_imported = 17.53,
                            grid_services_energy_exported = 3.81,
                            grid_energy_exported_from_solar = 43660,
                            grid_energy_exported_from_generator = 0,
                            grid_energy_exported_from_battery = 19,
                            battery_energy_exported = 10030,
                            battery_energy_imported_from_grid = 80,
                            battery_energy_imported_from_solar = 16800,
                            battery_energy_imported_from_generator = 0,
                            consumer_energy_imported_from_grid = 441,
                            consumer_energy_imported_from_solar = 10480,
                            consumer_energy_imported_from_battery = 10011,
                            consumer_energy_imported_from_generator = 0
                        }
                    }
                }
            },
            grid_import_export = new
            {
                response = new
                {
                    code = 204,
                    message = "Updated"
                }
            },
            live_status = new
            {
                response = new
                {
                    solar_power = random.Next(2500, 4000),
                    energy_left = 18020.89,
                    total_pack_energy = 39343,
                    percentage_charged = 45.80,
                    backup_capable = true,
                    battery_power = -3090,
                    load_power = 2581,
                    grid_status = "Active",
                    grid_power = 2569,
                    island_status = "on_grid",
                    storm_mode_active = false,
                    timestamp = ts.ToString("o")
                }
            },
            off_grid_vehicle_charging_reserve = new
            {
                response = new
                {
                    code = 201,
                    message = "Updated"
                }
            },
            operation = new
            {
                response = new
                {
                    code = 201,
                    message = "Updated"
                }
            },
            products = new
            {
                response = new object[]
                {
                    new
                    {
                        id = 100021,
                        user_id = 429511308124,
                        vehicle_id = 99999,
                        vin = "5YJ3000000NEXUS01",
                        color = (string?)null,
                        access_type = "OWNER",
                        display_name = "Owned",
                        option_codes = "TEST0,COUS",
                        cached_data = (object?)null,
                        mobile_access_disabled = false,
                        granular_access = new { hide_private = false },
                        tokens = new[] { "4f993c5b9e2b937b", "7a3153b1bbb48a96" },
                        state = "online",
                        in_service = false,
                        id_s = "100021",
                        calendar_enabled = false,
                        api_version = (int?)null,
                        backseat_token = (string?)null,
                        backseat_token_updated_at = (string?)null,
                        device_type = "vehicle",
                        command_signing = "off"
                    },
                    new
                    {
                        energy_site_id = 429124,
                        device_type = "energy",
                        resource_type = "battery",
                        site_name = "My Home",
                        id = "STE12345678-12345",
                        gateway_id = "1112345-00-E--TG0123456789",
                        energy_left = 35425,
                        total_pack_energy = 39362,
                        percentage_charged = 90,
                        battery_power = 1000
                    }
                },
                count = 2
            },
            site_info = new
            {
                response = new
                {
                    id = "0000000-00-A--TEST0000000DIN",
                    site_name = "My Home",
                    backup_reserve_percent = 20,
                    default_real_mode = "autonomous",
                    installation_date = ts.ToString("o"),
                    user_settings = new
                    {
                        storm_mode_enabled = true
                    },
                    components = new
                    {
                        solar = true,
                        solar_type = "pv_panel",
                        battery = true,
                        grid = true,
                        backup = true,
                        load_meter = true,
                        storm_mode_capable = true,
                        off_grid_vehicle_charging_reserve_supported = true,
                        solar_value_enabled = true,
                        set_islanding_mode_enabled = true,
                        battery_type = "ac_powerwall",
                        configurable = true
                    },
                    version = "23.12.11 452c76cb",
                    battery_count = 3,
                    nameplate_power = 15000,
                    nameplate_energy = 40500,
                    installation_time_zone = "America/Los_Angeles",
                    max_site_meter_power_ac = 1000000000,
                    min_site_meter_power_ac = -11.726
                }
            },
            storm_mode = new
            {
                response = new
                {
                    code = 201,
                    message = "Updated"
                }
            }
        };
    }
}
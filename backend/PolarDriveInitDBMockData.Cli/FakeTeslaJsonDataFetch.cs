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
                    new { type = "energy_endpoints", content = GenerateEnergyEndpoints(ts, random) },
                    new { type = "partner_public_key", content = GeneratePartnerEndpoints() },
                    new { type = "user_profile", content = GenerateUserEndpoints() },
                    new { type = "vehicle_commands", content = GenerateMockVehicleCommands(ts) }
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

    private static object GeneratePartnerEndpoints()
    {
        return new
        {
            fleet_telemetry_error_vins = new[] { "5YJ3000000NEXUS01", "5YJ3000000NEXUS02" },
            fleet_telemetry_errors = new[]
            {
            new {
                name = "evotesla-client",
                error = "Unable to parse GPS data",
                vin = "5YJ3000000NEXUS01"
            },
            new {
                name = "evotesla-client",
                error = "Battery status timeout",
                vin = "5YJ3000000NEXUS02"
            }
        },
            public_key = "0437d832a7a695151f5a671780a276aa4cf2d6be3b2786465397612a342fcf418e98022d3cedf4e9a6f4b3b160472dee4ca022383d9b4cc4001a0f3023caec58fa"
        };
    }

    private static object GenerateUserEndpoints()
    {
        return new
        {
            feature_config = new
            {
                response = new
                {
                    signaling = new
                    {
                        enabled = true,
                        subscribe_connectivity = false,
                        use_auth_token = false
                    }
                }
            },
            me = new
            {
                response = new
                {
                    email = "test-user@tesla.com",
                    full_name = "Testy McTesterson",
                    profile_image_url = "https://vehicle-files.tesla.com/profile_images/mock.jpg",
                    vault_uuid = Guid.NewGuid().ToString()
                }
            },
            orders = new
            {
                response = new[]
                {
                    new
                    {
                        vehicleMapId = 1234567,
                        referenceNumber = "RN00001234",
                        vin = "5YJ30000000000001",
                        orderStatus = "BOOKED",
                        orderSubstatus = "_Z",
                        modelCode = "m3",
                        countryCode = "IT",
                        locale = "it_IT",
                        mktOptions = "APBS,DV2W,IBB1,PMNG,PRM30,SC04,MDL3,W41B,MT322,CPF0,RSF1,CW03",
                        isB2b = false
                    }
                },
                count = 1
            },
            region = new
            {
                response = new
                {
                    region = "eu",
                    fleet_api_base_url = "https://fleet-api.prd.eu.vn.cloud.tesla.com"
                }
            }
        };
    }

    private static object GenerateMockVehicleCommands(DateTime ts)
    {
        return new object[]
        {
            new {
                command = "actuate_trunk",
                timestamp = ts.ToString("o"),
                parameters = new { which_trunk = "rear" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "add_charge_schedule",
                timestamp = ts.ToString("o"),
                parameters = new { start_time = "22:00", end_time = "06:00" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "add_precondition_schedule",
                timestamp = ts.ToString("o"),
                parameters = new { days = new[] { "mon", "wed", "fri" }, time = "07:30" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "adjust_volume",
                timestamp = ts.ToString("o"),
                parameters = new { level = 5 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "auto_conditioning_start",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "auto_conditioning_stop",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "cancel_software_update",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "charge_max_range",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "charge_port_door_close",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "charge_port_door_open",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "charge_standard",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "charge_start",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "charge_stop",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "clear_pin_to_drive_admin",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "door_lock",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "door_unlock",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "erase_user_data",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "flash_lights",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "guest_mode",
                timestamp = ts.ToString("o"),
                parameters = new { enabled = true },
                response = new { result = true, reason = "" }
            },
            new {
                command = "honk_horn",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "media_next_fav",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "media_next_track",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "media_prev_fav",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "media_prev_track",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "media_toggle_playback",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "media_volume_down",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "navigation_gps_request",
                timestamp = ts.ToString("o"),
                parameters = new { latitude = 41.9028, longitude = 12.4964 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "navigation_request",
                timestamp = ts.ToString("o"),
                parameters = new { location = "Piazza Venezia, Roma" },
                response = new { result = true, queued = false }
            },
            new {
                command = "navigation_sc_request",
                timestamp = ts.ToString("o"),
                parameters = new { supercharger_id = "SC_IT_001" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "navigation_waypoints_request",
                timestamp = ts.ToString("o"),
                parameters = new { waypoints = new[] { "Napoli", "Roma", "Firenze" } },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remote_auto_seat_climate_request",
                timestamp = ts.ToString("o"),
                parameters = new { enabled = true },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remote_auto_steering_wheel_heat_climate_request",
                timestamp = ts.ToString("o"),
                parameters = new { enabled = true },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remote_boombox",
                timestamp = ts.ToString("o"),
                parameters = new { sound_id = 2000 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remote_seat_cooler_request",
                timestamp = ts.ToString("o"),
                parameters = new { level = 2 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remote_seat_heater_request",
                timestamp = ts.ToString("o"),
                parameters = new { level = 3 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remote_start_drive",
                timestamp = ts.ToString("o"),
                parameters = new { password = "mockpassword" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remote_steering_wheel_heat_level_request",
                timestamp = ts.ToString("o"),
                parameters = new { level = 2 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remote_steering_wheel_heater_request",
                timestamp = ts.ToString("o"),
                parameters = new { on = true },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remove_charge_schedule",
                timestamp = ts.ToString("o"),
                parameters = new { schedule_id = 101 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remove_precondition_schedule",
                timestamp = ts.ToString("o"),
                parameters = new { schedule_id = 202 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "reset_pin_to_drive_pin",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "reset_valet_pin",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "schedule_software_update",
                timestamp = ts.ToString("o"),
                parameters = new { time = ts.AddMinutes(30).ToString("o") },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_bioweapon_mode",
                timestamp = ts.ToString("o"),
                parameters = new { on = true },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_cabin_overheat_protection",
                timestamp = ts.ToString("o"),
                parameters = new { state = "enabled" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_charge_limit",
                timestamp = ts.ToString("o"),
                parameters = new { percent = 80 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_charging_amps",
                timestamp = ts.ToString("o"),
                parameters = new { charging_amps = 16 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_climate_keeper_mode",
                timestamp = ts.ToString("o"),
                parameters = new { climate_keeper_mode = 2 }, // 0=Off, 1=Keep, 2=Dog, 3=Camp
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_cop_temp",
                timestamp = ts.ToString("o"),
                parameters = new { cop_temp = 1 }, // 0=Low, 1=Medium, 2=High
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_pin_to_drive",
                timestamp = ts.ToString("o"),
                parameters = new { pin = "1234" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_preconditioning_max",
                timestamp = ts.ToString("o"),
                parameters = new { on = true },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_scheduled_charging",
                timestamp = ts.ToString("o"),
                parameters = new { time = 120 }, // 2:00 AM
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_scheduled_departure",
                timestamp = ts.ToString("o"),
                parameters = new { departure_time = 420, end_off_peak_time = 480 }, // 7:00 - 8:00
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_sentry_mode",
                timestamp = ts.ToString("o"),
                parameters = new { on = true },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_temps",
                timestamp = ts.ToString("o"),
                parameters = new { driver_temp = 21.0, passenger_temp = 21.5 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_valet_mode",
                timestamp = ts.ToString("o"),
                parameters = new { on = true, password = "5678" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_vehicle_name",
                timestamp = ts.ToString("o"),
                parameters = new { vehicle_name = "PolarDrive-X" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "speed_limit_activate",
                timestamp = ts.ToString("o"),
                parameters = new { pin = "4321" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "speed_limit_clear_pin",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "speed_limit_clear_pin_admin",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "speed_limit_deactivate",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "speed_limit_set_limit",
                timestamp = ts.ToString("o"),
                parameters = new { limit_mph = 110 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "sun_roof_control",
                timestamp = ts.ToString("o"),
                parameters = new { state = "vent" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "trigger_homelink",
                timestamp = ts.ToString("o"),
                parameters = new { lat = 41.8919, lon = 12.5113 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "upcoming_calendar_entries",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { reason = "", result = true }
            },
            new {
                command = "window_control",
                timestamp = ts.ToString("o"),
                parameters = new { command = "close", lat = 41.9028, lon = 12.4964 },
                response = new { result = true, reason = "" }
            }
        };
    }
    //
}
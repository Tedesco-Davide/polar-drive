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
                    new { type = "vehicle_commands", content = GenerateMockVehicleCommands(ts) },
                    new { type = "vehicle_endpoints", content = GenerateVehicleEndpoints(ts) }
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

    private static object GenerateVehicleEndpoints(DateTime ts)
    {
        return new
        {
            drivers = new
            {
                response = new[]
                {
                new
                {
                    my_tesla_unique_id = 8888888,
                    user_id = 800001,
                    user_id_s = "800001",
                    vault_uuid = Guid.NewGuid().ToString(),
                    driver_first_name = "Testy",
                    driver_last_name = "McTesterson",
                    granular_access = new { hide_private = false },
                    active_pubkeys = Array.Empty<string>(),
                    public_key = ""
                }
            },
                count = 1
            },
            drivers_remove = new
            {
                response = "ok"
            },
            eligible_subscriptions = new
            {
                response = new
                {
                    country = "IT",
                    vin = "5YJ3000000NEXUS01",
                    eligible = new[]
                    {
                    new
                    {
                        optionCode = "AP4",
                        product = "Enhanced Autopilot",
                        startDate = ts.ToString("o"),
                        addons = new[]
                        {
                            new {
                                billingPeriod = "monthly",
                                currencyCode = "EUR",
                                optionCode = "NAVUPG",
                                price = 10,
                                tax = 2.2,
                                total = 12.2
                            }
                        },
                        billingOptions = new[]
                        {
                            new {
                                billingPeriod = "monthly",
                                currencyCode = "EUR",
                                optionCode = "AP4",
                                price = 100,
                                tax = 22,
                                total = 122
                            }
                        }
                    }
                }
                }
            },
            eligible_upgrades = new
            {
                response = new
                {
                    vin = "5YJ3000000NEXUS01",
                    country = "IT",
                    type = "VEHICLE",
                    eligible = new[]
                    {
                    new
                    {
                        optionCode = "$FM3U",
                        optionGroup = "PERF_FIRMWARE",
                        currentOptionCode = "$FM3B",
                        pricing = new[]
                        {
                            new {
                                price = 2000,
                                total = 2000,
                                currencyCode = "EUR",
                                isPrimary = true
                            }
                        }
                    }
                }
                }
            },
            fleet_status = new
            {
                response = new
                {
                    key_paired_vins = Array.Empty<string>(),
                    unpaired_vins = new[] { "5YJ3000000NEXUS01" },
                    vehicle_info = new Dictionary<string, object>
                {
                    {
                        "5YJ3000000NEXUS01", new {
                            firmware_version = "2024.14.30",
                            vehicle_command_protocol_required = true,
                            discounted_device_data = false,
                            fleet_telemetry_version = "1.0.0",
                            total_number_of_keys = 5
                        }
                    }
                }
                }
            },
            fleet_telemetry_config_create = new
            {
                response = new
                {
                    updated_vehicles = 1,
                    skipped_vehicles = new
                    {
                        missing_key = Array.Empty<string>(),
                        unsupported_hardware = new[] { "5YJ3000000NEXUS02" },
                        unsupported_firmware = new[] { "5YJ3000000NEXUS02" },
                        max_configs = Array.Empty<string>()
                    }
                }
            },
            fleet_telemetry_config_delete = new
            {
                response = new
                {
                    updated_vehicles = 1
                }
            },
            fleet_telemetry_config_get = new
            {
                response = new
                {
                    synced = true,
                    config = new
                    {
                        hostname = "test-telemetry.com",
                        ca = "-----BEGIN CERTIFICATE-----\\ncert\\n-----END CERTIFICATE-----\\n",
                        port = 4443,
                        prefer_typed = true,
                        fields = new
                        {
                            DriveRail = new { interval_seconds = 1800 },
                            BmsFullchargecomplete = new
                            {
                                interval_seconds = 1800,
                                resend_interval_seconds = 3600
                            },
                            ChargerVoltage = new
                            {
                                interval_seconds = 1,
                                minimum_delta = 5
                            }
                        },
                        alert_types = new[] { "service" }
                    },
                    limit_reached = false,
                    key_paired = false
                }
            },
            fleet_telemetry_config_jws = new
            {
                response = new
                {
                    updated_vehicles = 1,
                    skipped_vehicles = new
                    {
                        missing_key = Array.Empty<string>(),
                        unsupported_hardware = new[] { "5YJ3000000NEXUS02" },
                        unsupported_firmware = new[] { "5YJ3000000NEXUS02" },
                        max_configs = Array.Empty<string>()
                    }
                }
            },
            fleet_telemetry_errors = new
            {
                response = new
                {
                    fleet_telemetry_errors = new[]
                    {
                        new { name = "partner-client-id", error = "msg", vin = "5YJ3000000NEXUS01" },
                        new { name = "partner-client-id", error = "msg2", vin = "5YJ3000000NEXUS01" }
                    }
                }
            },
            list = new
            {
                response = new[]
                {
                    new
                    {
                        id = 100021,
                        vehicle_id = 99999,
                        vin = "TEST00000000VIN01",
                        color = (string?)null,
                        access_type = "OWNER",
                        display_name = "Owned",
                        option_codes = "TEST0,COUS",
                        granular_access = new { hide_private = false },
                        tokens = new[] { "4f993c5b9e2b937b", "7a3153b1bbb48a96" },
                        state = "online",
                        in_service = false,
                        id_s = "100021",
                        calendar_enabled = true,
                        api_version = (int?)null,
                        backseat_token = (string?)null,
                        backseat_token_updated_at = (string?)null
                    }
                },
                pagination = new
                {
                    previous = (string?)null,
                    next = (string?)null,
                    current = 1,
                    per_page = 2,
                    count = 2,
                    pages = 1
                },
                count = 1
            },
            mobile_enabled = new
            {
                response = new
                {
                    reason = "",
                    result = true
                }
            },
            nearby_charging_sites = new
            {
                response = new
                {
                    congestion_sync_time_utc_secs = 1693588513,
                    destination_charging = new[]
                    {
                        new {
                            location = new { lat = 37.409314, @long = -122.123068 },
                            name = "Hilton Garden Inn Palo Alto",
                            type = "destination",
                            distance_miles = 1.35024,
                            amenities = "restrooms,wifi,lodging"
                        },
                        new {
                            location = new { lat = 37.407771, @long = -122.120076 },
                            name = "Dinah's Garden Hotel & Poolside Restaurant",
                            type = "destination",
                            distance_miles = 1.534213,
                            amenities = "restrooms,restaurant,wifi,cafe,lodging"
                        }
                    },
                    superchargers = new[]
                    {
                        new {
                            location = new { lat = 37.399071, @long = -122.111216 },
                            name = "Los Altos, CA",
                            type = "supercharger",
                            distance_miles = 2.202902,
                            available_stalls = 12,
                            total_stalls = 16,
                            site_closed = false,
                            amenities = "restrooms,restaurant,wifi,cafe,shopping",
                            billing_info = ""
                        },
                        new {
                            location = new { lat = 37.441734, @long = -122.170202 },
                            name = "Palo Alto, CA - Stanford Shopping Center",
                            type = "supercharger",
                            distance_miles = 2.339135,
                            available_stalls = 11,
                            total_stalls = 20,
                            site_closed = false,
                            amenities = "restrooms,restaurant,wifi,cafe,shopping",
                            billing_info = ""
                        }
                    },
                    timestamp = 1693588576552
                }
            },
            options = new
            {
                response = new
                {
                    codes = new object[]
                    {
                        new { code = "$MT315", displayName = "Long Range All-Wheel Drive", isActive = true },
                        new { code = "$PPSW", colorCode = "PPSW", displayName = "Pearl White Multi-Coat", isActive = true },
                        new { code = "$W40B", displayName = "18’’ Aero Wheels", isActive = true },
                        new { code = "$IPB0", displayName = "All Black Premium Interior", isActive = true },
                        new { code = "$APBS", displayName = "Basic Autopilot", isActive = true },
                        new { code = "$APF2", displayName = "Full Self-Driving Capability", isActive = true },
                        new { code = "$SC04", displayName = "Supercharger Network Access + Pay-as-you-go", isActive = true }
                    }
                }
            },
            recent_alerts = new
            {
                response = new
                {
                    recent_alerts = new[]
                    {
                        new
                        {
                            name = "Name_Of_The_Alert",
                            time = ts.ToString("o"),
                            audience = new[] { "service-fix", "customer" },
                            user_text = "additional description text"
                        }
                    }
                }
            },
            release_notes = new
            {
                response = new
                {
                    response = new
                    {
                        release_notes = new[]
                        {
                            new
                            {
                                title = "Minor Fixes",
                                subtitle = "Some more info",
                                description = "This release contains minor fixes and improvements",
                                customer_version = "2022.42.0",
                                icon = "release_notes_icon",
                                image_url = "https://vehicle-files.teslamotors.com/release_notes/d0fa3e08a458696e6464a46c938ffc0a",
                                light_image_url = "https://vehicle-files.teslamotors.com/release_notes/9a122cff8916fffcb61cfd65a15c276f"
                            }
                        }
                    }
                }
            },
            service_data = new
            {
                response = new
                {
                    service_status = "in_service",
                    service_etc = "2023-05-02T17:10:53-10:00",
                    service_visit_number = "SV12345678",
                    status_id = 8
                }
            },
            share_invites = new
            {
                response = new[]
                {
                    new
                    {
                        id = 429509621657,
                        owner_id = 429511308124,
                        share_user_id = (long?)null,
                        product_id = "TEST00000000VIN01",
                        state = "pending",
                        code = "aqwl4JHU2q4aTeNROz8W9SpngoFvj-ReuDFIJs6-YOhA",
                        expires_at = "2023-06-29T00:42:00.000Z",
                        revoked_at = (string?)null,
                        borrowing_device_id = (string?)null,
                        key_id = (string?)null,
                        product_type = "vehicle",
                        share_type = "customer",
                        share_user_sso_id = (string?)null,
                        active_pubkeys = new object?[] { null },
                        id_s = "429509621657",
                        owner_id_s = "429511308124",
                        share_user_id_s = "",
                        borrowing_key_hash = (string?)null,
                        vin = "TEST00000000VIN01",
                        share_link = "https://www.tesla.com/_rs/1/aqwl4JHU2q4aTeNROz8W9SpngoFvj-ReuDFIJs6-YOhA"
                    }
                },
                pagination = new
                {
                    previous = (string?)null,
                    next = (string?)null,
                    current = 1,
                    per_page = 25,
                    count = 1,
                    pages = 1
                },
                count = 1
            },
            share_invites_create = new
            {
                response = new
                {
                    id = 429509621657,
                    owner_id = 429511308124,
                    share_user_id = (long?)null,
                    product_id = "TEST00000000VIN01",
                    state = "pending",
                    code = "aqwl4JHU2q4aTeNROz8W9SpngoFvj-ReuDFIJs6-YOhA",
                    expires_at = "2023-06-29T00:42:00.000Z",
                    revoked_at = (string?)null,
                    borrowing_device_id = (string?)null,
                    key_id = (string?)null,
                    product_type = "vehicle",
                    share_type = "customer",
                    share_user_sso_id = (string?)null,
                    active_pubkeys = new object?[] { null },
                    id_s = "429509621657",
                    owner_id_s = "429511308124",
                    share_user_id_s = "",
                    borrowing_key_hash = (string?)null,
                    vin = "TEST00000000VIN01",
                    share_link = "https://www.tesla.com/_rs/1/aqwl4JHU2q4aTeNROz8W9SpngoFvj-ReuDFIJs6-YOhA"
                }
            },
            share_invites_redeem = new
            {
                response = new
                {
                    vehicle_id_s = "88850",
                    vin = "5YJY000000NEXUS01"
                }
            },
            share_invites_revoke = new
            {
                response = true
            },
            signed_command = new
            {
                response = "YmFzZTY0X3Jlc3BvbnNl" // "base64_response" base64 encoded
            },
            subscriptions = new
            {
                vehicle = new
                {
                    ids = new[] { 100021 },
                    count = 1
                },
                energy_site = new
                {
                    ids = new[] { 429500927973 },
                    count = 1
                }
            },
            subscriptions_set = new
            {
                vehicle = new
                {
                    ids = new[] { 100021 },
                    count = 1
                },
                energy_site = new
                {
                    ids = new[] { 429500927973 },
                    count = 1
                }
            },
            vehicle = new
            {
                response = new
                {
                    id = 100021,
                    vehicle_id = 99999,
                    vin = "TEST00000000VIN01",
                    color = (string?)null,
                    access_type = "OWNER",
                    display_name = "Owned",
                    option_codes = "TEST0,COUS",
                    granular_access = new { hide_private = false },
                    tokens = new[] { "4f993c5b9e2b937b", "7a3153b1bbb48a96" },
                    state = "online",
                    in_service = false,
                    id_s = "100021",
                    calendar_enabled = true,
                    api_version = (int?)null,
                    backseat_token = (string?)null,
                    backseat_token_updated_at = (string?)null
                }
            },
            vehicle_data = new
            {
                response = new
                {
                    id = 100021,
                    user_id = 800001,
                    vehicle_id = 99999,
                    vin = "TEST00000000VIN01",
                    color = (string?)null,
                    access_type = "OWNER",
                    granular_access = new { hide_private = false },
                    tokens = new[] { "4f993c5b9e2b937b", "7a3153b1bbb48a96" },
                    state = "online",
                    in_service = false,
                    id_s = "100021",
                    calendar_enabled = true,
                    api_version = 54,
                    backseat_token = (string?)null,
                    backseat_token_updated_at = (string?)null,
                    charge_state = new
                    {
                        battery_heater_on = false,
                        battery_level = 42,
                        battery_range = 133.99,
                        charge_amps = 48,
                        charge_current_request = 48,
                        charge_current_request_max = 48,
                        charge_enable_request = true,
                        charge_energy_added = 48.45,
                        charge_limit_soc = 90,
                        charge_limit_soc_max = 100,
                        charge_limit_soc_min = 50,
                        charge_limit_soc_std = 90,
                        charge_miles_added_ideal = 202,
                        charge_miles_added_rated = 202,
                        charge_port_cold_weather_mode = false,
                        charge_port_color = "<invalid>",
                        charge_port_door_open = false,
                        charge_port_latch = "Engaged",
                        charge_rate = 0,
                        charger_actual_current = 0,
                        charger_phases = (int?)null,
                        charger_pilot_current = 48,
                        charger_power = 0,
                        charger_voltage = 2,
                        charging_state = "Disconnected",
                        conn_charge_cable = "<invalid>",
                        est_battery_range = 143.88,
                        fast_charger_brand = "<invalid>",
                        fast_charger_present = false,
                        fast_charger_type = "<invalid>",
                        ideal_battery_range = 133.99,
                        managed_charging_active = false,
                        managed_charging_start_time = (long?)null,
                        managed_charging_user_canceled = false,
                        max_range_charge_counter = 0,
                        minutes_to_full_charge = 0,
                        not_enough_power_to_heat = (bool?)null,
                        off_peak_charging_enabled = false,
                        off_peak_charging_times = "all_week",
                        off_peak_hours_end_time = 360,
                        preconditioning_enabled = false,
                        preconditioning_times = "all_week",
                        scheduled_charging_mode = "Off",
                        scheduled_charging_pending = false,
                        scheduled_charging_start_time = (long?)null,
                        scheduled_departure_time = 1634914800,
                        scheduled_departure_time_minutes = 480,
                        supercharger_session_trip_planner = false,
                        time_to_full_charge = 0,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        trip_charging = false,
                        usable_battery_level = 42,
                        user_charge_enable_request = (bool?)null
                    },
                    climate_state = new
                    {
                        allow_cabin_overheat_protection = true,
                        auto_seat_climate_left = false,
                        auto_seat_climate_right = false,
                        auto_steering_wheel_heat = false,
                        battery_heater = false,
                        battery_heater_no_power = (bool?)null,
                        bioweapon_mode = false,
                        cabin_overheat_protection = "On",
                        cabin_overheat_protection_actively_cooling = true,
                        climate_keeper_mode = "off",
                        cop_activation_temperature = "High",
                        defrost_mode = 0,
                        driver_temp_setting = 21,
                        fan_status = 0,
                        hvac_auto_request = "On",
                        inside_temp = 38.4,
                        is_auto_conditioning_on = true,
                        is_climate_on = false,
                        is_front_defroster_on = false,
                        is_preconditioning = false,
                        is_rear_defroster_on = false,
                        left_temp_direction = -293,
                        max_avail_temp = 28,
                        min_avail_temp = 15,
                        outside_temp = 36.5,
                        passenger_temp_setting = 21,
                        remote_heater_control_enabled = false,
                        right_temp_direction = -276,
                        seat_heater_left = 0,
                        seat_heater_rear_center = 0,
                        seat_heater_rear_left = 0,
                        seat_heater_rear_right = 0,
                        seat_heater_right = 0,
                        side_mirror_heaters = false,
                        steering_wheel_heat_level = 0,
                        steering_wheel_heater = false,
                        supports_fan_only_cabin_overheat_protection = true,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        wiper_blade_heater = false
                    },
                    drive_state = new
                    {
                        active_route_latitude = 37.7765494,
                        active_route_longitude = -122.4195418,
                        active_route_traffic_minutes_delay = 0,
                        gps_as_of = 1692137422,
                        heading = 289,
                        latitude = 37.7765494,
                        longitude = -122.4195418,
                        native_latitude = 37.7765494,
                        native_location_supported = 1,
                        native_longitude = -122.4195418,
                        native_type = "wgs",
                        power = 1,
                        shift_state = (string?)null,
                        speed = (int?)null,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    },
                    gui_settings = new
                    {
                        gui_24_hour_time = false,
                        gui_charge_rate_units = "mi/hr",
                        gui_distance_units = "mi/hr",
                        gui_range_display = "Rated",
                        gui_temperature_units = "F",
                        gui_tirepressure_units = "Psi",
                        show_range_units = false,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    },
                    vehicle_config = new
                    {
                        aux_park_lamps = "NaPremium",
                        badge_version = 0,
                        can_accept_navigation_requests = true,
                        can_actuate_trunks = true,
                        car_special_type = "base",
                        car_type = "modely",
                        charge_port_type = "US",
                        cop_user_set_temp_supported = true,
                        dashcam_clip_save_supported = true,
                        default_charge_to_max = false,
                        driver_assist = "TeslaAP3",
                        ece_restrictions = false,
                        efficiency_package = "MY2021",
                        eu_vehicle = false,
                        exterior_color = "MidnightSilver",
                        exterior_trim = "Black",
                        exterior_trim_override = "",
                        has_air_suspension = false,
                        has_ludicrous_mode = false,
                        has_seat_cooling = false,
                        headlamp_type = "Premium",
                        interior_trim_type = "Black2",
                        key_version = 2,
                        motorized_charge_port = true,
                        paint_color_override = "19,20,22,0.8,0.04",
                        performance_package = "Base",
                        plg = true,
                        pws = true,
                        rear_drive_unit = "PM216MOSFET",
                        rear_seat_heaters = 1,
                        rear_seat_type = 0,
                        rhd = false,
                        roof_color = "RoofColorGlass",
                        seat_type = (int?)null,
                        spoiler_type = "None",
                        sun_roof_installed = (bool?)null,
                        supports_qr_pairing = false,
                        third_row_seats = "None",
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        trim_badging = "74d",
                        use_range_badging = true,
                        utc_offset = -25200,
                        webcam_selfie_supported = true,
                        webcam_supported = true,
                        wheel_type = "Apollo19"
                    },
                    vehicle_state = new
                    {
                        api_version = 54,
                        autopark_state_v3 = "ready",
                        autopark_style = "dead_man",
                        calendar_supported = true,
                        car_version = "2023.7.20 7910d26d5c64",
                        center_display_state = 0,
                        dashcam_clip_save_available = false,
                        dashcam_state = "Unavailable",
                        df = 0,
                        dr = 0,
                        fd_window = 0,
                        feature_bitmask = "15dffbff,0",
                        fp_window = 0,
                        ft = 0,
                        homelink_device_count = 3,
                        homelink_nearby = false,
                        is_user_present = false,
                        last_autopark_error = "no_error",
                        locked = true,
                        media_info = new
                        {
                            a2dp_source_name = "Pixel 6",
                            audio_volume = 2.6667,
                            audio_volume_increment = 0.333333,
                            audio_volume_max = 10.333333,
                            media_playback_status = "Playing",
                            now_playing_album = "KQED",
                            now_playing_artist = "PBS Newshour on KQED FM",
                            now_playing_duration = 0,
                            now_playing_elapsed = 0,
                            now_playing_source = "13",
                            now_playing_station = "88.5 FM KQED",
                            now_playing_title = "PBS Newshour"
                        },
                        media_state = new { remote_control_enabled = true },
                        notifications_supported = true,
                        odometer = 15720.074889,
                        parsed_calendar_supported = true,
                        pf = 0,
                        pr = 0,
                        rd_window = 0,
                        remote_start = false,
                        remote_start_enabled = true,
                        remote_start_supported = true,
                        rp_window = 0,
                        rt = 0,
                        santa_mode = 0,
                        sentry_mode = false,
                        sentry_mode_available = true,
                        service_mode = false,
                        service_mode_plus = false,
                        smart_summon_available = true,
                        software_update = new
                        {
                            download_perc = 0,
                            expected_duration_sec = 2700,
                            install_perc = 1,
                            status = "",
                            version = " "
                        },
                        speed_limit_mode = new
                        {
                            active = false,
                            current_limit_mph = 85,
                            max_limit_mph = 120,
                            min_limit_mph = 50,
                            pin_code_set = false
                        },
                        summon_standby_mode_enabled = false,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        tpms_hard_warning_fl = false,
                        tpms_hard_warning_fr = false,
                        tpms_hard_warning_rl = false,
                        tpms_hard_warning_rr = false,
                        tpms_last_seen_pressure_time_fl = 1692136878,
                        tpms_last_seen_pressure_time_fr = 1692136878,
                        tpms_last_seen_pressure_time_rl = 1692136878,
                        tpms_last_seen_pressure_time_rr = 1692136878,
                        tpms_pressure_fl = 3.1,
                        tpms_pressure_fr = 3.1,
                        tpms_pressure_rl = 3.15,
                        tpms_pressure_rr = 3.0,
                        tpms_rcp_front_value = 2.9,
                        tpms_rcp_rear_value = 2.9,
                        tpms_soft_warning_fl = false,
                        tpms_soft_warning_fr = false,
                        tpms_soft_warning_rl = false,
                        tpms_soft_warning_rr = false,
                        valet_mode = false,
                        valet_pin_needed = true,
                        vehicle_name = "grADOFIN",
                        vehicle_self_test_progress = 0,
                        vehicle_self_test_requested = false,
                        webcam_available = true
                    }
                }
            },
            vehicle_subscriptions = new
            {
                response = new[] { 100021 },
                count = 1
            },
            vehicle_subscriptions_set = new
            {
                response = Array.Empty<object>(),
                count = 0
            },
            wake_up = new
            {
                response = new
                {
                    id = 100021,
                    user_id = 800001,
                    vehicle_id = 99999,
                    vin = "TEST00000000VIN01",
                    color = (string?)null,
                    access_type = "OWNER",
                    granular_access = new { hide_private = false },
                    tokens = new[] { "4f993c5b9e2b937b", "7a3153b1bbb48a96" },
                    state = "online",
                    in_service = false,
                    id_s = "100021",
                    calendar_enabled = true,
                    api_version = (int?)null,
                    backseat_token = (string?)null,
                    backseat_token_updated_at = (string?)null
                }
            },
            warranty_details = new
            {
                response = new
                {
                    activeWarranty = new[]
                    {
                        new {
                            warrantyType = "NEW_MFG_WARRANTY",
                            warrantyDisplayName = "Basic Vehicle Limited Warranty",
                            expirationDate = "2025-10-21T00:00:00Z",
                            expirationOdometer = 50000,
                            odometerUnit = "MI",
                            warrantyExpiredOn = (string?)null,
                            coverageAgeInYears = 4
                        },
                        new {
                            warrantyType = "BATTERY_WARRANTY",
                            warrantyDisplayName = "Battery Limited Warranty",
                            expirationDate = "2029-10-21T00:00:00Z",
                            expirationOdometer = 120000,
                            odometerUnit = "MI",
                            warrantyExpiredOn = (string?)null,
                            coverageAgeInYears = 8
                        },
                        new {
                            warrantyType = "DRIVEUNIT_WARRANTY",
                            warrantyDisplayName = "Drive Unit Limited Warranty",
                            expirationDate = "2029-10-21T00:00:00Z",
                            expirationOdometer = 120000,
                            odometerUnit = "MI",
                            warrantyExpiredOn = (string?)null,
                            coverageAgeInYears = 8
                        }
                    },
                    upcomingWarranty = Array.Empty<object>(),
                    expiredWarranty = Array.Empty<object>()
                }
            }
        };
    }
}
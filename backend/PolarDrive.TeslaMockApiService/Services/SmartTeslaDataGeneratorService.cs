using System.Text.Json;

namespace PolarDrive.TeslaMockApiService.Services;

// ✅ Stato persistente completo per ogni veicolo simulato
public class VehicleSimulationState
{
    public required string Vin { get; set; }

    // Basic Vehicle Info
    public int VehicleId { get; set; } = 99999;
    public string DisplayName { get; set; } = "Model 3 Mock";
    public string Color { get; set; } = "Midnight Silver Metallic";

    // Battery & Charging
    public int BatteryLevel { get; set; } = 75;
    public bool IsCharging { get; set; } = false;
    public string ChargingState { get; set; } = "Disconnected";
    public int ChargeRate { get; set; } = 0;
    public decimal ChargeEnergyAdded { get; set; } = 0;

    // Location & Movement
    public decimal Latitude { get; set; } = 41.9028m;
    public decimal Longitude { get; set; } = 12.4964m;
    public bool IsMoving { get; set; } = false;
    public int Heading { get; set; } = 0;
    public int? Speed { get; set; } = null;
    public decimal Odometer { get; set; } = 15000;

    // Temperature & Climate
    public decimal InsideTemp { get; set; } = 22.0m;
    public decimal OutsideTemp { get; set; } = 18.0m;
    public bool IsClimateOn { get; set; } = false;
    public int DriverTempSetting { get; set; } = 21;
    public int PassengerTempSetting { get; set; } = 21;

    // Vehicle States
    public bool IsLocked { get; set; } = true;
    public bool SentryMode { get; set; } = false;
    public bool RemoteStart { get; set; } = false;
    public string CarVersion { get; set; } = "2024.20.9 abc123def";

    // Trip Management
    public bool IsOnTrip { get; set; } = false;
    public decimal TripStartLat { get; set; }
    public decimal TripStartLng { get; set; }
    public decimal TripTargetLat { get; set; }
    public decimal TripTargetLng { get; set; }
    public decimal TripProgress { get; set; } = 0;

    // Media Info
    public string NowPlayingTitle { get; set; } = "PBS Newshour";
    public string NowPlayingArtist { get; set; } = "PBS Newshour on KQED FM";
    public decimal AudioVolume { get; set; } = 2.6667m;

    // Tire Pressure
    public decimal TpmsPressureFl { get; set; } = 3.1m;
    public decimal TpmsPressureFr { get; set; } = 3.1m;
    public decimal TpmsPressureRl { get; set; } = 3.15m;
    public decimal TpmsPressureRr { get; set; } = 3.0m;

    // Energy Data (for energy endpoints)
    public int SolarPower { get; set; } = 3500;
    public decimal EnergyLeft { get; set; } = 18020.89m;
    public int TotalPackEnergy { get; set; } = 39343;

    // Timestamps
    public DateTime LastUpdate { get; set; } = DateTime.Now;
}

public static partial class SmartTeslaDataGeneratorService
{
    private static readonly Random _random = new();

    private static object GenerateEnergyEndpoints(VehicleSimulationState state, DateTime ts)
    {
        return new
        {
            backup = new
            {
                response = new { code = 201, message = "Updated" }
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
            time_of_use = new
            {
                response = new
                {
                    periods = new[]
                    {
                        new {
                            fromDayOfWeek = 0,
                            toDayOfWeek = 6,
                            fromHour = 22,
                            fromMinute = 0,
                            toHour = 6,
                            toMinute = 0
                        }
                    },
                    seasons = new[]
                    {
                        new {
                            fromDay = 1,
                            fromMonth = 1,
                            toDay = 31,
                            toMonth = 12,
                            tou_periods = new[] { 0 }
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
                    code = 200,
                    message = "Success",
                    import_kwh = _random.Next(100, 500),
                    export_kwh = _random.Next(200, 800),
                    net_import_kwh = _random.Next(-300, 200)
                }
            },
            live_status = new
            {
                response = new
                {
                    solar_power = state.SolarPower + _random.Next(-500, 500),
                    energy_left = state.EnergyLeft,
                    total_pack_energy = state.TotalPackEnergy,
                    percentage_charged = state.BatteryLevel,
                    backup_capable = true,
                    battery_power = state.IsCharging ? _random.Next(1000, 5000) : _random.Next(-3000, -1000),
                    load_power = _random.Next(2000, 3000),
                    grid_status = "Active",
                    grid_power = _random.Next(2000, 3000),
                    island_status = "on_grid",
                    storm_mode_active = false,
                    timestamp = ts.ToString("o")
                }
            },
            off_grid_vehicle_charging_reserve = new
            {
                response = new { code = 201, message = "Updated" }
            },
            operation = new
            {
                response = new { code = 201, message = "Updated" }
            },
            products = new
            {
                response = new object[]
                {
                        new
                        {
                            id = 100021,
                            user_id = 429511308124,
                            vehicle_id = state.VehicleId,
                            vin = state.Vin,
                            color = state.Color,
                            access_type = "OWNER",
                            display_name = state.DisplayName,
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
                        }
                },
                count = 1
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
                    user_settings = new { storm_mode_enabled = true },
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
                response = new { code = 201, message = "Updated" }
            }
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
                command = "actuate_trunk",
                timestamp = ts.ToString("o"),
                parameters = new { which_trunk = "front" },
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
                command = "flash_lights",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
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
                command = "media_volume_up",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "remote_start_drive",
                timestamp = ts.ToString("o"),
                parameters = new { password = "mock_password" },
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
                parameters = new { charging_amps = 32 },
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
                command = "sun_roof_control",
                timestamp = ts.ToString("o"),
                parameters = new { state = "close" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "window_control",
                timestamp = ts.ToString("o"),
                parameters = new { command = "close", lat = 37.4056, lon = -122.1086 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_preconditioning_max",
                timestamp = ts.ToString("o"),
                parameters = new { on = true },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_cabin_overheat_protection",
                timestamp = ts.ToString("o"),
                parameters = new { on = true, fan_only = false },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remote_seat_heater_request",
                timestamp = ts.ToString("o"),
                parameters = new { heater = 0, level = 3 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remote_steering_wheel_heater_request",
                timestamp = ts.ToString("o"),
                parameters = new { on = true },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_bioweapon_mode",
                timestamp = ts.ToString("o"),
                parameters = new { on = true, manual_override = false },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_climate_keeper_mode",
                timestamp = ts.ToString("o"),
                parameters = new { climate_keeper_mode = 1 },
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
                command = "add_charge_schedule",
                timestamp = ts.ToString("o"),
                parameters = new {
                    latitude = 41.9028,
                    longitude = 12.4964,
                    time = 420,
                    days_of_week = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" },
                    enabled = true
                },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remove_charge_schedule",
                timestamp = ts.ToString("o"),
                parameters = new {
                    id = 12345,
                    latitude = 41.9028,
                    longitude = 12.4964
                },
                response = new { result = true, reason = "" }
            },
            new {
                command = "add_precondition_schedule",
                timestamp = ts.ToString("o"),
                parameters = new {
                    latitude = 41.9028,
                    longitude = 12.4964,
                    departure_time = 480,
                    days_of_week = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" },
                    enabled = true,
                    preconditioning_enabled = true,
                    preconditioning_weekdays_only = false,
                    off_peak_charging_enabled = true,
                    off_peak_charging_weekdays_only = false,
                    end_off_peak_time = 360
                },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remove_precondition_schedule",
                timestamp = ts.ToString("o"),
                parameters = new {
                    id = 67890,
                    latitude = 41.9028,
                    longitude = 12.4964
                },
                response = new { result = true, reason = "" }
            },
            new {
                command = "clear_pin_to_drive_admin",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "homelink_nearby",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "trigger_homelink",
                timestamp = ts.ToString("o"),
                parameters = new { lat = 37.4056, lon = -122.1086 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "speed_limit_activate",
                timestamp = ts.ToString("o"),
                parameters = new { pin = "1234" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "speed_limit_deactivate",
                timestamp = ts.ToString("o"),
                parameters = new { pin = "1234" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "speed_limit_set_limit",
                timestamp = ts.ToString("o"),
                parameters = new { limit_mph = 65 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "speed_limit_clear_pin",
                timestamp = ts.ToString("o"),
                parameters = new { pin = "1234" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "valet_mode",
                timestamp = ts.ToString("o"),
                parameters = new { on = true, password = "1234" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "reset_valet_pin",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "summon",
                timestamp = ts.ToString("o"),
                parameters = new { action = "start", lat = 37.4056, lon = -122.1086 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "navigation_request",
                timestamp = ts.ToString("o"),
                parameters = new {
                    type = "share_dest_content_raw",
                    value = "Piazza del Colosseo, Roma, Italia",
                    locale = "it-IT",
                    timestamp_ms = DateTimeOffset.Now.ToUnixTimeMilliseconds()
                },
                response = new { result = true, reason = "" }
            },
            new {
                command = "share",
                timestamp = ts.ToString("o"),
                parameters = new { type = "share_ext_content_raw", locale = "en-US", timestamp_ms = DateTimeOffset.Now.ToUnixTimeMilliseconds() },
                response = new { result = true, reason = "" }
            },
            new {
                command = "schedule_software_update",
                timestamp = ts.ToString("o"),
                parameters = new { offset_sec = 7200 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "cancel_software_update",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_vehicle_name",
                timestamp = ts.ToString("o"),
                parameters = new { vehicle_name = "My Tesla" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "take_drivenote",
                timestamp = ts.ToString("o"),
                parameters = new { note = "Test drive note" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "erase_user_data",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "poweroff",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "poweron",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "boombox",
                timestamp = ts.ToString("o"),
                parameters = new { sound = 1 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "remote_boombox",
                timestamp = ts.ToString("o"),
                parameters = new { sound = 1 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_cop_temp",
                timestamp = ts.ToString("o"),
                parameters = new { cop_temp = "Medium" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_pin_to_drive",
                timestamp = ts.ToString("o"),
                parameters = new { on = true, password = "mock_password" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "reset_pin_to_drive_pin",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "activate_device_token",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "deactivate_device_token",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
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
                command = "set_managed_charging_sites",
                timestamp = ts.ToString("o"),
                parameters = new { latitude = 37.4056, longitude = -122.1086, enabled = true },
                response = new { result = true, reason = "" }
            },
            new {
                command = "adjust_volume",
                timestamp = ts.ToString("o"),
                parameters = new { volume = 5.0 },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_guest_mode",
                timestamp = ts.ToString("o"),
                parameters = new { enable = true },
                response = new { result = true, reason = "" }
            },
            new {
                command = "dashcam_save_clip",
                timestamp = ts.ToString("o"),
                parameters = (object?)null,
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_dashcam_mode",
                timestamp = ts.ToString("o"),
                parameters = new { mode = "on" },
                response = new { result = true, reason = "" }
            },
            new {
                command = "set_tpms_pressure",
                timestamp = ts.ToString("o"),
                parameters = new { tire_position = "front_left", pressure = 3.1 },
                response = new { result = true, reason = "" }
            }
        };
    }

    private static object GenerateChargingHistory(VehicleSimulationState state, DateTime ts)
    {
        return new
        {
            sessionId = 100000 + _random.Next(1000, 9999),
            vin = state.Vin,
            siteLocationName = $"Tesla Supercharger - {(_random.Next(0, 2) == 0 ? "Milano" : "Roma")}",
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
                    rateBase = Math.Round(0.45m + (decimal)_random.NextDouble() * 0.10m, 2), // 0.45-0.55 EUR/kWh
                    rateTier1 = 0,
                    rateTier2 = 0,
                    rateTier3 = (decimal?)null,
                    rateTier4 = (decimal?)null,
                    usageBase = _random.Next(25, 50), // kWh caricati
                    usageTier1 = 0,
                    usageTier2 = _random.Next(10, 30),
                    usageTier3 = (decimal?)null,
                    usageTier4 = (decimal?)null,
                    totalBase = Math.Round((decimal)_random.NextDouble() * 20 + 15, 2), // 15-35 EUR
                    totalTier1 = 0,
                    totalTier2 = 0,
                    totalTier3 = 0,
                    totalTier4 = 0,
                    totalDue = Math.Round((decimal)_random.NextDouble() * 20 + 15, 2),
                    netDue = Math.Round((decimal)_random.NextDouble() * 20 + 15, 2),
                    uom = "kwh",
                    isPaid = _random.Next(0, 10) < 8, // 80% pagato
                    status = _random.Next(0, 10) < 8 ? "PAID" : "PENDING"
                },
                new {
                    sessionFeeId = 2,
                    feeType = "PARKING",
                    currencyCode = "EUR",
                    pricingType = _random.Next(0, 2) == 0 ? "NO_CHARGE" : "PAYMENT",
                    rateBase = _random.Next(0, 2) == 0 ? 0.0m : 0.05m, // Parking gratuito o 0.05 EUR/min
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
                    fileName = $"INV-{ts:yyyy}-{_random.Next(10000, 99999)}.pdf",
                    contentId = $"file-{Guid.NewGuid().ToString("N")[..10]}",
                    invoiceType = "IMMEDIATE"
                }
            }
        };
    }

    private static object GeneratePartnerEndpoints()
    {
        return new
        {
            fleet_telemetry_error_vins = new[]
            {
                "5YJ3000000NEXUS01",
                "5YJ3000000NEXUS02",
                "5YJ3000000NEXUS03",
                "5YJ3000000NEXUS04",
                "5YJ3000000NEXUS05",
                "5YJ3000000NEXUS06",
                "5YJ3000000NEXUS07",
                "5YJ3000000NEXUS08",
                "5YJ3000000NEXUS09",
                "5YJ3000000NEXUS10"
            },
            fleet_telemetry_errors = new[]
            {
                new {
                    name = "evotesla-client",
                    error = "Unable to parse GPS data",
                    vin = "5YJ3000000NEXUS01"
                },
                new {
                    name = "evotesla-client",
                    error = "Unable to parse GPS data",
                    vin = "5YJ3000000NEXUS02"
                },
                new {
                    name = "evotesla-client",
                    error = "Unable to parse GPS data",
                    vin = "5YJ3000000NEXUS03"
                },
                new {
                    name = "evotesla-client",
                    error = "Unable to parse GPS data",
                    vin = "5YJ3000000NEXUS04"
                },
                new {
                    name = "evotesla-client",
                    error = "Unable to parse GPS data",
                    vin = "5YJ3000000NEXUS05"
                },
                new {
                    name = "evotesla-client",
                    error = "Unable to parse GPS data",
                    vin = "5YJ3000000NEXUS06"
                },
                new {
                    name = "evotesla-client",
                    error = "Unable to parse GPS data",
                    vin = "5YJ3000000NEXUS07"
                },
                new {
                    name = "evotesla-client",
                    error = "Unable to parse GPS data",
                    vin = "5YJ3000000NEXUS08"
                },
                new {
                    name = "evotesla-client",
                    error = "Unable to parse GPS data",
                    vin = "5YJ3000000NEXUS09"
                },
                new {
                    name = "evotesla-client",
                    error = "Unable to parse GPS data",
                    vin = "5YJ3000000NEXUS10"
                }
            },
            public_key = "0437d832a7a695151f5a671780a276aa4cf2d6be3b2786465397612a342fcf418e98022d3cedf4e9a6f4b3b160472dee4ca022383d9b4cc4001a0f3023caec58fa"
        };
    }

    private static object GenerateVehicleEndpoints(VehicleSimulationState state, DateTime ts)
    {
        if (string.IsNullOrEmpty(state.Vin))
        {
            throw new ArgumentException($"VehicleSimulationState.Vin is null or empty for vehicle {state.VehicleId}");
        }

        var timestampMs = new DateTimeOffset(ts).ToUnixTimeMilliseconds();

        return new
        {
            // Tutti gli endpoint del VehicleEndpointsDto
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

            // ✅ IL VEHICLE_DATA COMPLETO
            vehicle_data = new
            {
                response = new
                {
                    id = 100021,
                    user_id = 800001,
                    vehicle_id = state.VehicleId,
                    vin = state.Vin,
                    color = state.Color,
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

                    // ✅ CHARGE STATE COMPLETO
                    charge_state = new
                    {
                        battery_heater_on = false,
                        battery_level = state.BatteryLevel,
                        battery_range = state.BatteryLevel * 4.2m,
                        charge_amps = state.IsCharging ? 48 : 0,
                        charge_current_request = 48,
                        charge_current_request_max = 48,
                        charge_enable_request = true,
                        charge_energy_added = state.ChargeEnergyAdded,
                        charge_limit_soc = 90,
                        charge_limit_soc_max = 100,
                        charge_limit_soc_min = 50,
                        charge_limit_soc_std = 90,
                        charge_miles_added_ideal = (int)(state.ChargeEnergyAdded * 4),
                        charge_miles_added_rated = (int)(state.ChargeEnergyAdded * 4),
                        charge_port_cold_weather_mode = false,
                        charge_port_color = state.IsCharging ? "Blue" : "<invalid>",
                        charge_port_door_open = state.IsCharging,
                        charge_port_latch = state.IsCharging ? "Engaged" : "Disengaged",
                        charge_rate = state.ChargeRate,
                        charger_actual_current = state.IsCharging ? state.ChargeRate : 0,
                        charger_phases = state.IsCharging ? 3 : (int?)null,
                        charger_pilot_current = 48,
                        charger_power = state.IsCharging ? state.ChargeRate * 230 / 1000 : 0,
                        charger_voltage = state.IsCharging ? 230 : 2,
                        charging_state = state.ChargingState,
                        conn_charge_cable = state.IsCharging ? "IEC" : "<invalid>",
                        est_battery_range = state.BatteryLevel * 4.5m,
                        fast_charger_brand = state.IsCharging ? "Tesla" : "<invalid>",
                        fast_charger_present = state.IsCharging,
                        fast_charger_type = state.IsCharging ? "Supercharger" : "<invalid>",
                        ideal_battery_range = state.BatteryLevel * 4.2m,
                        managed_charging_active = false,
                        managed_charging_start_time = (long?)null,
                        managed_charging_user_canceled = false,
                        max_range_charge_counter = 0,
                        minutes_to_full_charge = state.IsCharging ? (100 - state.BatteryLevel) * 60 / Math.Max(state.ChargeRate, 1) : 0,
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
                        time_to_full_charge = state.IsCharging ? (100 - state.BatteryLevel) / Math.Max(state.ChargeRate, 1) : 0,
                        timestamp = timestampMs,
                        trip_charging = false,
                        usable_battery_level = state.BatteryLevel,
                        user_charge_enable_request = (bool?)null
                    },

                    // ✅ CLIMATE STATE COMPLETO
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
                        cabin_overheat_protection_actively_cooling = Math.Abs(state.InsideTemp - state.OutsideTemp) > 10,
                        climate_keeper_mode = "off",
                        cop_activation_temperature = "High",
                        defrost_mode = 0,
                        driver_temp_setting = state.DriverTempSetting,
                        fan_status = state.IsClimateOn ? _random.Next(1, 7) : 0,
                        hvac_auto_request = "On",
                        inside_temp = state.InsideTemp,
                        is_auto_conditioning_on = state.IsClimateOn,
                        is_climate_on = state.IsClimateOn,
                        is_front_defroster_on = false,
                        is_preconditioning = false,
                        is_rear_defroster_on = false,
                        left_temp_direction = _random.Next(-300, 300),
                        max_avail_temp = 28,
                        min_avail_temp = 15,
                        outside_temp = state.OutsideTemp,
                        passenger_temp_setting = state.PassengerTempSetting,
                        remote_heater_control_enabled = false,
                        right_temp_direction = _random.Next(-300, 300),
                        seat_heater_left = 0,
                        seat_heater_rear_center = 0,
                        seat_heater_rear_left = 0,
                        seat_heater_rear_right = 0,
                        seat_heater_right = 0,
                        side_mirror_heaters = false,
                        steering_wheel_heat_level = 0,
                        steering_wheel_heater = false,
                        supports_fan_only_cabin_overheat_protection = true,
                        timestamp = timestampMs,
                        wiper_blade_heater = false
                    },

                    // ✅ DRIVE STATE COMPLETO
                    drive_state = new
                    {
                        active_route_latitude = state.IsOnTrip ? state.TripTargetLat : state.Latitude,
                        active_route_longitude = state.IsOnTrip ? state.TripTargetLng : state.Longitude,
                        active_route_traffic_minutes_delay = 0,
                        gps_as_of = timestampMs / 1000,
                        heading = state.Heading,
                        latitude = state.Latitude,
                        longitude = state.Longitude,
                        native_latitude = state.Latitude,
                        native_location_supported = 1,
                        native_longitude = state.Longitude,
                        native_type = "wgs",
                        power = state.IsMoving ? _random.Next(-50, 200) : 0,
                        shift_state = state.IsMoving ? "D" : (string?)null,
                        speed = state.Speed,
                        timestamp = timestampMs
                    },

                    // ✅ GUI SETTINGS COMPLETO
                    gui_settings = new
                    {
                        gui_24_hour_time = false,
                        gui_charge_rate_units = "mi/hr",
                        gui_distance_units = "mi/hr",
                        gui_range_display = "Rated",
                        gui_temperature_units = "F",
                        gui_tirepressure_units = "Psi",
                        show_range_units = false,
                        timestamp = timestampMs
                    },

                    // ✅ VEHICLE CONFIG COMPLETO
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
                        exterior_color = state.Color,
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
                        timestamp = timestampMs,
                        trim_badging = "74d",
                        use_range_badging = true,
                        utc_offset = -25200,
                        webcam_selfie_supported = true,
                        webcam_supported = true,
                        wheel_type = "Apollo19"
                    },

                    // ✅ VEHICLE STATE COMPLETO
                    vehicle_state = new
                    {
                        api_version = 54,
                        autopark_state_v3 = "ready",
                        autopark_style = "dead_man",
                        calendar_supported = true,
                        car_version = state.CarVersion,
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
                        is_user_present = state.IsMoving,
                        last_autopark_error = "no_error",
                        locked = state.IsLocked,
                        media_info = new
                        {
                            a2dp_source_name = "Pixel 6",
                            audio_volume = state.AudioVolume,
                            audio_volume_increment = 0.333333,
                            audio_volume_max = 10.333333,
                            media_playback_status = "Playing",
                            now_playing_album = "KQED",
                            now_playing_artist = state.NowPlayingArtist,
                            now_playing_duration = 0,
                            now_playing_elapsed = 0,
                            now_playing_source = "13",
                            now_playing_station = "88.5 FM KQED",
                            now_playing_title = state.NowPlayingTitle
                        },
                        media_state = new { remote_control_enabled = true },
                        notifications_supported = true,
                        odometer = state.Odometer,
                        parsed_calendar_supported = true,
                        pf = 0,
                        pr = 0,
                        rd_window = 0,
                        remote_start = state.RemoteStart,
                        remote_start_enabled = true,
                        remote_start_supported = true,
                        rp_window = 0,
                        rt = 0,
                        santa_mode = 0,
                        sentry_mode = state.SentryMode,
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
                        timestamp = timestampMs,
                        tpms_hard_warning_fl = false,
                        tpms_hard_warning_fr = false,
                        tpms_hard_warning_rl = false,
                        tpms_hard_warning_rr = false,
                        tpms_last_seen_pressure_time_fl = timestampMs / 1000,
                        tpms_last_seen_pressure_time_fr = timestampMs / 1000,
                        tpms_last_seen_pressure_time_rl = timestampMs / 1000,
                        tpms_last_seen_pressure_time_rr = timestampMs / 1000,
                        tpms_pressure_fl = state.TpmsPressureFl,
                        tpms_pressure_fr = state.TpmsPressureFr,
                        tpms_pressure_rl = state.TpmsPressureRl,
                        tpms_pressure_rr = state.TpmsPressureRr,
                        tpms_rcp_front_value = 2.9m,
                        tpms_rcp_rear_value = 2.9m,
                        tpms_soft_warning_fl = false,
                        tpms_soft_warning_fr = false,
                        tpms_soft_warning_rl = false,
                        tpms_soft_warning_rr = false,
                        valet_mode = false,
                        valet_pin_needed = true,
                        vehicle_name = state.DisplayName,
                        vehicle_self_test_progress = 0,
                        vehicle_self_test_requested = false,
                        webcam_available = true
                    }
                }
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
                    vin = state.Vin,
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
                    vin = state.Vin,
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
                    unpaired_vins = new[] { state.Vin },
                    vehicle_info = new Dictionary<string, object>
                        {
                            {
                                state.Vin, new {
                                    firmware_version = state.CarVersion,
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
                        unsupported_hardware = Array.Empty<string>(),
                        unsupported_firmware = Array.Empty<string>(),
                        max_configs = Array.Empty<string>()
                    }
                }
            },

            fleet_telemetry_config_delete = new
            {
                response = new { updated_vehicles = 1 }
            },

            fleet_telemetry_config_get = new
            {
                response = new
                {
                    synced = true,
                    config = new
                    {
                        hostname = "fleet-telemetry.datapolar.com",
                        ca = "-----BEGIN CERTIFICATE-----\nMIIE...MOCK_CA_CERTIFICATE_DATA...==\n-----END CERTIFICATE-----",
                        port = 4443,
                        prefer_typed = true,
                        delivery_policy = "latest",
                        fields = new
                        {
                            // Campi di guida e movimento
                            VehicleSpeed = new
                            {
                                interval_seconds = 1,
                                minimum_delta = 1.0
                            },
                            DriveRail = new
                            {
                                interval_seconds = 1800
                            },
                            Location = new
                            {
                                interval_seconds = 10,
                                minimum_delta = 0.001,
                                resend_interval_seconds = 300
                            },
                            GpsState = new
                            {
                                interval_seconds = 30
                            },
                            Heading = new
                            {
                                interval_seconds = 5,
                                minimum_delta = 5.0
                            },

                            // Campi batteria e ricarica
                            BatteryLevel = new
                            {
                                interval_seconds = 30,
                                minimum_delta = 0.5,
                                resend_interval_seconds = 1800
                            },
                            BmsFullchargecomplete = new
                            {
                                interval_seconds = 1800,
                                resend_interval_seconds = 3600
                            },
                            ChargerVoltage = new
                            {
                                interval_seconds = 1,
                                minimum_delta = 5.0
                            },
                            ChargerCurrent = new
                            {
                                interval_seconds = 2,
                                minimum_delta = 1.0
                            },
                            ChargingState = new
                            {
                                interval_seconds = 10
                            },

                            // Campi clima e temperatura
                            InsideTemp = new
                            {
                                interval_seconds = 60,
                                minimum_delta = 0.5
                            },
                            OutsideTemp = new
                            {
                                interval_seconds = 120,
                                minimum_delta = 1.0
                            },
                            CabinClimateState = new
                            {
                                interval_seconds = 30
                            },

                            // Campi pneumatici
                            TpmsPressureFl = new
                            {
                                interval_seconds = 300,
                                minimum_delta = 0.05
                            },
                            TpmsPressureFr = new
                            {
                                interval_seconds = 300,
                                minimum_delta = 0.05
                            },
                            TpmsPressureRl = new
                            {
                                interval_seconds = 300,
                                minimum_delta = 0.05
                            },
                            TpmsPressureRr = new
                            {
                                interval_seconds = 300,
                                minimum_delta = 0.05
                            },

                            // Campi veicolo
                            VehicleLocked = new
                            {
                                interval_seconds = 60
                            },
                            SentryMode = new
                            {
                                interval_seconds = 120
                            },
                            Odometer = new
                            {
                                interval_seconds = 600,
                                minimum_delta = 0.1
                            },

                            // Campi energia (per sistemi energetici)
                            SolarPower = new
                            {
                                interval_seconds = 30,
                                minimum_delta = 50
                            },
                            GridPower = new
                            {
                                interval_seconds = 15,
                                minimum_delta = 25
                            },
                            BatteryPower = new
                            {
                                interval_seconds = 5,
                                minimum_delta = 10
                            },
                            LoadPower = new
                            {
                                interval_seconds = 30,
                                minimum_delta = 20
                            }
                        },
                        alert_types = new[] {
                            "service",
                            "error",
                            "warning",
                            "connectivity",
                            "vehicle_state_change",
                            "charge_state",
                            "climate_state"
                        }
                    },
                    limit_reached = false,
                    key_paired = true
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
                        unsupported_hardware = Array.Empty<string>(),
                        unsupported_firmware = Array.Empty<string>(),
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
                            new { name = "partner-client-id", error = "msg", vin = state.Vin }
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
                            vehicle_id = state.VehicleId,
                            vin = state.Vin,
                            color = state.Color,
                            access_type = "OWNER",
                            display_name = state.DisplayName,
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
                    count = 1,
                    pages = 1
                },
                count = 1
            },

            mobile_enabled = new
            {
                response = new { reason = "", result = true }
            },

            nearby_charging_sites = new
            {
                response = new
                {
                    congestion_sync_time_utc_secs = timestampMs / 1000,
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
                                available_stalls = _random.Next(8, 16),
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
                                available_stalls = _random.Next(10, 20),
                                total_stalls = 20,
                                site_closed = false,
                                amenities = "restrooms,restaurant,wifi,cafe,shopping",
                                billing_info = ""
                            }
                        },
                    timestamp = timestampMs
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
                            new { code = "$W40B", displayName = "18'' Aero Wheels", isActive = true },
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
                                name = "Software_Update_Available",
                                time = ts.ToString("o"),
                                audience = new[] { "customer" },
                                user_text = "A new software update is available for your vehicle"
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
                                    title = "Enhanced Performance",
                                    subtitle = "Improved acceleration and efficiency",
                                    description = "This release includes performance improvements and enhanced energy efficiency",
                                    customer_version = "2024.20.9",
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
                    service_status = "not_in_service",
                    service_etc = (string?)null,
                    service_visit_number = (string?)null,
                    status_id = 0
                }
            },

            share_invites = new
            {
                response = new[]
                {
                        new
                        {
                            id = _random.Next(100000000, 999999999),
                            owner_id = 429511308124,
                            share_user_id = (long?)null,
                            product_id = state.Vin,
                            state = "pending",
                            code = "ABC" + _random.Next(1000, 9999),
                            expires_at = ts.AddDays(7).ToString("o"),
                            revoked_at = (string?)null,
                            borrowing_device_id = (string?)null,
                            key_id = (string?)null,
                            product_type = "vehicle",
                            share_type = "customer",
                            share_user_sso_id = (string?)null,
                            active_pubkeys = new object?[] { null },
                            id_s = _random.Next(100000000, 999999999).ToString(),
                            owner_id_s = "429511308124",
                            share_user_id_s = "",
                            borrowing_key_hash = (string?)null,
                            vin = state.Vin,
                            share_link = $"https://www.tesla.com/_rs/1/ABC{_random.Next(1000, 9999)}"
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
                    id = _random.Next(100000000, 999999999),
                    owner_id = 429511308124,
                    share_user_id = (long?)null,
                    product_id = state.Vin,
                    state = "pending",
                    code = "NEW" + _random.Next(1000, 9999),
                    expires_at = ts.AddDays(30).ToString("o"),
                    revoked_at = (string?)null,
                    borrowing_device_id = (string?)null,
                    key_id = (string?)null,
                    product_type = "vehicle",
                    share_type = "customer",
                    share_user_sso_id = (string?)null,
                    active_pubkeys = new object?[] { null },
                    id_s = _random.Next(100000000, 999999999).ToString(),
                    owner_id_s = "429511308124",
                    share_user_id_s = "",
                    borrowing_key_hash = (string?)null,
                    vin = state.Vin,
                    share_link = $"https://www.tesla.com/_rs/1/NEW{_random.Next(1000, 9999)}"
                }
            },

            share_invites_redeem = new
            {
                response = new
                {
                    vehicle_id_s = state.VehicleId.ToString(),
                    vin = state.Vin
                }
            },

            share_invites_revoke = new
            {
                response = true
            },

            signed_command = new
            {
                response = new
                {
                    result = true,
                    reason = "",
                    queued = false
                }
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
                    vehicle_id = state.VehicleId,
                    vin = state.Vin,
                    color = state.Color,
                    access_type = "OWNER",
                    display_name = state.DisplayName,
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
                    vehicle_id = state.VehicleId,
                    vin = state.Vin,
                    color = state.Color,
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
                                expirationDate = ts.AddYears(4).ToString("o"),
                                expirationOdometer = 50000,
                                odometerUnit = "MI",
                                warrantyExpiredOn = (string?)null,
                                coverageAgeInYears = 4
                            },
                            new {
                                warrantyType = "BATTERY_WARRANTY",
                                warrantyDisplayName = "Battery Limited Warranty",
                                expirationDate = ts.AddYears(8).ToString("o"),
                                expirationOdometer = 120000,
                                odometerUnit = "MI",
                                warrantyExpiredOn = (string?)null,
                                coverageAgeInYears = 8
                            },
                            new {
                                warrantyType = "DRIVEUNIT_WARRANTY",
                                warrantyDisplayName = "Drive Unit Limited Warranty",
                                expirationDate = ts.AddYears(8).ToString("o"),
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

    // Metodi PUBBLICI

    public static string GenerateRawVehicleJson(VehicleSimulationState state)
    {
        var ts = DateTime.Now;

        var json = new
        {
            response = new
            {
                data = new[] {
                    new { type = "charging_history", content = GenerateChargingHistory(state, ts) },
                    new { type = "energy_endpoints", content = GenerateEnergyEndpoints(state, ts) },
                    new { type = "partner_public_key", content = GeneratePartnerEndpoints() },
                    new { type = "user_profile", content = GenerateUserEndpoints() },
                    new { type = "vehicle_commands", content = GenerateMockVehicleCommands(ts) },
                    new { type = "vehicle_endpoints", content = GenerateVehicleEndpoints(state, ts) }
                }
            }
        };

        return JsonSerializer.Serialize(json);
    }

    public static object GenerateCompleteVehicleData(VehicleSimulationState state)
    {
        var ts = DateTime.Now;
        return GenerateVehicleEndpoints(state, ts);
    }

    public static object GenerateCommandResponse(string commandName, object? parameters)
    {
        var ts = DateTime.Now;

        return new
        {
            response = new
            {
                result = true,
                reason = ""
            }
        };
    }

    public static object GenerateWakeUpResponse(VehicleSimulationState state)
    {
        return new
        {
            response = new
            {
                id = 100021,
                user_id = 800001,
                vehicle_id = state.VehicleId,
                vin = state.Vin,
                color = state.Color,
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
        };
    }

    public static object GenerateVehiclesList(VehicleSimulationState[] states)
    {
        return new
        {
            response = states.Select(state => new
            {
                id = 100021,
                vehicle_id = state.VehicleId,
                vin = state.Vin,
                color = state.Color,
                access_type = "OWNER",
                display_name = state.DisplayName,
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
            }).ToArray(),
            count = states.Length
        };
    }

    public static object GenerateNearbyChargingSites(VehicleSimulationState state)
    {
        var ts = DateTime.Now;
        var timestampMs = new DateTimeOffset(ts).ToUnixTimeMilliseconds();

        return new
        {
            response = new
            {
                congestion_sync_time_utc_secs = timestampMs / 1000,
                destination_charging = new[]
                {
                    new {
                        location = new { lat = 41.9028, @long = 12.4964 },
                        name = "Hotel Artemide Roma",
                        type = "destination",
                        distance_miles = 2.5,
                        amenities = "restrooms,wifi,lodging"
                    }
                },
                superchargers = new[]
                {
                    new {
                        location = new { lat = 41.8919, @long = 12.5113 },
                        name = "Roma EUR",
                        type = "supercharger",
                        distance_miles = 5.2,
                        available_stalls = _random.Next(8, 16),
                        total_stalls = 16,
                        site_closed = false,
                        amenities = "restrooms,restaurant,wifi,shopping",
                        billing_info = ""
                    }
                },
                timestamp = timestampMs
            }
        };
    }
}

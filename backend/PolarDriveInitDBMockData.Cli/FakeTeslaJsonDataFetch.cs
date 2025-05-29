// File: FakeTeslaJsonDataFetch.cs
using System.Text.Json;

namespace PolarDriveInitDBMockData.Cli.Utils;

public static class FakeTeslaJsonDataFetch
{
    public static string GenerateRawVehicleJson(DateTime ts, Random random)
    {
        var json = new
        {
            timestamp = ts.ToString("o"),
            location = new
            {
                latitude = 40.7268 + random.NextDouble() * 0.01,
                longitude = 14.7933 + random.NextDouble() * 0.01
            },
            drive_state = new
            {
                shift_state = "D",
                speed = random.Next(0, 100),
                heading = random.Next(0, 360),
                power = random.Next(0, 150),
                latitude = 40.7268,
                longitude = 14.7933,
                gps_as_of = ((DateTimeOffset)ts).ToUnixTimeSeconds()
            },
            charge_state = new
            {
                charging_state = "Disconnected",
                battery_level = random.Next(40, 95),
                charge_limit_soc = 90,
                charge_energy_added = 0.0,
                charger_voltage = 0,
                charger_pilot_current = 0,
                battery_range = 370 + random.NextDouble() * 10,
                est_battery_range = 340 + random.NextDouble() * 10,
                ideal_battery_range = 400 + random.NextDouble() * 10
            },
            vehicle_state = new
            {
                odometer = 18000 + random.NextDouble() * 2000,
                locked = true,
                sentry_mode = false,
                tpms_pressure_fl = 2.5 + random.NextDouble() * 0.5,
                tpms_pressure_fr = 2.5 + random.NextDouble() * 0.5,
                tpms_pressure_rl = 2.5 + random.NextDouble() * 0.5,
                tpms_pressure_rr = 2.5 + random.NextDouble() * 0.5,
                car_version = "2024.12.7",
                software_update = new
                {
                    status = "available",
                    version = "2024.14.3"
                }
            },
            climate_state = new
            {
                inside_temp = 20 + random.NextDouble() * 5,
                outside_temp = 25 + random.NextDouble() * 7,
                is_auto_conditioning_on = random.Next(0, 2) == 1,
                fan_status = random.Next(0, 6),
                seat_heater_left = random.Next(0, 3),
                seat_heater_right = random.Next(0, 3)
            },
            gui_settings = new
            {
                gui_distance_units = "km/hr",
                gui_temperature_units = "C",
                gui_charge_rate_units = "kW",
                gui_24_hour_time = true
            },
            vehicle_config = new
            {
                car_type = "model3",
                car_special_type = "base",
                car_exterior_color = "Ultra Red",
                wheel_type = "Aero18",
                has_ludicrous_mode = false
            }
        };

        return JsonSerializer.Serialize(json);
    }
}
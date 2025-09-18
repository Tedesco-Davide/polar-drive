using PolarDrive.Data.DbContexts;

var avgChargeFromHome = agg.VehicleChargeHistory.Average(v => v.EnergyAdded);
                sb.AppendLine($"- **Ricarica veicoli da casa**: {agg.VehicleChargeHistory.Count} sessioni, {avgChargeFromHome:F0} Wh media");
            }
        }

        // 📊 ADAPTIVE PROFILING (sezione completa come nel vecchio codice)
        if (agg.AdaptiveSessionsCount > 0 || agg.HasActiveAdaptiveSession)
        {
            sb.AppendLine("### 📊 ADAPTIVE PROFILING COMPLETO");
            sb.AppendLine($"- **Sessioni Adaptive**: {agg.AdaptiveSessionsCount} attivazioni nel periodo");
            sb.AppendLine($"- **Terminazioni**: {agg.AdaptiveSessionsStoppedManually} manuali, {agg.AdaptiveSessionsStoppedAutomatically} automatiche");
            sb.AppendLine($"- **Sessione attiva**: {(agg.HasActiveAdaptiveSession ? "🟢 Sì" : "⚪ No")}");
            
            if (agg.MostActiveAdaptiveHour.HasValue)
            {
                sb.AppendLine($"- **Orario preferito**: {agg.MostActiveAdaptiveHour:00}:xx");
            }

            if (agg.AdaptiveSessionsByDay.Any())
            {
                var topDays = agg.AdaptiveSessionsByDay.OrderByDescending(kvp => kvp.Value).Take(2);
                sb.AppendLine($"- **Giorni più attivi**: {string.Join(", ", topDays.Select(kvp => $"{kvp.Key} ({kvp.Value})"))}");
            }

            if (agg.AdaptiveFrequencyValue > 0)
            {
                sb.AppendLine($"- **Frequenza utilizzo**: {agg.AdaptiveFrequencyAnalysis} ({agg.AdaptiveFrequencyValue:F2} sessioni/giorno)");
            }

            if (agg.AdaptiveDataRecordsCount > 0)
            {
                sb.AppendLine($"- **Dati raccolti**: {agg.AdaptiveDataRecordsCount:N0} record telemetrici durante sessioni Adaptive");
            }
        }

        // 🚨 AVVISI E ALERT RECENTI
        if (agg.RecentAlerts.Any())
        {
            sb.AppendLine("### 🚨 AVVISI E ALERT RECENTI");
            sb.AppendLine($"- **Alert totali**: {agg.RecentAlerts.Count}");
            
            if (agg.AlertsByType.Any())
            {
                var topAlert = agg.AlertsByType.OrderByDescending(kvp => kvp.Value).First();
                sb.AppendLine($"- **Tipo più frequente**: {topAlert.Key} ({topAlert.Value} occorrenze)");
            }

            // Mostra gli alert più recenti
            var recentestAlerts = agg.RecentAlerts.Take(3);
            sb.AppendLine("- **Alert recenti**:");
            foreach (var alert in recentestAlerts)
            {
                sb.AppendLine($"  • {alert.Time}: {alert.Name} - {alert.UserText}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("**NOTA**: Dati aggregati e processati mantenendo TUTTA la logica di RawDataPreparserFullMapped ma ottimizzati per l'analisi AI.");

        return sb.ToString();
    }

    private async Task<(DateTime firstUtc, DateTime lastUtc, int totalRecords, double realDensity)> GetLightStatsAsync(int vehicleId)
    {
        try
        {
            var firstRecord = await _dbContext.VehiclesData
                .Where(v => v.VehicleId == vehicleId)
                .OrderBy(v => v.Timestamp)
                .Select(v => v.Timestamp)
                .FirstOrDefaultAsync();

            var lastRecord = await _dbContext.VehiclesData
                .Where(v => v.VehicleId == vehicleId)
                .OrderByDescending(v => v.Timestamp)
                .Select(v => v.Timestamp)
                .FirstOrDefaultAsync();

            var total = await _dbContext.VehiclesData
                .Where(v => v.VehicleId == vehicleId)
                .CountAsync();

            if (firstRecord == default || lastRecord == default || total == 0)
                return (default, default, total, 0);

            var totalHoursReal = Math.Max(1.0, (lastRecord - firstRecord).TotalHours);
            var density = total / totalHoursReal;
            return (firstRecord, lastRecord, total, density);
        }
        catch
        {
            return (default, default, 0, 0);
        }
    }


}

    #region METODI HELPER COMPLETI (tutti i metodi del vecchio RawDataPreparserFullMapped)

    private static string? GetSafeStringValue(JsonElement element, string propertyName, string? defaultValue = "N/A")
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? (prop.GetString() ?? defaultValue)
            : defaultValue;
    }

    private static int GetSafeIntValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind == JsonValueKind.Number ? prop.GetInt32() : 0;
    }

    private static decimal GetSafeDecimalValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind == JsonValueKind.Number ? prop.GetDecimal() : 0m;
    }

    private static bool GetSafeBooleanValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind == JsonValueKind.True;
    }

    private static string TranslateShiftState(string shiftState)
    {
        return shiftState?.ToUpper() switch
        {
            "P" => "Parcheggio",
            "D" => "Avanti",
            "R" => "Retromarcia", 
            "N" => "Folle",
            null => "Non disponibile",
            _ => shiftState
        };
    }

    private static string GetCompassDirection(int heading)
    {
        return heading switch
        {
            _ when heading >= 337 || heading <= 22 => "Nord",
            >= 23 and <= 67 => "Nord-Est",
            >= 68 and <= 112 => "Est",
            >= 113 and <= 157 => "Sud-Est",
            >= 158 and <= 202 => "Sud",
            >= 203 and <= 247 => "Sud-Ovest",
            >= 248 and <= 292 => "Ovest",
            >= 293 and <= 336 => "Nord-Ovest",
            _ => "Sconosciuto"
        };
    }

    private static string FormatCoordinatesItalian(decimal latitude, decimal longitude)
    {
        var latDir = latitude >= 0 ? "N" : "S";
        var lonDir = longitude >= 0 ? "E" : "W";
        return $"{Math.Abs(latitude):F6}°{latDir}, {Math.Abs(longitude):F6}°{lonDir}";
    }

    private static string GetItalianLocationName(decimal latitude, decimal longitude)
    {
        return (latitude, longitude) switch
        {
            var (lat, lon) when Math.Abs(lat - 41.9028m) < 0.1m && Math.Abs(lon - 12.4964m) < 0.1m => "Roma",
            var (lat, lon) when Math.Abs(lat - 45.4642m) < 0.1m && Math.Abs(lon - 9.1900m) < 0.1m => "Milano",
            var (lat, lon) when Math.Abs(lat - 40.8518m) < 0.1m && Math.Abs(lon - 14.2681m) < 0.1m => "Napoli",
            var (lat, lon) when Math.Abs(lat - 45.0703m) < 0.1m && Math.Abs(lon - 7.6869m) < 0.1m => "Torino",
            var (lat, lon) when Math.Abs(lat - 44.4949m) < 0.1m && Math.Abs(lon - 11.3426m) < 0.1m => "Bologna",
            var (lat, lon) when Math.Abs(lat - 43.7696m) < 0.1m && Math.Abs(lon - 11.2558m) < 0.1m => "Firenze",
            _ => "Località italiana"
        };
    }

    private static string GetCommandCategoryComplete(string commandName)
    {
        return commandName switch
        {
            var cmd when cmd.StartsWith("charge_", StringComparison.OrdinalIgnoreCase) || cmd.Contains("charge", StringComparison.OrdinalIgnoreCase) => "Ricarica",
            var cmd when cmd.StartsWith("door_", StringComparison.OrdinalIgnoreCase) || cmd.Contains("trunk", StringComparison.OrdinalIgnoreCase) => "Accesso Veicolo",
            var cmd when cmd.Contains("climate", StringComparison.OrdinalIgnoreCase) || cmd.Contains("temp", StringComparison.OrdinalIgnoreCase) || cmd.Contains("heat", StringComparison.OrdinalIgnoreCase) || cmd.Contains("cool", StringComparison.OrdinalIgnoreCase) || cmd.Contains("conditioning", StringComparison.OrdinalIgnoreCase) => "Climatizzazione",
            var cmd when cmd.StartsWith("media_", StringComparison.OrdinalIgnoreCase) || cmd.Contains("volume", StringComparison.OrdinalIgnoreCase) => "Sistema Multimediale",
            var cmd when cmd.StartsWith("navigation_", StringComparison.OrdinalIgnoreCase) => "Navigazione",
            var cmd when cmd.Contains("sentry", StringComparison.OrdinalIgnoreCase) || cmd.Contains("valet", StringComparison.OrdinalIgnoreCase) || cmd.Contains("speed_limit", StringComparison.OrdinalIgnoreCase) || cmd.Contains("pin", StringComparison.OrdinalIgnoreCase) => "Sicurezza",
            var cmd when cmd.Contains("seat", StringComparison.OrdinalIgnoreCase) || cmd.Contains("steering_wheel", StringComparison.OrdinalIgnoreCase) || cmd.Contains("window", StringComparison.OrdinalIgnoreCase) || cmd.Contains("sun_roof", StringComparison.OrdinalIgnoreCase) => "Comfort",
            var cmd when cmd.Contains("software", StringComparison.OrdinalIgnoreCase) || cmd.Contains("schedule", StringComparison.OrdinalIgnoreCase) => "Sistema",
            var cmd when cmd.Contains("lights", StringComparison.OrdinalIgnoreCase) || cmd.Contains("horn", StringComparison.OrdinalIgnoreCase) || cmd.Contains("homelink", StringComparison.OrdinalIgnoreCase) || cmd.Contains("boombox", StringComparison.OrdinalIgnoreCase) => "Funzioni Esterne",
            _ => "Altro"
        };
    }

    private static string GetCommandDisplayName(string commandName)
    {
        return commandName switch
        {
            "actuate_trunk" => "Apertura bagagliaio",
            "add_charge_schedule" => "Aggiunta programmazione ricarica",
            "add_precondition_schedule" => "Aggiunta programmazione precondizionamento",
            "adjust_volume" => "Regolazione volume",
            "auto_conditioning_start" => "Avvio climatizzazione automatica",
            "auto_conditioning_stop" => "Stop climatizzazione automatica",
            "cancel_software_update" => "Annulla aggiornamento software",
            "charge_max_range" => "Ricarica a massima autonomia",
            "charge_port_door_close" => "Chiusura sportello ricarica",
            "charge_port_door_open" => "Apertura sportello ricarica",
            "charge_standard" => "Ricarica standard",
            "charge_start" => "Avvio ricarica",
            "charge_stop" => "Stop ricarica",
            "door_lock" => "Blocco porte",
            "door_unlock" => "Sblocco porte",
            "flash_lights" => "Lampeggio luci",
            "honk_horn" => "Suono clacson",
            "guest_mode" => "Modalità ospite",
            "media_next_track" => "Traccia successiva",
            "media_prev_track" => "Traccia precedente",
            "media_toggle_playback" => "Play/Pausa",
            "navigation_request" => "Navigazione verso destinazione",
            "navigation_gps_request" => "Navigazione GPS",
            "set_charge_limit" => "Impostazione limite ricarica",
            "set_temps" => "Impostazione temperature",
            "set_sentry_mode" => "Modalità sentinella",
            "set_valet_mode" => "Modalità parcheggiatore",
            "set_vehicle_name" => "Impostazione nome veicolo",
            "window_control" => "Controllo finestrini",
            "remote_start_drive" => "Avvio remoto",
            "homelink_request" => "Comando HomeLink",
            "speed_limit_set_limit" => "Impostazione limite velocità",
            "speed_limit_activate" => "Attivazione limite velocità",
            "speed_limit_deactivate" => "Disattivazione limite velocità",
            "speed_limit_clear_pin" => "Cancellazione PIN limite velocità",
            "sun_roof_control" => "Controllo tetto apribile",
            "trigger_homelink" => "Attivazione HomeLink",
            "remote_seat_heater_request" => "Riscaldamento sedili",
            "remote_steering_wheel_heater_request" => "Riscaldamento volante",
            "set_scheduled_charging" => "Programmazione ricarica",
            "set_scheduled_departure" => "Programmazione partenza",
            "navigation_share_location" => "Condivisione posizione",
            "media_volume_up" => "Volume su",
            "media_volume_down" => "Volume giù",
            "boombox" => "Modalità Boombox",
            "schedule_software_update" => "Programmazione aggiornamento",
            "set_cabin_overheat_protection" => "Protezione surriscaldamento",
            "set_climate_keeper_mode" => "Modalità mantenimento clima",
            "set_preconditioning_max" => "Precondizionamento massimo",
            _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(commandName.Replace("_", " "))
        };
    }

    private static string FormatCommandParameters(string commandName, JsonElement parameters)
    {
        var paramList = new List<string>();

        foreach (var prop in parameters.EnumerateObject())
        {
            var value = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.GetDecimal().ToString("F1"),
                JsonValueKind.True => "Sì",
                JsonValueKind.False => "No",
                JsonValueKind.Array => $"[{string.Join(", ", prop.Value.EnumerateArray().Select(e => e.GetString()))}]",
                _ => prop.Value.ToString()
            };

            var displayName = prop.Name switch
            {
                "which_trunk" => "Bagagliaio",
                "start_time" => "Ora inizio",
                "end_time" => "Ora fine",
                "level" => "Livello",
                "percent" => "Percentuale",
                "enabled" => "Abilitato",
                "location" => "Destinazione",
                "latitude" => "Latitudine",
                "longitude" => "Longitudine",
                "vehicle_name" => "Nome veicolo",
                "on" => "Attivo",
                "state" => "Stato",
                "command" => "Comando",
                "days" => "Giorni",
                "time" => "Orario",
                "driver_temp" => "Temp. guidatore",
                "passenger_temp" => "Temp. passeggero",
                "heat_level" => "Livello riscaldamento",
                "seat_position" => "Posizione sedile",
                "limit_mph" => "Limite velocità",
                "pin" => "PIN",
                "lat" => "Latitudine",
                "lon" => "Longitudine",
                "address" => "Indirizzo",
                "volume" => "Volume",
                "media_type" => "Tipo media",
                "preset" => "Preset",
                "repeat" => "Ripeti",
                "shuffle" => "Casuale",
                "charging_amps" => "Ampere ricarica",
                "charging_sites" => "Stazioni ricarica",
                "departure_time" => "Orario partenza",
                "preconditioning_enabled" => "Precondizionamento",
                "preconditioning_weekdays_only" => "Solo giorni feriali",
                "off_peak_charging_enabled" => "Ricarica ore non di punta",
                "off_peak_charging_weekdays_only" => "Ricarica off-peak solo feriali",
                "end_time_charging" => "Fine ricarica",
                _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(prop.Name.Replace("_", " "))
            };

            paramList.Add($"{displayName}: {value}");
        }

        return paramList.Count != 0 ? string.Join(", ", paramList) : "";
    }

    private static string GetCommandAnalysis(string commandName, bool success, JsonElement? parameters)
    {
        if (!success) return string.Empty;

        return commandName switch
        {
            "charge_start" => "Ricarica avviata - monitorare il progresso",
            "charge_stop" => "Ricarica interrotta - verificare se intenzionale",
            "auto_conditioning_start" => "Pre-condizionamento attivo - ottimizza l'autonomia",
            "door_unlock" => "Veicolo sbloccato - ricordare di richiudere",
            "set_sentry_mode" when parameters?.TryGetProperty("on", out var onParam) == true && GetSafeBooleanValue(parameters.Value, "on") =>
                "Sentry Mode attivato - maggiore sicurezza ma consumo batteria",
            "navigation_request" => "Destinazione impostata - percorso ottimizzato",
            "flash_lights" => "Utile per localizzare il veicolo",
            "honk_horn" => "Comando di localizzazione eseguito",
            _ => "Comando eseguito"
        };
    }

    private static string AnalyzeEnergyBalance(int solarExported, int gridImported, int batteryExported, int consumerTotal)
    {
        var netBalance = solarExported - consumerTotal;
        return netBalance switch
        {
            > 1000 => "Surplus energetico significativo",
            > 0 => "Leggero surplus energetico",
            > -1000 => "Bilancio energetico equilibrato",
            > -5000 => "Dipendenza moderata dalla rete",
            _ => "Alta dipendenza dalla rete elettrica"
        };
    }

    private static string CategorizeFleetTelemetryError(string errorMessage)
    {
        var lowerError = errorMessage.ToLower();
        return lowerError switch
        {
            var msg when msg.Contains("connection") || msg.Contains("connect") => "Connessione",
            var msg when msg.Contains("timeout") => "Timeout",
            var msg when msg.Contains("certificate") || msg.Contains("ssl") || msg.Contains("tls") => "Certificati",
            var msg when msg.Contains("authentication") || msg.Contains("auth") => "Autenticazione",
            var msg when msg.Contains("parse") || msg.Contains("format") => "Formato Dati",
            var msg when msg.Contains("config") => "Configurazione",
            var msg when msg.Contains("rate") || msg.Contains("limit") => "Rate Limiting",
            var msg when msg.Contains("key") => "Chiavi",
            _ => "Generico"
        };
    }

    private static string GetErrorSeverity(string category)
    {
        return category switch
        {
            "Connessione" => "🔴",
            "Certificati" => "🔴",
            "Autenticazione" => "🔴",
            "Timeout" => "🟠",
            "Rate Limiting" => "🟡",
            "Configurazione" => "🟠",
            "Formato Dati" => "🟡",
            "Chiavi" => "🔴",
            _ => "🔵"
        };
    }

    private static string GetErrorSolution(string errorMessage)
    {
        var lowerError = errorMessage.ToLower();
        return lowerError switch
        {
            var msg when msg.Contains("connection refused") => "Verificare connettività di rete e stato server",
            var msg when msg.Contains("timeout") => "Aumentare timeout o verificare latenza rete",
            var msg when msg.Contains("certificate") => "Rinnovare certificati SSL/TLS",
            var msg when msg.Contains("invalid key") => "Verificare e rigenerare chiavi API",
            var msg when msg.Contains("rate limit") => "Implementare backoff exponential",
            var msg when msg.Contains("parse error") => "Verificare formato dati inviati",
            var msg when msg.Contains("config") => "Controllare configurazione telemetria",
            _ => ""
        };
    }

    private static List<string> GenerateFleetTelemetryRecommendations(Dictionary<string, int> errorsByType, int totalErrors)
    {
        var recommendations = new List<string>();

        if (errorsByType.ContainsKey("Connessione"))
        {
            recommendations.Add("Verificare connettività di rete dei veicoli e stato server telemetria");
        }

        if (errorsByType.ContainsKey("Certificati"))
        {
            recommendations.Add("Rinnovare certificati SSL scaduti e verificare catena di certificazione");
        }

        if (errorsByType.ContainsKey("Rate Limiting"))
        {
            recommendations.Add("Implementare strategia di retry con backoff e ridurre frequenza invio dati");
        }

        if (totalErrors > 10)
        {
            recommendations.Add("Alto numero di errori - revisione configurazione necessaria");
        }

        recommendations.Add("Monitorare trend errori per identificare pattern sistematici");

        return recommendations;
    }

    private static string TranslateDayOfWeek(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "Lunedì",
            DayOfWeek.Tuesday => "Martedì", 
            DayOfWeek.Wednesday => "Mercoledì",
            DayOfWeek.Thursday => "Giovedì",
            DayOfWeek.Friday => "Venerdì",
            DayOfWeek.Saturday => "Sabato",
            DayOfWeek.Sunday => "Domenica",
            _ => dayOfWeek.ToString()
        };
    }

    #endregion
}

#region CLASSI DI AGGREGAZIONE COMPLETE (mantiene TUTTA la struttura dati)

/// <summary>
/// Aggregazione COMPLETA di tutti i dati Tesla mantenendo la stessa logica di RawDataPreparserFullMapped
/// </summary>
public class CompleteTeslaDataAggregation
{
    // Batteria - come prima ma più completo
    public List<int> BatteryLevels { get; set; } = new();
    public List<decimal> BatteryRanges { get; set; } = new();
    public List<int> ChargeLimits { get; set; } = new();
    public List<decimal> ChargeRates { get; set; } = new();
    public List<int> MinutesToFullReadings { get; set; } = new();
    public Dictionary<string, int> ChargingStates { get; set; } = new();
    public Dictionary<string, int> BatteryAnalyses { get; set; } = new();

    // Ricarica - molto più dettagliata
    public List<ChargingSessionComplete> ChargingSessions { get; set; } = new();
    public List<decimal> ChargingCosts { get; set; } = new();
    public List<decimal> EnergyConsumed { get; set; } = new();
    public List<decimal> CostPerKwhValues { get; set; } = new();
    public Dictionary<string, int> ChargingByCountry { get; set; } = new();
    public Dictionary<string, int> ChargingBySite { get; set; } = new();
    public Dictionary<string, int> InvoiceTypes { get; set; } = new();
    public Dictionary<string, int> PaymentStatus { get; set; } = new();
    public List<PricingTierData> PricingTiers { get; set; } = new();

    // Clima - molto più dettagliato
    public List<decimal> InsideTemperatures { get; set; } = new();
    public List<decimal> OutsideTemperatures { get; set; } = new();
    public List<decimal> DriverTempSettings { get; set; } = new();
    public List<decimal> PassengerTempSettings { get; set; } = new();
    public Dictionary<bool, int> ClimateUsage { get; set; } = new();
    public Dictionary<string, int> ClimateAnalyses { get; set; } = new();
    public Dictionary<string, int> CabinOverheatSettings { get; set; } = new();

    // Guida - con analisi posizioni complete
    public List<int> Speeds { get; set; } = new();
    public List<LocationPointComplete> Locations { get; set; } = new();
    public List<decimal> OdometerReadings { get; set; } = new();
    public Dictionary<string, int> ShiftStates { get; set; } = new();

    // Sicurezza e manutenzione
    public Dictionary<string, int> SecurityStates { get; set; } = new();
    public List<decimal> TirePressures { get; set; } = new();
    public Dictionary<string, int> TirePressureAnalyses { get; set; } = new();

    // Comandi - analisi molto avanzata
    public Dictionary<string, int> CommandCategories { get; set; } = new();
    public Dictionary<bool, int> CommandSuccess { get; set; } = new();
    public Dictionary<string, int> CommandTypes { get; set; } = new();
    public Dictionary<string, int> CommandsByHour { get; set; } = new();
    public List<CommandWithParameters> CommandsWithParameters { get; set; } = new();
    public Dictionary<string, int> CommandFailureReasons { get; set; } = new();
    public Dictionary<string, int> CommandAnalyses { get; set; } = new();
    public List<DateTime> CommandTimestamps { get; set; } = new();

    // Informazioni veicolo e flotta
    public string? VehicleVin { get; set; }
    public string? VehicleName { get; set; }
    public Dictionary<string, int> VehicleStates { get; set; } = new();
    public int AssociatedVehiclesCount { get; set; }
    public Dictionary<string, int> AssociatedVehicleStates { get; set; } = new();
    public int AuthorizedDriversCount { get; set; }
    public List<string> AuthorizedDrivers { get; set; } = new();
    
    // Fleet Status
    public int KeyPairedVehicles { get; set; }
    public int UnpairedVehicles { get; set; }
    public Dictionary<string, int> FirmwareVersions { get; set; } = new();

    // Stazioni ricarica vicine
    public List<NearbyChargerInfo> NearbyChargers { get; set; } = new();

    // Opzioni e upgrade veicolo
    public List<VehicleOptionInfo> VehicleOptions { get; set; } = new();
    public List<string> EligibleSubscriptions { get; set; } = new();
    public Dictionary<string, int> EligibleUpgrades { get; set; } = new();

    // Garanzie e assistenza
    public List<WarrantyInfo> ActiveWarranties { get; set; } = new();
    public ServiceStatusInfo? ServiceStatus { get; set; }

    // Fleet Telemetry
    public FleetTelemetryStatus? FleetTelemetryStatus { get; set; }
    public List<FleetTelemetryError> FleetTelemetryErrors { get; set; } = new();
    public Dictionary<string, int> TelemetryErrorsByCategory { get; set; } = new();
    public Dictionary<string, int> TelemetryErrorsByVin { get; set; } = new();

    // Avvisi recenti
    public List<AlertInfo> RecentAlerts { get; set; } = new();
    public Dictionary<string, int> AlertsByType { get; set; } = new();

    // Partner Public Key e sicurezza
    public PublicKeyInfo? PublicKeyInfo { get; set; }
    public int FleetTelemetryErrorVinsCount { get; set; }
    public string? FleetTelemetryErrorLevel { get; set; }

    // Profilo utente
    public UserProfileInfo? UserProfile { get; set; }
    public RegionInfo? RegionInfo { get; set; }
    public FeatureConfigInfo? FeatureConfig { get; set; }

    // Ordini
    public int OrdersCount { get; set; }
    public List<OrderInfo> Orders { get; set; } = new();
    public Dictionary<string, int> OrdersByStatus { get; set; } = new();
    public Dictionary<string, int> OrdersByModel { get; set; } = new();
    public Dictionary<string, int> OrdersByType { get; set; } = new();

    // Sistema energetico domestico - MOLTO più dettagliato
    public List<int> EnergySolar { get; set; } = new();
    public List<int> EnergyBattery { get; set; } = new();
    public List<int> EnergyGrid { get; set; } = new();
    public List<int> EnergyLoad { get; set; } = new();
    public decimal EnergySystemBatteryLevel { get; set; }
    public int EnergySystemCapacity { get; set; }
    public Dictionary<string, int> EnergySystemStatus { get; set; } = new();
    public Dictionary<string, int> SolarProductionStatus { get; set; } = new();
    public Dictionary<string, int> GridConnectionStatus { get; set; } = new();
    public int StormModeActivations { get; set; }
    public int BackupCapableReadings { get; set; }
    
    // Site info energetico
    public EnergySiteInfo? EnergySiteInfo { get; set; }
    public List<EnergyHistoryEntry> EnergyHistoryEntries { get; set; } = new();
    public Dictionary<string, int> EnergyBalanceAnalyses { get; set; } = new();
    public List<VehicleChargeEntry> VehicleChargeHistory { get; set; } = new();
    public int BackupEventsTotal { get; set; }
    public List<BackupEvent> BackupEvents { get; set; } = new();
    public int EnergyProductsCount { get; set; }
    public Dictionary<string, int> EnergyProductTypes { get; set; } = new();
    public Dictionary<string, int> ConnectedVehicleStates { get; set; } = new();
    public ConnectedEnergySystemInfo? ConnectedEnergySystemInfo { get; set; }

    // Adaptive Profiling - sezione completa
    public int AdaptiveSessionsCount { get; set; }
    public int AdaptiveSessionsStoppedManually { get; set; }
    public int AdaptiveSessionsStoppedAutomatically { get; set; }
    public bool HasActiveAdaptiveSession { get; set; }
    public int? MostActiveAdaptiveHour { get; set; }
    public Dictionary<string, int> AdaptiveSessionsByDay { get; set; } = new();
    public string AdaptiveFrequencyAnalysis { get; set; } = "";
    public double AdaptiveFrequencyValue { get; set; }
    public int AdaptiveDataRecordsCount { get; set; }

    // Proprietà calcolate (come nel vecchio codice)
    public decimal BatteryLevelAvg => BatteryLevels.Any() ? BatteryLevels.Average() : 0;
    public int BatteryLevelMin => BatteryLevels.Any() ? BatteryLevels.Min() : 0;
    public int BatteryLevelMax => BatteryLevels.Any() ? BatteryLevels.Max() : 0;
    public decimal BatteryRangeAvg => BatteryRanges.Any() ? BatteryRanges.Average() : 0;
    public decimal BatteryRangeMin => BatteryRanges.Any() ? BatteryRanges.Min() : 0;
    public decimal BatteryRangeMax => BatteryRanges.Any() ? BatteryRanges.Max() : 0;

    public decimal AvgChargingDuration => ChargingSessions.Any() ? (decimal)ChargingSessions.Average(s => s.Duration) : 0;
    public decimal AvgChargingCost => ChargingCosts.Any() ? ChargingCosts.Average() : 0;
    public decimal AvgEnergyEfficiency => (ChargingCosts.Any() && EnergyConsumed.Any() && EnergyConsumed.Average() != 0)
        ? ChargingCosts.Average() / EnergyConsumed.Average()
        : 0;

    public decimal AvgInsideTemp => InsideTemperatures.Any() ? InsideTemperatures.Average() : 0;
    public decimal MinInsideTemp => InsideTemperatures.Any() ? InsideTemperatures.Min() : 0;
    public decimal MaxInsideTemp => InsideTemperatures.Any() ? InsideTemperatures.Max() : 0;
    public decimal AvgOutsideTemp => OutsideTemperatures.Any() ? OutsideTemperatures.Average() : 0;
    public decimal MinOutsideTemp => OutsideTemperatures.Any() ? OutsideTemperatures.Min() : 0;
    public decimal MaxOutsideTemp => OutsideTemperatures.Any() ? OutsideTemperatures.Max() : 0;
    public decimal ClimateUsagePercent => ClimateUsage.Any()
        ? (ClimateUsage.GetValueOrDefault(true) * 100.0m / Math.Max(1, ClimateUsage.Values.Sum()))
        : 0;

    public decimal AvgSpeed => Speeds.Any() ? Speeds.Average() : 0;
    public decimal SecurityUsagePercent => SecurityStates.ContainsKey("locked") && SecurityStates.Values.Sum() > 0
        ? (SecurityStates["locked"] * 100.0m / SecurityStates.Values.Sum())
        : 0;
    public decimal SentryUsagePercent => SecurityStates.ContainsKey("sentry") && SecurityStates.Values.Sum() > 0
        ? (SecurityStates["sentry"] * 100.0m / SecurityStates.Values.Sum())
        : 0;

    public decimal AvgTirePressure => TirePressures.Any() ? TirePressures.Average() : 0;
    public decimal MinTirePressure => TirePressures.Any() ? TirePressures.Min() : 0;
    public decimal MaxTirePressure => TirePressures.Any() ? TirePressures.Max() : 0;

    public decimal CommandSuccessRate => CommandSuccess.Any() && CommandSuccess.Values.Sum() > 0
        ? (CommandSuccess.GetValueOrDefault(true) * 100.0m / CommandSuccess.Values.Sum())
        : 0;

    // Analisi qualitative (come nel vecchio codice)
    public string DrivingStyleAnalysis => AvgSpeed switch
    {
        <= 30 => "Guida urbana conservativa",
        <= 50 => "Guida mista equilibrata",
        <= 80 => "Guida scorrevole",
        _ => "Guida sportiva"
    };

    public string TireConditionAnalysis
    {
        get
        {
            if (!TirePressures.Any()) return "Dati non disponibili";
            var maxDiff = MaxTirePressure - MinTirePressure;
            return maxDiff switch
            {
                <= 0.2m => "Pressioni uniformi - ottimo",
                <= 0.5m => "Leggere differenze - buono",
                _ => "Differenze significative - controllo necessario"
            };
        }
    }

    public string EnergyBalanceAnalysis
    {
        get
        {
            if (!EnergySolar.Any() || !EnergyLoad.Any()) return "Dati insufficienti";
            var avgSolar = EnergySolar.Where(s => s > 0).DefaultIfEmpty(0).Average();
            var avgLoad = EnergyLoad.Where(l => l > 0).DefaultIfEmpty(0).Average();

            var balance = avgSolar - avgLoad;
            return balance switch
            {
                > 1000 => "Surplus energetico significativo",
                > 0 => "Leggero surplus energetico",
                > -1000 => "Bilancio energetico equilibrato",
                _ => "Dipendenza dalla rete elettrica"
            };
        }
    }

    public void FinalizeCalculations()
    {
        // Eventuali calcoli finali o validazioni
        // Ad esempio, normalizzazione dei dati o calcoli derivati
    }
}

#region CLASSI DI SUPPORTO COMPLETE (tutte le classi necessarie)

public class ChargingSessionComplete
{
    public int SessionId { get; set; }
    public double Duration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime StopTime { get; set; }
    public string Site { get; set; } = "";
    public string Country { get; set; } = "";
    public string BillingType { get; set; } = "";
    public string VehicleType { get; set; } = "";
    public string SessionType { get; set; } = "";
    public double DisconnectDelay { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalEnergy { get; set; }
    public decimal CostPerKwh { get; set; }
    public string Currency { get; set; } = "";
    public string CostAnalysis { get; set; } = "";
    public int InvoiceCount { get; set; }
}

public class LocationPointComplete
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int Heading { get; set; }
    public int Speed { get; set; }
    public string FormattedCoords { get; set; } = "";
    public string LocationName { get; set; } = "";
    public string CompassDirection { get; set; } = "";
}

public class PricingTierData
{
    public decimal Rate { get; set; }
    public decimal Usage { get; set; }
    public string Currency { get; set; } = "";
    public string Type { get; set; } = "";
}

public class CommandWithParameters
{
    public string CommandName { get; set; } = "";
    public string Parameters { get; set; } = "";
    public bool Success { get; set; }
    public string Timestamp { get; set; } = "";
}

public class NearbyChargerInfo
{
    public string Name { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public int AvailableStalls { get; set; }
    public int TotalStalls { get; set; }
    public string Type { get; set; } = "";
}

public class VehicleOptionInfo
{
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; }
}

public class WarrantyInfo
{
    public string DisplayName { get; set; } = "";
    public string ExpirationDate { get; set; } = "";
    public int ExpirationOdometer { get; set; }
    public string OdometerUnit { get; set; } = "";
}

public class FleetTelemetryStatus
{
    public bool Synced { get; set; }
    public bool LimitReached { get; set; }
    public bool KeyPaired { get; set; }
    public string ServerInfo { get; set; } = "";
    public string DeliveryPolicy { get; set; } = "";
    public int ConfiguredFieldsCount { get; set; }
}

public class FleetTelemetryError
{
    public string ClientName { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public string Vin { get; set; } = "";
    public string Category { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Solution { get; set; } = "";
}

public class AlertInfo
{
    public string Name { get; set; } = "";
    public string Time { get; set; } = "";
    public string UserText { get; set; } = "";
}

public class ServiceStatusInfo
{
    public string Status { get; set; } = "";
    public string EstimatedCompletion { get; set; } = "";
    public string VisitNumber { get; set; } = "";
}

public class PublicKeyInfo
{
    public int KeyLength { get; set; }
    public string KeyStrength { get; set; } = "";
    public string KeyPreview { get; set; } = "";
}

public class UserProfileInfo
{
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string VaultUuid { get; set; } = "";
}

public class RegionInfo
{
    public string RegionCode { get; set; } = "";
    public string FleetApiUrl { get; set; } = "";
}

public class FeatureConfigInfo
{
    public bool SignalingEnabled { get; set; }
    public bool SubscribeConnectivity { get; set; }
    public bool UseAuthToken { get; set; }
}

public class OrderInfo
{
    public string Status { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public bool IsB2B { get; set; }
    public int OptionsCount { get; set; }
}

public class EnergySiteInfo
{
    public string SiteName { get; set; } = "";
    public int BackupReservePercent { get; set; }
    public string RealMode { get; set; } = "";
    public string InstallationDate { get; set; } = "";
    public int BatteryCount { get; set; }
    public int NameplatePower { get; set; }
    public int NameplateEnergy { get; set; }
    public string Version { get; set; } = "";
    public bool HasSolar { get; set; }
    public bool HasBattery { get; set; }
    public bool HasGrid { get; set; }
}

public class EnergyHistoryEntry
{
    public string Timestamp { get; set; } = "";
    public int SolarExported { get; set; }
    public int GridImported { get; set; }
    public int GridExported { get; set; }
    public int BatteryExported { get; set; }
    public int ConsumerTotal { get; set; }
}

public class VehicleChargeEntry
{
    public string StartTime { get; set; } = "";
    public double DurationHours { get; set; }
    public int EnergyAdded { get; set; }
}

public class BackupEvent
{
    public string Timestamp { get; set; } = "";
    public double DurationHours { get; set; }
}

public class ConnectedEnergySystemInfo
{
    public string SiteName { get; set; } = "";
    public int EnergyLeft { get; set; }
    public int TotalEnergy { get; set; }
    public int PercentageCharged { get; set; }
}

#endregion

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// Aggregatore intelligente COMPLETO che processa 720h di dati JSON Tesla 
/// mantenendo TUTTA la logica di RawDataPreparserFullMapped ma con approccio aggregativo
/// per ottimizzare l'uso di token con Ollama
/// </summary>
public class IntelligentDataAggregator
{
    private readonly PolarDriveDbContext _dbContext;
    private readonly PolarDriveLogger _logger;

    private const int WindowHours = 720; // 30 giorni

    public IntelligentDataAggregator(PolarDriveDbContext dbContext)
    {
        _dbContext = dbContext;
        _logger = new PolarDriveLogger(dbContext);
    }

    public async Task<string> GenerateAggregatedInsights(List<string> rawJsonList, int vehicleId)
    {
        var source = "IntelligentDataAggregator.GenerateAggregatedInsights";
        await _logger.Info(source, $"Processando {rawJsonList.Count} record per aggregazione COMPLETA", $"VehicleId: {vehicleId}");

        var aggregation = new CompleteTeslaDataAggregation();
        var processedRecords = 0;

        // Processa ogni JSON e aggrega TUTTI i dati come faceva RawDataPreparserFullMapped
        foreach (var rawJson in rawJsonList)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rawJson))
                    continue;

                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("response", out var response) &&
                    response.TryGetProperty("data", out var dataArray) &&
                    dataArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in dataArray.EnumerateArray())
                    {
                        if (!item.TryGetProperty("type", out var typeProp) ||
                            !item.TryGetProperty("content", out var content))
                            continue;

                        var type = typeProp.GetString();

                        switch (type)
                        {
                            case "charging_history":
                                ProcessChargingHistoryComplete(content, aggregation);
                                break;
                            case "vehicle_endpoints":
                                ProcessVehicleEndpointsComplete(content, aggregation);
                                break;
                            case "vehicle_commands":
                                ProcessVehicleCommandsComplete(content, aggregation);
                                break;
                            case "energy_endpoints":
                                ProcessEnergyEndpointsComplete(content, aggregation);
                                break;
                            case "partner_public_key":
                                ProcessPartnerPublicKeyComplete(content, aggregation);
                                break;
                            case "user_profile":
                                ProcessUserProfileComplete(content, aggregation);
                                break;
                        }
                    }
                }
                processedRecords++;
            }
            catch (Exception ex)
            {
                await _logger.Debug(source, $"Errore processing JSON {processedRecords}", ex.Message);
            }
        }

        // Calcola le metriche finali
        aggregation.FinalizeCalculations();

        // Aggiungi dati SMS Adaptive Profiling
        await AddAdaptiveProfilingDataComplete(vehicleId, aggregation);

        // Genera output ottimizzato ma COMPLETO
        var result = await GenerateCompleteOptimizedPromptDataAsync(aggregation, processedRecords, vehicleId);

        // Log riduzione token
        var totalChars = rawJsonList.Sum(j => j?.Length ?? 0);
        var saved = Math.Max(0, totalChars - (result?.Length ?? 0));
        var reduction = totalChars > 0 ? (saved * 100.0 / totalChars) : 0.0;

        await _logger.Info(
            source,
            $"Aggregazione COMPLETA completata: {processedRecords} record → {(result?.Length ?? 0)} caratteri",
            $"Riduzione: {reduction:F1}% (da {totalChars} char) mantenendo TUTTA la logica");

        return result ?? string.Empty;
    }

    #region PROCESSAMENTO COMPLETO DEI DATI (come RawDataPreparserFullMapped)

    private void ProcessChargingHistoryComplete(JsonElement content, CompleteTeslaDataAggregation aggregation)
    {
        if (content.ValueKind != JsonValueKind.Object) return;

        // Informazioni base della sessione
        var site = GetSafeStringValue(content, "siteLocationName");
        var startDateTime = GetSafeStringValue(content, "chargeStartDateTime");
        var stopDateTime = GetSafeStringValue(content, "chargeStopDateTime");
        var vin = GetSafeStringValue(content, "vin");
        var unlatch = GetSafeStringValue(content, "unlatchDateTime");
        var country = GetSafeStringValue(content, "countryCode");
        var billingType = GetSafeStringValue(content, "billingType");
        var vehicleType = GetSafeStringValue(content, "vehicleMakeType");
        var sessionId = GetSafeIntValue(content, "sessionId");

        // Parsing date con logica completa
        if (DateTime.TryParse(startDateTime, out var start) && DateTime.TryParse(stopDateTime, out var stop))
        {
            var duration = (stop - start).TotalMinutes;
            var session = new ChargingSessionComplete
            {
                SessionId = sessionId,
                Duration = duration,
                StartTime = start,
                StopTime = stop,
                Site = site!,
                Country = country!,
                BillingType = billingType!,
                VehicleType = vehicleType!
            };

            // Analisi intelligente della sessione come nel vecchio codice
            session.SessionType = duration switch
            {
                < 15 => "Ricarica veloce (top-up)",
                < 60 => "Ricarica breve", 
                < 180 => "Ricarica standard",
                _ => "Ricarica completa"
            };

            // Gestione unlatch
            if (!string.IsNullOrEmpty(unlatch) && unlatch != "N/A" && DateTime.TryParse(unlatch, out var unlatchTime))
            {
                session.DisconnectDelay = (unlatchTime - stop).TotalMinutes;
            }

            aggregation.ChargingSessions.Add(session);

            // Analisi dettagliata delle fees
            if (content.TryGetProperty("fees", out var feesArray) && feesArray.ValueKind == JsonValueKind.Array)
            {
                ProcessChargingFees(feesArray, aggregation, session);
            }

            // Gestione invoices
            if (content.TryGetProperty("invoices", out var invoicesArray) && invoicesArray.ValueKind == JsonValueKind.Array)
            {
                session.InvoiceCount = invoicesArray.GetArrayLength();
                foreach (var invoice in invoicesArray.EnumerateArray())
                {
                    var invoiceType = GetSafeStringValue(invoice, "invoiceType");
                    aggregation.InvoiceTypes[invoiceType!] = aggregation.InvoiceTypes.GetValueOrDefault(invoiceType!) + 1;
                }
            }
        }

        // Aggregazione per paese e sito
        aggregation.ChargingByCountry[country!] = aggregation.ChargingByCountry.GetValueOrDefault(country!) + 1;
        aggregation.ChargingBySite[site!] = aggregation.ChargingBySite.GetValueOrDefault(site!) + 1;
    }

    private void ProcessChargingFees(JsonElement feesArray, CompleteTeslaDataAggregation aggregation, ChargingSessionComplete session)
    {
        var totalCost = 0m;
        var totalEnergy = 0m;
        var currency = "EUR";

        foreach (var fee in feesArray.EnumerateArray())
        {
            var feeType = GetSafeStringValue(fee, "feeType");
            var totalDue = GetSafeDecimalValue(fee, "totalDue");
            var isPaid = GetSafeBooleanValue(fee, "isPaid");
            currency = GetSafeStringValue(fee, "currencyCode") ?? currency;
            var pricingType = GetSafeStringValue(fee, "pricingType");
            var status = GetSafeStringValue(fee, "status");

            if (feeType == "CHARGING" && totalDue > 0)
            {
                var usageBase = GetSafeDecimalValue(fee, "usageBase");
                var usageTier2 = GetSafeDecimalValue(fee, "usageTier2");
                var energy = usageBase + usageTier2;

                totalCost += totalDue;
                totalEnergy += energy;

                // Analisi pricing tiers
                var rateBase = GetSafeDecimalValue(fee, "rateBase");
                if (rateBase > 0)
                {
                    aggregation.PricingTiers.Add(new PricingTierData
                    {
                        Rate = rateBase,
                        Usage = usageBase,
                        Currency = currency!,
                        Type = "Base"
                    });
                }

                var rateTier2 = GetSafeDecimalValue(fee, "rateTier2");
                if (rateTier2 > 0 && usageTier2 > 0)
                {
                    aggregation.PricingTiers.Add(new PricingTierData
                    {
                        Rate = rateTier2,
                        Usage = usageTier2,
                        Currency = currency!,
                        Type = "Tier2"
                    });
                }
            }

            // Statistiche payment status
            var paymentKey = $"{(isPaid ? "Paid" : "Unpaid")}_{status}";
            aggregation.PaymentStatus[paymentKey] = aggregation.PaymentStatus.GetValueOrDefault(paymentKey) + 1;
        }

        if (totalCost > 0 && totalEnergy > 0)
        {
            session.TotalCost = totalCost;
            session.TotalEnergy = totalEnergy;
            session.CostPerKwh = totalCost / totalEnergy;
            session.Currency = currency;

            // Analisi costi come nel vecchio codice
            session.CostAnalysis = session.CostPerKwh switch
            {
                < 0.30m => "Tariffa conveniente",
                < 0.50m => "Tariffa media", 
                < 0.70m => "Tariffa elevata",
                _ => "Tariffa molto cara"
            };

            aggregation.ChargingCosts.Add(totalCost);
            aggregation.EnergyConsumed.Add(totalEnergy);
            aggregation.CostPerKwhValues.Add(session.CostPerKwh);
        }
    }

    private void ProcessVehicleEndpointsComplete(JsonElement content, CompleteTeslaDataAggregation aggregation)
    {
        if (content.ValueKind != JsonValueKind.Object) return;

        // Vehicle Data
        if (content.TryGetProperty("vehicle_data", out var vehicleData) &&
            vehicleData.TryGetProperty("response", out var vdResponse))
        {
            ProcessVehicleDataComplete(vdResponse, aggregation);
        }

        // Vehicle List
        if (content.TryGetProperty("list", out var list) &&
            list.TryGetProperty("response", out var listResponse))
        {
            aggregation.AssociatedVehiclesCount = GetSafeIntValue(list, "count");
            ProcessVehicleList(listResponse, aggregation);
        }

        // Drivers
        if (content.TryGetProperty("drivers", out var drivers) &&
            drivers.TryGetProperty("response", out var driversResponse))
        {
            aggregation.AuthorizedDriversCount = GetSafeIntValue(drivers, "count");
            ProcessDriversList(driversResponse, aggregation);
        }

        // Eligible Subscriptions
        if (content.TryGetProperty("eligible_subscriptions", out var eligibleSubs))
        {
            ProcessEligibleSubscriptions(eligibleSubs, aggregation);
        }

        // Eligible Upgrades  
        if (content.TryGetProperty("eligible_upgrades", out var eligibleUpgrades))
        {
            ProcessEligibleUpgrades(eligibleUpgrades, aggregation);
        }

        // Fleet Status
        if (content.TryGetProperty("fleet_status", out var fleetStatus))
        {
            ProcessFleetStatus(fleetStatus, aggregation);
        }

        // Nearby Charging Sites
        if (content.TryGetProperty("nearby_charging_sites", out var chargingSites))
        {
            ProcessNearbyChargingSites(chargingSites, aggregation);
        }

        // Vehicle Options
        if (content.TryGetProperty("options", out var options))
        {
            ProcessVehicleOptions(options, aggregation);
        }

        // Warranty Details
        if (content.TryGetProperty("warranty_details", out var warranty))
        {
            ProcessWarrantyDetails(warranty, aggregation);
        }

        // Fleet Telemetry Config
        if (content.TryGetProperty("fleet_telemetry_config_get", out var telemetryConfig))
        {
            ProcessFleetTelemetryConfig(telemetryConfig, aggregation);
        }

        // Fleet Telemetry Errors
        if (content.TryGetProperty("fleet_telemetry_errors", out var telemetryErrors))
        {
            ProcessFleetTelemetryErrors(telemetryErrors, aggregation);
        }

        // Recent Alerts
        if (content.TryGetProperty("recent_alerts", out var alerts))
        {
            ProcessRecentAlerts(alerts, aggregation);
        }

        // Service Data
        if (content.TryGetProperty("service_data", out var serviceData))
        {
            ProcessServiceData(serviceData, aggregation);
        }
    }

    private void ProcessVehicleDataComplete(JsonElement vdResponse, CompleteTeslaDataAggregation aggregation)
    {
        // Informazioni base
        var vin = GetSafeStringValue(vdResponse, "vin");
        var state = GetSafeStringValue(vdResponse, "state");
        var accessType = GetSafeStringValue(vdResponse, "access_type");
        var inService = GetSafeBooleanValue(vdResponse, "in_service");
        var apiVersion = GetSafeIntValue(vdResponse, "api_version");

        if (!string.IsNullOrEmpty(vin)) aggregation.VehicleVin = vin;
        if (!string.IsNullOrEmpty(state)) aggregation.VehicleStates[state] = aggregation.VehicleStates.GetValueOrDefault(state) + 1;

        // Charge State - analisi completa come nel vecchio codice
        if (vdResponse.TryGetProperty("charge_state", out var chargeState))
        {
            ProcessChargeStateComplete(chargeState, aggregation);
        }

        // Climate State - analisi completa
        if (vdResponse.TryGetProperty("climate_state", out var climateState))
        {
            ProcessClimateStateComplete(climateState, aggregation);
        }

        // Drive State - con tutti i metodi helper
        if (vdResponse.TryGetProperty("drive_state", out var driveState))
        {
            ProcessDriveStateComplete(driveState, aggregation);
        }

        // Vehicle State - con TPMS dettagliato
        if (vdResponse.TryGetProperty("vehicle_state", out var vehicleState))
        {
            ProcessVehicleStateComplete(vehicleState, aggregation);
        }
    }

    private void ProcessChargeStateComplete(JsonElement chargeState, CompleteTeslaDataAggregation aggregation)
    {
        var batteryLevel = GetSafeIntValue(chargeState, "battery_level");
        var batteryRange = GetSafeDecimalValue(chargeState, "battery_range");
        var chargingState = GetSafeStringValue(chargeState, "charging_state");
        var chargeLimit = GetSafeIntValue(chargeState, "charge_limit_soc");
        var chargeRate = GetSafeDecimalValue(chargeState, "charge_rate");
        var minutesToFull = GetSafeIntValue(chargeState, "minutes_to_full_charge");

        if (batteryLevel > 0)
        {
            aggregation.BatteryLevels.Add(batteryLevel);
            
            // Analisi dello stato batteria come nel vecchio codice
            var batteryAnalysis = batteryLevel switch
            {
                < 20 => "Batteria scarica - ricarica consigliata",
                < 50 => "Livello medio - valutare ricarica",
                < 80 => "Buon livello di carica",
                _ => "Batteria ben carica"
            };
            aggregation.BatteryAnalyses[batteryAnalysis] = aggregation.BatteryAnalyses.GetValueOrDefault(batteryAnalysis) + 1;
        }

        if (batteryRange > 0) aggregation.BatteryRanges.Add(batteryRange);
        if (!string.IsNullOrEmpty(chargingState)) aggregation.ChargingStates[chargingState] = aggregation.ChargingStates.GetValueOrDefault(chargingState) + 1;
        if (chargeLimit > 0) aggregation.ChargeLimits.Add(chargeLimit);
        if (chargeRate > 0) aggregation.ChargeRates.Add(chargeRate);
        if (minutesToFull > 0) aggregation.MinutesToFullReadings.Add(minutesToFull);
    }

    private void ProcessClimateStateComplete(JsonElement climateState, CompleteTeslaDataAggregation aggregation)
    {
        var insideTemp = GetSafeDecimalValue(climateState, "inside_temp");
        var outsideTemp = GetSafeDecimalValue(climateState, "outside_temp");
        var driverTemp = GetSafeDecimalValue(climateState, "driver_temp_setting");
        var passengerTemp = GetSafeDecimalValue(climateState, "passenger_temp_setting");
        var isClimateOn = GetSafeBooleanValue(climateState, "is_climate_on");
        var cabinOverheat = GetSafeStringValue(climateState, "cabin_overheat_protection");

        if (insideTemp != 0) aggregation.InsideTemperatures.Add(insideTemp);
        if (outsideTemp != 0) aggregation.OutsideTemperatures.Add(outsideTemp);
        if (driverTemp != 0) aggregation.DriverTempSettings.Add(driverTemp);
        if (passengerTemp != 0) aggregation.PassengerTempSettings.Add(passengerTemp);
        
        aggregation.ClimateUsage[isClimateOn] = aggregation.ClimateUsage.GetValueOrDefault(isClimateOn) + 1;
        
        if (!string.IsNullOrEmpty(cabinOverheat))
            aggregation.CabinOverheatSettings[cabinOverheat] = aggregation.CabinOverheatSettings.GetValueOrDefault(cabinOverheat) + 1;

        // Analisi intelligente del clima come nel vecchio codice
        if (insideTemp != 0 && outsideTemp != 0)
        {
            var tempDifference = Math.Abs(insideTemp - outsideTemp);
            var climateAnalysis = tempDifference > 10 ?
                "Differenza significativa - sistema climatico probabilmente attivo" :
                "Temperature equilibrate";
            aggregation.ClimateAnalyses[climateAnalysis] = aggregation.ClimateAnalyses.GetValueOrDefault(climateAnalysis) + 1;
        }
    }

    private void ProcessDriveStateComplete(JsonElement driveState, CompleteTeslaDataAggregation aggregation)
    {
        var latitude = GetSafeDecimalValue(driveState, "latitude");
        var longitude = GetSafeDecimalValue(driveState, "longitude");
        var heading = GetSafeIntValue(driveState, "heading");
        var speed = GetSafeIntValue(driveState, "speed");
        var shiftState = GetSafeStringValue(driveState, "shift_state");

        if (speed > 0) aggregation.Speeds.Add(speed);
        if (latitude != 0 && longitude != 0)
        {
            var location = new LocationPointComplete
            {
                Latitude = latitude,
                Longitude = longitude,
                Heading = heading,
                Speed = speed,
                FormattedCoords = FormatCoordinatesItalian(latitude, longitude),
                LocationName = GetItalianLocationName(latitude, longitude),
                CompassDirection = GetCompassDirection(heading)
            };
            aggregation.Locations.Add(location);
        }

        if (!string.IsNullOrEmpty(shiftState))
        {
            var translatedShift = TranslateShiftState(shiftState);
            aggregation.ShiftStates[translatedShift] = aggregation.ShiftStates.GetValueOrDefault(translatedShift) + 1;
        }
    }

    private void ProcessVehicleStateComplete(JsonElement vehicleState, CompleteTeslaDataAggregation aggregation)
    {
        var locked = GetSafeBooleanValue(vehicleState, "locked");
        var sentryMode = GetSafeBooleanValue(vehicleState, "sentry_mode");
        var valetMode = GetSafeBooleanValue(vehicleState, "valet_mode");
        var odometer = GetSafeDecimalValue(vehicleState, "odometer");
        var vehicleName = GetSafeStringValue(vehicleState, "vehicle_name");

        if (!string.IsNullOrEmpty(vehicleName)) aggregation.VehicleName = vehicleName;
        if (odometer > 0) aggregation.OdometerReadings.Add(odometer);

        aggregation.SecurityStates["locked"] = aggregation.SecurityStates.GetValueOrDefault("locked") + (locked ? 1 : 0);
        aggregation.SecurityStates["sentry"] = aggregation.SecurityStates.GetValueOrDefault("sentry") + (sentryMode ? 1 : 0);
        aggregation.SecurityStates["valet"] = aggregation.SecurityStates.GetValueOrDefault("valet") + (valetMode ? 1 : 0);

        // TPMS con analisi avanzata come nel vecchio codice
        var tpmsFL = GetSafeDecimalValue(vehicleState, "tpms_pressure_fl");
        var tpmsFR = GetSafeDecimalValue(vehicleState, "tpms_pressure_fr");
        var tpmsRL = GetSafeDecimalValue(vehicleState, "tpms_pressure_rl");
        var tpmsRR = GetSafeDecimalValue(vehicleState, "tpms_pressure_rr");

        var pressures = new[] { tpmsFL, tpmsFR, tpmsRL, tpmsRR }.Where(p => p > 0).ToArray();
        if (pressures.Any())
        {
            foreach (var pressure in pressures)
            {
                aggregation.TirePressures.Add(pressure);
            }

            // Analisi pressioni come nel vecchio codice
            var avgPressure = pressures.Average();
            var maxDifference = pressures.Max() - pressures.Min();

            var pressureAnalysis = maxDifference > 0.3m ?
                "Differenze significative tra pneumatici" :
                avgPressure < 2.5m ? "Pressioni generalmente basse" :
                avgPressure > 3.5m ? "Pressioni generalmente alte" :
                "Pressioni nella norma";

            aggregation.TirePressureAnalyses[pressureAnalysis] = aggregation.TirePressureAnalyses.GetValueOrDefault(pressureAnalysis) + 1;
        }
    }

    // Continua con tutti gli altri metodi di processamento...
    private void ProcessVehicleList(JsonElement listResponse, CompleteTeslaDataAggregation aggregation)
    {
        foreach (var vehicle in listResponse.EnumerateArray())
        {
            var displayName = GetSafeStringValue(vehicle, "display_name");
            var state = GetSafeStringValue(vehicle, "state");
            var accessType = GetSafeStringValue(vehicle, "access_type");
            
            if (!string.IsNullOrEmpty(state))
                aggregation.AssociatedVehicleStates[state] = aggregation.AssociatedVehicleStates.GetValueOrDefault(state) + 1;
        }
    }

    private void ProcessDriversList(JsonElement driversResponse, CompleteTeslaDataAggregation aggregation)
    {
        foreach (var driver in driversResponse.EnumerateArray())
        {
            var firstName = GetSafeStringValue(driver, "driver_first_name");
            var lastName = GetSafeStringValue(driver, "driver_last_name");
            
            if (!string.IsNullOrEmpty(firstName) && !string.IsNullOrEmpty(lastName))
            {
                aggregation.AuthorizedDrivers.Add($"{firstName} {lastName}");
            }
        }
    }

    private void ProcessEligibleSubscriptions(JsonElement eligibleSubs, CompleteTeslaDataAggregation aggregation)
    {
        if (eligibleSubs.TryGetProperty("response", out var eligibleResponse) &&
            eligibleResponse.TryGetProperty("eligible", out var eligible))
        {
            foreach (var subscription in eligible.EnumerateArray())
            {
                var product = GetSafeStringValue(subscription, "product");
                if (!string.IsNullOrEmpty(product))
                {
                    aggregation.EligibleSubscriptions.Add(product);
                }
            }
        }
    }

    private void ProcessEligibleUpgrades(JsonElement eligibleUpgrades, CompleteTeslaDataAggregation aggregation)
    {
        if (eligibleUpgrades.TryGetProperty("response", out var upgradesResponse) &&
            upgradesResponse.TryGetProperty("eligible", out var upgrades))
        {
            foreach (var upgrade in upgrades.EnumerateArray())
            {
                var optionCode = GetSafeStringValue(upgrade, "optionCode");
                var optionGroup = GetSafeStringValue(upgrade, "optionGroup");
                
                if (!string.IsNullOrEmpty(optionGroup))
                {
                    aggregation.EligibleUpgrades[optionGroup] = aggregation.EligibleUpgrades.GetValueOrDefault(optionGroup) + 1;
                }
            }
        }
    }

    private void ProcessFleetStatus(JsonElement fleetStatus, CompleteTeslaDataAggregation aggregation)
    {
        if (fleetStatus.TryGetProperty("response", out var fleetResponse))
        {
            aggregation.KeyPairedVehicles = fleetResponse.TryGetProperty("key_paired_vins", out var kpv) ? kpv.GetArrayLength() : 0;
            aggregation.UnpairedVehicles = fleetResponse.TryGetProperty("unpaired_vins", out var uv) ? uv.GetArrayLength() : 0;

            if (fleetResponse.TryGetProperty("vehicle_info", out var vehicleInfo))
            {
                foreach (var prop in vehicleInfo.EnumerateObject())
                {
                    var info = prop.Value;
                    var firmware = GetSafeStringValue(info, "firmware_version");
                    var telemetryVersion = GetSafeStringValue(info, "fleet_telemetry_version");
                    
                    if (!string.IsNullOrEmpty(firmware))
                        aggregation.FirmwareVersions[firmware] = aggregation.FirmwareVersions.GetValueOrDefault(firmware) + 1;
                }
            }
        }
    }

    private void ProcessNearbyChargingSites(JsonElement chargingSites, CompleteTeslaDataAggregation aggregation)
    {
        if (chargingSites.TryGetProperty("response", out var sitesResponse))
        {
            if (sitesResponse.TryGetProperty("superchargers", out var superchargers))
            {
                foreach (var sc in superchargers.EnumerateArray())
                {
                    var name = GetSafeStringValue(sc, "name");
                    var distance = GetSafeDecimalValue(sc, "distance_miles") * 1.60934m; // Convert to km
                    var available = GetSafeIntValue(sc, "available_stalls");
                    var total = GetSafeIntValue(sc, "total_stalls");
                    
                    aggregation.NearbyChargers.Add(new NearbyChargerInfo
                    {
                        Name = name!,
                        DistanceKm = distance,
                        AvailableStalls = available,
                        TotalStalls = total,
                        Type = "Supercharger"
                    });
                }
            }

            if (sitesResponse.TryGetProperty("destination_charging", out var destinations))
            {
                foreach (var dest in destinations.EnumerateArray())
                {
                    var name = GetSafeStringValue(dest, "name");
                    var distance = GetSafeDecimalValue(dest, "distance_miles") * 1.60934m;
                    
                    aggregation.NearbyChargers.Add(new NearbyChargerInfo
                    {
                        Name = name!,
                        DistanceKm = distance,
                        Type = "Destination"
                    });
                }
            }
        }
    }

    private void ProcessVehicleOptions(JsonElement options, CompleteTeslaDataAggregation aggregation)
    {
        if (options.TryGetProperty("response", out var optionsResponse) &&
            optionsResponse.TryGetProperty("codes", out var codes))
        {
            foreach (var option in codes.EnumerateArray())
            {
                var code = GetSafeStringValue(option, "code");
                var displayName = GetSafeStringValue(option, "displayName");
                var isActive = GetSafeBooleanValue(option, "isActive");
                
                if (!string.IsNullOrEmpty(displayName))
                {
                    aggregation.VehicleOptions.Add(new VehicleOptionInfo
                    {
                        Code = code!,
                        DisplayName = displayName,
                        IsActive = isActive
                    });
                }
            }
        }
    }

    private void ProcessWarrantyDetails(JsonElement warranty, CompleteTeslaDataAggregation aggregation)
    {
        if (warranty.TryGetProperty("response", out var warrantyResponse) &&
            warrantyResponse.TryGetProperty("activeWarranty", out var activeWarranties))
        {
            foreach (var w in activeWarranties.EnumerateArray())
            {
                var displayName = GetSafeStringValue(w, "warrantyDisplayName", "[Garanzia sconosciuta]");
                var expirationDate = GetSafeStringValue(w, "expirationDate");
                var expirationOdometer = GetSafeIntValue(w, "expirationOdometer");
                var odometerUnit = GetSafeStringValue(w, "odometerUnit");

                aggregation.ActiveWarranties.Add(new WarrantyInfo
                {
                    DisplayName = displayName!,
                    ExpirationDate = expirationDate!,
                    ExpirationOdometer = expirationOdometer,
                    OdometerUnit = odometerUnit!
                });
            }
        }
    }

    private void ProcessFleetTelemetryConfig(JsonElement telemetryConfig, CompleteTeslaDataAggregation aggregation)
    {
        if (telemetryConfig.TryGetProperty("response", out var ftConfigResponse))
        {
            aggregation.FleetTelemetryStatus = new FleetTelemetryStatus
            {
                Synced = GetSafeBooleanValue(ftConfigResponse, "synced"),
                LimitReached = GetSafeBooleanValue(ftConfigResponse, "limit_reached"),
                KeyPaired = GetSafeBooleanValue(ftConfigResponse, "key_paired")
            };

            if (ftConfigResponse.TryGetProperty("config", out var config))
            {
                var hostname = GetSafeStringValue(config, "hostname");
                var port = GetSafeIntValue(config, "port");
                var deliveryPolicy = GetSafeStringValue(config, "delivery_policy");

                aggregation.FleetTelemetryStatus.ServerInfo = $"{hostname}:{port}";
                aggregation.FleetTelemetryStatus.DeliveryPolicy = deliveryPolicy!;

                if (config.TryGetProperty("fields", out var fields))
                {
                    aggregation.FleetTelemetryStatus.ConfiguredFieldsCount = fields.EnumerateObject().Count();
                }
            }
        }
    }

    private void ProcessFleetTelemetryErrors(JsonElement telemetryErrors, CompleteTeslaDataAggregation aggregation)
    {
        if (telemetryErrors.TryGetProperty("response", out var ftErrorsResponse) &&
            ftErrorsResponse.TryGetProperty("fleet_telemetry_errors", out var errors) &&
            errors.ValueKind == JsonValueKind.Array)
        {
            foreach (var error in errors.EnumerateArray())
            {
                var name = GetSafeStringValue(error, "name");
                var errorMsg = GetSafeStringValue(error, "error");
                var vin = GetSafeStringValue(error, "vin");

                var errorCategory = CategorizeFleetTelemetryError(errorMsg!);
                aggregation.TelemetryErrorsByCategory[errorCategory] = aggregation.TelemetryErrorsByCategory.GetValueOrDefault(errorCategory) + 1;
                aggregation.TelemetryErrorsByVin[vin!] = aggregation.TelemetryErrorsByVin.GetValueOrDefault(vin!) + 1;

                aggregation.FleetTelemetryErrors.Add(new FleetTelemetryError
                {
                    ClientName = name!,
                    ErrorMessage = errorMsg!,
                    Vin = vin!,
                    Category = errorCategory,
                    Severity = GetErrorSeverity(errorCategory),
                    Solution = GetErrorSolution(errorMsg!)
                });
            }
        }
    }

    private void ProcessRecentAlerts(JsonElement alerts, CompleteTeslaDataAggregation aggregation)
    {
        if (alerts.TryGetProperty("response", out var alertsResponse) &&
            alertsResponse.TryGetProperty("recent_alerts", out var recentAlerts))
        {
            foreach (var alert in recentAlerts.EnumerateArray())
            {
                var name = GetSafeStringValue(alert, "name");
                var time = GetSafeStringValue(alert, "time");
                var userText = GetSafeStringValue(alert, "user_text");

                aggregation.RecentAlerts.Add(new AlertInfo
                {
                    Name = name!,
                    Time = time!,
                    UserText = userText!
                });

                // Categorizza gli alert
                aggregation.AlertsByType[name!] = aggregation.AlertsByType.GetValueOrDefault(name!) + 1;
            }
        }
    }

    private void ProcessServiceData(JsonElement serviceData, CompleteTeslaDataAggregation aggregation)
    {
        if (serviceData.TryGetProperty("response", out var serviceResponse))
        {
            var serviceStatus = GetSafeStringValue(serviceResponse, "service_status");
            var serviceEtc = GetSafeStringValue(serviceResponse, "service_etc");
            var visitNumber = GetSafeStringValue(serviceResponse, "service_visit_number");

            aggregation.ServiceStatus = new ServiceStatusInfo
            {
                Status = serviceStatus!,
                EstimatedCompletion = serviceEtc!,
                VisitNumber = visitNumber!
            };
        }
    }

    private void ProcessVehicleCommandsComplete(JsonElement content, CompleteTeslaDataAggregation aggregation)
    {
        if (content.ValueKind != JsonValueKind.Array) return;

        var commands = content.EnumerateArray().ToList();
        
        foreach (var command in commands)
        {
            var commandName = GetSafeStringValue(command, "command");
            var timestamp = GetSafeStringValue(command, "timestamp");
            var success = command.TryGetProperty("response", out var resp) && GetSafeBooleanValue(resp, "result");
            var reason = command.TryGetProperty("response", out var respReason) ? GetSafeStringValue(respReason, "reason") : "";
            var queued = command.TryGetProperty("response", out var respQueued) && GetSafeBooleanValue(respQueued, "queued");

            if (!string.IsNullOrEmpty(commandName))
            {
                var category = GetCommandCategoryComplete(commandName);
                var displayName = GetCommandDisplayName(commandName);
                
                aggregation.CommandCategories[category] = aggregation.CommandCategories.GetValueOrDefault(category) + 1;
                aggregation.CommandSuccess[success] = aggregation.CommandSuccess.GetValueOrDefault(success) + 1;
                aggregation.CommandTypes[displayName] = aggregation.CommandTypes.GetValueOrDefault(displayName) + 1;

                if (!string.IsNullOrEmpty(timestamp) && DateTime.TryParse(timestamp, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var cmdTime))
                {
                    aggregation.CommandTimestamps.Add(cmdTime);
                    
                    // Analisi orari comandi
                    var hourKey = $"{cmdTime.Hour:00}:xx";
                    aggregation.CommandsByHour[hourKey] = aggregation.CommandsByHour.GetValueOrDefault(hourKey) + 1;
                }

                // Analisi del comando con parametri
                if (command.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Object)
                {
                    var paramDetails = FormatCommandParameters(commandName, parameters);
                    if (!string.IsNullOrEmpty(paramDetails))
                    {
                        aggregation.CommandsWithParameters.Add(new CommandWithParameters
                        {
                            CommandName = displayName,
                            Parameters = paramDetails,
                            Success = success,
                            Timestamp = timestamp!
                        });
                    }
                }

                // Analisi fallimenti
                if (!success && !string.IsNullOrEmpty(reason))
                {
                    aggregation.CommandFailureReasons[reason] = aggregation.CommandFailureReasons.GetValueOrDefault(reason) + 1;
                }

                // Analisi specifica per tipo di comando
                var analysis = GetCommandAnalysis(commandName, success, parameters);
                if (!string.IsNullOrEmpty(analysis))
                {
                    aggregation.CommandAnalyses[analysis] = aggregation.CommandAnalyses.GetValueOrDefault(analysis) + 1;
                }
            }
        }
    }

    private void ProcessEnergyEndpointsComplete(JsonElement content, CompleteTeslaDataAggregation aggregation)
    {
        if (content.ValueKind != JsonValueKind.Object) return;

        // Live Status
        if (content.TryGetProperty("live_status", out var liveStatus) &&
            liveStatus.TryGetProperty("response", out var liveResponse))
        {
            ProcessLiveStatusComplete(liveResponse, aggregation);
        }

        // Site Info
        if (content.TryGetProperty("site_info", out var siteInfo) &&
            siteInfo.TryGetProperty("response", out var siteResponse))
        {
            ProcessSiteInfoComplete(siteResponse, aggregation);
        }

        // Energy History
        if (content.TryGetProperty("energy_history", out var energyHistory) &&
            energyHistory.TryGetProperty("response", out var energyResponse))
        {
            ProcessEnergyHistoryComplete(energyResponse, aggregation);
        }

        // Charge History
        if (content.TryGetProperty("charge_history", out var chargeHistory))
        {
            ProcessVehicleChargeHistoryComplete(chargeHistory, aggregation);
        }

        // Backup History
        if (content.TryGetProperty("backup_history", out var backupHistory))
        {
            ProcessBackupHistoryComplete(backupHistory, aggregation);
        }

        // Products
        if (content.TryGetProperty("products", out var products))
        {
            ProcessEnergyProductsComplete(products, aggregation);
        }
    }

    private void ProcessLiveStatusComplete(JsonElement liveResponse, CompleteTeslaDataAggregation aggregation)
    {
        var solarPower = GetSafeIntValue(liveResponse, "solar_power");
        var energyLeft = GetSafeDecimalValue(liveResponse, "energy_left");
        var totalPackEnergy = GetSafeIntValue(liveResponse, "total_pack_energy");
        var percentageCharged = GetSafeDecimalValue(liveResponse, "percentage_charged");
        var batteryPower = GetSafeIntValue(liveResponse, "battery_power");
        var loadPower = GetSafeIntValue(liveResponse, "load_power");
        var gridPower = GetSafeIntValue(liveResponse, "grid_power");
        var gridStatus = GetSafeStringValue(liveResponse, "grid_status");
        var stormModeActive = GetSafeBooleanValue(liveResponse, "storm_mode_active");
        var backupCapable = GetSafeBooleanValue(liveResponse, "backup_capable");

        aggregation.EnergySolar.Add(solarPower);
        aggregation.EnergyBattery.Add(batteryPower);
        aggregation.EnergyGrid.Add(gridPower);
        aggregation.EnergyLoad.Add(loadPower);

        if (energyLeft > 0 && totalPackEnergy > 0)
        {
            aggregation.EnergySystemBatteryLevel = percentageCharged;
            aggregation.EnergySystemCapacity = totalPackEnergy;
        }

        // Analisi intelligente del sistema energetico
        var batteryStatus = batteryPower > 0 ? "Scarica" : "Ricarica";
        var solarStatus = solarPower switch
        {
            0 => "Nessuna produzione (notte)",
            < 1000 => "Produzione bassa",
            < 3000 => "Produzione media",
            _ => "Produzione alta"
        };

        aggregation.EnergySystemStatus[batteryStatus] = aggregation.EnergySystemStatus.GetValueOrDefault(batteryStatus) + 1;
        aggregation.SolarProductionStatus[solarStatus] = aggregation.SolarProductionStatus.GetValueOrDefault(solarStatus) + 1;
        
        if (!string.IsNullOrEmpty(gridStatus))
            aggregation.GridConnectionStatus[gridStatus] = aggregation.GridConnectionStatus.GetValueOrDefault(gridStatus) + 1;

        aggregation.StormModeActivations += stormModeActive ? 1 : 0;
        aggregation.BackupCapableReadings += backupCapable ? 1 : 0;
    }

    private void ProcessSiteInfoComplete(JsonElement siteResponse, CompleteTeslaDataAggregation aggregation)
    {
        var siteName = GetSafeStringValue(siteResponse, "site_name");
        var backupReserve = GetSafeIntValue(siteResponse, "backup_reserve_percent");
        var realMode = GetSafeStringValue(siteResponse, "default_real_mode");
        var installationDate = GetSafeStringValue(siteResponse, "installation_date");
        var batteryCount = GetSafeIntValue(siteResponse, "battery_count");
        var nameplatePower = GetSafeIntValue(siteResponse, "nameplate_power");
        var nameplateEnergy = GetSafeIntValue(siteResponse, "nameplate_energy");
        var version = GetSafeStringValue(siteResponse, "version");

        aggregation.EnergySiteInfo = new EnergySiteInfo
        {
            SiteName = siteName!,
            BackupReservePercent = backupReserve,
            RealMode = realMode!,
            InstallationDate = installationDate!,
            BatteryCount = batteryCount,
            NameplatePower = nameplatePower,
            NameplateEnergy = nameplateEnergy,
            Version = version!
        };

        if (siteResponse.TryGetProperty("components", out var components))
        {
            var hasSolar = GetSafeBooleanValue(components, "solar");
            var hasBattery = GetSafeBooleanValue(components, "battery");
            var hasGrid = GetSafeBooleanValue(components, "grid");

            aggregation.EnergySiteInfo.HasSolar = hasSolar;
            aggregation.EnergySiteInfo.HasBattery = hasBattery;
            aggregation.EnergySiteInfo.HasGrid = hasGrid;
        }
    }

    private void ProcessEnergyHistoryComplete(JsonElement energyResponse, CompleteTeslaDataAggregation aggregation)
    {
        var period = GetSafeStringValue(energyResponse, "period");
        
        if (energyResponse.TryGetProperty("time_series", out var timeSeries))
        {
            foreach (var entry in timeSeries.EnumerateArray())
            {
                var timestamp = GetSafeStringValue(entry, "timestamp");
                var solarExported = GetSafeIntValue(entry, "solar_energy_exported");
                var gridImported = GetSafeIntValue(entry, "grid_energy_imported");
                var gridExported = GetSafeIntValue(entry, "grid_energy_exported_from_solar");
                var batteryExported = GetSafeIntValue(entry, "battery_energy_exported");
                var batteryImportedSolar = GetSafeIntValue(entry, "battery_energy_imported_from_solar");
                var consumerFromGrid = GetSafeIntValue(entry, "consumer_energy_imported_from_grid");
                var consumerFromSolar = GetSafeIntValue(entry, "consumer_energy_imported_from_solar");
                var consumerFromBattery = GetSafeIntValue(entry, "consumer_energy_imported_from_battery");

                var consumerTotal = consumerFromGrid + consumerFromSolar + consumerFromBattery;

                aggregation.EnergyHistoryEntries.Add(new EnergyHistoryEntry
                {
                    Timestamp = timestamp!,
                    SolarExported = solarExported,
                    GridImported = gridImported,
                    GridExported = gridExported,
                    BatteryExported = batteryExported,
                    ConsumerTotal = consumerTotal
                });

                // Analisi bilancio energetico come nel vecchio codice
                var energyBalance = AnalyzeEnergyBalance(solarExported, gridImported, batteryExported, consumerTotal);
                aggregation.EnergyBalanceAnalyses[energyBalance] = aggregation.EnergyBalanceAnalyses.GetValueOrDefault(energyBalance) + 1;
            }
        }
    }

    private void ProcessVehicleChargeHistoryComplete(JsonElement chargeHistory, CompleteTeslaDataAggregation aggregation)
    {
        if (chargeHistory.TryGetProperty("response", out var chargeResponse) &&
            chargeResponse.TryGetProperty("charge_history", out var chargeArray))
        {
            foreach (var charge in chargeArray.EnumerateArray())
            {
                var startTimeSeconds = charge.GetProperty("charge_start_time").GetProperty("seconds").GetInt64();
                var duration = charge.GetProperty("charge_duration").GetProperty("seconds").GetInt32();
                var energyAdded = charge.GetProperty("energy_added_wh").GetInt32();

                var startTime = DateTimeOffset.FromUnixTimeSeconds(startTimeSeconds);
                var durationHours = duration / 3600.0;

                aggregation.VehicleChargeHistory.Add(new VehicleChargeEntry
                {
                    StartTime = startTime.ToString("yyyy-MM-dd HH:mm"),
                    DurationHours = durationHours,
                    EnergyAdded = energyAdded
                });
            }
        }
    }

    private void ProcessBackupHistoryComplete(JsonElement backupHistory, CompleteTeslaDataAggregation aggregation)
    {
        if (backupHistory.TryGetProperty("response", out var backupResponse))
        {
            var totalEvents = GetSafeIntValue(backupResponse, "total_events");
            aggregation.BackupEventsTotal = totalEvents;

            if (backupResponse.TryGetProperty("events", out var events))
            {
                foreach (var evt in events.EnumerateArray())
                {
                    var timestamp = GetSafeStringValue(evt, "timestamp");
                    var duration = GetSafeIntValue(evt, "duration");
                    var durationHours = duration / 3600.0;

                    aggregation.BackupEvents.Add(new BackupEvent
                    {
                        Timestamp = timestamp!,
                        DurationHours = durationHours
                    });
                }
            }
        }
    }

    private void ProcessEnergyProductsComplete(JsonElement products, CompleteTeslaDataAggregation aggregation)
    {
        if (products.TryGetProperty("response", out var productArray))
        {
            var count = GetSafeIntValue(products, "count");
            aggregation.EnergyProductsCount = count;

            foreach (var product in productArray.EnumerateArray())
            {
                if (product.TryGetProperty("device_type", out var deviceType))
                {
                    var deviceTypeStr = GetSafeStringValue(deviceType, "");
                    aggregation.EnergyProductTypes[deviceTypeStr!] = aggregation.EnergyProductTypes.GetValueOrDefault(deviceTypeStr!) + 1;

                    if (deviceTypeStr == "vehicle")
                    {
                        var displayName = GetSafeStringValue(product, "display_name");
                        var state = GetSafeStringValue(product, "state");
                        
                        if (!string.IsNullOrEmpty(state))
                            aggregation.ConnectedVehicleStates[state] = aggregation.ConnectedVehicleStates.GetValueOrDefault(state) + 1;
                    }
                    else if (deviceTypeStr == "energy")
                    {
                        var siteName = GetSafeStringValue(product, "site_name");
                        var energyLeft = GetSafeIntValue(product, "energy_left");
                        var totalEnergy = GetSafeIntValue(product, "total_pack_energy");
                        var percentage = GetSafeIntValue(product, "percentage_charged");

                        if (totalEnergy > 0)
                        {
                            aggregation.ConnectedEnergySystemInfo = new ConnectedEnergySystemInfo
                            {
                                SiteName = siteName!,
                                EnergyLeft = energyLeft,
                                TotalEnergy = totalEnergy,
                                PercentageCharged = percentage
                            };
                        }
                    }
                }
            }
        }
    }

    private void ProcessPartnerPublicKeyComplete(JsonElement content, CompleteTeslaDataAggregation aggregation)
    {
        if (content.ValueKind != JsonValueKind.Object) return;

        // Public Key con analisi sicurezza
        var publicKey = GetSafeStringValue(content, "public_key");
        if (publicKey != "N/A" && !string.IsNullOrEmpty(publicKey))
        {
            var keyLength = publicKey.Length;
            var keyStrength = keyLength switch
            {
                >= 128 => "Chiave forte",
                >= 64 => "Chiave media", 
                _ => "Chiave debole"
            };

            aggregation.PublicKeyInfo = new PublicKeyInfo
            {
                KeyLength = keyLength,
                KeyStrength = keyStrength,
                KeyPreview = keyLength > 20 ? $"{publicKey[..20]}...{publicKey[^10..]}" : publicKey
            };
        }

        // Fleet Telemetry Error VINs
        if (content.TryGetProperty("fleet_telemetry_error_vins", out var errorVins) &&
            errorVins.ValueKind == JsonValueKind.Array)
        {
            var vinCount = errorVins.GetArrayLength();
            aggregation.FleetTelemetryErrorVinsCount = vinCount;
            
            var errorLevel = vinCount switch
            {
                0 => "Nessun errore",
                1 => "Errore singolo",
                _ => "Errori multipli"  
            };
            aggregation.FleetTelemetryErrorLevel = errorLevel;
        }
    }

    private void ProcessUserProfileComplete(JsonElement content, CompleteTeslaDataAggregation aggregation)
    {
        if (content.ValueKind != JsonValueKind.Object) return;

        // User Information
        if (content.TryGetProperty("me", out var me) &&
            me.TryGetProperty("response", out var meResponse))
        {
            var email = GetSafeStringValue(meResponse, "email");
            var fullName = GetSafeStringValue(meResponse, "full_name");
            var vaultUuid = GetSafeStringValue(meResponse, "vault_uuid");

            aggregation.UserProfile = new UserProfileInfo
            {
                Email = email!,
                FullName = fullName!,
                VaultUuid = vaultUuid!
            };
        }

        // Region Information  
        if (content.TryGetProperty("region", out var region) &&
            region.TryGetProperty("response", out var regionResponse))
        {
            var regionCode = GetSafeStringValue(regionResponse, "region");
            var fleetApiUrl = GetSafeStringValue(regionResponse, "fleet_api_base_url");

            aggregation.RegionInfo = new RegionInfo
            {
                RegionCode = regionCode!,
                FleetApiUrl = fleetApiUrl!
            };
        }

        // Feature Configuration
        if (content.TryGetProperty("feature_config", out var featureConfig) &&
            featureConfig.TryGetProperty("response", out var featureResponse))
        {
            if (featureResponse.TryGetProperty("signaling", out var signaling))
            {
                var enabled = GetSafeBooleanValue(signaling, "enabled");
                var subscribeConnectivity = GetSafeBooleanValue(signaling, "subscribe_connectivity");
                var useAuthToken = GetSafeBooleanValue(signaling, "use_auth_token");

                aggregation.FeatureConfig = new FeatureConfigInfo
                {
                    SignalingEnabled = enabled,
                    SubscribeConnectivity = subscribeConnectivity,
                    UseAuthToken = useAuthToken
                };
            }
        }

        // Orders con analisi avanzata come nel vecchio codice
        if (content.TryGetProperty("orders", out var orders))
        {
            ProcessOrdersComplete(orders, aggregation);
        }
    }

    private void ProcessOrdersComplete(JsonElement orders, CompleteTeslaDataAggregation aggregation)
    {
        var count = GetSafeIntValue(orders, "count");
        aggregation.OrdersCount = count;

        if (orders.TryGetProperty("response", out var ordersArray) &&
            ordersArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var order in ordersArray.EnumerateArray())
            {
                var orderStatus = GetSafeStringValue(order, "orderStatus");
                var orderSubstatus = GetSafeStringValue(order, "orderSubstatus");
                var modelCode = GetSafeStringValue(order, "modelCode");
                var countryCode = GetSafeStringValue(order, "countryCode");
                var isB2b = GetSafeBooleanValue(order, "isB2b");
                var mktOptions = GetSafeStringValue(order, "mktOptions");

                // Analisi stato ordine
                var statusAnalysis = orderStatus switch
                {
                    "BOOKED" => "Ordinato",
                    "CONFIRMED" => "Confermato",
                    "IN_PRODUCTION" => "In produzione",
                    "READY_FOR_DELIVERY" => "Pronto per consegna",
                    "DELIVERED" => "Consegnato",
                    _ => orderStatus
                };

                // Analisi modello
                var modelName = modelCode?.ToUpper() switch
                {
                    "M3" => "Model 3",
                    "MY" => "Model Y", 
                    "MS" => "Model S",
                    "MX" => "Model X",
                    _ => modelCode
                };

                aggregation.Orders.Add(new OrderInfo
                {
                    Status = statusAnalysis!,
                    ModelName = modelName!,
                    CountryCode = countryCode!,
                    IsB2B = isB2b,
                    OptionsCount = !string.IsNullOrEmpty(mktOptions) ? mktOptions.Split(',').Length : 0
                });

                // Statistiche aggregate
                if (!string.IsNullOrEmpty(statusAnalysis))
                    aggregation.OrdersByStatus[statusAnalysis] = aggregation.OrdersByStatus.GetValueOrDefault(statusAnalysis) + 1;
                
                if (!string.IsNullOrEmpty(modelName))
                    aggregation.OrdersByModel[modelName] = aggregation.OrdersByModel.GetValueOrDefault(modelName) + 1;

                aggregation.OrdersByType[isB2b ? "Business" : "Personal"] = aggregation.OrdersByType.GetValueOrDefault(isB2b ? "Business" : "Personal") + 1;
            }
        }
    }

    #endregion

    #region ADAPTIVE PROFILING SMS (come nel vecchio codice)

    private async Task AddAdaptiveProfilingDataComplete(int vehicleId, CompleteTeslaDataAggregation aggregation)
    {
        try
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var adaptiveSessions = await _dbContext.AdaptiveProfilingSmsEvents
                .Where(e => e.VehicleId == vehicleId && e.ReceivedAt >= thirtyDaysAgo)
                .OrderByDescending(e => e.ReceivedAt)
                .ToListAsync();

            aggregation.AdaptiveSessionsCount = adaptiveSessions.Count(s => s.ParsedCommand == "ADAPTIVE_PROFILING_ON");
            var offSessions = adaptiveSessions.Count(s => s.ParsedCommand == "ADAPTIVE_PROFILING_OFF");
            aggregation.AdaptiveSessionsStoppedManually = offSessions;
            aggregation.AdaptiveSessionsStoppedAutomatically = Math.Max(0, aggregation.AdaptiveSessionsCount - offSessions);
            
            aggregation.HasActiveAdaptiveSession = await GetActiveAdaptiveSession(vehicleId) != null;

            if (adaptiveSessions.Any())
            {
                // Analisi pattern orari
                var sessionsByHour = adaptiveSessions
                    .Where(s => s.ParsedCommand == "ADAPTIVE_PROFILING_ON")
                    .GroupBy(s => s.ReceivedAt.Hour)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();
                    
                if (sessionsByHour != null)
                {
                    aggregation.MostActiveAdaptiveHour = sessionsByHour.Key;
                }

                // Analisi giorni della settimana
                var sessionsByDay = adaptiveSessions
                    .Where(s => s.ParsedCommand == "ADAPTIVE_PROFILING_ON")
                    .GroupBy(s => s.ReceivedAt.DayOfWeek)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .ToList();

                foreach (var dayGroup in sessionsByDay)
                {
                    var dayName = TranslateDayOfWeek(dayGroup.Key);
                    aggregation.AdaptiveSessionsByDay[dayName] = dayGroup.Count();
                }

                // Frequenza di utilizzo
                var totalDays = Math.Max(1, (DateTime.UtcNow - adaptiveSessions.Min(e => e.ReceivedAt)).TotalDays);
                var frequency = aggregation.AdaptiveSessionsCount / totalDays;

                aggregation.AdaptiveFrequencyAnalysis = frequency switch
                {
                    >= 1 => "Uso quotidiano",
                    >= 0.5 => "Uso frequente",
                    >= 0.2 => "Uso regolare",
                    _ => "Uso occasionale"
                };

                aggregation.AdaptiveFrequencyValue = frequency;
            }

            // Conteggio dati raccolti durante sessioni adaptive
            aggregation.AdaptiveDataRecordsCount = await _dbContext.VehiclesData
                .Where(d => d.VehicleId == vehicleId && d.IsAdaptiveProfiling)
                .CountAsync();

        }
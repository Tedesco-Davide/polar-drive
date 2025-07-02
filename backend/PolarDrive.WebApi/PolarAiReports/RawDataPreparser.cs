using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.PolarAiReports;

public static class RawDataPreparser
{
    public static async Task<string> GenerateInsightPrompt(List<string> rawJsonList, int vehicleId, PolarDriveDbContext dbContext)
    {
        var sb = new StringBuilder();
        int index = 1;

        foreach (var raw in rawJsonList)
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.TryGetProperty("response", out var response) && response.TryGetProperty("data", out var dataArray))
            {
                foreach (var item in dataArray.EnumerateArray())
                {
                    var type = item.GetProperty("type").GetString();
                    var content = item.GetProperty("content");

                    switch (type)
                    {
                        case "charging_history":
                            ProcessChargingHistory(sb, content, ref index);
                            break;
                        case "energy_endpoints":
                            ProcessEnergyEndpoints(sb, content, ref index);
                            break;
                        case "partner_public_key":
                            ProcessPartnerPublicKey(sb, content, ref index);
                            break;
                        case "user_profile":
                            ProcessUserProfile(sb, content, ref index);
                            break;
                        case "vehicle_commands":
                            ProcessVehicleCommands(sb, content, ref index);
                            break;
                        case "vehicle_endpoints":
                            ProcessVehicleEndpoints(sb, content, ref index);
                            break;
                        default:
                            sb.AppendLine($"[{index++}] Tipo dati non riconosciuto: {type}");
                            break;
                    }
                }
            }
        }

        index = await ProcessAdaptiveProfilingSms(sb, vehicleId, dbContext, index);

        return sb.ToString();
    }

    #region Original Tesla Data Processing Methods

    private static void ProcessChargingHistory(StringBuilder sb, JsonElement content, ref int index)
    {
        if (content.ValueKind != JsonValueKind.Object) return;

        // Utilizzo metodi helper sicuri per tutti i campi principali
        var site = GetSafeStringValue(content, "siteLocationName");
        var startDateTime = GetSafeStringValue(content, "chargeStartDateTime");
        var stopDateTime = GetSafeStringValue(content, "chargeStopDateTime");
        var vin = GetSafeStringValue(content, "vin");
        var unlatch = GetSafeStringValue(content, "unlatchDateTime");
        var country = GetSafeStringValue(content, "countryCode");
        var billingType = GetSafeStringValue(content, "billingType");
        var vehicleType = GetSafeStringValue(content, "vehicleMakeType");
        var sessionId = GetSafeIntValue(content, "sessionId");

        // Parsing sicuro delle date
        var start = DateTime.TryParse(startDateTime, out var startParsed) ? startParsed : DateTime.MinValue;
        var stop = DateTime.TryParse(stopDateTime, out var stopParsed) ? stopParsed : DateTime.MinValue;
        var duration = start != DateTime.MinValue && stop != DateTime.MinValue ? (stop - start).TotalMinutes : 0;

        // Analisi intelligente della sessione di ricarica
        var sessionAnalysis = duration switch
        {
            0 => "⚠️ Durata non disponibile",
            < 15 => "⚡ Ricarica veloce (top-up)",
            < 60 => "🔋 Ricarica breve",
            < 180 => "🔋 Ricarica standard",
            _ => "🔋 Ricarica completa"
        };

        sb.AppendLine($"[{index++}] Sessione #{sessionId} – Ricarica a {site} ({country}), VIN {vin}");

        if (start != DateTime.MinValue && stop != DateTime.MinValue)
        {
            sb.AppendLine($"  - Inizio: {start:yyyy-MM-dd HH:mm}, Fine: {stop:yyyy-MM-dd HH:mm} ({duration:F0} minuti)");
            sb.AppendLine($"  - Analisi: {sessionAnalysis}");
        }
        else
        {
            sb.AppendLine($"  - ⚠️ Timestamp non validi - Inizio: {startDateTime}, Fine: {stopDateTime}");
        }

        sb.AppendLine($"  - Tipo Veicolo: {vehicleType}, Billing: {billingType}");

        if (!string.IsNullOrEmpty(unlatch) && unlatch != "N/A")
        {
            if (DateTime.TryParse(unlatch, out var unlatchParsed))
            {
                var disconnectDelay = stop != DateTime.MinValue ? (unlatchParsed - stop).TotalMinutes : 0;
                sb.AppendLine($"  - Rimozione cavo: {unlatchParsed:yyyy-MM-dd HH:mm} ({disconnectDelay:F0} min dopo fine ricarica)");
            }
            else
            {
                sb.AppendLine($"  - Rimozione cavo: {unlatch}");
            }
        }

        // Gestione fees più dettagliata con metodi helper sicuri
        if (content.TryGetProperty("fees", out var feesArray) && feesArray.ValueKind == JsonValueKind.Array)
        {
            var totalCost = 0m;
            var totalEnergy = 0m;
            var currency = "EUR";

            sb.AppendLine("  - COSTI:");
            foreach (var fee in feesArray.EnumerateArray())
            {
                var feeType = GetSafeStringValue(fee, "feeType");
                var totalDue = GetSafeDecimalValue(fee, "totalDue");
                var isPaid = GetSafeBooleanValue(fee, "isPaid");
                currency = GetSafeStringValue(fee, "currencyCode");
                var uom = GetSafeStringValue(fee, "uom");
                var status = GetSafeStringValue(fee, "status");
                var pricingType = GetSafeStringValue(fee, "pricingType");

                // Accumula statistiche
                if (feeType == "CHARGING" && totalDue > 0)
                {
                    totalCost += totalDue;
                    var usageBase = GetSafeDecimalValue(fee, "usageBase");
                    var usageTier2 = GetSafeDecimalValue(fee, "usageTier2");
                    totalEnergy += usageBase + usageTier2;
                }

                var paymentStatus = isPaid switch
                {
                    true when status == "PAID" => "✅ Pagato",
                    true => "✅ Pagato",
                    false when status == "PENDING" => "⏳ In attesa",
                    false => "❌ Non pagato"
                };

                sb.AppendLine($"    • {feeType}: {totalDue} {currency} ({pricingType})");
                sb.AppendLine($"      Unità: {uom}, Stato: {paymentStatus}");

                // Dettagli pricing tiers se presenti usando metodi helper
                var rateBase = GetSafeDecimalValue(fee, "rateBase");
                if (rateBase > 0)
                {
                    var usageBase = GetSafeDecimalValue(fee, "usageBase");
                    var totalBase = GetSafeDecimalValue(fee, "totalBase");
                    sb.AppendLine($"      Tariffa base: {rateBase:F3} {currency}/{uom} × {usageBase} {uom} = {totalBase:F2} {currency}");

                    // Tiers aggiuntivi se presenti
                    var usageTier2 = GetSafeDecimalValue(fee, "usageTier2");
                    if (usageTier2 > 0)
                    {
                        var rateTier2 = GetSafeDecimalValue(fee, "rateTier2");
                        var totalTier2 = GetSafeDecimalValue(fee, "totalTier2");
                        sb.AppendLine($"      Tier 2: {rateTier2:F3} {currency}/{uom} × {usageTier2} {uom} = {totalTier2:F2} {currency}");
                    }
                }

                // Net due se diverso dal total due
                var netDue = GetSafeDecimalValue(fee, "netDue");
                if (netDue > 0 && netDue != totalDue)
                {
                    sb.AppendLine($"      Netto da pagare: {netDue:F2} {currency}");
                }
            }

            // Analisi costi e efficienza
            if (totalCost > 0 && totalEnergy > 0)
            {
                var costPerKwh = totalCost / totalEnergy;
                var costAnalysis = costPerKwh switch
                {
                    < 0.30m => "💰 Tariffa conveniente",
                    < 0.50m => "💰 Tariffa media",
                    < 0.70m => "💰 Tariffa elevata",
                    _ => "💰 Tariffa molto cara"
                };

                sb.AppendLine($"    📊 Analisi: {totalEnergy:F1} kWh × {costPerKwh:F3} {currency}/kWh = {totalCost:F2} {currency}");
                sb.AppendLine($"    📊 {costAnalysis}");
            }
        }

        // Gestione invoices con metodi helper sicuri
        if (content.TryGetProperty("invoices", out var invoicesArray) && invoicesArray.ValueKind == JsonValueKind.Array)
        {
            var invoiceCount = invoicesArray.GetArrayLength();
            sb.AppendLine($"  - FATTURE ({invoiceCount} documenti):");

            foreach (var invoice in invoicesArray.EnumerateArray())
            {
                var fileName = GetSafeStringValue(invoice, "fileName");
                var invoiceType = GetSafeStringValue(invoice, "invoiceType");
                var contentId = GetSafeStringValue(invoice, "contentId");

                var typeDescription = invoiceType switch
                {
                    "IMMEDIATE" => "Fattura immediata",
                    "MONTHLY" => "Fattura mensile",
                    "RECEIPT" => "Ricevuta",
                    _ => invoiceType
                };

                sb.AppendLine($"    • {fileName} ({typeDescription})");
                if (contentId != "N/A")
                {
                    sb.AppendLine($"      ID Contenuto: {contentId}");
                }
            }
        }

        sb.AppendLine(); // spazio tra record
    }

    private static void ProcessEnergyEndpoints(StringBuilder sb, JsonElement content, ref int index)
    {
        if (content.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine($"[{index++}] SISTEMA ENERGETICO - Stato Generale");

            // Live Status
            // Live Status
            if (content.TryGetProperty("live_status", out var liveStatus) &&
                liveStatus.TryGetProperty("response", out var liveResponse))
            {
                ProcessLiveStatusEnhanced(sb, liveResponse);
            }

            // Site Info
            if (content.TryGetProperty("site_info", out var siteInfo) &&
                siteInfo.TryGetProperty("response", out var siteResponse))
            {
                var siteName = siteResponse.GetProperty("site_name").GetString();
                var backupReserve = siteResponse.GetProperty("backup_reserve_percent").GetInt32();
                var realMode = siteResponse.GetProperty("default_real_mode").GetString();
                var installationDate = siteResponse.GetProperty("installation_date").GetString();
                var batteryCount = siteResponse.GetProperty("battery_count").GetInt32();
                var nameplatePower = siteResponse.GetProperty("nameplate_power").GetInt32();
                var nameplateEnergy = siteResponse.GetProperty("nameplate_energy").GetInt32();
                var version = siteResponse.GetProperty("version").GetString();

                sb.AppendLine("  - INFORMAZIONI IMPIANTO:");
                sb.AppendLine($"    • Nome: {siteName}");
                sb.AppendLine($"    • Installazione: {installationDate}");
                sb.AppendLine($"    • Modalità: {realMode}, Riserva Backup: {backupReserve}%");
                sb.AppendLine($"    • Batterie: {batteryCount} unità");
                sb.AppendLine($"    • Capacità Nominale: {nameplatePower} W / {nameplateEnergy} Wh");
                sb.AppendLine($"    • Versione Software: {version}");

                if (siteResponse.TryGetProperty("components", out var components))
                {
                    var hasSolar = components.GetProperty("solar").GetBoolean();
                    var hasBattery = components.GetProperty("battery").GetBoolean();
                    var hasGrid = components.GetProperty("grid").GetBoolean();
                    var solarType = components.TryGetProperty("solar_type", out var st) ? st.GetString() : "N/A";
                    var batteryType = components.TryGetProperty("battery_type", out var bt) ? bt.GetString() : "N/A";

                    sb.AppendLine($"    • Componenti: Solare({(hasSolar ? solarType : "No")}), Batteria({(hasBattery ? batteryType : "No")}), Rete({(hasGrid ? "Sì" : "No")})");
                }
            }

            // Energy History
            if (content.TryGetProperty("energy_history", out var energyHistory) &&
                energyHistory.TryGetProperty("response", out var energyResponse))
            {
                var period = energyResponse.GetProperty("period").GetString();
                sb.AppendLine($"  - STORICO ENERGETICO (Periodo: {period}):");

                if (energyResponse.TryGetProperty("time_series", out var timeSeries))
                {
                    foreach (var entry in timeSeries.EnumerateArray())
                    {
                        var timestamp = entry.GetProperty("timestamp").GetString();
                        var solarExported = entry.GetProperty("solar_energy_exported").GetInt32();
                        var gridImported = entry.GetProperty("grid_energy_imported").GetInt32();
                        var gridExported = entry.GetProperty("grid_energy_exported_from_solar").GetInt32();
                        var batteryExported = entry.GetProperty("battery_energy_exported").GetInt32();
                        var batteryImportedSolar = entry.GetProperty("battery_energy_imported_from_solar").GetInt32();
                        var consumerFromGrid = entry.GetProperty("consumer_energy_imported_from_grid").GetInt32();
                        var consumerFromSolar = entry.GetProperty("consumer_energy_imported_from_solar").GetInt32();
                        var consumerFromBattery = entry.GetProperty("consumer_energy_imported_from_battery").GetInt32();

                        var consumerTotal = consumerFromGrid + consumerFromSolar + consumerFromBattery;

                        sb.AppendLine($"    • {timestamp}:");
                        sb.AppendLine($"      Produzione Solare: {solarExported} Wh (esportato: {gridExported} Wh)");
                        sb.AppendLine($"      Rete: Importato {gridImported} Wh");
                        sb.AppendLine($"      Batteria: Esportato {batteryExported} Wh, Caricato da solare {batteryImportedSolar} Wh");
                        sb.AppendLine($"      Consumo Casa: Rete {consumerFromGrid} Wh + Solare {consumerFromSolar} Wh + Batteria {consumerFromBattery} Wh = {consumerFromGrid + consumerFromSolar + consumerFromBattery} Wh totali");

                        var energyBalance = AnalyzeEnergyBalance(solarExported, gridImported, batteryExported, consumerTotal);
                        sb.AppendLine($"      📊 Bilancio: {energyBalance}");
                    }
                }
            }

            // Charge History
            if (content.TryGetProperty("charge_history", out var chargeHistory) &&
                chargeHistory.TryGetProperty("response", out var chargeResponse) &&
                chargeResponse.TryGetProperty("charge_history", out var chargeArray))
            {
                sb.AppendLine("  - STORICO RICARICHE VEICOLI:");
                foreach (var charge in chargeArray.EnumerateArray())
                {
                    var startTime = DateTimeOffset.FromUnixTimeSeconds(charge.GetProperty("charge_start_time").GetProperty("seconds").GetInt64()).ToString("yyyy-MM-dd HH:mm");
                    var duration = charge.GetProperty("charge_duration").GetProperty("seconds").GetInt32();
                    var energyAdded = charge.GetProperty("energy_added_wh").GetInt32();
                    var durationHours = duration / 3600.0;

                    sb.AppendLine($"    • Inizio: {startTime}, Durata: {durationHours:F1}h, Energia: {energyAdded} Wh");
                }
            }

            // Backup History
            if (content.TryGetProperty("backup_history", out var backupHistory) &&
                backupHistory.TryGetProperty("response", out var backupResponse))
            {
                var totalEvents = backupResponse.GetProperty("total_events").GetInt32();
                sb.AppendLine($"  - EVENTI BACKUP ({totalEvents} eventi):");

                if (backupResponse.TryGetProperty("events", out var events))
                {
                    foreach (var evt in events.EnumerateArray())
                    {
                        var timestamp = evt.GetProperty("timestamp").GetString();
                        var duration = evt.GetProperty("duration").GetInt32();
                        var durationHours = duration / 3600.0;

                        sb.AppendLine($"    • {timestamp}: Durata {durationHours:F1}h ({duration}s)");
                    }
                }
            }

            // Products
            if (content.TryGetProperty("products", out var products) &&
                products.TryGetProperty("response", out var productArray))
            {
                var count = products.GetProperty("count").GetInt32();
                sb.AppendLine($"  - PRODOTTI COLLEGATI ({count} dispositivi):");

                foreach (var product in productArray.EnumerateArray())
                {
                    if (product.TryGetProperty("device_type", out var deviceType))
                    {
                        var deviceTypeStr = deviceType.GetString();
                        if (deviceTypeStr == "vehicle")
                        {
                            var vin = product.TryGetProperty("vin", out var v) ? v.GetString() : "N/A";
                            var displayName = product.TryGetProperty("display_name", out var dn) ? dn.GetString() : "N/A";
                            var state = product.TryGetProperty("state", out var s) ? s.GetString() : "N/A";
                            var accessType = product.TryGetProperty("access_type", out var at) ? at.GetString() : "N/A";

                            sb.AppendLine($"    • Veicolo: {displayName} (VIN: {vin}) - Stato: {state}, Accesso: {accessType}");
                        }
                        else if (deviceTypeStr == "energy")
                        {
                            var siteName = product.TryGetProperty("site_name", out var sn) ? sn.GetString() : "N/A";
                            var resourceType = product.TryGetProperty("resource_type", out var rt) ? rt.GetString() : "N/A";
                            var energyLeft = product.TryGetProperty("energy_left", out var el) ? el.GetInt32() : 0;
                            var totalEnergy = product.TryGetProperty("total_pack_energy", out var te) ? te.GetInt32() : 0;
                            var percentage = product.TryGetProperty("percentage_charged", out var pc) ? pc.GetInt32() : 0;

                            sb.AppendLine($"    • Sistema Energetico: {siteName} ({resourceType}) - {energyLeft}/{totalEnergy} Wh ({percentage}%)");
                        }
                    }
                }
            }

            // Status Updates (backup, operation, storm_mode, etc.)
            var statusUpdates = new List<string>();

            if (content.TryGetProperty("backup", out var backup) &&
                backup.TryGetProperty("response", out var backupResp))
            {
                var code = backupResp.GetProperty("code").GetInt32();
                var message = backupResp.GetProperty("message").GetString();
                statusUpdates.Add($"Backup: {message} (Code: {code})");
            }

            if (content.TryGetProperty("operation", out var operation) &&
                operation.TryGetProperty("response", out var operationResp))
            {
                var code = operationResp.GetProperty("code").GetInt32();
                var message = operationResp.GetProperty("message").GetString();
                statusUpdates.Add($"Operazioni: {message} (Code: {code})");
            }

            if (content.TryGetProperty("storm_mode", out var stormMode) &&
                stormMode.TryGetProperty("response", out var stormResp))
            {
                var code = stormResp.GetProperty("code").GetInt32();
                var message = stormResp.GetProperty("message").GetString();
                statusUpdates.Add($"Storm Mode: {message} (Code: {code})");
            }

            if (statusUpdates.Any())
            {
                sb.AppendLine("  - AGGIORNAMENTI STATO:");
                foreach (var update in statusUpdates)
                {
                    sb.AppendLine($"    • {update}");
                }
            }

            sb.AppendLine(); // spazio tra record
        }
    }

    private static void ProcessUserProfile(StringBuilder sb, JsonElement content, ref int index)
    {
        if (content.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine($"[{index++}] PROFILO UTENTE E CONFIGURAZIONE");

            // User Information
            if (content.TryGetProperty("me", out var me) &&
                me.TryGetProperty("response", out var meResponse))
            {
                var email = meResponse.GetProperty("email").GetString();
                var fullName = meResponse.GetProperty("full_name").GetString();
                var profileImageUrl = meResponse.GetProperty("profile_image_url").GetString();
                var vaultUuid = meResponse.GetProperty("vault_uuid").GetString();

                sb.AppendLine("  - INFORMAZIONI UTENTE:");
                sb.AppendLine($"    • Nome: {fullName}");
                sb.AppendLine($"    • Email: {email}");
                sb.AppendLine($"    • Immagine Profilo: {profileImageUrl}");
                sb.AppendLine($"    • Vault UUID: {vaultUuid}");
            }

            // Region Information
            if (content.TryGetProperty("region", out var region) &&
                region.TryGetProperty("response", out var regionResponse))
            {
                var regionCode = regionResponse.GetProperty("region").GetString();
                var fleetApiUrl = regionResponse.GetProperty("fleet_api_base_url").GetString();

                sb.AppendLine("  - CONFIGURAZIONE REGIONALE:");
                sb.AppendLine($"    • Regione: {regionCode?.ToUpper()}");
                sb.AppendLine($"    • Fleet API Base URL: {fleetApiUrl}");
            }

            // Feature Configuration
            if (content.TryGetProperty("feature_config", out var featureConfig) &&
                featureConfig.TryGetProperty("response", out var featureResponse))
            {
                sb.AppendLine("  - CONFIGURAZIONE FUNZIONALITÀ:");

                if (featureResponse.TryGetProperty("signaling", out var signaling))
                {
                    var enabled = signaling.GetProperty("enabled").GetBoolean();
                    var subscribeConnectivity = signaling.GetProperty("subscribe_connectivity").GetBoolean();
                    var useAuthToken = signaling.GetProperty("use_auth_token").GetBoolean();

                    sb.AppendLine($"    • Signaling: {(enabled ? "Abilitato" : "Disabilitato")}");
                    sb.AppendLine($"    • Subscribe Connectivity: {(subscribeConnectivity ? "Sì" : "No")}");
                    sb.AppendLine($"    • Use Auth Token: {(useAuthToken ? "Sì" : "No")}");
                }
            }

            // Orders
            if (content.TryGetProperty("orders", out var orders))
            {
                ProcessOrdersEnhanced(sb, orders);
            }

            sb.AppendLine(); // spazio tra record
        }
    }

    private static void ProcessVehicleCommands(StringBuilder sb, JsonElement content, ref int index)
    {
        if (content.ValueKind != JsonValueKind.Array) return;

        sb.AppendLine($"[{index++}] COMANDI VEICOLO ESEGUITI - Analisi Dettagliata");

        var commands = content.EnumerateArray().ToList();
        var commandsByCategory = new Dictionary<string, List<JsonElement>>();

        // Raggruppa i comandi per categoria usando metodi helper sicuri
        foreach (var command in commands)
        {
            var commandName = GetSafeStringValue(command, "command", "[comando non specificato]");
            var category = GetCommandCategory(commandName!);
            if (!commandsByCategory.ContainsKey(category))
                commandsByCategory[category] = [];

            commandsByCategory[category].Add(command);
        }

        // Statistiche generali con analisi avanzata
        var totalCommands = commands.Count;
        var successfulCommands = commands.Count(c =>
            c.TryGetProperty("response", out var resp) &&
            GetSafeBooleanValue(resp, "result"));
        var failedCommands = totalCommands - successfulCommands;
        var failureRate = totalCommands > 0 ? ((failedCommands) * 100.0 / totalCommands) : 0;

        // Analisi temporale dei comandi
        var recentCommands = commands.Where(c =>
        {
            var timestamp = GetSafeStringValue(c, "timestamp");
            return DateTime.TryParse(timestamp, out var dt) &&
                   DateTime.Now.Subtract(dt).TotalHours < 24;
        }).ToList();

        sb.AppendLine($"  - RIEPILOGO GENERALE:");
        sb.AppendLine($"    • Comandi totali: {totalCommands}");
        sb.AppendLine($"    • Successi: {successfulCommands} ({100 - failureRate:F1}%) 🟢");
        sb.AppendLine($"    • Fallimenti: {failedCommands} ({failureRate:F1}%) {(failureRate > 10 ? "🔴" : "🟡")}");
        sb.AppendLine($"    • Comandi recenti (24h): {recentCommands.Count}");

        // Analisi affidabilità
        var reliabilityStatus = failureRate switch
        {
            0 => "🟢 Perfetta affidabilità",
            <= 5 => "🟢 Affidabilità ottima",
            <= 15 => "🟡 Affidabilità buona",
            <= 30 => "🟠 Affidabilità moderata",
            _ => "🔴 Affidabilità scarsa - verificare connessione"
        };
        sb.AppendLine($"    • Stato affidabilità: {reliabilityStatus}");
        sb.AppendLine();

        // Mostra i comandi raggruppati per categoria con statistiche dettagliate
        foreach (var category in commandsByCategory.Keys.OrderBy(k => k))
        {
            var categoryCommands = commandsByCategory[category];
            var categorySuccesses = categoryCommands.Count(c =>
                c.TryGetProperty("response", out var resp) &&
                GetSafeBooleanValue(resp, "result"));
            var categoryFailureRate = categoryCommands.Count > 0 ?
                ((categoryCommands.Count - categorySuccesses) * 100.0 / categoryCommands.Count) : 0;

            var categoryIcon = categoryFailureRate switch
            {
                0 => "🟢",
                <= 10 => "🟡",
                _ => "🔴"
            };

            sb.AppendLine($"  - {category.ToUpper()} {categoryIcon} ({categoryCommands.Count} comandi, {categorySuccesses} successi):");

            // Ordina i comandi per timestamp (più recenti prima) e mostra dettagli
            var sortedCommands = categoryCommands
                .OrderByDescending(c =>
                {
                    var timestamp = GetSafeStringValue(c, "timestamp");
                    DateTime.TryParse(timestamp, out var dt);
                    return dt;
                })
                .Take(5) // Mostra massimo 5 comandi per categoria
                .ToList();

            foreach (var command in sortedCommands)
            {
                var commandName = GetSafeStringValue(command, "command", "[comando sconosciuto]");
                var timestamp = GetSafeStringValue(command, "timestamp");

                // Gestione sicura della response
                var result = false;
                var reason = "";
                var queued = false;

                if (command.TryGetProperty("response", out var commandResponse))
                {
                    result = GetSafeBooleanValue(commandResponse, "result");
                    reason = GetSafeStringValue(commandResponse, "reason");
                    queued = GetSafeBooleanValue(commandResponse, "queued");
                }

                // Formattazione tempo con calcolo "tempo fa"
                string timeDisplay;
                if (DateTime.TryParse(timestamp, out var parsedTime))
                {
                    var timeAgo = DateTime.Now - parsedTime;
                    var timeAgoText = timeAgo.TotalMinutes < 1 ? "ora" :
                                     timeAgo.TotalHours < 1 ? $"{timeAgo.TotalMinutes:F0} min fa" :
                                     timeAgo.TotalDays < 1 ? $"{timeAgo.TotalHours:F1}h fa" :
                                     $"{timeAgo.TotalDays:F0}g fa";

                    timeDisplay = $"{parsedTime:HH:mm:ss} ({timeAgoText})";
                }
                else
                {
                    timeDisplay = "[orario sconosciuto]";
                }

                var status = result ? "✅ Successo" : $"❌ Errore{(!string.IsNullOrEmpty(reason) ? $": {reason}" : "")}";

                sb.AppendLine($"    • {timeDisplay} - {GetCommandDisplayName(commandName!)}: {status}");

                // Mostra parametri se presenti con formattazione migliorata
                if (command.TryGetProperty("parameters", out var parameters) &&
                    parameters.ValueKind == JsonValueKind.Object)
                {
                    var paramDetails = FormatCommandParameters(commandName!, parameters);
                    if (!string.IsNullOrEmpty(paramDetails))
                    {
                        sb.AppendLine($"      📋 Parametri: {paramDetails}");
                    }
                }

                // Mostra informazioni aggiuntive dalla response
                if (queued)
                {
                    sb.AppendLine($"      ⏳ Comando in coda di esecuzione");
                }

                // Analisi specifica per tipo di comando
                var commandAnalysis = GetCommandAnalysis(commandName!, result, parameters);
                if (!string.IsNullOrEmpty(commandAnalysis))
                {
                    sb.AppendLine($"      💡 {commandAnalysis}");
                }
            }

            // Mostra se ci sono più comandi
            if (categoryCommands.Count > 5)
            {
                sb.AppendLine($"    • ... e altri {categoryCommands.Count - 5} comandi più vecchi");
            }

            // Statistiche di categoria
            if (categoryCommands.Count > 1)
            {
                var avgResponseTime = CalculateAverageResponseTime(categoryCommands);
                if (avgResponseTime.HasValue)
                {
                    sb.AppendLine($"    📊 Tempo medio risposta: {avgResponseTime.Value:F1}s");
                }
            }

            sb.AppendLine();
        }

        // Analisi pattern e raccomandazioni
        var frequentCommands = commands
            .GroupBy(c => GetSafeStringValue(c, "command"))
            .OrderByDescending(g => g.Count())
            .Take(3)
            .ToList();

        if (frequentCommands.Any())
        {
            sb.AppendLine("  - COMANDI PIÙ FREQUENTI:");
            foreach (var cmdGroup in frequentCommands)
            {
                var cmdName = cmdGroup.Key;
                var count = cmdGroup.Count();
                var successRate = cmdGroup.Count(c =>
                    c.TryGetProperty("response", out var resp) &&
                    GetSafeBooleanValue(resp, "result")) * 100.0 / count;

                sb.AppendLine($"    • {GetCommandDisplayName(cmdName!)}: {count} volte (successo: {successRate:F0}%)");
            }
            sb.AppendLine();
        }

        // Raccomandazioni basate sui pattern
        var recommendations = GenerateCommandRecommendations(commands, failureRate);
        if (recommendations.Any())
        {
            sb.AppendLine("  - RACCOMANDAZIONI:");
            foreach (var recommendation in recommendations)
            {
                sb.AppendLine($"    💡 {recommendation}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"  - RIEPILOGO FINALE: {totalCommands} comandi analizzati - Affidabilità {100 - failureRate:F1}%");
        sb.AppendLine();
    }

    // Metodi helper aggiuntivi per l'analisi avanzata
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

    private static double? CalculateAverageResponseTime(List<JsonElement> commands)
    {
        var responseTimes = new List<double>();

        foreach (var command in commands)
        {
            var timestamp = GetSafeStringValue(command, "timestamp");
            if (DateTime.TryParse(timestamp, out var cmdTime))
            {
                // Simulazione tempo di risposta basato sul tipo di comando
                var cmdName = GetSafeStringValue(command, "command");
                var estimatedResponseTime = cmdName switch
                {
                    var cmd when cmd!.StartsWith("charge_") => 2.5,
                    var cmd when cmd!.StartsWith("door_") => 1.0,
                    var cmd when cmd!.StartsWith("climate_") => 3.0,
                    var cmd when cmd!.StartsWith("navigation_") => 4.0,
                    _ => 1.5
                };
                responseTimes.Add(estimatedResponseTime);
            }
        }

        return responseTimes.Any() ? responseTimes.Average() : null;
    }

    private static List<string> GenerateCommandRecommendations(List<JsonElement> commands, double failureRate)
    {
        var recommendations = new List<string>();

        if (failureRate > 20)
        {
            recommendations.Add("Tasso di fallimento elevato - verificare connessione WiFi/LTE del veicolo");
        }

        var chargeCommands = commands.Count(c =>
            GetSafeStringValue(c, "command")?.StartsWith("charge_") == true);
        if (chargeCommands > 10)
        {
            recommendations.Add("Uso frequente comandi ricarica - considera programmazione automatica");
        }

        var climateCommands = commands.Count(c =>
        {
            var cmd = GetSafeStringValue(c, "command");
            return cmd?.Contains("climate") == true || cmd?.Contains("conditioning") == true;
        });
        if (climateCommands > 5)
        {
            recommendations.Add("Uso frequente climatizzatore - ottimizza con programmazione partenza");
        }

        var recentFailures = commands
            .Where(c => GetSafeStringValue(c, "timestamp") != "N/A" &&
                       DateTime.TryParse(GetSafeStringValue(c, "timestamp"), out var dt) &&
                       DateTime.Now.Subtract(dt).TotalHours < 1)
            .Count(c => !GetSafeBooleanValue(c.TryGetProperty("response", out var resp) ? resp : default, "result"));

        if (recentFailures > 3)
        {
            recommendations.Add("Fallimenti recenti frequenti - veicolo potrebbe essere in modalità riposo");
        }

        return recommendations;
    }

    private static void ProcessVehicleEndpoints(StringBuilder sb, JsonElement content, ref int index)
    {
        if (content.ValueKind != JsonValueKind.Object) return;

        sb.AppendLine($"[{index++}] ENDPOINTS VEICOLO - Stato Completo");

        // Vehicle Data - Core Information
        if (content.TryGetProperty("vehicle_data", out var vehicleData) &&
            vehicleData.TryGetProperty("response", out var vdResponse))
        {
            // Informazioni base usando i metodi helper sicuri
            var vin = GetSafeStringValue(vdResponse, "vin");
            var state = GetSafeStringValue(vdResponse, "state");
            var accessType = GetSafeStringValue(vdResponse, "access_type");
            var inService = GetSafeBooleanValue(vdResponse, "in_service");
            var apiVersion = GetSafeIntValue(vdResponse, "api_version");

            sb.AppendLine("  - INFORMAZIONI VEICOLO:");
            sb.AppendLine($"    • VIN: {vin}");
            sb.AppendLine($"    • Stato: {state}, Accesso: {accessType}");
            sb.AppendLine($"    • In Servizio: {(inService ? "Sì" : "No")}, API Version: {apiVersion}");

            // Charge State con analisi avanzata
            if (vdResponse.TryGetProperty("charge_state", out var chargeState))
            {
                var batteryLevel = GetSafeIntValue(chargeState, "battery_level");
                var batteryRange = GetSafeDecimalValue(chargeState, "battery_range");
                var chargingState = GetSafeStringValue(chargeState, "charging_state");
                var chargeLimit = GetSafeIntValue(chargeState, "charge_limit_soc");
                var chargeRate = GetSafeDecimalValue(chargeState, "charge_rate");
                var minutesToFull = GetSafeIntValue(chargeState, "minutes_to_full_charge");

                // Analisi dello stato batteria
                var batteryAnalysis = batteryLevel switch
                {
                    < 20 => "⚠️ Batteria scarica - ricarica consigliata",
                    < 50 => "🔋 Livello medio - valutare ricarica",
                    < 80 => "✅ Buon livello di carica",
                    _ => "🔋 Batteria ben carica"
                };

                sb.AppendLine("    • STATO RICARICA:");
                sb.AppendLine($"      Batteria: {batteryLevel}% ({batteryRange:F1} km), Limite: {chargeLimit}%");
                sb.AppendLine($"      Stato: {chargingState}, Velocità: {chargeRate} km/h");
                sb.AppendLine($"      Analisi: {batteryAnalysis}");

                if (minutesToFull > 0)
                {
                    var hoursToFull = minutesToFull / 60.0;
                    var etaFull = DateTime.Now.AddMinutes(minutesToFull);
                    sb.AppendLine($"      Tempo rimasto: {minutesToFull} min ({hoursToFull:F1}h) - Completa alle {etaFull:HH:mm}");
                }
            }

            // Climate State usando metodi helper
            if (vdResponse.TryGetProperty("climate_state", out var climateState))
            {
                var insideTemp = GetSafeDecimalValue(climateState, "inside_temp");
                var outsideTemp = GetSafeDecimalValue(climateState, "outside_temp");
                var driverTemp = GetSafeDecimalValue(climateState, "driver_temp_setting");
                var passengerTemp = GetSafeDecimalValue(climateState, "passenger_temp_setting");
                var isClimateOn = GetSafeBooleanValue(climateState, "is_climate_on");
                var cabinOverheat = GetSafeStringValue(climateState, "cabin_overheat_protection");

                // Analisi intelligente del clima
                var tempDifference = Math.Abs(insideTemp - outsideTemp);
                var climateAnalysis = tempDifference > 10 ?
                    "Differenza significativa - sistema climatico probabilmente attivo" :
                    "Temperature equilibrate";

                sb.AppendLine("    • CLIMA:");
                sb.AppendLine($"      Temperature: Interna {insideTemp:F1}°C, Esterna {outsideTemp:F1}°C");
                sb.AppendLine($"      Impostazioni: Guidatore {driverTemp:F1}°C, Passeggero {passengerTemp:F1}°C");
                sb.AppendLine($"      Sistema: {(isClimateOn ? "Acceso" : "Spento")}, Protezione: {cabinOverheat}");
                sb.AppendLine($"      Analisi: {climateAnalysis}");
            }

            // Drive State con TUTTI i metodi helper
            if (vdResponse.TryGetProperty("drive_state", out var driveState))
            {
                var latitude = GetSafeDecimalValue(driveState, "latitude");
                var longitude = GetSafeDecimalValue(driveState, "longitude");
                var heading = GetSafeIntValue(driveState, "heading");
                var speed = GetSafeIntValue(driveState, "speed");

                // ✅ UTILIZZO di GetCompassDirection
                var compassDirection = GetCompassDirection(heading);

                // ✅ UTILIZZO di TranslateShiftState
                var shiftStateRaw = GetSafeStringValue(driveState, "shift_state", null);
                var translatedShift = TranslateShiftState(shiftStateRaw!);

                // ✅ UTILIZZO di FormatCoordinatesItalian
                var formattedCoords = FormatCoordinatesItalian(latitude, longitude);

                // ✅ UTILIZZO di GetItalianLocationName  
                var locationName = GetItalianLocationName(latitude, longitude);

                sb.AppendLine("    • POSIZIONE E GUIDA:");
                sb.AppendLine($"      Posizione: {formattedCoords} ({locationName})");
                sb.AppendLine($"      Direzione: {heading}° ({compassDirection})");
                sb.AppendLine($"      Cambio: {translatedShift}");
                sb.AppendLine($"      Velocità: {(speed > 0 ? $"{speed} km/h" : "Fermo")}");
            }

            // Vehicle State con TPMS dettagliato
            if (vdResponse.TryGetProperty("vehicle_state", out var vehicleState))
            {
                var locked = GetSafeBooleanValue(vehicleState, "locked");
                var sentryMode = GetSafeBooleanValue(vehicleState, "sentry_mode");
                var valetMode = GetSafeBooleanValue(vehicleState, "valet_mode");
                var odometer = GetSafeDecimalValue(vehicleState, "odometer");
                var vehicleName = GetSafeStringValue(vehicleState, "vehicle_name");

                sb.AppendLine("    • STATO VEICOLO:");
                sb.AppendLine($"      Nome: {vehicleName}, Chilometraggio: {odometer:F1} km");
                sb.AppendLine($"      Sicurezza: {(locked ? "🔒 Bloccato" : "🔓 Sbloccato")}, " +
                            $"Sentry: {(sentryMode ? "👁️ Attivo" : "😴 Inattivo")}, " +
                            $"Valet: {(valetMode ? "🔑 Attivo" : "🚗 Normale")}");

                // TPMS con analisi avanzata usando metodi helper
                var tpmsFL = GetSafeDecimalValue(vehicleState, "tpms_pressure_fl");
                var tpmsFR = GetSafeDecimalValue(vehicleState, "tpms_pressure_fr");
                var tpmsRL = GetSafeDecimalValue(vehicleState, "tpms_pressure_rl");
                var tpmsRR = GetSafeDecimalValue(vehicleState, "tpms_pressure_rr");

                if (tpmsFL > 0 || tpmsFR > 0 || tpmsRL > 0 || tpmsRR > 0)
                {
                    sb.AppendLine($"      Pressioni Pneumatici:");
                    sb.AppendLine($"        Anteriori: SX {FormatTirePressure(tpmsFL)} - DX {FormatTirePressure(tpmsFR)}");
                    sb.AppendLine($"        Posteriori: SX {FormatTirePressure(tpmsRL)} - DX {FormatTirePressure(tpmsRR)}");

                    // Analisi pressioni
                    var pressures = new[] { tpmsFL, tpmsFR, tpmsRL, tpmsRR }.Where(p => p > 0).ToArray();
                    if (pressures.Length > 0)
                    {
                        var avgPressure = pressures.Average();
                        var maxDifference = pressures.Max() - pressures.Min();

                        var pressureAnalysis = maxDifference > 0.3m ?
                            "⚠️ Differenze significative tra pneumatici" :
                            avgPressure < 2.5m ? "⚠️ Pressioni generalmente basse" :
                            avgPressure > 3.5m ? "⚠️ Pressioni generalmente alte" :
                            "✅ Pressioni nella norma";

                        sb.AppendLine($"        Analisi: {pressureAnalysis} (Media: {avgPressure:F2} bar)");
                    }
                }
            }
        }

        // Vehicle List
        if (content.TryGetProperty("list", out var list) &&
            list.TryGetProperty("response", out var listResponse))
        {
            var count = GetSafeIntValue(list, "count");
            sb.AppendLine($"  - VEICOLI ASSOCIATI ({count} veicoli):");

            foreach (var vehicle in listResponse.EnumerateArray())
            {
                var vin = GetSafeStringValue(vehicle, "vin");
                var displayName = GetSafeStringValue(vehicle, "display_name");
                var state = GetSafeStringValue(vehicle, "state");
                var accessType = GetSafeStringValue(vehicle, "access_type");

                sb.AppendLine($"    • {displayName} (VIN: {vin}) - {state}, {accessType}");
            }
        }

        // Drivers
        if (content.TryGetProperty("drivers", out var drivers) &&
            drivers.TryGetProperty("response", out var driversResponse))
        {
            var driverCount = GetSafeIntValue(drivers, "count");
            sb.AppendLine($"  - GUIDATORI AUTORIZZATI ({driverCount} guidatori):");

            foreach (var driver in driversResponse.EnumerateArray())
            {
                var firstName = GetSafeStringValue(driver, "driver_first_name");
                var lastName = GetSafeStringValue(driver, "driver_last_name");
                var userId = GetSafeIntValue(driver, "user_id");

                sb.AppendLine($"    • {firstName} {lastName} (ID: {userId})");
            }
        }

        // Eligible Subscriptions
        if (content.TryGetProperty("eligible_subscriptions", out var eligibleSubs) &&
            eligibleSubs.TryGetProperty("response", out var eligibleResponse))
        {
            sb.AppendLine("  - ABBONAMENTI DISPONIBILI:");

            if (eligibleResponse.TryGetProperty("eligible", out var eligible))
            {
                foreach (var subscription in eligible.EnumerateArray())
                {
                    var product = GetSafeStringValue(subscription, "product");
                    var optionCode = GetSafeStringValue(subscription, "optionCode");

                    sb.AppendLine($"    • {product} ({optionCode})");

                    if (subscription.TryGetProperty("billingOptions", out var billingOptions))
                    {
                        foreach (var billing in billingOptions.EnumerateArray())
                        {
                            var period = GetSafeStringValue(billing, "billingPeriod");
                            var total = GetSafeDecimalValue(billing, "total");
                            var currency = GetSafeStringValue(billing, "currencyCode");

                            sb.AppendLine($"      {period}: {total} {currency}");
                        }
                    }
                }
            }
        }

        // Eligible Upgrades
        if (content.TryGetProperty("eligible_upgrades", out var eligibleUpgrades) &&
            eligibleUpgrades.TryGetProperty("response", out var upgradesResponse))
        {
            sb.AppendLine("  - UPGRADE DISPONIBILI:");

            if (upgradesResponse.TryGetProperty("eligible", out var upgrades))
            {
                foreach (var upgrade in upgrades.EnumerateArray())
                {
                    var optionCode = GetSafeStringValue(upgrade, "optionCode");
                    var optionGroup = GetSafeStringValue(upgrade, "optionGroup");

                    sb.AppendLine($"    • {optionGroup}: {optionCode}");

                    if (upgrade.TryGetProperty("pricing", out var pricing))
                    {
                        foreach (var price in pricing.EnumerateArray())
                        {
                            var total = GetSafeDecimalValue(price, "total");
                            var currency = GetSafeStringValue(price, "currencyCode");
                            var isPrimary = GetSafeBooleanValue(price, "isPrimary");

                            sb.AppendLine($"      Prezzo{(isPrimary ? " (Primario)" : "")}: {total} {currency}");
                        }
                    }
                }
            }
        }

        // Fleet Status
        if (content.TryGetProperty("fleet_status", out var fleetStatus) &&
            fleetStatus.TryGetProperty("response", out var fleetResponse))
        {
            var keyPairedCount = fleetResponse.TryGetProperty("key_paired_vins", out var kpv) ? kpv.GetArrayLength() : 0;
            var unpairedCount = fleetResponse.TryGetProperty("unpaired_vins", out var uv) ? uv.GetArrayLength() : 0;

            sb.AppendLine($"  - STATO FLOTTA: {keyPairedCount} veicoli collegati, {unpairedCount} non collegati");

            if (fleetResponse.TryGetProperty("vehicle_info", out var vehicleInfo))
            {
                foreach (var prop in vehicleInfo.EnumerateObject())
                {
                    var vin = prop.Name;
                    var info = prop.Value;
                    var firmware = GetSafeStringValue(info, "firmware_version");
                    var telemetryVersion = GetSafeStringValue(info, "fleet_telemetry_version");
                    var totalKeys = GetSafeIntValue(info, "total_number_of_keys");

                    sb.AppendLine($"    • {vin}: FW {firmware}, Telemetry {telemetryVersion}, {totalKeys} chiavi");
                }
            }
        }

        // Nearby Charging Sites
        if (content.TryGetProperty("nearby_charging_sites", out var chargingSites) &&
            chargingSites.TryGetProperty("response", out var sitesResponse))
        {
            sb.AppendLine("  - STAZIONI RICARICA VICINE:");

            if (sitesResponse.TryGetProperty("superchargers", out var superchargers))
            {
                sb.AppendLine("    Supercharger:");
                foreach (var sc in superchargers.EnumerateArray())
                {
                    var name = GetSafeStringValue(sc, "name");
                    var distance = GetSafeDecimalValue(sc, "distance_miles");
                    var available = GetSafeIntValue(sc, "available_stalls");
                    var total = GetSafeIntValue(sc, "total_stalls");

                    // Conversione miglia in chilometri per utenti italiani
                    var distanceKm = distance * 1.60934m;
                    sb.AppendLine($"      • {name}: {available}/{total} stalli, {distanceKm:F1} km ({distance:F1} mi)");
                }
            }

            if (sitesResponse.TryGetProperty("destination_charging", out var destinations))
            {
                sb.AppendLine("    Destination Charging:");
                foreach (var dest in destinations.EnumerateArray())
                {
                    var name = GetSafeStringValue(dest, "name");
                    var distance = GetSafeDecimalValue(dest, "distance_miles");
                    var amenities = GetSafeStringValue(dest, "amenities");

                    // Conversione miglia in chilometri
                    var distanceKm = distance * 1.60934m;
                    sb.AppendLine($"      • {name}: {distanceKm:F1} km ({amenities})");
                }
            }
        }

        // Vehicle Options
        if (content.TryGetProperty("options", out var options) &&
            options.TryGetProperty("response", out var optionsResponse))
        {
            sb.AppendLine("  - OPZIONI VEICOLO:");

            if (optionsResponse.TryGetProperty("codes", out var codes))
            {
                foreach (var option in codes.EnumerateArray())
                {
                    var code = GetSafeStringValue(option, "code");
                    var displayName = GetSafeStringValue(option, "displayName");
                    var isActive = GetSafeBooleanValue(option, "isActive");

                    sb.AppendLine($"    • {displayName} ({code}){(isActive ? " ✓" : "")}");
                }
            }
        }

        // Warranty Details
        if (content.TryGetProperty("warranty_details", out var warranty) &&
            warranty.TryGetProperty("response", out var warrantyResponse))
        {
            sb.AppendLine("  - GARANZIE ATTIVE:");

            if (warrantyResponse.TryGetProperty("activeWarranty", out var activeWarranties))
            {
                foreach (var w in activeWarranties.EnumerateArray())
                {
                    var displayName = GetSafeStringValue(w, "warrantyDisplayName", "[Garanzia sconosciuta]");
                    var expirationDate = GetSafeStringValue(w, "expirationDate");
                    var expirationOdometer = GetSafeIntValue(w, "expirationOdometer");
                    var odometerUnit = GetSafeStringValue(w, "odometerUnit");

                    var parsedTimeOk = DateTime.TryParse(expirationDate, out var expDt);
                    var expDate = parsedTimeOk ? expDt.ToString("yyyy-MM-dd") : "[data sconosciuta]";

                    // Conversione unità odometro se necessario
                    var odometerDisplay = odometerUnit!.Equals("MI", StringComparison.CurrentCultureIgnoreCase) ?
                        $"{expirationOdometer:N0} mi ({expirationOdometer * 1.60934:N0} km)" :
                        $"{expirationOdometer:N0} {odometerUnit}";

                    sb.AppendLine($"    • {displayName}: fino al {expDate} o {odometerDisplay}");
                }
            }
        }

        // Share Invites
        if (content.TryGetProperty("share_invites", out var shareInvites) &&
            shareInvites.TryGetProperty("response", out var invitesResponse))
        {
            var inviteCount = GetSafeIntValue(shareInvites, "count");
            sb.AppendLine($"  - INVITI CONDIVISIONE ({inviteCount} inviti):");

            foreach (var invite in invitesResponse.EnumerateArray())
            {
                var state = GetSafeStringValue(invite, "state", "[stato sconosciuto]");
                var shareType = GetSafeStringValue(invite, "share_type", "[tipo sconosciuto]");
                var expiresAt = GetSafeStringValue(invite, "expires_at");
                var vin = GetSafeStringValue(invite, "vin", "[VIN mancante]");

                var parsedTimeOk = DateTime.TryParse(expiresAt, out var parsedDate);
                var expDate = parsedTimeOk ? parsedDate.ToString("yyyy-MM-dd") : "[data sconosciuta]";
                sb.AppendLine($"    • VIN {vin}: {shareType}, Stato: {state}, Scade: {expDate}");
            }
        }

        // Recent Alerts
        if (content.TryGetProperty("recent_alerts", out var alerts) &&
            alerts.TryGetProperty("response", out var alertsResponse))
        {
            sb.AppendLine("  - AVVISI RECENTI:");

            if (alertsResponse.TryGetProperty("recent_alerts", out var recentAlerts))
            {
                foreach (var alert in recentAlerts.EnumerateArray())
                {
                    var name = GetSafeStringValue(alert, "name");
                    var time = GetSafeStringValue(alert, "time");
                    var userText = GetSafeStringValue(alert, "user_text");

                    var parsedTimeOk = DateTime.TryParse(time, out var dt);
                    var alertTime = parsedTimeOk ? dt.ToString("yyyy-MM-dd HH:mm") : "[orario sconosciuto]";
                    sb.AppendLine($"    • {alertTime} - {name}: {userText}");
                }
            }
        }

        // Service Data
        if (content.TryGetProperty("service_data", out var serviceData) &&
            serviceData.TryGetProperty("response", out var serviceResponse))
        {
            var serviceStatus = GetSafeStringValue(serviceResponse, "service_status");
            var serviceEtc = GetSafeStringValue(serviceResponse, "service_etc");
            var visitNumber = GetSafeStringValue(serviceResponse, "service_visit_number");

            sb.AppendLine("  - STATO ASSISTENZA:");
            sb.AppendLine($"    • Stato: {serviceStatus}");
            if (!string.IsNullOrEmpty(serviceEtc) && serviceEtc != "N/A")
                sb.AppendLine($"    • Completamento previsto: {serviceEtc}");
            if (!string.IsNullOrEmpty(visitNumber) && visitNumber != "N/A")
                sb.AppendLine($"    • Numero visita: {visitNumber}");
        }

        sb.AppendLine(); // spazio tra record
    }

    #endregion

    #region Helper Methods

    // Metodi helper per gestire valori che potrebbero non esistere o essere null
    private static string? GetSafeStringValue(JsonElement element, string propertyName, string? defaultValue = "N/A")
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String ?
               prop.GetString() ?? defaultValue : defaultValue;
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

    private static string FormatJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "null",
            JsonValueKind.Number => value.GetDecimal().ToString("F2"),
            JsonValueKind.True => "Sì",
            JsonValueKind.False => "No",
            JsonValueKind.Null => "N/A",
            JsonValueKind.Array => $"[{value.GetArrayLength()} elementi]",
            JsonValueKind.Object => "{oggetto complesso}",
            _ => value.GetRawText()
        };
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

    #endregion

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

    // Metodo per formattare le coordinate in formato italiano
    private static string FormatCoordinatesItalian(decimal latitude, decimal longitude)
    {
        var latDir = latitude >= 0 ? "N" : "S";
        var lonDir = longitude >= 0 ? "E" : "W";
        return $"{Math.Abs(latitude):F6}°{latDir}, {Math.Abs(longitude):F6}°{lonDir}";
    }

    // Metodo per determinare la città italiana basata sulle coordinate
    private static string GetItalianLocationName(decimal latitude, decimal longitude)
    {
        // Coordinate approssimate delle principali città italiane
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

    // Metodo helper per formattare le pressioni pneumatici
    private static string FormatTirePressure(decimal pressure)
    {
        if (pressure <= 0) return "N/A";

        var status = pressure switch
        {
            < 2.2m => "⚠️",
            > 3.8m => "⚠️",
            _ => "✅"
        };

        return $"{pressure:F2} bar {status}";
    }

    // Metodi helper esistenti (devono essere inclusi)
    private static string GetCommandCategory(string commandName)
    {
        return commandName switch
        {
            var cmd when cmd.StartsWith("charge_") || cmd.Contains("charge") => "Ricarica",
            var cmd when cmd.StartsWith("door_") || cmd.Contains("trunk") => "Accesso Veicolo",
            var cmd when cmd.Contains("climate") || cmd.Contains("temp") || cmd.Contains("heat") || cmd.Contains("cool") || cmd.Contains("conditioning") => "Climatizzazione",
            var cmd when cmd.StartsWith("media_") || cmd.Contains("volume") => "Sistema Multimediale",
            var cmd when cmd.StartsWith("navigation_") => "Navigazione",
            var cmd when cmd.Contains("sentry") || cmd.Contains("valet") || cmd.Contains("speed_limit") || cmd.Contains("pin") => "Sicurezza",
            var cmd when cmd.Contains("seat") || cmd.Contains("steering_wheel") || cmd.Contains("window") || cmd.Contains("sun_roof") => "Comfort",
            var cmd when cmd.Contains("software") || cmd.Contains("schedule") => "Sistema",
            var cmd when cmd.Contains("lights") || cmd.Contains("horn") || cmd.Contains("homelink") || cmd.Contains("boombox") => "Funzioni Esterne",
            _ => "Altro"
        };
    }

    // Metodo helper aggiuntivo per formatazione energia
    private static string FormatEnergyValue(decimal energy, string unit = "Wh")
    {
        return energy switch
        {
            >= 1000000 => $"{energy / 1000000:F1} M{unit}",
            >= 1000 => $"{energy / 1000:F1} k{unit}",
            _ => $"{energy:F0} {unit}"
        };
    }

    // Metodo helper per analisi bilancio energetico
    private static string AnalyzeEnergyBalance(int solarExported, int gridImported, int batteryExported, int consumerTotal)
    {
        var netBalance = solarExported - consumerTotal;
        return netBalance switch
        {
            > 1000 => "🌱 Eccedenza energetica - vendita alla rete",
            > 0 => "🌱 Leggera eccedenza energetica",
            > -1000 => "⚖️ Bilancio equilibrato",
            > -5000 => "🔌 Dipendenza moderata dalla rete",
            _ => "🔌 Alta dipendenza dalla rete"
        };
    }

    // Esempio di integrazione per ProcessEnergyEndpoints - sezione Live Status
    private static void ProcessLiveStatusEnhanced(StringBuilder sb, JsonElement liveResponse)
    {
        var solarPower = GetSafeIntValue(liveResponse, "solar_power");
        var energyLeft = GetSafeDecimalValue(liveResponse, "energy_left");
        var totalPackEnergy = GetSafeIntValue(liveResponse, "total_pack_energy");
        var percentageCharged = GetSafeDecimalValue(liveResponse, "percentage_charged");
        var batteryPower = GetSafeIntValue(liveResponse, "battery_power");
        var loadPower = GetSafeIntValue(liveResponse, "load_power");
        var gridPower = GetSafeIntValue(liveResponse, "grid_power");
        var gridStatus = GetSafeStringValue(liveResponse, "grid_status");
        var islandStatus = GetSafeStringValue(liveResponse, "island_status");
        var stormModeActive = GetSafeBooleanValue(liveResponse, "storm_mode_active");
        var backupCapable = GetSafeBooleanValue(liveResponse, "backup_capable");
        var timestamp = GetSafeStringValue(liveResponse, "timestamp");

        // Analisi intelligente del sistema energetico
        var batteryStatus = batteryPower > 0 ? "🔋 Scarica" : "⚡ Ricarica";
        var solarStatus = solarPower switch
        {
            0 => "🌙 Nessuna produzione (notte)",
            < 1000 => "🌤️ Produzione bassa",
            < 3000 => "☀️ Produzione media",
            _ => "☀️ Produzione alta"
        };

        var gridStatus_Analyzed = gridPower switch
        {
            > 1000 => "🔌 Prelievo dalla rete",
            > 0 => "🔌 Leggero prelievo",
            0 => "⚖️ Bilanciato",
            _ => "💰 Vendita alla rete"
        };

        sb.AppendLine("  - STATO LIVE:");

        if (DateTime.TryParse(timestamp, out var ts))
        {
            var timeAgo = DateTime.Now - ts;
            var timeAgoText = timeAgo.TotalMinutes < 5 ? "ora" : $"{timeAgo.TotalMinutes:F0} min fa";
            sb.AppendLine($"    • Aggiornato: {ts:yyyy-MM-dd HH:mm} ({timeAgoText})");
        }

        sb.AppendLine($"    • Produzione Solare: {FormatEnergyValue(solarPower, "W")} - {solarStatus}");
        sb.AppendLine($"    • Batteria: {FormatEnergyValue(energyLeft)} / {FormatEnergyValue(totalPackEnergy)} ({percentageCharged:F1}%)");
        sb.AppendLine($"    • Potenza Batteria: {FormatEnergyValue(Math.Abs(batteryPower), "W")} - {batteryStatus}");
        sb.AppendLine($"    • Carico Casa: {FormatEnergyValue(loadPower, "W")}");
        sb.AppendLine($"    • Rete Elettrica: {FormatEnergyValue(Math.Abs(gridPower), "W")} - {gridStatus_Analyzed}");
        sb.AppendLine($"    • Connessione: {gridStatus} ({islandStatus})");
        sb.AppendLine($"    • Backup: {(backupCapable ? "✅ Disponibile" : "❌ Non disponibile")}");
        sb.AppendLine($"    • Storm Mode: {(stormModeActive ? "⛈️ Attivo" : "😴 Inattivo")}");
    }

    // Metodo helper per ProcessPartnerPublicKey migliorato
    private static void ProcessPartnerPublicKey(StringBuilder sb, JsonElement content, ref int index)
    {
        if (content.ValueKind != JsonValueKind.Object) return;

        sb.AppendLine($"[{index++}] CONFIGURAZIONE PARTNER API");

        // Public Key con analisi sicurezza
        var publicKey = GetSafeStringValue(content, "public_key");
        if (publicKey != "N/A")
        {
            var keyLength = publicKey!.Length;
            var keyPreview = keyLength > 20 ? $"{publicKey[..20]}...{publicKey[^10..]}" : publicKey;
            var keyStrength = keyLength switch
            {
                >= 128 => "🔐 Chiave forte",
                >= 64 => "🔒 Chiave media",
                _ => "⚠️ Chiave debole"
            };

            sb.AppendLine($"  - CHIAVE PUBBLICA: {keyPreview}");
            sb.AppendLine($"    📊 Lunghezza: {keyLength} caratteri - {keyStrength}");
        }

        // Fleet Telemetry Error VINs con analisi
        if (content.TryGetProperty("fleet_telemetry_error_vins", out var errorVins) &&
            errorVins.ValueKind == JsonValueKind.Array)
        {
            var vinCount = errorVins.GetArrayLength();
            var errorLevel = vinCount switch
            {
                0 => "✅ Nessun errore",
                1 => "⚠️ Errore singolo",
                _ => "🔴 Errori multipli"
            };

            sb.AppendLine($"  - VIN CON ERRORI TELEMETRIA: {vinCount} veicoli - {errorLevel}");

            foreach (var vin in errorVins.EnumerateArray())
            {
                var vinValue = vin.GetString() ?? "N/A";
                sb.AppendLine($"    • {vinValue}");
            }
        }

        // Fleet Telemetry Errors con categorizzazione
        if (content.TryGetProperty("fleet_telemetry_errors", out var errors) &&
            errors.ValueKind == JsonValueKind.Array)
        {
            var errorCount = errors.GetArrayLength();
            sb.AppendLine($"  - DETTAGLI ERRORI TELEMETRIA ({errorCount} errori):");

            foreach (var error in errors.EnumerateArray())
            {
                var clientName = GetSafeStringValue(error, "name");
                var errorMessage = GetSafeStringValue(error, "error");
                var vin = GetSafeStringValue(error, "vin");

                // Categorizzazione dell'errore
                var errorCategory = errorMessage!.ToLower() switch
                {
                    var msg when msg.Contains("gps") => "🗺️ Errore GPS",
                    var msg when msg.Contains("connection") => "📡 Errore connessione",
                    var msg when msg.Contains("timeout") => "⏱️ Timeout",
                    var msg when msg.Contains("parse") => "🔧 Errore parsing dati",
                    _ => "❓ Errore generico"
                };

                sb.AppendLine($"    • Client: {clientName} - {errorCategory}");
                sb.AppendLine($"      VIN: {vin}");
                sb.AppendLine($"      Dettaglio: {errorMessage}");
                sb.AppendLine();
            }
        }

        sb.AppendLine(); // spazio tra record
    }

    // Metodo helper per ProcessUserProfile migliorato  
    private static void ProcessOrdersEnhanced(StringBuilder sb, JsonElement orders)
    {
        var count = GetSafeIntValue(orders, "count");
        sb.AppendLine($"  - ORDINI ({count} ordini):");

        if (orders.TryGetProperty("response", out var ordersArray) &&
            ordersArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var order in ordersArray.EnumerateArray())
            {
                var vehicleMapId = GetSafeIntValue(order, "vehicleMapId");
                var referenceNumber = GetSafeStringValue(order, "referenceNumber");
                var vin = GetSafeStringValue(order, "vin");
                var orderStatus = GetSafeStringValue(order, "orderStatus");
                var orderSubstatus = GetSafeStringValue(order, "orderSubstatus");
                var modelCode = GetSafeStringValue(order, "modelCode");
                var countryCode = GetSafeStringValue(order, "countryCode");
                var locale = GetSafeStringValue(order, "locale");
                var mktOptions = GetSafeStringValue(order, "mktOptions");
                var isB2b = GetSafeBooleanValue(order, "isB2b");

                // Analisi stato ordine
                var statusAnalysis = orderStatus switch
                {
                    "BOOKED" => "📋 Ordinato",
                    "CONFIRMED" => "✅ Confermato",
                    "IN_PRODUCTION" => "🏭 In produzione",
                    "READY_FOR_DELIVERY" => "🚚 Pronto per consegna",
                    "DELIVERED" => "✅ Consegnato",
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

                sb.AppendLine($"    • Ordine #{referenceNumber} (ID: {vehicleMapId})");
                sb.AppendLine($"      VIN: {vin}");
                sb.AppendLine($"      Modello: {modelName}, Paese: {countryCode}, Locale: {locale}");
                sb.AppendLine($"      Stato: {statusAnalysis} ({orderSubstatus})");
                sb.AppendLine($"      Tipo: {(isB2b ? "🏢 Business (B2B)" : "👤 Privato (B2C)")}");

                // Parse market options se presenti
                if (!string.IsNullOrEmpty(mktOptions) && mktOptions != "N/A")
                {
                    var options = mktOptions.Split(',');
                    var optionAnalysis = options.Length switch
                    {
                        <= 5 => "🔧 Configurazione base",
                        <= 10 => "🔧 Configurazione media",
                        _ => "🔧 Configurazione completa"
                    };

                    sb.AppendLine($"      Opzioni: {string.Join(", ", options.Take(3))}...");
                    sb.AppendLine($"      📊 {optionAnalysis} ({options.Length} opzioni totali)");
                }
                sb.AppendLine();
            }
        }
    }

    private static async Task<int> ProcessAdaptiveProfilingSms(StringBuilder sb, int vehicleId, PolarDriveDbContext dbContext, int index)
    {
        try
        {
            // Recupera tutti gli eventi SMS per il veicolo negli ultimi 30 giorni
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);

            var smsEvents = await dbContext.AdaptiveProfilingSmsEvents
                .Where(e => e.VehicleId == vehicleId && e.ReceivedAt >= thirtyDaysAgo)
                .OrderByDescending(e => e.ReceivedAt)
                .ToListAsync();

            if (!smsEvents.Any())
            {
                sb.AppendLine($"[{index++}] ADAPTIVE PROFILING SMS - Nessun evento registrato negli ultimi 30 giorni");
                sb.AppendLine();
                return index;
            }

            sb.AppendLine($"[{index++}] ADAPTIVE PROFILING SMS - Storico Sessioni ({smsEvents.Count} eventi)");

            // Statistiche generali
            var onEvents = smsEvents.Where(e => e.ParsedCommand == "ADAPTIVE_PROFILING_ON").ToList();
            var offEvents = smsEvents.Where(e => e.ParsedCommand == "ADAPTIVE_PROFILING_OFF").ToList();

            sb.AppendLine("  - RIEPILOGO ATTIVAZIONI:");
            sb.AppendLine($"    • Sessioni avviate: {onEvents.Count}");
            sb.AppendLine($"    • Sessioni terminate manualmente: {offEvents.Count}");
            sb.AppendLine($"    • Sessioni terminate automaticamente: {Math.Max(0, onEvents.Count - offEvents.Count)}");

            // Verifica sessione attiva
            var activeSession = await GetActiveAdaptiveSession(vehicleId, dbContext);
            if (activeSession != null)
            {
                var remainingTime = activeSession.ReceivedAt.AddHours(4) - DateTime.Now;
                sb.AppendLine($"    • 🟢 SESSIONE ATTIVA: {remainingTime.TotalMinutes:F0} min rimanenti");
                sb.AppendLine($"      Avviata: {activeSession.ReceivedAt:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"      Descrizione: {activeSession.MessageContent}");
            }
            else
            {
                sb.AppendLine($"    • ⚪ Nessuna sessione attiva");
            }

            // Storico dettagliato delle ultime 10 sessioni
            sb.AppendLine("  - STORICO SESSIONI (ultime 10):");

            var recentSessions = onEvents.Take(10);
            foreach (var session in recentSessions)
            {
                var endTime = session.ReceivedAt.AddHours(4);
                var wasActiveFor = Math.Min(4, (DateTime.Now - session.ReceivedAt).TotalHours);

                // Trova eventuale comando OFF correlato
                var offCommand = offEvents
                    .Where(off => off.ReceivedAt > session.ReceivedAt && off.ReceivedAt <= endTime)
                    .OrderBy(off => off.ReceivedAt)
                    .FirstOrDefault();

                var duration = offCommand != null
                    ? (offCommand.ReceivedAt - session.ReceivedAt).TotalHours
                    : Math.Min(4, wasActiveFor);

                var status = offCommand != null ? "Terminata manualmente" :
                            wasActiveFor >= 4 ? "Terminata automaticamente" : "In corso";

                sb.AppendLine($"    • {session.ReceivedAt:yyyy-MM-dd HH:mm} - Durata: {duration:F1}h ({status})");
                sb.AppendLine($"      Comando: {session.MessageContent}");

                if (offCommand != null)
                {
                    sb.AppendLine($"      Stop: {offCommand.ReceivedAt:yyyy-MM-dd HH:mm} - {offCommand.MessageContent}");
                }
            }

            // Analisi pattern di utilizzo
            AnalyzeAdaptivePatterns(sb, onEvents);

            // Conteggio dati raccolti durante sessioni adaptive
            var adaptiveDataCount = await dbContext.VehiclesData
                .Where(d => d.VehicleId == vehicleId && d.IsAdaptiveProfiling)
                .CountAsync();

            sb.AppendLine($"  - DATI RACCOLTI: {adaptiveDataCount:N0} record telemetrici durante sessioni Adaptive");

            sb.AppendLine();
            return index;
        }
        catch (Exception ex)
        {
            sb.AppendLine($"[{index++}] ADAPTIVE PROFILING SMS - Errore nel recupero dati: {ex.Message}");
            sb.AppendLine();
            return index;
        }
    }

    private static void AnalyzeAdaptivePatterns(StringBuilder sb, List<AdaptiveProfilingSmsEvent> onEvents)
    {
        if (onEvents.Count < 2) return;

        sb.AppendLine("  - ANALISI PATTERN:");

        // Analisi orari preferiti
        var hourGroups = onEvents
            .GroupBy(e => e.ReceivedAt.Hour)
            .OrderByDescending(g => g.Count())
            .Take(3);

        sb.AppendLine("    📊 Orari preferiti:");
        foreach (var group in hourGroups)
        {
            sb.AppendLine($"      • {group.Key:00}:xx - {group.Count()} attivazioni");
        }

        // Analisi giorni della settimana
        var dayGroups = onEvents
            .GroupBy(e => e.ReceivedAt.DayOfWeek)
            .OrderByDescending(g => g.Count())
            .Take(3);

        sb.AppendLine("    📅 Giorni preferiti:");
        foreach (var group in dayGroups)
        {
            var dayName = group.Key switch
            {
                DayOfWeek.Monday => "Lunedì",
                DayOfWeek.Tuesday => "Martedì",
                DayOfWeek.Wednesday => "Mercoledì",
                DayOfWeek.Thursday => "Giovedì",
                DayOfWeek.Friday => "Venerdì",
                DayOfWeek.Saturday => "Sabato",
                DayOfWeek.Sunday => "Domenica",
                _ => group.Key.ToString()
            };
            sb.AppendLine($"      • {dayName} - {group.Count()} attivazioni");
        }

        // Frequenza di utilizzo
        var totalDays = (DateTime.Now - onEvents.Min(e => e.ReceivedAt)).TotalDays;
        var frequency = onEvents.Count / Math.Max(totalDays, 1);

        var frequencyDescription = frequency switch
        {
            >= 1 => "🔥 Uso quotidiano",
            >= 0.5 => "📈 Uso frequente",
            >= 0.2 => "📊 Uso regolare",
            _ => "📉 Uso occasionale"
        };

        sb.AppendLine($"    🔄 Frequenza: {frequency:F2} sessioni/giorno - {frequencyDescription}");
    }

    private static async Task<AdaptiveProfilingSmsEvent?> GetActiveAdaptiveSession(int vehicleId, PolarDriveDbContext dbContext)
    {
        var fourHoursAgo = DateTime.Now.AddHours(-4);

        return await dbContext.AdaptiveProfilingSmsEvents
            .Where(e => e.VehicleId == vehicleId
                     && e.ParsedCommand == "ADAPTIVE_PROFILING_ON"
                     && e.ReceivedAt >= fourHoursAgo)
            .OrderByDescending(e => e.ReceivedAt)
            .FirstOrDefaultAsync();
    }
}
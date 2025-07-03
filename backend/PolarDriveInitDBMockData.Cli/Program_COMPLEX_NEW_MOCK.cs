// using Microsoft.EntityFrameworkCore;
// using PolarDrive.Data.DbContexts;
// using PolarDrive.Data.Entities;
// using System.Text.Json;

// var basePath = AppContext.BaseDirectory;
// var dbPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "PolarDriveInitDB.Cli", "datapolar.db"));
// var wwwRoot = Path.Combine(basePath, "..", "..", "..", "..", "PolarDrive.WebApi", "wwwroot");
// var storageRoot = Path.Combine(wwwRoot, "storage");

// var options = new DbContextOptionsBuilder<PolarDriveDbContext>()
//     .UseSqlite($"Data Source={dbPath}")
//     .Options;

// using var db = new PolarDriveDbContext(options);

// try
// {
//     // ✅ Inizializza il logger DOPO aver verificato la connessione
//     if (!await db.Database.CanConnectAsync())
//     {
//         Console.WriteLine("❌ Cannot connect to database. Make sure to run PolarDriveInitDB.Cli first.");
//         return;
//     }

//     var logger = new PolarDriveLogger(db);
//     await logger.Info("PolarDriveInitDBMockData.Cli", "Starting comprehensive mock DB setup");

//     Console.WriteLine("🧹 Starting full cleanup of mock data...");

//     // ───────────────────────────────
//     // 1. ✅ CLEANUP INTELLIGENTE
//     // ───────────────────────────────
//     try
//     {
//         var tableNames = new[]
//         {
//             "AnonymizedVehiclesData",
//             "VehiclesData",
//             "AdaptiveProfilingSmsEvents",
//             "SmsAuditLogs",
//             "PhoneVehicleMappings",
//             "ClientTokens",
//             "PdfReports",
//             "ClientConsents",
//             "OutagePeriods",
//             "AdminFileManager",
//             "ClientVehicles",
//             "ClientCompanies"
//         };

//         foreach (var tableName in tableNames)
//         {
//             try
//             {
//                 Console.WriteLine($"🗑️ Clearing {tableName}...");
//                 await db.Database.ExecuteSqlAsync($"DELETE FROM {tableName}");
//                 Console.WriteLine($"✅ Cleared {tableName}");
//             }
//             catch (Exception tableEx)
//             {
//                 Console.WriteLine($"⚠️ Table {tableName} not found or already empty: {tableEx.Message}");
//                 await logger.Warning("PolarDriveInitDBMockData.Cli", $"Table cleanup warning for {tableName}", tableEx.Message);
//             }
//         }

//         // Reset sequenze SQLite
//         try
//         {
//             await db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence");
//             Console.WriteLine("✅ Reset all sequence counters");
//         }
//         catch (Exception seqEx)
//         {
//             Console.WriteLine($"⚠️ Sequence reset warning: {seqEx.Message}");
//         }

//         await logger.Info("PolarDriveInitDBMockData.Cli", "Cleanup completed successfully");
//     }
//     catch (Exception cleanupEx)
//     {
//         Console.WriteLine($"⚠️ Cleanup had issues: {cleanupEx.Message}");
//         await logger.Warning("PolarDriveInitDBMockData.Cli", "Cleanup had warnings", cleanupEx.Message);
//     }

//     // ───────────────────────────────
//     // 2. 🏢 AZIENDE MOCK ESTESE
//     // ───────────────────────────────
//     Console.WriteLine("🏢 Creating comprehensive mock companies...");

//     var companies = new[]
//     {
//         new ClientCompany
//         {
//             Name = "Paninoteca Rossi",
//             VatNumber = "00000000001",
//             Address = "Via Roma 123, 20100 Milano MI",
//             Email = "info@paninotecarossi.com",
//             PecAddress = "pec@paninotecarossi.com",
//             LandlineNumber = "0212345678",
//             ReferentName = "Luca Rossi",
//             ReferentEmail = "luca@paninotecarossi.com",
//             ReferentMobileNumber = "3201234567",
//             ReferentPecAddress = "luca.rossi@pec.paninotecarossi.com",
//             CreatedAt = DateTime.Now.AddDays(-180)
//         },
//         new ClientCompany
//         {
//             Name = "TechFlow Solutions SRL",
//             VatNumber = "01234567890",
//             Address = "Corso Italia 45, 10121 Torino TO",
//             Email = "contact@techflow.it",
//             PecAddress = "techflow@pec.techflow.it",
//             LandlineNumber = "0115556789",
//             ReferentName = "Maria Bianchi",
//             ReferentEmail = "m.bianchi@techflow.it",
//             ReferentMobileNumber = "3389876543",
//             ReferentPecAddress = "maria.bianchi@pec.techflow.it",
//             CreatedAt = DateTime.Now.AddDays(-90)
//         },
//         new ClientCompany
//         {
//             Name = "Green Logistics Express",
//             VatNumber = "09876543210",
//             Address = "Via del Porto 88, 16100 Genova GE",
//             Email = "logistics@greenexpress.it",
//             PecAddress = "green@pec.greenexpress.it",
//             LandlineNumber = "0102223344",
//             ReferentName = "Giuseppe Verdi",
//             ReferentEmail = "g.verdi@greenexpress.it",
//             ReferentMobileNumber = "3456789012",
//             CreatedAt = DateTime.Now.AddDays(-45)
//         },
//         new ClientCompany
//         {
//             Name = "AutoService Roma Center",
//             VatNumber = "55566677788",
//             Address = "Via Appia Nuova 200, 00179 Roma RM",
//             Email = "service@autoroma.it",
//             PecAddress = "autoservice@pec.autoroma.it",
//             LandlineNumber = "0667890123",
//             ReferentName = "Franco Neri",
//             ReferentEmail = "f.neri@autoroma.it",
//             ReferentMobileNumber = "3205554321",
//             CreatedAt = DateTime.Now.AddDays(-20)
//         },
//         new ClientCompany
//         {
//             Name = "FleetManager Pro",
//             VatNumber = "11122233344",
//             Address = "Viale Europa 15, 40100 Bologna BO",
//             Email = "fleet@managerpro.it",
//             LandlineNumber = "0517778899",
//             ReferentName = "Anna Gialli",
//             ReferentEmail = "a.gialli@managerpro.it",
//             ReferentMobileNumber = "3331112233",
//             CreatedAt = DateTime.Now.AddDays(-5)
//         }
//     };

//     db.ClientCompanies.AddRange(companies);
//     await db.SaveChangesAsync();
//     Console.WriteLine($"✅ Created {companies.Length} comprehensive mock companies");
//     await logger.Info("PolarDriveInitDBMockData.Cli", $"Inserted {companies.Length} mock companies");

//     // ───────────────────────────────
//     // 3. 🚗 VEICOLI MOCK VARIEGATI
//     // ───────────────────────────────
//     Console.WriteLine("🚗 Creating diverse mock vehicles...");

//     var vehicles = new List<ClientVehicle>();
//     var random = new Random();

//     // Dati per generare veicoli realistici
//     var brands = new[] { "Tesla", "BMW", "Audi", "Mercedes", "Volkswagen", "Ford", "Renault", "Nissan", "Hyundai", "Volvo" };
//     var models = new Dictionary<string, string[]>
//     {
//         ["Tesla"] = ["Model 3", "Model S", "Model X", "Model Y"],
//         ["BMW"] = ["i3", "iX3", "i4", "iX"],
//         ["Audi"] = ["e-tron", "e-tron GT", "Q4 e-tron"],
//         ["Mercedes"] = ["EQC", "EQS", "EQA", "EQB"],
//         ["Volkswagen"] = ["ID.3", "ID.4", "e-Golf", "e-up!"],
//         ["Ford"] = ["Mustang Mach-E", "E-Transit"],
//         ["Renault"] = ["Zoe", "Twingo E-Tech"],
//         ["Nissan"] = ["Leaf", "Ariya"],
//         ["Hyundai"] = ["Kona Electric", "IONIQ 5"],
//         ["Volvo"] = ["XC40 Recharge", "C40 Recharge"]
//     };
//     var colors = new[] { "Bianco Perla", "Nero Metallizzato", "Grigio Titanio", "Blu Oceano", "Rosso Fiamma", "Verde Smeraldo", "Silver" };
//     var fuelTypes = new[] { "Electric", "Hybrid", "Plug-in Hybrid" };

//     // Genera 15-20 veicoli distribuiti tra le aziende
//     for (int i = 0; i < 18; i++)
//     {
//         var company = companies[i % companies.Length];
//         var brand = brands[random.Next(brands.Length)];
//         var model = models[brand][random.Next(models[brand].Length)];

//         var vehicle = new ClientVehicle
//         {
//             ClientCompanyId = company.Id,
//             Vin = GenerateRealisticVin(brand, i + 1),
//             Brand = brand,
//             Model = model,
//             Trim = GenerateRandomTrim(),
//             Color = colors[random.Next(colors.Length)],
//             FuelType = fuelTypes[random.Next(fuelTypes.Length)],
//             IsActiveFlag = random.NextDouble() > 0.2, // 80% attivi
//             IsFetchingDataFlag = random.NextDouble() > 0.3, // 70% in fetching
//             ClientOAuthAuthorized = random.NextDouble() > 0.15, // 85% autorizzati
//             FirstActivationAt = company.CreatedAt.AddDays(random.Next(1, 30)),
//             LastDeactivationAt = random.NextDouble() > 0.7 ? DateTime.Now.AddDays(-random.Next(1, 30)) : null,
//             LastFetchingDataAt = DateTime.Now.AddMinutes(-random.Next(1, 1440)),
//             LastDataUpdate = DateTime.Now.AddMinutes(-random.Next(1, 180)),
//             CreatedAt = company.CreatedAt.AddDays(random.Next(1, 45))
//         };

//         vehicles.Add(vehicle);
//     }

//     db.ClientVehicles.AddRange(vehicles);
//     await db.SaveChangesAsync();
//     Console.WriteLine($"✅ Created {vehicles.Count} diverse mock vehicles");
//     await logger.Info("PolarDriveInitDBMockData.Cli", $"Inserted {vehicles.Count} mock vehicles");

//     // ───────────────────────────────
//     // 4. 🔑 CLIENT TOKENS
//     // ───────────────────────────────
//     Console.WriteLine("🔑 Creating mock client tokens...");

//     var tokens = new List<ClientToken>();
//     foreach (var vehicle in vehicles.Where(v => v.ClientOAuthAuthorized))
//     {
//         var token = new ClientToken
//         {
//             VehicleId = vehicle.Id,
//             AccessToken = $"mock_access_token_{vehicle.Id}_{Guid.NewGuid().ToString("N")[..8]}",
//             RefreshToken = $"mock_refresh_token_{vehicle.Id}_{Guid.NewGuid().ToString("N")[..8]}",
//             AccessTokenExpiresAt = DateTime.Now.AddHours(random.Next(1, 24)),
//             RefreshTokenExpiresAt = DateTime.Now.AddDays(random.Next(7, 30)),
//             CreatedAt = vehicle.CreatedAt.AddDays(1),
//             UpdatedAt = DateTime.Now.AddHours(-random.Next(1, 72))
//         };
//         tokens.Add(token);
//     }

//     db.ClientTokens.AddRange(tokens);
//     await db.SaveChangesAsync();
//     Console.WriteLine($"✅ Created {tokens.Count} mock client tokens");

//     // ───────────────────────────────
//     // 5. 📱 PHONE MAPPINGS ESTESI
//     // ───────────────────────────────
//     Console.WriteLine("📱 Creating extended phone mappings...");

//     var phoneMappings = new List<PhoneVehicleMapping>();
//     var phoneNumbers = new[]
//     {
//         "+393334455666", "+393201234567", "+393389876543", "+393456789012",
//         "+393205554321", "+393331112233", "+393478901234", "+393665544332"
//     };

//     for (int i = 0; i < Math.Min(phoneNumbers.Length, vehicles.Count); i++)
//     {
//         var mapping = new PhoneVehicleMapping
//         {
//             PhoneNumber = phoneNumbers[i],
//             VehicleId = vehicles[i].Id,
//             CreatedAt = vehicles[i].CreatedAt.AddDays(2),
//             UpdatedAt = DateTime.Now.AddDays(-random.Next(1, 15)),
//             IsActive = random.NextDouble() > 0.1, // 90% attivi
//             Notes = $"Telefono associato per SMS - {vehicles[i].Brand} {vehicles[i].Model}"
//         };
//         phoneMappings.Add(mapping);
//     }

//     db.PhoneVehicleMappings.AddRange(phoneMappings);
//     await db.SaveChangesAsync();
//     Console.WriteLine($"✅ Created {phoneMappings.Count} phone mappings");

//     // ───────────────────────────────
//     // 6. 📲 SMS AUDIT LOGS ESTESI
//     // ───────────────────────────────
//     Console.WriteLine("📲 Creating comprehensive SMS audit logs...");

//     var smsLogs = new List<SmsAuditLog>();
//     var statuses = new[] { "SUCCESS", "ERROR", "REJECTED" };
//     var smsCommands = new[] { "ADAPTIVE 0001", "ADAPTIVE 0000", "STATUS", "INFO" };

//     for (int i = 0; i < 25; i++)
//     {
//         var phoneMapping = phoneMappings[random.Next(phoneMappings.Count)];
//         var status = statuses[random.Next(statuses.Length)];
//         var command = smsCommands[random.Next(smsCommands.Length)];

//         var smsLog = new SmsAuditLog
//         {
//             MessageSid = $"SM_MOCK_{status}_{i + 1:D5}",
//             FromPhoneNumber = phoneMapping.PhoneNumber,
//             ToPhoneNumber = "+393901234567", // Numero DataPolar
//             MessageBody = $"{command} session {i + 1}",
//             ReceivedAt = DateTime.Now.AddDays(-random.Next(0, 60)).AddMinutes(-random.Next(1, 1440)),
//             ProcessingStatus = status,
//             ErrorMessage = status == "ERROR" ? "Veicolo non trovato o non autorizzato" : null,
//             VehicleIdResolved = status != "REJECTED" ? phoneMapping.VehicleId : null,
//             ResponseSent = GenerateSmsResponse(status, command, phoneMapping.VehicleId)
//         };
//         smsLogs.Add(smsLog);
//     }

//     db.SmsAuditLogs.AddRange(smsLogs);
//     await db.SaveChangesAsync();
//     Console.WriteLine($"✅ Created {smsLogs.Count} SMS audit logs");

//     // ───────────────────────────────
//     // 7. 🎯 ADAPTIVE PROFILING EVENTS
//     // ───────────────────────────────
//     Console.WriteLine("🎯 Creating adaptive profiling SMS events...");

//     var adaptiveEvents = new List<AdaptiveProfilingSmsEvent>();
//     var commands = new[] { "ADAPTIVE_PROFILING_ON", "ADAPTIVE_PROFILING_OFF" };

//     foreach (var smsLog in smsLogs.Where(s => s.ProcessingStatus == "SUCCESS" && s.MessageBody.Contains("ADAPTIVE")))
//     {
//         var command = smsLog.MessageBody.Contains("0001") ? "ADAPTIVE_PROFILING_ON" : "ADAPTIVE_PROFILING_OFF";
//         var adaptiveEvent = new AdaptiveProfilingSmsEvent
//         {
//             VehicleId = smsLog.VehicleIdResolved!.Value,
//             ReceivedAt = smsLog.ReceivedAt,
//             MessageContent = smsLog.MessageBody,
//             ParsedCommand = command
//         };
//         adaptiveEvents.Add(adaptiveEvent);
//     }

//     db.AdaptiveProfilingSmsEvents.AddRange(adaptiveEvents);
//     await db.SaveChangesAsync();
//     Console.WriteLine($"✅ Created {adaptiveEvents.Count} adaptive profiling events");

//     // ───────────────────────────────
//     // 8. 🔐 CONSENSI ESTESI
//     // ───────────────────────────────
//     Console.WriteLine("🔐 Creating comprehensive consents...");

//     var consents = new List<ClientConsent>();
//     var consentTypes = new[] { "Consent Activation", "Consent Deactivation", "Consent Stop Data Fetching", "Consent Reactivation" };

//     // Crea cartelle storage se non esistono
//     EnsureStorageDirectories(storageRoot, companies);

//     foreach (var vehicle in vehicles)
//     {
//         var numConsents = random.Next(1, 4); // 1-3 consensi per veicolo
//         for (int i = 0; i < numConsents; i++)
//         {
//             var consentType = consentTypes[random.Next(consentTypes.Length)];
//             var uploadDate = vehicle.CreatedAt.AddDays(random.Next(0, 30));

//             var consent = new ClientConsent
//             {
//                 ClientCompanyId = vehicle.ClientCompanyId,
//                 VehicleId = vehicle.Id,
//                 ConsentType = consentType,
//                 UploadDate = uploadDate,
//                 ZipFilePath = $"companies/company-{vehicle.ClientCompanyId}/consents-zips/consent_{vehicle.Id}_{i + 1}.zip",
//                 ConsentHash = GenerateConsentHash(vehicle.Id, i + 1),
//                 Notes = $"Consenso {consentType} per veicolo {vehicle.Vin}"
//             };
//             consents.Add(consent);
//         }
//     }

//     db.ClientConsents.AddRange(consents);
//     await db.SaveChangesAsync();
//     Console.WriteLine($"✅ Created {consents.Count} mock consents");

//     // ───────────────────────────────
//     // 9. 📊 PDF REPORTS ESTESI
//     // ───────────────────────────────
//     Console.WriteLine("📊 Creating comprehensive PDF reports...");

//     var pdfReports = new List<PdfReport>();
//     var reportStatuses = new[] { "GENERATED", "PENDING", "ERROR", "REGENERATED" };

//     foreach (var vehicle in vehicles)
//     {
//         var numReports = random.Next(2, 8); // 2-7 report per veicolo
//         var startDate = vehicle.FirstActivationAt ?? vehicle.CreatedAt;

//         for (int i = 0; i < numReports; i++)
//         {
//             var periodStart = startDate.AddDays(i * 30);
//             var periodEnd = periodStart.AddDays(29);
//             var status = reportStatuses[random.Next(reportStatuses.Length)];

//             var report = new PdfReport
//             {
//                 ClientCompanyId = vehicle.ClientCompanyId,
//                 ClientVehicleId = vehicle.Id,
//                 ReportPeriodStart = periodStart,
//                 ReportPeriodEnd = periodEnd,
//                 GeneratedAt = status == "GENERATED" || status == "REGENERATED" ?
//                     periodEnd.AddDays(random.Next(1, 5)) : null,
//                 Status = status,
//                 RegenerationCount = status == "REGENERATED" ? random.Next(1, 3) : 0,
//                 Notes = $"Report {status} per periodo {periodStart:yyyy-MM-dd} - {periodEnd:yyyy-MM-dd}",
//                 CreatedAt = periodStart.AddDays(25)
//             };
//             pdfReports.Add(report);
//         }
//     }

//     db.PdfReports.AddRange(pdfReports);
//     await db.SaveChangesAsync();
//     Console.WriteLine($"✅ Created {pdfReports.Count} PDF reports");

//     // ───────────────────────────────
//     // 10. ⚠️ OUTAGE PERIODS ESTESI
//     // ───────────────────────────────
//     Console.WriteLine("⚠️ Creating comprehensive outage periods...");

//     var outages = new List<OutagePeriod>();
//     var outageTypes = new[] { "Outage Vehicle", "Outage Fleet Api" };
//     var outageReasons = new[]
//     {
//         "Manutenzione programmata", "Disconnessione OBD", "Errore API Tesla",
//         "Veicolo spento prolungato", "Problemi di connettività", "Aggiornamento software"
//     };

//     // Outages per veicoli specifici
//     foreach (var vehicle in vehicles.Take(12)) // Solo alcuni veicoli hanno outages
//     {
//         var numOutages = random.Next(1, 4);
//         for (int i = 0; i < numOutages; i++)
//         {
//             var outageStart = vehicle.CreatedAt.AddDays(random.Next(5, 60));
//             var isActive = i == numOutages - 1 && random.NextDouble() > 0.7; // 30% chance ultima sia attiva

//             var outage = new OutagePeriod
//             {
//                 AutoDetected = random.NextDouble() > 0.3, // 70% auto-detected
//                 OutageType = outageTypes[0], // Vehicle
//                 OutageBrand = vehicle.Brand,
//                 CreatedAt = outageStart.AddMinutes(random.Next(5, 120)),
//                 OutageStart = outageStart,
//                 OutageEnd = isActive ? null : outageStart.AddHours(random.Next(1, 48)),
//                 VehicleId = vehicle.Id,
//                 ClientCompanyId = vehicle.ClientCompanyId,
//                 ZipFilePath = $"companies/company-{vehicle.ClientCompanyId}/outages-zips/outage_{vehicle.Id}_{i + 1}.zip",
//                 Notes = outageReasons[random.Next(outageReasons.Length)]
//             };
//             outages.Add(outage);
//         }
//     }

//     // Outages Fleet API (senza veicolo specifico)
//     for (int i = 0; i < 5; i++)
//     {
//         var company = companies[random.Next(companies.Length)];
//         var outageStart = DateTime.Now.AddDays(-random.Next(1, 30));

//         var fleetOutage = new OutagePeriod
//         {
//             AutoDetected = true,
//             OutageType = outageTypes[1], // Fleet API
//             OutageBrand = "Tesla", // API Tesla down
//             CreatedAt = outageStart.AddMinutes(random.Next(1, 60)),
//             OutageStart = outageStart,
//             OutageEnd = outageStart.AddHours(random.Next(2, 12)),
//             VehicleId = null,
//             ClientCompanyId = company.Id,
//             ZipFilePath = $"companies/company-{company.Id}/outages-zips/fleet_outage_{i + 1}.zip",
//             Notes = "Interruzione servizio API Tesla Fleet"
//         };
//         outages.Add(fleetOutage);
//     }

//     db.OutagePeriods.AddRange(outages);
//     await db.SaveChangesAsync();
//     Console.WriteLine($"✅ Created {outages.Count} outage periods");

//     // ───────────────────────────────
//     // 11. 📈 VEHICLE DATA ESTESI
//     // ───────────────────────────────
//     Console.WriteLine("📈 Creating comprehensive vehicle data...");

//     var vehicleDataEntries = new List<VehicleData>();

//     foreach (var vehicle in vehicles.Where(v => v.IsFetchingDataFlag).Take(8)) // Solo alcuni per performance
//     {
//         var dataCount = random.Next(50, 200); // 50-200 entries per veicolo
//         var startTime = vehicle.FirstActivationAt ?? vehicle.CreatedAt;

//         for (int i = 0; i < dataCount; i++)
//         {
//             var timestamp = startTime.AddMinutes(i * random.Next(5, 30));
//             var isAdaptive = adaptiveEvents.Any(ae => ae.VehicleId == vehicle.Id &&
//                 ae.ReceivedAt <= timestamp && ae.ParsedCommand == "ADAPTIVE_PROFILING_ON");

//             var vehicleData = new VehicleData
//             {
//                 VehicleId = vehicle.Id,
//                 Timestamp = timestamp,
//                 IsAdaptiveProfiling = isAdaptive,
//                 RawJson = GenerateRealisticVehicleDataJson(vehicle, timestamp, isAdaptive)
//             };
//             vehicleDataEntries.Add(vehicleData);
//         }
//     }

//     db.VehiclesData.AddRange(vehicleDataEntries);
//     await db.SaveChangesAsync();
//     Console.WriteLine($"✅ Created {vehicleDataEntries.Count} vehicle data entries");

//     // ───────────────────────────────
//     // 12. 🔒 ANONYMIZED DATA
//     // ───────────────────────────────
//     Console.WriteLine("🔒 Creating anonymized vehicle data...");

//     var anonymizedData = new List<AnonymizedVehicleData>();

//     foreach (var originalData in vehicleDataEntries.Take(100)) // Anonimizza solo parte dei dati
//     {
//         var anonymized = new AnonymizedVehicleData
//         {
//             OriginalDataId = originalData.Id,
//             VehicleId = originalData.VehicleId,
//             Timestamp = originalData.Timestamp,
//             AnonymizedJson = AnonymizeVehicleData(originalData.RawJson)
//         };
//         anonymizedData.Add(anonymized);
//     }

//     db.AnonymizedVehiclesData.AddRange(anonymizedData);
//     await db.SaveChangesAsync();
//     Console.WriteLine($"✅ Created {anonymizedData.Count} anonymized data entries");

//     // ───────────────────────────────
//     // 13. 📁 ADMIN FILE MANAGER
//     // ───────────────────────────────
//     Console.WriteLine("📁 Creating admin file manager requests...");

//     var adminRequests = new List<AdminFileManager>();
//     var requestStatuses = new[] { "COMPLETED", "PENDING", "ERROR", "PROCESSING" };

//     for (int i = 0; i < 8; i++)
//     {
//         var status = requestStatuses[random.Next(requestStatuses.Length)];
//         var periodStart = DateTime.Now.AddDays(-random.Next(30, 90));
//         var periodEnd = periodStart.AddDays(30);
//         var requestedAt = periodEnd.AddDays(random.Next(1, 10));

//         var request = new AdminFileManager
//         {
//             RequestedAt = requestedAt,
//             StartedAt = status != "PENDING" ? requestedAt.AddMinutes(random.Next(1, 30)) : null,
//             CompletedAt = status == "COMPLETED" ? requestedAt.AddMinutes(random.Next(30, 120)) : null,
//             PeriodStart = periodStart,
//             PeriodEnd = periodEnd,
//             CompanyList = companies.Take(random.Next(1, 4)).Select(c => c.Name).ToList(),
//             VinList = vehicles.Take(random.Next(2, 6)).Select(v => v.Vin).ToList(),
//             BrandList = brands.Take(random.Next(1, 3)).ToList(),
//             Status = status,
//             TotalPdfCount = random.Next(50, 200),
//             IncludedPdfCount = random.Next(30, 150),
//             ZipFileSizeMB = (decimal)(random.NextDouble() * 100 + 10), // 10-110 MB
//             ResultZipPath = status == "COMPLETED" ? $"admin-exports/export_{i + 1}_{DateTime.Now:yyyyMMdd}.zip" : null,
//             Notes = status == "ERROR" ? "Errore durante la generazione del file ZIP" :
//                     status == "COMPLETED" ? "Export completato con successo" : null,
//             RequestedBy = $"admin.user{random.Next(1, 4)}@datapolar.it"
//         };
//         adminRequests.Add(request);
//     }

//     db.AdminFileManager.AddRange(adminRequests);
//     await db.SaveChangesAsync();
//     Console.WriteLine($"✅ Created {adminRequests.Count} admin file manager requests");

//     // ───────────────────────────────
//     // 🎉 RIEPILOGO FINALE ESTESO
//     // ───────────────────────────────
//     Console.WriteLine();
//     Console.WriteLine("🎉 ========== COMPREHENSIVE MOCK DATA SETUP COMPLETED ========== 🎉");
//     Console.WriteLine();
//     Console.WriteLine($"🏢 Companies: {companies.Length}");
//     foreach (var company in companies)
//     {
//         var companyVehicles = vehicles.Where(v => v.ClientCompanyId == company.Id).Count();
//         Console.WriteLine($"   • {company.Name} (ID: {company.Id}) - {companyVehicles} veicoli");
//     }
//     Console.WriteLine();
//     Console.WriteLine($"🚗 Vehicles: {vehicles.Count} (Active: {vehicles.Count(v => v.IsActiveFlag)}, Fetching: {vehicles.Count(v => v.IsFetchingDataFlag)})");
//     Console.WriteLine($"🔑 Tokens: {tokens.Count}");
//     Console.WriteLine($"📱 Phone Mappings: {phoneMappings.Count}");
//     Console.WriteLine($"📲 SMS Logs: {smsLogs.Count} (Success: {smsLogs.Count(s => s.ProcessingStatus == "SUCCESS")})");
//     Console.WriteLine($"🎯 Adaptive Events: {adaptiveEvents.Count}");
//     Console.WriteLine($"🔐 Consents: {consents.Count}");
//     Console.WriteLine($"📊 PDF Reports: {pdfReports.Count} (Generated: {pdfReports.Count(r => r.Status == "GENERATED")})");
//     Console.WriteLine($"⚠️ Outages: {outages.Count} (Active: {outages.Count(o => o.OutageEnd == null)})");
//     Console.WriteLine($"📈 Vehicle Data: {vehicleDataEntries.Count} entries");
//     Console.WriteLine($"🔒 Anonymized Data: {anonymizedData.Count} entries");
//     Console.WriteLine($"📁 Admin Requests: {adminRequests.Count}");
//     Console.WriteLine();
//     Console.WriteLine("🚀 TESTING SCENARIOS AVAILABLE:");
//     Console.WriteLine("   📱 SMS Testing:");
//     foreach (var mapping in phoneMappings.Take(3))
//     {
//         var vehicle = vehicles.First(v => v.Id == mapping.VehicleId);
//         Console.WriteLine($"      • From: {mapping.PhoneNumber} → {vehicle.Brand} {vehicle.Model} (VIN: {vehicle.Vin})");
//     }
//     Console.WriteLine("   📊 Report Testing: Multiple periods and statuses available");
//     Console.WriteLine("   ⚠️ Outage Testing: Both vehicle-specific and fleet-wide outages");
//     Console.WriteLine("   🔐 Consent Testing: Multiple consent types per vehicle");
//     Console.WriteLine("   📈 Data Analysis: Vehicle data with Adaptive Profiling flags");
//     Console.WriteLine("   📁 Admin Export: Various request statuses to test");
//     Console.WriteLine("================================================================");

//     await logger.Info("PolarDriveInitDBMockData.Cli", "Comprehensive mock data setup completed successfully");
// }
// catch (Exception ex)
// {
//     Console.WriteLine($"❌ FATAL ERROR: {ex.Message}");
//     if (ex.InnerException != null)
//     {
//         Console.WriteLine($"🔍 Inner: {ex.InnerException.Message}");
//     }

//     Console.WriteLine($"🔍 Stack Trace: {ex.StackTrace}");

//     try
//     {
//         if (await db.Database.CanConnectAsync())
//         {
//             var logger = new PolarDriveLogger(db);
//             await logger.Error("PolarDriveInitDBMockData.Cli", "Exception during comprehensive mock setup", ex.ToString());
//         }
//     }
//     catch
//     {
//         Console.WriteLine("⚠️ Could not log error to database");
//     }
// }

// // ═══════════════════════════════════════════════════════════════════════════════════
// // METODI HELPER PER GENERAZIONE DATI REALISTICI
// // ═══════════════════════════════════════════════════════════════════════════════════

// static string GenerateRealisticVin(string brand, int sequence)
// {
//     var brandCodes = new Dictionary<string, string>
//     {
//         ["Tesla"] = "5YJ3",
//         ["BMW"] = "WBA3",
//         ["Audi"] = "WA1A",
//         ["Mercedes"] = "WDD2",
//         ["Volkswagen"] = "WVW2",
//         ["Ford"] = "1FA6",
//         ["Renault"] = "VF1J",
//         ["Nissan"] = "1N4A",
//         ["Hyundai"] = "KMH5",
//         ["Volvo"] = "YV4A"
//     };

//     var code = brandCodes.GetValueOrDefault(brand, "TEST");
//     return $"{code}{sequence:D6}MOCK{sequence:D5}";
// }

// static string GenerateRandomTrim()
// {
//     var trims = new[] { "Base", "Long Range", "Performance", "Premium", "Sport", "Luxury", "Executive", "Advanced" };
//     return trims[new Random().Next(trims.Length)];
// }

// static string GenerateSmsResponse(string status, string command, int vehicleId)
// {
//     if (status != "SUCCESS") return null;

//     var time = DateTime.Now.AddHours(4).ToString("HH:mm");

//     return command.Contains("0001") ?
//         $@"<?xml version=""1.0"" encoding=""UTF-8""?>
// <Response>
//     <Message>✅ Adaptive Profiling ATTIVATO per 4 ore
// 🚗 Veicolo ID: {vehicleId}
// ⏰ Attivo fino alle: {time} UTC
// 📊 Modalità: Adaptive Profiling ATTIVA
// </Message>
// </Response>" :
//         $@"<?xml version=""1.0"" encoding=""UTF-8""?>
// <Response>
//     <Message>⏹️ Adaptive Profiling DISATTIVATO
// 🚗 Veicolo ID: {vehicleId}
// 📊 Modalità: Normale
// </Message>
// </Response>";
// }

// static string GenerateConsentHash(int vehicleId, int sequence)
// {
//     return $"mock_hash_{vehicleId}_{sequence}_{Guid.NewGuid().ToString("N")[..16]}";
// }

// static void EnsureStorageDirectories(string storageRoot, ClientCompany[] companies)
// {
//     try
//     {
//         if (!Directory.Exists(storageRoot))
//             Directory.CreateDirectory(storageRoot);

//         var directories = new[]
//         {
//             "client-profiles",
//             "consents-zips",
//             "dev-reports",
//             "filemanager-zips",
//             "outages-zips",
//             "reports",
//             "admin-exports"
//         };

//         foreach (var dir in directories)
//         {
//             var fullPath = Path.Combine(storageRoot, dir);
//             if (!Directory.Exists(fullPath))
//                 Directory.CreateDirectory(fullPath);
//         }

//         // Crea cartelle per le aziende
//         foreach (var company in companies)
//         {
//             var companyPath = Path.Combine(storageRoot, "companies", $"company-{company.Id}");
//             if (!Directory.Exists(companyPath))
//                 Directory.CreateDirectory(companyPath);

//             var subDirs = new[] { "consents-zips", "outages-zips", "reports" };
//             foreach (var subDir in subDirs)
//             {
//                 var subPath = Path.Combine(companyPath, subDir);
//                 if (!Directory.Exists(subPath))
//                     Directory.CreateDirectory(subPath);
//             }
//         }
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"⚠️ Warning: Could not create storage directories: {ex.Message}");
//     }
// }

// static string GenerateRealisticVehicleDataJson(ClientVehicle vehicle, DateTime timestamp, bool isAdaptive)
// {
//     var random = new Random();

//     var baseData = new
//     {
//         vin = vehicle.Vin,
//         timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"),
//         location = new
//         {
//             latitude = 45.4642 + (random.NextDouble() - 0.5) * 0.1, // Milano area
//             longitude = 9.1900 + (random.NextDouble() - 0.5) * 0.1,
//             heading = random.Next(0, 360),
//             speed = random.Next(0, 120)
//         },
//         battery = new
//         {
//             level = random.Next(20, 100),
//             range = random.Next(50, 400),
//             charging = random.NextDouble() > 0.8,
//             chargeRate = random.NextDouble() > 0.8 ? random.Next(1, 50) : 0
//         },
//         climate = new
//         {
//             insideTemp = random.Next(18, 28),
//             outsideTemp = random.Next(-5, 35),
//             hvacOn = random.NextDouble() > 0.5
//         },
//         vehicle = new
//         {
//             locked = random.NextDouble() > 0.3,
//             odometer = random.Next(1000, 50000),
//             softwareVersion = $"2024.{random.Next(1, 20)}.{random.Next(1, 10)}",
//             isUserPresent = random.NextDouble() > 0.4
//         },
//         adaptiveProfiling = isAdaptive ? new
//         {
//             active = true,
//             sessionStart = timestamp.AddHours(-random.Next(0, 4)).ToString("yyyy-MM-ddTHH:mm:ssZ"),
//             dataPoints = random.Next(50, 200),
//             highFrequency = true
//         } : null
//     };

//     return JsonSerializer.Serialize(baseData, new JsonSerializerOptions { WriteIndented = false });
// }

// static string AnonymizeVehicleData(string originalJson)
// {
//     try
//     {
//         var data = JsonSerializer.Deserialize<JsonElement>(originalJson);
//         var anonymized = new Dictionary<string, object>();

//         // Rimuovi informazioni sensibili e anonimizza
//         foreach (var property in data.EnumerateObject())
//         {
//             switch (property.Name.ToLower())
//             {
//                 case "vin":
//                     anonymized["vin"] = "ANON_" + Guid.NewGuid().ToString("N")[..8].ToUpper();
//                     break;
//                 case "location":
//                     // Anonimizza coordinate aggiungendo rumore
//                     var loc = property.Value;
//                     var random = new Random();
//                     anonymized["location"] = new
//                     {
//                         latitude = Math.Round(45.4642 + (random.NextDouble() - 0.5) * 0.5, 4),
//                         longitude = Math.Round(9.1900 + (random.NextDouble() - 0.5) * 0.5, 4),
//                         heading = property.Value.GetProperty("heading").GetInt32(),
//                         speed = property.Value.GetProperty("speed").GetInt32()
//                     };
//                     break;
//                 default:
//                     // Mantieni altri dati non sensibili
//                     anonymized[property.Name] = JsonSerializer.Deserialize<object>(property.Value.GetRawText());
//                     break;
//             }
//         }

//         anonymized["anonymizedAt"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ");
//         anonymized["anonymizationVersion"] = "1.0";

//         return JsonSerializer.Serialize(anonymized, new JsonSerializerOptions { WriteIndented = false });
//     }
//     catch
//     {
//         return $@"{{""error"":""Failed to anonymize data"",""timestamp"":""{DateTime.Now:yyyy-MM-ddTHH:mm:ssZ}""}}";
//     }
// }
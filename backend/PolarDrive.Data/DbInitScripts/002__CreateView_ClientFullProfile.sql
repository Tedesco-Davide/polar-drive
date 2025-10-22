-- SQL Server: Controlla se la view esiste prima di crearla
IF NOT EXISTS (SELECT * FROM sys.views WHERE name = 'vw_ClientFullProfile')
BEGIN
    EXEC('CREATE VIEW vw_ClientFullProfile AS
    WITH VehicleStats AS (
        SELECT 
            cv.ClientCompanyId,
            cv.Id AS VehicleId,
            cv.Vin,
            cv.Brand,
            cv.Model,
            cv.FuelType,
            cv.IsActiveFlag,
            cv.IsFetchingDataFlag,
            cv.ClientOAuthAuthorized,
            cv.FirstActivationAt,
            cv.LastDeactivationAt,
            cv.CreatedAt AS VehicleCreatedAt,
            cv.ReferentName,
            cv.ReferentMobileNumber,
            cv.ReferentEmail,
            
            -- Statistiche consensi per veicolo
            COALESCE(consent_stats.TotalConsents, 0) AS TotalConsents,
            COALESCE(consent_stats.ActivationConsents, 0) AS ActivationConsents,
            COALESCE(consent_stats.DeactivationConsents, 0) AS DeactivationConsents,
            consent_stats.LastConsentDate,
            
            -- Statistiche outages per veicolo
            COALESCE(outage_stats.TotalOutages, 0) AS TotalOutages,
            COALESCE(outage_stats.ActiveOutages, 0) AS ActiveOutages,
            outage_stats.LastOutageStart,
            outage_stats.TotalOutageDays,
            
            -- Statistiche report per veicolo
            COALESCE(report_stats.TotalReports, 0) AS TotalReports,
            COALESCE(report_stats.GeneratedReports, 0) AS GeneratedReports,
            COALESCE(report_stats.TotalRegenerations, 0) AS TotalRegenerations,
            report_stats.LastReportGenerated,
            
            -- Statistiche SMS per veicolo
            COALESCE(sms_stats.TotalSmsEvents, 0) AS TotalSmsEvents,
            COALESCE(sms_stats.AdaptiveOnEvents, 0) AS AdaptiveOnEvents,
            COALESCE(sms_stats.AdaptiveOffEvents, 0) AS AdaptiveOffEvents,
            sms
            COALESCE(sms_stats.ActiveSessions, 0) AS ActiveSessions,
            sms_stats.LastActiveSessionExpires
            
        FROM ClientVehicles cv
        
        -- Statistiche consensi
        LEFT JOIN (
            SELECT 
                cc.VehicleId,
                COUNT(*) AS TotalConsents,
                SUM(CASE WHEN cc.ConsentType LIKE ''%Activation%'' THEN 1 ELSE 0 END) AS ActivationConsents,
                SUM(CASE WHEN cc.ConsentType LIKE ''%Deactivation%'' THEN 1 ELSE 0 END) AS DeactivationConsents,
                MAX(cc.UploadDate) AS LastConsentDate
            FROM ClientConsents cc
            GROUP BY cc.VehicleId
        ) consent_stats ON cv.Id = consent_stats.VehicleId
        
        -- Statistiche outages
        LEFT JOIN (
            SELECT 
                op.VehicleId,
                COUNT(*) AS TotalOutages,
                SUM(CASE WHEN op.OutageEnd IS NULL THEN 1 ELSE 0 END) AS ActiveOutages,
                MAX(op.OutageStart) AS LastOutageStart,
                SUM(
                    CASE 
                        WHEN op.OutageEnd IS NOT NULL 
                        THEN DATEDIFF(day, op.OutageStart, op.OutageEnd)
                        ELSE DATEDIFF(day, op.OutageStart, GETDATE())
                    END
                ) AS TotalOutageDays
            FROM OutagePeriods op
            WHERE op.VehicleId IS NOT NULL
            GROUP BY op.VehicleId
        ) outage_stats ON cv.Id = outage_stats.VehicleId
        
        LEFT JOIN (
            SELECT 
                pr.VehicleId,
                COUNT(*) AS TotalReports,
                SUM(CASE WHEN pr.GeneratedAt IS NOT NULL THEN 1 ELSE 0 END) AS GeneratedReports,
                SUM(pr.RegenerationCount) AS TotalRegenerations,
                MAX(pr.GeneratedAt) AS LastReportGenerated
            FROM PdfReports pr
            GROUP BY pr.VehicleId
        ) report_stats ON cv.Id = report_stats.VehicleId
        
        -- Statistiche SMS
        LEFT JOIN (
            SELECT 
                apse.VehicleId,
                COUNT(*) AS TotalSmsEvents,
                SUM(CASE WHEN apse.ParsedCommand = ''ADAPTIVE_PROFILING_ON'' THEN 1 ELSE 0 END) AS AdaptiveOnEvents,
                SUM(CASE WHEN apse.ParsedCommand = ''ADAPTIVE_PROFILING_OFF'' THEN 1 ELSE 0 END) AS AdaptiveOffEvents,
                SUM(CASE 
                    WHEN apse.ParsedCommand = ''ADAPTIVE_PROFILING_ON'' 
                    AND apse.ConsentAccepted = 1 
                    AND apse.ExpiresAt > GETDATE() 
                    THEN 1 
                    ELSE 0 
                END) AS ActiveSessions,
                MAX(apse.ReceivedAt) AS LastSmsReceived,
                MAX(CASE 
                    WHEN apse.ParsedCommand = ''ADAPTIVE_PROFILING_ON''
                    AND apse.ConsentAccepted = 1 
                    AND apse.ExpiresAt > GETDATE() 
                    THEN apse.ExpiresAt 
                    ELSE NULL 
                END) AS LastActiveSessionExpires
            FROM SmsAdaptiveProfiling apse
            GROUP BY apse.VehicleId
        ) sms_stats ON cv.Id = sms_stats.VehicleId
    ),

    CompanyStats AS (
        SELECT 
            cc.Id AS ClientCompanyId,
            cc.VatNumber,
            cc.Name,
            cc.Address,
            cc.Email,
            cc.PecAddress,
            cc.LandlineNumber,
            cc.CreatedAt AS CompanyCreatedAt,
            
            -- Calcolo giorni di registrazione
            DATEDIFF(day, cc.CreatedAt, GETDATE()) AS DaysRegistered,
            
            -- Statistiche veicoli aggregate
            COUNT(vs.VehicleId) AS TotalVehicles,
            SUM(CASE WHEN vs.IsActiveFlag = 1 THEN 1 ELSE 0 END) AS ActiveVehicles,
            SUM(CASE WHEN vs.IsFetchingDataFlag = 1 THEN 1 ELSE 0 END) AS FetchingVehicles,
            SUM(CASE WHEN vs.ClientOAuthAuthorized = 1 THEN 1 ELSE 0 END) AS AuthorizedVehicles,
            
            -- Statistiche aggregate consensi
            SUM(vs.TotalConsents) AS TotalConsentsCompany,
            SUM(vs.ActivationConsents) AS TotalActivationConsents,
            SUM(vs.DeactivationConsents) AS TotalDeactivationConsents,
            MAX(vs.LastConsentDate) AS LastConsentDateCompany,
            
            -- Statistiche aggregate outages
            SUM(vs.TotalOutages) AS TotalOutagesCompany,
            SUM(vs.ActiveOutages) AS ActiveOutagesCompany,
            SUM(vs.TotalOutageDays) AS TotalOutageDaysCompany,
            MAX(vs.LastOutageStart) AS LastOutageStartCompany,
            
            -- Statistiche aggregate report
            SUM(vs.TotalReports) AS TotalReportsCompany,
            SUM(vs.GeneratedReports) AS GeneratedReportsCompany,
            SUM(vs.TotalRegenerations) AS TotalRegenerationsCompany,
            MAX(vs.LastReportGenerated) AS LastReportGeneratedCompany,
            
            -- Statistiche aggregate SMS
            SUM(vs.TotalSmsEvents) AS TotalSmsEventsCompany,
            SUM(vs.AdaptiveOnEvents) AS AdaptiveOnEventsCompany,
            SUM(vs.AdaptiveOffEvents) AS AdaptiveOffEventsCompany,
            MAX(vs.LastSmsReceived) AS LastSmsReceivedCompany,
            SUM(vs.ActiveSessions) AS ActiveSessionsCompany,
            MAX(vs.LastActiveSessionExpires) AS LastActiveSessionExpiresCompany,

            -- Conteggio brand unici
            COUNT(DISTINCT vs.Brand) AS UniqueBrands,
            
            -- Prima e ultima attivazione
            MIN(vs.FirstActivationAt) AS FirstVehicleActivation,
            MAX(vs.LastDeactivationAt) AS LastVehicleDeactivation
            
        FROM ClientCompanies cc
        LEFT JOIN VehicleStats vs ON cc.Id = vs.ClientCompanyId
        GROUP BY cc.Id, cc.VatNumber, cc.Name, cc.Address, cc.Email, cc.PecAddress, 
                 cc.LandlineNumber, cc.CreatedAt
    )

    SELECT 
        cs.*,
        vs.VehicleId,
        vs.Vin,
        vs.Brand,
        vs.Model,
        vs.FuelType,
        vs.IsActiveFlag AS VehicleIsActive,
        vs.IsFetchingDataFlag AS VehicleIsFetching,
        vs.ClientOAuthAuthorized AS VehicleIsAuthorized,
        vs.FirstActivationAt AS VehicleFirstActivation,
        vs.LastDeactivationAt AS VehicleLastDeactivation,
        vs.TotalOutageDays AS VehicleOutageDays,
        vs.VehicleCreatedAt,
        vs.ReferentName,
        vs.ReferentMobileNumber,
        vs.ReferentEmail,
        
        -- Statistiche dettagliate per veicolo
        vs.TotalConsents AS VehicleConsents,
        vs.ActivationConsents AS VehicleActivationConsents,
        vs.DeactivationConsents AS VehicleDeactivationConsents,
        vs.LastConsentDate AS VehicleLastConsent,
        
        vs.TotalOutages AS VehicleOutages,
        vs.ActiveOutages AS VehicleActiveOutages,
        vs.LastOutageStart AS VehicleLastOutage,
        
        vs.TotalReports AS VehicleReports,
        vs.GeneratedReports AS VehicleGeneratedReports,
        vs.TotalRegenerations AS VehicleRegenerations,
        vs.LastReportGenerated AS VehicleLastReport,
        
        vs.TotalSmsEvents AS VehicleSmsEvents,
        vs.AdaptiveOnEvents AS VehicleAdaptiveOn,
        vs.AdaptiveOffEvents AS VehicleAdaptiveOff,
        vs.LastSmsReceived AS VehicleLastSms,
        vs.ActiveSessions AS VehicleActiveSessions,
        vs.LastActiveSessionExpires AS VehicleActiveSessionExpires,
        
        -- Calcoli aggiuntivi
        CASE 
            WHEN vs.FirstActivationAt IS NOT NULL 
            THEN DATEDIFF(day, vs.FirstActivationAt, GETDATE())
            ELSE NULL 
        END AS DaysSinceFirstActivation,
        
        CASE 
            WHEN vs.LastDeactivationAt IS NOT NULL 
            THEN DATEDIFF(day, vs.LastDeactivationAt, GETDATE())
            ELSE NULL 
        END AS DaysSinceLastDeactivation

    FROM CompanyStats cs
    LEFT JOIN VehicleStats vs ON cs.ClientCompanyId = vs.ClientCompanyId')
END

-- ===============================================
-- INDICI AGGIUNTIVI PER PERFORMANCE
-- ===============================================

-- SQL Server: Controlla se l'indice esiste prima di crearlo
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_outage_active_vehicle' AND object_id = OBJECT_ID('OutagePeriods'))
BEGIN
    CREATE INDEX idx_outage_active_vehicle 
    ON OutagePeriods (VehicleId, OutageEnd)
    WHERE OutageType = 'Outage Vehicle';
END

-- Indice per migliorare le performance dei report
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_pdfreports_vehicle_period' AND object_id = OBJECT_ID('PdfReports'))
BEGIN
    CREATE INDEX idx_pdfreports_vehicle_period 
    ON PdfReports (VehicleId, ReportPeriodStart, ReportPeriodEnd);
END

-- Indice per migliorare le performance dei consensi
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_clientconsents_vehicle_date' AND object_id = OBJECT_ID('ClientConsents'))
BEGIN
    CREATE INDEX idx_clientconsents_vehicle_date 
    ON ClientConsents (VehicleId, UploadDate);
END

-- Indice per migliorare le performance degli SMS
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_smsevents_vehicle_date' AND object_id = OBJECT_ID('SmsAdaptiveProfiling'))
BEGIN
    CREATE INDEX idx_smsevents_vehicle_date 
    ON SmsAdaptiveProfiling (VehicleId, ReceivedAt);
END
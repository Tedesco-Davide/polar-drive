CREATE VIEW IF NOT EXISTS vw_ClientFullProfile AS
SELECT
    cc.Id                    AS CompanyId,
    cc.VatNumber,
    cc.Name,
    cc.Address,
    cc.Email,
    cc.PecAddress,

    tv.Id                    AS TeslaVehicleId,
    tv.Vin,
    tv.Model,
    tw.IsActiveFlag          AS WorkflowStatus,
    tw.IsFetchingDataFlag    AS DataCollectionFlag,
    tw.LastStatusChangeAt,

    ev.FieldChanged          AS EventType,
    ev.EventTimestamp

FROM   ClientCompanies cc
JOIN   ClientTeslaVehicles tv  ON tv.ClientCompanyId = cc.Id
JOIN   TeslaWorkflows tw       ON tw.TeslaVehicleId  = tv.Id
LEFT   JOIN TeslaWorkflowEvents ev ON ev.TeslaVehicleId = tv.Id;
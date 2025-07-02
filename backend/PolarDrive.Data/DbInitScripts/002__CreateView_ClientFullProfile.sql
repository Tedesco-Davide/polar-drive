CREATE VIEW IF NOT EXISTS vw_ClientFullProfile AS
SELECT
    cc.Id                    AS CompanyId,
    cc.VatNumber,
    cc.Name,
    cc.Address,
    cc.Email,
    cc.PecAddress,

    tv.Id                    AS VehicleId,
    tv.Vin,
    tv.Model,
    tw.IsActiveFlag          AS WorkflowStatus,
    tw.IsFetchingDataFlag    AS DataCollectionFlag,
    tw.LastStatusChangeAt,

    ev.FieldChanged          AS EventType,
    ev.EventTimestamp

FROM   ClientCompanies cc
JOIN   ClientVehicles tv  ON tv.ClientCompanyId = cc.Id
JOIN   Workflows tw       ON tw.VehicleId  = tv.Id
LEFT   JOIN WorkflowEvents ev ON ev.VehicleId = tv.Id;
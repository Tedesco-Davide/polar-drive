namespace PolarDrive.TeslaMockApiService.Models;

using System.Collections.Generic;

public class TeslaCompleteDataDto
{
    public ResponseDto? Response { get; set; }
}

public class ResponseDto
{
    public DataItemDto[]? Data { get; set; }
}

public class DataItemDto
{
    public string? Type { get; set; }
    public object? Content { get; set; }
}

// Charging History DTO
public class ChargingHistoryDto
{
    public int SessionId { get; set; }
    public string? Vin { get; set; }
    public string? SiteLocationName { get; set; }
    public string? ChargeStartDateTime { get; set; }
    public string? ChargeStopDateTime { get; set; }
    public string? UnlatchDateTime { get; set; }
    public string? CountryCode { get; set; }
    public string? BillingType { get; set; }
    public string? VehicleMakeType { get; set; }
    public FeeDto[]? Fees { get; set; }
    public InvoiceDto[]? Invoices { get; set; }
}

public class FeeDto
{
    public int SessionFeeId { get; set; }
    public string? FeeType { get; set; }
    public string? CurrencyCode { get; set; }
    public string? PricingType { get; set; }
    public decimal RateBase { get; set; }
    public decimal RateTier1 { get; set; }
    public decimal RateTier2 { get; set; }
    public decimal? RateTier3 { get; set; }
    public decimal? RateTier4 { get; set; }
    public decimal UsageBase { get; set; }
    public decimal UsageTier1 { get; set; }
    public decimal UsageTier2 { get; set; }
    public decimal? UsageTier3 { get; set; }
    public decimal? UsageTier4 { get; set; }
    public decimal TotalBase { get; set; }
    public decimal TotalTier1 { get; set; }
    public decimal TotalTier2 { get; set; }
    public decimal TotalTier3 { get; set; }
    public decimal TotalTier4 { get; set; }
    public decimal TotalDue { get; set; }
    public decimal NetDue { get; set; }
    public string? Uom { get; set; }
    public bool IsPaid { get; set; }
    public string? Status { get; set; }
}

public class InvoiceDto
{
    public string? FileName { get; set; }
    public string? ContentId { get; set; }
    public string? InvoiceType { get; set; }
}

// Energy Endpoints DTO
public class EnergyEndpointsDto
{
    public BackupDto? Backup { get; set; }
    public BackupHistoryDto? BackupHistory { get; set; }
    public ChargeHistoryDto? ChargeHistory { get; set; }
    public EnergyHistoryDto? EnergyHistory { get; set; }
    public GridImportExportDto? GridImportExport { get; set; }
    public LiveStatusDto? LiveStatus { get; set; }
    public OffGridVehicleChargingReserveDto? OffGridVehicleChargingReserve { get; set; }
    public OperationDto? Operation { get; set; }
    public ProductsDto? Products { get; set; }
    public SiteInfoDto? SiteInfo { get; set; }
    public StormModeDto? StormMode { get; set; }
}

public class BackupDto
{
    public BackupResponseDto? Response { get; set; }
}

public class BackupResponseDto
{
    public int Code { get; set; }
    public string? Message { get; set; }
}

public class BackupHistoryDto
{
    public BackupHistoryResponseDto? Response { get; set; }
}

public class BackupHistoryResponseDto
{
    public BackupEventDto[]? Events { get; set; }
    public int TotalEvents { get; set; }
}

public class BackupEventDto
{
    public string? Timestamp { get; set; }
    public int Duration { get; set; }
}

public class ChargeHistoryDto
{
    public ChargeHistoryResponseDto? Response { get; set; }
}

public class ChargeHistoryResponseDto
{
    public ChargeHistoryItemDto[]? ChargeHistory { get; set; }
}

public class ChargeHistoryItemDto
{
    public TimeSecondsDto? ChargeStartTime { get; set; }
    public TimeSecondsDto? ChargeDuration { get; set; }
    public int EnergyAddedWh { get; set; }
}

public class TimeSecondsDto
{
    public long Seconds { get; set; }
}

public class EnergyHistoryDto
{
    public EnergyHistoryResponseDto? Response { get; set; }
}

public class EnergyHistoryResponseDto
{
    public string? Period { get; set; }
    public EnergyTimeSeriesDto[]? TimeSeries { get; set; }
}

public class EnergyTimeSeriesDto
{
    public string? Timestamp { get; set; }
    public int SolarEnergyExported { get; set; }
    public int GeneratorEnergyExported { get; set; }
    public int GridEnergyImported { get; set; }
    public decimal GridServicesEnergyImported { get; set; }
    public decimal GridServicesEnergyExported { get; set; }
    public int GridEnergyExportedFromSolar { get; set; }
    public int GridEnergyExportedFromGenerator { get; set; }
    public int GridEnergyExportedFromBattery { get; set; }
    public int BatteryEnergyExported { get; set; }
    public int BatteryEnergyImportedFromGrid { get; set; }
    public int BatteryEnergyImportedFromSolar { get; set; }
    public int BatteryEnergyImportedFromGenerator { get; set; }
    public int ConsumerEnergyImportedFromGrid { get; set; }
    public int ConsumerEnergyImportedFromSolar { get; set; }
    public int ConsumerEnergyImportedFromBattery { get; set; }
    public int ConsumerEnergyImportedFromGenerator { get; set; }
}

public class GridImportExportDto
{
    public GridImportExportResponseDto? Response { get; set; }
}

public class GridImportExportResponseDto
{
    public int Code { get; set; }
    public string? Message { get; set; }
}

public class LiveStatusDto
{
    public LiveStatusResponseDto? Response { get; set; }
}

public class LiveStatusResponseDto
{
    public int SolarPower { get; set; }
    public decimal EnergyLeft { get; set; }
    public int TotalPackEnergy { get; set; }
    public decimal PercentageCharged { get; set; }
    public bool BackupCapable { get; set; }
    public int BatteryPower { get; set; }
    public int LoadPower { get; set; }
    public string? GridStatus { get; set; }
    public int GridPower { get; set; }
    public string? IslandStatus { get; set; }
    public bool StormModeActive { get; set; }
    public string? Timestamp { get; set; }
}

public class OffGridVehicleChargingReserveDto
{
    public OffGridVehicleChargingReserveResponseDto? Response { get; set; }
}

public class OffGridVehicleChargingReserveResponseDto
{
    public int Code { get; set; }
    public string? Message { get; set; }
}

public class OperationDto
{
    public OperationResponseDto? Response { get; set; }
}

public class OperationResponseDto
{
    public int Code { get; set; }
    public string? Message { get; set; }
}

public class ProductsDto
{
    public object[]? Response { get; set; }
    public int Count { get; set; }
}

public class ProductVehicleDto
{
    public int Id { get; set; }
    public long UserId { get; set; }
    public int VehicleId { get; set; }
    public string? Vin { get; set; }
    public string? Color { get; set; }
    public string? AccessType { get; set; }
    public string? DisplayName { get; set; }
    public string? OptionCodes { get; set; }
    public object? CachedData { get; set; }
    public bool MobileAccessDisabled { get; set; }
    public GranularAccessDto? GranularAccess { get; set; }
    public string[]? Tokens { get; set; }
    public string? State { get; set; }
    public bool InService { get; set; }
    public string? IdS { get; set; }
    public bool CalendarEnabled { get; set; }
    public int? ApiVersion { get; set; }
    public string? BackseatToken { get; set; }
    public string? BackseatTokenUpdatedAt { get; set; }
    public string? DeviceType { get; set; }
    public string? CommandSigning { get; set; }
}

public class ProductEnergyDto
{
    public int EnergySiteId { get; set; }
    public string? DeviceType { get; set; }
    public string? ResourceType { get; set; }
    public string? SiteName { get; set; }
    public string? Id { get; set; }
    public string? GatewayId { get; set; }
    public int EnergyLeft { get; set; }
    public int TotalPackEnergy { get; set; }
    public int PercentageCharged { get; set; }
    public int BatteryPower { get; set; }
}

public class GranularAccessDto
{
    public bool HidePrivate { get; set; }
}

public class SiteInfoDto
{
    public SiteInfoResponseDto? Response { get; set; }
}

public class SiteInfoResponseDto
{
    public string? Id { get; set; }
    public string? SiteName { get; set; }
    public int BackupReservePercent { get; set; }
    public string? DefaultRealMode { get; set; }
    public string? InstallationDate { get; set; }
    public UserSettingsDto? UserSettings { get; set; }
    public ComponentsDto? Components { get; set; }
    public string? Version { get; set; }
    public int BatteryCount { get; set; }
    public int NameplatePower { get; set; }
    public int NameplateEnergy { get; set; }
    public string? InstallationTimeZone { get; set; }
    public int MaxSiteMeterPowerAc { get; set; }
    public decimal MinSiteMeterPowerAc { get; set; }
}

public class UserSettingsDto
{
    public bool StormModeEnabled { get; set; }
}

public class ComponentsDto
{
    public bool Solar { get; set; }
    public string? SolarType { get; set; }
    public bool Battery { get; set; }
    public bool Grid { get; set; }
    public bool Backup { get; set; }
    public bool LoadMeter { get; set; }
    public bool StormModeCapable { get; set; }
    public bool OffGridVehicleChargingReserveSupported { get; set; }
    public bool SolarValueEnabled { get; set; }
    public bool SetIslandingModeEnabled { get; set; }
    public string? BatteryType { get; set; }
    public bool Configurable { get; set; }
}

public class StormModeDto
{
    public StormModeResponseDto? Response { get; set; }
}

public class StormModeResponseDto
{
    public int Code { get; set; }
    public string? Message { get; set; }
}

// Partner Endpoints DTO
public class PartnerEndpointsDto
{
    public string[]? FleetTelemetryErrorVins { get; set; }
    public FleetTelemetryErrorDto[]? FleetTelemetryErrors { get; set; }
    public string? PublicKey { get; set; }
}

public class FleetTelemetryErrorDto
{
    public string? Name { get; set; }
    public string? Error { get; set; }
    public string? Vin { get; set; }
}

// User Endpoints DTO
public class UserEndpointsDto
{
    public FeatureConfigDto? FeatureConfig { get; set; }
    public MeDto? Me { get; set; }
    public OrdersDto? Orders { get; set; }
    public RegionDto? Region { get; set; }
}

public class FeatureConfigDto
{
    public FeatureConfigResponseDto? Response { get; set; }
}

public class FeatureConfigResponseDto
{
    public SignalingDto? Signaling { get; set; }
}

public class SignalingDto
{
    public bool Enabled { get; set; }
    public bool SubscribeConnectivity { get; set; }
    public bool UseAuthToken { get; set; }
}

public class MeDto
{
    public MeResponseDto? Response { get; set; }
}

public class MeResponseDto
{
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? VaultUuid { get; set; }
}

public class OrdersDto
{
    public OrderDto[]? Response { get; set; }
    public int Count { get; set; }
}

public class OrderDto
{
    public int VehicleMapId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Vin { get; set; }
    public string? OrderStatus { get; set; }
    public string? OrderSubstatus { get; set; }
    public string? ModelCode { get; set; }
    public string? CountryCode { get; set; }
    public string? Locale { get; set; }
    public string? MktOptions { get; set; }
    public bool IsB2b { get; set; }
}

public class RegionDto
{
    public RegionResponseDto? Response { get; set; }
}

public class RegionResponseDto
{
    public string? Region { get; set; }
    public string? FleetApiBaseUrl { get; set; }
}

// Vehicle Commands DTO
public class VehicleCommandDto
{
    public string? Command { get; set; }
    public string? Timestamp { get; set; }
    public object? Parameters { get; set; }
    public CommandResponseDto? Response { get; set; }
}

public class CommandResponseDto
{
    public bool Result { get; set; }
    public string? Reason { get; set; }
    public bool? Queued { get; set; }
}

// Vehicle Endpoints DTO
public class VehicleEndpointsDto
{
    public DriversDto? Drivers { get; set; }
    public DriversRemoveDto? DriversRemove { get; set; }
    public EligibleSubscriptionsDto? EligibleSubscriptions { get; set; }
    public EligibleUpgradesDto? EligibleUpgrades { get; set; }
    public FleetStatusDto? FleetStatus { get; set; }
    public FleetTelemetryConfigCreateDto? FleetTelemetryConfigCreate { get; set; }
    public FleetTelemetryConfigDeleteDto? FleetTelemetryConfigDelete { get; set; }
    public FleetTelemetryConfigGetDto? FleetTelemetryConfigGet { get; set; }
    public FleetTelemetryConfigJwsDto? FleetTelemetryConfigJws { get; set; }
    public FleetTelemetryErrorsDto? FleetTelemetryErrors { get; set; }
    public ListDto? List { get; set; }
    public MobileEnabledDto? MobileEnabled { get; set; }
    public NearbyChargingSitesDto? NearbyChargingSites { get; set; }
    public OptionsDto? Options { get; set; }
    public RecentAlertsDto? RecentAlerts { get; set; }
    public ReleaseNotesDto? ReleaseNotes { get; set; }
    public ServiceDataDto? ServiceData { get; set; }
    public ShareInvitesDto? ShareInvites { get; set; }
    public ShareInvitesCreateDto? ShareInvitesCreate { get; set; }
    public ShareInvitesRedeemDto? ShareInvitesRedeem { get; set; }
    public ShareInvitesRevokeDto? ShareInvitesRevoke { get; set; }
    public SignedCommandDto? SignedCommand { get; set; }
    public SubscriptionsDto? Subscriptions { get; set; }
    public SubscriptionsSetDto? SubscriptionsSet { get; set; }
    public VehicleDto? Vehicle { get; set; }
    public VehicleDataDto? VehicleData { get; set; }
    public VehicleSubscriptionsDto? VehicleSubscriptions { get; set; }
    public VehicleSubscriptionsSetDto? VehicleSubscriptionsSet { get; set; }
    public WakeUpDto? WakeUp { get; set; }
    public WarrantyDetailsDto? WarrantyDetails { get; set; }
}

public class DriversDto
{
    public DriverDto[]? Response { get; set; }
    public int Count { get; set; }
}

public class DriverDto
{
    public int MyTeslaUniqueId { get; set; }
    public int UserId { get; set; }
    public string? UserIdS { get; set; }
    public string? VaultUuid { get; set; }
    public string? DriverFirstName { get; set; }
    public string? DriverLastName { get; set; }
    public GranularAccessDto? GranularAccess { get; set; }
    public string[]? ActivePubkeys { get; set; }
    public string? PublicKey { get; set; }
}

public class DriversRemoveDto
{
    public string? Response { get; set; }
}

public class EligibleSubscriptionsDto
{
    public EligibleSubscriptionsResponseDto? Response { get; set; }
}

public class EligibleSubscriptionsResponseDto
{
    public string? Country { get; set; }
    public string? Vin { get; set; }
    public EligibleSubscriptionDto[]? Eligible { get; set; }
}

public class EligibleSubscriptionDto
{
    public string? OptionCode { get; set; }
    public string? Product { get; set; }
    public string? StartDate { get; set; }
    public AddonDto[]? Addons { get; set; }
    public BillingOptionDto[]? BillingOptions { get; set; }
}

public class AddonDto
{
    public string? BillingPeriod { get; set; }
    public string? CurrencyCode { get; set; }
    public string? OptionCode { get; set; }
    public decimal Price { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
}

public class BillingOptionDto
{
    public string? BillingPeriod { get; set; }
    public string? CurrencyCode { get; set; }
    public string? OptionCode { get; set; }
    public decimal Price { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
}

public class EligibleUpgradesDto
{
    public EligibleUpgradesResponseDto? Response { get; set; }
}

public class EligibleUpgradesResponseDto
{
    public string? Vin { get; set; }
    public string? Country { get; set; }
    public string? Type { get; set; }
    public EligibleUpgradeDto[]? Eligible { get; set; }
}

public class EligibleUpgradeDto
{
    public string? OptionCode { get; set; }
    public string? OptionGroup { get; set; }
    public string? CurrentOptionCode { get; set; }
    public PricingDto[]? Pricing { get; set; }
}

public class PricingDto
{
    public decimal Price { get; set; }
    public decimal Total { get; set; }
    public string? CurrencyCode { get; set; }
    public bool IsPrimary { get; set; }
}

public class FleetStatusDto
{
    public FleetStatusResponseDto? Response { get; set; }
}

public class FleetStatusResponseDto
{
    public string[]? KeyPairedVins { get; set; }
    public string[]? UnpairedVins { get; set; }
    public Dictionary<string, VehicleInfoDto>? VehicleInfo { get; set; }
}

public class VehicleInfoDto
{
    public string? FirmwareVersion { get; set; }
    public bool VehicleCommandProtocolRequired { get; set; }
    public bool DiscountedDeviceData { get; set; }
    public string? FleetTelemetryVersion { get; set; }
    public int TotalNumberOfKeys { get; set; }
}

public class FleetTelemetryConfigCreateDto
{
    public FleetTelemetryConfigCreateResponseDto? Response { get; set; }
}

public class FleetTelemetryConfigCreateResponseDto
{
    public int UpdatedVehicles { get; set; }
    public SkippedVehiclesDto? SkippedVehicles { get; set; }
}

public class SkippedVehiclesDto
{
    public string[]? MissingKey { get; set; }
    public string[]? UnsupportedHardware { get; set; }
    public string[]? UnsupportedFirmware { get; set; }
    public string[]? MaxConfigs { get; set; }
}

public class FleetTelemetryConfigDeleteDto
{
    public FleetTelemetryConfigDeleteResponseDto? Response { get; set; }
}

public class FleetTelemetryConfigDeleteResponseDto
{
    public int UpdatedVehicles { get; set; }
}

public class FleetTelemetryConfigGetDto
{
    public FleetTelemetryConfigGetResponseDto? Response { get; set; }
}

public class FleetTelemetryConfigGetResponseDto
{
    public bool Synced { get; set; }
    public TelemetryConfigDto? Config { get; set; }
    public bool LimitReached { get; set; }
    public bool KeyPaired { get; set; }
}

public class TelemetryConfigDto
{
    public string? Hostname { get; set; }
    public string? Ca { get; set; }
    public int Port { get; set; }
    public bool PreferTyped { get; set; }
    public TelemetryFieldsDto? Fields { get; set; }
    public string[]? AlertTypes { get; set; }
}

public class TelemetryFieldsDto
{
    public TelemetryFieldConfigDto? DriveRail { get; set; }
    public TelemetryFieldConfigDto? BmsFullchargecomplete { get; set; }
    public TelemetryFieldConfigDto? ChargerVoltage { get; set; }
}

public class TelemetryFieldConfigDto
{
    public int IntervalSeconds { get; set; }
    public int? ResendIntervalSeconds { get; set; }
    public decimal? MinimumDelta { get; set; }
}

public class FleetTelemetryConfigJwsDto
{
    public FleetTelemetryConfigJwsResponseDto? Response { get; set; }
}

public class FleetTelemetryConfigJwsResponseDto
{
    public int UpdatedVehicles { get; set; }
    public SkippedVehiclesDto? SkippedVehicles { get; set; }
}

public class FleetTelemetryErrorsDto
{
    public FleetTelemetryErrorsResponseDto? Response { get; set; }
}

public class FleetTelemetryErrorsResponseDto
{
    public FleetTelemetryErrorItemDto[]? FleetTelemetryErrors { get; set; }
}

public class FleetTelemetryErrorItemDto
{
    public string? Name { get; set; }
    public string? Error { get; set; }
    public string? Vin { get; set; }
}

public class ListDto
{
    public VehicleListItemDto[]? Response { get; set; }
    public PaginationDto? Pagination { get; set; }
    public int Count { get; set; }
}

public class VehicleListItemDto
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public string? Vin { get; set; }
    public string? Color { get; set; }
    public string? AccessType { get; set; }
    public string? DisplayName { get; set; }
    public string? OptionCodes { get; set; }
    public GranularAccessDto? GranularAccess { get; set; }
    public string[]? Tokens { get; set; }
    public string? State { get; set; }
    public bool InService { get; set; }
    public string? IdS { get; set; }
    public bool CalendarEnabled { get; set; }
    public int? ApiVersion { get; set; }
    public string? BackseatToken { get; set; }
    public string? BackseatTokenUpdatedAt { get; set; }
}

public class PaginationDto
{
    public string? Previous { get; set; }
    public string? Next { get; set; }
    public int Current { get; set; }
    public int PerPage { get; set; }
    public int Count { get; set; }
    public int Pages { get; set; }
}

public class MobileEnabledDto
{
    public MobileEnabledResponseDto? Response { get; set; }
}

public class MobileEnabledResponseDto
{
    public string? Reason { get; set; }
    public bool Result { get; set; }
}

public class NearbyChargingSitesDto
{
    public NearbyChargingSitesResponseDto? Response { get; set; }
}

public class NearbyChargingSitesResponseDto
{
    public long CongestionSyncTimeUtcSecs { get; set; }
    public DestinationChargingDto[]? DestinationCharging { get; set; }
    public SuperchargerDto[]? Superchargers { get; set; }
    public long Timestamp { get; set; }
}

public class DestinationChargingDto
{
    public LocationDto? Location { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public decimal DistanceMiles { get; set; }
    public string? Amenities { get; set; }
}

public class SuperchargerDto
{
    public LocationDto? Location { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public decimal DistanceMiles { get; set; }
    public int AvailableStalls { get; set; }
    public int TotalStalls { get; set; }
    public bool SiteClosed { get; set; }
    public string? Amenities { get; set; }
    public string? BillingInfo { get; set; }
}

public class LocationDto
{
    public decimal Lat { get; set; }
    public decimal Long { get; set; }
}

public class OptionsDto
{
    public OptionsResponseDto? Response { get; set; }
}

public class OptionsResponseDto
{
    public OptionCodeDto[]? Codes { get; set; }
}

public class OptionCodeDto
{
    public string? Code { get; set; }
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; }
    public string? ColorCode { get; set; }
}

public class RecentAlertsDto
{
    public RecentAlertsResponseDto? Response { get; set; }
}

public class RecentAlertsResponseDto
{
    public RecentAlertDto[]? RecentAlerts { get; set; }
}

public class RecentAlertDto
{
    public string? Name { get; set; }
    public string? Time { get; set; }
    public string[]? Audience { get; set; }
    public string? UserText { get; set; }
}

public class ReleaseNotesDto
{
    public ReleaseNotesResponseDto? Response { get; set; }
}

public class ReleaseNotesResponseDto
{
    public ReleaseNotesInnerResponseDto? Response { get; set; }
}

public class ReleaseNotesInnerResponseDto
{
    public ReleaseNoteDto[]? ReleaseNotes { get; set; }
}

public class ReleaseNoteDto
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public string? CustomerVersion { get; set; }
    public string? Icon { get; set; }
    public string? ImageUrl { get; set; }
    public string? LightImageUrl { get; set; }
}

public class ServiceDataDto
{
    public ServiceDataResponseDto? Response { get; set; }
}

public class ServiceDataResponseDto
{
    public string? ServiceStatus { get; set; }
    public string? ServiceEtc { get; set; }
    public string? ServiceVisitNumber { get; set; }
    public int StatusId { get; set; }
}

public class ShareInvitesDto
{
    public ShareInviteDto[]? Response { get; set; }
    public PaginationDto? Pagination { get; set; }
    public int Count { get; set; }
}

public class ShareInviteDto
{
    public long Id { get; set; }
    public long OwnerId { get; set; }
    public long? ShareUserId { get; set; }
    public string? ProductId { get; set; }
    public string? State { get; set; }
    public string? Code { get; set; }
    public string? ExpiresAt { get; set; }
    public string? RevokedAt { get; set; }
    public string? BorrowingDeviceId { get; set; }
    public string? KeyId { get; set; }
    public string? ProductType { get; set; }
    public string? ShareType { get; set; }
    public string? ShareUserSsoId { get; set; }
    public object[]? ActivePubkeys { get; set; }
    public string? IdS { get; set; }
    public string? OwnerIdS { get; set; }
    public string? ShareUserIdS { get; set; }
    public string? BorrowingKeyHash { get; set; }
    public string? Vin { get; set; }
    public string? ShareLink { get; set; }
}

public class ShareInvitesCreateDto
{
    public ShareInviteDto? Response { get; set; }
}

public class ShareInvitesRedeemDto
{
    public ShareInvitesRedeemResponseDto? Response { get; set; }
}

public class ShareInvitesRedeemResponseDto
{
    public string? VehicleIdS { get; set; }
    public string? Vin { get; set; }
}

public class ShareInvitesRevokeDto
{
    public bool Response { get; set; }
}

public class SignedCommandDto
{
    public string? Response { get; set; }
}

public class SubscriptionsDto
{
    public SubscriptionTypeDto? Vehicle { get; set; }
    public SubscriptionTypeDto? EnergySource { get; set; }
}

public class SubscriptionTypeDto
{
    public int[]? Ids { get; set; }
    public int Count { get; set; }
}

public class SubscriptionsSetDto
{
    public SubscriptionTypeDto? Vehicle { get; set; }
    public SubscriptionTypeDto? EnergySource { get; set; }
}

public class VehicleDto
{
    public VehicleResponseDto? Response { get; set; }
}

public class VehicleResponseDto
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public string? Vin { get; set; }
    public string? Color { get; set; }
    public string? AccessType { get; set; }
    public string? DisplayName { get; set; }
    public string? OptionCodes { get; set; }
    public GranularAccessDto? GranularAccess { get; set; }
    public string[]? Tokens { get; set; }
    public string? State { get; set; }
    public bool InService { get; set; }
    public string? IdS { get; set; }
    public bool CalendarEnabled { get; set; }
    public int? ApiVersion { get; set; }
    public string? BackseatToken { get; set; }
    public string? BackseatTokenUpdatedAt { get; set; }
}

public class VehicleDataDto
{
    public VehicleDataResponseDto? Response { get; set; }
}

public class VehicleDataResponseDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int VehicleId { get; set; }
    public string? Vin { get; set; }
    public string? Color { get; set; }
    public string? AccessType { get; set; }
    public GranularAccessDto? GranularAccess { get; set; }
    public string[]? Tokens { get; set; }
    public string? State { get; set; }
    public bool InService { get; set; }
    public string? IdS { get; set; }
    public bool CalendarEnabled { get; set; }
    public int ApiVersion { get; set; }
    public string? BackseatToken { get; set; }
    public string? BackseatTokenUpdatedAt { get; set; }
    public ChargeStateDto? ChargeState { get; set; }
    public ClimateStateDto? ClimateState { get; set; }
    public DriveStateDto? DriveState { get; set; }
    public GuiSettingsDto? GuiSettings { get; set; }
    public VehicleConfigDto? VehicleConfig { get; set; }
    public VehicleStateDto? VehicleState { get; set; }
}

public class ChargeStateDto
{
    public bool BatteryHeaterOn { get; set; }
    public int BatteryLevel { get; set; }
    public decimal BatteryRange { get; set; }
    public int ChargeAmps { get; set; }
    public int ChargeCurrentRequest { get; set; }
    public int ChargeCurrentRequestMax { get; set; }
    public bool ChargeEnableRequest { get; set; }
    public decimal ChargeEnergyAdded { get; set; }
    public int ChargeLimitSoc { get; set; }
    public int ChargeLimitSocMax { get; set; }
    public int ChargeLimitSocMin { get; set; }
    public int ChargeLimitSocStd { get; set; }
    public int ChargeMilesAddedIdeal { get; set; }
    public int ChargeMilesAddedRated { get; set; }
    public bool ChargePortColdWeatherMode { get; set; }
    public string? ChargePortColor { get; set; }
    public bool ChargePortDoorOpen { get; set; }
    public string? ChargePortLatch { get; set; }
    public int ChargeRate { get; set; }
    public int ChargerActualCurrent { get; set; }
    public int? ChargerPhases { get; set; }
    public int ChargerPilotCurrent { get; set; }
    public int ChargerPower { get; set; }
    public int ChargerVoltage { get; set; }
    public string? ChargingState { get; set; }
    public string? ConnChargeCable { get; set; }
    public decimal EstBatteryRange { get; set; }
    public string? FastChargerBrand { get; set; }
    public bool FastChargerPresent { get; set; }
    public string? FastChargerType { get; set; }
    public decimal IdealBatteryRange { get; set; }
    public bool ManagedChargingActive { get; set; }
    public long? ManagedChargingStartTime { get; set; }
    public bool ManagedChargingUserCanceled { get; set; }
    public int MaxRangeChargeCounter { get; set; }
    public int MinutesToFullCharge { get; set; }
    public bool? NotEnoughPowerToHeat { get; set; }
    public bool OffPeakChargingEnabled { get; set; }
    public string? OffPeakChargingTimes { get; set; }
    public int OffPeakHoursEndTime { get; set; }
    public bool PreconditioningEnabled { get; set; }
    public string? PreconditioningTimes { get; set; }
    public string? ScheduledChargingMode { get; set; }
    public bool ScheduledChargingPending { get; set; }
    public long? ScheduledChargingStartTime { get; set; }
    public long ScheduledDepartureTime { get; set; }
    public int ScheduledDepartureTimeMinutes { get; set; }
    public bool SuperchargerSessionTripPlanner { get; set; }
    public int TimeToFullCharge { get; set; }
    public long Timestamp { get; set; }
    public bool TripCharging { get; set; }
    public int UsableBatteryLevel { get; set; }
    public bool? UserChargeEnableRequest { get; set; }
}

public class ClimateStateDto
{
    public bool AllowCabinOverheatProtection { get; set; }
    public bool AutoSeatClimateLeft { get; set; }
    public bool AutoSeatClimateRight { get; set; }
    public bool AutoSteeringWheelHeat { get; set; }
    public bool BatteryHeater { get; set; }
    public bool? BatteryHeaterNoPower { get; set; }
    public bool BioweaponMode { get; set; }
    public string? CabinOverheatProtection { get; set; }
    public bool CabinOverheatProtectionActivelyCooling { get; set; }
    public string? ClimateKeeperMode { get; set; }
    public string? CopActivationTemperature { get; set; }
    public int DefrostMode { get; set; }
    public int DriverTempSetting { get; set; }
    public int FanStatus { get; set; }
    public string? HvacAutoRequest { get; set; }
    public decimal InsideTemp { get; set; }
    public bool IsAutoConditioningOn { get; set; }
    public bool IsClimateOn { get; set; }
    public bool IsFrontDefrosterOn { get; set; }
    public bool IsPreconditioning { get; set; }
    public bool IsRearDefrosterOn { get; set; }
    public int LeftTempDirection { get; set; }
    public int MaxAvailTemp { get; set; }
    public int MinAvailTemp { get; set; }
    public decimal OutsideTemp { get; set; }
    public int PassengerTempSetting { get; set; }
    public bool RemoteHeaterControlEnabled { get; set; }
    public int RightTempDirection { get; set; }
    public int SeatHeaterLeft { get; set; }
    public int SeatHeaterRearCenter { get; set; }
    public int SeatHeaterRearLeft { get; set; }
    public int SeatHeaterRearRight { get; set; }
    public int SeatHeaterRight { get; set; }
    public bool SideMirrorHeaters { get; set; }
    public int SteeringWheelHeatLevel { get; set; }
    public bool SteeringWheelHeater { get; set; }
    public bool SupportsFanOnlyCabinOverheatProtection { get; set; }
    public long Timestamp { get; set; }
    public bool WiperBladeHeater { get; set; }
}

public class DriveStateDto
{
    public decimal ActiveRouteLatitude { get; set; }
    public decimal ActiveRouteLongitude { get; set; }
    public int ActiveRouteTrafficMinutesDelay { get; set; }
    public long GpsAsOf { get; set; }
    public int Heading { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal NativeLatitude { get; set; }
    public int NativeLocationSupported { get; set; }
    public decimal NativeLongitude { get; set; }
    public string? NativeType { get; set; }
    public int Power { get; set; }
    public string? ShiftState { get; set; }
    public int? Speed { get; set; }
    public long Timestamp { get; set; }
}

public class GuiSettingsDto
{
    public bool Gui24HourTime { get; set; }
    public string? GuiChargeRateUnits { get; set; }
    public string? GuiDistanceUnits { get; set; }
    public string? GuiRangeDisplay { get; set; }
    public string? GuiTemperatureUnits { get; set; }
    public string? GuiTirepressureUnits { get; set; }
    public bool ShowRangeUnits { get; set; }
    public long Timestamp { get; set; }
}

public class VehicleConfigDto
{
    public string? AuxParkLamps { get; set; }
    public int BadgeVersion { get; set; }
    public bool CanAcceptNavigationRequests { get; set; }
    public bool CanActuateTrunks { get; set; }
    public string? CarSpecialType { get; set; }
    public string? CarType { get; set; }
    public string? ChargePortType { get; set; }
    public bool CopUserSetTempSupported { get; set; }
    public bool DashcamClipSaveSupported { get; set; }
    public bool DefaultChargeToMax { get; set; }
    public string? DriverAssist { get; set; }
    public bool EceRestrictions { get; set; }
    public string? EfficiencyPackage { get; set; }
    public bool EuVehicle { get; set; }
    public string? ExteriorColor { get; set; }
    public string? ExteriorTrim { get; set; }
    public string? ExteriorTrimOverride { get; set; }
    public bool HasAirSuspension { get; set; }
    public bool HasLudicrousMode { get; set; }
    public bool HasSeatCooling { get; set; }
    public string? HeadlampType { get; set; }
    public string? InteriorTrimType { get; set; }
    public int KeyVersion { get; set; }
    public bool MotorizedChargePort { get; set; }
    public string? PaintColorOverride { get; set; }
    public string? PerformancePackage { get; set; }
    public bool Plg { get; set; }
    public bool Pws { get; set; }
    public string? RearDriveUnit { get; set; }
    public int RearSeatHeaters { get; set; }
    public int RearSeatType { get; set; }
    public bool Rhd { get; set; }
    public string? RoofColor { get; set; }
    public int? SeatType { get; set; }
    public string? SpoilerType { get; set; }
    public bool? SunRoofInstalled { get; set; }
    public bool SupportsQrPairing { get; set; }
    public string? ThirdRowSeats { get; set; }
    public long Timestamp { get; set; }
    public string? TrimBadging { get; set; }
    public bool UseRangeBadging { get; set; }
    public int UtcOffset { get; set; }
    public bool WebcamSelfieSupported { get; set; }
    public bool WebcamSupported { get; set; }
    public string? WheelType { get; set; }
}

public class VehicleStateDto
{
    public int ApiVersion { get; set; }
    public string? AutoparkStateV3 { get; set; }
    public string? AutoparkStyle { get; set; }
    public bool CalendarSupported { get; set; }
    public string? CarVersion { get; set; }
    public int CenterDisplayState { get; set; }
    public bool DashcamClipSaveAvailable { get; set; }
    public string? DashcamState { get; set; }
    public int Df { get; set; }
    public int Dr { get; set; }
    public int FdWindow { get; set; }
    public string? FeatureBitmask { get; set; }
    public int FpWindow { get; set; }
    public int Ft { get; set; }
    public int HomelinkDeviceCount { get; set; }
    public bool HomelinkNearby { get; set; }
    public bool IsUserPresent { get; set; }
    public string? LastAutoparkError { get; set; }
    public bool Locked { get; set; }
    public MediaInfoDto? MediaInfo { get; set; }
    public MediaStateDto? MediaState { get; set; }
    public bool NotificationsSupported { get; set; }
    public decimal Odometer { get; set; }
    public bool ParsedCalendarSupported { get; set; }
    public int Pf { get; set; }
    public int Pr { get; set; }
    public int RdWindow { get; set; }
    public bool RemoteStart { get; set; }
    public bool RemoteStartEnabled { get; set; }
    public bool RemoteStartSupported { get; set; }
    public int RpWindow { get; set; }
    public int Rt { get; set; }
    public int SantaMode { get; set; }
    public bool SentryMode { get; set; }
    public bool SentryModeAvailable { get; set; }
    public bool ServiceMode { get; set; }
    public bool ServiceModePlus { get; set; }
    public bool SmartSummonAvailable { get; set; }
    public SoftwareUpdateDto? SoftwareUpdate { get; set; }
    public SpeedLimitModeDto? SpeedLimitMode { get; set; }
    public bool SummonStandbyModeEnabled { get; set; }
    public long Timestamp { get; set; }
    public bool TpmsHardWarningFl { get; set; }
    public bool TpmsHardWarningFr { get; set; }
    public bool TpmsHardWarningRl { get; set; }
    public bool TpmsHardWarningRr { get; set; }
    public long TpmsLastSeenPressureTimeFl { get; set; }
    public long TpmsLastSeenPressureTimeFr { get; set; }
    public long TpmsLastSeenPressureTimeRl { get; set; }
    public long TpmsLastSeenPressureTimeRr { get; set; }
    public decimal TpmsPressureFl { get; set; }
    public decimal TpmsPressureFr { get; set; }
    public decimal TpmsPressureRl { get; set; }
    public decimal TpmsPressureRr { get; set; }
    public decimal TpmsRcpFrontValue { get; set; }
    public decimal TpmsRcpRearValue { get; set; }
    public bool TpmsSoftWarningFl { get; set; }
    public bool TpmsSoftWarningFr { get; set; }
    public bool TpmsSoftWarningRl { get; set; }
    public bool TpmsSoftWarningRr { get; set; }
    public bool ValetMode { get; set; }
    public bool ValetPinNeeded { get; set; }
    public string? VehicleName { get; set; }
    public int VehicleSelfTestProgress { get; set; }
    public bool VehicleSelfTestRequested { get; set; }
    public bool WebcamAvailable { get; set; }
}

public class MediaInfoDto
{
    public string? A2dpSourceName { get; set; }
    public decimal AudioVolume { get; set; }
    public decimal AudioVolumeIncrement { get; set; }
    public decimal AudioVolumeMax { get; set; }
    public string? MediaPlaybackStatus { get; set; }
    public string? NowPlayingAlbum { get; set; }
    public string? NowPlayingArtist { get; set; }
    public int NowPlayingDuration { get; set; }
    public int NowPlayingElapsed { get; set; }
    public string? NowPlayingSource { get; set; }
    public string? NowPlayingStation { get; set; }
    public string? NowPlayingTitle { get; set; }
}

public class MediaStateDto
{
    public bool RemoteControlEnabled { get; set; }
}

public class SoftwareUpdateDto
{
    public int DownloadPerc { get; set; }
    public int ExpectedDurationSec { get; set; }
    public int InstallPerc { get; set; }
    public string? Status { get; set; }
    public string? Version { get; set; }
}

public class SpeedLimitModeDto
{
    public bool Active { get; set; }
    public int CurrentLimitMph { get; set; }
    public int MaxLimitMph { get; set; }
    public int MinLimitMph { get; set; }
    public bool PinCodeSet { get; set; }
}

public class VehicleSubscriptionsDto
{
    public int[]? Response { get; set; }
    public int Count { get; set; }
}

public class VehicleSubscriptionsSetDto
{
    public object[]? Response { get; set; }
    public int Count { get; set; }
}

public class WakeUpDto
{
    public WakeUpResponseDto? Response { get; set; }
}

public class WakeUpResponseDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int VehicleId { get; set; }
    public string? Vin { get; set; }
    public string? Color { get; set; }
    public string? AccessType { get; set; }
    public GranularAccessDto? GranularAccess { get; set; }
    public string[]? Tokens { get; set; }
    public string? State { get; set; }
    public bool InService { get; set; }
    public string? IdS { get; set; }
    public bool CalendarEnabled { get; set; }
    public int? ApiVersion { get; set; }
    public string? BackseatToken { get; set; }
    public string? BackseatTokenUpdatedAt { get; set; }
}

public class WarrantyDetailsDto
{
    public WarrantyDetailsResponseDto? Response { get; set; }
}

public class WarrantyDetailsResponseDto
{
    public WarrantyDto[]? ActiveWarranty { get; set; }
    public object[]? UpcomingWarranty { get; set; }
    public object[]? ExpiredWarranty { get; set; }
}

public class WarrantyDto
{
    public string? WarrantyType { get; set; }
    public string? WarrantyDisplayName { get; set; }
    public string? ExpirationDate { get; set; }
    public int ExpirationOdometer { get; set; }
    public string? OdometerUnit { get; set; }
    public string? WarrantyExpiredOn { get; set; }
    public int CoverageAgeInYears { get; set; }
}


using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.Entities;
using PolarDrive.Data.DbContexts.Gdpr;

namespace PolarDrive.Data.DbContexts;

public class PolarDriveDbContext(DbContextOptions<PolarDriveDbContext> options) : DbContext(options)
{
    public DbSet<ClientCompany> ClientCompanies => Set<ClientCompany>();
    public DbSet<ClientVehicle> ClientVehicles => Set<ClientVehicle>();
    public DbSet<ClientConsent> ClientConsents => Set<ClientConsent>();
    public DbSet<PdfReport> PdfReports => Set<PdfReport>();
    public DbSet<VehicleData> VehiclesData => Set<VehicleData>();
    public DbSet<VehicleDataArchive> VehiclesDataArchive => Set<VehicleDataArchive>();
    public DbSet<SmsAdaptiveGdpr> SmsAdaptiveGdpr { get; set; }
    public DbSet<SmsAdaptiveProfile> SmsAdaptiveProfile => Set<SmsAdaptiveProfile>();
    public DbSet<SmsAuditLog> SmsAuditLog { get; set; }
    public DbSet<OutagePeriod> OutagePeriods => Set<OutagePeriod>();
    public DbSet<ClientToken> ClientTokens => Set<ClientToken>();
    public DbSet<AdminFileManager> AdminFileManager => Set<AdminFileManager>();
    public DbSet<PhoneVehicleMapping> PhoneVehicleMappings { get; set; }
    public DbSet<ClientProfilePdf> ClientProfilePdfs => Set<ClientProfilePdf>();
    public DbSet<GapValidation> GapValidations => Set<GapValidation>();
    public DbSet<GapValidationPdf> GapValidationPdfs => Set<GapValidationPdf>();
    public DbSet<FetchFailureLog> FetchFailureLogs => Set<FetchFailureLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        try
        {
            base.OnModelCreating(modelBuilder);

            // ===== GDPR Value Converter per crittografia automatica PII =====
            // Cifra in scrittura, decifra in lettura - trasparente per l'applicazione
            GdprValueConverter? gdprConverter = null;
            GdprNullableValueConverter? gdprNullableConverter = null;
            GdprEncryptedStringComparer? gdprComparer = null;

            if (GdprValueConverterFactory.IsInitialized)
            {
                gdprConverter = GdprValueConverterFactory.Create();
                gdprNullableConverter = GdprValueConverterFactory.CreateNullable();
                gdprComparer = new GdprEncryptedStringComparer();
            }

            modelBuilder.Entity<ClientCompany>(entity =>
            {
                entity.HasIndex(e => e.VatNumber).IsUnique();
            });

            modelBuilder.Entity<ClientVehicle>(entity =>
            {
                entity.HasOne(e => e.ClientCompany)
                    .WithMany(cc => cc.ClientVehicles)
                    .HasForeignKey(e => e.ClientCompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                // ===== GDPR Encryption per campi PII =====
                if (gdprConverter != null && gdprNullableConverter != null && gdprComparer != null)
                {
                    // Non-nullable fields
                    entity.Property(e => e.Vin)
                        .HasConversion(gdprConverter)
                        .Metadata.SetValueComparer(gdprComparer);

                    entity.Property(e => e.VehicleMobileNumber)
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasConversion(gdprConverter)
                        .Metadata.SetValueComparer(gdprComparer);

                    // Nullable fields
                    entity.Property(e => e.ReferentName)
                        .HasConversion(gdprNullableConverter)
                        .Metadata.SetValueComparer(gdprComparer);

                    entity.Property(e => e.ReferentEmail)
                        .HasConversion(gdprNullableConverter)
                        .Metadata.SetValueComparer(gdprComparer);
                }
                else
                {
                    entity.Property(e => e.VehicleMobileNumber)
                        .IsRequired()
                        .HasMaxLength(100);
                }

                // Indici su campi hash per lookup esatto (invece di campi cifrati)
                entity.HasIndex(e => e.VinHash).IsUnique();
                entity.HasIndex(e => e.VehicleMobileNumberHash);
                entity.HasIndex(e => e.ReferentEmailHash);
            });

            modelBuilder.Entity<ClientConsent>(entity =>
            {
                entity.HasOne(e => e.ClientCompany)
                    .WithMany()
                    .HasForeignKey(e => e.ClientCompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ClientVehicle)
                    .WithMany()
                    .HasForeignKey(e => e.VehicleId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(e => e.ConsentHash);
            });

            modelBuilder.Entity<PdfReport>(entity =>
            {
                entity.HasOne(e => e.ClientCompany)
                    .WithMany(cc => cc.PdfReports)
                    .HasForeignKey(e => e.ClientCompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ClientVehicle)
                    .WithMany(v => v.PdfReports)
                    .HasForeignKey(e => e.VehicleId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(e => new { e.ClientCompanyId, e.VehicleId, e.ReportPeriodStart, e.ReportPeriodEnd })
                    .IsUnique();

                entity.Property(e => e.Status).HasMaxLength(50);

                entity.HasIndex(e => e.Status);

                entity.Property(e => e.GeneratedAt)
                    .IsRequired(false);

                entity.Property(e => e.PdfContent).HasColumnType("VARBINARY(MAX)").IsRequired(false);

                entity.Property(e => e.TsaTimestamp).HasColumnType("VARBINARY(MAX)").IsRequired(false);
                entity.Property(e => e.TsaServerUrl).HasMaxLength(500).IsRequired(false);
                entity.Property(e => e.TsaTimestampDate).IsRequired(false);
                entity.Property(e => e.TsaMessageImprint).HasMaxLength(128).IsRequired(false);
                entity.Property(e => e.TsaVerified).HasDefaultValue(false);
                entity.Property(e => e.TsaError).HasMaxLength(2000).IsRequired(false);
            });

            modelBuilder.Entity<VehicleData>(entity =>
            {
                entity.HasOne(e => e.ClientVehicle)
                    .WithMany(cv => cv.VehiclesData)
                    .HasForeignKey(e => e.VehicleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<VehicleDataArchive>(entity =>
            {
                entity.ToTable("VehiclesDataArchive");
                
                entity.HasIndex(e => e.VehicleId);
                entity.HasIndex(e => e.Timestamp);
            });

            modelBuilder.Entity<OutagePeriod>(entity =>
            {
                entity.HasOne(e => e.ClientVehicle)
                    .WithMany()
                    .HasForeignKey(e => e.VehicleId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.ClientCompany)
                    .WithMany()
                    .HasForeignKey(e => e.ClientCompanyId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => new { e.VehicleId, e.ClientCompanyId, e.OutageStart, e.OutageEnd })
                    .IsUnique();

                entity.HasIndex(e => e.ZipHash);

                entity.HasIndex(e => new { e.VehicleId, e.OutageStart, e.OutageEnd })
                    .HasDatabaseName("IX_OutagePeriods_GapLookup")
                    .IncludeProperties(e => new { e.OutageType, e.OutageBrand });
            });

            modelBuilder.Entity<ClientToken>(entity =>
            {
                entity.HasIndex(e => new { e.VehicleId, e.AccessTokenExpiresAt });

                entity.HasOne(e => e.ClientVehicle)
                    .WithOne()
                    .HasForeignKey<ClientToken>(e => e.VehicleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.VehicleId).IsUnique();
            });

            modelBuilder.Entity<AdminFileManager>(entity =>
            {
                entity.ToTable("AdminFileManager");

                entity.Property(e => e.RequestedAt)
                    .IsRequired()
                    .HasDefaultValueSql("GETDATE()");

                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasDefaultValue("PENDING");

                entity.Property(e => e.Notes)
                    .HasMaxLength(2000)
                    .IsRequired(false)
                    .HasDefaultValue(null);

                entity.Property(e => e.RequestedBy)
                    .HasMaxLength(100);

                entity.Property(e => e.ZipFileSizeMB)
                    .HasColumnType("FLOAT");

                entity.Property(e => e.ZipHash)
                    .HasMaxLength(64);

                entity.HasIndex(e => e.ZipHash);

                entity.Property(e => e.CompanyList)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                    .HasColumnType("NVARCHAR(MAX)");

                entity.Property(e => e.VinList)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                    .HasColumnType("NVARCHAR(MAX)");

                entity.Property(e => e.BrandList)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                    .HasColumnType("NVARCHAR(MAX)");

                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.RequestedAt);
                entity.HasIndex(e => e.PeriodStart);
                entity.HasIndex(e => e.PeriodEnd);
                entity.HasIndex(e => new { e.PeriodStart, e.PeriodEnd });
                entity.HasIndex(e => e.RequestedBy);
            });

            modelBuilder.Entity<SmsAdaptiveGdpr>(entity =>
            {
                entity.HasOne(e => e.ClientCompany)
                    .WithMany()
                    .HasForeignKey(e => e.ClientCompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                // ===== GDPR Encryption per campi PII =====
                if (gdprConverter != null && gdprNullableConverter != null && gdprComparer != null)
                {
                    // Non-nullable fields
                    entity.Property(e => e.AdaptiveNumber)
                        .HasConversion(gdprConverter)
                        .Metadata.SetValueComparer(gdprComparer);

                    entity.Property(e => e.AdaptiveSurnameName)
                        .HasConversion(gdprConverter)
                        .Metadata.SetValueComparer(gdprComparer);

                    // Nullable fields
                    entity.Property(e => e.IpAddress)
                        .HasConversion(gdprNullableConverter)
                        .Metadata.SetValueComparer(gdprComparer);

                    entity.Property(e => e.UserAgent)
                        .HasConversion(gdprNullableConverter)
                        .Metadata.SetValueComparer(gdprComparer);
                }

                // Indice univoco su HASH invece di campi cifrati
                entity.HasIndex(e => new { e.AdaptiveNumberHash, e.AdaptiveSurnameNameHash, e.Brand })
                    .IsUnique();

                entity.HasIndex(e => e.ConsentToken).IsUnique();
                entity.HasIndex(e => new { e.AdaptiveNumberHash, e.RequestedAt });
                entity.HasIndex(e => e.ConsentAccepted);
            });

            modelBuilder.Entity<SmsAdaptiveProfile>(entity =>
            {
                entity.HasOne(e => e.ClientVehicle)
                    .WithMany()
                    .HasForeignKey(e => e.VehicleId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Relazione con SmsAdaptiveGdpr
                entity.HasOne(e => e.SmsAdaptiveGdpr)
                    .WithMany()
                    .HasForeignKey(e => e.SmsAdaptiveGdprId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ===== GDPR Encryption per campi PII =====
                if (gdprConverter != null && gdprComparer != null)
                {
                    entity.Property(e => e.AdaptiveNumber)
                        .HasConversion(gdprConverter)
                        .Metadata.SetValueComparer(gdprComparer);

                    entity.Property(e => e.AdaptiveSurnameName)
                        .HasConversion(gdprConverter)
                        .Metadata.SetValueComparer(gdprComparer);

                    entity.Property(e => e.MessageContent)
                        .HasConversion(gdprConverter)
                        .Metadata.SetValueComparer(gdprComparer);
                }

                // Indice su hash invece di campi cifrati
                entity.HasIndex(e => new { e.AdaptiveNumberHash, e.AdaptiveSurnameNameHash });
            });

            modelBuilder.Entity<PhoneVehicleMapping>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                // ===== GDPR Encryption per campo PII =====
                if (gdprConverter != null && gdprComparer != null)
                {
                    entity.Property(e => e.PhoneNumber)
                        .IsRequired()
                        .HasConversion(gdprConverter)
                        .Metadata.SetValueComparer(gdprComparer);
                }
                else
                {
                    entity.Property(e => e.PhoneNumber)
                        .IsRequired();
                }

                entity.HasOne(e => e.ClientVehicle)
                    .WithMany()
                    .HasForeignKey(e => e.VehicleId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indice univoco su hash invece di campo cifrato
                entity.HasIndex(e => new { e.PhoneNumberHash, e.VehicleId }).IsUnique();
                entity.HasIndex(e => e.IsActive);
            });

            modelBuilder.Entity<SmsAuditLog>(entity =>
            {
                entity.HasOne(e => e.ResolvedVehicle)
                    .WithMany()
                    .HasForeignKey(e => e.VehicleIdResolved)
                    .OnDelete(DeleteBehavior.SetNull);

                // ===== GDPR Encryption per campi PII =====
                if (gdprConverter != null && gdprComparer != null)
                {
                    entity.Property(e => e.FromPhoneNumber)
                        .HasConversion(gdprConverter)
                        .Metadata.SetValueComparer(gdprComparer);

                    entity.Property(e => e.ToPhoneNumber)
                        .HasConversion(gdprConverter)
                        .Metadata.SetValueComparer(gdprComparer);

                    entity.Property(e => e.MessageBody)
                        .HasConversion(gdprConverter)
                        .Metadata.SetValueComparer(gdprComparer);
                }

                entity.HasIndex(e => e.MessageSid).IsUnique();
                // Indice su hash invece di campo cifrato
                entity.HasIndex(e => e.FromPhoneNumberHash);
                entity.HasIndex(e => e.ReceivedAt);
                entity.HasIndex(e => e.ProcessingStatus);
            });

            modelBuilder.Entity<ClientProfilePdf>(entity =>
            {
                entity.ToTable("ClientProfilePdf");

                entity.HasOne(e => e.ClientCompany)
                    .WithMany()
                    .HasForeignKey(e => e.ClientCompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.PdfContent).HasColumnType("VARBINARY(MAX)");
                entity.Property(e => e.FileName).HasMaxLength(255);
                entity.HasIndex(e => e.ClientCompanyId);
                entity.HasIndex(e => e.GeneratedAt);
            });

            modelBuilder.Entity<GapValidation>(entity =>
            {
                entity.ToTable("GapValidations");

                entity.HasOne(e => e.ClientVehicle)
                    .WithMany()
                    .HasForeignKey(e => e.VehicleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.PdfReport)
                    .WithMany()
                    .HasForeignKey(e => e.PdfReportId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.JustificationText).HasMaxLength(2000);
                entity.Property(e => e.ValidationHash).HasMaxLength(64);

                entity.HasIndex(e => new { e.VehicleId, e.PdfReportId });
                entity.HasIndex(e => e.GapTimestamp);
                entity.HasIndex(e => e.ValidatedAt);
            });

            modelBuilder.Entity<FetchFailureLog>(entity =>
            {
                entity.ToTable("FetchFailureLogs");

                entity.HasOne(e => e.ClientVehicle)
                    .WithMany()
                    .HasForeignKey(e => e.VehicleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.FailureReason).HasMaxLength(50);
                entity.Property(e => e.ErrorDetails).HasMaxLength(4000);
                entity.Property(e => e.RequestUrl).HasMaxLength(500);

                entity.HasIndex(e => new { e.VehicleId, e.AttemptedAt });
                entity.HasIndex(e => e.FailureReason);
                entity.HasIndex(e => e.AttemptedAt);
            });

            modelBuilder.Entity<GapValidationPdf>(entity =>
            {
                entity.ToTable("GapValidationPdfs");

                entity.HasOne(e => e.PdfReport)
                    .WithOne()
                    .HasForeignKey<GapValidationPdf>(e => e.PdfReportId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.PdfContent)
                    .HasColumnType("VARBINARY(MAX)")
                    .IsRequired(false);

                entity.Property(e => e.PdfHash)
                    .HasMaxLength(64)
                    .IsRequired(false);

                entity.Property(e => e.Status)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.AverageConfidence)
                    .HasColumnType("FLOAT");

                entity.HasIndex(e => e.PdfReportId).IsUnique();
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.PdfHash);

                // ===== TSA (Timestamp Authority) Fields =====
                entity.Property(e => e.TsaTimestamp).HasColumnType("VARBINARY(MAX)").IsRequired(false);
                entity.Property(e => e.TsaServerUrl).HasMaxLength(500).IsRequired(false);
                entity.Property(e => e.TsaTimestampDate).IsRequired(false);
                entity.Property(e => e.TsaMessageImprint).HasMaxLength(128).IsRequired(false);
                entity.Property(e => e.TsaVerified).HasDefaultValue(false);
                entity.Property(e => e.TsaError).HasMaxLength(2000).IsRequired(false);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during OnModelCreating: {ex.Message}");
            throw;
        }
    }
}
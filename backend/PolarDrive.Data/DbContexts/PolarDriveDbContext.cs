using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.Entities;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        try
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ClientCompany>(entity =>
            {
                entity.HasIndex(e => e.VatNumber).IsUnique();
            });

            modelBuilder.Entity<ClientVehicle>(entity =>
            {
                entity.HasIndex(e => e.Vin).IsUnique();

                entity.HasOne(e => e.ClientCompany)
                    .WithMany(cc => cc.ClientVehicles)
                    .HasForeignKey(e => e.ClientCompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
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

                entity.Property(e => e.GeneratedAt)
                    .IsRequired(false);

                entity.Property(e => e.PdfContent).HasColumnType("VARBINARY(MAX)").IsRequired(false);
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

                // Indice univoco su AdaptiveNumber + AdaptiveSurnameName + Brand
                entity.HasIndex(e => new { e.AdaptiveNumber, e.AdaptiveSurnameName, e.Brand })
                    .IsUnique();
                
                entity.HasIndex(e => e.ConsentToken).IsUnique();
                entity.HasIndex(e => new { e.AdaptiveNumber, e.RequestedAt });
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

                entity.HasIndex(e => new { e.AdaptiveNumber, e.AdaptiveSurnameName });
            });

            modelBuilder.Entity<PhoneVehicleMapping>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.PhoneNumber)
                    .IsRequired();

                entity.HasOne(e => e.ClientVehicle)
                    .WithMany()
                    .HasForeignKey(e => e.VehicleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.PhoneNumber, e.VehicleId }).IsUnique();
                entity.HasIndex(e => e.IsActive);
            });

            modelBuilder.Entity<SmsAuditLog>(entity =>
            {
                entity.HasOne(e => e.ResolvedVehicle)
                    .WithMany()
                    .HasForeignKey(e => e.VehicleIdResolved)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.MessageSid).IsUnique();
                entity.HasIndex(e => e.FromPhoneNumber);
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during OnModelCreating: {ex.Message}");
            throw;
        }
    }
}
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.Entities;

namespace PolarDrive.Data.DbContexts;

public class PolarDriveDbContext(DbContextOptions<PolarDriveDbContext> options) : DbContext(options)
{
    public DbSet<ClientCompany> ClientCompanies => Set<ClientCompany>();
    public DbSet<ClientVehicle> ClientVehicles => Set<ClientVehicle>();
    public DbSet<VehicleWorkflow> VehicleWorkflows => Set<VehicleWorkflow>();
    public DbSet<VehicleWorkflowEvent> VehicleWorkflowsEvent => Set<VehicleWorkflowEvent>();
    public DbSet<ClientConsent> ClientConsents => Set<ClientConsent>();
    public DbSet<PdfReport> PdfReports => Set<PdfReport>();
    public DbSet<VehicleData> VehiclesData => Set<VehicleData>();
    public DbSet<DemoSmsEvent> DemoSmsEvents => Set<DemoSmsEvent>();
    public DbSet<AnonymizedVehicleData> AnonymizedVehiclesData => Set<AnonymizedVehicleData>();
    public DbSet<OutagePeriod> OutagePeriods => Set<OutagePeriod>();
    public DbSet<ClientToken> ClientTokens => Set<ClientToken>();
    public DbSet<PolarDriveLog> PolarDriveLogs => Set<PolarDriveLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
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
            .WithMany()
            .HasForeignKey(e => e.ClientCompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VehicleWorkflow>(entity =>
        {
            entity.HasKey(e => e.VehicleId);

            entity.HasOne(e => e.ClientVehicle)
                .WithOne()
                .HasForeignKey<VehicleWorkflow>(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VehicleWorkflowEvent>(entity =>
        {
            entity.HasOne(e => e.ClientVehicle)
                .WithMany()
                .HasForeignKey(e => e.VehicleId)
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
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PdfReport>(entity =>
        {
            entity.HasOne(e => e.ClientCompany)
                .WithMany()
                .HasForeignKey(e => e.ClientCompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ClientVehicle)
                .WithMany()
                .HasForeignKey(e => e.ClientVehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ClientCompanyId, e.ClientVehicleId, e.ReportPeriodStart, e.ReportPeriodEnd })
                .IsUnique();

            entity.Property(e => e.GeneratedAt)
                .IsRequired(false);
        });

        modelBuilder.Entity<VehicleData>(entity =>
        {
            entity.HasOne(e => e.ClientVehicle)
                .WithMany()
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DemoSmsEvent>(entity =>
        {
            entity.HasOne(e => e.ClientVehicle)
                .WithMany()
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnonymizedVehicleData>(entity =>
        {
            entity.HasOne(e => e.OriginalData)
                .WithMany()
                .HasForeignKey(e => e.OriginalDataId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ClientVehicle)
                .WithMany()
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.OriginalDataId).IsUnique();
        });

        modelBuilder.Entity<OutagePeriod>(entity =>
        {
            entity.HasOne(e => e.ClientVehicle)
                .WithMany()
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ClientCompany)
                .WithMany()
                .HasForeignKey(e => e.ClientCompanyId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.VehicleId, e.ClientCompanyId, e.OutageStart, e.OutageEnd })
                .IsUnique();
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

        modelBuilder.Entity<PolarDriveLog>(entity =>
        {
            entity.Property(e => e.Timestamp)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.Level)
                .HasConversion<string>()
                .HasDefaultValue(PolarDriveLogLevel.INFO);
        });


    }
}

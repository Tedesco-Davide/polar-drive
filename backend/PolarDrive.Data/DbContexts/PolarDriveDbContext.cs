using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.Entities;

namespace PolarDrive.Data.DbContexts;

public class PolarDriveDbContext(DbContextOptions<PolarDriveDbContext> options) : DbContext(options)
{
    public DbSet<ClientCompany> ClientCompanies => Set<ClientCompany>();
    public DbSet<ClientTeslaVehicle> ClientTeslaVehicles => Set<ClientTeslaVehicle>();
    public DbSet<TeslaWorkflow> TeslaWorkflows => Set<TeslaWorkflow>();
    public DbSet<TeslaWorkflowEvent> TeslaWorkflowEvents => Set<TeslaWorkflowEvent>();
    public DbSet<ClientConsent> ClientConsents => Set<ClientConsent>();
    public DbSet<PdfReport> PdfReports => Set<PdfReport>();
    public DbSet<TeslaVehicleData> TeslaVehicleData => Set<TeslaVehicleData>();
    public DbSet<DemoSmsEvent> DemoSmsEvents => Set<DemoSmsEvent>();
    public DbSet<AnonymizedTeslaVehicleData> AnonymizedTeslaVehicleData => Set<AnonymizedTeslaVehicleData>();
    public DbSet<OutagePeriod> OutagePeriods => Set<OutagePeriod>();
    public DbSet<ClientTeslaToken> ClientTeslaTokens => Set<ClientTeslaToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ClientCompany>(entity =>
        {
            entity.HasIndex(e => e.VatNumber).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.PecAddress).IsUnique();
        });
        
        modelBuilder.Entity<ClientTeslaVehicle>(entity =>
        {
            entity.HasIndex(e => e.Vin).IsUnique();

            entity.HasOne(e => e.ClientCompany)
            .WithMany()
            .HasForeignKey(e => e.ClientCompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeslaWorkflow>(entity =>
        {
            entity.HasKey(e => e.TeslaVehicleId);

            entity.HasOne(e => e.ClientTeslaVehicle)
                .WithOne()
                .HasForeignKey<TeslaWorkflow>(e => e.TeslaVehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeslaWorkflowEvent>(entity =>
        {
            entity.HasOne(e => e.ClientTeslaVehicle)
                .WithMany()
                .HasForeignKey(e => e.TeslaVehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClientConsent>(entity =>
        {
            entity.HasOne(e => e.ClientCompany)
                .WithMany()
                .HasForeignKey(e => e.ClientCompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ClientTeslaVehicle)
                .WithMany()
                .HasForeignKey(e => e.TeslaVehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PdfReport>(entity =>{});

        modelBuilder.Entity<TeslaVehicleData>(entity =>
        {
            entity.HasOne(e => e.ClientTeslaVehicle)
                .WithMany()
                .HasForeignKey(e => e.TeslaVehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DemoSmsEvent>(entity =>
        {
            entity.HasOne(e => e.ClientTeslaVehicle)
                .WithMany()
                .HasForeignKey(e => e.TeslaVehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnonymizedTeslaVehicleData>(entity =>
        {
            entity.HasOne(e => e.OriginalData)
                .WithMany()
                .HasForeignKey(e => e.OriginalDataId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ClientTeslaVehicle)
                .WithMany()
                .HasForeignKey(e => e.TeslaVehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.OriginalDataId).IsUnique();
        });

        modelBuilder.Entity<OutagePeriod>(entity =>
        {
            entity.HasOne(e => e.ClientTeslaVehicle)
                .WithMany()
                .HasForeignKey(e => e.TeslaVehicleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ClientCompany)
                .WithMany()
                .HasForeignKey(e => e.ClientCompanyId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ClientTeslaToken>(entity =>
        {
            entity.HasIndex(e => new { e.TeslaVehicleId, e.AccessTokenExpiresAt });

            entity.HasOne(e => e.ClientTeslaVehicle)
                .WithOne()
                .HasForeignKey<ClientTeslaToken>(e => e.TeslaVehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.TeslaVehicleId).IsUnique();
        });

    }
}

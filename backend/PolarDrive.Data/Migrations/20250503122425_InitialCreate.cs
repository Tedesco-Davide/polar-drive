using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarDrive.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientCompanies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VatNumber = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    PecAddress = table.Column<string>(type: "TEXT", nullable: true),
                    LandlineNumber = table.Column<string>(type: "TEXT", nullable: true),
                    ReferentName = table.Column<string>(type: "TEXT", nullable: true),
                    ReferentMobileNumber = table.Column<string>(type: "TEXT", nullable: true),
                    ReferentEmail = table.Column<string>(type: "TEXT", nullable: true),
                    ReferentPecAddress = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientCompanies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PdfReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReportPeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReportPeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PdfFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompanyVatNumber = table.Column<string>(type: "TEXT", nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", nullable: false),
                    VehicleVin = table.Column<string>(type: "TEXT", nullable: false),
                    VehicleDisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientTeslaVehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientCompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Vin = table.Column<string>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    Trim = table.Column<string>(type: "TEXT", nullable: true),
                    Color = table.Column<string>(type: "TEXT", nullable: true),
                    IsActiveFlag = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFetchingDataFlag = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstActivationAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastDeactivationAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastFetchingDataAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientTeslaVehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientTeslaVehicles_ClientCompanies_ClientCompanyId",
                        column: x => x.ClientCompanyId,
                        principalTable: "ClientCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientConsents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientCompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeslaVehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    UploadDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ZipFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    ConsentHash = table.Column<string>(type: "TEXT", nullable: false),
                    ConsentType = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientConsents_ClientCompanies_ClientCompanyId",
                        column: x => x.ClientCompanyId,
                        principalTable: "ClientCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientConsents_ClientTeslaVehicles_TeslaVehicleId",
                        column: x => x.TeslaVehicleId,
                        principalTable: "ClientTeslaVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientTeslaTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeslaVehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccessToken = table.Column<string>(type: "TEXT", nullable: false),
                    RefreshToken = table.Column<string>(type: "TEXT", nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RefreshTokenExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientTeslaTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientTeslaTokens_ClientTeslaVehicles_TeslaVehicleId",
                        column: x => x.TeslaVehicleId,
                        principalTable: "ClientTeslaVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DemoSmsEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeslaVehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MessageContent = table.Column<string>(type: "TEXT", nullable: false),
                    ParsedCommand = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoSmsEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemoSmsEvents_ClientTeslaVehicles_TeslaVehicleId",
                        column: x => x.TeslaVehicleId,
                        principalTable: "ClientTeslaVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutagePeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AutoDetected = table.Column<bool>(type: "INTEGER", nullable: false),
                    OutageType = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OutageStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OutageEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TeslaVehicleId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientCompanyId = table.Column<int>(type: "INTEGER", nullable: true),
                    ZipFilePath = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutagePeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutagePeriods_ClientCompanies_ClientCompanyId",
                        column: x => x.ClientCompanyId,
                        principalTable: "ClientCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OutagePeriods_ClientTeslaVehicles_TeslaVehicleId",
                        column: x => x.TeslaVehicleId,
                        principalTable: "ClientTeslaVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TeslaVehicleData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeslaVehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RawJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeslaVehicleData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeslaVehicleData_ClientTeslaVehicles_TeslaVehicleId",
                        column: x => x.TeslaVehicleId,
                        principalTable: "ClientTeslaVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeslaWorkflowEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeslaVehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    FieldChanged = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<bool>(type: "INTEGER", nullable: false),
                    NewValue = table.Column<bool>(type: "INTEGER", nullable: false),
                    EventTimestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeslaWorkflowEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeslaWorkflowEvents_ClientTeslaVehicles_TeslaVehicleId",
                        column: x => x.TeslaVehicleId,
                        principalTable: "ClientTeslaVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeslaWorkflows",
                columns: table => new
                {
                    TeslaVehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActiveFlag = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFetchingDataFlag = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastStatusChangeAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeslaWorkflows", x => x.TeslaVehicleId);
                    table.ForeignKey(
                        name: "FK_TeslaWorkflows_ClientTeslaVehicles_TeslaVehicleId",
                        column: x => x.TeslaVehicleId,
                        principalTable: "ClientTeslaVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnonymizedTeslaVehicleData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OriginalDataId = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TeslaVehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    AnonymizedJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnonymizedTeslaVehicleData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnonymizedTeslaVehicleData_ClientTeslaVehicles_TeslaVehicleId",
                        column: x => x.TeslaVehicleId,
                        principalTable: "ClientTeslaVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnonymizedTeslaVehicleData_TeslaVehicleData_OriginalDataId",
                        column: x => x.OriginalDataId,
                        principalTable: "TeslaVehicleData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnonymizedTeslaVehicleData_OriginalDataId",
                table: "AnonymizedTeslaVehicleData",
                column: "OriginalDataId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnonymizedTeslaVehicleData_TeslaVehicleId",
                table: "AnonymizedTeslaVehicleData",
                column: "TeslaVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientCompanies_Email",
                table: "ClientCompanies",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientCompanies_PecAddress",
                table: "ClientCompanies",
                column: "PecAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientCompanies_VatNumber",
                table: "ClientCompanies",
                column: "VatNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientConsents_ClientCompanyId",
                table: "ClientConsents",
                column: "ClientCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientConsents_TeslaVehicleId",
                table: "ClientConsents",
                column: "TeslaVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTeslaTokens_TeslaVehicleId",
                table: "ClientTeslaTokens",
                column: "TeslaVehicleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientTeslaTokens_TeslaVehicleId_AccessTokenExpiresAt",
                table: "ClientTeslaTokens",
                columns: new[] { "TeslaVehicleId", "AccessTokenExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientTeslaVehicles_ClientCompanyId",
                table: "ClientTeslaVehicles",
                column: "ClientCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTeslaVehicles_Vin",
                table: "ClientTeslaVehicles",
                column: "Vin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemoSmsEvents_TeslaVehicleId",
                table: "DemoSmsEvents",
                column: "TeslaVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_OutagePeriods_ClientCompanyId",
                table: "OutagePeriods",
                column: "ClientCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_OutagePeriods_TeslaVehicleId",
                table: "OutagePeriods",
                column: "TeslaVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_TeslaVehicleData_TeslaVehicleId",
                table: "TeslaVehicleData",
                column: "TeslaVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_TeslaWorkflowEvents_TeslaVehicleId",
                table: "TeslaWorkflowEvents",
                column: "TeslaVehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnonymizedTeslaVehicleData");

            migrationBuilder.DropTable(
                name: "ClientConsents");

            migrationBuilder.DropTable(
                name: "ClientTeslaTokens");

            migrationBuilder.DropTable(
                name: "DemoSmsEvents");

            migrationBuilder.DropTable(
                name: "OutagePeriods");

            migrationBuilder.DropTable(
                name: "PdfReports");

            migrationBuilder.DropTable(
                name: "TeslaWorkflowEvents");

            migrationBuilder.DropTable(
                name: "TeslaWorkflows");

            migrationBuilder.DropTable(
                name: "TeslaVehicleData");

            migrationBuilder.DropTable(
                name: "ClientTeslaVehicles");

            migrationBuilder.DropTable(
                name: "ClientCompanies");
        }
    }
}

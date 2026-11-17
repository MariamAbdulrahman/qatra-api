using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Qatra.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceKeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Neighborhoods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PriorCycleDays = table.Column<double>(type: "float", nullable: false),
                    PriorCycleSd = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Neighborhoods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NeighborhoodId = table.Column<int>(type: "int", nullable: false),
                    IssueType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaxDurationDays = table.Column<int>(type: "int", nullable: true),
                    DistinctDevices = table.Column<int>(type: "int", nullable: false),
                    IsRepeat = table.Column<bool>(type: "bit", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActualArrivalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WasAbnormal = table.Column<bool>(type: "bit", nullable: true),
                    LastReportAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidents_Neighborhoods_NeighborhoodId",
                        column: x => x.NeighborhoodId,
                        principalTable: "Neighborhoods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Predictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NeighborhoodId = table.Column<int>(type: "int", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Inputs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Outputs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Predictions_Neighborhoods_NeighborhoodId",
                        column: x => x.NeighborhoodId,
                        principalTable: "Neighborhoods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WaterSupplyEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NeighborhoodId = table.Column<int>(type: "int", nullable: false),
                    EventDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterSupplyEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaterSupplyEvents_Neighborhoods_NeighborhoodId",
                        column: x => x.NeighborhoodId,
                        principalTable: "Neighborhoods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    NeighborhoodId = table.Column<int>(type: "int", nullable: false),
                    IncidentId = table.Column<int>(type: "int", nullable: true),
                    RawText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    QuickButton = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IssueType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DurationDays = table.Column<int>(type: "int", nullable: true),
                    EventDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WaterArrived = table.Column<bool>(type: "bit", nullable: false),
                    ProcessingStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExtractionConfidence = table.Column<double>(type: "float", nullable: true),
                    HumanLabel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reports_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Neighborhoods_NeighborhoodId",
                        column: x => x.NeighborhoodId,
                        principalTable: "Neighborhoods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Neighborhoods",
                columns: new[] { "Id", "Name", "PriorCycleDays", "PriorCycleSd" },
                values: new object[,]
                {
                    { 1, "الديس", 4.0, 1.0 },
                    { 2, "فوه", 5.0, 1.2 },
                    { 3, "الشرج", 7.0, 1.5 },
                    { 4, "روكب", 9.0, 2.0 },
                    { 5, "السلام", 6.0, 1.5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceKeyHash",
                table: "Devices",
                column: "DeviceKeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_NeighborhoodId_Status_LastReportAt",
                table: "Incidents",
                columns: new[] { "NeighborhoodId", "Status", "LastReportAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Neighborhoods_Name",
                table: "Neighborhoods",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_NeighborhoodId_CreatedAt",
                table: "Predictions",
                columns: new[] { "NeighborhoodId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_DeviceId_CreatedAt",
                table: "Reports",
                columns: new[] { "DeviceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_IncidentId",
                table: "Reports",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_NeighborhoodId_ProcessingStatus",
                table: "Reports",
                columns: new[] { "NeighborhoodId", "ProcessingStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_WaterSupplyEvents_NeighborhoodId_EventDate",
                table: "WaterSupplyEvents",
                columns: new[] { "NeighborhoodId", "EventDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Predictions");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "WaterSupplyEvents");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "Neighborhoods");
        }
    }
}

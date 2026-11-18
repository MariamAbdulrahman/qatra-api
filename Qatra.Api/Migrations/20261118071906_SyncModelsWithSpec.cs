using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Qatra.Api.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelsWithSpec : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Neighborhoods",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Neighborhoods",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Neighborhoods",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Neighborhoods",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Neighborhoods",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Neighborhoods",
                newName: "NameAr");

            migrationBuilder.RenameIndex(
                name: "IX_Neighborhoods_Name",
                table: "Neighborhoods",
                newName: "IX_Neighborhoods_NameAr");

            migrationBuilder.RenameColumn(
                name: "ActualArrivalDate",
                table: "Incidents",
                newName: "ResolvedAt");

            migrationBuilder.AlterColumn<string>(
                name: "QuickButton",
                table: "Reports",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsFollowup",
                table: "Reports",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Lat",
                table: "Neighborhoods",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Lng",
                table: "Neighborhoods",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "Population",
                table: "Neighborhoods",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "UrgentHealth",
                table: "Incidents",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NeedsFollowup",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Lat",
                table: "Neighborhoods");

            migrationBuilder.DropColumn(
                name: "Lng",
                table: "Neighborhoods");

            migrationBuilder.DropColumn(
                name: "Population",
                table: "Neighborhoods");

            migrationBuilder.DropColumn(
                name: "UrgentHealth",
                table: "Incidents");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "Neighborhoods",
                newName: "Name");

            migrationBuilder.RenameIndex(
                name: "IX_Neighborhoods_NameAr",
                table: "Neighborhoods",
                newName: "IX_Neighborhoods_Name");

            migrationBuilder.RenameColumn(
                name: "ResolvedAt",
                table: "Incidents",
                newName: "ActualArrivalDate");

            migrationBuilder.AlterColumn<string>(
                name: "QuickButton",
                table: "Reports",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

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
        }
    }
}

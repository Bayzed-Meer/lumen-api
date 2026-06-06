using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lumen.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpPurposeAndIsUsed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUsed",
                table: "OtpRecords",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "OtpRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsUsed",
                table: "OtpRecords");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "OtpRecords");
        }
    }
}

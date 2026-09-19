using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pf2e.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CampaignShell : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DmKey",
                table: "Campaigns",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // "Exploration" rather than the scaffolder's empty string, which is not a mode and
            // would throw the moment a campaign written before this migration was read.
            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "Campaigns",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Exploration");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DmKey",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "Campaigns");
        }
    }
}

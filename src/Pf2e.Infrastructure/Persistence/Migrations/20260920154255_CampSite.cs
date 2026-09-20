using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pf2e.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CampSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Camp",
                table: "Campaigns",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Camp",
                table: "Campaigns");
        }
    }
}

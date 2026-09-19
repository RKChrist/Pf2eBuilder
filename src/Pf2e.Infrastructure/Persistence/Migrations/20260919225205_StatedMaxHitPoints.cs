using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pf2e.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StatedMaxHitPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StatedMaxHitPoints",
                table: "TrackedCharacters",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatedMaxHitPoints",
                table: "TrackedCharacters");
        }
    }
}

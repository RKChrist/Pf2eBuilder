using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pf2e.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SheetFeatsAndSpells : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Feats",
                table: "TrackedCharacters",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Spells",
                table: "TrackedCharacters",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Feats",
                table: "TrackedCharacters");

            migrationBuilder.DropColumn(
                name: "Spells",
                table: "TrackedCharacters");
        }
    }
}

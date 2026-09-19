using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pf2e.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SkillsWeaponsAndSpellcasting : Migration
    {
        /// <inheritdoc />
        // A character imported before this migration has no skills, no weapons and no
        // spellcasting recorded. The list columns default to an empty JSON array rather than an
        // empty string so a row reads the same whether it predates this or was written after it,
        // and the next re-import fills them from the export.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Skills",
                table: "TrackedCharacters",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Spellcasting",
                table: "TrackedCharacters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Weapons",
                table: "TrackedCharacters",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Skills",
                table: "TrackedCharacters");

            migrationBuilder.DropColumn(
                name: "Spellcasting",
                table: "TrackedCharacters");

            migrationBuilder.DropColumn(
                name: "Weapons",
                table: "TrackedCharacters");
        }
    }
}

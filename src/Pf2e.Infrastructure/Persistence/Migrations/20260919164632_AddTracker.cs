using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pf2e.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTracker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackedTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrackedCharacters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TableId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    ClassName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AncestryName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    KeyAttribute = table.Column<int>(type: "INTEGER", nullable: false),
                    Strength = table.Column<int>(type: "INTEGER", nullable: false),
                    Dexterity = table.Column<int>(type: "INTEGER", nullable: false),
                    Constitution = table.Column<int>(type: "INTEGER", nullable: false),
                    Intelligence = table.Column<int>(type: "INTEGER", nullable: false),
                    Wisdom = table.Column<int>(type: "INTEGER", nullable: false),
                    Charisma = table.Column<int>(type: "INTEGER", nullable: false),
                    Fortitude = table.Column<int>(type: "INTEGER", nullable: false),
                    Reflex = table.Column<int>(type: "INTEGER", nullable: false),
                    Will = table.Column<int>(type: "INTEGER", nullable: false),
                    Perception = table.Column<int>(type: "INTEGER", nullable: false),
                    ClassDc = table.Column<int>(type: "INTEGER", nullable: false),
                    ArmorRank = table.Column<int>(type: "INTEGER", nullable: false),
                    ArmorName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ArmorItemBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    ArmorDexCap = table.Column<int>(type: "INTEGER", nullable: true),
                    AncestryHitPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    ClassHitPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    BonusHitPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    BonusHitPointsPerLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentHitPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    TemporaryHitPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    HeroPoints = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedCharacters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackedCharacters_TrackedTables_TableId",
                        column: x => x.TableId,
                        principalTable: "TrackedTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrackedEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Value = table.Column<int>(type: "INTEGER", nullable: false),
                    Duration = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SourceKind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Modifiers = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackedEffects_TrackedCharacters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "TrackedCharacters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackedCharacters_TableId",
                table: "TrackedCharacters",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedEffects_CharacterId",
                table: "TrackedEffects",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedTables_Code",
                table: "TrackedTables",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackedEffects");

            migrationBuilder.DropTable(
                name: "TrackedCharacters");

            migrationBuilder.DropTable(
                name: "TrackedTables");
        }
    }
}

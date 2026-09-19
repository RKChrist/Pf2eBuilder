using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pf2e.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EffectApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dropped rather than migrated. An effect hanging off one character cannot say what
            // reached five, so there is no row here that means anything in the new shape, and
            // the only databases this runs against are the disposable ones on developers' disks.
            migrationBuilder.DropTable(
                name: "TrackedEffects");

            migrationBuilder.CreateTable(
                name: "EffectApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SourceKind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Modifiers = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Timing = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    SourceCombatantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PersistentDamage = table.Column<int>(type: "INTEGER", nullable: true),
                    PersistentDamageType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EffectApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EffectApplications_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EffectTargets",
                columns: table => new
                {
                    ApplicationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Value = table.Column<int>(type: "INTEGER", nullable: false),
                    RemainingRounds = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EffectTargets", x => new { x.ApplicationId, x.TargetId });
                    table.ForeignKey(
                        name: "FK_EffectTargets_EffectApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "EffectApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EffectApplications_CampaignId",
                table: "EffectApplications",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_EffectTargets_TargetId",
                table: "EffectTargets",
                column: "TargetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EffectTargets");

            migrationBuilder.DropTable(
                name: "EffectApplications");

            migrationBuilder.CreateTable(
                name: "TrackedEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Duration = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Modifiers = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SourceKind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Value = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "IX_TrackedEffects_CharacterId",
                table: "TrackedEffects",
                column: "CharacterId");
        }
    }
}

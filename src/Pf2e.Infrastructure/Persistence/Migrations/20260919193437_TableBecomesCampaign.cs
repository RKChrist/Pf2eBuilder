using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pf2e.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Written by hand over what the scaffolder produced. It wanted to drop TrackedTables and
    /// create Campaigns, which loses every row and leaves the characters pointing at nothing. A
    /// rename is what this change actually is, and SQLite rewrites the foreign key clauses in
    /// the referencing table when a table is renamed.
    /// </summary>
    public partial class TableBecomesCampaign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "TrackedTables", newName: "Campaigns");

            migrationBuilder.RenameIndex(
                name: "IX_TrackedTables_Code",
                table: "Campaigns",
                newName: "IX_Campaigns_Code");

            migrationBuilder.RenameColumn(
                name: "TableId",
                table: "TrackedCharacters",
                newName: "CampaignId");

            migrationBuilder.RenameIndex(
                name: "IX_TrackedCharacters_TableId",
                table: "TrackedCharacters",
                newName: "IX_TrackedCharacters_CampaignId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_TrackedCharacters_CampaignId",
                table: "TrackedCharacters",
                newName: "IX_TrackedCharacters_TableId");

            migrationBuilder.RenameColumn(
                name: "CampaignId",
                table: "TrackedCharacters",
                newName: "TableId");

            migrationBuilder.RenameIndex(
                name: "IX_Campaigns_Code",
                table: "Campaigns",
                newName: "IX_TrackedTables_Code");

            migrationBuilder.RenameTable(name: "Campaigns", newName: "TrackedTables");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pf2e.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuleRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    RulesetVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: true),
                    Rarity = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PrimarySource = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Traits = table.Column<string>(type: "TEXT", nullable: false),
                    Mechanics = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeedState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RulesetVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RecordCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SeededAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeedState", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuleRecords_Category_Level",
                table: "RuleRecords",
                columns: new[] { "Category", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_RuleRecords_Category_Name",
                table: "RuleRecords",
                columns: new[] { "Category", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuleRecords");

            migrationBuilder.DropTable(
                name: "SeedState");
        }
    }
}

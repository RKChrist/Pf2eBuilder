using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pf2e.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DowntimeDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DowntimeActivity",
                table: "TrackedCharacters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DowntimeTaskLevel",
                table: "TrackedCharacters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Day",
                table: "Campaigns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DowntimeActivity",
                table: "TrackedCharacters");

            migrationBuilder.DropColumn(
                name: "DowntimeTaskLevel",
                table: "TrackedCharacters");

            migrationBuilder.DropColumn(
                name: "Day",
                table: "Campaigns");
        }
    }
}

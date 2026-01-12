using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoveLetter.App.Migrations
{
    /// <inheritdoc />
    public partial class AddTravelCountries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TravelCountries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    CountryName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IsVisited = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VisitedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelCountries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TravelCountries_CountryCode",
                table: "TravelCountries",
                column: "CountryCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TravelCountries");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoveLetter.App.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchlistMovies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WatchlistMovies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImdbId = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    Year = table.Column<string>(type: "TEXT", maxLength: 12, nullable: true),
                    PosterUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Watched = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WatchedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistMovies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistMovies_ImdbId",
                table: "WatchlistMovies",
                column: "ImdbId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchlistMovies");
        }
    }
}

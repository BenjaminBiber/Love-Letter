using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoveLetter.App.Migrations
{
    /// <inheritdoc />
    public partial class AddBucketListMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BucketListMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    IsVideo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BucketListMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BucketListMedia_BucketListEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "BucketListEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BucketListMedia_EntryId",
                table: "BucketListMedia",
                column: "EntryId");

            migrationBuilder.Sql(@"
INSERT INTO BucketListMedia (Id, EntryId, FilePath, OriginalFileName, ContentType, IsVideo, CreatedAt)
SELECT Id,
       Id,
       PhotoPath,
       PhotoOriginalFileName,
       NULL,
       0,
       COALESCE(CompletedAt, CreatedAt, CURRENT_TIMESTAMP)
FROM BucketListEntries
WHERE PhotoPath IS NOT NULL AND LENGTH(TRIM(PhotoPath)) > 0;
");

            migrationBuilder.DropColumn(
                name: "PhotoOriginalFileName",
                table: "BucketListEntries");

            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "BucketListEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoOriginalFileName",
                table: "BucketListEntries",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "BucketListEntries",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE BucketListEntries
SET PhotoPath = (
        SELECT FilePath
        FROM BucketListMedia
        WHERE BucketListMedia.EntryId = BucketListEntries.Id
        ORDER BY CreatedAt
        LIMIT 1
    ),
    PhotoOriginalFileName = (
        SELECT OriginalFileName
        FROM BucketListMedia
        WHERE BucketListMedia.EntryId = BucketListEntries.Id
        ORDER BY CreatedAt
        LIMIT 1
    );
");

            migrationBuilder.DropTable(
                name: "BucketListMedia");
        }
    }
}

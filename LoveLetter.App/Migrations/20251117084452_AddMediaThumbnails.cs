using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoveLetter.App.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaThumbnails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThumbnailPath",
                table: "GalleryPhotos",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailPath",
                table: "BucketListMedia",
                type: "TEXT",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThumbnailPath",
                table: "GalleryPhotos");

            migrationBuilder.DropColumn(
                name: "ThumbnailPath",
                table: "BucketListMedia");
        }
    }
}

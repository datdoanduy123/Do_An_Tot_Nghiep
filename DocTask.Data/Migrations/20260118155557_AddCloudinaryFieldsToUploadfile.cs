using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocTask.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudinaryFieldsToUploadfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "uploadfile",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "uploadfile",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "uploadfile",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "uploadfile");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "uploadfile");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "uploadfile");
        }
    }
}

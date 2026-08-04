using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogGraphQlApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaTypeToVideoCalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MediaType",
                table: "ActiveVideoCalls",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ActiveVideoCalls_MediaType",
                table: "ActiveVideoCalls",
                column: "MediaType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActiveVideoCalls_MediaType",
                table: "ActiveVideoCalls");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "ActiveVideoCalls");
        }
    }
}

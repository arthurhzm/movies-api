using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoviesAPI.Migrations
{
    /// <inheritdoc />
    public partial class ApiKeyColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiKey",
                table: "Auth",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auth_Email",
                table: "Auth",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Auth_Email",
                table: "Auth");

            migrationBuilder.DropColumn(
                name: "ApiKey",
                table: "Auth");
        }
    }
}

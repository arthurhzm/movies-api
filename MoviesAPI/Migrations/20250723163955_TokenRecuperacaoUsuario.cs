using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoviesAPI.Migrations
{
    /// <inheritdoc />
    public partial class TokenRecuperacaoUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecoveryToken",
                table: "Auth",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_UserMovieFeedback_UserId",
                table: "UserMovieFeedback",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserMovieFeedback_Auth_UserId",
                table: "UserMovieFeedback",
                column: "UserId",
                principalTable: "Auth",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserMovieFeedback_Auth_UserId",
                table: "UserMovieFeedback");

            migrationBuilder.DropIndex(
                name: "IX_UserMovieFeedback_UserId",
                table: "UserMovieFeedback");

            migrationBuilder.DropColumn(
                name: "RecoveryToken",
                table: "Auth");
        }
    }
}

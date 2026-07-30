using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoviesAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddTmdbIdToFeedbackAndMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TmdbId",
                table: "UserMovieFeedback",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TmdbId",
                table: "Matches",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMovieFeedback_UserId_TmdbId",
                table: "UserMovieFeedback",
                columns: new[] { "UserId", "TmdbId" },
                filter: "\"TmdbId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMovieFeedback_UserId_TmdbId",
                table: "UserMovieFeedback");

            migrationBuilder.DropColumn(
                name: "TmdbId",
                table: "UserMovieFeedback");

            migrationBuilder.DropColumn(
                name: "TmdbId",
                table: "Matches");
        }
    }
}

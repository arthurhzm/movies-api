using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoviesAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddLetterboxdFullHistoryImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMovieFeedback_UserId",
                table: "UserMovieFeedback");

            migrationBuilder.AddColumn<string>(
                name: "LetterboxdUri",
                table: "UserMovieFeedback",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MovieYear",
                table: "UserMovieFeedback",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LetterboxdLastImport",
                table: "Auth",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMovieFeedback_UserId_LetterboxdUri",
                table: "UserMovieFeedback",
                columns: new[] { "UserId", "LetterboxdUri" },
                unique: true,
                filter: "\"LetterboxdUri\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMovieFeedback_UserId_LetterboxdUri",
                table: "UserMovieFeedback");

            migrationBuilder.DropColumn(
                name: "LetterboxdUri",
                table: "UserMovieFeedback");

            migrationBuilder.DropColumn(
                name: "MovieYear",
                table: "UserMovieFeedback");

            migrationBuilder.DropColumn(
                name: "LetterboxdLastImport",
                table: "Auth");

            migrationBuilder.CreateIndex(
                name: "IX_UserMovieFeedback_UserId",
                table: "UserMovieFeedback",
                column: "UserId");
        }
    }
}

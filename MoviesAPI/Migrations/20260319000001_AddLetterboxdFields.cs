using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoviesAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddLetterboxdFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LetterboxdUsername",
                table: "Auth",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LetterboxdLastSync",
                table: "Auth",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LetterboxdUsername",
                table: "Auth");

            migrationBuilder.DropColumn(
                name: "LetterboxdLastSync",
                table: "Auth");
        }
    }
}

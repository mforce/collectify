using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collectify.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddRichDetailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ReleaseDate",
                table: "MusicAlbums",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cast",
                table: "Movies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "ProviderRating",
                table: "Movies",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReleaseDate",
                table: "Movies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgeRating",
                table: "Games",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReleaseDate",
                table: "Games",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "MusicAlbums");

            migrationBuilder.DropColumn(
                name: "Cast",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "ProviderRating",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "AgeRating",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "Games");
        }
    }
}

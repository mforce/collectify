using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collectify.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalAcquisitionAndTagFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AcquiredOn",
                table: "MusicAlbums",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcquisitionCurrency",
                table: "MusicAlbums",
                type: "TEXT",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcquisitionPrice",
                table: "MusicAlbums",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcquisitionSource",
                table: "MusicAlbums",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Condition",
                table: "MusicAlbums",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "MusicAlbums",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastPlayedOn",
                table: "MusicAlbums",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ListenCount",
                table: "MusicAlbums",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PersonalRating",
                table: "MusicAlbums",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "MusicAlbums",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AcquiredOn",
                table: "Movies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcquisitionCurrency",
                table: "Movies",
                type: "TEXT",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcquisitionPrice",
                table: "Movies",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcquisitionSource",
                table: "Movies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Condition",
                table: "Movies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Movies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastWatchedOn",
                table: "Movies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PersonalRating",
                table: "Movies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Movies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WatchCount",
                table: "Movies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WatchStatus",
                table: "Movies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AcquiredOn",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcquisitionCurrency",
                table: "Games",
                type: "TEXT",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcquisitionPrice",
                table: "Games",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcquisitionSource",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompletionStatus",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Condition",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HoursPlayed",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastPlayedOn",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PersonalRating",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameTag",
                columns: table => new
                {
                    GamesId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTag", x => new { x.GamesId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_GameTag_Games_GamesId",
                        column: x => x.GamesId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovieTag",
                columns: table => new
                {
                    MoviesId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieTag", x => new { x.MoviesId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_MovieTag_Movies_MoviesId",
                        column: x => x.MoviesId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovieTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MusicAlbumTag",
                columns: table => new
                {
                    MusicAlbumsId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicAlbumTag", x => new { x.MusicAlbumsId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_MusicAlbumTag_MusicAlbums_MusicAlbumsId",
                        column: x => x.MusicAlbumsId,
                        principalTable: "MusicAlbums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MusicAlbumTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameTag_TagsId",
                table: "GameTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_MovieTag_TagsId",
                table: "MovieTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicAlbumTag_TagsId",
                table: "MusicAlbumTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_OwnerId_Name",
                table: "Tags",
                columns: new[] { "OwnerId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameTag");

            migrationBuilder.DropTable(
                name: "MovieTag");

            migrationBuilder.DropTable(
                name: "MusicAlbumTag");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropColumn(
                name: "AcquiredOn",
                table: "MusicAlbums");

            migrationBuilder.DropColumn(
                name: "AcquisitionCurrency",
                table: "MusicAlbums");

            migrationBuilder.DropColumn(
                name: "AcquisitionPrice",
                table: "MusicAlbums");

            migrationBuilder.DropColumn(
                name: "AcquisitionSource",
                table: "MusicAlbums");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "MusicAlbums");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "MusicAlbums");

            migrationBuilder.DropColumn(
                name: "LastPlayedOn",
                table: "MusicAlbums");

            migrationBuilder.DropColumn(
                name: "ListenCount",
                table: "MusicAlbums");

            migrationBuilder.DropColumn(
                name: "PersonalRating",
                table: "MusicAlbums");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "MusicAlbums");

            migrationBuilder.DropColumn(
                name: "AcquiredOn",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "AcquisitionCurrency",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "AcquisitionPrice",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "AcquisitionSource",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "LastWatchedOn",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "PersonalRating",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "WatchCount",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "WatchStatus",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "AcquiredOn",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AcquisitionCurrency",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AcquisitionPrice",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AcquisitionSource",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CompletionStatus",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HoursPlayed",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "LastPlayedOn",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "PersonalRating",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Games");
        }
    }
}

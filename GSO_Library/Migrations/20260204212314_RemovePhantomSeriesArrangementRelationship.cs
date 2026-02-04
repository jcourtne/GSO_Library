using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSO_Library.Migrations
{
    /// <inheritdoc />
    public partial class RemovePhantomSeriesArrangementRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Arrangements_Series_SeriesId",
                table: "Arrangements");

            migrationBuilder.DropIndex(
                name: "IX_Arrangements_SeriesId",
                table: "Arrangements");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "Arrangements");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeriesId",
                table: "Arrangements",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Arrangements_SeriesId",
                table: "Arrangements",
                column: "SeriesId");

            migrationBuilder.AddForeignKey(
                name: "FK_Arrangements_Series_SeriesId",
                table: "Arrangements",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id");
        }
    }
}

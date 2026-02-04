using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSO_Library.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDifficultyFromArrangements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Arrangements");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Difficulty",
                table: "Arrangements",
                type: "int",
                nullable: true);
        }
    }
}

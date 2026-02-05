using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSO_Library.Migrations
{
    /// <inheritdoc />
    public partial class ChangePerformanceToManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Performances_Arrangements_ArrangementId",
                table: "Performances");

            migrationBuilder.DropIndex(
                name: "IX_Performances_ArrangementId",
                table: "Performances");

            migrationBuilder.DropColumn(
                name: "ArrangementId",
                table: "Performances");

            migrationBuilder.CreateTable(
                name: "ArrangementPerformances",
                columns: table => new
                {
                    ArrangementsId = table.Column<int>(type: "int", nullable: false),
                    PerformancesId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArrangementPerformances", x => new { x.ArrangementsId, x.PerformancesId });
                    table.ForeignKey(
                        name: "FK_ArrangementPerformances_Arrangements_ArrangementsId",
                        column: x => x.ArrangementsId,
                        principalTable: "Arrangements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArrangementPerformances_Performances_PerformancesId",
                        column: x => x.PerformancesId,
                        principalTable: "Performances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArrangementPerformances_PerformancesId",
                table: "ArrangementPerformances",
                column: "PerformancesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArrangementPerformances");

            migrationBuilder.AddColumn<int>(
                name: "ArrangementId",
                table: "Performances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Performances_ArrangementId",
                table: "Performances",
                column: "ArrangementId");

            migrationBuilder.AddForeignKey(
                name: "FK_Performances_Arrangements_ArrangementId",
                table: "Performances",
                column: "ArrangementId",
                principalTable: "Arrangements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

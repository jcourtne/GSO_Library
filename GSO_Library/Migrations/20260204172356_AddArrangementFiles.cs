using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSO_Library.Migrations
{
    /// <inheritdoc />
    public partial class AddArrangementFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrangementFilePath",
                table: "Arrangements");

            migrationBuilder.DropColumn(
                name: "PdfFilePath",
                table: "Arrangements");

            migrationBuilder.CreateTable(
                name: "ArrangementFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArrangementId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArrangementFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArrangementFiles_Arrangements_ArrangementId",
                        column: x => x.ArrangementId,
                        principalTable: "Arrangements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArrangementFiles_ArrangementId",
                table: "ArrangementFiles",
                column: "ArrangementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArrangementFiles");

            migrationBuilder.AddColumn<string>(
                name: "ArrangementFilePath",
                table: "Arrangements",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfFilePath",
                table: "Arrangements",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HngStageOne.Api.Migrations
{
    public partial class StageTwoQueryableEngine : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Profiles");

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false, collation: "NOCASE"),
                    Gender = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    GenderProbability = table.Column<double>(type: "REAL", nullable: false),
                    Age = table.Column<int>(type: "INTEGER", nullable: false),
                    AgeGroup = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CountryId = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    CountryName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CountryProbability = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_Age",
                table: "Profiles",
                column: "Age");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_AgeGroup",
                table: "Profiles",
                column: "AgeGroup");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_CountryId",
                table: "Profiles",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_CreatedAt",
                table: "Profiles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_Gender",
                table: "Profiles",
                column: "Gender");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_Name",
                table: "Profiles",
                column: "Name",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Profiles");

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "randomblob(16)"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Gender = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    GenderProbability = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    SampleSize = table.Column<int>(type: "INTEGER", nullable: false),
                    Age = table.Column<int>(type: "INTEGER", nullable: false),
                    AgeGroup = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CountryId = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CountryProbability = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_NormalizedName",
                table: "Profiles",
                column: "NormalizedName",
                unique: true);
        }
    }
}

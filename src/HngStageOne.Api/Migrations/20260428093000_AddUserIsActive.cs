using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HngStageOne.Api.Migrations
{
    [Migration("20260428093000_AddUserIsActive")]
    public partial class AddUserIsActive : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AuthSchemaInitializer adds this column idempotently for SQLite.
            // Keeping this migration as a model-snapshot marker avoids duplicate-column
            // failures for databases that already created Users during Stage 3 testing.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

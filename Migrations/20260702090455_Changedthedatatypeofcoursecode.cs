using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TmsApi.Migrations
{
    /// <inheritdoc />
    public partial class Changedthedatatypeofcoursecode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""Courses"" ALTER COLUMN ""Code"" TYPE integer USING ""Code""::integer;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""Courses"" ALTER COLUMN ""Code"" TYPE character varying(20) USING ""Code""::character varying;");
        }
    }
}

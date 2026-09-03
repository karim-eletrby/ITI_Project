using Infrastructure.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260902020000_AddUniqueUsernameIndex")]
    public partial class AddUniqueUsernameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE u
                SET
                    u.DisplayName = LOWER(LTRIM(RTRIM(u.DisplayName))),
                    u.UserName = LOWER(LTRIM(RTRIM(u.DisplayName))),
                    u.NormalizedUserName = UPPER(LOWER(LTRIM(RTRIM(u.DisplayName))))
                FROM AspNetUsers u
                WHERE u.UserName LIKE '%@%'
                  AND u.DisplayName IS NOT NULL
                  AND LTRIM(RTRIM(u.DisplayName)) <> '';
                """);

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DisplayName",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DisplayName",
                table: "AspNetUsers",
                column: "DisplayName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DisplayName",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DisplayName",
                table: "AspNetUsers",
                column: "DisplayName");
        }
    }
}

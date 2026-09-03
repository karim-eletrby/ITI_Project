using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetEmail",
                table: "EmailOtps",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailOtps_UserId_Purpose_TargetEmail_IsUsed",
                table: "EmailOtps",
                columns: new[] { "UserId", "Purpose", "TargetEmail", "IsUsed" });

            // Legacy EmailOtpPurpose: Registration=0, ChangeEmail=1 → OtpPurpose: Registration=1, EmailChange=3
            migrationBuilder.Sql("""
                UPDATE EmailOtps SET Purpose = 99 WHERE Purpose = 1;
                UPDATE EmailOtps SET Purpose = 1 WHERE Purpose = 0;
                UPDATE EmailOtps SET Purpose = 3 WHERE Purpose = 99;
                UPDATE EmailOtps SET TargetEmail = Email WHERE Purpose = 3 AND TargetEmail IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE EmailOtps SET Purpose = 99 WHERE Purpose = 3;
                UPDATE EmailOtps SET Purpose = 0 WHERE Purpose = 1;
                UPDATE EmailOtps SET Purpose = 1 WHERE Purpose = 99;
                """);

            migrationBuilder.DropIndex(
                name: "IX_EmailOtps_UserId_Purpose_TargetEmail_IsUsed",
                table: "EmailOtps");

            migrationBuilder.DropColumn(
                name: "TargetEmail",
                table: "EmailOtps");
        }
    }
}

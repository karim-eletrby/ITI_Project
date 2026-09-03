using Infrastructure.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260903101500_AddCommentReplies")]
    public partial class AddCommentReplies : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Comments', 'ParentCommentId') IS NULL
                BEGIN
                    ALTER TABLE [Comments] ADD [ParentCommentId] int NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Comments_ParentCommentId' AND object_id = OBJECT_ID('Comments'))
                BEGIN
                    CREATE INDEX [IX_Comments_ParentCommentId] ON [Comments] ([ParentCommentId]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Comments_Comments_ParentCommentId')
                BEGIN
                    ALTER TABLE [Comments] ADD CONSTRAINT [FK_Comments_Comments_ParentCommentId]
                        FOREIGN KEY ([ParentCommentId]) REFERENCES [Comments] ([Id]) ON DELETE NO ACTION;
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

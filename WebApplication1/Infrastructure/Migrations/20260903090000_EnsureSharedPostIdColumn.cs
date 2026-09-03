using Infrastructure.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260903090000_EnsureSharedPostIdColumn")]
    public partial class EnsureSharedPostIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Posts', 'SharedPostId') IS NULL
                BEGIN
                    ALTER TABLE [Posts] ADD [SharedPostId] int NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Posts_SharedPostId' AND object_id = OBJECT_ID('Posts'))
                BEGIN
                    CREATE INDEX [IX_Posts_SharedPostId] ON [Posts] ([SharedPostId]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Posts_Posts_SharedPostId')
                BEGIN
                    ALTER TABLE [Posts] ADD CONSTRAINT [FK_Posts_Posts_SharedPostId]
                        FOREIGN KEY ([SharedPostId]) REFERENCES [Posts] ([Id]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

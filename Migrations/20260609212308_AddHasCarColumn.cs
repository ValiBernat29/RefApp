using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RefApp.Migrations
{
    /// <inheritdoc />
    public partial class AddHasCarColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add HasCar column using existence check — safe to run on SQL Server
            // even if the column was already added manually or by a previous deployment.
            // PreferredRole is intentionally omitted: it is already handled by the
            // AddLocationAndRankColumns migration that runs before this one.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'HasCar'
                )
                ALTER TABLE [AspNetUsers] ADD [HasCar] bit NOT NULL DEFAULT 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'HasCar'
                )
                ALTER TABLE [AspNetUsers] DROP COLUMN [HasCar];
            ");
        }
    }
}

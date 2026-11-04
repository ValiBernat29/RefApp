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
            // On SQLite (development) the SQL Server batch below cannot run, and no earlier migration
            // adds these two columns, so add them with provider-neutral operations.
            // SQL Server keeps the original idempotent SQL: PreferredRole there is added by DbInitializer.
            if (migrationBuilder.ActiveProvider != "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.AddColumn<bool>(
                    name: "HasCar",
                    table: "AspNetUsers",
                    type: "INTEGER",
                    nullable: false,
                    defaultValue: false);

                migrationBuilder.AddColumn<int>(
                    name: "PreferredRole",
                    table: "AspNetUsers",
                    type: "INTEGER",
                    nullable: false,
                    defaultValue: 0);

                return;
            }

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

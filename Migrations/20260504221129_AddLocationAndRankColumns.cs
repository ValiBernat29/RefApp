using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RefApp.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationAndRankColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── AspNetUsers ───────────────────────────────────────────────────

            // Add Rank column if it doesn't exist yet (as nullable first)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'Rank'
                )
                ALTER TABLE [AspNetUsers] ADD [Rank] int NULL;
            ");

            // ALWAYS fix any NULL values → 0 (RefereeRank.None), then enforce NOT NULL
            migrationBuilder.Sql("UPDATE [AspNetUsers] SET [Rank] = 0 WHERE [Rank] IS NULL;");
            migrationBuilder.Sql("ALTER TABLE [AspNetUsers] ALTER COLUMN [Rank] int NOT NULL;");

            // HomeCity
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'HomeCity'
                )
                ALTER TABLE [AspNetUsers] ADD [HomeCity] nvarchar(100) NULL;
            ");

            // Latitude
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'Latitude'
                )
                ALTER TABLE [AspNetUsers] ADD [Latitude] float NULL;
            ");

            // Longitude
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'Longitude'
                )
                ALTER TABLE [AspNetUsers] ADD [Longitude] float NULL;
            ");

            // ── Teams ─────────────────────────────────────────────────────────

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('Teams') AND name = 'City'
                )
                ALTER TABLE [Teams] ADD [City] nvarchar(100) NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('Teams') AND name = 'Latitude'
                )
                ALTER TABLE [Teams] ADD [Latitude] float NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('Teams') AND name = 'Longitude'
                )
                ALTER TABLE [Teams] ADD [Longitude] float NULL;
            ");

            // ── TeamRefereeRefusals ───────────────────────────────────────────

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TeamRefereeRefusals')
                BEGIN
                    CREATE TABLE [TeamRefereeRefusals] (
                        [Id]          int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [TeamId]      int NOT NULL,
                        [RefereeId]   nvarchar(450) NOT NULL,
                        [Reason]      nvarchar(max) NULL,
                        [DateRefused] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                        CONSTRAINT [FK_TeamRefereeRefusals_Teams]
                            FOREIGN KEY ([TeamId]) REFERENCES [Teams]([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_TeamRefereeRefusals_Users]
                            FOREIGN KEY ([RefereeId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_TeamRefereeRefusals_TeamId]     ON [TeamRefereeRefusals]([TeamId]);
                    CREATE INDEX [IX_TeamRefereeRefusals_RefereeId]  ON [TeamRefereeRefusals]([RefereeId]);
                END
            ");

            // ── Matches: add HomeTeamId / AwayTeamId if old schema present ────

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('Matches') AND name = 'HomeTeamId'
                )
                BEGIN
                    ALTER TABLE [Matches] ADD [HomeTeamId] int NULL;
                    ALTER TABLE [Matches] ADD [AwayTeamId] int NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TeamRefereeRefusals') DROP TABLE [TeamRefereeRefusals];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'Rank') ALTER TABLE [AspNetUsers] DROP COLUMN [Rank];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'HomeCity') ALTER TABLE [AspNetUsers] DROP COLUMN [HomeCity];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'Latitude') ALTER TABLE [AspNetUsers] DROP COLUMN [Latitude];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'Longitude') ALTER TABLE [AspNetUsers] DROP COLUMN [Longitude];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Teams') AND name = 'City') ALTER TABLE [Teams] DROP COLUMN [City];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Teams') AND name = 'Latitude') ALTER TABLE [Teams] DROP COLUMN [Latitude];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Teams') AND name = 'Longitude') ALTER TABLE [Teams] DROP COLUMN [Longitude];");
        }
    }
}

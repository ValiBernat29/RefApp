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
            // ── AspNetUsers: new location + rank columns ─────────────────────
            // Rank: non-nullable int, default 0 (= RefereeRank.None) so existing rows don't get NULL
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'Rank')
                    ALTER TABLE [AspNetUsers] ADD [Rank] int NOT NULL DEFAULT 0;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'HomeCity')
                    ALTER TABLE [AspNetUsers] ADD [HomeCity] nvarchar(100) NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'Latitude')
                    ALTER TABLE [AspNetUsers] ADD [Latitude] float NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'Longitude')
                    ALTER TABLE [AspNetUsers] ADD [Longitude] float NULL;
            ");

            // ── Teams: new location columns ───────────────────────────────────
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Teams') AND name = 'City')
                    ALTER TABLE [Teams] ADD [City] nvarchar(100) NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Teams') AND name = 'Latitude')
                    ALTER TABLE [Teams] ADD [Latitude] float NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Teams') AND name = 'Longitude')
                    ALTER TABLE [Teams] ADD [Longitude] float NULL;
            ");

            // ── Matches: HomeTeamId / AwayTeamId (if old schema still present) ─
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Matches') AND name = 'HomeTeamId')
                BEGIN
                    ALTER TABLE [Matches] ADD [HomeTeamId] int NOT NULL DEFAULT 0;
                    ALTER TABLE [Matches] ADD [AwayTeamId] int NOT NULL DEFAULT 0;
                END
            ");

            // ── TeamRefereeRefusals table ─────────────────────────────────────
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TeamRefereeRefusals')
                BEGIN
                    CREATE TABLE [TeamRefereeRefusals] (
                        [Id]          int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [TeamId]      int NOT NULL,
                        [RefereeId]   nvarchar(450) NOT NULL,
                        [Reason]      nvarchar(max) NULL,
                        [DateRefused] datetime2 NOT NULL,
                        CONSTRAINT [FK_TeamRefereeRefusals_Teams_TeamId]
                            FOREIGN KEY ([TeamId]) REFERENCES [Teams]([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_TeamRefereeRefusals_AspNetUsers_RefereeId]
                            FOREIGN KEY ([RefereeId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_TeamRefereeRefusals_TeamId] ON [TeamRefereeRefusals]([TeamId]);
                    CREATE INDEX [IX_TeamRefereeRefusals_RefereeId] ON [TeamRefereeRefusals]([RefereeId]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [TeamRefereeRefusals];");
            migrationBuilder.Sql("ALTER TABLE [AspNetUsers] DROP COLUMN IF EXISTS [Rank];");
            migrationBuilder.Sql("ALTER TABLE [AspNetUsers] DROP COLUMN IF EXISTS [HomeCity];");
            migrationBuilder.Sql("ALTER TABLE [AspNetUsers] DROP COLUMN IF EXISTS [Latitude];");
            migrationBuilder.Sql("ALTER TABLE [AspNetUsers] DROP COLUMN IF EXISTS [Longitude];");
            migrationBuilder.Sql("ALTER TABLE [Teams] DROP COLUMN IF EXISTS [City];");
            migrationBuilder.Sql("ALTER TABLE [Teams] DROP COLUMN IF EXISTS [Latitude];");
            migrationBuilder.Sql("ALTER TABLE [Teams] DROP COLUMN IF EXISTS [Longitude];");
        }
    }
}

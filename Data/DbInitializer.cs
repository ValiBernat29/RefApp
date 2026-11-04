using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RefApp.Models;

namespace RefApp.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (context.Database.IsSqlServer())
        {
            // Ensure the migrations history table exists first
            await context.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory')
                    CREATE TABLE [__EFMigrationsHistory] (
                        [MigrationId]    nvarchar(150) NOT NULL,
                        [ProductVersion] nvarchar(32)  NOT NULL,
                        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                    );
            ");

            // If the AspNetUsers table already exists but our consolidated InitialCreate
            // migration ID is not yet recorded, insert it so EF doesn't try to re-create tables.
            await context.Database.ExecuteSqlRawAsync(@"
                IF OBJECT_ID('dbo.AspNetUsers', 'U') IS NOT NULL
                AND NOT EXISTS (
                    SELECT 1 FROM [__EFMigrationsHistory]
                    WHERE [MigrationId] = '20260504200938_InitialCreate'
                )
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES ('20260504200938_InitialCreate', '8.0.0');
            ");
        }

        // Apply any pending migrations (adds new columns, creates new tables, etc.)
        await context.Database.MigrateAsync();

        if (context.Database.IsSqlServer())
        {
            // Unconditional safety net: ensure Rank column exists and has no NULLs.
            // Also make old HomeTeam/AwayTeam string columns nullable so EF inserts
            // (which omit those columns) don't fail the NOT NULL constraint.
            await context.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'Rank')
                    ALTER TABLE [AspNetUsers] ADD [Rank] int NULL;
                UPDATE [AspNetUsers] SET [Rank] = 0 WHERE [Rank] IS NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'PreferredRole')
                    ALTER TABLE [AspNetUsers] ADD [PreferredRole] int NOT NULL DEFAULT 0;

                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Matches') AND name = 'HomeTeam')
                BEGIN
                    ALTER TABLE [Matches] ALTER COLUMN [HomeTeam] nvarchar(200) NULL;
                    ALTER TABLE [Matches] ALTER COLUMN [AwayTeam] nvarchar(200) NULL;
                END
            ");
        }

        string[] roleNames = { "Board", "Referee" };

        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // Seed accounts. Credentials come from configuration (Seed:BoardEmail / Seed:BoardPassword,
        // Seed:RefereeEmail / Seed:RefereePassword). Demo defaults apply only in Development;
        // elsewhere an account is created only when a password is explicitly configured.
        var config = services.GetRequiredService<IConfiguration>();
        var isDevelopment = services.GetRequiredService<IHostEnvironment>().IsDevelopment();

        await SeedUserAsync(userManager, config, isDevelopment, "Board", "Board User",
            defaultEmail: "board@example.com", defaultPassword: "Board123!");
        await SeedUserAsync(userManager, config, isDevelopment, "Referee", "Referee User",
            defaultEmail: "referee@example.com", defaultPassword: "Referee123!");

        // Demo fixtures are seeded once per league, only while that league has no teams.
        // Re-running them on every boot would re-create matches the Board deleted or moved
        // and reset dates the Board edited. Team coordinates are filled in later by
        // TeamGeocodingBackgroundService, so startup never waits on Nominatim.
        var seeded = false;
        seeded |= await SeedLeagueIfEmptyAsync(context, League.L4, SeedL4FixturesAsync);
        seeded |= await SeedLeagueIfEmptyAsync(context, League.L5A, SeedL5AFixturesAsync);
        seeded |= await SeedLeagueIfEmptyAsync(context, League.L5B, SeedL5BFixturesAsync);
        seeded |= await SeedLeagueIfEmptyAsync(context, League.L5C, SeedL5CFixturesAsync);

        if (seeded)
        {
            // Freshly seeded matches take their location from the home team's city.
            // From here on, MatchesController and TeamsController keep Location in sync.
            var allMatches = await context.Matches.Include(m => m.HomeTeam).ToListAsync();
            foreach (var m in allMatches)
            {
                var correctLoc = m.HomeTeam?.City ?? m.HomeTeam?.Name ?? m.Location;
                if (m.Location != correctLoc)
                    m.Location = correctLoc;
            }
            await context.SaveChangesAsync();
        }
    }

    /// <summary>Runs <paramref name="seed"/> only if the league has no teams. Returns true if it ran.</summary>
    private static async Task<bool> SeedLeagueIfEmptyAsync(
        ApplicationDbContext context, League league, Func<ApplicationDbContext, Task> seed)
    {
        if (await context.Teams.AnyAsync(t => t.League == league))
            return false;

        await seed(context);
        return true;
    }

    private static async Task SeedUserAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration config,
        bool isDevelopment,
        string role,
        string displayName,
        string defaultEmail,
        string defaultPassword)
    {
        var email = config[$"Seed:{role}Email"] ?? (isDevelopment ? defaultEmail : null);
        var password = config[$"Seed:{role}Password"] ?? (isDevelopment ? defaultPassword : null);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        if (await userManager.FindByEmailAsync(email) != null)
            return;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(user, role);
    }

    private static string GetLocation(string homeTeam)
    {
        return homeTeam switch
        {
            "Viitorul Satu Nou" => "Satu Nou",
            "Crișul Alb Buteni" => "Buteni",
            "Viitorul Olimpia Bârzava" => "Bârzava",
            "Foresta Oil Sânpetru German" => "Sânpetru German",
            "Team United Sânpaul" => "Sânpaul",
            _ => homeTeam.Split(' ').Length > 1 ? homeTeam.Split(' ')[1] : homeTeam
        };
    }

    private static async Task SeedL4FixturesAsync(ApplicationDbContext context)
    {
        // ── Teams ─────────────────────────────────────────────────────────────
        var teamNames = new[]
        {
            "CS Socodor", "Athletico Vinga", "Național Sebiș", "CS Vladimirescu",
            "Victoria Felnac", "Șoimii Șimand", "Podgoria Pâncota", "Victoria Nădlac",
            "Olimpia Bocsig", "Frontiera Curtici", "Șiriana Șiria", "Progresul Pecica II",
            "Voința Macea"
        };

        var existingTeamNames = context.Teams
            .Where(t => t.League == League.L4)
            .Select(t => t.Name)
            .ToHashSet();

        foreach (var name in teamNames)
        {
            if (!existingTeamNames.Contains(name))
            {
                context.Teams.Add(new Team
                {
                    Name = name,
                    League = League.L4,
                    PreferredMatchDay = DayOfWeek.Saturday,
                    City = GetLocation(name)
                });
            }
        }
        await context.SaveChangesAsync();

        // ── Fixtures from program_meciuri_fotbal.csv ───────────────────────────
        // Format: (HomeTeam, AwayTeam, MatchDate ISO)
        var fixtures = new (string Home, string Away, DateTime Date)[]
        {
            // Etapa 21 – 02.05.2026
            ("CS Socodor",          "Athletico Vinga",        new DateTime(2026, 5,  2, 17, 0, 0)),
            ("Național Sebiș",      "CS Vladimirescu",        new DateTime(2026, 5,  2, 17, 0, 0)),
            ("Victoria Felnac",     "Șoimii Șimand",          new DateTime(2026, 5,  2, 17, 0, 0)),
            ("Podgoria Pâncota",    "Victoria Nădlac",        new DateTime(2026, 5,  2, 17, 0, 0)),
            ("Olimpia Bocsig",      "Frontiera Curtici",      new DateTime(2026, 5,  2, 17, 0, 0)),
            ("Șiriana Șiria",       "Progresul Pecica II",    new DateTime(2026, 5,  2, 17, 0, 0)),

            // Etapa 22 – 09.05.2026
            ("Progresul Pecica II", "Victoria Felnac",        new DateTime(2026, 5,  9, 17, 0, 0)),
            ("Victoria Nădlac",     "Național Sebiș",         new DateTime(2026, 5,  9, 17, 0, 0)),
            ("Frontiera Curtici",   "Șiriana Șiria",          new DateTime(2026, 5,  9, 17, 0, 0)),
            ("CS Vladimirescu",     "Voința Macea",           new DateTime(2026, 5,  9, 17, 0, 0)),
            ("Șoimii Șimand",       "Podgoria Pâncota",       new DateTime(2026, 5,  9, 17, 0, 0)),
            ("Athletico Vinga",     "Olimpia Bocsig",         new DateTime(2026, 5,  9, 17, 0, 0)),

            // Etapa 23 – 16.05.2026
            ("Victoria Felnac",     "Frontiera Curtici",      new DateTime(2026, 5, 16, 17, 0, 0)),
            ("Olimpia Bocsig",      "CS Socodor",             new DateTime(2026, 5, 16, 17, 0, 0)),
            ("Șiriana Șiria",       "Athletico Vinga",        new DateTime(2026, 5, 16, 17, 0, 0)),
            ("Național Sebiș",      "Șoimii Șimand",          new DateTime(2026, 5, 16, 17, 0, 0)),
            ("Podgoria Pâncota",    "Progresul Pecica II",    new DateTime(2026, 5, 16, 17, 0, 0)),
            ("Voința Macea",        "Victoria Nădlac",        new DateTime(2026, 5, 16, 17, 0, 0)),

            // Etapa 24 – 23.05.2026
            ("CS Socodor",          "Șiriana Șiria",          new DateTime(2026, 5, 23, 17, 0, 0)),
            ("Athletico Vinga",     "Victoria Felnac",        new DateTime(2026, 5, 23, 17, 0, 0)),
            ("Frontiera Curtici",   "Podgoria Pâncota",       new DateTime(2026, 5, 23, 17, 0, 0)),
            ("Victoria Nădlac",     "CS Vladimirescu",        new DateTime(2026, 5, 23, 17, 0, 0)),
            ("Progresul Pecica II", "Național Sebiș",         new DateTime(2026, 5, 23, 17, 0, 0)),
            ("Șoimii Șimand",       "Voința Macea",           new DateTime(2026, 5, 23, 17, 0, 0)),

            // Etapa 25 – 30.05.2026
            ("Victoria Felnac",     "CS Socodor",             new DateTime(2026, 5, 30, 17, 0, 0)),
            ("Șiriana Șiria",       "Olimpia Bocsig",         new DateTime(2026, 5, 30, 17, 0, 0)),
            ("Podgoria Pâncota",    "Athletico Vinga",        new DateTime(2026, 5, 30, 17, 0, 0)),
            ("Național Sebiș",      "Frontiera Curtici",      new DateTime(2026, 5, 30, 17, 0, 0)),
            ("CS Vladimirescu",     "Șoimii Șimand",          new DateTime(2026, 5, 30, 17, 0, 0)),
            ("Voința Macea",        "Progresul Pecica II",    new DateTime(2026, 5, 30, 17, 0, 0)),

            // Etapa 26 – 06.06.2026
            ("Olimpia Bocsig",      "Victoria Felnac",        new DateTime(2026, 6,  6, 17, 0, 0)),
            ("CS Socodor",          "Podgoria Pâncota",       new DateTime(2026, 6,  6, 17, 0, 0)),
            ("Athletico Vinga",     "Național Sebiș",         new DateTime(2026, 6,  6, 17, 0, 0)),
            ("Șoimii Șimand",       "Victoria Nădlac",        new DateTime(2026, 6,  6, 17, 0, 0)),
            ("Frontiera Curtici",   "Voința Macea",           new DateTime(2026, 6,  6, 17, 0, 0)),
            ("Progresul Pecica II", "CS Vladimirescu",        new DateTime(2026, 6,  6, 17, 0, 0)),
        };

        // ── Fix any existing fixtures that still have "TBD" as location ──────
        var teamDict = await context.Teams.ToDictionaryAsync(t => t.Name, t => t.Id);
        var tbd = await context.Matches.Include(m => m.HomeTeam).Where(m => m.Location == "TBD").ToListAsync();
        foreach (var m in tbd)
        {
            // Use the manual City you entered, fallback to hardcoded guess only if empty
            m.Location = !string.IsNullOrEmpty(m.HomeTeam?.City) 
                ? m.HomeTeam.City 
                : GetLocation(m.HomeTeam?.Name ?? "");
        }
        await context.SaveChangesAsync();

        // ── Insert new fixtures (idempotent) ──────────────────────────────────
        foreach (var (home, away, date) in fixtures)
        {
            bool alreadyExists = context.Matches.Any(m =>
                m.HomeTeamId == teamDict[home] &&
                m.AwayTeamId == teamDict[away] &&
                m.MatchDate == date);

            if (!alreadyExists)
            {
                context.Matches.Add(new Match
                {
                    HomeTeamId = teamDict[home],
                    AwayTeamId = teamDict[away],
                    MatchDate = date,
                    Location = GetLocation(home)
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedL5AFixturesAsync(ApplicationDbContext context)
    {
        var teamNames = new[]
        {
            "Viitorul Pereg", "Olimpia Bujac", "Foresta Oil Sânpetru German",
            "ACS Sâmbăteni", "Voința Mailat", "Unirea Șeitin", "Mureșul Zădăreni",
            "Șoimii Livada", "Gloria Secusigiu", "Team United Sânpaul", "Banatul Arad"
        };

        var existingTeamNames = context.Teams
            .Where(t => t.League == League.L5A)
            .Select(t => t.Name)
            .ToHashSet();

        foreach (var name in teamNames)
        {
            if (!existingTeamNames.Contains(name))
            {
                context.Teams.Add(new Team
                {
                    Name = name,
                    League = League.L5A,
                    PreferredMatchDay = DayOfWeek.Sunday,
                    City = GetLocation(name)
                });
            }
        }
        await context.SaveChangesAsync();

        var fixtures = new (string Home, string Away, DateTime Date)[]
        {
            ("Viitorul Pereg", "Olimpia Bujac", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Foresta Oil Sânpetru German", "ACS Sâmbăteni", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Voința Mailat", "Unirea Șeitin", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Mureșul Zădăreni", "Șoimii Livada", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Gloria Secusigiu", "Team United Sânpaul", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Olimpia Bujac", "Banatul Arad", new DateTime(2026, 05, 09, 18, 00, 0)),
            ("ACS Sâmbăteni", "Viitorul Pereg", new DateTime(2026, 05, 09, 18, 00, 0)),
            ("Unirea Șeitin", "Gloria Secusigiu", new DateTime(2026, 05, 09, 18, 00, 0)),
            ("Șoimii Livada", "Foresta Oil Sânpetru German", new DateTime(2026, 05, 09, 18, 00, 0)),
            ("Team United Sânpaul", "Mureșul Zădăreni", new DateTime(2026, 05, 09, 18, 00, 0)),
            ("Voința Mailat", "Olimpia Bujac", new DateTime(2026, 05, 16, 18, 00, 0)),
            ("Mureșul Zădăreni", "Unirea Șeitin", new DateTime(2026, 05, 16, 18, 00, 0)),
            ("Viitorul Pereg", "Foresta Oil Sânpetru German", new DateTime(2026, 05, 16, 18, 00, 0)),
            ("Banatul Arad", "ACS Sâmbăteni", new DateTime(2026, 05, 16, 18, 00, 0)),
            ("Team United Sânpaul", "Șoimii Livada", new DateTime(2026, 05, 16, 18, 00, 0)),
            ("Olimpia Bujac", "Gloria Secusigiu", new DateTime(2026, 05, 23, 18, 00, 0)),
            ("Unirea Șeitin", "Team United Sânpaul", new DateTime(2026, 05, 23, 18, 00, 0)),
            ("Foresta Oil Sânpetru German", "Banatul Arad", new DateTime(2026, 05, 23, 18, 00, 0)),
            ("ACS Sâmbăteni", "Voința Mailat", new DateTime(2026, 05, 23, 18, 00, 0)),
            ("Șoimii Livada", "Viitorul Pereg", new DateTime(2026, 05, 23, 18, 00, 0)),
            ("Mureșul Zădăreni", "Olimpia Bujac", new DateTime(2026, 05, 30, 18, 00, 0)),
            ("Banatul Arad", "Viitorul Pereg", new DateTime(2026, 05, 30, 18, 00, 0)),
            ("Unirea Șeitin", "Șoimii Livada", new DateTime(2026, 05, 30, 18, 00, 0)),
            ("Voința Mailat", "Foresta Oil Sânpetru German", new DateTime(2026, 05, 30, 18, 00, 0)),
            ("Gloria Secusigiu", "ACS Sâmbăteni", new DateTime(2026, 05, 30, 18, 00, 0)),
            ("Olimpia Bujac", "Team United Sânpaul", new DateTime(2026, 06, 06, 18, 00, 0)),
            ("ACS Sâmbăteni", "Mureșul Zădăreni", new DateTime(2026, 06, 06, 18, 00, 0)),
            ("Viitorul Pereg", "Voința Mailat", new DateTime(2026, 06, 06, 18, 00, 0)),
            ("Foresta Oil Sânpetru German", "Gloria Secusigiu", new DateTime(2026, 06, 06, 18, 00, 0)),
            ("Șoimii Livada", "Banatul Arad", new DateTime(2026, 06, 06, 18, 00, 0)),
        };

        var teamDict = await context.Teams.ToDictionaryAsync(t => t.Name, t => t.Id);
        var saturdayTeams = new[] { "Olimpia Bujac", "ACS Sâmbăteni", "Voința Mailat", "Mureșul Zădăreni" };

        foreach (var (home, away, originalDate) in fixtures)
        {
            var date = originalDate;
            if (!saturdayTeams.Contains(home))
            {
                date = date.AddDays(1);
            }

            var existingMatch = await context.Matches.FirstOrDefaultAsync(m =>
                m.HomeTeamId == teamDict[home] && m.AwayTeamId == teamDict[away]);

            if (existingMatch == null)
            {
                context.Matches.Add(new Match
                {
                    HomeTeamId = teamDict[home],
                    AwayTeamId = teamDict[away],
                    MatchDate = date,
                    Location = GetLocation(home)
                });
            }
            else
            {
                if (existingMatch.MatchDate != date)
                {
                    existingMatch.MatchDate = date;
                }
            }
        }
        await context.SaveChangesAsync();
    }

    private static async Task SeedL5BFixturesAsync(ApplicationDbContext context)
    {
        var teamNames = new[]
        {
            "Cetate Săvârșin", "Avântul Târnova", "Viitorul Olimpia Bârzava",
            "CS Șofronea", "Real Horia", "FC Sântana", "Spicul Olari",
            "Podgoria Ghioroc", "Frontiera Pilu", "Unirea Sântana II"
        };

        var existingTeamNames = context.Teams
            .Where(t => t.League == League.L5B)
            .Select(t => t.Name)
            .ToHashSet();

        foreach (var name in teamNames)
        {
            if (!existingTeamNames.Contains(name))
            {
                context.Teams.Add(new Team
                {
                    Name = name,
                    League = League.L5B,
                    PreferredMatchDay = DayOfWeek.Sunday,
                    City = GetLocation(name)
                });
            }
        }
        await context.SaveChangesAsync();

        var fixtures = new (string Home, string Away, DateTime Date)[]
        {
            ("Cetate Săvârșin", "Avântul Târnova", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Viitorul Olimpia Bârzava", "CS Șofronea", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Real Horia", "FC Sântana", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Spicul Olari", "Podgoria Ghioroc", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Frontiera Pilu", "Unirea Sântana II", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Avântul Târnova", "Real Horia", new DateTime(2026, 05, 09, 17, 00, 0)),
            ("Podgoria Ghioroc", "Cetate Săvârșin", new DateTime(2026, 05, 09, 17, 00, 0)),
            ("CS Șofronea", "Spicul Olari", new DateTime(2026, 05, 09, 17, 00, 0)),
            ("Unirea Sântana II", "Viitorul Olimpia Bârzava", new DateTime(2026, 05, 09, 17, 00, 0)),
            ("FC Sântana", "Frontiera Pilu", new DateTime(2026, 05, 09, 17, 00, 0)),
            ("Cetate Săvârșin", "CS Șofronea", new DateTime(2026, 05, 16, 17, 00, 0)),
            ("Spicul Olari", "Viitorul Olimpia Bârzava", new DateTime(2026, 05, 16, 17, 00, 0)),
            ("Frontiera Pilu", "Avântul Târnova", new DateTime(2026, 05, 16, 17, 00, 0)),
            ("Real Horia", "Podgoria Ghioroc", new DateTime(2026, 05, 16, 17, 00, 0)),
            ("FC Sântana", "Unirea Sântana II", new DateTime(2026, 05, 16, 17, 00, 0)),
            ("Viitorul Olimpia Bârzava", "Cetate Săvârșin", new DateTime(2026, 05, 23, 17, 00, 0)),
            ("Avântul Târnova", "FC Sântana", new DateTime(2026, 05, 23, 17, 00, 0)),
            ("CS Șofronea", "Real Horia", new DateTime(2026, 05, 23, 17, 00, 0)),
            ("Unirea Sântana II", "Spicul Olari", new DateTime(2026, 05, 23, 17, 00, 0)),
            ("Podgoria Ghioroc", "Frontiera Pilu", new DateTime(2026, 05, 23, 17, 00, 0)),
        };

        var teamDict = await context.Teams.ToDictionaryAsync(t => t.Name, t => t.Id);
        var saturdayTeams = new[] { "FC Sântana", "Podgoria Ghioroc", "Real Horia" };

        foreach (var (home, away, originalDate) in fixtures)
        {
            var date = originalDate;
            if (!saturdayTeams.Contains(home))
            {
                date = date.AddDays(1);
            }

            var existingMatch = await context.Matches.FirstOrDefaultAsync(m =>
                m.HomeTeamId == teamDict[home] && m.AwayTeamId == teamDict[away]);

            if (existingMatch == null)
            {
                context.Matches.Add(new Match
                {
                    HomeTeamId = teamDict[home],
                    AwayTeamId = teamDict[away],
                    MatchDate = date,
                    Location = GetLocation(home)
                });
            }
            else
            {
                if (existingMatch.MatchDate != date)
                {
                    existingMatch.MatchDate = date;
                }
            }
        }
        await context.SaveChangesAsync();
    }

    private static async Task SeedL5CFixturesAsync(ApplicationDbContext context)
    {
        var teamNames = new[]
        {
            "Voința Sintea Mare", "Victoria Ineu", "Șoimii Archiș", "Viitorul Șepreuș",
            "Crișul Alb Buteni", "Progresul Hălmagiu", "Viitorul Satu Nou", "Cetate Dezna",
            "Recolta Apateu", "ACS Vârfurile", "Unirea Gurahonț", "Flacăra Țipar"
        };

        var existingTeamNames = context.Teams
            .Where(t => t.League == League.L5C)
            .Select(t => t.Name)
            .ToHashSet();

        foreach (var name in teamNames)
        {
            if (!existingTeamNames.Contains(name))
            {
                context.Teams.Add(new Team
                {
                    Name = name,
                    League = League.L5C,
                    PreferredMatchDay = DayOfWeek.Sunday,
                    City = GetLocation(name)
                });
            }
        }
        await context.SaveChangesAsync();

        var fixtures = new (string Home, string Away, DateTime Date)[]
        {
            ("Voința Sintea Mare", "Victoria Ineu", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Șoimii Archiș", "Viitorul Șepreuș", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Crișul Alb Buteni", "Progresul Hălmagiu", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Viitorul Satu Nou", "Cetate Dezna", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Recolta Apateu", "ACS Vârfurile", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Unirea Gurahonț", "Flacăra Țipar", new DateTime(2026, 05, 02, 17, 00, 0)),
            ("Victoria Ineu", "Crișul Alb Buteni", new DateTime(2026, 05, 09, 18, 00, 0)),
            ("Viitorul Șepreuș", "Recolta Apateu", new DateTime(2026, 05, 09, 18, 00, 0)),
            ("Progresul Hălmagiu", "Șoimii Archiș", new DateTime(2026, 05, 09, 18, 00, 0)),
            ("Flacăra Țipar", "Viitorul Satu Nou", new DateTime(2026, 05, 09, 18, 00, 0)),
            ("ACS Vârfurile", "Unirea Gurahonț", new DateTime(2026, 05, 09, 18, 00, 0)),
            ("Cetate Dezna", "Voința Sintea Mare", new DateTime(2026, 05, 09, 18, 00, 0)),
            ("Șoimii Archiș", "Victoria Ineu", new DateTime(2026, 05, 16, 18, 00, 0)),
            ("Unirea Gurahonț", "Viitorul Șepreuș", new DateTime(2026, 05, 16, 18, 00, 0)),
            ("Recolta Apateu", "Progresul Hălmagiu", new DateTime(2026, 05, 16, 18, 00, 0)),
            ("Flacăra Țipar", "Cetate Dezna", new DateTime(2026, 05, 16, 18, 00, 0)),
            ("Viitorul Satu Nou", "ACS Vârfurile", new DateTime(2026, 05, 16, 18, 00, 0)),
            ("Crișul Alb Buteni", "Voința Sintea Mare", new DateTime(2026, 05, 16, 18, 00, 0)),
            ("Victoria Ineu", "Recolta Apateu", new DateTime(2026, 05, 23, 18, 00, 0)),
            ("Viitorul Șepreuș", "Viitorul Satu Nou", new DateTime(2026, 05, 23, 18, 00, 0)),
            ("Progresul Hălmagiu", "Unirea Gurahonț", new DateTime(2026, 05, 23, 18, 00, 0)),
            ("Cetate Dezna", "Crișul Alb Buteni", new DateTime(2026, 05, 23, 18, 00, 0)),
            ("ACS Vârfurile", "Flacăra Țipar", new DateTime(2026, 05, 23, 18, 00, 0)),
            ("Voința Sintea Mare", "Șoimii Archiș", new DateTime(2026, 05, 23, 18, 00, 0)),
            ("Unirea Gurahonț", "Victoria Ineu", new DateTime(2026, 05, 30, 18, 00, 0)),
            ("Flacăra Țipar", "Viitorul Șepreuș", new DateTime(2026, 05, 30, 18, 00, 0)),
            ("Viitorul Satu Nou", "Progresul Hălmagiu", new DateTime(2026, 05, 30, 18, 00, 0)),
            ("Recolta Apateu", "Voința Sintea Mare", new DateTime(2026, 05, 30, 18, 00, 0)),
            ("Șoimii Archiș", "Crișul Alb Buteni", new DateTime(2026, 05, 30, 18, 00, 0)),
            ("ACS Vârfurile", "Cetate Dezna", new DateTime(2026, 05, 30, 18, 00, 0)),
            ("Victoria Ineu", "Viitorul Satu Nou", new DateTime(2026, 06, 06, 18, 00, 0)),
            ("Crișul Alb Buteni", "Recolta Apateu", new DateTime(2026, 06, 06, 18, 00, 0)),
            ("Viitorul Șepreuș", "ACS Vârfurile", new DateTime(2026, 06, 06, 18, 00, 0)),
            ("Cetate Dezna", "Șoimii Archiș", new DateTime(2026, 06, 06, 18, 00, 0)),
            ("Progresul Hălmagiu", "Flacăra Țipar", new DateTime(2026, 06, 06, 18, 00, 0)),
            ("Voința Sintea Mare", "Unirea Gurahonț", new DateTime(2026, 06, 06, 18, 00, 0)),
        };

        var teamDict = await context.Teams.ToDictionaryAsync(t => t.Name, t => t.Id);
        foreach (var (home, away, originalDate) in fixtures)
        {
            var date = originalDate;
            if (home != "Victoria Ineu")
            {
                date = date.AddDays(1);
            }

            var existingMatch = await context.Matches.FirstOrDefaultAsync(m =>
                m.HomeTeamId == teamDict[home] && m.AwayTeamId == teamDict[away]);

            if (existingMatch == null)
            {
                context.Matches.Add(new Match
                {
                    HomeTeamId = teamDict[home],
                    AwayTeamId = teamDict[away],
                    MatchDate = date,
                    Location = GetLocation(home)
                });
            }
            else
            {
                if (existingMatch.MatchDate != date)
                {
                    existingMatch.MatchDate = date;
                }
            }
        }
        await context.SaveChangesAsync();
    }
}


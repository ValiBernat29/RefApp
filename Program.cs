using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;
using RefApp.Services;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db";
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("ProductionConnection") 
        ?? "Server=fake;Database=fake;";
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(
            connectionString,
            sqlServerOptionsAction: sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            }));
}

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false; // Set to true only if you plan to confirm accounts
        options.User.RequireUniqueEmail = false;
        //options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ── Location & Scoring services ────────────────────────────────────────────
builder.Services.AddHttpClient("Nominatim", client =>
{
    client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("RefApp/1.0 (referee-appointment-system)");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddScoped<GeocodingService>();
builder.Services.AddScoped<RefereeScoringService>();
builder.Services.AddScoped<AssignmentService>();
builder.Services.AddHostedService<TeamGeocodingBackgroundService>();
builder.Services.AddScoped<ISmartAllocationEngine, SmartAllocationEngine>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    try
    {
        await DbInitializer.InitializeAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        // Log the full details server-side (visible in Azure log stream) and fail startup.
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>()
            .LogCritical(ex, "Database initialization failed");
        throw;
    }
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

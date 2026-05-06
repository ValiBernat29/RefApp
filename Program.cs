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
        options.UseSqlServer(connectionString));
}
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ProductionConnection"),
        sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
else
{
    // TEMPORARY: show full error details to diagnose production 500
    app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
    {
        var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        ctx.Response.ContentType = "text/plain";
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsync(
            $"REQUEST ERROR:\n{ex?.Error?.GetType().Name}: {ex?.Error?.Message}\n\n{ex?.Error?.StackTrace}" +
            $"\n\nInner: {ex?.Error?.InnerException?.Message}\n{ex?.Error?.InnerException?.StackTrace}");
    }));
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

try 
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        await DbInitializer.InitializeAsync(services);
    }
}
catch (Exception ex)
{
    var msg = $"STARTUP ERROR: {ex.Message}\nInner: {ex.InnerException?.Message}\nInner2: {ex.InnerException?.InnerException?.Message}\n\nStackTrace:\n{ex.StackTrace}\n\nInner StackTrace:\n{ex.InnerException?.StackTrace}";
    app.MapGet("/", () => msg);
    app.Run();
    return;
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.MapGet("/debug-conn", (IConfiguration config) => {
    var connStr = config.GetConnectionString("ProductionConnection");
    if (string.IsNullOrEmpty(connStr)) return "CONNECTION STRING IS MISSING OR NULL!";
    if (connStr == "Server=fake;Database=fake;") return "CONNECTION STRING FELL BACK TO FAKE!";
    
    // Safely print part of it to verify it loaded
    var safeStr = connStr.Length > 20 ? connStr.Substring(0, 20) + "..." : connStr;
    return $"CONNECTION STRING FOUND! Starts with: {safeStr}";
});

app.Run();

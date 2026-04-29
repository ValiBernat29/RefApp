using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;

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

// try 
// {
//     using (var scope = app.Services.CreateScope())
//     {
//         var services = scope.ServiceProvider;
//         await DbInitializer.InitializeAsync(services);
//     }
// }
// catch (Exception ex)
// {
//     app.MapGet("/", () => $"STARTUP ERROR: {ex.Message} | StackTrace: {ex.StackTrace}");
//     app.Run();
//     return;
// }

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();



app.Run();

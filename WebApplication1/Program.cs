using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using Serilog;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using WebApplication1.Areas.ProjectManagement.Models;
using WebApplication1.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddDbContext<ApplicationDBContext>(options =>
{
    // Try to read individual PostgreSQL environment variables first
    var pgHost = Environment.GetEnvironmentVariable("PGHOST");
    var pgPort = Environment.GetEnvironmentVariable("PGPORT") ?? "5432";
    var pgDatabase = Environment.GetEnvironmentVariable("PGDATABASE");
    var pgUser = Environment.GetEnvironmentVariable("PGUSER");
    var pgPassword = Environment.GetEnvironmentVariable("PGPASSWORD");
    
    string connectionString;
    if (!string.IsNullOrEmpty(pgHost) && !string.IsNullOrEmpty(pgDatabase) && !string.IsNullOrEmpty(pgUser))
    {
        connectionString = $"Host={pgHost};Port={pgPort};Database={pgDatabase};Username={pgUser};Password={pgPassword}";
    }
    else
    {
        // Fallback to DATABASE_URL
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrEmpty(databaseUrl))
        {
            // Parse the DATABASE_URL
            var uri = new Uri(databaseUrl);
            connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]}";
        }
        else
        {
            connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Database connection string not configured");
        }
    }
    
    options.UseNpgsql(connectionString);
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDBContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender, EmailSender>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

try
{
    await CreateSuperAdminUser(app.Services);
}
catch (Exception ex)
{
    Log.Fatal(ex, "An error occurred while creating the superadmin user.");
    throw; 
}

app.Run();

async Task CreateSuperAdminUser(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    const string adminUserName = "superadmin@pmtool.com";
    const string adminPassword = "12345678@PMtool";
    const string adminRoleName = "SuperAdmin";

    if (!await roleManager.RoleExistsAsync(adminRoleName))
    {
        await roleManager.CreateAsync(new IdentityRole(adminRoleName));
    }

    var user = await userManager.FindByEmailAsync(adminUserName);
    if (user == null)
    {
        user = new ApplicationUser {
            UserName = adminUserName, 
            Email = adminUserName, 
            EmailConfirmed = true,
            FirstName = "Super",
            LastName = "Admin"
        };
        var result = await userManager.CreateAsync(user, adminPassword);
        if (result.Succeeded)
        {
            var roleResult = await userManager.AddToRoleAsync(user, adminRoleName);
            if (!roleResult.Succeeded)
            {
                Log.Logger.Error("Failed to add user to role due to errors: {Errors}", roleResult.Errors);
            }
        }
        else
        {
            foreach (var error in result.Errors)
            {
                Log.Logger.Error("Failed to create superadmin: {Code} - {Description}", error.Code, error.Description);
            }
        }
    }
    else
    {
        Log.Logger.Information("SuperAdmin already exists.");
    }
}

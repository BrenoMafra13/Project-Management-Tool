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
    var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
        ?? builder.Configuration.GetConnectionString("DefaultConnection");
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

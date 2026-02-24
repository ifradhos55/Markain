using Microsoft.EntityFrameworkCore;
using OzarkLMS.Data;
using OzarkLMS.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();
builder.Services.AddSignalR();
builder.Services.AddScoped<ISelfTestService, SelfTestService>();
builder.Services.AddHttpClient();
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => false;
    options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
});

builder.Services.AddDbContext<OzarkLMS.Data.AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<OzarkLMS.Data.AppDbContext>();
        context.Database.Migrate(); // Apply pending migrations (Fixes CLI permission issues)
        OzarkLMS.Data.DbInitializer.Initialize(context);
        
        // Backfill Vote Counts (One-time fix for existing data)
        context.Database.ExecuteSqlRaw(
            "UPDATE \"Posts\" p " +
            "SET \"UpvoteCount\" = (SELECT COUNT(*) FROM \"PostVotes\" pv WHERE pv.\"PostId\" = p.\"Id\" AND pv.\"Value\" = 1), " +
            "    \"DownvoteCount\" = (SELECT COUNT(*) FROM \"PostVotes\" pv WHERE pv.\"PostId\" = p.\"Id\" AND pv.\"Value\" = -1)");

        
        // Create Meetings table if it doesn't exist
        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""Meetings"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""CourseId"" INTEGER NOT NULL REFERENCES ""Courses""(""Id"") ON DELETE CASCADE,
                ""Name"" TEXT NOT NULL,
                ""StartTime"" TIMESTAMP WITH TIME ZONE NOT NULL,
                ""EndTime"" TIMESTAMP WITH TIME ZONE NOT NULL,
                ""Url"" TEXT NOT NULL
            );
        ");

        // Create SharedPosts table if it doesn't exist
        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""SharedPosts"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""UserId"" INTEGER NOT NULL REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
                ""PostId"" INTEGER NOT NULL REFERENCES ""Posts""(""Id"") ON DELETE CASCADE,
                ""SharedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
        ");

    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the DB.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles(); 
app.UseCookiePolicy();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<OzarkLMS.Hubs.VoteHub>("/voteHub");

app.Run();

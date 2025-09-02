using InventoryManagementSystem.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(ConvertPostgresConnectionString(connectionString)));

builder.Services.AddControllersWithViews();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    options.KnownNetworks.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.Run();

static string ConvertPostgresConnectionString(string? databaseUrl)
{
    if (string.IsNullOrEmpty(databaseUrl))
    {
        throw new ArgumentNullException(nameof(databaseUrl), "Database connection string is not configured.");
    }

    if (!databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return databaseUrl;
    }

    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port,
        Username = userInfo[0],
        Password = userInfo[1],
        Database = uri.LocalPath.TrimStart('/'),
        SslMode = SslMode.Prefer
    };

    return builder.ToString();
}
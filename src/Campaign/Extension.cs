using DnDiscord.Campaign.BL.Campaigns;
using DnDiscord.Campaign.BL.Snapshots;
using DnDiscord.Campaign.DataAccess;
using DnDiscord.Campaign.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DnDiscord.Campaign;

/// <summary>
/// Extension methods for registering Campaign module services.
/// </summary>
public static class CampaignExtensions
{
    /// <summary>
    /// Adds Campaign module services to the dependency injection container.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IServiceCollection AddCampaignModule(this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext
        services.AddDbContext<CampaignDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b =>
                {
                    b.MigrationsAssembly(typeof(CampaignDbContext).Assembly.FullName);
                    b.MigrationsHistoryTable("__EFMigrationsHistory_Campaign");
                }));

        // Services mùtier
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<IUserContextService, UserContextService>();

        // Validation
        services.AddScoped<ICampaignValidator, CampaignValidator>();


        return services;
    }
  
    /// <summary>
    /// Configures Campaign module endpoints and middleware.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The application for chaining.</returns>
    public static WebApplication UseCampaignModule(this WebApplication app)
    {
        // Apply pending migrations (always, for Docker setup)
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();

            try
            {
                app.Logger.LogInformation("Applying database migrations...");
                dbContext.Database.Migrate();
                app.Logger.LogInformation("Database migrations applied successfully.");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("PendingModelChangesWarning"))
            {
                if (app.Environment.IsProduction())
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "An error occurred while applying database migrations.");
                if (app.Environment.IsProduction())
                {
                    throw;
                }
            }
        }
        return app;
        
    }
}


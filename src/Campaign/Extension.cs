using DnDiscord.Campaign.BL.Campaigns;
using DnDiscord.Campaign.BL.Snapshots;
using DnDiscord.Campaign.DataAccess;
using DnDiscord.Campaign.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    public static IHostApplicationBuilder AddCampaignModule(this IHostApplicationBuilder builder)
    {
        // Register DbContext
        builder.AddCampaignDbContext();
        
        // Register Campaign services
        builder.AddCampaignServices();
        
        // Register Snapshot services
        builder.AddSnapshotServices();
        
        return builder;
    }
    
    /// <summary>
    /// Adds the Campaign DbContext with PostgreSQL.
    /// </summary>
    private static IHostApplicationBuilder AddCampaignDbContext(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        
        builder.Services.AddDbContext<CampaignDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_Campaign");
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            });
            
            // Enable detailed errors in development
            if (builder.Environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });
        
        return builder;
    }
    
    /// <summary>
    /// Adds Campaign-related services.
    /// </summary>
    private static IHostApplicationBuilder AddCampaignServices(this IHostApplicationBuilder builder)
    {
        // HttpContextAccessor for user context
        builder.Services.AddHttpContextAccessor();

        // Singleton services (stateless validators)
        builder.Services.AddSingleton<ICampaignValidator, CampaignValidator>();

        // Scoped services (per-request with DbContext dependency)
        builder.Services.AddScoped<ICampaignService, CampaignService>();
        builder.Services.AddScoped<IUserContextService, UserContextService>();

        return builder;
    }
    
    /// <summary>
    /// Adds Snapshot-related services.
    /// </summary>
    private static IHostApplicationBuilder AddSnapshotServices(this IHostApplicationBuilder builder)
    {
        // Singleton services (stateless utilities)
        builder.Services.AddSingleton<ISnapshotSerializer, SnapshotSerializer>();
        builder.Services.AddSingleton<ISnapshotValidator, SnapshotValidator>();
        
        // Scoped services (per-request with DbContext dependency)
        builder.Services.AddScoped<ISnapshotService, SnapshotService>();
        
        return builder;
    }
    
    /// <summary>
    /// Configures Campaign module endpoints and middleware.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The application for chaining.</returns>
    public static WebApplication UseCampaignModule(this WebApplication app)
    {
        // Apply pending migrations (always, for Docker setup)
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
        try
        {
            dbContext.Database.Migrate();
        }
        catch (Exception ex)
        {
            // Log but don't crash - migrations might fail in some scenarios
            Console.WriteLine($"Migration warning: {ex.Message}");
        }

        return app;
    }
}


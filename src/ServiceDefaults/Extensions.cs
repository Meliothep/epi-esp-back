using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using Serilog;

using Scalar.AspNetCore;

using HealthChecks.UI.Client;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    public static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder)
    {

        builder.ConfigureSerilog();

        return builder;
    }

    public static IHostApplicationBuilder AddApiDefaults(this IHostApplicationBuilder builder)
    {
        
        builder.Services.AddControllers();

        builder.Services.AddOpenApi();

        builder.AddDefaultHealthChecks();

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(
            "/health",
            new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
        
        app.AddScalar();

        return app;
    }

    public static IHostApplicationBuilder ConfigureSerilog(this IHostApplicationBuilder builder)
    {
        var config = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .WriteTo.Seq(builder.Configuration["Seq"]!);

        if (builder.Environment.IsDevelopment())
        {
            var logDir = Path.Combine(builder.Environment.ContentRootPath, "logs");
            Directory.CreateDirectory(logDir);
            config = config.WriteTo.File(
                path: Path.Combine(logDir, "back-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");
        }

        Log.Logger = config.CreateLogger();
        builder.Services.AddSerilog();

        return builder;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        var npgSqlString = builder.Configuration["ConnectionStrings:DefaultConnection"];

        builder.Services.AddHealthChecks()
            .AddNpgSql(npgSqlString!);

        return builder;
    }

    public static WebApplication AddScalar(this WebApplication app)
    {
        app.MapOpenApi();
        string? scalarURL = Environment.GetEnvironmentVariable("SCALAR_URLS");
        scalarURL = scalarURL != null ? scalarURL : app.Configuration["Urls"];
        app.MapScalarApiReference(options =>{ options.Servers = [new ScalarServer(scalarURL!)];});
     
        return app;
    }
}
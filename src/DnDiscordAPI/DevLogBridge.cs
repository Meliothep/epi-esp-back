using System.Text;
using System.Text.Json.Serialization;

namespace DnDiscordAPI;

/// Dev-only endpoint: front POSTs console.log/warn/error batches here, we
/// append each line to logs/front-YYYY-MM-DD.log so Claude can Read it
/// directly. Not registered outside Development (Program.cs gate).
public static class DevLogBridge
{
    private static readonly object FileLock = new();

    public static WebApplication MapDevLogBridge(this WebApplication app)
    {
        app.MapPost("/api/dev/log", (DevLogBatch batch, IWebHostEnvironment env) =>
        {
            if (batch?.Entries is null || batch.Entries.Count == 0)
                return Results.NoContent();

            var logDir = Path.Combine(env.ContentRootPath, "logs");
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, $"front-{DateTime.UtcNow:yyyy-MM-dd}.log");

            var sb = new StringBuilder(batch.Entries.Count * 128);
            foreach (var entry in batch.Entries)
            {
                var ts = entry.Ts ?? DateTime.UtcNow.ToString("O");
                var level = (entry.Level ?? "log").ToUpperInvariant();
                var msg = entry.Message ?? string.Empty;
                sb.Append(ts).Append(" [").Append(level).Append("] ").AppendLine(msg);
            }

            lock (FileLock)
            {
                File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            }
            return Results.NoContent();
        })
        .AllowAnonymous()
        .RequireCors("AllowFrontend");

        return app;
    }
}

public sealed class DevLogBatch
{
    [JsonPropertyName("entries")]
    public List<DevLogEntry> Entries { get; set; } = new();
}

public sealed class DevLogEntry
{
    [JsonPropertyName("level")]
    public string? Level { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("ts")]
    public string? Ts { get; set; }
}

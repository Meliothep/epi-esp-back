using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DnDiscord.Campaign.DataAccess.Models;
using Microsoft.Extensions.Logging;

namespace DnDiscord.Campaign.BL.Snapshots;

/// <summary>
/// Handles serialization and deserialization of snapshot data.
/// </summary>
public interface ISnapshotSerializer
{
    /// <summary>
    /// Serializes snapshot data to JSON string.
    /// </summary>
    string Serialize(SnapshotData data);
    
    /// <summary>
    /// Deserializes JSON string to snapshot data.
    /// </summary>
    SnapshotData? Deserialize(string json);
    
    /// <summary>
    /// Attempts to deserialize with error handling.
    /// </summary>
    (SnapshotData? Data, string? Error) TryDeserialize(string json);
    
    /// <summary>
    /// Computes SHA-256 hash of the JSON data for integrity verification.
    /// </summary>
    string ComputeHash(string json);
    
    /// <summary>
    /// Verifies that the JSON data matches the expected hash.
    /// </summary>
    bool VerifyHash(string json, string expectedHash);
    
    /// <summary>
    /// Gets the size of the serialized data in bytes.
    /// </summary>
    long GetSizeBytes(string json);
}

/// <summary>
/// JSON serializer implementation for snapshot data.
/// </summary>
public class SnapshotSerializer : ISnapshotSerializer
{
    private readonly ILogger<SnapshotSerializer> _logger;
    private readonly JsonSerializerOptions _serializerOptions;
    
    public SnapshotSerializer(ILogger<SnapshotSerializer> logger)
    {
        _logger = logger;
        _serializerOptions = CreateSerializerOptions();
    }
    
    private static JsonSerializerOptions CreateSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false, // Compact for storage
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            },
            // Handle circular references
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            // Maximum depth for complex nested structures
            MaxDepth = 64
        };
    }
    
    /// <inheritdoc />
    public string Serialize(SnapshotData data)
    {
        try
        {
            return JsonSerializer.Serialize(data, _serializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to serialize snapshot data");
            throw new SnapshotSerializationException("Failed to serialize snapshot data", ex);
        }
    }
    
    /// <inheritdoc />
    public SnapshotData? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SnapshotData>(json, _serializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize snapshot data");
            throw new SnapshotSerializationException("Failed to deserialize snapshot data", ex);
        }
    }
    
    /// <inheritdoc />
    public (SnapshotData? Data, string? Error) TryDeserialize(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<SnapshotData>(json, _serializerOptions);
            return (data, null);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize snapshot data");
            return (null, ex.Message);
        }
    }
    
    /// <inheritdoc />
    public string ComputeHash(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
    
    /// <inheritdoc />
    public bool VerifyHash(string json, string expectedHash)
    {
        var actualHash = ComputeHash(json);
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <inheritdoc />
    public long GetSizeBytes(string json)
    {
        return Encoding.UTF8.GetByteCount(json);
    }
}

/// <summary>
/// Exception thrown when snapshot serialization fails.
/// </summary>
public class SnapshotSerializationException : Exception
{
    public SnapshotSerializationException(string message) : base(message)
    {
    }
    
    public SnapshotSerializationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}


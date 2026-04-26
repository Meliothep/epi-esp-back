using System.Text;
using DnDiscord.Campaign.BL.Snapshots;
using DnDiscord.Campaign.DataAccess.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnDiscordAPI.Tests.Utils;

public sealed class SnapshotSerializerTests
{
    private readonly SnapshotSerializer _serializer;

    public SnapshotSerializerTests()
    {
        _serializer = new SnapshotSerializer(NullLogger<SnapshotSerializer>.Instance);
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTrips()
    {
        var data = new SnapshotData
        {
            SchemaVersion = 1,
            Campaign = new CampaignData
            {
                Id = Guid.NewGuid(),
                Name = "Test Campaign",
                Description = "A test",
                DungeonMasterId = Guid.NewGuid()
            }
        };

        var json = _serializer.Serialize(data);
        var deserialized = _serializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(data.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(data.Campaign.Name, deserialized.Campaign.Name);
        Assert.Equal(data.Campaign.Id, deserialized.Campaign.Id);
    }

    [Fact]
    public void ComputeHash_IsDeterministic()
    {
        var json = "{\"schemaVersion\":1,\"campaign\":{\"name\":\"Test\"}}";

        var hash1 = _serializer.ComputeHash(json);
        var hash2 = _serializer.ComputeHash(json);

        Assert.Equal(hash1, hash2);
        Assert.False(string.IsNullOrEmpty(hash1));
    }

    [Fact]
    public void ComputeHash_DifferentInput_DifferentHash()
    {
        var hash1 = _serializer.ComputeHash("{\"a\":1}");
        var hash2 = _serializer.ComputeHash("{\"a\":2}");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyHash_CorrectHash_ReturnsTrue()
    {
        var json = "{\"test\":\"data\"}";
        var hash = _serializer.ComputeHash(json);

        Assert.True(_serializer.VerifyHash(json, hash));
    }

    [Fact]
    public void VerifyHash_WrongHash_ReturnsFalse()
    {
        var json = "{\"test\":\"data\"}";

        Assert.False(_serializer.VerifyHash(json, "0000000000000000000000000000000000000000000000000000000000000000"));
    }

    [Fact]
    public void TryDeserialize_InvalidJson_ReturnsError()
    {
        var (data, error) = _serializer.TryDeserialize("not valid json {{{");

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TryDeserialize_ValidJson_ReturnsData()
    {
        var original = new SnapshotData { SchemaVersion = 2 };
        var json = _serializer.Serialize(original);

        var (data, error) = _serializer.TryDeserialize(json);

        Assert.NotNull(data);
        Assert.Null(error);
        Assert.Equal(2, data.SchemaVersion);
    }

    [Fact]
    public void GetSizeBytes_ReturnsCorrectUtf8ByteCount()
    {
        var json = "{\"name\":\"héros\"}"; // Contains non-ASCII
        var expectedBytes = Encoding.UTF8.GetByteCount(json);

        var result = _serializer.GetSizeBytes(json);

        Assert.Equal(expectedBytes, result);
    }
}

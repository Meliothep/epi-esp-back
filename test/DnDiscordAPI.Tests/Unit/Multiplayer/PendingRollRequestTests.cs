using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Multiplayer.Models;
using Xunit;

namespace DnDiscordAPI.Tests.Unit.Multiplayer;

public class PendingRollRequestTests
{
    private static PendingRollRequest Make(params (Guid, int)[] values)
    {
        var dict = values.ToDictionary(v => v.Item1, v => v.Item2);
        return new PendingRollRequest
        {
            RequestId = Guid.NewGuid(),
            DiceType = "d20",
            Label = "Perception",
            RollValues = dict,
            PendingUserIds = new HashSet<Guid>(dict.Keys),
        };
    }

    [Fact]
    public void Constructor_PendingIdMissingFromRollValues_Throws()
    {
        var a = Guid.NewGuid();
        var ghost = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new PendingRollRequest
        {
            RequestId = Guid.NewGuid(),
            DiceType = "d20",
            Label = null,
            RollValues = new Dictionary<Guid, int> { [a] = 7 },
            PendingUserIds = new HashSet<Guid> { a, ghost },
        });
    }

    [Fact]
    public void TrySubmit_ValidTarget_ReturnsValueAndUpdatesPending()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var req = Make((a, 17), (b, 4));

        var ok = req.TrySubmit(a, out var value, out var complete);

        Assert.True(ok);
        Assert.Equal(17, value);
        Assert.False(complete);
        Assert.DoesNotContain(a, req.PendingUserIds);
        Assert.Contains(b, req.PendingUserIds);
        Assert.Equal(17, req.SubmittedValues[a]);
    }

    [Fact]
    public void TrySubmit_LastTarget_MarksComplete()
    {
        var a = Guid.NewGuid();
        var req = Make((a, 20));

        var ok = req.TrySubmit(a, out var value, out var complete);

        Assert.True(ok);
        Assert.Equal(20, value);
        Assert.True(complete);
    }

    [Fact]
    public void TrySubmit_NonTarget_ReturnsFalseAndNoMutation()
    {
        var a = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var req = Make((a, 11));

        var ok = req.TrySubmit(intruder, out var value, out var complete);

        Assert.False(ok);
        Assert.Equal(0, value);
        Assert.False(complete);
        Assert.Contains(a, req.PendingUserIds);
        Assert.Empty(req.SubmittedValues);
    }

    [Fact]
    public void TrySubmit_DoubleSubmit_ReturnsFalseSecondTime()
    {
        var a = Guid.NewGuid();
        var req = Make((a, 7));

        var first = req.TrySubmit(a, out _, out _);
        var second = req.TrySubmit(a, out var value, out var complete);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(0, value);
        Assert.False(complete);
    }

    [Fact]
    public async Task TrySubmit_ParallelLastSubmitters_ExactlyOneSeesComplete()
    {
        // 16 threads race to submit the one pending user. Exactly one must win.
        var a = Guid.NewGuid();
        var req = Make((a, 13));

        var results = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            {
                var ok = req.TrySubmit(a, out var v, out var c);
                return (ok, complete: c);
            })));

        var successes = results.Count(r => r.ok);
        Assert.Equal(1, successes);
        Assert.Equal(1, results.Count(r => r.ok && r.complete));
    }
}

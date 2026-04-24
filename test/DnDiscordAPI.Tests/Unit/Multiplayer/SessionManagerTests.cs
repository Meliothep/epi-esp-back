using System;
using Microsoft.Extensions.Logging.Abstractions;
using Multiplayer.Services;
using Xunit;

namespace DnDiscordAPI.Tests.Unit.Multiplayer;

public class SessionManagerTests
{
    private static (SessionManager sessions, MessageSequencer sequencer) Make()
    {
        var sequencer = new MessageSequencer();
        var sessions = new SessionManager(
            NullLogger<SessionManager>.Instance,
            sequencer);
        return (sessions, sequencer);
    }

    [Fact]
    public void RemoveSession_drops_session_entry_from_primary_index()
    {
        var (mgr, _) = Make();
        var session = mgr.CreateSession(Guid.NewGuid(), Guid.NewGuid(), "DM");

        var removed = mgr.RemoveSession(session.SessionId);

        Assert.True(removed);
        Assert.Null(mgr.GetSession(session.SessionId));
    }

    [Fact]
    public void RemoveSession_drops_connection_to_session_mapping()
    {
        // Regression pin for commit 0e90953: before the fix, _connectionToSession
        // entries survived RemoveSession and GetSessionByConnection returned a
        // stale session id → FindSessionByUser auto-rejoined a ghost session.
        var (mgr, _) = Make();
        var dmId = Guid.NewGuid();
        var session = mgr.CreateSession(Guid.NewGuid(), dmId, "DM");
        const string dmConnection = "conn-dm";
        var playerId = Guid.NewGuid();
        const string playerConnection = "conn-player";

        mgr.JoinSession(session.SessionId, dmId, "DM", dmConnection);
        mgr.JoinSession(session.SessionId, playerId, "Player", playerConnection);

        Assert.Equal(session.SessionId, mgr.GetSessionByConnection(dmConnection));
        Assert.Equal(session.SessionId, mgr.GetSessionByConnection(playerConnection));

        mgr.RemoveSession(session.SessionId);

        Assert.Null(mgr.GetSessionByConnection(dmConnection));
        Assert.Null(mgr.GetSessionByConnection(playerConnection));
    }

    [Fact]
    public void RemoveSession_resets_message_sequence_so_numbers_do_not_leak()
    {
        // Regression pin for commit 0e90953: MessageSequencer held the per-session
        // counter after RemoveSession, so a back-to-back session reusing the id
        // got a non-1 starting sequence.
        var (mgr, sequencer) = Make();
        var sessionId = mgr.CreateSession(Guid.NewGuid(), Guid.NewGuid(), "DM").SessionId;
        sequencer.GetNextSequence(sessionId);
        sequencer.GetNextSequence(sessionId);
        sequencer.GetNextSequence(sessionId);

        mgr.RemoveSession(sessionId);

        Assert.Equal(1, sequencer.GetNextSequence(sessionId));
    }

    [Fact]
    public void RemoveSession_returns_false_for_unknown_session()
    {
        var (mgr, _) = Make();
        Assert.False(mgr.RemoveSession("session_deadbeef"));
    }

    [Fact]
    public void RemoveSession_drops_join_code_lookup()
    {
        var (mgr, _) = Make();
        var session = mgr.CreateSession(Guid.NewGuid(), Guid.NewGuid(), "DM");
        var joinCode = session.JoinCode;
        Assert.False(string.IsNullOrWhiteSpace(joinCode));

        mgr.RemoveSession(session.SessionId);

        var rejoin = mgr.JoinSession(joinCode!, Guid.NewGuid(), "Latecomer", "conn-late");
        Assert.False(rejoin.Success);
    }
}

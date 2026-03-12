using DnDiscordAPI.Tests.Utils;

namespace DnDiscordAPI.Tests.Integration;


public class DnDiscordIntegrationFixture : IntegrationFixture<DnDiscordAPIProgram>
{
    public override string ProjectName => "DnDiscord";
    public override int ProjectDBPort => 5432;
}
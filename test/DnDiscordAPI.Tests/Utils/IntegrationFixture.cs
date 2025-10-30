
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace DnDiscordAPI.Tests.Utils;
public abstract class IntegrationFixture<TEntryPoint> : IAsyncLifetime where TEntryPoint : class
{

    public virtual string ProjectName { get { return "DnDiscord"; } }
    
    public virtual int ProjectDBPort { get { return 5432; } }
    
    private readonly PostgreSqlContainer _dbContainer; 

    public IntegrationFixture(){
        _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:latest")
            .WithName(ProjectName + "Postgres")
            .WithDatabase(ProjectName + "DB")
            .WithUsername(ProjectName)
            .WithPassword(ProjectName + "Secured")
            .WithPortBinding(ProjectDBPort, 5432)
            .Build();
    }

    public WebApplicationFactory<TEntryPoint>? factory;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        factory = new WebApplicationFactory<TEntryPoint>();
    }

    public async Task DisposeAsync()
    {
        await factory!.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }
}
using AuditLogService.Application.DTOs;
using AuditLogService.Domain.Entities;
using AuditLogService.Domain.Repositories;
using AuditLogService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using Testcontainers.PostgreSql;
using Xunit;

namespace AuditLogService.Api.Tests;

public sealed class AuditEventsControllerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("auditlog_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the production DbContext and NpgsqlDataSource registrations
                    var dbCtxDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AuditDbContext>));
                    if (dbCtxDescriptor != null) services.Remove(dbCtxDescriptor);

                    var dsDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(NpgsqlDataSource));
                    if (dsDescriptor != null) services.Remove(dsDescriptor);

                    var dataSourceBuilder = new NpgsqlDataSourceBuilder(_postgres.GetConnectionString());
                    dataSourceBuilder.EnableDynamicJson();
                    var dataSource = dataSourceBuilder.Build();

                    services.AddSingleton(dataSource);
                    services.AddDbContext<AuditDbContext>(opts => opts.UseNpgsql(dataSource));
                });
            });

        _client = _factory.CreateClient();

        // Apply migrations
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Post_RecordsEvent_Returns201()
    {
        var payload = new
        {
            actor = "user@example.com",
            action = "create",
            resource = "order",
            resourceId = "ord-1",
            correlationId = (string?)null,
            metadata = (object?)null
        };

        var response = await _client.PostAsJsonAsync("/api/audit-events", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<AuditEventDto>();
        dto!.Actor.ShouldBe("user@example.com");
        dto.Timestamp.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Post_TimestampIsServerAssigned()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var payload = new
        {
            actor = "system",
            action = "sync",
            resource = "inventory"
        };

        var response = await _client.PostAsJsonAsync("/api/audit-events", payload);
        var dto = await response.Content.ReadFromJsonAsync<AuditEventDto>();

        dto!.Timestamp.ShouldBeGreaterThan(before);
        dto.Timestamp.ShouldBeLessThan(DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Get_ReturnsRecordedEvents()
    {
        var actor = $"test-{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/api/audit-events",
            new { actor, action = "read", resource = "doc" });

        var response = await _client.GetAsync($"/api/audit-events?actor={actor}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var events = await response.Content.ReadFromJsonAsync<List<AuditEventDto>>();
        events!.Count.ShouldBe(1);
        events[0].Actor.ShouldBe(actor);
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=201")]
    [InlineData("page=0")]
    public async Task Get_InvalidPagination_Returns400(string queryString)
    {
        var response = await _client.GetAsync($"/api/audit-events?{queryString}");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Post_BlankActor_Returns400(string actor)
    {
        var response = await _client.PostAsJsonAsync("/api/audit-events",
            new { actor, action = "x", resource = "y" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Actor");
    }

    [Fact]
    public async Task Post_MissingRequiredFields_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/audit-events",
            new { actor = "a" }); // action and resource missing

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnhandledException_InProduction_DoesNotLeakStackTrace()
    {
        // Override IAuditEventRepository with one that always throws, then
        // boot a Production-environment factory so UseExceptionHandler is wired
        // (not UseDeveloperExceptionPage) and assert no stack trace leaks.
        await using var prodFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureServices(services =>
                {
                    var dbCtxDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AuditDbContext>));
                    if (dbCtxDescriptor != null) services.Remove(dbCtxDescriptor);

                    var dsDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(NpgsqlDataSource));
                    if (dsDescriptor != null) services.Remove(dsDescriptor);

                    var dataSourceBuilder = new NpgsqlDataSourceBuilder(_postgres.GetConnectionString());
                    dataSourceBuilder.EnableDynamicJson();
                    var ds = dataSourceBuilder.Build();
                    services.AddSingleton(ds);
                    services.AddDbContext<AuditDbContext>(opts => opts.UseNpgsql(ds));

                    var repoDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IAuditEventRepository));
                    if (repoDescriptor != null) services.Remove(repoDescriptor);
                    services.AddScoped<IAuditEventRepository, ThrowingAuditEventRepository>();
                });
            });
        using var prodClient = prodFactory.CreateClient();

        var payload = new { actor = "alice", action = "create", resource = "doc" };
        var response = await prodClient.PostAsJsonAsync("/api/audit-events", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("at AuditLogService.");
        body.ShouldNotContain("StackTrace");
        body.ShouldNotContain("ThrowingAuditEventRepository");
    }

    private sealed class ThrowingAuditEventRepository : IAuditEventRepository
    {
        public Task AppendAsync(AuditEvent auditEvent, CancellationToken ct = default)
            => throw new InvalidOperationException("simulated infrastructure failure");

        public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditEventFilter filter, CancellationToken ct = default)
            => throw new InvalidOperationException("simulated infrastructure failure");

        public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default)
            => throw new InvalidOperationException("simulated infrastructure failure");
    }

    [Fact]
    public async Task Post_CorrelationIdPropagatedInResponse()
    {
        var correlationId = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/audit-events")
        {
            Content = JsonContent.Create(new { actor = "a", action = "b", resource = "c" })
        };
        request.Headers.Add("X-Correlation-ID", correlationId);

        var response = await _client.SendAsync(request);

        response.Headers.Contains("X-Correlation-ID").ShouldBeTrue();
        response.Headers.GetValues("X-Correlation-ID").ShouldContain(correlationId);
    }
}

using System.Net.Http.Json;
using MemeHelper.Api.Database;
using MemeHelper.Api.Features.Status;
using MemeHelper.Api.Telegram;
using MemeHelper.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace MemeHelper.Tests.Features.Status;

public sealed class GetStatusTests : IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly MemeApiClient client;

    public GetStatusTests()
    {
        client = new MemeApiClient(factory.CreateClient());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task GetStatus_兩張其中一張停用_應回總數與啟用數()
    {
        await client.UploadOkAsync(SampleImages.Png(), "a.png", "六點", Ct);
        var disabled = await client.UploadOkAsync(SampleImages.Jpg(), "b.jpg", "海螺", Ct);
        await factory.Services.GetRequiredService<IMemeRepository>().SetEnabledAsync(disabled.Id, false, Ct);

        var status = await GetAsync();

        status.MemeCount.Should().Be(2);
        status.EnabledCount.Should().Be(1);
    }

    [Fact]
    public async Task GetStatus_還沒polling成功過_LastPollingSuccessAt應為null()
    {
        (await GetAsync()).LastPollingSuccessAt.Should().BeNull();
    }

    [Fact]
    public async Task GetStatus_polling成功過_應回那次的時間()
    {
        var health = factory.Services.GetRequiredService<PollingHealth>();
        health.MarkSuccess();

        var status = await GetAsync();

        status.LastPollingSuccessAt.Should().Be(health.LastSuccessAt);
        status.LastPollingSuccessAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStatus_備份資料夾沒有快照_LastBackupAt應為null()
    {
        (await GetAsync()).LastBackupAt.Should().BeNull();
    }

    [Fact]
    public async Task GetStatus_備份資料夾有快照_應回最新快照的時間()
    {
        var snapshots = Path.Combine(factory.BackupRoot, "db");
        Directory.CreateDirectory(snapshots);
        await File.WriteAllBytesAsync(Path.Combine(snapshots, "memehelper-20261028T030000Z.db"), [], Ct);
        await File.WriteAllBytesAsync(Path.Combine(snapshots, "memehelper-20261029T030000Z.db"), [], Ct);

        var status = await GetAsync();

        status.LastBackupAt.Should().Be(new DateTimeOffset(2026, 10, 29, 3, 0, 0, TimeSpan.Zero));
    }

    private async Task<ServiceStatus> GetAsync() =>
        (await client.Http.GetFromJsonAsync<ServiceStatus>("/api/status", Ct))!;
}

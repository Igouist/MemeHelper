using Dapper;
using MemeHelper.Api.Configuration;
using MemeHelper.Api.Database;
using MemeHelper.Api.Features.Memes;
using MemeHelper.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace MemeHelper.Tests.Features.Memes;

public sealed class ContentHashBackfillTests : IDisposable
{
    private readonly TempDatabase db = new();
    private readonly TempDirectory images = new();
    private readonly SqliteConnectionFactory connections;
    private readonly SqliteMemeRepository repository;
    private readonly FileSystemImageStore store;
    private readonly RecordingLogger<ContentHashBackfill> log = new();

    public ContentHashBackfillTests()
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = db.ConnectionString });
        new DatabaseMigrator(options, NullLogger<DatabaseMigrator>.Instance).Migrate();
        connections = new SqliteConnectionFactory(options);
        repository = new SqliteMemeRepository(connections, new FakeTimeProvider());
        store = new FileSystemImageStore(Options.Create(new StorageOptions { ImageRoot = images.Path }));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        db.Dispose();
        images.Dispose();
    }

    [Fact]
    public async Task RunAsync_舊梗圖沒有雜湊_應從圖片檔補算()
    {
        var id = await AddLegacyAsync("png", SampleImages.Png());

        await Backfill().RunAsync(Ct);

        var found = await repository.FindByContentHashAsync(ContentHash.Compute(SampleImages.Png()), Ct);
        found!.Id.Should().Be(id);
        (await repository.ListWithoutContentHashAsync(Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_圖片檔不存在_應記錄警告並留空()
    {
        var id = await AddLegacyAsync("png", content: null);

        await Backfill().RunAsync(Ct);

        (await repository.ListWithoutContentHashAsync(Ct)).Select(m => m.Id).Should().Equal(id);
        log.Has(LogLevel.Warning, $"編號 {id}").Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_兩張舊圖內容相同_後者應留空並記錄警告()
    {
        var first = await AddLegacyAsync("png", SampleImages.Png());
        var second = await AddLegacyAsync("png", SampleImages.Png());

        await Backfill().RunAsync(Ct);

        (await repository.FindByContentHashAsync(ContentHash.Compute(SampleImages.Png()), Ct))!.Id.Should().Be(first);
        (await repository.ListWithoutContentHashAsync(Ct)).Select(m => m.Id).Should().Equal(second);
        log.Has(LogLevel.Warning, $"編號 {second} 與編號 {first}").Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_都已有雜湊_不應記錄任何東西()
    {
        await repository.AddAsync("png", "already", ["六點"], Ct);

        await Backfill().RunAsync(Ct);

        log.Entries.Should().BeEmpty();
    }

    private ContentHashBackfill Backfill() => new(repository, store, log);

    /// <summary>模擬升級前新增的梗圖：資料庫沒有雜湊，圖片檔可有可無。</summary>
    private async Task<int> AddLegacyAsync(string extension, byte[]? content)
    {
        var id = await repository.AddAsync(extension, $"legacy-{Guid.NewGuid():N}", ["舊圖"], Ct);
        await using (var connection = await connections.OpenAsync(Ct))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE memes SET content_hash = NULL WHERE id = @id", new { id }, cancellationToken: Ct));
        }

        if (content is not null)
        {
            await store.SaveAsync(id, extension, new MemoryStream(content), Ct);
        }

        return id;
    }
}

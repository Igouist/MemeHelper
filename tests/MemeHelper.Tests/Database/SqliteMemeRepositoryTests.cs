using Dapper;
using MemeHelper.Api.Configuration;
using MemeHelper.Api.Database;
using MemeHelper.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace MemeHelper.Tests.Database;

public sealed class SqliteMemeRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);

    private readonly TempDatabase db = new();
    private readonly SqliteConnectionFactory connections;
    private readonly SqliteMemeRepository repository;

    public SqliteMemeRepositoryTests()
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = db.ConnectionString });
        new DatabaseMigrator(options, NullLogger<DatabaseMigrator>.Instance).Migrate();
        connections = new SqliteConnectionFactory(options);
        repository = new SqliteMemeRepository(connections, new FakeTimeProvider(Now));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task AddAsync_新增一張圖_應回傳遞增編號且GetAsync讀得到相同內容()
    {
        var id = await repository.AddAsync("png", ["六點", "下班"], Ct);

        var meme = await repository.GetAsync(id, Ct);

        id.Should().Be(1);
        meme.Should().NotBeNull();
        meme!.Extension.Should().Be("png");
        meme.IsEnabled.Should().BeTrue();
        meme.TelegramFileId.Should().BeNull();
        meme.Keywords.Should().Equal("六點", "下班");
        meme.CreatedAt.Should().Be(Now);
        meme.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task ListAsync_includeDisabled為false_不應包含停用的圖()
    {
        var enabledId = await repository.AddAsync("png", ["啟用"], Ct);
        var disabledId = await repository.AddAsync("png", ["停用"], Ct);
        await repository.SetEnabledAsync(disabledId, false, Ct);

        var memes = await repository.ListAsync(includeDisabled: false, Ct);

        memes.Select(m => m.Id).Should().Equal(enabledId);
    }

    [Fact]
    public async Task ListAsync_新增三張_應依編號由小到大()
    {
        await repository.AddAsync("png", ["一"], Ct);
        await repository.AddAsync("jpg", ["二"], Ct);
        await repository.AddAsync("png", ["三"], Ct);

        var memes = await repository.ListAsync(includeDisabled: true, Ct);

        memes.Select(m => m.Id).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task AddKeywordsAsync_新增已存在但大小寫不同的關鍵字_不應重複()
    {
        var id = await repository.AddAsync("png", ["Hello"], Ct);

        var found = await repository.AddKeywordsAsync(id, ["hello", "新詞"], Ct);

        found.Should().BeTrue();
        (await repository.GetAsync(id, Ct))!.Keywords.Should().Equal("Hello", "新詞");
    }

    [Fact]
    public async Task AddKeywordsAsync_編號不存在_應回傳false()
    {
        var found = await repository.AddKeywordsAsync(999, ["x"], Ct);

        found.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveKeywordAsync_移除存在的關鍵字_應回傳Removed且讀回不含該字()
    {
        var id = await repository.AddAsync("png", ["六點", "下班", "蟹老闆"], Ct);

        var result = await repository.RemoveKeywordAsync(id, "下班", Ct);

        result.Should().Be(RemoveKeywordResult.Removed);
        (await repository.GetAsync(id, Ct))!.Keywords.Should().Equal("六點", "蟹老闆");
    }

    [Fact]
    public async Task RemoveKeywordAsync_關鍵字不存在_應回傳KeywordNotFound()
    {
        var id = await repository.AddAsync("png", ["六點"], Ct);

        var result = await repository.RemoveKeywordAsync(id, "不存在", Ct);

        result.Should().Be(RemoveKeywordResult.KeywordNotFound);
    }

    [Fact]
    public async Task RemoveKeywordAsync_編號不存在_應回傳MemeNotFound()
    {
        var result = await repository.RemoveKeywordAsync(999, "六點", Ct);

        result.Should().Be(RemoveKeywordResult.MemeNotFound);
    }

    [Fact]
    public async Task SetEnabledAsync_停用後_GetAsync的IsEnabled應為false()
    {
        var id = await repository.AddAsync("png", ["六點"], Ct);

        var found = await repository.SetEnabledAsync(id, false, Ct);

        found.Should().BeTrue();
        (await repository.GetAsync(id, Ct))!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SetTelegramFileIdAsync_設定後_GetAsync應讀到相同file_id()
    {
        var id = await repository.AddAsync("png", ["六點"], Ct);

        var found = await repository.SetTelegramFileIdAsync(id, "FILE_ID_1", Ct);

        found.Should().BeTrue();
        (await repository.GetAsync(id, Ct))!.TelegramFileId.Should().Be("FILE_ID_1");
    }

    [Fact]
    public async Task DeleteAsync_刪除存在的圖_應回傳該紀錄且關鍵字一併刪除()
    {
        var id = await repository.AddAsync("jpg", ["六點", "下班"], Ct);

        var deleted = await repository.DeleteAsync(id, Ct);

        deleted.Should().NotBeNull();
        deleted!.Extension.Should().Be("jpg");
        (await repository.GetAsync(id, Ct)).Should().BeNull();
        await using var connection = await connections.OpenAsync(Ct);
        var keywordCount = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM meme_keywords", cancellationToken: Ct));
        keywordCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_編號不存在_應回傳null()
    {
        var deleted = await repository.DeleteAsync(999, Ct);

        deleted.Should().BeNull();
    }
}

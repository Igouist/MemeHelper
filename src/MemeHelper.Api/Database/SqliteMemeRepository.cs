using System.Data.Common;
using System.Globalization;
using Dapper;
using MemeHelper.Api.Features.Memes;

namespace MemeHelper.Api.Database;

public sealed class SqliteMemeRepository(IDbConnectionFactory connections, TimeProvider clock) : IMemeRepository
{
    public async Task<int> AddAsync(string extension, IReadOnlyList<string> keywords, CancellationToken ct)
    {
        var now = Timestamp();
        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO memes (extension, is_enabled, created_at, updated_at)
            VALUES (@extension, 1, @now, @now);
            SELECT last_insert_rowid();
            """,
            new { extension, now },
            transaction,
            cancellationToken: ct));

        await InsertKeywordsAsync(connection, transaction, id, keywords, ct);
        await transaction.CommitAsync(ct);
        return id;
    }

    public async Task<Meme?> GetAsync(int id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        var memes = await ReadMemesAsync(connection, "WHERE m.id = @id", new { id }, ct);
        return memes.SingleOrDefault();
    }

    public async Task<IReadOnlyList<Meme>> ListAsync(bool includeDisabled, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await ReadMemesAsync(connection, includeDisabled ? "" : "WHERE m.is_enabled = 1", new { }, ct);
    }

    public async Task<bool> AddKeywordsAsync(int id, IReadOnlyList<string> keywords, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var existing = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT keyword FROM meme_keywords WHERE meme_id = @id",
            new { id },
            transaction,
            cancellationToken: ct))).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!await ExistsAsync(connection, transaction, id, ct))
        {
            return false;
        }

        var fresh = keywords.Where(k => !existing.Contains(k)).ToList();
        await InsertKeywordsAsync(connection, transaction, id, fresh, ct);
        await TouchAsync(connection, transaction, id, ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<RemoveKeywordResult> RemoveKeywordAsync(int id, string keyword, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        if (!await ExistsAsync(connection, transaction, id, ct))
        {
            return RemoveKeywordResult.MemeNotFound;
        }

        var removed = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM meme_keywords WHERE meme_id = @id AND keyword = @keyword COLLATE NOCASE",
            new { id, keyword },
            transaction,
            cancellationToken: ct));

        if (removed == 0)
        {
            return RemoveKeywordResult.KeywordNotFound;
        }

        await TouchAsync(connection, transaction, id, ct);
        await transaction.CommitAsync(ct);
        return RemoveKeywordResult.Removed;
    }

    public async Task<bool> SetEnabledAsync(int id, bool isEnabled, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE memes SET is_enabled = @isEnabled, updated_at = @now WHERE id = @id",
            new { id, isEnabled, now = Timestamp() },
            cancellationToken: ct));
        return updated == 1;
    }

    public async Task<bool> SetTelegramFileIdAsync(int id, string? fileId, CancellationToken ct)
    {
        // 不更新 updated_at：file_id 是快取，不是使用者看得到的內容異動。
        await using var connection = await connections.OpenAsync(ct);
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE memes SET telegram_file_id = @fileId WHERE id = @id",
            new { id, fileId },
            cancellationToken: ct));
        return updated == 1;
    }

    public async Task<Meme?> DeleteAsync(int id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var meme = (await ReadMemesAsync(connection, "WHERE m.id = @id", new { id }, ct, transaction)).SingleOrDefault();
        if (meme is null)
        {
            return null;
        }

        // meme_keywords 靠 ON DELETE CASCADE 一起刪，連線工廠已開 foreign_keys。
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM memes WHERE id = @id", new { id }, transaction, cancellationToken: ct));
        await transaction.CommitAsync(ct);
        return meme;
    }

    private string Timestamp() => clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);

    private static async Task<bool> ExistsAsync(DbConnection connection, DbTransaction transaction, int id, CancellationToken ct)
    {
        var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM memes WHERE id = @id", new { id }, transaction, cancellationToken: ct));
        return count == 1;
    }

    private async Task TouchAsync(DbConnection connection, DbTransaction transaction, int id, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE memes SET updated_at = @now WHERE id = @id",
            new { id, now = Timestamp() },
            transaction,
            cancellationToken: ct));
    }

    private static async Task InsertKeywordsAsync(
        DbConnection connection, DbTransaction transaction, int id, IReadOnlyList<string> keywords, CancellationToken ct)
    {
        foreach (var keyword in keywords)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO meme_keywords (meme_id, keyword) VALUES (@id, @keyword)",
                new { id, keyword },
                transaction,
                cancellationToken: ct));
        }
    }

    /// <summary>
    /// 先讀 memes 再讀關鍵字，關鍵字依 rowid 排序保留插入順序。
    /// 數千筆的規模兩次查詢就夠，不需要 JOIN 後再手動分組。
    /// </summary>
    private static async Task<IReadOnlyList<Meme>> ReadMemesAsync(
        DbConnection connection, string where, object parameters, CancellationToken ct, DbTransaction? transaction = null)
    {
        var rows = (await connection.QueryAsync<MemeRow>(new CommandDefinition(
            $"""
            SELECT m.id AS Id, m.extension AS Extension, m.is_enabled AS IsEnabled,
                   m.telegram_file_id AS TelegramFileId, m.created_at AS CreatedAt, m.updated_at AS UpdatedAt
            FROM memes m {where} ORDER BY m.id
            """,
            parameters,
            transaction,
            cancellationToken: ct))).ToList();

        if (rows.Count == 0)
        {
            return [];
        }

        var keywordRows = await connection.QueryAsync<KeywordRow>(new CommandDefinition(
            $"SELECT k.meme_id AS MemeId, k.keyword AS Keyword FROM meme_keywords k JOIN memes m ON m.id = k.meme_id {where} ORDER BY k.rowid",
            parameters,
            transaction,
            cancellationToken: ct));

        var keywordsByMeme = keywordRows
            .GroupBy(k => k.MemeId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(k => k.Keyword).ToList());

        return rows.Select(r => new Meme(
            r.Id,
            r.Extension,
            r.IsEnabled == 1,
            r.TelegramFileId,
            keywordsByMeme.GetValueOrDefault(r.Id, []),
            DateTimeOffset.Parse(r.CreatedAt, CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(r.UpdatedAt, CultureInfo.InvariantCulture))).ToList();
    }

    // Dapper 對 positional record 要求建構式參數型別與欄位完全一致（SQLite 整數是 Int64），
    // 用可設定屬性的類別讓 Dapper 自己做型別轉換。
    private sealed class MemeRow
    {
        public int Id { get; set; }

        public string Extension { get; set; } = "";

        public long IsEnabled { get; set; }

        public string? TelegramFileId { get; set; }

        public string CreatedAt { get; set; } = "";

        public string UpdatedAt { get; set; } = "";
    }

    private sealed class KeywordRow
    {
        public int MemeId { get; set; }

        public string Keyword { get; set; } = "";
    }
}

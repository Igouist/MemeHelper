using MemeHelper.Api.Database;
using Microsoft.AspNetCore.Mvc;

namespace MemeHelper.Api.Features.Memes;

internal static class UploadMeme
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", Handle)
            // 後台只在區網用、沒有登入，antiforgery 沒有東西可以保護，只會擋掉 Swagger UI 的上傳。
            .DisableAntiforgery()
            .Produces<Meme>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<IResult> Handle(
        IFormFile file,
        [FromForm] string? keywords,
        IMemeRepository repository,
        IImageStore images,
        CancellationToken ct)
    {
        if (!ImageFileRules.TryGetExtension(file.FileName, out var extension))
        {
            return Invalid("file", "只接受 jpg 或 png");
        }

        if (file.Length > ImageFileRules.MaxBytes)
        {
            return Invalid("file", "檔案超過 10 MB");
        }

        // 整個檔案先進記憶體：上限 10 MB、單人使用，換來檔頭檢查後不必依賴表單串流能否 seek。
        using var content = new MemoryStream();
        await file.CopyToAsync(content, ct);
        if (!ImageFileRules.HasValidSignature(content.GetBuffer().AsSpan(0, (int)content.Length), extension))
        {
            return Invalid("file", $"檔案內容不是 {extension}");
        }

        var contentHash = ContentHash.Compute(content.GetBuffer().AsSpan(0, (int)content.Length));
        if (await repository.FindByContentHashAsync(contentHash, ct) is { } existing)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "圖片已存在",
                detail: $"與編號 {existing.Id} 內容相同",
                extensions: new Dictionary<string, object?> { ["existingId"] = existing.Id });
        }

        var normalized = KeywordNormalizer.Normalize([keywords ?? ""]);
        if (normalized.Count == 0)
        {
            return Invalid("keywords", "至少要一個關鍵字");
        }

        var id = await repository.AddAsync(extension, contentHash, normalized, ct);
        try
        {
            content.Position = 0;
            await images.SaveAsync(id, extension, content, ct);
        }
        catch
        {
            // 檔案沒存成功就不該留下一筆沒有圖的紀錄。
            await repository.DeleteAsync(id, ct);
            throw;
        }

        var meme = await repository.GetAsync(id, ct);
        return Results.Created($"/api/memes/{id}", meme);
    }

    private static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}

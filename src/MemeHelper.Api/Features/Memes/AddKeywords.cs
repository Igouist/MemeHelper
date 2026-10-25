using MemeHelper.Api.Database;

namespace MemeHelper.Api.Features.Memes;

internal static class AddKeywords
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:int}/keywords", Handle)
            .Produces<Meme>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

    private static async Task<IResult> Handle(int id, string[] keywords, IMemeRepository repository, CancellationToken ct)
    {
        var normalized = KeywordNormalizer.Normalize(keywords);
        if (normalized.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["keywords"] = ["至少要一個關鍵字"] });
        }

        if (!await repository.AddKeywordsAsync(id, normalized, ct))
        {
            return Results.NotFound();
        }

        return Results.Ok(await repository.GetAsync(id, ct));
    }
}

using MemeHelper.Api.Database;

namespace MemeHelper.Api.Features.Memes;

internal static class RemoveKeyword
{
    public static void Map(RouteGroupBuilder group) =>
        // catch-all 讓含 / 的關鍵字也刪得掉。
        group.MapDelete("/{id:int}/keywords/{**keyword}", Handle)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

    private static async Task<IResult> Handle(int id, string keyword, IMemeRepository repository, CancellationToken ct) =>
        await repository.RemoveKeywordAsync(id, keyword, ct) == RemoveKeywordResult.Removed
            ? Results.NoContent()
            : Results.NotFound();
}

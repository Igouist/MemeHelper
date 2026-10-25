using MemeHelper.Api.Database;

namespace MemeHelper.Api.Features.Memes;

internal static class DeleteMeme
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapDelete("/{id:int}", Handle)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

    private static async Task<IResult> Handle(int id, IMemeRepository repository, IImageStore images, CancellationToken ct)
    {
        var deleted = await repository.DeleteAsync(id, ct);
        if (deleted is null)
        {
            return Results.NotFound();
        }

        // 先刪紀錄再刪檔：檔案已經不見的紀錄照樣要能刪掉，所以不看 Delete 的回傳值。
        images.Delete(id, deleted.Extension);
        return Results.NoContent();
    }
}

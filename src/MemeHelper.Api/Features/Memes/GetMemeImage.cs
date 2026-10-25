using MemeHelper.Api.Database;

namespace MemeHelper.Api.Features.Memes;

internal static class GetMemeImage
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:int}/image", Handle).Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status404NotFound);

    private static async Task<IResult> Handle(int id, IMemeRepository repository, IImageStore images, CancellationToken ct)
    {
        var meme = await repository.GetAsync(id, ct);
        if (meme is null)
        {
            return Results.NotFound();
        }

        // 紀錄在、檔案不在（被手動刪掉）也回 404，讓後台看得出這張圖壞了。
        var stream = images.OpenRead(id, meme.Extension);
        return stream is null
            ? Results.NotFound()
            : Results.File(stream, ImageFileRules.GetContentType(meme.Extension));
    }
}

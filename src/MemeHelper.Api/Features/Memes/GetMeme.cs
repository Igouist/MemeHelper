using MemeHelper.Api.Database;

namespace MemeHelper.Api.Features.Memes;

internal static class GetMeme
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:int}", Handle).Produces<Meme>().Produces(StatusCodes.Status404NotFound);

    private static async Task<IResult> Handle(int id, IMemeRepository repository, CancellationToken ct) =>
        await repository.GetAsync(id, ct) is { } meme ? Results.Ok(meme) : Results.NotFound();
}

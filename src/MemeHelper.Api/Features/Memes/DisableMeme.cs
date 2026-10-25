using MemeHelper.Api.Database;

namespace MemeHelper.Api.Features.Memes;

internal static class DisableMeme
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:int}/disable", Handle).Produces<Meme>().Produces(StatusCodes.Status404NotFound);

    private static async Task<IResult> Handle(int id, IMemeRepository repository, CancellationToken ct) =>
        await repository.SetEnabledAsync(id, false, ct)
            ? Results.Ok(await repository.GetAsync(id, ct))
            : Results.NotFound();
}

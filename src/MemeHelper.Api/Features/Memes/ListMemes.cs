using MemeHelper.Api.Features.Search;

namespace MemeHelper.Api.Features.Memes;

internal static class ListMemes
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", Handle).Produces<IReadOnlyList<Meme>>();

    private static async Task<IReadOnlyList<Meme>> Handle(
        MemeSearch search,
        CancellationToken ct,
        string? keyword = null,
        bool includeDisabled = true) =>
        await search.FindAsync(keyword ?? "", includeDisabled, ct);
}

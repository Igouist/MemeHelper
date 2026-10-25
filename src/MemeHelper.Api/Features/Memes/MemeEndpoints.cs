namespace MemeHelper.Api.Features.Memes;

internal static class MemeEndpoints
{
    public static IEndpointRouteBuilder MapMemeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/memes").WithTags("Memes");

        UploadMeme.Map(group);
        ListMemes.Map(group);
        GetMeme.Map(group);
        GetMemeImage.Map(group);

        return app;
    }
}

using MemeHelper.Api.Configuration;
using Microsoft.Extensions.Options;

namespace MemeHelper.Api.Features.Memes;

public sealed class FileSystemImageStore(IOptions<StorageOptions> options) : IImageStore
{
    public async Task SaveAsync(int id, string extension, Stream content, CancellationToken ct)
    {
        Directory.CreateDirectory(options.Value.ImageRoot);
        await using var file = File.Create(PathOf(id, extension));
        await content.CopyToAsync(file, ct);
    }

    public Stream? OpenRead(int id, string extension)
    {
        var path = PathOf(id, extension);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public bool Delete(int id, string extension)
    {
        var path = PathOf(id, extension);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    private string PathOf(int id, string extension) => Path.Combine(options.Value.ImageRoot, $"{id}.{extension}");
}

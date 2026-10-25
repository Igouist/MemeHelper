using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MemeHelper.Api.Features.Memes;

namespace MemeHelper.Tests.Support;

public sealed class MemeApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;

    public async Task<HttpResponseMessage> UploadAsync(byte[] content, string fileName, string keywords, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        form.Add(new StringContent(keywords), "keywords");
        return await Http.PostAsync("/api/memes", form, ct);
    }

    public async Task<Meme> UploadOkAsync(byte[] content, string fileName, string keywords, CancellationToken ct)
    {
        var response = await UploadAsync(content, fileName, keywords, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Meme>(ct))!;
    }
}

using MemeHelper.Api.Database;
using MemeHelper.Api.Features.Memes;
using MemeHelper.Api.Features.Search;
using NSubstitute;

namespace MemeHelper.Tests.Features.Search;

public class MemeSearchTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<Meme> Library =
    [
        new(1, "png", true, null, ["六點", "下班", "蟹老闆"], Now, Now),
        new(2, "png", true, null, ["六點", "太棒了", "章魚哥"], Now, Now),
        new(3, "png", true, null, ["神奇海螺", "不行"], Now, Now),
    ];

    private readonly IMemeRepository repository = Substitute.For<IMemeRepository>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FindAsync_查詢命中兩張_應只回傳命中的兩張並保持順序()
    {
        repository.ListAsync(false, Arg.Any<CancellationToken>()).Returns(Library);
        var search = new MemeSearch(repository);

        var found = await search.FindAsync("六點", includeDisabled: false, Ct);

        found.Select(m => m.Id).Should().Equal(1, 2);
    }

    [Fact]
    public async Task FindAsync_查詢為空字串_應回傳repository的全部結果()
    {
        repository.ListAsync(true, Arg.Any<CancellationToken>()).Returns(Library);
        var search = new MemeSearch(repository);

        var found = await search.FindAsync("", includeDisabled: true, Ct);

        found.Should().BeEquivalentTo(Library);
    }

    [Fact]
    public async Task FindAsync_includeDisabled為false_應以false呼叫repository()
    {
        repository.ListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([]);
        var search = new MemeSearch(repository);

        await search.FindAsync("x", includeDisabled: false, Ct);

        await repository.Received(1).ListAsync(false, Arg.Any<CancellationToken>());
    }
}

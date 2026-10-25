using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace MemeHelper.Tests.Support;

/// <summary>
/// 跑真的 Program，但資料庫與圖片都指到臨時資料夾，並拿掉所有 hosted service
/// （目前只有 Telegram polling），測試才不會真的連 Telegram。
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly TempDirectory temp = new();

    public string ImageRoot => Path.Combine(temp.Path, "images");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 用 ConfigureAppConfiguration 而非 UseSetting：Program 之後還會 AddJsonFile("appsettings.Local.json")，
        // 這裡的來源要排在它後面才能蓋過開發機上真實的 token。
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = $"Data Source={Path.Combine(temp.Path, "test.db")}",
            ["Storage:ImageRoot"] = ImageRoot,
            ["Telegram:BotToken"] = "123456789:AAFakeTokenForTests-0123456789abcdefg",
            ["Telegram:AllowedUserIds:0"] = "1",
        }));

        builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            SqliteConnection.ClearAllPools();
            temp.Dispose();
        }
    }
}

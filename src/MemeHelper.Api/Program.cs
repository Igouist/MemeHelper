using MemeHelper.Api.Configuration;
using MemeHelper.Api.Database;
using MemeHelper.Api.Features.Search;

// 以組件所在目錄為工作目錄，否則 Data/ 與 appsettings 這類相對路徑會跟著
// 啟動位置跑（systemd 會繼承啟動它的環境）。
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// 不在 systemd 底下時這行是 no-op，開發時直接 dotnet run 也安全。
builder.Host.UseSystemd();

builder.Services.AddMemeHelperOptions(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
builder.Services.AddSingleton<DatabaseMigrator>();
builder.Services.AddSingleton<IMemeRepository, SqliteMemeRepository>();
builder.Services.AddSingleton<MemeSearch>();

var app = builder.Build();

app.Services.GetRequiredService<DatabaseMigrator>().Migrate();

app.MapGet("/health", () => Results.Ok());

app.Run();

public partial class Program;

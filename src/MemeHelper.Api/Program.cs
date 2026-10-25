// 以組件所在目錄為工作目錄，否則 Data/ 與 appsettings 這類相對路徑會跟著
// 啟動位置跑（systemd 會繼承啟動它的環境）。
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok());

app.Run();

public partial class Program;

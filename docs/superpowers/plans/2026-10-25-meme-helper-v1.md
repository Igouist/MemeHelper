# MemeHelper 第一版實作計畫

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 做出一個跑在家中 Linux 設備上的 .NET 10 服務：Telegram bot 用自訂關鍵字找梗圖並回圖，Minimal API 加 Swagger UI 管理圖片與關鍵字。

**Architecture:** 單一 `MemeHelper.Api` 專案，依功能分資料夾。SQLite 存 metadata（Dapper 手寫 SQL，DbUp 套 schema），圖片放本機資料夾，Telegram 用 long polling 跑在同一程序的 hosted service。搜尋比對是純 C# 函式，Telegram 與管理 API 共用。Telegram 的收發包在一個三方法的 `IBotMessenger` 後面，bot 邏輯才能用 NSubstitute 測。

**Tech Stack:** .NET 10、ASP.NET Core Minimal API、Swashbuckle、Dapper、Microsoft.Data.Sqlite、dbup-sqlite、Telegram.Bot 22、xunit.v3、AutoFixture、NSubstitute、AwesomeAssertions、Microsoft.AspNetCore.Mvc.Testing。

**Spec:** `docs/design.md`（搭配 `docs/features/*.feature` 與 `docs/adr/`）

## Global Constraints

- `net10.0`；`Directory.Build.props` 設 `TreatWarningsAsErrors`、`EnforceCodeStyleInBuild`、`Nullable`、`ImplicitUsings`、`InvariantGlobalization`。
- 每個 task 的 commit 前三道門檻都要過：`dotnet build` 零警告、`dotnet test` 全綠、`dotnet format --verify-no-changes`。
- Commit 訊息：英文 type 加一句中文結論（`feat`、`fix`、`refactor`、`docs`、`test`、`config`、`chore`），內文寫為什麼。結尾加 `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`。
- 只收 jpg、png；單檔上限 `10 * 1024 * 1024` bytes。
- 清單上限 `Search:MaxListResults`，預設 20。找不到回「找不到」。清單每行 `【{id}】{關鍵字以逗號連接}`，編號由小到大。
- 預設值：`Urls` = `http://0.0.0.0:5090`，`Database:ConnectionString` = `Data Source=Data/memehelper.db`，`Storage:ImageRoot` = `Data/images`。
- 祕密只放 `appsettings.Local.json`；啟動時驗證 `Telegram:BotToken` 與 `Telegram:AllowedUserIds` 非空，缺了啟動失敗並指名欄位。
- 一個 endpoint 一個檔案，放 `Features/<功能>/`。比對邏輯只存在 `Features/Search`。
- Dapper 一律 `new CommandDefinition(sql, args, cancellationToken: ct)`。已執行過的 migration 不可修改。
- 時間一律用注入的 `TimeProvider`，資料庫時間戳存 UTC ISO-8601（`ToString("O")`）。
- 測試方法用三段式中文命名 `方法_情境_預期`，非同步測試傳 `TestContext.Current.CancellationToken`。
- 文件與註解用台灣繁體中文；識別字英文；註解解釋為什麼。
- `docs/features/*.feature` 是純文件，不接執行器。

## Review Focus

1. Telegram 的 `file_id` 失效（換過 bot、Telegram 拒絕）：用 ID 重送失敗時必須退回用檔案上傳並更新 `file_id`，而非回錯。測試在 Task 10。
2. 圖片檔被手動刪掉但資料庫還有紀錄：bot 回一則錯誤文字而非崩潰，`GET /api/memes/{id}/image` 回 404。測試在 Task 8 與 Task 10。
3. 關鍵字裡含逗號：清單顯示會混淆，所以任何入口（上傳、新增關鍵字）都先以逗號切開再正規化。測試在 Task 4。
4. 改了副檔名的非圖片檔：只看副檔名會收下，等到 Telegram 送圖才失敗，所以上傳時檢查檔頭 magic bytes。測試在 Task 7。
5. 中文關鍵字走 URL 路徑移除（`DELETE .../keywords/{keyword}`）：必須正確解碼。測試在 Task 9。

---

### Task 1: 方案骨架與慣例

**Files:**
- Create: `MemeHelper.sln`、`Directory.Build.props`、`Directory.Packages.props`、`.editorconfig`
- Create: `src/MemeHelper.Api/MemeHelper.Api.csproj`、`src/MemeHelper.Api/Program.cs`
- Create: `tests/MemeHelper.Tests/MemeHelper.Tests.csproj`、`tests/MemeHelper.Tests/SmokeTests.cs`
- Delete: `src/.gitkeep`、`tests/.gitkeep`

**Interfaces:**
- Produces: `public partial class Program;`（`Program.cs` 最後一行，Task 8 的 `WebApplicationFactory<Program>` 需要）。

- [ ] **Step 1: 先把已核准的文件 commit 進去**

```bash
git add .gitignore CLAUDE.md README.md docs .claude
git commit -m "docs: 建立需求設計、Gherkin 場景、ADR 與 CLAUDE.md

第一版範圍、技術決策與沿用的慣例都在 docs/，Azure 方案保留為廢案。

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

- [ ] **Step 2: 建立 `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <LangVersion>latest</LangVersion>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: 建立 `Directory.Packages.props`**

`ManagePackageVersionsCentrally=true`、`CentralPackageTransitivePinningEnabled=true`。版本（2026-10-25 的最新穩定版，restore 若抱怨相依就調整並在 commit 內文說明）：

| 套件 | 版本 |
|---|---|
| Telegram.Bot | 22.10.3.3 |
| Dapper | 2.1.89 |
| Microsoft.Data.Sqlite | 10.0.13 |
| dbup-core | 6.1.1 |
| dbup-sqlite | 6.0.4 |
| Microsoft.Extensions.Hosting.Systemd | 10.0.13 |
| Microsoft.Extensions.Options.DataAnnotations | 10.0.13 |
| Swashbuckle.AspNetCore | 10.3.1 |
| Microsoft.NET.Test.Sdk | 18.11.0 |
| xunit.v3 | 4.0.2 |
| xunit.runner.visualstudio | 4.0.1 |
| coverlet.collector | 10.1.0 |
| AutoFixture | 4.18.1 |
| AutoFixture.Xunit3 | 4.19.0 |
| AutoFixture.AutoNSubstitute | 4.18.1 |
| NSubstitute | 6.2.0 |
| AwesomeAssertions | 9.6.0 |
| Microsoft.Extensions.TimeProvider.Testing | 10.11.0 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.13 |

- [ ] **Step 4: 複製 `.editorconfig`**

從同機既有 .NET 服務的 `.editorconfig` 整份複製。若拿不到，`dotnet new editorconfig` 後套用檔頭註解列的五項修改（using 不分組、`insert_final_newline = true`、私有欄位 `camelCase` 無底線、偏好 `var`、已遵守的規則升為 warning）。

- [ ] **Step 5: 建立 API 專案**

```bash
dotnet new sln -n MemeHelper
dotnet new web -n MemeHelper.Api -o src/MemeHelper.Api
dotnet sln add src/MemeHelper.Api
```

`MemeHelper.Api.csproj`（`Microsoft.NET.Sdk.Web`）加：`<InternalsVisibleTo Include="MemeHelper.Tests" />`、`<EmbeddedResource Include="Migrations\*.sql" />`、`<NoWarn>$(NoWarn);CS1591</NoWarn>`，以及 PackageReference：Telegram.Bot、Dapper、Microsoft.Data.Sqlite、dbup-core、dbup-sqlite、Microsoft.Extensions.Hosting.Systemd、Microsoft.Extensions.Options.DataAnnotations、Swashbuckle.AspNetCore。

`Program.cs` 暫時只有：

```csharp
Directory.SetCurrentDirectory(AppContext.BaseDirectory);
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/health", () => Results.Ok());
app.Run();

public partial class Program;
```

刪掉範本產生的 `appsettings.Development.json` 以外不需要的東西；`Properties/launchSettings.json` 保留但 `applicationUrl` 改 `http://localhost:5090`。

- [ ] **Step 6: 建立測試專案**

```bash
dotnet new xunit -n MemeHelper.Tests -o tests/MemeHelper.Tests
dotnet sln add tests/MemeHelper.Tests
dotnet add tests/MemeHelper.Tests reference src/MemeHelper.Api
```

csproj：`<OutputType>Exe</OutputType>`、`<IsPackable>false</IsPackable>`、`<IsTestProject>true</IsTestProject>`，PackageReference：Microsoft.NET.Test.Sdk、xunit.v3、xunit.runner.visualstudio、coverlet.collector、AutoFixture、AutoFixture.Xunit3、AutoFixture.AutoNSubstitute、NSubstitute、AwesomeAssertions、Microsoft.Extensions.TimeProvider.Testing、Microsoft.AspNetCore.Mvc.Testing。範本若產生 `xunit`（v2）的參考，移除。

`SmokeTests.cs`：

```csharp
[Fact]
public void 測試專案_建置後_應能執行() => true.Should().BeTrue();
```

- [ ] **Step 7: 三道門檻**

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
```

Expected：build 零警告、1 test passed、format 無變更。有警告就修到零，不要加 NoWarn 擋。

- [ ] **Step 8: Commit**

```bash
git rm src/.gitkeep tests/.gitkeep
git add -A
git commit -m "chore: 建立方案骨架與建置慣例

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: 設定與 Program 組態

**Files:**
- Create: `src/MemeHelper.Api/Configuration/TelegramOptions.cs`、`DatabaseOptions.cs`、`StorageOptions.cs`、`SearchOptions.cs`
- Create: `src/MemeHelper.Api/appsettings.Local.json.example`
- Modify: `src/MemeHelper.Api/appsettings.json`、`src/MemeHelper.Api/Program.cs`
- Test: `tests/MemeHelper.Tests/Configuration/OptionsValidationTests.cs`

**Interfaces:**
- Produces:

```csharp
public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";
    [Required] public string BotToken { get; set; } = "";
    [MinLength(1)] public long[] AllowedUserIds { get; set; } = [];
}
public sealed class DatabaseOptions { public const string SectionName = "Database"; public string ConnectionString { get; set; } = "Data Source=Data/memehelper.db"; }
public sealed class StorageOptions { public const string SectionName = "Storage"; public string ImageRoot { get; set; } = "Data/images"; }
public sealed class SearchOptions { public const string SectionName = "Search"; public int MaxListResults { get; set; } = 20; }
```

- `Program.cs` 提供 `internal static IServiceCollection AddMemeHelperOptions(this IServiceCollection services, IConfiguration configuration)`（放 `Configuration/OptionsRegistration.cs`），四個 options 都 `Bind` 對應 section，`TelegramOptions` 加 `ValidateDataAnnotations().ValidateOnStart()`。

- [ ] **Step 1: 寫失敗的測試**

```csharp
[Fact]
public void AddMemeHelperOptions_缺少BotToken_解析時應拋出OptionsValidationException()
// 用 ConfigurationBuilder.AddInMemoryCollection 只給 Telegram:AllowedUserIds:0=1，
// services.AddMemeHelperOptions(config)，BuildServiceProvider()，
// 取 IOptions<TelegramOptions>.Value 應 Throw<OptionsValidationException>() 且訊息含 "BotToken"

[Fact]
public void AddMemeHelperOptions_缺少AllowedUserIds_解析時應拋出OptionsValidationException()

[Fact]
public void AddMemeHelperOptions_未設定Search區段_MaxListResults應為20()
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test --filter "FullyQualifiedName~OptionsValidationTests"`
Expected: 編譯失敗，找不到 `AddMemeHelperOptions`。

- [ ] **Step 3: 實作四個 options 類別與 `AddMemeHelperOptions`**

- [ ] **Step 4: `appsettings.json` 與範本**

`appsettings.json`：

```json
{
  "Urls": "http://0.0.0.0:5090",
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "Database": { "ConnectionString": "Data Source=Data/memehelper.db" },
  "Storage": { "ImageRoot": "Data/images" },
  "Search": { "MaxListResults": 20 },
  "Telegram": { "BotToken": "", "AllowedUserIds": [] }
}
```

`appsettings.Local.json.example`：

```json
{
  "Telegram": {
    "BotToken": "1234567:AAxxxx",
    "AllowedUserIds": [ 123456789 ]
  }
}
```

`Program.cs`：`builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);`、`builder.Host.UseSystemd();`、`builder.Services.AddMemeHelperOptions(builder.Configuration);`、`builder.Services.AddSingleton(TimeProvider.System);`。

- [ ] **Step 5: 跑測試確認通過，三道門檻**

Run: `dotnet test` → 全綠；`dotnet build`、`dotnet format --verify-no-changes`。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: 設定類別與啟動時驗證

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: 資料庫連線與 schema

**Files:**
- Create: `src/MemeHelper.Api/Database/IDbConnectionFactory.cs`、`SqliteConnectionFactory.cs`、`DatabaseMigrator.cs`
- Create: `src/MemeHelper.Api/Migrations/20261025_001_CreateMemes.sql`
- Modify: `src/MemeHelper.Api/Program.cs`
- Create: `tests/MemeHelper.Tests/Support/TempDatabase.cs`
- Test: `tests/MemeHelper.Tests/Database/DatabaseMigratorTests.cs`、`SqliteConnectionFactoryTests.cs`

**Interfaces:**
- Produces:

```csharp
public interface IDbConnectionFactory { Task<DbConnection> OpenAsync(CancellationToken ct); }
public sealed class SqliteConnectionFactory(IOptions<DatabaseOptions> options) : IDbConnectionFactory
public sealed class DatabaseMigrator(IOptions<DatabaseOptions> options, ILogger<DatabaseMigrator> logger)
{
    public void Migrate();   // 失敗拋 InvalidOperationException，訊息含 DbUp 的錯誤
}
```

- `TempDatabase : IDisposable`，屬性 `ConnectionString`、`FilePath`，Dispose 時 `SqliteConnection.ClearAllPools()` 後刪 `.db`、`-wal`、`-shm`。

- [ ] **Step 1: 寫失敗的測試**

```csharp
// DatabaseMigratorTests
[Fact] public void Migrate_對空資料庫執行_應建立memes與meme_keywords表()
// 用 Dapper 查 sqlite_master，name IN ('memes','meme_keywords') 的 COUNT 應為 2
[Fact] public void Migrate_連續執行兩次_不應拋出例外()
[Fact] public void Migrate_連線字串指向不存在的目錄_應先建立目錄()

// SqliteConnectionFactoryTests
[Fact] public async Task OpenAsync_開啟連線_journal_mode應為wal()
// PRAGMA journal_mode 回傳 "wal"
[Fact] public async Task OpenAsync_開啟連線_foreign_keys應為開啟()
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test --filter "FullyQualifiedName~Database"`
Expected: 編譯失敗。

- [ ] **Step 3: 寫 migration SQL**

```sql
CREATE TABLE memes (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    extension        TEXT    NOT NULL,
    is_enabled       INTEGER NOT NULL DEFAULT 1,
    telegram_file_id TEXT,
    created_at       TEXT    NOT NULL,
    updated_at       TEXT    NOT NULL
);

CREATE TABLE meme_keywords (
    meme_id INTEGER NOT NULL REFERENCES memes(id) ON DELETE CASCADE,
    keyword TEXT    NOT NULL,
    PRIMARY KEY (meme_id, keyword)
);
```

- [ ] **Step 4: 實作 `SqliteConnectionFactory`（PRAGMA `journal_mode=WAL; busy_timeout=5000; foreign_keys=ON`，PRAGMA 失敗要 Dispose 連線再拋）與 `DatabaseMigrator`（DbUp `DeployChanges.To.SqliteDatabase(cs).WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrator).Assembly).WithTransactionPerScript().LogToNowhere()`；先從連線字串取出 `Data Source=` 的路徑並 `Directory.CreateDirectory`）**

- [ ] **Step 5: `Program.cs` 註冊 `IDbConnectionFactory`、`DatabaseMigrator` 為 singleton，`var app = builder.Build();` 之後、`app.Run()` 之前呼叫 `app.Services.GetRequiredService<DatabaseMigrator>().Migrate();`**

Migration 不做成 hosted service，這樣 Task 8 的測試可以用 `RemoveAll<IHostedService>()` 一次拿掉 polling，不會順便拿掉 migration。

- [ ] **Step 6: 跑測試確認通過，三道門檻**

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: SQLite 連線與 DbUp schema

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: 關鍵字正規化

**Files:**
- Create: `src/MemeHelper.Api/Features/Memes/KeywordNormalizer.cs`
- Test: `tests/MemeHelper.Tests/Features/Memes/KeywordNormalizerTests.cs`

**Interfaces:**
- Produces: `public static class KeywordNormalizer { public static IReadOnlyList<string> Normalize(IEnumerable<string> raw); }`
  規則依序：每個輸入先以 `,` 切開；去頭尾空白；空字串丟掉；以 `StringComparer.OrdinalIgnoreCase` 去重並保留第一次出現的寫法與順序。

- [ ] **Step 1: 寫失敗的測試**

```csharp
[Fact] public void Normalize_輸入含頭尾空白_應去除空白()           // [" 下班 "] → ["下班"]
[Fact] public void Normalize_輸入空字串與純空白_應忽略()           // ["", "  ", "六點"] → ["六點"]
[Fact] public void Normalize_輸入大小寫不同的重複英文_應只保留第一個() // ["Hello","hello"] → ["Hello"]
[Fact] public void Normalize_輸入中文重複_應只保留一個()           // ["下班","下班"] → ["下班"]
[Fact] public void Normalize_單一字串含逗號_應切成多個關鍵字()     // ["六點,下班"] → ["六點","下班"]
[Fact] public void Normalize_混合來源_應保留首次出現順序()         // [" 下班 ,,下班,Hello,hello"] → ["下班","Hello"]（manage-memes.feature 的正規化場景）
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test --filter "FullyQualifiedName~KeywordNormalizerTests"`

- [ ] **Step 3: 實作 `Normalize`**

- [ ] **Step 4: 跑測試確認通過，三道門檻**

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: 關鍵字正規化規則

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: Meme 模型與 Dapper repository

**Files:**
- Create: `src/MemeHelper.Api/Features/Memes/Meme.cs`
- Create: `src/MemeHelper.Api/Database/IMemeRepository.cs`、`SqliteMemeRepository.cs`
- Modify: `src/MemeHelper.Api/Program.cs`
- Test: `tests/MemeHelper.Tests/Database/SqliteMemeRepositoryTests.cs`

**Interfaces:**
- Consumes: `IDbConnectionFactory`、`DatabaseMigrator`（Task 3）、`TempDatabase`。
- Produces:

```csharp
public sealed record Meme(
    int Id, string Extension, bool IsEnabled, string? TelegramFileId,
    IReadOnlyList<string> Keywords, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public enum RemoveKeywordResult { Removed, MemeNotFound, KeywordNotFound }

public interface IMemeRepository
{
    Task<int> AddAsync(string extension, IReadOnlyList<string> keywords, CancellationToken ct);
    Task<Meme?> GetAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<Meme>> ListAsync(bool includeDisabled, CancellationToken ct);   // id 由小到大
    Task<bool> AddKeywordsAsync(int id, IReadOnlyList<string> keywords, CancellationToken ct);   // false = meme 不存在
    Task<RemoveKeywordResult> RemoveKeywordAsync(int id, string keyword, CancellationToken ct);
    Task<bool> SetEnabledAsync(int id, bool isEnabled, CancellationToken ct);
    Task<bool> SetTelegramFileIdAsync(int id, string? fileId, CancellationToken ct);
    Task<Meme?> DeleteAsync(int id, CancellationToken ct);   // 回傳被刪的紀錄，呼叫端用它刪檔
}
public sealed class SqliteMemeRepository(IDbConnectionFactory connections, TimeProvider clock) : IMemeRepository
```

  行為細節：關鍵字讀出時依 `meme_keywords.rowid` 排序（插入順序）。`AddKeywordsAsync` 先讀既有關鍵字，用 `OrdinalIgnoreCase` 濾掉已存在的再插入，更新 `updated_at`。`RemoveKeywordAsync` 用 `keyword = @keyword COLLATE NOCASE` 比對。`created_at`、`updated_at` 存 `clock.GetUtcNow().ToString("O")`，讀回用 `DateTimeOffset.Parse(..., CultureInfo.InvariantCulture)`。

- [ ] **Step 1: 寫失敗的測試**

每個測試自建 `TempDatabase`、跑 `DatabaseMigrator.Migrate()`、用 `FakeTimeProvider`（固定 `2026-10-25T00:00:00Z`）建 repository。

```csharp
[Fact] public async Task AddAsync_新增一張圖_應回傳遞增編號且GetAsync讀得到相同內容()
// extension "png"、keywords ["六點","下班"]；Get 後 IsEnabled true、TelegramFileId null、Keywords 順序相同、CreatedAt 等於 fake 時間
[Fact] public async Task ListAsync_includeDisabled為false_不應包含停用的圖()
[Fact] public async Task ListAsync_新增三張_應依編號由小到大()
[Fact] public async Task AddKeywordsAsync_新增已存在但大小寫不同的關鍵字_不應重複()  // 既有 ["Hello"]，新增 ["hello","新詞"] → ["Hello","新詞"]
[Fact] public async Task AddKeywordsAsync_編號不存在_應回傳false()
[Fact] public async Task RemoveKeywordAsync_移除存在的關鍵字_應回傳Removed且讀回不含該字()
[Fact] public async Task RemoveKeywordAsync_關鍵字不存在_應回傳KeywordNotFound()
[Fact] public async Task RemoveKeywordAsync_編號不存在_應回傳MemeNotFound()
[Fact] public async Task SetEnabledAsync_停用後_GetAsync的IsEnabled應為false()
[Fact] public async Task SetTelegramFileIdAsync_設定後_GetAsync應讀到相同file_id()
[Fact] public async Task DeleteAsync_刪除存在的圖_應回傳該紀錄且關鍵字一併刪除()  // 查 meme_keywords COUNT 為 0
[Fact] public async Task DeleteAsync_編號不存在_應回傳null()
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test --filter "FullyQualifiedName~SqliteMemeRepositoryTests"`

- [ ] **Step 3: 實作 `Meme`、`IMemeRepository`、`SqliteMemeRepository`；`Program.cs` 註冊 `IMemeRepository` 為 singleton**

- [ ] **Step 4: 跑測試確認通過，三道門檻**

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: Meme 模型與 SQLite repository

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: 搜尋比對

**Files:**
- Create: `src/MemeHelper.Api/Features/Search/KeywordMatcher.cs`、`MemeSearch.cs`
- Modify: `src/MemeHelper.Api/Program.cs`
- Test: `tests/MemeHelper.Tests/Features/Search/KeywordMatcherTests.cs`、`MemeSearchTests.cs`

**Interfaces:**
- Consumes: `IMemeRepository`、`Meme`（Task 5）。
- Produces:

```csharp
public static class KeywordMatcher
{
    public static IReadOnlyList<string> ParseTerms(string query);   // 以空白切開、去空字串
    public static bool Matches(IReadOnlyList<string> keywords, IReadOnlyList<string> terms);
    // 每個 term 都要有任一 keyword Contains(term, StringComparison.OrdinalIgnoreCase)；terms 為空回 true
}
public sealed class MemeSearch(IMemeRepository repository)
{
    public Task<IReadOnlyList<Meme>> FindAsync(string query, bool includeDisabled, CancellationToken ct);
    // repository.ListAsync(includeDisabled) 後用 KeywordMatcher 過濾，保持 id 順序
}
```

- [ ] **Step 1: 寫失敗的測試**

```csharp
// KeywordMatcherTests
[Fact] public void ParseTerms_輸入多個空白分隔的詞_應切成多個詞()          // "六點  下班" → ["六點","下班"]
[Fact] public void ParseTerms_輸入純空白_應回傳空集合()
[Fact] public void Matches_單一詞為關鍵字的子字串_應回傳true()              // ["蟹老闆"], ["老闆"]
[Fact] public void Matches_英文大小寫不同_應回傳true()                      // ["Hello"], ["hello"]
[Fact] public void Matches_多個詞全部命中_應回傳true()                      // ["六點","下班"], ["六點","下班"]
[Fact] public void Matches_多個詞只命中一個_應回傳false()                   // ["六點","太棒了"], ["六點","下班"]
[Fact] public void Matches_詞不在任何關鍵字內_應回傳false()

// MemeSearchTests（NSubstitute 的 IMemeRepository 回固定清單）
[Fact] public async Task FindAsync_查詢命中兩張_應只回傳命中的兩張並保持順序()
[Fact] public async Task FindAsync_查詢為空字串_應回傳repository的全部結果()
[Fact] public async Task FindAsync_includeDisabled為false_應以false呼叫repository()
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test --filter "FullyQualifiedName~Search"`

- [ ] **Step 3: 實作兩個類別；`Program.cs` 註冊 `MemeSearch` 為 singleton**

- [ ] **Step 4: 跑測試確認通過，三道門檻**

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: 關鍵字比對與搜尋

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: 圖片檔規則與檔案儲存

**Files:**
- Create: `src/MemeHelper.Api/Features/Memes/ImageFileRules.cs`、`IImageStore.cs`、`FileSystemImageStore.cs`
- Modify: `src/MemeHelper.Api/Program.cs`
- Create: `tests/MemeHelper.Tests/Support/TempDirectory.cs`、`tests/MemeHelper.Tests/Support/SampleImages.cs`
- Test: `tests/MemeHelper.Tests/Features/Memes/ImageFileRulesTests.cs`、`FileSystemImageStoreTests.cs`

**Interfaces:**
- Produces:

```csharp
public static class ImageFileRules
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public static bool TryGetExtension(string fileName, out string extension);
    // ".jpg"/".jpeg" → "jpg"，".png" → "png"，不分大小寫；其他回 false
    public static bool HasValidSignature(ReadOnlySpan<byte> head, string extension);
    // png：89 50 4E 47 0D 0A 1A 0A；jpg：FF D8 FF
    public static string GetContentType(string extension);   // "image/jpeg" | "image/png"
}
public interface IImageStore
{
    Task SaveAsync(int id, string extension, Stream content, CancellationToken ct);
    Stream? OpenRead(int id, string extension);   // 檔案不存在回 null
    bool Delete(int id, string extension);        // 檔案不存在回 false
}
public sealed class FileSystemImageStore(IOptions<StorageOptions> options) : IImageStore
// 路徑 Path.Combine(ImageRoot, $"{id}.{extension}")；Save 前 Directory.CreateDirectory(ImageRoot)
```

- `TempDirectory : IDisposable`，屬性 `Path`，Dispose 遞迴刪除。
- `SampleImages`：`static byte[] Png()`（8 bytes 檔頭加任意 16 bytes）、`static byte[] Jpg()`（FF D8 FF E0 加任意 bytes）、`static byte[] Gif()`（`GIF89a`）。

- [ ] **Step 1: 寫失敗的測試**

```csharp
// ImageFileRulesTests
[Theory] [InlineData("a.jpg","jpg")] [InlineData("a.JPEG","jpg")] [InlineData("a.PNG","png")]
public void TryGetExtension_輸入支援的副檔名_應回傳正規化副檔名(string fileName, string expected)
[Theory] [InlineData("a.gif")] [InlineData("a")] [InlineData("a.webp")]
public void TryGetExtension_輸入不支援的副檔名_應回傳false(string fileName)
[Fact] public void HasValidSignature_png檔頭配png_應回傳true()
[Fact] public void HasValidSignature_gif檔頭配png_應回傳false()
[Fact] public void HasValidSignature_jpg檔頭配jpg_應回傳true()
[Fact] public void GetContentType_輸入jpg_應回傳image_jpeg()

// FileSystemImageStoreTests（TempDirectory 當 ImageRoot）
[Fact] public async Task SaveAsync_目錄不存在_應建立目錄並寫入檔案()   // 檔名 "7.png" 存在且內容相同
[Fact] public async Task OpenRead_檔案存在_應回傳可讀取的串流()
[Fact] public void OpenRead_檔案不存在_應回傳null()
[Fact] public async Task Delete_檔案存在_應刪除並回傳true()
[Fact] public void Delete_檔案不存在_應回傳false()
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test --filter "FullyQualifiedName~ImageFile|FullyQualifiedName~FileSystemImageStore"`

- [ ] **Step 3: 實作三個型別；`Program.cs` 註冊 `IImageStore` 為 singleton**

- [ ] **Step 4: 跑測試確認通過，三道門檻**

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: 圖片檔規則與本機檔案儲存

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 8: 管理 API（上傳、列表、取圖）與 Swagger

**Files:**
- Create: `src/MemeHelper.Api/Features/Memes/MemeEndpoints.cs`、`UploadMeme.cs`、`ListMemes.cs`、`GetMeme.cs`、`GetMemeImage.cs`
- Modify: `src/MemeHelper.Api/Program.cs`
- Create: `tests/MemeHelper.Tests/Support/ApiFactory.cs`、`tests/MemeHelper.Tests/Support/MemeApiClient.cs`
- Test: `tests/MemeHelper.Tests/Features/Memes/UploadMemeTests.cs`、`ListMemesTests.cs`、`GetMemeImageTests.cs`

**Interfaces:**
- Consumes: `IMemeRepository`、`MemeSearch`、`IImageStore`、`ImageFileRules`、`KeywordNormalizer`。
- Produces:

```csharp
internal static class MemeEndpoints
{
    public static IEndpointRouteBuilder MapMemeEndpoints(this IEndpointRouteBuilder app);
    // var group = app.MapGroup("/api/memes").WithTags("Memes"); 依序呼叫每個檔案的 Map(group)
}
internal static class UploadMeme { public static void Map(RouteGroupBuilder group); }   // 其餘檔案同形
```

  Endpoint 行為（回應本體一律是 `Meme` record）：
  - `POST /api/memes`：multipart，欄位 `file`（`IFormFile`）與 `keywords`（`[FromForm] string`，逗號分隔）。`.DisableAntiforgery()`。驗證順序：副檔名、大小 ≤ `MaxBytes`、檔頭 signature、正規化後關鍵字至少一個；任一失敗回 `Results.ValidationProblem`（400），錯誤 key 為 `file` 或 `keywords`。成功：`AddAsync` 取得 id → `SaveAsync` → 回 `Results.Created($"/api/memes/{id}", meme)`。`SaveAsync` 拋例外時 `DeleteAsync(id)` 後重拋。
  - `GET /api/memes?keyword=&includeDisabled=true`：`MemeSearch.FindAsync(keyword ?? "", includeDisabled)`。
  - `GET /api/memes/{id:int}`：200 或 404。
  - `GET /api/memes/{id:int}/image`：`Results.File(stream, ImageFileRules.GetContentType(ext))`；紀錄或檔案不存在皆 404。
- `ApiFactory : WebApplicationFactory<Program>`：建 `TempDirectory`；`ConfigureWebHost` 用 `UseSetting` 設 `Database:ConnectionString` = `Data Source=<temp>/test.db`、`Storage:ImageRoot` = `<temp>/images`、`Telegram:BotToken` = `123456789:AAFakeTokenForTests-0123456789abcdefg`、`Telegram:AllowedUserIds:0` = `1`；`ConfigureTestServices(s => s.RemoveAll<IHostedService>())`；Dispose 時 `SqliteConnection.ClearAllPools()` 再刪 temp。
- `MemeApiClient(HttpClient http)`：`Task<HttpResponseMessage> UploadAsync(byte[] content, string fileName, string keywords, CancellationToken ct)`（組 `MultipartFormDataContent`）、`Task<Meme> UploadOkAsync(...)`（斷言 201 後反序列化）。

- [ ] **Step 1: 寫失敗的測試**

```csharp
// UploadMemeTests
[Fact] public async Task Upload_上傳png並設定關鍵字_應回傳201且圖片檔存在()
// 回應 Location 為 /api/memes/{id}，Keywords ["太棒了","章魚哥"]，IsEnabled true，<ImageRoot>/{id}.png 存在
[Fact] public async Task Upload_關鍵字含空白與重複_應正規化()          // " 下班 ,,下班,Hello,hello" → ["下班","Hello"]
[Fact] public async Task Upload_上傳gif_應回傳400()
[Fact] public async Task Upload_檔案超過10MB_應回傳400()               // 10*1024*1024+1 bytes 的 png 檔頭
[Fact] public async Task Upload_副檔名為png但檔頭不是png_應回傳400()
[Fact] public async Task Upload_沒有有效關鍵字_應回傳400()             // keywords " , "

// ListMemesTests
[Fact] public async Task List_預設_應包含停用的圖()     // 上傳兩張、停用一張（直接用 IMemeRepository 從 factory.Services 取）
[Fact] public async Task List_帶keyword_應只回傳命中的圖()
[Fact] public async Task Get_編號不存在_應回傳404()

// GetMemeImageTests
[Fact] public async Task GetImage_圖片存在_應回傳image_png與原始內容()
[Fact] public async Task GetImage_紀錄存在但檔案被刪_應回傳404()
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test --filter "FullyQualifiedName~Features.Memes"`

- [ ] **Step 3: 實作四個 endpoint 與 `MemeEndpoints`**

- [ ] **Step 4: `Program.cs`：`AddProblemDetails()`、`AddEndpointsApiExplorer()`、`AddSwaggerGen()`；`app.UseExceptionHandler()`、`app.UseSwagger()`、`app.UseSwaggerUI()`、`app.MapMemeEndpoints()`；移除 Task 1 的 `/health`**

Swagger UI 不分環境都開，因為服務只在區網。

- [ ] **Step 5: 跑測試確認通過，三道門檻**

- [ ] **Step 6: 手動確認 Swagger 能上傳**

Run: `dotnet run --project src/MemeHelper.Api`（先在 `src/MemeHelper.Api/appsettings.Local.json` 填任意格式正確的 token 與 user id；此時還沒有 polling，不會連 Telegram）。開 `http://localhost:5090/swagger`，用 `POST /api/memes` 上傳一張 png。
Expected: 201，`Data/images/1.png` 出現。

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: 上傳、列表、取圖 API 與 Swagger UI

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 9: 管理 API（關鍵字、停用、啟用、刪除）

**Files:**
- Create: `src/MemeHelper.Api/Features/Memes/AddKeywords.cs`、`RemoveKeyword.cs`、`DisableMeme.cs`、`EnableMeme.cs`、`DeleteMeme.cs`
- Modify: `src/MemeHelper.Api/Features/Memes/MemeEndpoints.cs`
- Test: `tests/MemeHelper.Tests/Features/Memes/KeywordEndpointsTests.cs`、`EnableDisableDeleteTests.cs`

**Interfaces:**
- Consumes: Task 8 的 `MemeEndpoints`、`ApiFactory`、`MemeApiClient`。
- Produces（每個檔案 `public static void Map(RouteGroupBuilder group)`）：
  - `POST /api/memes/{id:int}/keywords`，body `string[]`，先 `KeywordNormalizer.Normalize`，空集合 400；`AddKeywordsAsync` false → 404；成功回 200 與更新後的 `Meme`。
  - `DELETE /api/memes/{id:int}/keywords/{keyword}`：`RemoveKeywordResult.Removed` → 204，其餘 404。
  - `POST /api/memes/{id:int}/disable`、`POST /api/memes/{id:int}/enable`：`SetEnabledAsync` false → 404；成功 200 與 `Meme`。
  - `DELETE /api/memes/{id:int}`：`DeleteAsync` null → 404；成功後 `images.Delete(...)`，回 204。

- [ ] **Step 1: 寫失敗的測試**

```csharp
// KeywordEndpointsTests
[Fact] public async Task AddKeywords_新增一個關鍵字_應回傳200且關鍵字附加在最後()
[Fact] public async Task AddKeywords_新增已存在的關鍵字_應回傳200且不重複()
[Fact] public async Task AddKeywords_body全是空字串_應回傳400()
[Fact] public async Task RemoveKeyword_移除中文關鍵字_應回傳204且清單不含該字()   // 路徑用 Uri.EscapeDataString("下班")
[Fact] public async Task RemoveKeyword_關鍵字不存在_應回傳404()
[Fact] public async Task AddKeywords_編號不存在_應回傳404()

// EnableDisableDeleteTests
[Fact] public async Task Disable_停用後_列表中該圖IsEnabled應為false且搜尋不到()   // GET /api/memes?keyword=六點&includeDisabled=false 不含
[Fact] public async Task Enable_重新啟用_搜尋應找得到()
[Fact] public async Task Delete_刪除存在的圖_應回傳204且圖片檔與紀錄都消失()
[Fact] public async Task Disable_編號不存在_應回傳404()
```

- [ ] **Step 2: 跑測試確認失敗**

- [ ] **Step 3: 實作五個 endpoint，加進 `MemeEndpoints`**

- [ ] **Step 4: 跑測試確認通過，三道門檻**

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: 關鍵字編輯、停用啟用與刪除 API

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 10: Bot 回覆邏輯

**Files:**
- Create: `src/MemeHelper.Api/Telegram/IBotMessenger.cs`、`ReplyFormatter.cs`、`MemeBotHandler.cs`
- Test: `tests/MemeHelper.Tests/Telegram/ReplyFormatterTests.cs`、`MemeBotHandlerTests.cs`

**Interfaces:**
- Consumes: `IMemeRepository`、`MemeSearch`、`IImageStore`、`TelegramOptions`、`SearchOptions`。
- Produces:

```csharp
public interface IBotMessenger
{
    Task SendTextAsync(long chatId, string text, CancellationToken ct);
    Task<string> SendPhotoFileAsync(long chatId, Stream content, string fileName, CancellationToken ct);   // 回傳 file_id
    Task SendPhotoByFileIdAsync(long chatId, string fileId, CancellationToken ct);
}
public static class ReplyFormatter
{
    public const string NotFound = "找不到";
    public const string Help = "傳關鍵字找梗圖，多個詞用空白隔開；命中多張時會列清單，再傳編號取圖。";
    public const string ImageMissing = "圖片檔不見了，請到後台檢查編號 {0}";
    public const string SendFailed = "送圖失敗，請稍後再試";
    public static string FormatList(IReadOnlyList<Meme> memes, int maxListResults);
    // 每行 $"【{m.Id}】{string.Join(",", m.Keywords)}"，以 "\n" 連接；超過 max 時只取前 max 行，
    // 最後加一行 $"還有 {memes.Count - max} 張沒列出，請再加關鍵字縮小範圍"
}
public sealed class MemeBotHandler(
    IMemeRepository repository, MemeSearch search, IImageStore images, IBotMessenger messenger,
    IOptions<TelegramOptions> telegram, IOptions<SearchOptions> searchOptions, ILogger<MemeBotHandler> logger)
{
    public Task HandleTextAsync(long userId, long chatId, string text, CancellationToken ct);
}
```

  `HandleTextAsync` 流程：
  1. `userId` 不在 `AllowedUserIds` → 什麼都不做。
  2. `text.Trim()` 為 `/start` 或 `/help` → `SendTextAsync(Help)`。
  3. 純數字（`int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, ...)`）→ `GetAsync`；存在且 `IsEnabled` → `SendMemeAsync` 後結束；否則落到 4。
  4. `KeywordMatcher.ParseTerms(text)` 為空 → `NotFound`。`search.FindAsync(text, includeDisabled: false)`：0 張 → `NotFound`；1 張 → `SendMemeAsync`；其餘 → `SendTextAsync(FormatList(results, MaxListResults))`。

  `SendMemeAsync(chatId, meme, ct)`（private）：
  1. `TelegramFileId` 非 null → `SendPhotoByFileIdAsync`；成功即結束；拋例外則 `LogWarning` 後往下。
  2. `images.OpenRead` 為 null → `SendTextAsync(string.Format(ImageMissing, id))`，結束。
  3. `SendPhotoFileAsync(chatId, stream, $"{id}.{ext}")` → `SetTelegramFileIdAsync(id, fileId)`；拋例外則 `LogError` 並 `SendTextAsync(SendFailed)`。

- [ ] **Step 1: 寫失敗的測試**

```csharp
// ReplyFormatterTests
[Fact] public void FormatList_輸入兩張_應輸出兩行編號加關鍵字()
// 【1】六點,下班,蟹老闆\n【2】六點,太棒了,章魚哥
[Fact] public void FormatList_輸入25張上限20_應只有20行並附提示()   // 共 21 行，最後一行 "還有 5 張沒列出，請再加關鍵字縮小範圍"

// MemeBotHandlerTests（IMemeRepository、IImageStore、IBotMessenger 用 NSubstitute；MemeSearch 用真的，包住 substitute repository）
// 共用資料：背景表的五張圖（telegram-search.feature）
[Fact] public async Task HandleTextAsync_非白名單使用者_不應呼叫messenger()
[Fact] public async Task HandleTextAsync_傳送start_應回覆Help()
[Fact] public async Task HandleTextAsync_關鍵字命中一張_應用檔案送圖並儲存file_id()
// OpenRead 回 MemoryStream，SendPhotoFileAsync 回 "FILE_ID_1"，驗證 SetTelegramFileIdAsync(3, "FILE_ID_1")
[Fact] public async Task HandleTextAsync_關鍵字命中多張_應回覆清單()
[Fact] public async Task HandleTextAsync_多詞查詢_每個詞都要命中()        // "六點 下班" → 送編號 1
[Fact] public async Task HandleTextAsync_停用的圖_不應出現在結果()
[Fact] public async Task HandleTextAsync_找不到_應回覆找不到()
[Fact] public async Task HandleTextAsync_傳送編號_應送出該編號的圖()
[Fact] public async Task HandleTextAsync_純數字但查無編號_應改當關鍵字搜尋()   // "2077" 命中關鍵字 2077 的圖
[Fact] public async Task HandleTextAsync_已有file_id_應用file_id重送且不開檔()  // SendPhotoByFileIdAsync 被呼叫，OpenRead 未被呼叫
[Fact] public async Task HandleTextAsync_file_id重送失敗_應改用檔案上傳並更新file_id()  // Review Focus 1
[Fact] public async Task HandleTextAsync_圖片檔不存在_應回覆ImageMissing()   // Review Focus 2
[Fact] public async Task HandleTextAsync_純空白_應回覆找不到()
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test --filter "FullyQualifiedName~Telegram"`

- [ ] **Step 3: 實作三個型別**

- [ ] **Step 4: 跑測試確認通過，三道門檻**

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: Telegram 查詢回覆邏輯

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 11: Telegram 接線與 long polling

**Files:**
- Create: `src/MemeHelper.Api/Telegram/TelegramBotMessenger.cs`、`TelegramPollingService.cs`
- Modify: `src/MemeHelper.Api/Program.cs`

**Interfaces:**
- Consumes: `IBotMessenger`、`MemeBotHandler`（Task 10）。
- Produces:

```csharp
public sealed class TelegramBotMessenger(ITelegramBotClient bot) : IBotMessenger
// SendTextAsync → bot.SendMessage(chatId, text, cancellationToken: ct)
// SendPhotoFileAsync → var m = await bot.SendPhoto(chatId, InputFile.FromStream(content, fileName), cancellationToken: ct); return m.Photo![^1].FileId;
// SendPhotoByFileIdAsync → bot.SendPhoto(chatId, InputFile.FromFileId(fileId), cancellationToken: ct)
public sealed class TelegramPollingService(ITelegramBotClient bot, MemeBotHandler handler, ILogger<TelegramPollingService> logger) : BackgroundService
// ExecuteAsync：bot.ReceiveAsync(HandleUpdate, HandleError, new ReceiverOptions { AllowedUpdates = [UpdateType.Message], DropPendingUpdates = true }, stoppingToken)
// HandleUpdate：update.Message is { Text: { } text, From: { } from, Chat: { } chat } 才呼叫 handler.HandleTextAsync(from.Id, chat.Id, text, ct)，整段 try/catch LogError，不讓例外中斷 polling
// HandleError：LogError 後 return Task.CompletedTask（Telegram.Bot 會自行重試）
```

  `Program.cs`：`AddSingleton<ITelegramBotClient>(sp => new TelegramBotClient(sp.GetRequiredService<IOptions<TelegramOptions>>().Value.BotToken))`、`AddSingleton<IBotMessenger, TelegramBotMessenger>()`、`AddSingleton<MemeBotHandler>()`、`AddHostedService<TelegramPollingService>()`。

  `DropPendingUpdates = true` 的理由寫在註解：服務停機期間累積的查詢都已過時，重啟後不該補回。

- [ ] **Step 1: 實作兩個類別與 DI**

這兩個類別是對 Telegram.Bot 的薄包裝，`ITelegramBotClient` 的方法都是 extension method，無法用替身驗證，所以沒有單元測試；正確性靠下一步的真機驗證。

- [ ] **Step 2: 三道門檻**

Run: `dotnet build`、`dotnet test`、`dotnet format --verify-no-changes`。Task 8 的 `ApiFactory` 移除了所有 hosted service，所以整合測試不會真的去 polling。

- [ ] **Step 3: 真機驗證（需要使用者提供 bot）**

使用者用 BotFather 建一個新 bot，把 token 與自己的 user id 填進 `src/MemeHelper.Api/appsettings.Local.json`。`dotnet run --project src/MemeHelper.Api`，用 Swagger 上傳兩張圖（關鍵字各含「六點」），然後在 Telegram 對 bot：
- 傳「六點」→ 收到兩行清單。
- 傳其中一個編號 → 收到圖片。
- 再傳同一個編號 → 仍收到圖片，log 沒有重新上傳（第二次走 file_id）。
- 傳「不存在的詞」→「找不到」。
- 用另一個 Telegram 帳號傳訊息 → 沒有任何回應。

Expected 全部符合才進下一步。

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: Telegram long polling 接線

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 12: 部署檔與文件

**Files:**
- Create: `deploy/memehelper.service`
- Modify: `README.md`、`CLAUDE.md`

- [ ] **Step 1: 寫 `deploy/memehelper.service`**

```ini
[Unit]
Description=MemeHelper Telegram bot and admin API
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
ExecStart=%h/Apps/MemeHelper/MemeHelper.Api
WorkingDirectory=%h/Apps/MemeHelper
Restart=always
RestartSec=10
TimeoutStopSec=60

[Install]
WantedBy=default.target
```

- [ ] **Step 2: README 加「部署」一節**

步驟：確認 `dotnet --list-runtimes` 有 `Microsoft.AspNetCore.App 10.0.x`；clone；複製 `appsettings.Local.json.example` 為 `appsettings.Local.json` 並填 token 與 user id；`dotnet run` 驗證；`dotnet publish src/MemeHelper.Api -c Release -o ~/Apps/MemeHelper`；`cp deploy/memehelper.service ~/.config/systemd/user/`；`systemctl --user daemon-reload && systemctl --user enable --now memehelper && loginctl enable-linger "$USER"`；`journalctl --user -u memehelper -f`。更新版本：`git pull`、stop、publish、start。另加「批次匯入」一段：用 curl 對 `POST /api/memes` 的範例一行，讓使用者自己寫 CSV 腳本。

- [ ] **Step 3: CLAUDE.md 的「指令」一節補上**

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
dotnet run --project src/MemeHelper.Api     # http://localhost:5090/swagger
```

並在「執行注意」補一行：`journalctl --user -u memehelper -f` 看正式服務 log。

- [ ] **Step 4: 檢查**

`git status` 確認 `appsettings.Local.json` 與 `Data/` 沒被加進來。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs: systemd unit 與部署步驟

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

# 從 Telegram 傳圖新增梗圖 實作計畫

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 使用者在 Telegram 傳照片或圖片檔給 bot、caption 填關鍵字，bot 下載存檔並建立梗圖。

**Architecture:** 沿用 v1 的結構。`IBotMessenger` 多一個下載方法，`MemeBotHandler` 多一個 `HandleImageAsync`，`TelegramPollingService` 把照片與檔案訊息分派過去。驗證規則與 API 上傳共用 `ImageFileRules` 與 `KeywordNormalizer`。

**Tech Stack:** 同 v1。

**Spec:** `docs/design.md` §5.1.1、`docs/features/telegram-upload.feature`

## Global Constraints

同 `2026-10-25-meme-helper-v1.md` 的 Global Constraints。

## Review Focus

1. 檔案形式的 `file_id` 不可存進 `telegram_file_id`（`sendPhoto` 不能重送 document 的 ID）。測試在 Task 1。
2. 下載成功但存檔失敗時，已建立的紀錄要刪掉。測試在 Task 1。
3. 照片沒有 caption 時不可建立紀錄。測試在 Task 1。

---

### Task 1: 傳圖新增

**Files:**
- Modify: `src/MemeHelper.Api/Telegram/IBotMessenger.cs`、`TelegramBotMessenger.cs`、`ReplyFormatter.cs`、`MemeBotHandler.cs`、`TelegramPollingService.cs`
- Test: `tests/MemeHelper.Tests/Telegram/MemeBotHandlerTests.cs`

**Interfaces:**

```csharp
// IBotMessenger 新增
Task<byte[]> DownloadFileAsync(string fileId, CancellationToken ct);   // bot.GetInfoAndDownloadFile(fileId, stream, ct)

// ReplyFormatter 新增
public const string UploadNeedsKeywords = "請在圖片的說明文字填關鍵字，用逗號隔開";
public const string UploadUnsupportedType = "只接受 jpg 或 png";
public const string UploadTooLarge = "檔案超過 10 MB";
public const string UploadBadContent = "檔案內容不是 {0}";
public const string UploadFailed = "上傳失敗，請稍後再試";
public static string FormatAdded(Meme meme);   // $"已新增【{meme.Id}】{string.Join(",", meme.Keywords)}"

// MemeBotHandler 新增
public Task HandleImageAsync(long userId, long chatId, string fileId, string? fileName, bool isPhoto, string? caption, CancellationToken ct);
```

  `HandleImageAsync` 流程：
  1. 非白名單 → 不回應。
  2. 副檔名：`isPhoto` → `"jpg"`；否則 `ImageFileRules.TryGetExtension(fileName ?? "")`，失敗 → `UploadUnsupportedType`。
  3. `KeywordNormalizer.Normalize([caption ?? ""])` 為空 → `UploadNeedsKeywords`。
  4. `DownloadFileAsync`；長度 > `MaxBytes` → `UploadTooLarge`；`HasValidSignature` 失敗 → `string.Format(UploadBadContent, extension)`。下載拋例外 → `LogError`、`UploadFailed`。
  5. `AddAsync` → `SaveAsync`（失敗則 `DeleteAsync`、`images.Delete`、`UploadFailed`）→ `isPhoto` 時 `SetTelegramFileIdAsync(id, fileId)` → `GetAsync` → `FormatAdded`。

  `TelegramPollingService.HandleUpdateAsync` 分派：`Text` → 現有；`Photo` 非空 → `HandleImageAsync(from.Id, chat.Id, photo[^1].FileId, null, true, message.Caption, ct)`；`Document` → `HandleImageAsync(from.Id, chat.Id, document.FileId, document.FileName, false, message.Caption, ct)`。

- [ ] **Step 1: 寫失敗的測試**（`MemeBotHandlerTests`，`DownloadFileAsync` 預設回 `SampleImages.Jpg()`；`repository.AddAsync` 回 7、`GetAsync(7)` 回新增後的 Meme）

```csharp
[Fact] HandleImageAsync_照片加caption_應新增並回覆編號且快取file_id()   // AddAsync("jpg", ["六點","下班"])、SaveAsync(7,"jpg",...)、SetTelegramFileIdAsync(7,"PHOTO_ID")、SendTextAsync(chat,"已新增【7】六點,下班")
[Fact] HandleImageAsync_檔案形式png_應以png新增且不快取file_id()       // DownloadFileAsync 回 Png()，AddAsync("png",...)，DidNotReceive SetTelegramFileIdAsync
[Fact] HandleImageAsync_caption經正規化_應去重去空白()                   // " 下班 ，，下班,Hello,hello" → ["下班","Hello"]
[Fact] HandleImageAsync_沒有caption_應回提示且不新增()
[Fact] HandleImageAsync_caption沒有有效關鍵字_應回提示且不新增()
[Fact] HandleImageAsync_檔案形式非圖片_應回只接受jpg或png()              // fileName "a.pdf"
[Fact] HandleImageAsync_檔案內容與副檔名不符_應回檔案內容不是png()        // fileName "a.png"，DownloadFileAsync 回 Gif()
[Fact] HandleImageAsync_檔案超過10MB_應回檔案超過10MB()
[Fact] HandleImageAsync_下載失敗_應回上傳失敗且不新增()
[Fact] HandleImageAsync_存檔失敗_應刪除紀錄並回上傳失敗()               // SaveAsync 拋例外 → DeleteAsync(7)
[Fact] HandleImageAsync_非白名單_不應呼叫messenger()
```

- [ ] **Step 2: 跑測試確認失敗**（編譯失敗）
- [ ] **Step 3: 實作五個檔案的修改**
- [ ] **Step 4: 三道門檻**
- [ ] **Step 5: 真機驗證**：啟動後在手機相簿分享一張圖給 bot、caption 填「測試,上傳」，應回「已新增【N】測試,上傳」；再傳「上傳」應直接回圖。
- [ ] **Step 6: Commit 並 push**

```bash
git add -A
git commit -m "feat: 從 Telegram 傳圖新增梗圖

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
git push
```

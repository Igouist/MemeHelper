# 快速上手

目標：十分鐘內讓 bot 在你自己的 Telegram 上回第一張梗圖。

## 需要準備

- .NET 10 SDK（`dotnet --version` 顯示 10.x）
- 一個 Telegram 帳號

## 1. 建立 bot

1. 在 Telegram 找 [@BotFather](https://t.me/BotFather)，傳 `/newbot`，照指示取名。
2. 它會給你一串 token，長得像 `1234567:AAxxxxxxxx`，先留著。

## 2. 查自己的 user id

Bot 只回應白名單裡的人，所以要知道自己的 id：

1. 先對剛建好的 bot 隨便傳一句話。
2. 瀏覽器開 `https://api.telegram.org/bot<TOKEN>/getUpdates`（把 `<TOKEN>` 換掉）。
3. 找 `"from": { "id": 123456789 ... }`，那串數字就是。

## 3. 設定並啟動

```bash
git clone <這個 repo>
cd MemeHelper
cp src/MemeHelper.Api/appsettings.Local.json.example src/MemeHelper.Api/appsettings.Local.json
```

編輯 `appsettings.Local.json`，填入 token 與 user id：

```json
{
  "Telegram": {
    "BotToken": "1234567:AAxxxxxxxx",
    "AllowedUserIds": [ 123456789 ]
  }
}
```

這個檔案不會進 git。然後：

```bash
dotnet run --project src/MemeHelper.Api
```

看到 `Telegram long polling 開始` 就是跑起來了。

## 4. 第一張梗圖

在手機上把一張圖分享給 bot，說明文字（caption）填關鍵字，用逗號或頓號隔開，例如 `六點、下班`，中文輸入法不用切換。Bot 會回「已新增【1】六點,下班」。

接著傳 `下班`，bot 就會把那張圖回給你。命中多張時會先回清單，再傳編號取圖。

## 5. 後台

瀏覽器開 `http://localhost:5090/swagger`，可以列出、停用、刪除梗圖與編輯關鍵字。

## 常見問題

| 現象 | 原因 |
|---|---|
| 啟動就失敗，訊息含 `BotToken field is required` | `appsettings.Local.json` 沒建或沒填 |
| Bot 完全不回 | user id 填錯；或同一個 token 已經有另一個程序在 polling（同一時間只能有一個） |
| 傳圖後回「請在圖片的說明文字填關鍵字」 | 分享時沒填 caption |
| 傳圖後回「只接受 jpg 或 png」 | 以檔案形式傳了其他格式；照片形式一律可收 |

## 下一步

- 長期跑在 Linux 設備上：README 的「部署」一節。
- 想知道 bot 怎麼收到訊息：[telegram-long-polling.md](telegram-long-polling.md)。
- 想改程式：[architecture.md](architecture.md)。

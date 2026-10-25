# Telegram Bot 怎麼收到訊息：Long Polling

## 兩種方式

Telegram 不會主動找到你的程式，除非你告訴它怎麼找。Bot API 提供兩種取得訊息的方式：

| | Webhook | Long polling |
|---|---|---|
| 方向 | Telegram 連到你的伺服器 | 你的程式連到 Telegram |
| 需要 | 公開的 HTTPS 網址、有效憑證、開放 443/80/88/8443 其中一個 port | 只要能連外網 |
| 適合 | 雲端服務、縮到零的無伺服器環境 | 家裡的機器、NAT 後面、不想開 port |
| 程式 | 一個 HTTP endpoint 等著被呼叫 | 一個常駐的迴圈 |

這個專案跑在家中設備上，不想對外開任何 port，所以用 long polling（決策記錄在 [ADR 0002](../adr/0002-telegram-long-polling.md)）。

## Long polling 怎麼運作

「long」的意思是：請求送出後，Telegram 不會馬上回，而是**等到有新訊息，或等到你指定的秒數用完**才回。

```
bot                                   Telegram
 |-- getUpdates(offset=0, timeout=90) -->|
 |                                       |  （沒有訊息，先掛著...）
 |                                       |  使用者傳了「六點」
 |<-- [ update #101 ]  -------------------|
 |  處理 #101
 |-- getUpdates(offset=102, timeout=90) ->|
 |                                       |  （等 90 秒都沒訊息）
 |<-- []  --------------------------------|
 |-- getUpdates(offset=102, timeout=90) ->|
```

兩個重點：

- **offset**：告訴 Telegram「編號小於這個的我都處理過了」，它就不會再給你。沒更新 offset 會一直收到同一則。
- **timeout**：伺服器最多等幾秒。設 0 是短輪詢，每秒狂問，只適合測試。

## 這個專案的做法

`Telegram/TelegramPollingService.cs` 是那個常駐迴圈，實際的 `getUpdates` 由 [Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot) 套件的 `ReceiveAsync` 處理：

```csharp
await bot.ReceiveAsync(HandleUpdateAsync, HandleErrorAsync, new ReceiverOptions
{
    AllowedUpdates = [UpdateType.Message],   // 只要訊息，不要其他事件
    DropPendingUpdates = true,               // 停機期間累積的訊息不補回
}, stoppingToken);
```

收到的每一則 `Update` 進 `HandleUpdateAsync`：文字交給查詢，照片與檔案交給新增，其他忽略。

## 三個會踩到的坑

**1. 同一個 token 只能有一個程序 polling。** 第二個程序會收到 `409 Conflict`，而且兩邊會互搶訊息。在開發機測試前，先停掉設備上的服務，或另外建一個測試用 bot。

**2. timeout 的計時從誰開始。** Telegram.Bot 預設把伺服器等待秒數設成和 `HttpClient.Timeout` 一樣（100 秒）。沒有訊息時伺服器會等滿才回，但 client 的計時從送出就開始，含網路延遲一定先到期，於是每段閒置都會冒一次 `Request timed out`。`Telegram/LongPollingBotClient.cs` 把伺服器等待秒數減 10 秒，讓伺服器永遠先回。

**3. 迴圈不能因為一次失敗就死掉。** 斷網、Telegram 短暫故障都會讓 `getUpdates` 拋例外。`TelegramPollingService` 做兩層保護：單次失敗退避 5 秒再試；整個 `ReceiveAsync` 中斷（例如開機時網路還沒好）則等 10 秒重新開始，而且不影響同一個程序裡的 HTTP 後台。

## 延伸閱讀

- [Telegram Bot API：getUpdates](https://core.telegram.org/bots/api#getupdates)
- [Telegram.Bot 的 polling 說明](https://telegrambots.github.io/book/3/updates/polling.html)

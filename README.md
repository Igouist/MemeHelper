# MemeHelper

個人用梗圖搜尋服務。在 Telegram 輸入關鍵字，幾秒內拿到對應的梗圖，再貼到任何聊天軟體。

## 第一次來？

- [快速上手](docs/guide/getting-started.md)：建 bot、填設定、回第一張圖。
- [Telegram 怎麼收到訊息](docs/guide/telegram-long-polling.md)：long polling 是什麼、為什麼選它、會踩的坑。
- [專案架構](docs/guide/architecture.md)：程式怎麼分、一次查詢經過哪些東西、想改東西從哪下手。

更深的文件：[需求與設計](docs/design.md)、[使用場景](docs/features/)、[決策紀錄](docs/adr/)。

## 怎麼用

對 bot 傳關鍵字，多個詞用空白隔開，每個詞都要命中：

- 命中一張：直接回圖。
- 命中多張：回清單 `【編號】關鍵字1,關鍵字2`，再傳編號取圖。
- 找不到：回「找不到」。

新增梗圖最快的方式是在手機上把圖分享給 bot，說明文字填關鍵字（用 `,`、`，` 或 `、` 隔開都可以）。以「照片」傳會被 Telegram 壓縮，要保留原圖請選「以檔案傳送」。收過的圖會回「已存在【編號】關鍵字」，不會重複新增；不填說明文字直接傳圖，就能查一張圖收過沒有。

管理圖片與關鍵字用 Swagger UI：`http://<主機>:5090/swagger`。

## 開發

需要 .NET 10 SDK。

```bash
cp src/MemeHelper.Api/appsettings.Local.json.example src/MemeHelper.Api/appsettings.Local.json
# 填入 BotFather 給的 token 與自己的 Telegram user id
dotnet build
dotnet test
dotnet run --project src/MemeHelper.Api
```

提交前三道門檻都要過：`dotnet build` 零警告、`dotnet test` 全綠、`dotnet format --verify-no-changes`。

`appsettings.Local.json` 與執行期的 `Data/`（SQLite 與圖片）都不進 git。

## 部署到 Linux 設備（systemd user unit）

1. 確認環境：`dotnet --list-runtimes` 要有 `Microsoft.AspNetCore.App 10.0.x`。
2. Clone 到 `~/Projects/MemeHelper`，建立並填好 `src/MemeHelper.Api/appsettings.Local.json`。`Backup:Directory` 填另一顆硬碟或同步資料夾的絕對路徑，見「備份與還原」。
3. `dotnet run --project src/MemeHelper.Api` 驗證 bot 會回應，確認後 Ctrl+C。
4. Publish：

   ```bash
   dotnet publish src/MemeHelper.Api -c Release -o ~/Apps/MemeHelper
   ```

   憑證檔會跟著過去。`Data/` 會建立在 `~/Apps/MemeHelper/Data/`，整個系統的狀態都在這裡。

5. 註冊服務：

   ```bash
   mkdir -p ~/.config/systemd/user
   cp deploy/memehelper.service ~/.config/systemd/user/
   systemctl --user daemon-reload
   systemctl --user enable --now memehelper
   loginctl enable-linger "$USER"
   ```

   `enable-linger` 不能省，否則登出就停、開機也不會自動起來。

6. 確認：`systemctl --user status memehelper`，log 用 `journalctl --user -u memehelper -f`。

更新版本：

```bash
cd ~/Projects/MemeHelper && git pull
systemctl --user stop memehelper
dotnet publish src/MemeHelper.Api -c Release -o ~/Apps/MemeHelper
systemctl --user start memehelper
```

Publish 不會動到 `Data/`。同一個 bot token 同時只能有一個程序 polling，開發機要測 bot 之前先停掉設備上的服務。

## 備份與還原

設定 `Backup:Directory` 後，服務每 24 小時把 `Data/` 備份到那裡，啟動時若已超過 24 小時沒備份會先補一次：

```
<Backup:Directory>/
  db/memehelper-20261029T030000Z.db   資料庫快照，保留最新 14 份（Backup:KeepCount）
  images/1.png ...                    圖片只增不刪，刪掉的梗圖也找得回來
```

路徑要在另一顆硬碟、NAS 掛載點或雲端同步資料夾，放同一顆硬碟防不了硬碟壞掉。成功或失敗都會寫進 log：

```bash
journalctl --user -u memehelper | grep 備份
```

還原：

```bash
systemctl --user stop memehelper
mkdir -p ~/Apps/MemeHelper/Data/images
cp <Backup:Directory>/db/memehelper-<時間>.db ~/Apps/MemeHelper/Data/memehelper.db
rm -f ~/Apps/MemeHelper/Data/memehelper.db-wal ~/Apps/MemeHelper/Data/memehelper.db-shm
cp -n <Backup:Directory>/images/* ~/Apps/MemeHelper/Data/images/
systemctl --user start memehelper
```

`-wal` 與 `-shm` 屬於舊資料庫，留著會蓋過剛還原的快照，一定要刪。

## 批次匯入

上傳 API 是 multipart，`keywords` 用逗號分隔。整理成 CSV 後逐行呼叫：

```bash
curl -F "file=@/path/to/meme.png" -F "keywords=六點,下班,蟹老闆" http://localhost:5090/api/memes
```

只收 jpg、png，單檔 10 MB 以內。內容已經收過的圖回 409，回應的 `existingId` 是既有編號。

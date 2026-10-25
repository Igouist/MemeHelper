---
status: accepted
date: 2026-10-25
---

# Telegram 更新用 long polling 接收

Telegram Bot API 的 webhook 需要公開 HTTPS 端點；long polling 由 bot 主動連線 `getUpdates`，只需要對外的 HTTPS 連線能力。採用 long polling，透過 Telegram.Bot 的接收器以 hosted service 跑在 API 同一個程序裡，宿主就不必曝露（ADR 0001）。

## 後果

- 程序必須常駐，由 systemd 保證重啟。
- 同一個 bot token 同時只能有一個程序 polling，開發時要先停掉設備上的服務或改用測試 bot。
- 若日後改上會縮到零的雲端宿主，必須換成 webhook。

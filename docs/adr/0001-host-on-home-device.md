---
status: accepted
date: 2026-10-25
---

# 宿主放在家中設備，不上雲端

目標是每月 0 元。Azure 的免費層（App Service F1、Container Apps、Static Web Apps）都做得到，但都有冷啟動，也都需要公開 HTTPS 端點；家中有一台 24 小時開機、不對外的 Linux 設備，配合 long polling（ADR 0002）可以完全不開對外 port。第一版只跑在這台設備上，以 systemd service 執行，不為雲端預留抽象層。

## 放棄的選項

Azure 各方案的調查與當時的選型保留在 `docs/research/azure-hosting-options.md`。

## 後果

- 可用性跟著設備：停電、重開機、斷網就停。
- 管理後台只在區網與私人 VPN 內可用，不做登入。
- 備份（`Data/` 資料夾）由使用者自行處理。

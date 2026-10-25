---
status: accepted
date: 2026-10-25
---

# 單一 ASP.NET Core 專案，Minimal API 加 feature 資料夾

這個專案只有十個左右的 endpoint 加一個 Telegram 訊息處理器，要求簡單。採用一個 `MemeHelper.Api` 專案加一個測試專案，用內建的 Minimal API，依功能分資料夾，一個 endpoint 一個檔案，維持 REPR 的閱讀習慣。Telegram polling 以 hosted service 跑在同一個程序。

## 放棄的選項

- FastEndpoints：內建 Minimal API 在這個規模已經夠用，少一個第三方依賴。
- Clean Architecture 多專案、三層式：抽象層數超過這個規模的需要，改一個功能要跨層。

## 後果

- 規模變大時再拆專案。
- 搜尋比對邏輯放在 `Features/Search`，Telegram 和管理 API 共用。

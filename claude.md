# L1MapViewer 開發指南

## 建置設定

### 避免 IDE 與 CLI 建置衝突

為了避免 Rider/IDE 與 CLI 建置互相干擾，請使用不同的輸出目錄：

```bash
# CLI 建置使用獨立輸出目錄
dotnet build -o bin/Debug/cli

# 執行 CLI 建置的程式
dotnet run --project . -o bin/Debug/cli
```

IDE 預設輸出至 `bin/Debug/net8.0-windows/`，CLI 輸出至 `bin/Debug/cli/`，兩者不會衝突。

## 專案結構

- `CLI/` - 命令列工具（S32 解析、寫入）
- `Helper/` - 輔助類別
- `Models/` - 資料模型（ViewState, EditState, MapDocument 等）
- `Other/` - 其他工具類別
- `plans/` - 設計文件

## 座標系統

### 世界座標 (World Coordinates)
- 整張地圖的像素座標系統
- 原點在地圖左上角
- 儲存於 `ViewState.ScrollX` / `ViewState.ScrollY`

### 螢幕座標 (Screen Coordinates)
- PictureBox 上的座標
- 計算方式: `(WorldCoord - ScrollPosition) * ZoomLevel`

### 遊戲座標 (Game Coordinates)
- Layer3 格子座標
- 用於遊戲邏輯

## Viewport 渲染

只渲染可見區域加上緩衝區（1024px），而非整張地圖：
- `ViewState.GetViewportWorldRect()` - 取得可見區域
- `ViewState.GetRenderWorldRect()` - 取得含緩衝區的渲染範圍
- `ViewState.NeedsRerender()` - 檢查是否需要重新渲染

## S32 區塊

每個區塊大小：3072 x 1536 像素 (64*24*2 x 64*12*2)

# L1MapViewer 開發指南

## 錯誤處理與日誌

### 日誌記錄規範

- **所有日誌必須使用 NLog**，不可使用 `Console.WriteLine`
- **所有 exception 都必須被記錄**，絕不可靜默吞掉錯誤

```csharp
// 在類別中加入 logger
using NLog;

private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

// 記錄錯誤（包含完整 exception）
catch (Exception ex)
{
    _logger.Error(ex, "描述發生了什麼事");
    // 處理錯誤...
}

// 其他日誌等級
_logger.Debug("除錯訊息");
_logger.Info("一般資訊");
_logger.Warn("警告訊息");
_logger.Error("錯誤訊息");
```

### 禁止的做法

```csharp
// ❌ 錯誤：靜默吞掉 exception
catch { }

// ❌ 錯誤：只 catch 不記錄
catch (Exception ex)
{
    // 沒有 log
    return null;
}

// ❌ 錯誤：使用 Console.WriteLine
Console.WriteLine($"Error: {ex.Message}");
```

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
- Layer3 格子座標 (每格對應 Layer1 的 2x1)
- 用於遊戲邏輯
- 範圍: 每個 S32 區塊是 64x64 遊戲格

### Layer1 座標
- 地板座標系統
- 每個 S32 區塊是 128x64 (X 是遊戲座標的 2 倍)

### 座標轉換公式

**遊戲座標 → 世界像素座標** (參考 `MapForm.JumpToGameCoordinate`):
```csharp
// 1. 找到包含該遊戲座標的 S32
// 2. 計算 S32 內的本地座標
int layer3LocalX = gameX - s32Data.SegInfo.nLinBeginX;  // 遊戲本地 0~63
int localX = layer3LocalX * 2;  // 轉換為 Layer1 座標 0~127
int localY = gameY - s32Data.SegInfo.nLinBeginY;  // 0~63

// 3. 取得 S32 的世界像素起點
int[] loc = s32Data.SegInfo.GetLoc(1.0);
int mx = loc[0];
int my = loc[1];

// 4. 計算基準偏移 (菱形地圖計算)
int localBaseX = 0 - 24 * (localX / 2);
int localBaseY = 63 * 12 - 12 * (localX / 2);

// 5. 計算最終世界座標 (格子左上角)
int worldX = mx + localBaseX + localX * 24 + localY * 24;
int worldY = my + localBaseY + localY * 12;

// 6. 格子中心點 (可選)
int centerX = worldX + 12;
int centerY = worldY + 12;
```

**Layer1 座標 → 世界像素座標** (用於 Tile 渲染):
```csharp
// Layer1 座標 (0~127 x, 0~63 y)
int halfX = layer1X / 2;
int baseX = -24 * halfX;
int baseY = 63 * 12 - 12 * halfX;
int pixelX = s32LocX + baseX + layer1X * 24 + layer1Y * 24;
int pixelY = s32LocY + baseY + layer1Y * 12;
```

**遊戲座標 ↔ Layer1 座標**:
```csharp
// 遊戲 → Layer1
int layer1X = gameX * 2;  // 或 (gameX - s32.nLinBeginX) * 2 取得本地座標
int layer1Y = gameY;

// Layer1 → 遊戲
int gameX = layer1X / 2;
int gameY = layer1Y;
```

## Viewport 渲染

只渲染可見區域加上緩衝區（1024px），而非整張地圖：
- `ViewState.GetViewportWorldRect()` - 取得可見區域
- `ViewState.GetRenderWorldRect()` - 取得含緩衝區的渲染範圍
- `ViewState.NeedsRerender()` - 檢查是否需要重新渲染

## S32 區塊

每個區塊大小：3072 x 1536 像素 (64*24*2 x 64*12*2)

## 測試工具

### 視窗截圖工具

`tests/capture_window.py` 可以抓取指定視窗的截圖，用於自動化驗證 UI 渲染。

```bash
# 基本用法（等待 10 秒後抓取標題含 "L1" 的視窗）
python tests/capture_window.py

# 指定參數
python tests/capture_window.py "L1MapViewer" screenshot.bmp 10
```

**參數：**
- `title` - 視窗標題關鍵字（預設: "L1"）
- `output` - 輸出檔案路徑（預設: "screenshot.bmp"）
- `delay` - 等待秒數（預設: 10）

**自動化測試流程：**
```bash
# 1. 建置並啟動程式（背景執行）
dotnet run -o bin/Debug/cli &

# 2. 等待並截圖
python tests/capture_window.py "L1MapViewer" tests/result.bmp 10

# 3. 檢查截圖確認渲染正常
```

注意：`tests/` 資料夾已加入 `.gitignore`，不會被提交。

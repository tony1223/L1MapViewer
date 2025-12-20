# fs32 / fs3p 格式與素材庫系統規劃

## 概述

實作兩種新格式用於地圖資料轉移：
- **fs32** - 地圖打包格式（S32 + Tiles）
- **fs3p** - 素材庫格式（可選 Layer + Tiles，跨地圖共享）

---

## 1. 格式規格

### 1.1 fs32 格式（地圖打包）

**用途**：
- 整張地圖備份
- 選取區塊匯出
- 選取區域匯出

**二進位結構**：
```
[Header]
0x00  4B   Magic: "FS32" (0x32335346)
0x04  2B   Version: 0x0001
0x06  2B   LayerFlags (bit0-7 = Layer1-8)
0x08  1B   Mode: 0=整張地圖, 1=選取區塊, 2=選取區域
0x09  4B   MapId 長度
0x0D  NB   MapId (UTF-8)

[選取區域資訊] (Mode=2 時)
  4B   OriginX (相對於第一個區塊)
  4B   OriginY
  4B   Width (格子數)
  4B   Height (格子數)

[區塊列表]
0x00  4B   BlockCount
[每個區塊]
  4B   BlockX (區塊座標)
  4B   BlockY (區塊座標)
  4B   S32DataLength
  NB   S32Data (依 LayerFlags 選擇性包含各層)

[Tile 資料區段]
0x00  4B   TileCount
[每個 Tile]
  4B   TileId
  16B  MD5 Hash
  4B   DataLength
  NB   TilData (.til 原始資料)
```

### 1.2 fs3p 格式（素材庫）

**用途**：
- 跨地圖共享的素材
- 可重複使用的地圖模板
- 預製件 (Prefab) 管理

**二進位結構**：
```
[Header]
0x00  4B   Magic: "FS3P" (0x50335346)
0x04  2B   Version: 0x0001
0x06  2B   LayerFlags (bit0=L1, bit1=L2, bit2=L3, bit3=L4)
0x08  4B   Name 長度
0x0C  NB   Name (UTF-8)
0x..  4B   Thumbnail 長度 (可為 0)
0x..  NB   Thumbnail PNG 資料

[範圍資訊]
4B   OriginOffsetX
4B   OriginOffsetY
4B   Width (格子數)
4B   Height (格子數)

[Layer1 資料] (如果 LayerFlags & 0x01)
4B   Count
[每項]
  4B   RelativeX
  4B   RelativeY
  1B   IndexId
  2B   TileId
  1B   Reserved

[Layer2 資料] (如果 LayerFlags & 0x02)
4B   Count
[每項]
  4B   RelativeX
  4B   RelativeY
  1B   IndexId
  2B   TileId
  1B   UK

[Layer3 資料] (如果 LayerFlags & 0x04)
4B   Count
[每項]
  4B   RelativeX
  4B   RelativeY
  2B   Attribute1
  2B   Attribute2

[Layer4 資料] (如果 LayerFlags & 0x08)
4B   Count
[每項]
  4B   RelativeX
  4B   RelativeY
  4B   GroupId (相對，從 0 開始重新編號)
  1B   Layer (渲染順序)
  1B   IndexId
  2B   TileId

[Tile 資料區段] - 同 fs32

[Metadata]
8B   CreatedTime (Unix timestamp)
8B   ModifiedTime (Unix timestamp)
4B   TagCount
[每個標籤]
  4B   Length
  NB   Tag (UTF-8)
```

---

## 2. Tile 對碰處理機制

### 2.1 處理流程

匯入 fs3p/fs32 時的 Tile 處理邏輯：

```
對於每個打包的 Tile (originalId, md5, tilData):

1. 檢查 Tile.pak 中是否有相同 originalId
   │
   ├─ 存在 originalId:
   │   │
   │   ├─ 計算現有 Tile 的 MD5
   │   │   ├─ MD5 一致 → 直接使用現有 Tile (IdMapping[originalId] = originalId)
   │   │   └─ MD5 不同 → 找新編號匯入 (IdMapping[originalId] = newId)
   │
   └─ 不存在 originalId:
       │
       ├─ 搜尋是否有相同 MD5 的其他 Tile
       │   ├─ 找到 → 使用該 Tile ID (IdMapping[originalId] = existingId)
       │   └─ 沒找到 → 匯入新 Tile
       │              ├─ 嘗試使用 originalId (如果可用)
       │              └─ 否則從 StartSearchId 開始找空位
```

### 2.2 設定項目

| 設定項 | 預設值 | 說明 |
|--------|--------|------|
| `TileSearchStartId` | 10000 | 找新編號時的起始位置 |
| `MaterialLibraryPath` | `Documents\L1MapViewer\Materials` | 素材庫存放路徑 |
| `MaxRecentMaterials` | 10 | 最近使用列表數量 |

---

## 3. 新增檔案清單

### 3.1 資料模型 (`Models/`)

```csharp
// Models/Fs32Data.cs
public class Fs32Data
{
    public const uint MAGIC = 0x32335346; // "FS32"
    public ushort Version { get; set; } = 1;
    public ushort LayerFlags { get; set; }
    public Fs32Mode Mode { get; set; }
    public string SourceMapId { get; set; }

    // 選取區域資訊 (Mode=2 時)
    public int SelectionOriginX { get; set; }
    public int SelectionOriginY { get; set; }
    public int SelectionWidth { get; set; }
    public int SelectionHeight { get; set; }

    public List<Fs32Block> Blocks { get; set; } = new();
    public Dictionary<int, TilePackageData> Tiles { get; set; } = new();
}

public enum Fs32Mode : byte
{
    WholeMap = 0,
    SelectedBlocks = 1,
    SelectedRegion = 2
}

// Models/Fs3pData.cs
public class Fs3pData
{
    public const uint MAGIC = 0x50335346; // "FS3P"
    public ushort Version { get; set; } = 1;
    public ushort LayerFlags { get; set; }
    public string Name { get; set; }
    public byte[] ThumbnailPng { get; set; }

    public int OriginOffsetX { get; set; }
    public int OriginOffsetY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public List<Fs3pLayer1Item> Layer1Items { get; set; } = new();
    public List<Fs3pLayer2Item> Layer2Items { get; set; } = new();
    public List<Fs3pLayer3Item> Layer3Items { get; set; } = new();
    public List<Fs3pLayer4Item> Layer4Items { get; set; } = new();

    public Dictionary<int, TilePackageData> Tiles { get; set; } = new();

    public long CreatedTime { get; set; }
    public long ModifiedTime { get; set; }
    public List<string> Tags { get; set; } = new();
}

// Models/TileMappingResult.cs
public class TileMappingResult
{
    public Dictionary<int, int> IdMapping { get; } = new(); // OriginalId -> NewId
    public int ReuseCount { get; set; }      // 直接使用現有的數量
    public int RemappedCount { get; set; }   // 重新分配編號的數量
    public int ImportedCount { get; set; }   // 新匯入的數量
}
```

### 3.2 解析器/寫入器 (`CLI/`)

| 檔案 | 說明 |
|------|------|
| `Fs32Parser.cs` | fs32 二進位解析 |
| `Fs32Writer.cs` | fs32 二進位寫入 |
| `Fs3pParser.cs` | fs3p 二進位解析 |
| `Fs3pWriter.cs` | fs3p 二進位寫入 |

### 3.3 輔助類別 (`Helper/`)

| 檔案 | 說明 |
|------|------|
| `TileHashManager.cs` | MD5 計算與快取 |
| `TileImportManager.cs` | Tile 對碰處理 |
| `MaterialLibrary.cs` | 素材庫管理（索引、搜尋、最近使用） |

### 3.4 UI 元件 (`Forms/`)

| 檔案 | 說明 |
|------|------|
| `ExportOptionsDialog.cs` | 匯出選項對話框 |
| `MaterialBrowserForm.cs` | 完整素材瀏覽器視窗 |

> 注意：素材面板直接在 `MapForm.Designer.cs` 中新增控件（lblMaterials, lvMaterials, btnMoreMaterials），不需要獨立的 UserControl。

---

## 4. 修改現有檔案

| 檔案 | 修改內容 |
|------|----------|
| `Properties/Settings.settings` | 新增 MaterialLibraryPath, TileSearchStartId, MaxRecentMaterials, RecentMaterials |
| `MapForm.cs` | 刪除 Layer4 群組篩選相關方法、新增素材面板事件處理、右鍵選單項目、素材貼上預覽 |
| `MapForm.Designer.cs` | 刪除 lblLayer4Groups, lvLayer4Groups；新增 lblMaterials, lvMaterials, btnMoreMaterials |
| `Models/EditState.cs` | 刪除 SelectedLayer4Groups；新增 IsMaterialPreviewMode, PreviewMaterial 等屬性 |
| `CLI/CliHandler.cs` | 新增 export-fs32, import-fs32, list-materials 等命令 |
| `Helper/ClipboardManager.cs` | 新增 ApplyFs3p() 方法 |

---

## 5. UI 設計

### 5.1 右側面板改造

**現有結構** (`MapForm.Designer.cs`):
```
rightPanel (220px 寬)
├── Tile 列表區 (y: 0-180)
│   ├── lblTileList
│   ├── txtTileSearch
│   └── lvTiles
├── Layer4 群組篩選 (y: 185-330) ← 刪除，替換成素材面板
│   ├── lblLayer4Groups        ← 刪除
│   └── lvLayer4Groups         ← 刪除
└── 群組縮圖列表 (y: 335-645)
    ├── lblGroupThumbnails
    ├── btnShowAllGroups
    └── lvGroupThumbnails
```

**修改後結構**:
```
rightPanel (220px 寬)
├── Tile 列表區 (y: 0-180) - 不變
├── 素材面板 (y: 185-330) ← 新增
│   ├── lblMaterials           (5, 185) "最近使用的素材"
│   ├── lvMaterials            (5, 210) 210x95 縮圖列表
│   └── btnMoreMaterials       (5, 308) "更多..." 按鈕
└── 群組縮圖列表 (y: 335-645) - 不變
```

### 5.2 素材面板細節

```
┌─────────────────────────────┐  y=185
│  最近使用的素材              │  lblMaterials
├─────────────────────────────┤  y=210
│ ┌────┐ ┌────┐ ┌────┐ ┌────┐│
│ │縮圖│ │縮圖│ │縮圖│ │縮圖││  lvMaterials
│ │    │ │    │ │    │ │    ││  (LargeIcon view)
│ └────┘ └────┘ └────┘ └────┘│  210x95
├─────────────────────────────┤  y=308
│        [更多...]            │  btnMoreMaterials
└─────────────────────────────┘  y=330
```

**功能**:
- 點擊縮圖 → 進入素材貼上模式（滑鼠跟隨預覽）
- 點擊「更多...」→ 開啟 MaterialBrowserForm

### 5.3 右鍵選單新增項目

選取區域後右鍵選單：
```
├─ 複製 (Ctrl+C)
├─ 貼上 (Ctrl+V)
├─ ─────────────
├─ 匯出為 fs32...
├─ 儲存為素材 (fs3p)...
```

### 5.4 匯出對話框

```
┌─ 匯出選項 ────────────────────────────┐
│                                       │
│  匯出模式:                            │
│  ○ 整張地圖                           │
│  ● 選取區域                           │
│                                       │
│  包含圖層:                            │
│  ☑ Layer1 - 地板                      │
│  ☑ Layer2 - 裝飾                      │
│  ☑ Layer3 - 屬性 (通行性)             │
│  ☑ Layer4 - 物件                      │
│  ☐ Layer5-8 - 進階 (事件/傳送點/特效)  │
│                                       │
│  ☑ 包含 Tile 資料                     │
│                                       │
│  ┌───────────────────────────────┐   │
│  │ 預覽區域                       │   │
│  │                                │   │
│  │  區塊數: 4                     │   │
│  │  Tile 數: 23                   │   │
│  │  預估大小: 1.2 MB              │   │
│  └───────────────────────────────┘   │
│                                       │
│           [匯出]    [取消]            │
└───────────────────────────────────────┘
```

### 5.5 素材瀏覽器

```
┌─ 素材庫瀏覽器 ────────────────────────────────────────┐
│ 搜尋: [____________] [🔍]   標籤: [全部 ▼]            │
├───────────────────────────────────────────────────────┤
│ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐      │
│ │         │ │         │ │         │ │         │      │
│ │  縮圖   │ │  縮圖   │ │  縮圖   │ │  縮圖   │      │
│ │         │ │         │ │         │ │         │      │
│ ├─────────┤ ├─────────┤ ├─────────┤ ├─────────┤      │
│ │ 草地    │ │ 石牆    │ │ 木屋    │ │ 水池    │      │
│ └─────────┘ └─────────┘ └─────────┘ └─────────┘      │
│                                                       │
│ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐      │
│ │  ...    │ │  ...    │ │  ...    │ │  ...    │      │
│ └─────────┘ └─────────┘ └─────────┘ └─────────┘      │
├───────────────────────────────────────────────────────┤
│ 已選擇: 木屋                                          │
│ 大小: 12x8 格子 | 建立時間: 2024-01-15               │
│ 標籤: 建築, 裝飾                                      │
│                                                       │
│              [使用]    [刪除]    [關閉]               │
└───────────────────────────────────────────────────────┘
```

---

## 6. 實作順序

### 第一階段：核心格式支援

| 步驟 | 檔案 | 說明 |
|------|------|------|
| 1 | `Models/Fs32Data.cs` | fs32 資料結構定義 |
| 2 | `Models/Fs3pData.cs` | fs3p 資料結構定義 |
| 3 | `Models/TileMappingResult.cs` | Tile 對碰結果結構 |
| 4 | `Helper/TileHashManager.cs` | MD5 計算與快取 |
| 5 | `CLI/Fs32Parser.cs` | fs32 讀取 |
| 6 | `CLI/Fs32Writer.cs` | fs32 寫入 |
| 7 | `CLI/Fs3pParser.cs` | fs3p 讀取 |
| 8 | `CLI/Fs3pWriter.cs` | fs3p 寫入 |

### 第二階段：Tile 管理

| 步驟 | 檔案 | 說明 |
|------|------|------|
| 9 | `Helper/TileImportManager.cs` | Tile 對碰處理邏輯 |
| 10 | `Properties/Settings.settings` | 新增設定項目 |

### 第三階段：UI 整合

| 步驟 | 檔案 | 說明 |
|------|------|------|
| 11 | `MapForm.Designer.cs` | 刪除 lblLayer4Groups, lvLayer4Groups |
| 12 | `MapForm.cs` | 刪除 Layer4 群組篩選相關方法 |
| 13 | `MapForm.Designer.cs` | 新增 lblMaterials, lvMaterials, btnMoreMaterials |
| 14 | `Helper/MaterialLibrary.cs` | 素材庫索引管理 |
| 15 | `MapForm.cs` | 素材面板事件處理、右鍵選單整合 |
| 16 | `Forms/ExportOptionsDialog.cs` | 匯出選項對話框 |
| 17 | `Forms/MaterialBrowserForm.cs` | 完整素材瀏覽器 |
| 18 | `Models/EditState.cs` | 素材預覽狀態 |

### 第四階段：CLI 支援

| 步驟 | 檔案 | 說明 |
|------|------|------|
| 19 | `CLI/CliHandler.cs` | 新增 CLI 命令 |

---

## 7. 關鍵依賴與參考

| 現有檔案 | 參考內容 |
|----------|----------|
| `CLI/S32Parser.cs` | S32 二進位解析邏輯 |
| `CLI/S32Writer.cs` | S32 二進位寫入邏輯 |
| `Reader/L1PakWriter.cs` | `AppendFiles()` 批次寫入 Tile |
| `Reader/L1PakReader.cs` | `UnPack()` 讀取現有 Tile |
| `Reader/L1IdxReader.cs` | `Find()` 檢查 Tile 是否存在 |
| `Helper/ClipboardManager.cs` | 複製貼上邏輯參考 |
| `Models/S32DataModels.cs` | 現有 Layer 資料結構定義 |

---

## 8. 測試項目

### 8.1 格式測試
- [ ] fs32 寫入/讀取往返測試
- [ ] fs3p 寫入/讀取往返測試
- [ ] 各種 LayerFlags 組合測試
- [ ] 大型地圖（多區塊）匯出測試

### 8.2 Tile 對碰測試
- [ ] MD5 一致 → 直接使用
- [ ] MD5 不同 → 重新分配編號
- [ ] 新 Tile → 匯入到原始 ID
- [ ] 新 Tile → 匯入到新 ID（原始 ID 被占用）
- [ ] 批次匯入多個 Tile

### 8.3 UI 測試
- [ ] 選取區域後右鍵匯出
- [ ] 素材面板顯示最近使用
- [ ] 素材瀏覽器搜尋和標籤篩選
- [ ] 素材貼上預覽
- [ ] 設定路徑變更

---

## 9. 未來擴充

- 素材分類（資料夾結構）
- 素材版本管理
- 雲端素材庫同步
- 素材合併（多個 fs3p 合併為一個）
- 素材預覽 3D 視圖（如果未來支援）

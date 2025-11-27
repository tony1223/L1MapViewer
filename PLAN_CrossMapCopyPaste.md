# 跨地圖複製貼上功能規劃

## 現況分析

### Tile 資源結構
- Tile 檔案存放在全域 "Tile" pak 中，用 `{tileId}.til` 作為 key
- `TileId` 和 `IndexId` 是全域唯一的，不分地圖
- 所有地圖共用同一個 Tile 資源庫

### 現有複製貼上流程
1. **複製時**：記錄 `RelativeX`, `RelativeY`, `GroupId`, `Layer`, `IndexId`, `TileId`, `OriginalIndex`
2. **貼上時**：根據相對座標計算目標位置，建立新的 `ObjectTile`

### 結論
由於 Tile 資源是全域共用的，**跨地圖複製貼上在 Tile 層面不需要額外處理**。只要 `TileId` 和 `IndexId` 相同，在任何地圖都能正確渲染。

---

## 需要修改的項目

### 1. 記錄來源地圖資訊（用於顯示/偵錯）

**修改 `CopiedObjectTile` 類別：**
```csharp
private class CopiedObjectTile
{
    public int RelativeX { get; set; }
    public int RelativeY { get; set; }
    public int GroupId { get; set; }
    public int Layer { get; set; }
    public int IndexId { get; set; }
    public int TileId { get; set; }
    public int OriginalIndex { get; set; }
    public string SourceMapId { get; set; }  // 新增：來源地圖 ID
}
```

### 2. 複製時記錄來源地圖

**修改 `CopyLayer4Objects()` 方法：**
```csharp
layer4Clipboard.Add(new CopiedObjectTile
{
    // ... 現有屬性 ...
    SourceMapId = currentMapId  // 新增
});
```

### 3. 貼上時的處理

**修改 `PasteLayer4Objects()` 方法：**
- 檢查是否跨地圖貼上
- 顯示適當的提示訊息

```csharp
// 檢查是否跨地圖貼上
bool isCrossMap = layer4Clipboard.Any(o => o.SourceMapId != currentMapId);
if (isCrossMap)
{
    string sourceMapId = layer4Clipboard.First().SourceMapId;
    // 可選：顯示確認對話框
    // DialogResult result = MessageBox.Show(
    //     $"正在從地圖 {sourceMapId} 貼上到 {currentMapId}，確定要繼續嗎？",
    //     "跨地圖貼上",
    //     MessageBoxButtons.YesNo);
}
```

### 4. 狀態列顯示

**複製後顯示來源地圖：**
```csharp
this.toolStripStatusLabel1.Text = $"已複製 {count} 個 Layer4 物件 (來源: {currentMapId})";
```

**貼上時顯示跨地圖資訊：**
```csharp
if (isCrossMap)
{
    message += $" (從地圖 {sourceMapId} 跨地圖貼上)";
}
```

---

## 潛在風險與處理

### 1. GroupId 衝突
- **風險**：不同地圖可能有相同的 GroupId 但代表不同物件群組
- **建議**：貼上時保留原始 GroupId，讓使用者自行管理
- **可選功能**：提供「重新分配 GroupId」選項

### 2. Layer 值範圍
- **風險**：不同地圖的 Layer 值範圍可能不同
- **建議**：保留原始 Layer 值，渲染順序會自動處理

### 3. 座標範圍檢查
- **現有處理**：已經檢查 `localX < 0 || localX >= 128 || localY < 0 || localY >= 64`
- **跨地圖處理**：相同邏輯適用，超出範圍的物件會被跳過並計入 `skippedCount`

---

## 實作步驟

### 第一階段：基本跨地圖支援
1. [ ] 修改 `CopiedObjectTile` 增加 `SourceMapId`
2. [ ] 修改 `CopyLayer4Objects()` 記錄 `SourceMapId`
3. [ ] 修改狀態列顯示來源地圖資訊

### 第二階段：使用者體驗優化
4. [ ] 貼上時檢查是否跨地圖
5. [ ] 跨地圖貼上時顯示提示訊息
6. [ ] 修改貼上完成訊息顯示跨地圖資訊

### 第三階段：進階功能（可選）
7. [ ] 跨地圖貼上確認對話框
8. [ ] GroupId 重新分配選項
9. [ ] 貼上預覽功能

---

## 測試項目

1. 同地圖複製貼上（迴歸測試）
2. 跨地圖複製貼上
3. 複製後切換地圖再貼上
4. 貼上到目標地圖邊界（部分物件超出範圍）
5. 貼上後 Undo/Redo
6. 跨地圖貼上後存檔

---

## 工時估計

- 第一階段：約 15 分鐘
- 第二階段：約 15 分鐘
- 第三階段：約 30 分鐘（可選）

總計：約 30 分鐘（基本功能）至 1 小時（含進階功能）

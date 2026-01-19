# L1MapViewer

Lineage 1 地圖檢視與編輯工具，可預覽、編輯線上遊戲客戶端的地圖資源檔（S32 格式）。

## Features

### GUI Mode

- 載入地圖資料夾並顯示完整地圖
- 多圖層檢視：Layer1（地板）、Layer2（裝飾）、Layer3（屬性）、Layer4（物件）、Layer5/7（擴展資料）
- 支援縮放、平移瀏覽
- 即時編輯 Layer1/2/3/4/5/7 資料
- Undo/Redo 支援
- 群組選取與批次刪除 Layer4 物件
- Minimap 快速導航
- 匯出地圖為 PNG 圖片

### CLI Mode

執行時加上 `-cli` 參數進入命令列模式：

```
L1MapViewerCore.exe -cli <command> [arguments]
```

#### Commands

**info** - 顯示 S32 檔案資訊
```
L1MapViewerCore.exe -cli info <s32_file>
```

**extract-tile** - 從 Tile.idx 解出指定 Tile
```
L1MapViewerCore.exe -cli extract-tile <tile_idx> <tile_id> <output_dir> [--downscale]
```

**render-adjacent** - 渲染多個相鄰 S32 區塊
```
L1MapViewerCore.exe -cli render-adjacent <map_dir> <center_x> <center_y> --size <n>
```

**benchmark-*** - 效能測試指令
```
L1MapViewerCore.exe -cli benchmark-viewport <map_dir> [--regions n]
L1MapViewerCore.exe -cli benchmark-minimap <map_dir> [--runs n]
L1MapViewerCore.exe -cli benchmark-thumbnails <map_dir>
```

## S32 File Structure

每個 S32 區塊包含：
- **Layer1**: 64x128 地板圖磚（菱形）
- **Layer2**: 裝飾圖層
- **Layer3**: 64x64 地圖屬性（可通行性等）
- **Layer4**: 物件（樹木、房屋等）
- **Layer5/7**: 擴展資料

區塊大小：3072 x 1536 像素

## Building

需要 .NET 10.0 SDK：

```bash
dotnet build --configuration Release
```

發布單一執行檔：

```bash
dotnet publish --configuration Release --runtime win-x64 --self-contained true -o publish /p:PublishSingleFile=true
```

## Requirements

- Windows 10/11
- .NET 10.0 Runtime（framework-dependent 版本）或無需額外安裝（self-contained 版本）

## Credits

* srwh1234   original project which I fork from.
- **非誠勿擾** - 幫忙解決幾個非常棘手的 Bug！
- **aardvark** - Tile type bit 4/5 research and analysis (Tile type bit 4/5 研究與解析)


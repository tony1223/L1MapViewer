using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using L1MapViewer.Models;
using L1MapViewer.Other;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// 剪貼簿管理器 - 處理複製/貼上功能
    /// </summary>
    public class ClipboardManager
    {
        // 複製的資料
        public List<CopiedCellData> CellClipboard { get; private set; } = new List<CopiedCellData>();
        public List<Layer2Item> Layer2Clipboard { get; private set; } = new List<Layer2Item>();
        public List<Layer5Item> Layer5Clipboard { get; private set; } = new List<Layer5Item>();
        public List<int> Layer6Clipboard { get; private set; } = new List<int>();
        public List<Layer7Item> Layer7Clipboard { get; private set; } = new List<Layer7Item>();
        public List<Layer8Item> Layer8Clipboard { get; private set; } = new List<Layer8Item>();

        // 狀態
        public bool HasClipboardData => CellClipboard.Count > 0;
        public string SourceMapId { get; private set; } = string.Empty;

        // 複製設定
        public bool CopyLayer1 { get; set; } = true;
        public bool CopyLayer2 { get; set; } = true;
        public bool CopyLayer3 { get; set; } = true;
        public bool CopyLayer4 { get; set; } = true;
        public bool CopyLayer5to8 { get; set; } = true;

        /// <summary>
        /// 清除剪貼簿
        /// </summary>
        public void Clear()
        {
            CellClipboard.Clear();
            Layer2Clipboard.Clear();
            Layer5Clipboard.Clear();
            Layer6Clipboard.Clear();
            Layer7Clipboard.Clear();
            Layer8Clipboard.Clear();
            SourceMapId = null!;
        }

        /// <summary>
        /// 複製選取的格子
        /// </summary>
        public CopyResult CopySelectedCells(
            List<SelectedCell> selectedCells,
            string currentMapId,
            HashSet<int> selectedLayer4Groups,
            bool isFilteringLayer4Groups)
        {
            var result = new CopyResult();

            if (selectedCells == null || selectedCells.Count == 0)
            {
                result.Success = false;
                result.Message = "選取區域內沒有任何格子";
                return result;
            }

            if (!CopyLayer1 && !CopyLayer3 && !CopyLayer4)
            {
                result.Success = false;
                result.Message = "請點擊「複製設定...」按鈕選擇要複製的圖層";
                return result;
            }

            Clear();

            // 計算所有格子的全域 Layer1 座標範圍
            int minGlobalX = int.MaxValue, minGlobalY = int.MaxValue;
            foreach (var cell in selectedCells)
            {
                int globalX = cell.S32Data.SegInfo.nLinBeginX * 2 + cell.LocalX;
                int globalY = cell.S32Data.SegInfo.nLinBeginY + cell.LocalY;
                if (globalX < minGlobalX) minGlobalX = globalX;
                if (globalY < minGlobalY) minGlobalY = globalY;
            }

            int layer1Count = 0, layer3Count = 0, layer4Count = 0;

            // 收集每個格子的資料
            foreach (var cell in selectedCells)
            {
                int globalX = cell.S32Data.SegInfo.nLinBeginX * 2 + cell.LocalX;
                int globalY = cell.S32Data.SegInfo.nLinBeginY + cell.LocalY;

                var cellData = new CopiedCellData
                {
                    RelativeX = globalX - minGlobalX,
                    RelativeY = globalY - minGlobalY
                };

                // Layer1 資料（地板）
                if (CopyLayer1)
                {
                    int layer1X = cell.LocalX;
                    if (layer1X >= 0 && layer1X < 128 && cell.LocalY >= 0 && cell.LocalY < 64)
                    {
                        var cell1 = cell.S32Data.Layer1[cell.LocalY, layer1X];
                        if (cell1 != null)
                        {
                            cellData.Layer1Cell1 = new TileCell { X = cell1.X, Y = cell1.Y, TileId = cell1.TileId, IndexId = cell1.IndexId };
                            if (cell1.TileId > 0) layer1Count++;
                        }
                    }
                    if (layer1X + 1 >= 0 && layer1X + 1 < 128 && cell.LocalY >= 0 && cell.LocalY < 64)
                    {
                        var cell2 = cell.S32Data.Layer1[cell.LocalY, layer1X + 1];
                        if (cell2 != null)
                        {
                            cellData.Layer1Cell2 = new TileCell { X = cell2.X, Y = cell2.Y, TileId = cell2.TileId, IndexId = cell2.IndexId };
                            if (cell2.TileId > 0) layer1Count++;
                        }
                    }
                }

                // Layer3 資料（屬性）
                if (CopyLayer3)
                {
                    int layer3X = cell.LocalX / 2;
                    if (layer3X >= 0 && layer3X < 64 && cell.LocalY >= 0 && cell.LocalY < 64)
                    {
                        var attr = cell.S32Data.Layer3[cell.LocalY, layer3X];
                        if (attr != null)
                        {
                            cellData.Layer3Attr = new MapAttribute { Attribute1 = attr.Attribute1, Attribute2 = attr.Attribute2 };
                            layer3Count++;
                        }
                    }
                }

                // Layer4 資料（物件）
                if (CopyLayer4)
                {
                    for (int i = 0; i < cell.S32Data.Layer4.Count; i++)
                    {
                        var obj = cell.S32Data.Layer4[i];
                        if ((obj.X / 2) != (cell.LocalX / 2) || obj.Y != cell.LocalY)
                            continue;

                        if (isFilteringLayer4Groups && selectedLayer4Groups.Count > 0)
                        {
                            if (!selectedLayer4Groups.Contains(obj.GroupId))
                                continue;
                        }

                        int objGlobalX = cell.S32Data.SegInfo.nLinBeginX * 2 + obj.X;
                        int objGlobalY = cell.S32Data.SegInfo.nLinBeginY + obj.Y;

                        cellData.Layer4Objects.Add(new CopiedObjectTile
                        {
                            RelativeX = objGlobalX - minGlobalX,
                            RelativeY = objGlobalY - minGlobalY,
                            GroupId = obj.GroupId,
                            Layer = obj.Layer,
                            IndexId = obj.IndexId,
                            TileId = obj.TileId,
                            OriginalIndex = i
                        });
                        layer4Count++;
                    }
                }

                CellClipboard.Add(cellData);
            }

            SourceMapId = currentMapId;

            // 複製 Layer2 和 Layer5-8 資料
            var processedS32 = new HashSet<S32Data>();
            foreach (var cell in selectedCells)
            {
                if (!processedS32.Contains(cell.S32Data))
                {
                    processedS32.Add(cell.S32Data);

                    if (CopyLayer2)
                    {
                        foreach (var item in cell.S32Data.Layer2)
                        {
                            if (!Layer2Clipboard.Any(l => l.X == item.X && l.Y == item.Y && l.TileId == item.TileId))
                            {
                                Layer2Clipboard.Add(new Layer2Item
                                {
                                    X = item.X,
                                    Y = item.Y,
                                    IndexId = item.IndexId,
                                    TileId = item.TileId,
                                    UK = item.UK
                                });
                            }
                        }
                    }

                    if (CopyLayer5to8)
                    {
                        foreach (var item in cell.S32Data.Layer5)
                        {
                            if (!Layer5Clipboard.Any(l => l.X == item.X && l.Y == item.Y))
                            {
                                Layer5Clipboard.Add(new Layer5Item { X = item.X, Y = item.Y, ObjectIndex = item.ObjectIndex, Type = item.Type });
                            }
                        }
                        foreach (var tilId in cell.S32Data.Layer6)
                        {
                            if (!Layer6Clipboard.Contains(tilId))
                            {
                                Layer6Clipboard.Add(tilId);
                            }
                        }
                        foreach (var item in cell.S32Data.Layer7)
                        {
                            if (!Layer7Clipboard.Any(l => l.Name == item.Name && l.X == item.X && l.Y == item.Y))
                            {
                                Layer7Clipboard.Add(new Layer7Item { Name = item.Name, X = item.X, Y = item.Y, TargetMapId = item.TargetMapId, PortalId = item.PortalId });
                            }
                        }
                        foreach (var item in cell.S32Data.Layer8)
                        {
                            if (!Layer8Clipboard.Any(l => l.SprId == item.SprId && l.X == item.X && l.Y == item.Y))
                            {
                                Layer8Clipboard.Add(new Layer8Item { SprId = item.SprId, X = item.X, Y = item.Y, ExtendedData = item.ExtendedData });
                            }
                        }
                    }
                }
            }

            // 組合提示訊息
            var parts = new List<string>();
            if (CopyLayer1 && layer1Count > 0) parts.Add($"L1:{layer1Count}");
            if (CopyLayer2 && Layer2Clipboard.Count > 0) parts.Add($"L2:{Layer2Clipboard.Count}");
            if (CopyLayer3 && layer3Count > 0) parts.Add($"L3:{layer3Count}");
            if (CopyLayer4 && layer4Count > 0) parts.Add($"L4:{layer4Count}");
            if (CopyLayer5to8 && (Layer5Clipboard.Count > 0 || Layer6Clipboard.Count > 0 || Layer7Clipboard.Count > 0 || Layer8Clipboard.Count > 0))
                parts.Add($"L5:{Layer5Clipboard.Count} L6:{Layer6Clipboard.Count} L7:{Layer7Clipboard.Count} L8:{Layer8Clipboard.Count}");

            string layerInfo = parts.Count > 0 ? string.Join(", ", parts) : "無資料";
            result.Success = true;
            result.CellCount = selectedCells.Count;
            result.LayerInfo = layerInfo;
            result.Message = $"已複製 {selectedCells.Count} 格 ({layerInfo}) 來源: {currentMapId}，Shift+左鍵選取貼上位置後按 Ctrl+V";

            return result;
        }

        /// <summary>
        /// 貼上到目標位置
        /// </summary>
        public PasteResult PasteCells(
            Point pasteOrigin,
            string currentMapId,
            Func<int, int, S32Data> getS32DataByGameCoords)
        {
            var result = new PasteResult();

            if (CellClipboard.Count == 0)
            {
                result.Success = false;
                result.Message = "剪貼簿沒有資料";
                return result;
            }

            int pasteOriginX = pasteOrigin.X;
            int pasteOriginY = pasteOrigin.Y;

            int layer1Count = 0, layer3Count = 0, layer4Count = 0;
            int skippedCount = 0;
            var affectedS32Set = new HashSet<S32Data>();

            foreach (var cellData in CellClipboard)
            {
                int targetGlobalX = pasteOriginX + cellData.RelativeX;
                int targetGlobalY = pasteOriginY + cellData.RelativeY;
                int targetGameX = targetGlobalX / 2;
                int targetGameY = targetGlobalY;

                S32Data targetS32 = getS32DataByGameCoords(targetGameX, targetGameY);
                if (targetS32 == null)
                {
                    skippedCount++;
                    continue;
                }

                int localX = targetGlobalX - targetS32.SegInfo.nLinBeginX * 2;
                int localY = targetGlobalY - targetS32.SegInfo.nLinBeginY;

                if (localX < 0 || localX >= 128 || localY < 0 || localY >= 64)
                {
                    skippedCount++;
                    continue;
                }

                affectedS32Set.Add(targetS32);

                // Layer1 資料
                if (cellData.Layer1Cell1 != null && localX >= 0 && localX < 128)
                {
                    targetS32.Layer1[localY, localX] = new TileCell
                    {
                        X = localX,
                        Y = localY,
                        TileId = cellData.Layer1Cell1.TileId,
                        IndexId = cellData.Layer1Cell1.IndexId
                    };
                    if (cellData.Layer1Cell1.TileId > 0) layer1Count++;
                    targetS32.IsModified = true;
                }
                if (cellData.Layer1Cell2 != null && localX + 1 >= 0 && localX + 1 < 128)
                {
                    targetS32.Layer1[localY, localX + 1] = new TileCell
                    {
                        X = localX + 1,
                        Y = localY,
                        TileId = cellData.Layer1Cell2.TileId,
                        IndexId = cellData.Layer1Cell2.IndexId
                    };
                    if (cellData.Layer1Cell2.TileId > 0) layer1Count++;
                    targetS32.IsModified = true;
                }

                // Layer3 資料
                if (cellData.Layer3Attr != null)
                {
                    int layer3X = localX / 2;
                    if (layer3X >= 0 && layer3X < 64)
                    {
                        targetS32.Layer3[localY, layer3X] = new MapAttribute
                        {
                            Attribute1 = cellData.Layer3Attr.Attribute1,
                            Attribute2 = cellData.Layer3Attr.Attribute2
                        };
                        layer3Count++;
                        targetS32.IsModified = true;
                    }
                }

                // Layer4 資料
                if (cellData.Layer4Objects.Count > 0)
                {
                    int layer3X = localX / 2;
                    targetS32.Layer4.RemoveAll(o => (o.X / 2) == layer3X && o.Y == localY);

                    foreach (var objData in cellData.Layer4Objects.OrderBy(o => o.OriginalIndex))
                    {
                        int objTargetGlobalX = pasteOriginX + objData.RelativeX;
                        int objTargetGlobalY = pasteOriginY + objData.RelativeY;
                        int objLocalX = objTargetGlobalX - targetS32.SegInfo.nLinBeginX * 2;
                        int objLocalY = objTargetGlobalY - targetS32.SegInfo.nLinBeginY;

                        if (objLocalX >= 0 && objLocalX < 128 && objLocalY >= 0 && objLocalY < 64)
                        {
                            targetS32.Layer4.Add(new ObjectTile
                            {
                                GroupId = objData.GroupId,
                                X = objLocalX,
                                Y = objLocalY,
                                Layer = objData.Layer,
                                IndexId = objData.IndexId,
                                TileId = objData.TileId
                            });
                            layer4Count++;
                        }
                    }
                    targetS32.IsModified = true;
                }
            }

            // 合併 Layer2 和 Layer5-8
            int layer2AddedCount = 0;
            int layer5to8CopiedCount = 0;

            foreach (var targetS32 in affectedS32Set)
            {
                if (CopyLayer2 && Layer2Clipboard.Count > 0)
                {
                    foreach (var item in Layer2Clipboard)
                    {
                        if (!targetS32.Layer2.Any(l => l.X == item.X && l.Y == item.Y && l.TileId == item.TileId))
                        {
                            targetS32.Layer2.Add(new Layer2Item
                            {
                                X = item.X,
                                Y = item.Y,
                                IndexId = item.IndexId,
                                TileId = item.TileId,
                                UK = item.UK
                            });
                            layer2AddedCount++;
                            targetS32.IsModified = true;
                        }
                    }
                }

                if (CopyLayer5to8)
                {
                    foreach (var item in Layer5Clipboard)
                    {
                        if (!targetS32.Layer5.Any(l => l.X == item.X && l.Y == item.Y))
                        {
                            targetS32.Layer5.Add(new Layer5Item { X = item.X, Y = item.Y, ObjectIndex = item.ObjectIndex, Type = item.Type });
                            targetS32.IsModified = true;
                        }
                    }
                    int layer6Added = 0;
                    foreach (var tilId in Layer6Clipboard)
                    {
                        if (!targetS32.Layer6.Contains(tilId))
                        {
                            targetS32.Layer6.Add(tilId);
                            targetS32.IsModified = true;
                            layer6Added++;
                        }
                    }
                    if (layer6Added > 0) layer5to8CopiedCount++;
                    foreach (var item in Layer7Clipboard)
                    {
                        if (!targetS32.Layer7.Any(l => l.Name == item.Name && l.X == item.X && l.Y == item.Y))
                        {
                            targetS32.Layer7.Add(new Layer7Item { Name = item.Name, X = item.X, Y = item.Y, TargetMapId = item.TargetMapId, PortalId = item.PortalId });
                            targetS32.IsModified = true;
                        }
                    }
                    foreach (var item in Layer8Clipboard)
                    {
                        if (!targetS32.Layer8.Any(l => l.SprId == item.SprId && l.X == item.X && l.Y == item.Y))
                        {
                            targetS32.Layer8.Add(new Layer8Item { SprId = item.SprId, X = item.X, Y = item.Y, ExtendedData = item.ExtendedData });
                            targetS32.IsModified = true;
                        }
                    }
                }
            }

            // 組合提示訊息
            bool isCrossMap = !string.IsNullOrEmpty(SourceMapId) && SourceMapId != currentMapId;
            var parts = new List<string>();
            if (layer1Count > 0) parts.Add($"L1:{layer1Count}");
            if (layer2AddedCount > 0) parts.Add($"L2:+{layer2AddedCount}");
            if (layer3Count > 0) parts.Add($"L3:{layer3Count}");
            if (layer4Count > 0) parts.Add($"L4:{layer4Count}");
            if (layer5to8CopiedCount > 0) parts.Add($"L6:+{layer5to8CopiedCount}");

            string layerInfo = parts.Count > 0 ? string.Join(", ", parts) : "無資料";
            result.Success = true;
            result.CellCount = CellClipboard.Count;
            result.SkippedCount = skippedCount;
            result.IsCrossMap = isCrossMap;
            result.SourceMapId = SourceMapId;

            string message = $"已貼上 {CellClipboard.Count} 格 ({layerInfo})";
            if (isCrossMap)
                message += $" (從 {SourceMapId} 跨地圖貼上)";
            if (skippedCount > 0)
                message += $"，{skippedCount} 格超出範圍被跳過";
            result.Message = message;

            return result;
        }
    }

    /// <summary>
    /// 複製結果
    /// </summary>
    public class CopyResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int CellCount { get; set; }
        public string LayerInfo { get; set; } = string.Empty;
    }

    /// <summary>
    /// 貼上結果
    /// </summary>
    public class PasteResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int CellCount { get; set; }
        public int SkippedCount { get; set; }
        public bool IsCrossMap { get; set; }
        public string SourceMapId { get; set; } = string.Empty;
    }
}

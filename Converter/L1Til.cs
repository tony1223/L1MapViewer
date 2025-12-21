using System;
using System.Collections.Generic;
using System.IO;

namespace L1MapViewer.Converter {
    class L1Til {

        /// <summary>
        /// Tile 版本類型
        /// </summary>
        public enum TileVersion
        {
            /// <summary>24x24 舊版</summary>
            Classic,
            /// <summary>48x48 R版 (Remaster)</summary>
            Remaster,
            /// <summary>混合格式：block 大小在 Classic 範圍，但座標在 48x48 範圍</summary>
            Hybrid,
            /// <summary>無法判斷</summary>
            Unknown
        }

        /// <summary>
        /// 判斷 til 資料是否為 R 版 (48x48) 或混合格式
        /// 根據第一個 block 的大小判斷：
        /// - 24x24: 約 625 bytes (2*12*13*2 + 1)
        /// - 48x48: 約 2401 bytes (2*24*25*2 + 1)
        /// </summary>
        public static bool IsRemaster(byte[] tilData)
        {
            var version = GetVersion(tilData);
            return version == TileVersion.Remaster || version == TileVersion.Hybrid;
        }

        /// <summary>
        /// 取得 til 資料的版本
        /// </summary>
        public static TileVersion GetVersion(byte[] tilData)
        {
            if (tilData == null || tilData.Length < 8)
                return TileVersion.Unknown;

            try
            {
                using (var br = new BinaryReader(new MemoryStream(tilData)))
                {
                    int blockCount = br.ReadInt32();
                    if (blockCount <= 0)
                        return TileVersion.Unknown;

                    // 讀取前兩個 offset 來計算第一個 block 大小
                    int offset0 = br.ReadInt32();
                    int offset1 = br.ReadInt32();
                    int firstBlockSize = offset1 - offset0;

                    // 根據 block 大小判斷
                    // 24x24: 2*12*13*2 + 1 = 625 bytes (容許範圍 10-1000)
                    // 48x48: 2*24*25*2 + 1 = 2401 bytes (容許範圍 1800-3500)
                    // 混合格式的第一個 block 可能很小（壓縮格式）
                    if (firstBlockSize >= 1800 && firstBlockSize <= 3500)
                        return TileVersion.Remaster;
                    else if (firstBlockSize >= 10 && firstBlockSize <= 1800)
                    {
                        // 解析所有 blocks 檢查格式
                        var blocks = Parse(tilData);

                        // 先檢查是否有任何 block 的 xxLen 或 yLen 超過 24
                        // 如果有，這是 Remaster（壓縮得很好的 48x48）
                        if (HasRemasterPixelSize(blocks))
                            return TileVersion.Remaster;

                        // 檢查是否為混合格式：座標在 48x48 範圍
                        if (HasHybridCoordinates(blocks))
                            return TileVersion.Hybrid;

                        // 如果沒有超出範圍的座標，且 block 大小在 Classic 範圍內，則是 Classic
                        if (firstBlockSize >= 400 && firstBlockSize <= 1000)
                            return TileVersion.Classic;

                        // 其他情況視為 Unknown
                        return TileVersion.Unknown;
                    }
                    else
                        return TileVersion.Unknown;
                }
            }
            catch
            {
                return TileVersion.Unknown;
            }
        }

        /// <summary>
        /// 檢查是否有 block 的像素尺寸超過 24x24（表示這是 Remaster）
        /// </summary>
        private static bool HasRemasterPixelSize(List<byte[]> blocks)
        {
            HashSet<byte> simpleDiamondTypes = new HashSet<byte> { 0, 1, 8, 9, 16, 17 };

            foreach (var block in blocks)
            {
                if (block == null || block.Length < 6)
                    continue;

                byte type = block[0];

                // 簡單菱形格式：檢查資料大小
                // 48x48 約 2400 bytes，24x24 約 624 bytes
                if (simpleDiamondTypes.Contains(type))
                {
                    if (block.Length > 1200)
                        return true;
                    continue;
                }

                // 壓縮格式：檢查 xxLen 和 yLen
                byte xxLen = block[3];
                byte yLen = block[4];

                if (xxLen > 24 || yLen > 24)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 檢查 block 列表是否為混合格式
        /// Hybrid 的特徵：座標使用 48x48 系統（x_offset 或 y_offset 超過 24），
        /// 但像素尺寸在 24x24 範圍內（xxLen 和 yLen 都不超過 24）
        ///
        /// Remaster 的特徵：xxLen 或 yLen 超過 24（實際有 48x48 像素）
        /// </summary>
        private static bool HasHybridCoordinates(List<byte[]> blocks)
        {
            // 簡單菱形格式: 0, 1, 8, 9, 16, 17 - 這些沒有座標欄位
            HashSet<byte> simpleDiamondTypes = new HashSet<byte> { 0, 1, 8, 9, 16, 17 };

            foreach (var block in blocks)
            {
                if (block == null || block.Length < 6)
                    continue;

                byte type = block[0];

                // 跳過簡單菱形格式
                if (simpleDiamondTypes.Contains(type))
                    continue;

                // 壓縮格式
                byte x_offset = block[1];
                byte y_offset = block[2];
                byte xxLen = block[3];
                byte yLen = block[4];

                // 如果 xxLen 或 yLen 超過 24，這是標準的 Remaster (48x48 像素)
                // 不是 Hybrid
                if (xxLen > 24 || yLen > 24)
                    return false;

                // 如果座標超過 24，但像素尺寸在 24x24 範圍內，這是 Hybrid
                if (x_offset > 24 || y_offset > 24 ||
                    x_offset + xxLen > 24 || y_offset + yLen > 24)
                {
                    // 確認這不是 Remaster：繼續檢查其他 block
                    // 只要有一個 block 的 xxLen 或 yLen 超過 24，就是 Remaster
                }
            }

            // 第二輪檢查：是否有座標超過 24 且所有 block 像素尺寸都在 24 以內
            bool has48Coordinates = false;
            foreach (var block in blocks)
            {
                if (block == null || block.Length < 6)
                    continue;

                byte type = block[0];
                if (simpleDiamondTypes.Contains(type))
                    continue;

                byte x_offset = block[1];
                byte y_offset = block[2];
                byte xxLen = block[3];
                byte yLen = block[4];

                if (x_offset > 24 || y_offset > 24 ||
                    x_offset + xxLen > 24 || y_offset + yLen > 24)
                {
                    has48Coordinates = true;
                    break;
                }
            }

            return has48Coordinates;
        }

        /// <summary>
        /// 取得 til 版本對應的 tile 尺寸
        /// </summary>
        public static int GetTileSize(TileVersion version)
        {
            switch (version)
            {
                case TileVersion.Classic: return 24;
                case TileVersion.Remaster: return 48;
                case TileVersion.Hybrid: return 48;  // 混合格式使用 48x48 座標系統
                default: return 24;
            }
        }

        /// <summary>
        /// 從 til 資料取得 tile 尺寸
        /// </summary>
        public static int GetTileSize(byte[] tilData)
        {
            return GetTileSize(GetVersion(tilData));
        }

        /// <summary>
        /// 將 R 版 (48x48) 的 block 縮小成 Classic 版 (24x24)
        /// 支援所有 block type：
        /// - Type 0,1,8,9,16,17: 簡單菱形格式
        /// - 其他 Type (3,34,35等): 壓縮格式，需要先解碼再降採樣再重新編碼
        /// </summary>
        public static byte[] DownscaleBlock(byte[] blockData)
        {
            if (blockData == null || blockData.Length < 2)
                return blockData;

            byte type = blockData[0];

            // 判斷是否為簡單菱形格式
            bool isSimpleDiamond = (type == 0 || type == 1 || type == 8 || type == 9 || type == 16 || type == 17);

            if (isSimpleDiamond)
            {
                return DownscaleSimpleDiamondBlock(blockData, type);
            }
            else
            {
                // 壓縮格式：先解碼到 48x48 bitmap，降採樣到 24x24，再重新編碼
                return DownscaleCompressedBlock(blockData, type);
            }
        }

        /// <summary>
        /// 降採樣簡單菱形格式的 block (type 0,1,8,9,16,17)
        /// 48x48 有 48 行像素，每行像素數根據菱形結構決定
        /// </summary>
        private static byte[] DownscaleSimpleDiamondBlock(byte[] blockData, byte type)
        {
            // 48x48 菱形總像素數 = 2 * 24 * 25 = 1200 pixels = 2400 bytes + 1 type byte = 2401 bytes
            // 24x24 菱形總像素數 = 2 * 12 * 13 = 312 pixels = 624 bytes + 1 type byte = 625 bytes

            int srcDataLen = blockData.Length - 1; // 扣掉 type byte
            int srcPixelCount = srcDataLen / 2;

            // 48x48 的像素數應該是 2400，24x24 是 624
            // 如果不是 R 版的大小，直接返回
            if (srcPixelCount < 1000)  // 已經是 24x24 版本
                return blockData;

            const int srcTileSize = 48;  // R 版 tile 尺寸
            const int dstTileSize = 24;  // Classic 版 tile 尺寸

            // 解析來源像素到 2D 陣列 (48 行)
            var srcRows = new List<ushort[]>();
            int srcOffset = 1;  // 跳過 type byte

            for (int ty = 0; ty < srcTileSize; ty++)
            {
                int n;
                if (ty <= srcTileSize / 2 - 1)  // ty <= 23
                    n = (ty + 1) * 2;
                else
                    n = (srcTileSize - 1 - ty) * 2;

                var row = new ushort[n];
                for (int x = 0; x < n; x++)
                {
                    if (srcOffset + 1 < blockData.Length)
                    {
                        row[x] = (ushort)(blockData[srcOffset] | (blockData[srcOffset + 1] << 8));
                        srcOffset += 2;
                    }
                }
                srcRows.Add(row);
            }

            // 建立目標像素陣列 (2x2 降採樣)
            var result = new List<byte> { type };

            for (int dstY = 0; dstY < dstTileSize; dstY++)
            {
                int dstN;
                if (dstY <= dstTileSize / 2 - 1)  // dstY <= 11
                    dstN = (dstY + 1) * 2;
                else
                    dstN = (dstTileSize - 1 - dstY) * 2;

                for (int dstX = 0; dstX < dstN; dstX++)
                {
                    // 對應來源的 2x2 區塊
                    int srcY1 = dstY * 2;
                    int srcY2 = dstY * 2 + 1;
                    int srcX1 = dstX * 2;
                    int srcX2 = dstX * 2 + 1;

                    int r = 0, g = 0, b = 0, count = 0;

                    // 取樣來源 2x2 區塊的像素
                    void SamplePixel(int sy, int sx)
                    {
                        if (sy < srcRows.Count && sx >= 0 && sx < srcRows[sy].Length)
                        {
                            ushort c = srcRows[sy][sx];
                            r += (c >> 10) & 0x1F;
                            g += (c >> 5) & 0x1F;
                            b += c & 0x1F;
                            count++;
                        }
                    }

                    SamplePixel(srcY1, srcX1);
                    SamplePixel(srcY1, srcX2);
                    SamplePixel(srcY2, srcX1);
                    SamplePixel(srcY2, srcX2);

                    if (count > 0)
                    {
                        r /= count;
                        g /= count;
                        b /= count;
                    }

                    ushort avgColor = (ushort)((r << 10) | (g << 5) | b);
                    result.Add((byte)(avgColor & 0xFF));
                    result.Add((byte)((avgColor >> 8) & 0xFF));
                }
            }

            // 加上 Parse 多讀的 1 byte (與原始格式相容)
            result.Add(0);

            return result.ToArray();
        }

        /// <summary>
        /// 降採樣壓縮格式的 block (type 3, 6, 7, 34, 35 等)
        /// 這些格式有 x_offset, y_offset, xxLen, yLen 和 segment 結構
        /// 支援三種情況：
        /// 1. 標準 R 版 (48x48)：完整的 48x48 像素，需要降採樣像素
        /// 2. 混合格式：block 大小在 Classic 範圍，但座標在 48x48 範圍，只需縮放座標
        /// 3. Classic 版：不需處理
        /// </summary>
        private static byte[] DownscaleCompressedBlock(byte[] blockData, byte type)
        {
            if (blockData.Length < 6)
                return blockData;

            // 讀取壓縮格式的 header
            int idx = 1;  // 跳過 type byte
            byte x_offset = blockData[idx++];
            byte y_offset = blockData[idx++];
            byte xxLen = blockData[idx++];
            byte yLen = blockData[idx++];

            // 檢查是否為混合格式：座標超過 24x24 範圍（使用 48x48 座標系統）
            bool isHybrid = (x_offset > 24 || y_offset > 24 ||
                             x_offset + xxLen > 48 || y_offset + yLen > 48);

            // 混合格式：只需縮放座標，不需降採樣像素
            // 這種格式的像素已經是 24x24 尺寸，但座標使用 48x48 系統
            if (isHybrid)
            {
                return DownscaleHybridBlock(blockData, type);
            }

            // 注意：此函數只會被 DownscaleTil 呼叫，而 DownscaleTil 已經用 GetVersion
            // 確認整個 tile 是 Remaster 版本，所以這裡不再做額外判斷，直接進行降採樣

            // 標準 R 版：需要完整的 2x2 降採樣
            // 解碼到 48x48 像素陣列 (使用 -1 表示透明)
            const int srcSize = 48;
            const int dstSize = 24;
            int[,] srcPixels = new int[srcSize, srcSize];
            for (int y = 0; y < srcSize; y++)
                for (int x = 0; x < srcSize; x++)
                    srcPixels[y, x] = -1;  // -1 表示透明

            // 解析壓縮資料到 srcPixels
            for (int ty = 0; ty < yLen && idx < blockData.Length - 1; ty++)
            {
                int tx = x_offset;
                byte xSegmentCount = blockData[idx++];

                for (int nx = 0; nx < xSegmentCount && idx < blockData.Length - 2; nx++)
                {
                    int skip = blockData[idx++] / 2;  // 跳過的像素數
                    tx += skip;
                    int xLen = blockData[idx++];  // 這個 segment 的像素數

                    for (int p = 0; p < xLen && idx + 1 < blockData.Length; p++)
                    {
                        ushort color = (ushort)(blockData[idx] | (blockData[idx + 1] << 8));
                        idx += 2;

                        int pixY = ty + y_offset;
                        int pixX = tx;
                        if (pixY < srcSize && pixX < srcSize)
                        {
                            srcPixels[pixY, pixX] = color;
                        }
                        tx++;
                    }
                }
            }

            // 2x2 降採樣到 24x24
            int[,] dstPixels = new int[dstSize, dstSize];
            for (int y = 0; y < dstSize; y++)
                for (int x = 0; x < dstSize; x++)
                    dstPixels[y, x] = -1;

            for (int dstY = 0; dstY < dstSize; dstY++)
            {
                for (int dstX = 0; dstX < dstSize; dstX++)
                {
                    int srcY1 = dstY * 2;
                    int srcY2 = dstY * 2 + 1;
                    int srcX1 = dstX * 2;
                    int srcX2 = dstX * 2 + 1;

                    int r = 0, g = 0, b = 0, count = 0;

                    void SamplePixel(int sy, int sx)
                    {
                        if (sy < srcSize && sx < srcSize && srcPixels[sy, sx] >= 0)
                        {
                            ushort c = (ushort)srcPixels[sy, sx];
                            r += (c >> 10) & 0x1F;
                            g += (c >> 5) & 0x1F;
                            b += c & 0x1F;
                            count++;
                        }
                    }

                    SamplePixel(srcY1, srcX1);
                    SamplePixel(srcY1, srcX2);
                    SamplePixel(srcY2, srcX1);
                    SamplePixel(srcY2, srcX2);

                    if (count > 0)
                    {
                        r /= count;
                        g /= count;
                        b /= count;
                        dstPixels[dstY, dstX] = (r << 10) | (g << 5) | b;
                    }
                }
            }

            // 重新編碼回壓縮格式
            return EncodeCompressedBlock(dstPixels, type, dstSize);
        }

        /// <summary>
        /// 處理混合格式的 block：像素已經是 24x24 尺寸，但座標使用 48x48 系統
        /// 只需要將座標除以 2 即可
        /// </summary>
        private static byte[] DownscaleHybridBlock(byte[] blockData, byte type)
        {
            if (blockData.Length < 6)
                return blockData;

            // 讀取原始 header
            byte x_offset = blockData[1];
            byte y_offset = blockData[2];
            byte xxLen = blockData[3];
            byte yLen = blockData[4];

            // 將座標縮放到 24x24 系統
            byte new_x_offset = (byte)(x_offset / 2);
            byte new_y_offset = (byte)(y_offset / 2);
            // xxLen 和 yLen 保持不變（表示像素數量，不是座標）

            // 解碼像素到陣列，使用新座標系統
            const int dstSize = 24;
            int[,] dstPixels = new int[dstSize, dstSize];
            for (int y = 0; y < dstSize; y++)
                for (int x = 0; x < dstSize; x++)
                    dstPixels[y, x] = -1;

            int idx = 5;  // 跳過 header

            for (int ty = 0; ty < yLen && idx < blockData.Length - 1; ty++)
            {
                int tx = x_offset;  // 使用原始座標來讀取
                byte xSegmentCount = blockData[idx++];

                for (int nx = 0; nx < xSegmentCount && idx < blockData.Length - 2; nx++)
                {
                    int skip = blockData[idx++] / 2;
                    tx += skip;
                    int xLen = blockData[idx++];

                    for (int p = 0; p < xLen && idx + 1 < blockData.Length; p++)
                    {
                        ushort color = (ushort)(blockData[idx] | (blockData[idx + 1] << 8));
                        idx += 2;

                        // 將座標從 48x48 系統轉換到 24x24 系統
                        int pixY = (ty + y_offset) / 2;
                        int pixX = tx / 2;

                        if (pixY >= 0 && pixY < dstSize && pixX >= 0 && pixX < dstSize)
                        {
                            dstPixels[pixY, pixX] = color;
                        }
                        tx++;
                    }
                }
            }

            // 重新編碼回壓縮格式
            return EncodeCompressedBlock(dstPixels, type, dstSize);
        }

        /// <summary>
        /// 將像素陣列編碼回壓縮格式
        /// </summary>
        private static byte[] EncodeCompressedBlock(int[,] pixels, byte type, int size)
        {
            var result = new List<byte>();
            result.Add(type);

            // 計算有效區域的 bounding box
            int minX = size, minY = size, maxX = -1, maxY = -1;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (pixels[y, x] >= 0)
                    {
                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            // 如果沒有有效像素，返回空 block
            if (maxX < 0)
            {
                result.Add(0);  // x_offset
                result.Add(0);  // y_offset
                result.Add(0);  // xxLen
                result.Add(0);  // yLen
                result.Add(0);  // Parse 多讀的 1 byte
                return result.ToArray();
            }

            byte x_offset = (byte)minX;
            byte y_offset = (byte)minY;
            byte xxLen = (byte)(maxX - minX + 1);
            byte yLen = (byte)(maxY - minY + 1);

            result.Add(x_offset);
            result.Add(y_offset);
            result.Add(xxLen);
            result.Add(yLen);

            // 編碼每一行
            for (int y = minY; y <= maxY; y++)
            {
                // 找出這一行的所有 segment
                var segments = new List<(int start, List<ushort> pixels)>();
                int x = minX;
                while (x <= maxX)
                {
                    // 跳過透明像素
                    while (x <= maxX && pixels[y, x] < 0)
                        x++;

                    if (x > maxX)
                        break;

                    // 收集連續的非透明像素
                    int startX = x;
                    var segmentPixels = new List<ushort>();
                    while (x <= maxX && pixels[y, x] >= 0)
                    {
                        segmentPixels.Add((ushort)pixels[y, x]);
                        x++;
                    }
                    segments.Add((startX, segmentPixels));
                }

                // 寫入 segment count
                result.Add((byte)segments.Count);

                int currentX = x_offset;
                foreach (var seg in segments)
                {
                    // 寫入跳過的像素數 (乘以 2，因為原始格式是這樣)
                    int skip = seg.start - currentX;
                    result.Add((byte)(skip * 2));

                    // 寫入這個 segment 的像素數
                    result.Add((byte)seg.pixels.Count);

                    // 寫入像素資料
                    foreach (var color in seg.pixels)
                    {
                        result.Add((byte)(color & 0xFF));
                        result.Add((byte)((color >> 8) & 0xFF));
                    }

                    currentX = seg.start + seg.pixels.Count;
                }
            }

            // 加上 Parse 多讀的 1 byte
            result.Add(0);

            return result.ToArray();
        }

        /// <summary>
        /// 將整個 R 版 til 檔案縮小成 Classic 版
        /// </summary>
        public static byte[] DownscaleTil(byte[] tilData)
        {
            if (!IsRemaster(tilData))
                return tilData; // 已經是 Classic 版，不需縮小

            var blocks = Parse(tilData);
            var downscaledBlocks = new List<byte[]>();

            foreach (var block in blocks)
            {
                downscaledBlocks.Add(DownscaleBlock(block));
            }

            // 重新組裝 til 檔案
            return BuildTil(downscaledBlocks);
        }

        /// <summary>
        /// 從 block 列表組裝 til 檔案
        /// Parse 讀取的 block 會多 1 byte，所以寫入時要扣掉
        /// </summary>
        public static byte[] BuildTil(List<byte[]> blocks)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // 寫入 block 數量
                bw.Write(blocks.Count);

                // 計算並寫入偏移量 (blockCount + 1 個偏移量)
                int currentOffset = 0;
                for (int i = 0; i < blocks.Count; i++)
                {
                    bw.Write(currentOffset);
                    currentOffset += blocks[i].Length - 1; // 實際資料長度 (Parse 多讀了 1 byte)
                }
                // 最後一個偏移量是結尾位置
                bw.Write(currentOffset);

                // 寫入 block 資料
                foreach (var block in blocks)
                {
                    // Parse 多讀了 1 byte，寫入時扣掉
                    int writeLen = block.Length - 1;
                    if (writeLen > 0)
                        bw.Write(block, 0, writeLen);
                }

                return ms.ToArray();
            }
        }

        //畫大地圖用的(將.til檔案 拆成更小的單位)
        public static List<byte[]> Parse(byte[] srcData) {
            List<byte[]> result = new List<byte[]>();
            try {
                using (BinaryReader br = new BinaryReader(new MemoryStream(srcData))) {

                    // 取得Block數量. 
                    int nAllBlockCount = br.ReadInt32();

                    int[] nsBlockOffset = new int[nAllBlockCount + 1];
                    for (int i = 0; i <= nAllBlockCount; i++) {
                        nsBlockOffset[i] = br.ReadInt32();// 載入Block的偏移位置.
                    }
                   
                    int nCurPosition = (int)br.BaseStream.Position;

                    // 載入Block的資料.
                    for (int i = 0; i < nAllBlockCount; i++) {
                        int nPosition = nCurPosition + nsBlockOffset[i];
                        br.BaseStream.Seek(nPosition, SeekOrigin.Begin);

                        int nSize = nsBlockOffset[i + 1] - nsBlockOffset[i];
                        if (nSize <= 0) {
                            nSize = srcData.Length - nsBlockOffset[i];
                        }

                        // int type = br.ReadByte();
                        byte[] data = br.ReadBytes(nSize + 1);
                        result.Add(data);
                    }
                }
            } catch {
                // Utils.outputText("L1Til_Parse發生問題的檔案:" + logFileName, "Log.txt");
            }
            return result;
        }
    }
}


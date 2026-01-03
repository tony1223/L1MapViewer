using System;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// 通行性判定服務 - 處理對角方向通行性的純邏輯判斷
    /// </summary>
    public static class PassabilityService
    {
        /// <summary>
        /// 檢查 D1 方向（右上）的通行性
        /// </summary>
        /// <param name="t1">屬性陣列 1</param>
        /// <param name="t3">屬性陣列 3</param>
        /// <param name="x">X 座標</param>
        /// <param name="y">Y 座標</param>
        /// <param name="xLen">X 長度</param>
        /// <param name="yLen">Y 長度</param>
        public static bool IsPassable_D1(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 0 || x + 1 >= xLen || y < 0 || y >= yLen || y - 1 < 0) return false;
            return (t1[x, y] & 1) == 0 && (t1[x + 1, y] & 1) == 0 &&
                   (t3[x + 1, y] & 1) == 0 && (t3[x + 1, y - 1] & 1) == 0;
        }

        /// <summary>
        /// 檢查 D3 方向（右下）的通行性
        /// </summary>
        public static bool IsPassable_D3(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 0 || x + 1 >= xLen || y < 0 || y + 1 >= yLen) return false;
            return (t1[x, y + 1] & 1) == 0 && (t1[x + 1, y + 1] & 1) == 0 &&
                   (t3[x, y] & 1) == 0 && (t3[x, y + 1] & 1) == 0;
        }

        /// <summary>
        /// 檢查 D5 方向（左下）的通行性
        /// </summary>
        public static bool IsPassable_D5(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 1 || x >= xLen || y < 0 || y + 1 >= yLen) return false;
            return (t1[x, y + 1] & 1) == 0 && (t1[x - 1, y + 1] & 1) == 0 &&
                   (t3[x - 1, y] & 1) == 0 && (t3[x - 1, y + 1] & 1) == 0;
        }

        /// <summary>
        /// 檢查 D7 方向（左上）的通行性
        /// </summary>
        public static bool IsPassable_D7(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 1 || x >= xLen || y < 1 || y >= yLen) return false;
            return (t1[x, y] & 1) == 0 && (t1[x - 1, y] & 1) == 0 &&
                   (t3[x - 1, y] & 1) == 0 && (t3[x - 1, y - 1] & 1) == 0;
        }

        /// <summary>
        /// 替換例外值（完全按照 MapTool 的 replaceException 邏輯）
        /// 某些特殊屬性值需要替換為 5
        /// </summary>
        public static int ReplaceException(int value)
        {
            if (value == 65 || value == 69 || value == 73 || value == 33 || value == 77)
                return 5;
            return value;
        }

        /// <summary>
        /// 取得區域類型（完全按照 MapTool 的 getZone 邏輯）
        /// 看 tileValue 的低 4 位元
        /// </summary>
        /// <returns>256=一般區域, 512=安全區域, 1024=戰鬥區域</returns>
        public static int GetZone(int tileValue)
        {
            int lowNibble = tileValue & 0x0F;
            // 0-3: 一般區域 (256)
            if (lowNibble >= 0x00 && lowNibble <= 0x03)
                return 256;
            // 4-7, C-F: 安全區域 (512)
            if ((lowNibble >= 0x04 && lowNibble <= 0x07) ||
                (lowNibble >= 0x0C && lowNibble <= 0x0F))
                return 512;
            // 8-B: 戰鬥區域 (1024)
            if (lowNibble >= 0x08 && lowNibble <= 0x0B)
                return 1024;
            return 256;
        }
    }
}

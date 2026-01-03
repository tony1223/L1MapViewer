using System.Collections.Generic;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// Layer3 屬性解碼器 - 解析屬性 bit 標記
    /// </summary>
    public static class Layer3AttributeDecoder
    {
        /// <summary>
        /// 取得屬性標記說明
        /// </summary>
        public static string GetAttributeFlags(short value)
        {
            List<string> flags = new List<string>();

            if ((value & 0x0001) != 0) flags.Add("不可通行");

            // MapTool 邏輯: 低4位 4-7,C-F=安全, 8-B=戰鬥
            int lowNibble = value & 0x0F;
            if ((lowNibble & 0x04) != 0) flags.Add("安全區");
            else if ((lowNibble & 0x0C) == 0x08) flags.Add("戰鬥區");

            if ((value & 0x0002) != 0) flags.Add("bit1");
            if ((value & 0x0010) != 0) flags.Add("bit4");
            if ((value & 0x0020) != 0) flags.Add("bit5");
            if ((value & 0x0040) != 0) flags.Add("bit6");
            if ((value & 0x0080) != 0) flags.Add("bit7");
            if ((value & 0x0100) != 0) flags.Add("bit8");
            if ((value & 0x0200) != 0) flags.Add("bit9");
            if ((value & 0x0400) != 0) flags.Add("bit10");
            if ((value & 0x0800) != 0) flags.Add("bit11");
            if ((value & 0x1000) != 0) flags.Add("bit12");
            if ((value & 0x2000) != 0) flags.Add("bit13");
            if ((value & 0x4000) != 0) flags.Add("bit14");
            if ((value & 0x8000) != 0) flags.Add("bit15");

            if (flags.Count == 0) flags.Add("無標記(可通行)");

            return string.Join(", ", flags);
        }

        /// <summary>
        /// 檢查是否為不可通行
        /// </summary>
        public static bool IsBlocked(short value)
        {
            return (value & 0x0001) != 0;
        }

        /// <summary>
        /// 檢查是否為安全區
        /// </summary>
        public static bool IsSafeZone(short value)
        {
            int lowNibble = value & 0x0F;
            return (lowNibble & 0x04) != 0;
        }

        /// <summary>
        /// 檢查是否為戰鬥區
        /// </summary>
        public static bool IsCombatZone(short value)
        {
            int lowNibble = value & 0x0F;
            return !IsSafeZone(value) && (lowNibble & 0x0C) == 0x08;
        }

        /// <summary>
        /// 取得區域類型名稱
        /// </summary>
        public static string GetZoneName(short value)
        {
            if (IsSafeZone(value)) return "安全區";
            if (IsCombatZone(value)) return "戰鬥區";
            return "一般區";
        }
    }
}

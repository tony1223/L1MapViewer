using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
// using System.Drawing; // Replaced with Eto.Drawing
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static L1MapViewer.Other.Struct;

namespace L1MapViewer {
    /// <summary>
    /// Debug 日誌工具 - 用於診斷載入問題
    /// 日誌檔案會寫入 %TEMP%\L1MapViewer_debug.log
    /// </summary>
    public static class DebugLog {
        private static readonly string _logPath = Path.Combine(Path.GetTempPath(), "L1MapViewer_debug.log");
        private static readonly Stopwatch _sw = Stopwatch.StartNew();
        private static readonly object _lock = new object();

        /// <summary>
        /// 是否啟用 Debug 日誌（預設啟用）
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// 寫入日誌
        /// </summary>
        public static void Log(string message) {
            if (!Enabled) return;
            try {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string elapsed = $"+{_sw.ElapsedMilliseconds}ms";
                string line = $"[{timestamp}] {elapsed,-12} {message}";
                lock (_lock) {
                    File.AppendAllText(_logPath, line + Environment.NewLine);
                }
            } catch {
                // 忽略寫入錯誤
            }
        }

        /// <summary>
        /// 清除日誌檔案（程式啟動時呼叫）
        /// </summary>
        public static void Clear() {
            try {
                if (File.Exists(_logPath)) {
                    File.Delete(_logPath);
                }
                Log($"=== L1MapViewer Debug Log Started ===");
                Log($"Log path: {_logPath}");
            } catch {
                // 忽略錯誤
            }
        }

        /// <summary>
        /// 取得日誌檔案路徑
        /// </summary>
        public static string LogPath => _logPath;
    }

    class Share {
        //共享的天堂資料夾路徑
        public static string LineagePath { get; set; } = string.Empty;

        //共享的idx資料清單
        public static Dictionary<string, Dictionary<string, L1Idx>> IdxDataList = new Dictionary<string, Dictionary<string, L1Idx>>();
        //共享的地圖資料清單
        public static Dictionary<string, L1Map> MapDataList = new Dictionary<string, L1Map>();
        //共享的地圖座標清單
        public static Dictionary<Region, Dictionary<Region, LinLocation>> RegionList = new Dictionary<Region, Dictionary<Region, LinLocation>>();
        //共享的地圖座標清單
        public static Dictionary<Region, Dictionary<Region, LinLocation>> RegionList2 = new Dictionary<Region, Dictionary<Region, LinLocation>>();
        //共享的地圖座標清單
        public static Dictionary<string, LinLocation> LinLocList = new Dictionary<string, LinLocation>();
        //共享的地圖座標清單
        public static Dictionary<string, LinLocation> LinLocList2 = new Dictionary<string, LinLocation>();

        //共享的Npc資料清單
        public static Dictionary<string, DataRow> NpcList = new Dictionary<string, DataRow>();
        //共享的Item資料清單
        public static Dictionary<string, DataRow> ItemList = new Dictionary<string, DataRow>();

        //zone3desc-c.tbl
        public static List<string> Zone3descList = new List<string>();

        //zone3-c.xml/zone3-c.tbl
        public static Dictionary<string, L1Zone> ZoneList = new Dictionary<string, L1Zone>();
    }
}

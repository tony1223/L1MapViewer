using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using L1MapViewer.Other;

namespace L1MapViewer.Models
{
    /// <summary>
    /// 地圖文件 - 管理整張地圖的所有 S32 資料
    /// </summary>
    public class MapDocument
    {
        /// <summary>
        /// 當前地圖 ID
        /// </summary>
        public string MapId { get; set; }

        /// <summary>
        /// 地圖資訊
        /// </summary>
        public Struct.L1Map MapInfo { get; private set; }

        /// <summary>
        /// 所有 S32 資料 (FilePath -> S32Data)
        /// </summary>
        public Dictionary<string, S32Data> S32Files { get; private set; } = new Dictionary<string, S32Data>();

        /// <summary>
        /// S32 檔案顯示項目清單
        /// </summary>
        public List<S32FileItem> S32FileItems { get; private set; } = new List<S32FileItem>();

        /// <summary>
        /// 已勾選顯示的 S32 檔案路徑
        /// </summary>
        public HashSet<string> CheckedS32Files { get; private set; } = new HashSet<string>();

        /// <summary>
        /// 是否有未儲存的修改
        /// </summary>
        public bool HasUnsavedChanges => S32Files.Values.Any(s => s.IsModified);

        /// <summary>
        /// 取得已修改的 S32 檔案清單
        /// </summary>
        public IEnumerable<S32Data> ModifiedS32Files => S32Files.Values.Where(s => s.IsModified);

        /// <summary>
        /// 地圖像素寬度
        /// </summary>
        public int MapPixelWidth { get; private set; }

        /// <summary>
        /// 地圖像素高度
        /// </summary>
        public int MapPixelHeight { get; private set; }

        /// <summary>
        /// 每個區塊的像素寬度 (3072)
        /// </summary>
        public const int BlockPixelWidth = 64 * 24 * 2;

        /// <summary>
        /// 每個區塊的像素高度 (1536)
        /// </summary>
        public const int BlockPixelHeight = 64 * 12 * 2;

        /// <summary>
        /// 文件變更事件
        /// </summary>
        public event EventHandler DocumentChanged;

        /// <summary>
        /// S32 資料變更事件
        /// </summary>
        public event EventHandler<S32DataChangedEventArgs> S32DataChanged;

        /// <summary>
        /// 載入地圖
        /// </summary>
        public bool Load(string mapId)
        {
            if (string.IsNullOrEmpty(mapId) || !Share.MapDataList.ContainsKey(mapId))
                return false;

            MapId = mapId;
            MapInfo = Share.MapDataList[mapId];

            // 計算地圖像素大小
            MapPixelWidth = MapInfo.nBlockCountX * BlockPixelWidth;
            MapPixelHeight = MapInfo.nBlockCountX * BlockPixelHeight / 2 + MapInfo.nBlockCountY * BlockPixelHeight / 2;

            // 清除現有資料
            S32Files.Clear();
            S32FileItems.Clear();
            CheckedS32Files.Clear();

            // 載入所有 S32 檔案
            LoadS32Files();

            DocumentChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// 載入 S32 檔案
        /// </summary>
        private void LoadS32Files()
        {
            string s32Folder = Path.Combine(Share.LineagePath, "Map", MapId);
            if (!Directory.Exists(s32Folder))
                return;

            string[] s32FilePaths = Directory.GetFiles(s32Folder, "*.s32");

            foreach (string filePath in s32FilePaths)
            {
                try
                {
                    S32Data s32Data = CLI.S32Parser.ParseFile(filePath);

                    // 設置 SegInfo
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    if (fileName.Length >= 8)
                    {
                        string blockXStr = fileName.Substring(0, 4);
                        string blockYStr = fileName.Substring(4, 4);
                        if (int.TryParse(blockXStr, System.Globalization.NumberStyles.HexNumber, null, out int blockX) &&
                            int.TryParse(blockYStr, System.Globalization.NumberStyles.HexNumber, null, out int blockY))
                        {
                            var segInfo = new Struct.L1MapSeg(blockX, blockY, true);
                            segInfo.nMapMinBlockX = MapInfo.nMinBlockX;
                            segInfo.nMapMinBlockY = MapInfo.nMinBlockY;
                            segInfo.nMapBlockCountX = MapInfo.nBlockCountX;
                            s32Data.SegInfo = segInfo;
                        }
                    }

                    S32Files[filePath] = s32Data;

                    // 建立顯示項目
                    var fileItem = new S32FileItem
                    {
                        FilePath = filePath,
                        DisplayName = Path.GetFileName(filePath),
                        SegInfo = s32Data.SegInfo,
                        IsChecked = true
                    };
                    S32FileItems.Add(fileItem);
                    CheckedS32Files.Add(filePath);
                }
                catch (Exception)
                {
                    // 忽略無法載入的檔案
                }
            }
        }

        /// <summary>
        /// 重新載入地圖
        /// </summary>
        public void Reload()
        {
            if (!string.IsNullOrEmpty(MapId))
            {
                Load(MapId);
            }
        }

        /// <summary>
        /// 取得指定位置的 S32 資料
        /// </summary>
        public S32Data GetS32AtWorldPosition(int worldX, int worldY)
        {
            foreach (var s32Data in S32Files.Values)
            {
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                if (worldX >= mx && worldX < mx + BlockPixelWidth &&
                    worldY >= my && worldY < my + BlockPixelHeight)
                {
                    return s32Data;
                }
            }
            return null;
        }

        /// <summary>
        /// 取得與指定區域相交的 S32 資料
        /// </summary>
        public IEnumerable<S32Data> GetS32IntersectingRect(int x, int y, int width, int height)
        {
            foreach (var s32Data in S32Files.Values)
            {
                if (!CheckedS32Files.Contains(s32Data.FilePath))
                    continue;

                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 檢查是否相交
                if (mx < x + width && mx + BlockPixelWidth > x &&
                    my < y + height && my + BlockPixelHeight > y)
                {
                    yield return s32Data;
                }
            }
        }

        /// <summary>
        /// 設置 S32 檔案勾選狀態
        /// </summary>
        public void SetS32Checked(string filePath, bool isChecked)
        {
            if (isChecked)
                CheckedS32Files.Add(filePath);
            else
                CheckedS32Files.Remove(filePath);

            var item = S32FileItems.FirstOrDefault(i => i.FilePath == filePath);
            if (item != null)
                item.IsChecked = isChecked;
        }

        /// <summary>
        /// 儲存所有已修改的 S32 檔案
        /// </summary>
        public int SaveAllModified()
        {
            int savedCount = 0;
            foreach (var s32Data in ModifiedS32Files.ToList())
            {
                if (SaveS32File(s32Data))
                    savedCount++;
            }
            return savedCount;
        }

        /// <summary>
        /// 儲存單一 S32 檔案
        /// </summary>
        public bool SaveS32File(S32Data s32Data)
        {
            if (s32Data == null || string.IsNullOrEmpty(s32Data.FilePath))
                return false;

            try
            {
                CLI.S32Writer.Write(s32Data, s32Data.FilePath);
                s32Data.IsModified = false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 標記 S32 資料已變更
        /// </summary>
        public void MarkS32Modified(S32Data s32Data)
        {
            if (s32Data != null)
            {
                s32Data.IsModified = true;
                S32DataChanged?.Invoke(this, new S32DataChangedEventArgs(s32Data));
            }
        }

        /// <summary>
        /// 取得依照渲染順序排序的 S32 檔案路徑
        /// </summary>
        public IEnumerable<string> GetSortedS32FilePaths()
        {
            return Utils.SortDesc(S32Files.Keys).Cast<string>();
        }

        /// <summary>
        /// 卸載地圖
        /// </summary>
        public void Unload()
        {
            MapId = null;
            MapInfo = default;
            S32Files.Clear();
            S32FileItems.Clear();
            CheckedS32Files.Clear();
            MapPixelWidth = 0;
            MapPixelHeight = 0;

            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// S32 資料變更事件參數
    /// </summary>
    public class S32DataChangedEventArgs : EventArgs
    {
        public S32Data S32Data { get; }

        public S32DataChangedEventArgs(S32Data s32Data)
        {
            S32Data = s32Data;
        }
    }
}

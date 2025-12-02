using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using L1MapViewer.Models;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// Layer5 異常檢查結果
    /// </summary>
    public class Layer5CheckResult
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public Layer5Item Item { get; set; }
        public int ItemIndex { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// Layer5 異常檢查器
    /// </summary>
    public static class Layer5Checker
    {
        /// <summary>
        /// 檢查 Layer5 異常
        /// </summary>
        /// <param name="s32Files">S32 檔案字典 (filePath -> S32Data)</param>
        /// <param name="radius">檢查半徑（周圍幾格）</param>
        /// <param name="getSegInfo">取得 SegInfo 的委派（用於計算遊戲座標），如果為 null 則從檔名解析</param>
        /// <returns>異常項目列表</returns>
        public static List<Layer5CheckResult> Check(
            Dictionary<string, S32Data> s32Files,
            int radius = 0,
            Func<S32Data, (int nLinBeginX, int nLinBeginY)?> getSegInfo = null)
        {
            var invalidItems = new List<Layer5CheckResult>();

            if (s32Files.Count == 0)
                return invalidItems;

            // 收集所有 Layer4 的 GroupId
            HashSet<int> validGroupIds = new HashSet<int>();
            foreach (var s32Data in s32Files.Values)
            {
                foreach (var obj in s32Data.Layer4)
                {
                    validGroupIds.Add(obj.GroupId);
                }
            }

            // 建立 (GroupId, 遊戲座標) -> 是否存在 的索引
            var groupAtPosition = new HashSet<(int groupId, int gameX, int gameY)>();
            foreach (var kvp in s32Files)
            {
                S32Data s32Data = kvp.Value;

                // 取得座標資訊
                var segInfo = getSegInfo?.Invoke(s32Data);
                if (segInfo == null)
                {
                    // 從檔名解析
                    segInfo = ParseSegInfoFromFileName(kvp.Key);
                }
                if (segInfo == null) continue;

                int nLinBeginX = segInfo.Value.nLinBeginX;
                int nLinBeginY = segInfo.Value.nLinBeginY;

                foreach (var obj in s32Data.Layer4)
                {
                    // Layer4 的 X 是 Layer1 座標 (0-127)，轉換為遊戲座標
                    int gameX = nLinBeginX + obj.X / 2;
                    int gameY = nLinBeginY + obj.Y;
                    groupAtPosition.Add((obj.GroupId, gameX, gameY));
                }
            }

            // 檢查 Layer5 異常
            foreach (var kvp in s32Files)
            {
                string filePath = kvp.Key;
                string fileName = Path.GetFileName(kvp.Key);
                S32Data s32Data = kvp.Value;

                if (s32Data.Layer5.Count == 0) continue;

                // 取得座標資訊
                var segInfo = getSegInfo?.Invoke(s32Data);
                if (segInfo == null)
                {
                    segInfo = ParseSegInfoFromFileName(filePath);
                }
                if (segInfo == null) continue;

                int nLinBeginX = segInfo.Value.nLinBeginX;
                int nLinBeginY = segInfo.Value.nLinBeginY;

                for (int i = 0; i < s32Data.Layer5.Count; i++)
                {
                    var item = s32Data.Layer5[i];

                    // ObjectIndex 不存在於任何 Layer4 的 GroupId
                    if (!validGroupIds.Contains(item.ObjectIndex))
                    {
                        invalidItems.Add(new Layer5CheckResult
                        {
                            FilePath = filePath,
                            FileName = fileName,
                            Item = item,
                            ItemIndex = i,
                            Reason = "GroupId不存在"
                        });
                        continue;
                    }

                    // Layer5 的 X 是 0-127（Layer1 座標系），Y 是 0-63
                    int l5GameX = nLinBeginX + item.X / 2;
                    int l5GameY = nLinBeginY + item.Y;

                    // 檢查該格及周圍 radius 格是否有對應 GroupId 的物件
                    if (radius != -1)
                    {
                        bool found = false;
                        for (int dx = -radius; dx <= radius && !found; dx++)
                        {
                            for (int dy = -radius; dy <= radius && !found; dy++)
                            {
                                if (groupAtPosition.Contains((item.ObjectIndex, l5GameX + dx, l5GameY + dy)))
                                {
                                    found = true;
                                }
                            }
                        }

                        if (!found)
                        {
                            invalidItems.Add(new Layer5CheckResult
                            {
                                FilePath = filePath,
                                FileName = fileName,
                                Item = item,
                                ItemIndex = i,
                                Reason = "周圍無對應物件"
                            });
                        }
                    }
                }
            }

            return invalidItems;
        }

        /// <summary>
        /// 從檔名解析 SegInfo
        /// </summary>
        private static (int nLinBeginX, int nLinBeginY)? ParseSegInfoFromFileName(string filePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            if (fileName.Length < 8) return null;

            try
            {
                int blockX = Convert.ToInt32(fileName.Substring(0, 4), 16);
                int blockY = Convert.ToInt32(fileName.Substring(4, 4), 16);

                int nLinBeginX = (blockX - 0x7FFF) * 64 + 0x7FFF - 64 + 1;
                int nLinBeginY = (blockY - 0x7FFF) * 64 + 0x7FFF - 64 + 1;

                return (nLinBeginX, nLinBeginY);
            }
            catch
            {
                return null;
            }
        }
    }
}

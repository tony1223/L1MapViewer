using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static L1MapViewer.Other.Struct;

namespace L1MapViewer.Reader
{
    /// <summary>
    /// 寫入 idx/pak 檔案
    /// </summary>
    public static class L1PakWriter
    {
        /// <summary>
        /// idx 記錄結構
        /// </summary>
        private class IdxRecord
        {
            public string FileName { get; set; }
            public int Position { get; set; }
            public int Size { get; set; }
            public int CompressSize { get; set; }
            public int CompressType { get; set; }
        }

        /// <summary>
        /// 特殊排序比較器 - 底線 (_) 排在字母之前 (數字 → 底線 → 字母)
        /// </summary>
        private class UnderscoreFirstComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                int minLen = Math.Min(x.Length, y.Length);
                for (int i = 0; i < minLen; i++)
                {
                    char cx = char.ToLowerInvariant(x[i]);
                    char cy = char.ToLowerInvariant(y[i]);

                    if (cx == cy) continue;

                    // 底線排在字母之前
                    int ox = GetOrder(cx);
                    int oy = GetOrder(cy);

                    if (ox != oy) return ox.CompareTo(oy);
                    return cx.CompareTo(cy);
                }
                return x.Length.CompareTo(y.Length);
            }

            private int GetOrder(char c)
            {
                // 其他符號 → -1
                // 數字 0-9 → 0
                // 底線 _ → 1  (在字母之前)
                // 字母 a-z → 2
                if (c >= '0' && c <= '9') return 0;
                if (c == '_') return 1;
                if (c >= 'a' && c <= 'z') return 2;
                return c < '0' ? -1 : 3;
            }
        }

        /// <summary>
        /// 使用二分搜尋找到插入位置
        /// </summary>
        private static int FindInsertIndex(List<IdxRecord> records, string fileName, IComparer<string> comparer)
        {
            int left = 0;
            int right = records.Count;

            while (left < right)
            {
                int mid = (left + right) / 2;
                if (comparer.Compare(records[mid].FileName, fileName) < 0)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid;
                }
            }
            return left;
        }

        /// <summary>
        /// 取得 idx 檔案的結構類型
        /// </summary>
        public static IdxType GetIdxType(string idxFilePath)
        {
            if (!File.Exists(idxFilePath))
                return IdxType.OLD;

            byte[] head = new byte[4];
            using (var fs = File.OpenRead(idxFilePath))
            {
                fs.Read(head, 0, 4);
            }

            string headStr = Encoding.Default.GetString(head).ToLower();
            if (headStr == "_ext")
                return IdxType.EXT;
            else if (headStr == "_rms")
                return IdxType.RMS;
            else
                return IdxType.OLD;
        }

        /// <summary>
        /// 讀取現有的 idx 記錄
        /// </summary>
        private static List<IdxRecord> ReadIdxRecords(string idxFilePath, IdxType structType)
        {
            var records = new List<IdxRecord>();

            if (!File.Exists(idxFilePath))
                return records;

            byte[] data = File.ReadAllBytes(idxFilePath);

            int headerSize = (structType == IdxType.OLD) ? 4 : 8;
            int recordSize = GetRecordSize(structType);

            if (data.Length < headerSize)
                return records;

            using (var br = new BinaryReader(new MemoryStream(data)))
            {
                br.BaseStream.Seek(headerSize, SeekOrigin.Begin);

                while (br.BaseStream.Position + recordSize <= data.Length)
                {
                    var record = new IdxRecord();

                    if (structType == IdxType.EXT)
                    {
                        record.Position = br.ReadInt32();
                        record.Size = br.ReadInt32();
                        record.CompressSize = br.ReadInt32();
                        record.CompressType = br.ReadInt32();
                        record.FileName = Encoding.Default.GetString(br.ReadBytes(112)).TrimEnd('\0');
                    }
                    else if (structType == IdxType.RMS)
                    {
                        record.Position = br.ReadInt32();
                        record.Size = br.ReadInt32();
                        record.CompressSize = br.ReadInt32();
                        record.CompressType = br.ReadInt32();
                        record.FileName = Encoding.Default.GetString(br.ReadBytes(260)).TrimEnd('\0');
                    }
                    else
                    {
                        record.Position = br.ReadInt32();
                        record.FileName = Encoding.Default.GetString(br.ReadBytes(20)).TrimEnd('\0');
                        record.Size = br.ReadInt32();
                    }

                    if (!string.IsNullOrEmpty(record.FileName))
                    {
                        records.Add(record);
                    }
                }
            }

            return records;
        }

        /// <summary>
        /// 取得記錄大小
        /// </summary>
        private static int GetRecordSize(IdxType structType)
        {
            switch (structType)
            {
                case IdxType.EXT: return 128;
                case IdxType.RMS: return 276;
                default: return 28;
            }
        }

        /// <summary>
        /// 重建 idx 檔案（寫入排序後的記錄）
        /// </summary>
        private static void RebuildIdx(string idxFilePath, IdxType structType, List<IdxRecord> records)
        {
            int recordSize = GetRecordSize(structType);
            int headerSize = (structType == IdxType.OLD) ? 4 : 8;

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // 寫入 header
                if (structType == IdxType.EXT)
                {
                    bw.Write(Encoding.Default.GetBytes("_EXT"));
                    bw.Write(records.Count);
                }
                else if (structType == IdxType.RMS)
                {
                    bw.Write(Encoding.Default.GetBytes("_RMS"));
                    bw.Write(records.Count);
                }
                else
                {
                    bw.Write(records.Count);
                }

                // 寫入記錄
                foreach (var record in records)
                {
                    if (structType == IdxType.EXT)
                    {
                        bw.Write(record.Position);
                        bw.Write(record.Size);
                        bw.Write(record.CompressSize);
                        bw.Write(record.CompressType);
                        byte[] nameBytes = new byte[112];
                        byte[] srcBytes = Encoding.Default.GetBytes(record.FileName);
                        Array.Copy(srcBytes, nameBytes, Math.Min(srcBytes.Length, 112));
                        bw.Write(nameBytes);
                    }
                    else if (structType == IdxType.RMS)
                    {
                        bw.Write(record.Position);
                        bw.Write(record.Size);
                        bw.Write(record.CompressSize);
                        bw.Write(record.CompressType);
                        byte[] nameBytes = new byte[260];
                        byte[] srcBytes = Encoding.Default.GetBytes(record.FileName);
                        Array.Copy(srcBytes, nameBytes, Math.Min(srcBytes.Length, 260));
                        bw.Write(nameBytes);
                    }
                    else
                    {
                        bw.Write(record.Position);
                        byte[] nameBytes = new byte[20];
                        byte[] srcBytes = Encoding.Default.GetBytes(record.FileName);
                        Array.Copy(srcBytes, nameBytes, Math.Min(srcBytes.Length, 20));
                        bw.Write(nameBytes);
                        bw.Write(record.Size);
                    }
                }

                File.WriteAllBytes(idxFilePath, ms.ToArray());
            }
        }

        /// <summary>
        /// 更新 pak 中已存在的檔案（將新資料追加到 pak 末尾，更新 idx 記錄）
        /// </summary>
        /// <param name="idxType">Tile, Text, etc.</param>
        /// <param name="fileName">檔案名稱 (如 list.til)</param>
        /// <param name="data">新的檔案資料</param>
        /// <returns>是否成功</returns>
        public static bool UpdateFile(string idxType, string fileName, byte[] data)
        {
            string idxFilePath = Path.Combine(Share.LineagePath, $"{idxType}.idx");
            string pakFilePath = Path.Combine(Share.LineagePath, $"{idxType}.pak");

            if (!File.Exists(idxFilePath) || !File.Exists(pakFilePath))
                return false;

            // 檢查檔案是否存在
            var existingRecord = L1IdxReader.Find(idxType, fileName);
            if (existingRecord == null)
            {
                // 檔案不存在，使用 AppendFile 新增
                return AppendFile(idxType, fileName, data);
            }

            IdxType structType = GetIdxType(idxFilePath);

            // 建立備份
            string pakBackup = pakFilePath + ".bak";
            string idxBackup = idxFilePath + ".bak";

            try
            {
                if (File.Exists(pakBackup)) File.Delete(pakBackup);
                if (File.Exists(idxBackup)) File.Delete(idxBackup);

                File.Copy(pakFilePath, pakBackup);
                File.Copy(idxFilePath, idxBackup);
            }
            catch
            {
                // 備份失敗，繼續執行
            }

            try
            {
                // 讀取現有記錄
                var records = ReadIdxRecords(idxFilePath, structType);

                // 找到要更新的記錄
                int targetIndex = -1;
                for (int i = 0; i < records.Count; i++)
                {
                    if (string.Equals(records[i].FileName, fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetIndex = i;
                        break;
                    }
                }

                if (targetIndex < 0)
                    return false;

                // 取得 pak 檔案目前大小作為新偏移
                var pakFileInfo = new FileInfo(pakFilePath);
                int newOffset = (int)pakFileInfo.Length;

                // 附加新資料到 pak 末尾
                using (var pakFs = new FileStream(pakFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    pakFs.Write(data, 0, data.Length);
                }

                // 更新記錄（指向新位置）
                records[targetIndex].Position = newOffset;
                records[targetIndex].Size = data.Length;
                records[targetIndex].CompressSize = 0;
                records[targetIndex].CompressType = 0;

                // 重建 idx
                RebuildIdx(idxFilePath, structType, records);

                // 成功後刪除備份
                try
                {
                    if (File.Exists(pakBackup)) File.Delete(pakBackup);
                    if (File.Exists(idxBackup)) File.Delete(idxBackup);
                }
                catch { }

                // 清除緩存
                if (Share.IdxDataList.ContainsKey(idxType))
                {
                    Share.IdxDataList.Remove(idxType);
                }

                return true;
            }
            catch
            {
                // 發生錯誤，從備份還原
                try
                {
                    if (File.Exists(pakBackup))
                    {
                        File.Copy(pakBackup, pakFilePath, true);
                        File.Delete(pakBackup);
                    }
                    if (File.Exists(idxBackup))
                    {
                        File.Copy(idxBackup, idxFilePath, true);
                        File.Delete(idxBackup);
                    }
                }
                catch { }

                return false;
            }
        }

        /// <summary>
        /// 將檔案附加到 pak 並更新 idx
        /// </summary>
        /// <param name="idxType">Tile, Text, etc.</param>
        /// <param name="fileName">檔案名稱 (如 5050.til)</param>
        /// <param name="data">檔案資料</param>
        /// <returns>是否成功</returns>
        public static bool AppendFile(string idxType, string fileName, byte[] data)
        {
            var files = new Dictionary<string, byte[]> { { fileName, data } };
            return AppendFiles(idxType, files) == 1;
        }

        /// <summary>
        /// 批次將多個檔案附加到 pak 並更新 idx
        /// </summary>
        public static int AppendFiles(string idxType, Dictionary<string, byte[]> files)
        {
            if (files.Count == 0)
                return 0;

            string idxFilePath = Path.Combine(Share.LineagePath, $"{idxType}.idx");
            string pakFilePath = Path.Combine(Share.LineagePath, $"{idxType}.pak");

            if (!File.Exists(idxFilePath) || !File.Exists(pakFilePath))
                return 0;

            IdxType structType = GetIdxType(idxFilePath);

            // 建立備份
            string pakBackup = pakFilePath + ".bak";
            string idxBackup = idxFilePath + ".bak";

            try
            {
                if (File.Exists(pakBackup)) File.Delete(pakBackup);
                if (File.Exists(idxBackup)) File.Delete(idxBackup);

                File.Copy(pakFilePath, pakBackup);
                File.Copy(idxFilePath, idxBackup);
            }
            catch
            {
                // 備份失敗，繼續執行（不中斷）
            }

            try
            {
                // 讀取現有記錄
                var records = ReadIdxRecords(idxFilePath, structType);

                // 取得 pak 檔案目前大小作為起始偏移
                var pakFileInfo = new FileInfo(pakFilePath);
                int currentOffset = (int)pakFileInfo.Length;

                int successCount = 0;
                var newRecords = new List<IdxRecord>();

                // 附加資料到 pak 並收集新記錄
                using (var pakFs = new FileStream(pakFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    foreach (var kvp in files)
                    {
                        string fileName = kvp.Key;
                        byte[] data = kvp.Value;

                        pakFs.Write(data, 0, data.Length);

                        newRecords.Add(new IdxRecord
                        {
                            FileName = fileName,
                            Position = currentOffset,
                            Size = data.Length,
                            CompressSize = 0,
                            CompressType = 0
                        });

                        currentOffset += data.Length;
                        successCount++;
                    }
                }

                // 使用二分搜尋將新記錄插入到正確位置
                var comparer = new UnderscoreFirstComparer();
                foreach (var newRec in newRecords)
                {
                    int insertIndex = FindInsertIndex(records, newRec.FileName, comparer);
                    records.Insert(insertIndex, newRec);
                }

                // 重建 idx（已排序的記錄）
                RebuildIdx(idxFilePath, structType, records);

                // 成功後刪除備份
                try
                {
                    if (File.Exists(pakBackup)) File.Delete(pakBackup);
                    if (File.Exists(idxBackup)) File.Delete(idxBackup);
                }
                catch { }

                // 清除緩存
                if (Share.IdxDataList.ContainsKey(idxType))
                {
                    Share.IdxDataList.Remove(idxType);
                }

                return successCount;
            }
            catch
            {
                // 發生錯誤，從備份還原
                try
                {
                    if (File.Exists(pakBackup))
                    {
                        File.Copy(pakBackup, pakFilePath, true);
                        File.Delete(pakBackup);
                    }
                    if (File.Exists(idxBackup))
                    {
                        File.Copy(idxBackup, idxFilePath, true);
                        File.Delete(idxBackup);
                    }
                }
                catch { }

                return 0;
            }
        }

        /// <summary>
        /// 檢查檔案是否已存在於 idx 中
        /// </summary>
        public static bool FileExists(string idxType, string fileName)
        {
            return L1IdxReader.Find(idxType, fileName) != null;
        }
    }
}

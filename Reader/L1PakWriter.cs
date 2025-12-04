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
        /// 將檔案附加到 pak 並更新 idx
        /// </summary>
        /// <param name="idxType">Tile, Text, etc.</param>
        /// <param name="fileName">檔案名稱 (如 5050.til)</param>
        /// <param name="data">檔案資料</param>
        /// <returns>是否成功</returns>
        public static bool AppendFile(string idxType, string fileName, byte[] data)
        {
            string idxFilePath = Path.Combine(Share.LineagePath, $"{idxType}.idx");
            string pakFilePath = Path.Combine(Share.LineagePath, $"{idxType}.pak");

            if (!File.Exists(idxFilePath) || !File.Exists(pakFilePath))
                return false;

            IdxType structType = GetIdxType(idxFilePath);

            // 附加資料到 pak 檔尾端
            long position;
            using (var pakFs = new FileStream(pakFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                position = pakFs.Position;
                pakFs.Write(data, 0, data.Length);
            }

            // 建立新的 idx 項目並附加到 idx 檔尾端
            byte[] idxEntry = CreateIdxEntry(structType, fileName, (int)position, data.Length);
            using (var idxFs = new FileStream(idxFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                idxFs.Write(idxEntry, 0, idxEntry.Length);
            }

            // 清除緩存，讓下次讀取能重新載入
            if (Share.IdxDataList.ContainsKey(idxType))
            {
                Share.IdxDataList.Remove(idxType);
            }

            return true;
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
            int successCount = 0;

            // 收集所有 idx 項目
            var idxEntries = new List<byte[]>();

            using (var pakFs = new FileStream(pakFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                foreach (var kvp in files)
                {
                    string fileName = kvp.Key;
                    byte[] data = kvp.Value;

                    long position = pakFs.Position;
                    pakFs.Write(data, 0, data.Length);

                    byte[] idxEntry = CreateIdxEntry(structType, fileName, (int)position, data.Length);
                    idxEntries.Add(idxEntry);
                    successCount++;
                }
            }

            // 批次寫入 idx
            using (var idxFs = new FileStream(idxFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                foreach (var entry in idxEntries)
                {
                    idxFs.Write(entry, 0, entry.Length);
                }
            }

            // 清除緩存
            if (Share.IdxDataList.ContainsKey(idxType))
            {
                Share.IdxDataList.Remove(idxType);
            }

            return successCount;
        }

        /// <summary>
        /// 建立 idx 項目
        /// </summary>
        private static byte[] CreateIdxEntry(IdxType structType, string fileName, int position, int size)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                if (structType == IdxType.EXT)
                {
                    // 128 bytes: position(4) + size(4) + compressSize(4) + compressType(4) + fileName(112)
                    bw.Write(position);
                    bw.Write(size);
                    bw.Write(0); // compressSize = 0 (未壓縮)
                    bw.Write(0); // compressType = 0 (未壓縮)
                    byte[] nameBytes = new byte[112];
                    byte[] srcBytes = Encoding.Default.GetBytes(fileName);
                    Array.Copy(srcBytes, nameBytes, Math.Min(srcBytes.Length, 112));
                    bw.Write(nameBytes);
                }
                else if (structType == IdxType.RMS)
                {
                    // 276 bytes: position(4) + size(4) + compressSize(4) + compressType(4) + fileName(260)
                    bw.Write(position);
                    bw.Write(size);
                    bw.Write(0); // compressSize = 0 (未壓縮)
                    bw.Write(0); // compressType = 0 (未壓縮)
                    byte[] nameBytes = new byte[260];
                    byte[] srcBytes = Encoding.Default.GetBytes(fileName);
                    Array.Copy(srcBytes, nameBytes, Math.Min(srcBytes.Length, 260));
                    bw.Write(nameBytes);
                }
                else
                {
                    // OLD: 28 bytes: position(4) + fileName(20) + size(4)
                    bw.Write(position);
                    byte[] nameBytes = new byte[20];
                    byte[] srcBytes = Encoding.Default.GetBytes(fileName);
                    Array.Copy(srcBytes, nameBytes, Math.Min(srcBytes.Length, 20));
                    bw.Write(nameBytes);
                    bw.Write(size);
                }

                return ms.ToArray();
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Eto.Forms;
using Eto.Drawing;
using L1FlyMapViewer;
using static L1MapViewer.Other.Struct;

namespace L1MapViewer.Reader {
    class L1IdxReader {

        //讀取指定檔案的idx資料
        public static L1Idx? Find(string szIdxType, string szFileName) {
            if (string.IsNullOrEmpty(szFileName)) {
                return null;
            }
            szFileName = szFileName.ToLower();
            Dictionary<string, L1Idx> result = Load(szIdxType);

            if (!result.ContainsKey(szFileName)) {
                return null;
            }
            return result[szFileName.ToLower()];
        }

        //取得指定類型的所有idx資料
        public static Dictionary<string, L1Idx> GetAll(string szIdxType) {
            return Load(szIdxType);
        }

        //讀取idx資料 並放入緩存
        private static Dictionary<string, L1Idx> Load(string szIdxType) {
            Dictionary<string, L1Idx> result = new Dictionary<string, L1Idx>();

            DebugLog.Log($"[L1IdxReader.Load] Loading idx type: {szIdxType}");

            //緩存內已經有了 直接回傳
            if (Share.IdxDataList.ContainsKey(szIdxType)) {
                DebugLog.Log($"[L1IdxReader.Load] Already cached: {szIdxType}, count={Share.IdxDataList[szIdxType].Count}");
                return Share.IdxDataList[szIdxType];
            }
            //idx/pak檔的路徑
            string szIdxFullName = string.Format(@"{0}\{1}.idx", Share.LineagePath, szIdxType);
            string szPakFullName = szIdxFullName.Replace(".idx", ".pak");

            DebugLog.Log($"[L1IdxReader.Load] Idx path: {szIdxFullName}");
            DebugLog.Log($"[L1IdxReader.Load] Pak path: {szPakFullName}");

            //兩個路徑都需要存在
            if (File.Exists(szIdxFullName) && File.Exists(szPakFullName)) {
                DebugLog.Log($"[L1IdxReader.Load] Files exist, reading idx...");
                byte[] data = File.ReadAllBytes(szIdxFullName);
                DebugLog.Log($"[L1IdxReader.Load] Idx file size: {data.Length} bytes");

                //取前4個byte
                byte[] head = new byte[4];
                Array.Copy(data, 0, head, 0, head.Length);

                //判斷idx的結構類型
                IdxType structType = IdxType.OLD;
                if (data.Length >= 6 && data[0] == '_' && data[1] == 'E' && data[2] == 'X' &&
                    data[3] == 'T' && data[4] == 'B' && data[5] == '$') {
                    // _EXTB$ 格式 (Extended Index Block)
                    structType = IdxType.EXTB;
                } else if (Encoding.Default.GetString(head).ToLower().Equals("_ext")) {
                    structType = IdxType.EXT;
                } else if (Encoding.Default.GetString(head).ToLower().Equals("_rms")) {
                    structType = IdxType.RMS;
                }

                int nBaseOffset = 0;
                bool isDesEncode = false;

                // _EXTB$ 格式使用不同的解析邏輯
                if (structType == IdxType.EXTB) {
                    result = LoadExtB(data, szPakFullName);
                    return Share.IdxDataList[szIdxType] = result;
                }

                //text.idx -->先用des解密出明碼
                if (szIdxType.ToLower().Equals("text")) {
                    int nBeginIndex = (structType == IdxType.OLD) ? 4 : 8;
                    isDesEncode = true;
                    //回傳的陣列已經移除開頭的byte了
                    data = Algorithm.DecodeDes(data, nBeginIndex);
                    nBaseOffset = 0;
                } else {
                    nBaseOffset = (structType == IdxType.OLD) ? 4 : 8;
                }
                using (BinaryReader br = new BinaryReader(new MemoryStream(data))) {
                    try {
                        br.BaseStream.Seek(nBaseOffset, SeekOrigin.Begin);

                        while (br.BaseStream.Position < data.Length) {
                            L1Idx pIdx = new L1Idx(structType);

                            pIdx.nPosition = br.ReadInt32(); //此檔案在pak檔的位置
                            pIdx.szIdxFullName = szIdxFullName;
                            pIdx.szPakFullName = szPakFullName;
                            pIdx.isDesEncode = isDesEncode;

                            if (structType == IdxType.EXT) {
                                // 128byte
                                pIdx.nSize = br.ReadInt32();
                                pIdx.nCompressSize = br.ReadInt32();
                                pIdx.nCompressType = br.ReadInt32(); //0:none  1:zlib1  2:brotli
                                pIdx.szFileName = Encoding.Default.GetString(br.ReadBytes(112)).Replace('\0', ' ').Trim();
                            } else if (structType == IdxType.RMS) {
                                //276byte
                                pIdx.nSize = br.ReadInt32();
                                pIdx.nCompressSize = br.ReadInt32();
                                pIdx.nCompressType = br.ReadInt32(); //0:none  1:zlib1  2:brotli
                                pIdx.szFileName = Encoding.Default.GetString(br.ReadBytes(260)).Replace('\0', ' ').Trim();
                            } else {
                                //28 byte
                                pIdx.szFileName = Encoding.Default.GetString(br.ReadBytes(20)).Replace('\0', ' ').Trim();
                                pIdx.nSize = br.ReadInt32(); //檔案大小
                            }
                            //因為結尾符號\0被換成空格了
                            if (pIdx.szFileName.Contains(" ")) {
                                pIdx.szFileName = pIdx.szFileName.Substring(0, pIdx.szFileName.IndexOf(" "));
                            }
                            if (result.ContainsKey(pIdx.szFileName)) {
                                continue;
                            }
                            //用小寫檔案名稱當作索引
                            if (!result.ContainsKey(pIdx.szFileName.ToLower())) {
                                result.Add(pIdx.szFileName.ToLower(), pIdx);
                            }
                        }
                    } catch (EndOfStreamException) {
                        DebugLog.Log($"[L1IdxReader.Load] ERROR: EndOfStreamException in {szIdxType}");
                        WinFormsMessageBox.Show("idx檔案長度錯誤");
                    }
                }
                DebugLog.Log($"[L1IdxReader.Load] Parsed {result.Count} entries from {szIdxType}");
            } else {
                DebugLog.Log($"[L1IdxReader.Load] Files not found: idx={File.Exists(szIdxFullName)}, pak={File.Exists(szPakFullName)}");
            }
            return Share.IdxDataList[szIdxType] = result;
        }

        /// <summary>
        /// 載入 _EXTB$ 格式的索引檔案 (Extended Index Block)
        /// 格式：
        /// - Header: 16 bytes (magic "_EXTB$" + metadata)
        /// - Entry: 128 bytes each
        ///   - Offset 0-3: Unknown (sort key)
        ///   - Offset 4-7: Compression (0=none, 1=zlib, 2=brotli)
        ///   - Offset 8-119: FileName (112 bytes, null-padded)
        ///   - Offset 120-123: PAK Offset
        ///   - Offset 124-127: Uncompressed Size
        /// </summary>
        private static Dictionary<string, L1Idx> LoadExtB(byte[] data, string szPakFullName) {
            var result = new Dictionary<string, L1Idx>();

            const int headerSize = 0x10;  // 16 bytes
            const int entrySize = 0x80;   // 128 bytes

            int entryCount = (data.Length - headerSize) / entrySize;

            // 建立排序的 offset 列表（用於計算壓縮大小）
            var offsets = new List<int>();
            for (int i = 0; i < entryCount; i++) {
                int entryOffset = headerSize + i * entrySize;
                int pakOffset = BitConverter.ToInt32(data, entryOffset + 120);
                offsets.Add(pakOffset);
            }
            offsets.Sort();
            offsets.Add(int.MaxValue);  // 用於計算最後一個檔案的大小

            for (int i = 0; i < entryCount; i++) {
                int entryOffset = headerSize + i * entrySize;

                int pakOffset = BitConverter.ToInt32(data, entryOffset + 120);
                int compression = BitConverter.ToInt32(data, entryOffset + 4);
                int uncompressedSize = BitConverter.ToInt32(data, entryOffset + 124);

                // 讀取檔案名稱 (offset 8-119, 112 bytes)
                int nameStart = entryOffset + 8;
                int nameEnd = nameStart;
                while (nameEnd < entryOffset + 120 && data[nameEnd] != 0) {
                    nameEnd++;
                }

                if (nameEnd <= nameStart) continue;

                string fileName = Encoding.Default.GetString(data, nameStart, nameEnd - nameStart);
                if (string.IsNullOrEmpty(fileName)) continue;

                // 計算壓縮大小（找到下一個 offset）
                int compressedSize = 0;
                int idx = offsets.IndexOf(pakOffset);
                if (idx >= 0 && idx < offsets.Count - 1) {
                    compressedSize = offsets[idx + 1] - pakOffset;
                    if (compressedSize == int.MaxValue - pakOffset) {
                        compressedSize = uncompressedSize;  // 最後一個檔案，使用未壓縮大小
                    }
                }

                var pIdx = new L1Idx(IdxType.EXTB) {
                    szPakFullName = szPakFullName,
                    szFileName = fileName,
                    nPosition = pakOffset,
                    nSize = uncompressedSize,
                    nCompressType = compression,
                    nCompressSize = compression > 0 ? compressedSize : 0,
                    isDesEncode = false
                };

                if (!result.ContainsKey(fileName.ToLower())) {
                    result.Add(fileName.ToLower(), pIdx);
                }
            }

            return result;
        }
    }
}


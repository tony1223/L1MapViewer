using System;
using System.Collections.Generic;
using System.IO;
using static L1MapViewer.Other.Struct;

namespace L1MapViewer.Reader {
    class L1PakReader {

        public static byte[]? UnPack(string szIdxType, string szFileName) {
            DebugLog.Log($"[L1PakReader.UnPack] Finding {szIdxType}/{szFileName}...");

            L1Idx? pIdx = L1IdxReader.Find(szIdxType, szFileName);

            if (pIdx == null) {
                DebugLog.Log($"[L1PakReader.UnPack] Not found: {szIdxType}/{szFileName}");
                return null;
            }

            DebugLog.Log($"[L1PakReader.UnPack] Reading from pak: pos={pIdx.nPosition}, size={pIdx.nSize}");
            byte[] data = Read(pIdx);

            //解DES-通常只有text有加密
            if (pIdx.isDesEncode) {
                DebugLog.Log("[L1PakReader.UnPack] Decoding DES...");
                data = Algorithm.DecodeDes(data, 0);
            }
            //解壓縮
            if (pIdx.nCompressSize > 0) {
                if (pIdx.nCompressType == 2) {
                    DebugLog.Log("[L1PakReader.UnPack] Decompressing Brotli...");
                    data = Algorithm.BrotliDecompress(data);
                } else if (pIdx.nCompressType == 1) {
                    DebugLog.Log("[L1PakReader.UnPack] Decompressing Zlib...");
                    data = Algorithm.ZilbDecompress(data, pIdx.nSize);
                }
            }

            //解XML
            if (szFileName.ToLower().EndsWith(".spz")) {
                DebugLog.Log("[L1PakReader.UnPack] Decoding SPZ...");
                data = DecodeXml(data, 5);
            } else if (szFileName.ToLower().EndsWith(".xml") || szFileName.ToLower().EndsWith(".json") || szFileName.EndsWith(".ui")) {
                DebugLog.Log("[L1PakReader.UnPack] Decoding XML/JSON...");
                data = DecodeXml(data, 4);
            }

            DebugLog.Log($"[L1PakReader.UnPack] Done: {data?.Length ?? 0} bytes");
            return data;
        }

        //讀取pak內的原檔
        private static byte[] Read(L1Idx pIdx) {

            //有壓縮過的要取壓縮後的長度
            int len = pIdx.nCompressSize > 0 ? pIdx.nCompressSize : pIdx.nSize;

            byte[] data = new byte[len];

            using (FileStream fs = File.Open(pIdx.szPakFullName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                fs.Seek(pIdx.nPosition, SeekOrigin.Begin);
                fs.Read(data, 0, data.Length);
            }
            return data;
        }

        //AES(xml)
        private static byte[] DecodeXml(byte[] encodeBinary, int headLength) {
            //spz :b[0] = 0x53
            //xml :b[0] = 0x58
            //檔案頭的長度 spz=5 ,xml=4

            List<byte> buffer = new List<byte>();
            try {
                byte[] head = new byte[headLength];
                Array.Copy(encodeBinary, 0, head, 0, head.Length);

                if (head[0] == 0x58) {
                    head[0] = 0x3C; //"X"--> "<"
                }
                buffer.AddRange(head);

                //head是明碼不用解密-->所以分成兩部分處理

                byte[] b = new byte[encodeBinary.Length - headLength];
                Array.Copy(encodeBinary, headLength, b, 0, b.Length);
                buffer.AddRange(Algorithm.DecodeAes(b));
            } catch {
                //
            }
            return buffer.ToArray();
        }

        /// <summary>
        /// 從指定的 idx/pak 路徑讀取檔案（用於讀取 R client 的資源）
        /// </summary>
        /// <param name="idxPath">idx 檔案完整路徑</param>
        /// <param name="fileName">要讀取的檔案名稱</param>
        /// <returns>檔案內容，如果找不到則返回 null</returns>
        public static byte[]? UnPackFromPath(string idxPath, string fileName) {
            if (string.IsNullOrEmpty(idxPath) || string.IsNullOrEmpty(fileName))
                return null;

            string pakPath = idxPath.Replace(".idx", ".pak");
            if (!File.Exists(idxPath) || !File.Exists(pakPath))
                return null;

            fileName = fileName.ToLower();

            // 讀取 idx 檔案
            var idxData = LoadIdxFromPath(idxPath, pakPath);
            if (idxData == null || !idxData.ContainsKey(fileName))
                return null;

            var pIdx = idxData[fileName];
            byte[] data = ReadFromPak(pIdx, pakPath);

            // 解 DES（通常只有 text 有加密）
            if (pIdx.isDesEncode) {
                data = Algorithm.DecodeDes(data, 0);
            }
            // 解壓縮
            if (pIdx.nCompressSize > 0) {
                if (pIdx.nCompressType == 2) {
                    data = Algorithm.BrotliDecompress(data);
                } else if (pIdx.nCompressType == 1) {
                    data = Algorithm.ZilbDecompress(data, pIdx.nSize);
                }
            }

            return data;
        }

        /// <summary>
        /// 從指定路徑載入 idx 資料（支援多種格式）
        /// </summary>
        private static Dictionary<string, L1Idx>? LoadIdxFromPath(string idxPath, string pakPath) {
            var result = new Dictionary<string, L1Idx>();

            try {
                byte[] data = File.ReadAllBytes(idxPath);
                if (data.Length < 6) return null;

                // 判斷 idx 格式
                IdxType structType = IdxType.OLD;
                if (data[0] == '_' && data[1] == 'E' && data[2] == 'X' &&
                    data[3] == 'T' && data[4] == 'B' && data[5] == '$') {
                    structType = IdxType.EXTB;
                } else if (data.Length >= 4) {
                    string head = System.Text.Encoding.Default.GetString(data, 0, 4).ToLower();
                    if (head == "_ext") structType = IdxType.EXT;
                    else if (head == "_rms") structType = IdxType.RMS;
                }

                // EXTB 格式
                if (structType == IdxType.EXTB) {
                    return LoadExtBFromPath(data, pakPath);
                }

                // 其他格式
                int nBaseOffset = (structType == IdxType.OLD) ? 4 : 8;

                using (BinaryReader br = new BinaryReader(new MemoryStream(data))) {
                    br.BaseStream.Seek(nBaseOffset, SeekOrigin.Begin);

                    while (br.BaseStream.Position < data.Length) {
                        L1Idx pIdx = new L1Idx(structType);
                        pIdx.szPakFullName = pakPath;

                        pIdx.nPosition = br.ReadInt32();

                        if (structType == IdxType.EXT) {
                            pIdx.nSize = br.ReadInt32();
                            pIdx.nCompressSize = br.ReadInt32();
                            pIdx.nCompressType = br.ReadInt32();
                            pIdx.szFileName = System.Text.Encoding.Default.GetString(br.ReadBytes(112)).Replace('\0', ' ').Trim();
                        } else if (structType == IdxType.RMS) {
                            pIdx.nSize = br.ReadInt32();
                            pIdx.nCompressSize = br.ReadInt32();
                            pIdx.nCompressType = br.ReadInt32();
                            pIdx.szFileName = System.Text.Encoding.Default.GetString(br.ReadBytes(260)).Replace('\0', ' ').Trim();
                        } else {
                            pIdx.szFileName = System.Text.Encoding.Default.GetString(br.ReadBytes(20)).Replace('\0', ' ').Trim();
                            pIdx.nSize = br.ReadInt32();
                        }

                        if (pIdx.szFileName.Contains(" ")) {
                            pIdx.szFileName = pIdx.szFileName.Substring(0, pIdx.szFileName.IndexOf(" "));
                        }

                        if (!string.IsNullOrEmpty(pIdx.szFileName) && !result.ContainsKey(pIdx.szFileName.ToLower())) {
                            result[pIdx.szFileName.ToLower()] = pIdx;
                        }
                    }
                }
            } catch {
                return null;
            }

            return result;
        }

        /// <summary>
        /// 載入 EXTB 格式的 idx
        /// </summary>
        private static Dictionary<string, L1Idx> LoadExtBFromPath(byte[] data, string pakPath) {
            var result = new Dictionary<string, L1Idx>();

            const int headerSize = 0x10;
            const int entrySize = 0x80;

            int entryCount = (data.Length - headerSize) / entrySize;

            var offsets = new List<int>();
            for (int i = 0; i < entryCount; i++) {
                int entryOffset = headerSize + i * entrySize;
                int pakOffset = BitConverter.ToInt32(data, entryOffset + 120);
                offsets.Add(pakOffset);
            }
            offsets.Sort();
            offsets.Add(int.MaxValue);

            for (int i = 0; i < entryCount; i++) {
                int entryOffset = headerSize + i * entrySize;

                int pakOffset = BitConverter.ToInt32(data, entryOffset + 120);
                int compression = BitConverter.ToInt32(data, entryOffset + 4);
                int uncompressedSize = BitConverter.ToInt32(data, entryOffset + 124);

                int nameStart = entryOffset + 8;
                int nameEnd = nameStart;
                while (nameEnd < entryOffset + 120 && data[nameEnd] != 0) {
                    nameEnd++;
                }

                if (nameEnd <= nameStart) continue;

                string fileName = System.Text.Encoding.Default.GetString(data, nameStart, nameEnd - nameStart);
                if (string.IsNullOrEmpty(fileName)) continue;

                int compressedSize = 0;
                int idx = offsets.IndexOf(pakOffset);
                if (idx >= 0 && idx < offsets.Count - 1) {
                    compressedSize = offsets[idx + 1] - pakOffset;
                    if (compressedSize == int.MaxValue - pakOffset) {
                        compressedSize = uncompressedSize;
                    }
                }

                var pIdx = new L1Idx(IdxType.EXTB) {
                    szPakFullName = pakPath,
                    szFileName = fileName,
                    nPosition = pakOffset,
                    nSize = uncompressedSize,
                    nCompressType = compression,
                    nCompressSize = compression > 0 ? compressedSize : 0,
                    isDesEncode = false
                };

                if (!result.ContainsKey(fileName.ToLower())) {
                    result[fileName.ToLower()] = pIdx;
                }
            }

            return result;
        }

        /// <summary>
        /// 從指定的 pak 檔案讀取資料
        /// </summary>
        private static byte[] ReadFromPak(L1Idx pIdx, string pakPath) {
            int len = pIdx.nCompressSize > 0 ? pIdx.nCompressSize : pIdx.nSize;
            byte[] data = new byte[len];

            using (FileStream fs = File.Open(pakPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                fs.Seek(pIdx.nPosition, SeekOrigin.Begin);
                fs.Read(data, 0, data.Length);
            }
            return data;
        }

        /// <summary>
        /// 取得指定 idx 檔案中的所有檔案名稱
        /// </summary>
        public static List<string>? GetFileListFromPath(string idxPath) {
            if (string.IsNullOrEmpty(idxPath) || !File.Exists(idxPath))
                return null;

            string pakPath = idxPath.Replace(".idx", ".pak");
            var idxData = LoadIdxFromPath(idxPath, pakPath);
            return idxData?.Keys.ToList();
        }
    }
}


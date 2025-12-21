using System;
using System.Collections.Generic;
using System.IO;
using static L1MapViewer.Other.Struct;

namespace L1MapViewer.Reader {
    class L1PakReader {

        public static byte[]? UnPack(string szIdxType, string szFileName) {

            L1Idx? pIdx = L1IdxReader.Find(szIdxType, szFileName);

            if (pIdx == null) {
                return null;
            }

            byte[] data = Read(pIdx);

            //解DES-通常只有text有加密
            if (pIdx.isDesEncode) {
                data = Algorithm.DecodeDes(data, 0);
            }
            //解壓縮
            if (pIdx.nCompressSize > 0) {
                if (pIdx.nCompressType == 2) {
                    data = Algorithm.BrotliDecompress(data);
                } else if (pIdx.nCompressType == 1) {
                    data = Algorithm.ZilbDecompress(data, pIdx.nSize);
                }
            }

            //解XML
            if (szFileName.ToLower().EndsWith(".spz")) {
                data = DecodeXml(data, 5);
            } else if (szFileName.ToLower().EndsWith(".xml") || szFileName.ToLower().EndsWith(".json") || szFileName.EndsWith(".ui")) {
                data = DecodeXml(data, 4);
            }

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
        /// 從指定路徑載入 idx 資料
        /// </summary>
        private static Dictionary<string, L1Idx>? LoadIdxFromPath(string idxPath, string pakPath) {
            var result = new Dictionary<string, L1Idx>();

            try {
                using (FileStream fs = File.Open(idxPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader br = new BinaryReader(fs)) {
                    int cnt = br.ReadInt32();

                    for (int i = 0; i < cnt; i++) {
                        L1Idx idx = new L1Idx(IdxType.OLD);
                        idx.szPakFullName = pakPath;
                        idx.szFileName = new string(br.ReadChars(260)).Trim('\0').ToLower();
                        idx.nPosition = br.ReadInt32();
                        idx.nSize = br.ReadInt32();
                        idx.nCompressType = br.ReadInt32();
                        idx.nCompressSize = br.ReadInt32();
                        int nIsDesEncode = br.ReadInt32();
                        idx.isDesEncode = nIsDesEncode == 1;

                        result[idx.szFileName] = idx;
                    }
                }
            } catch {
                return null;
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


using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
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

            //緩存內已經有了 直接回傳
            if (Share.IdxDataList.ContainsKey(szIdxType)) {
                return Share.IdxDataList[szIdxType];
            }
            //idx/pak檔的路徑
            string szIdxFullName = string.Format(@"{0}\{1}.idx", Share.LineagePath, szIdxType);
            string szPakFullName = szIdxFullName.Replace(".idx", ".pak");

            //兩個路徑都需要存在
            if (File.Exists(szIdxFullName) && File.Exists(szPakFullName)) {
                byte[] data = File.ReadAllBytes(szIdxFullName);

                //取前4個byte
                byte[] head = new byte[4];
                Array.Copy(data, 0, head, 0, head.Length);

                //判斷idx的結構類型
                IdxType structType = IdxType.OLD;
                if (Encoding.Default.GetString(head).ToLower().Equals("_ext")) {
                    structType = IdxType.EXT;
                } else if (Encoding.Default.GetString(head).ToLower().Equals("_rms")) {
                    structType = IdxType.RMS;
                }

                int nBaseOffset = 0;
                bool isDesEncode = false;
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
                        MessageBox.Show("idx檔案長度錯誤");
                    }
                }
            }
            return Share.IdxDataList[szIdxType] = result;
        }
    }
}


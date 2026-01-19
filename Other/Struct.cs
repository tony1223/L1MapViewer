using L1MapViewer.Helper;
using System;
using System.Collections.Generic;
// using System.Drawing; // Replaced with Eto.Drawing
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace L1MapViewer.Other {
    public class Struct {
        public enum IdxType {
            OLD,
            EXT,
            RMS,
            EXTB,  // _EXTB$ 格式 (Extended Index Block with Brotli support)
        }
        //idx檔案的結構
        public class L1Idx {
            public L1Idx(IdxType type) {
                nType = type;
            }
            public IdxType nType;      //idx檔案的結構類型
            public int nPosition;            //在pak檔的開始位置
            public string szFileName = string.Empty;        //檔案名
            public int nSize;                //檔案大小
            public int nCompressSize;        //壓縮後大小
            public int nCompressType;        //壓縮方式
            public string szIdxFullName = string.Empty;     //idx檔案的完整路徑
            public string szPakFullName = string.Empty;     //pak檔案的完整路徑
            public bool isDesEncode;         //是否有DES加密(通常只有text有加密)
        }

        //地圖
        public class L1Map {
            public L1Map(string mapid, string fullDirName) {
                szMapId = mapid;
                szFullDirName = fullDirName;
            }
            public string szMapId;               //地圖編號
            public string szName = string.Empty;                //地圖名稱
            public string szFullDirName;         //地圖檔資料夾的路徑
            public int nMinBlockX = 0xFFFF;      //最小地圖檔區塊座標X
            public int nMinBlockY = 0xFFFF;      //最小地圖檔區塊座標Y
            public int nMaxBlockX = 0;           //最大地圖檔區塊座標X
            public int nMaxBlockY = 0;           //最大地圖檔區塊座標Y
            public int nBlockCountX;             //地圖檔區塊數量X軸
            public int nBlockCountY;             //地圖檔區塊數量Y軸
            public int nLinBeginX;                  //X軸的起點座標
            public int nLinBeginY;                  //Y軸的起點座標
            public int nLinEndX;                    //X軸的終點座標
            public int nLinEndY;                    //Y軸的終點座標
            public int nLinLengthX;                 //X軸的長度
            public int nLinLengthY;                 //Y軸的長度
            public Dictionary<string, L1MapSeg> FullFileNameList = new Dictionary<string, L1MapSeg>(); //存放地圖檔的路徑,地圖單一檔案資訊      
        }
        //地圖單一檔案
        public class L1MapSeg {
            public L1MapSeg(int bX, int bY, bool is_s32) {
                isS32 = is_s32;
                nBlockX = bX;
                nBlockY = bY;
                nLinEndX = (nBlockX - 0x7fff) * 64 + 0x7fff;
                nLinEndY = (nBlockY - 0x7fff) * 64 + 0x7fff;
                nLinBeginX = nLinEndX - 64 + 1;
                nLinBeginY = nLinEndY - 64 + 1;
            }
            public int nLinBeginX;                  //X軸的起點座標
            public int nLinBeginY;                  //Y軸的起點座標
            public int nLinEndX;                    //X軸的終點座標
            public int nLinEndY;                    //Y軸的終點座標
            public int nBlockX = 0;            //地圖檔區塊座標X
            public int nBlockY = 0;            //地圖檔區塊座標Y            
            public int nMapMinBlockX = 0;      //最小地圖檔區塊座標X
            public int nMapMinBlockY = 0;      //最小地圖檔區塊座標Y
            public int nMapBlockCountX = 0;         //地圖檔區塊數量X軸
            public bool isS32;                  //是否為.s32檔
            public bool isRemastered;           //是否為天R

            public int[] GetLoc(double rate) {
                int blockWidth = L1MapHelper.BMP_W; //每一個區塊的寬度
                int blockHeight = L1MapHelper.BMP_H;//每一個區塊的長度

                if (isRemastered) {
                    blockWidth = L1MapHelper.BMP_R_W;
                    blockHeight = L1MapHelper.BMP_R_H;
                }

                // 使用 decimal 並 round 到小數點後6位，避免浮點數精度問題 (0.999999 -> 1.0)
                decimal r = Math.Round((decimal)rate, 6);

                int baseX = 0;
                int baseY = (int)Math.Round((nMapBlockCountX - 1) * blockHeight / 2 * r, 0);

                //區塊的x,y座標
                int blockX = nBlockX - nMapMinBlockX;
                int blockY = nBlockY - nMapMinBlockY;

                int mx = (int)Math.Round(blockX * blockWidth * r, 0);
                int my = (int)Math.Round(blockY * blockHeight * r, 0);

                mx += baseX + my * 2;
                my += baseY;

                mx -= (int)Math.Round(blockX * blockWidth / 2 * r, 0);
                my -= (int)Math.Round(blockX * blockHeight / 2 * r, 0);

                mx -= (int)Math.Round(blockY * blockWidth / 2 * r, 0);
                my -= (int)Math.Round(blockY * blockHeight / 2 * r, 0);
                return new int[] { mx, my };
            }
        }
        //地圖Til
        public struct TilBlockInfo {
            public int x;       // X軸 
            public int y;       // Y軸
            public int nLayer;  //順序 0是最低的
            public int nIndex;  //til的index
            public int nTilId;  //til編號
            public int uk;      //未知
        }
        //地圖在天堂內的座標
        public class LinLocation {
            public int x; // X軸 
            public int y; // Y軸
            public Region region;
            public LinLocation(int x, int y, Region region) {
                this.x = x;
                this.y = y;
                this.region = region;
            }
        }

        //天堂地圖區域
        public class L1Zone {
            public string szMapId;
            public List<L1ZoneArea> mZoneAreaList = new List<L1ZoneArea>();

            public L1Zone(string szMapId) {
                this.szMapId = szMapId;
            }
            public void addZoneArea(string szName, int left, int top, int right, int bottom) {
                L1ZoneArea desc = new L1ZoneArea();
                desc.szName = szName;
                desc.szName = szName;
                desc.left = left;
                desc.top = top;
                desc.right = right;
                desc.bottom = bottom;
                mZoneAreaList.Add(desc);
            }
        }

        //天堂地圖單一區域的資料
        public struct L1ZoneArea {
            public string szName;
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
    }
}


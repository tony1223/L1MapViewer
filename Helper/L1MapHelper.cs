using L1MapViewer.Converter;
using L1MapViewer.Other;
using L1MapViewer.Reader;
using L1MapViewer.Compatibility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
// using System.Drawing; // Replaced with Eto.Drawing
// using System.Drawing.Drawing2D; // Replaced with L1MapViewer.Compatibility
// using System.Drawing.Imaging; // Replaced with SkiaSharp
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Eto.Forms;
using Eto.Drawing;
using System.Xml;
using L1FlyMapViewer;
using static L1MapViewer.Other.Struct;

namespace L1MapViewer.Helper {
    public class L1MapHelper {
        public static readonly int BMP_W = 64 * 24 * 2;        //地圖區塊尺寸
        public static readonly int BMP_H = 64 * 12 * 2;        //地圖區塊尺寸
        public static readonly int BMP_R_W = 64 * 48 * 2;      //R版的地圖區塊尺寸
        public static readonly int BMP_R_H = 64 * 24 * 2;      //R版的地圖區塊尺寸      

        public static readonly int PAGE_1 = 1;
        public static readonly int PAGE_2 = 2;

        private static bool isRemastered;

        // 防止重入的旗標
        private static bool _isReading = false;

        // 載入時間統計（供外部查看）
        public static long LastLoadZone3descMs { get; private set; }
        public static long LastLoadZoneXmlMs { get; private set; }
        public static long LastScanDirectoriesMs { get; private set; }
        public static int LastMapCount { get; private set; }
        public static int LastTotalFileCount { get; private set; }

        //讀取地圖檔資料
        public static Dictionary<string, L1Map> Read(string szSelectedPath) {
            DebugLog.Log($"[L1MapHelper.Read] Start: path={szSelectedPath}, _isReading={_isReading}");
            DebugLog.Log($"[L1MapHelper.Read] Share.LineagePath={Share.LineagePath}");

            // 防止重入（Application.DoEvents 可能造成重入）
            if (_isReading) {
                DebugLog.Log("[L1MapHelper.Read] WARNING: Re-entry detected! Returning cached list.");
                return Share.MapDataList;
            }

            if (string.IsNullOrEmpty(szSelectedPath)) {
                DebugLog.Log("[L1MapHelper.Read] Empty path, returning cached list");
                return Share.MapDataList;
            }

            //確認路徑
            string szMapPath = string.Format(@"{0}\map\", szSelectedPath);
            DebugLog.Log($"[L1MapHelper.Read] Checking map path: {szMapPath}");

            if (!Directory.Exists(szMapPath)) {
                DebugLog.Log($"[L1MapHelper.Read] ERROR: Map path does not exist!");
                WinFormsMessageBox.Show("錯誤的天堂路徑");
                return Share.MapDataList;
            }

            _isReading = true;
            try {
            //是否為天R - 每次都需要重新判斷
            isRemastered = Directory.Exists(szSelectedPath + @"/bin32/") || Directory.Exists(szSelectedPath + @"\bin32\");
            DebugLog.Log($"[L1MapHelper.Read] isRemastered={isRemastered}");

            var stopwatch = Stopwatch.StartNew();

            //map
            DebugLog.Log("[L1MapHelper.Read] Loading Zone3descTbl...");
            LoadZone3descTbl();
            LastLoadZone3descMs = stopwatch.ElapsedMilliseconds;
            DebugLog.Log($"[L1MapHelper.Read] Zone3descTbl done: {LastLoadZone3descMs}ms, count={Share.Zone3descList.Count}");

            stopwatch.Restart();
            DebugLog.Log("[L1MapHelper.Read] Loading ZoneXml...");
            LoadZoneXml();
            LastLoadZoneXmlMs = stopwatch.ElapsedMilliseconds;
            DebugLog.Log($"[L1MapHelper.Read] ZoneXml done: {LastLoadZoneXmlMs}ms, count={Share.ZoneList.Count}");

            stopwatch.Restart();
            int totalFileCount = 0;

            if (Share.MapDataList.Count == 0) {
                DebugLog.Log($"[L1MapHelper.Read] Scanning directories in: {szMapPath}");
                DebugLog.Log("[L1MapHelper.Read] Calling GetDirectories()...");
                var directories = new DirectoryInfo(szMapPath).GetDirectories();
                DebugLog.Log($"[L1MapHelper.Read] Found {directories.Length} directories to scan");
                DebugLog.Log($"[L1MapHelper.Read] First dir: {(directories.Length > 0 ? directories[0].Name : "none")}");

                int dirIndex = 0;
                //開始讀取資料夾
                DebugLog.Log("[L1MapHelper.Read] Entering foreach loop...");
                foreach (DirectoryInfo di in directories) {
                    try {
                    dirIndex++;
                    // 前 5 個和每 50 個都輸出 log
                    if (dirIndex <= 5 || dirIndex % 50 == 0) {
                        DebugLog.Log($"[L1MapHelper.Read] Scanning dir {dirIndex}/{directories.Length}: {di.Name}");
                    }
                    //地圖檔的資料夾名稱應該都是數字
                    if (Share.MapDataList.ContainsKey(di.Name)) {
                        DebugLog.Log($"[L1MapHelper.Read] Dir {di.Name} already in cache, skipping");
                        continue;
                    }
                    DebugLog.Log($"[L1MapHelper.Read] Creating L1Map for {di.Name}...");
                    L1Map pMap = new L1Map(di.Name, di.FullName);
                    DebugLog.Log($"[L1MapHelper.Read] L1Map created, getting description...");

                    pMap.szName = getDescribe(di.Name);
                    DebugLog.Log($"[L1MapHelper.Read] Description: {pMap.szName}, getting files...");

                    //取得地圖資料夾內檔案資料
                    // 先收集所有有效的 seg 和 s32 檔案，按檔名分組
                    var filesByName = new Dictionary<string, (FileInfo s32File, FileInfo segFile)>(StringComparer.OrdinalIgnoreCase);

                    foreach (FileInfo file in di.GetFiles()) {
                        string szExt = Path.GetExtension(file.FullName).ToLower(); //7ff88000.s32->.s32
                        string szFileName = Path.GetFileNameWithoutExtension(file.FullName).ToLower();

                        if (!szExt.Equals(".seg") && !szExt.Equals(".s32")) {
                            continue; //不是.seg 或 .s32
                        }
                        if (!Regex.IsMatch(szFileName, "^[a-fA-F0-9]+$")) {
                            continue; //不是16進位的檔名
                        }

                        // 按檔名分組
                        if (!filesByName.ContainsKey(szFileName)) {
                            filesByName[szFileName] = (null, null);
                        }

                        var entry = filesByName[szFileName];
                        if (szExt.Equals(".s32")) {
                            filesByName[szFileName] = (file, entry.segFile);
                        } else {
                            filesByName[szFileName] = (entry.s32File, file);
                        }
                    }

                    // 處理每個檔名：s32 優先，沒有才用 seg
                    foreach (var kvp in filesByName) {
                        string szFileName = kvp.Key;
                        var (s32File, segFile) = kvp.Value;

                        // 決定要使用的檔案
                        FileInfo fileToUse = null;
                        bool isS32 = false;

                        if (s32File != null) {
                            fileToUse = s32File;
                            isS32 = true;
                        } else if (segFile != null && !isRemastered) {
                            // 只有非天R才讀取 seg
                            fileToUse = segFile;
                            isS32 = false;
                        }

                        if (fileToUse == null) {
                            continue;
                        }

                        //取得邊界
                        int nBlockX = Convert.ToInt32(szFileName.Substring(0, 4), 16); //7ff8
                        int nBlockY = Convert.ToInt32(szFileName.Substring(4, 4), 16); //8000

                        //全部的s32檔案名稱會用來構成整張圖的邊界
                        pMap.nMinBlockX = Math.Min(pMap.nMinBlockX, nBlockX);
                        pMap.nMinBlockY = Math.Min(pMap.nMinBlockY, nBlockY);
                        pMap.nMaxBlockX = Math.Max(pMap.nMaxBlockX, nBlockX);
                        pMap.nMaxBlockY = Math.Max(pMap.nMaxBlockY, nBlockY);

                        L1MapSeg pMapSeg = new L1MapSeg(nBlockX, nBlockY, isS32);
                        pMap.FullFileNameList.Add(fileToUse.FullName, pMapSeg);
                        totalFileCount++;
                    }

                    //計算整張圖的起始坐標
                    pMap.nBlockCountX = pMap.nMaxBlockX - pMap.nMinBlockX + 1;
                    pMap.nBlockCountY = pMap.nMaxBlockY - pMap.nMinBlockY + 1;

                    if (pMap.nBlockCountX < 0 || pMap.nBlockCountY < 0) {
                        Console.WriteLine("map發生錯誤的資料夾" + pMap.szFullDirName);
                        continue;
                    }
                    //1個block = 64個坐標
                    pMap.nLinLengthX = pMap.nBlockCountX * 64;
                    pMap.nLinLengthY = pMap.nBlockCountY * 64;
                    //計算終點座標(公式)
                    pMap.nLinEndX = (pMap.nMaxBlockX - 0x7FFF) * 64 + 0x7FFF;
                    pMap.nLinEndY = (pMap.nMaxBlockY - 0x7FFF) * 64 + 0x7FFF;
                    //回推起點座標
                    pMap.nLinBeginX = pMap.nLinEndX - pMap.nLinLengthX + 1;
                    pMap.nLinBeginY = pMap.nLinEndY - pMap.nLinLengthY + 1;

                    //加入到緩存內
                    Share.MapDataList.Add(di.Name, pMap);

                    //將地圖會共用的值填入L1MapSeg
                    foreach (L1MapSeg pMapSeg in pMap.FullFileNameList.Values) {
                        pMapSeg.isRemastered = isRemastered;
                        pMapSeg.nMapMinBlockX = pMap.nMinBlockX;
                        pMapSeg.nMapMinBlockY = pMap.nMinBlockY;
                        pMapSeg.nMapBlockCountX = pMap.nBlockCountX;
                    }

                    if (dirIndex <= 5 || dirIndex % 50 == 0) {
                        DebugLog.Log($"[L1MapHelper.Read] Dir {di.Name} done, files={pMap.FullFileNameList.Count}, calling DoEvents...");
                    }
                    //系統就會暫時把頁面還給你
                    ApplicationHelper.DoEvents();
                    if (dirIndex <= 5 || dirIndex % 50 == 0) {
                        DebugLog.Log($"[L1MapHelper.Read] DoEvents returned for dir {di.Name}");
                    }
                    }
                    catch (Exception ex) {
                        DebugLog.Log($"[L1MapHelper.Read] ERROR in dir {di.Name}: {ex.GetType().Name}: {ex.Message}");
                        DebugLog.Log($"[L1MapHelper.Read] StackTrace: {ex.StackTrace}");
                    }
                }
            }

            LastScanDirectoriesMs = stopwatch.ElapsedMilliseconds;
            LastMapCount = Share.MapDataList.Count;
            LastTotalFileCount = totalFileCount;

            DebugLog.Log($"[L1MapHelper.Read] Complete! Maps={LastMapCount}, Files={LastTotalFileCount}, ScanTime={LastScanDirectoriesMs}ms");
            return Share.MapDataList;
            }
            finally {
                _isReading = false;
            }
        }

        /// <summary>
        /// 重新整理單一地圖的檔案清單（用於匯入新 S32 後更新）
        /// </summary>
        public static void RefreshMap(string mapId) {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(Share.LineagePath))
                return;

            string mapFolderPath = Path.Combine(Share.LineagePath, "map", mapId);
            if (!Directory.Exists(mapFolderPath))
                return;

            // 移除舊資料
            if (Share.MapDataList.ContainsKey(mapId)) {
                Share.MapDataList.Remove(mapId);
            }

            // 重新讀取該地圖
            DirectoryInfo di = new DirectoryInfo(mapFolderPath);
            L1Map pMap = new L1Map(di.Name, di.FullName);
            pMap.szName = getDescribe(di.Name);

            // 收集所有有效的 seg 和 s32 檔案
            var filesByName = new Dictionary<string, (FileInfo s32File, FileInfo segFile)>(StringComparer.OrdinalIgnoreCase);

            foreach (FileInfo file in di.GetFiles()) {
                string szExt = Path.GetExtension(file.FullName).ToLower();
                string szFileName = Path.GetFileNameWithoutExtension(file.FullName).ToLower();

                if (!szExt.Equals(".seg") && !szExt.Equals(".s32"))
                    continue;
                if (!Regex.IsMatch(szFileName, "^[a-fA-F0-9]+$"))
                    continue;

                if (!filesByName.ContainsKey(szFileName)) {
                    filesByName[szFileName] = (null, null);
                }

                var entry = filesByName[szFileName];
                if (szExt.Equals(".s32")) {
                    filesByName[szFileName] = (file, entry.segFile);
                } else {
                    filesByName[szFileName] = (entry.s32File, file);
                }
            }

            // 處理每個檔名：s32 優先
            foreach (var kvp in filesByName) {
                string szFileName = kvp.Key;
                var (s32File, segFile) = kvp.Value;

                FileInfo fileToUse = null;
                bool isS32 = false;

                if (s32File != null) {
                    fileToUse = s32File;
                    isS32 = true;
                } else if (segFile != null && !isRemastered) {
                    fileToUse = segFile;
                    isS32 = false;
                }

                if (fileToUse == null)
                    continue;

                int nBlockX = Convert.ToInt32(szFileName.Substring(0, 4), 16);
                int nBlockY = Convert.ToInt32(szFileName.Substring(4, 4), 16);

                pMap.nMinBlockX = Math.Min(pMap.nMinBlockX, nBlockX);
                pMap.nMinBlockY = Math.Min(pMap.nMinBlockY, nBlockY);
                pMap.nMaxBlockX = Math.Max(pMap.nMaxBlockX, nBlockX);
                pMap.nMaxBlockY = Math.Max(pMap.nMaxBlockY, nBlockY);

                L1MapSeg pMapSeg = new L1MapSeg(nBlockX, nBlockY, isS32);
                pMap.FullFileNameList.Add(fileToUse.FullName, pMapSeg);
            }

            // 計算邊界
            pMap.nBlockCountX = pMap.nMaxBlockX - pMap.nMinBlockX + 1;
            pMap.nBlockCountY = pMap.nMaxBlockY - pMap.nMinBlockY + 1;

            if (pMap.nBlockCountX < 0 || pMap.nBlockCountY < 0) {
                Console.WriteLine($"RefreshMap: map {mapId} 發生錯誤");
                return;
            }

            pMap.nLinLengthX = pMap.nBlockCountX * 64;
            pMap.nLinLengthY = pMap.nBlockCountY * 64;
            pMap.nLinEndX = (pMap.nMaxBlockX - 0x7FFF) * 64 + 0x7FFF;
            pMap.nLinEndY = (pMap.nMaxBlockY - 0x7FFF) * 64 + 0x7FFF;
            pMap.nLinBeginX = pMap.nLinEndX - pMap.nLinLengthX + 1;
            pMap.nLinBeginY = pMap.nLinEndY - pMap.nLinLengthY + 1;

            Share.MapDataList[mapId] = pMap;

            // 填入共用值
            foreach (L1MapSeg pMapSeg in pMap.FullFileNameList.Values) {
                pMapSeg.isRemastered = isRemastered;
                pMapSeg.nMapMinBlockX = pMap.nMinBlockX;
                pMapSeg.nMapMinBlockY = pMap.nMinBlockY;
                pMapSeg.nMapBlockCountX = pMap.nBlockCountX;
            }

            Console.WriteLine($"[RefreshMap] Map {mapId} refreshed: {pMap.FullFileNameList.Count} files");
        }



        //zone3desc-c.tbl -->地圖區塊代號的中文翻譯
        public static void LoadZone3descTbl() {
            DebugLog.Log("[LoadZone3descTbl] Start");
            if (Share.Zone3descList.Count > 0) {
                DebugLog.Log("[LoadZone3descTbl] Already loaded, skip");
                return;
            }
            DebugLog.Log("[LoadZone3descTbl] Trying Text/zone3desc-c.tbl...");
            byte[] data = L1PakReader.UnPack("Text", "zone3desc-c.tbl");

            if (data == null) {
                DebugLog.Log("[LoadZone3descTbl] Not found, trying Text/zone3desc.tbl...");
                data = L1PakReader.UnPack("Text", "zone3desc.tbl");
            }
            if (data == null) {
                DebugLog.Log("[LoadZone3descTbl] No zone3desc file found");
                return;
            }
            DebugLog.Log($"[LoadZone3descTbl] Got data: {data.Length} bytes");
            using (StreamReader sr = new StreamReader(new MemoryStream(data), Encoding.GetEncoding("big5"))) {
                string line = null;
                sr.ReadLine(); //line 0
                while ((line = sr.ReadLine()) != null) {
                    Share.Zone3descList.Add(line);
                }
            }
            DebugLog.Log($"[LoadZone3descTbl] Done, loaded {Share.Zone3descList.Count} lines");
        }

        //zone3-c.xml -->地圖區塊的設定
        public static void LoadZoneXml() {
            DebugLog.Log("[LoadZoneXml] Start");

            if (Share.ZoneList.Count > 0) {
                DebugLog.Log("[LoadZoneXml] Already loaded, skip");
                return;
            }

            DebugLog.Log("[LoadZoneXml] Trying Tile/zone3-c.xml...");
            byte[] data = L1PakReader.UnPack("Tile", "zone3-c.xml");

            if (data == null) {
                DebugLog.Log("[LoadZoneXml] Not found, trying Tile/zone3.xml...");
                data = L1PakReader.UnPack("Tile", "zone3.xml");
            }
            if (data == null) {
                DebugLog.Log("[LoadZoneXml] Not found, trying Data/zone3-c.xml...");
                data = L1PakReader.UnPack("Data", "zone3-c.xml");
            }
            if (data == null) {
                DebugLog.Log("[LoadZoneXml] No XML found, falling back to LoadZoneTbl...");
                LoadZoneTbl();  //zone3-c.xml 或 zone3.xml 沒有就改找zone3.tbl
                return;
            }
            DebugLog.Log($"[LoadZoneXml] Got data: {data.Length} bytes");


            // zone3.xml的內容
            string xmllText = Encoding.GetEncoding("utf-8").GetString(data);

            /*
             * <zone>
                    <name>말하는 섬-오크 망루지대</name>
            <zone3desc>818</zone3desc>
                    <level min="50" max="54"/>
                    <map num="0" left="32364" top="32932" right="32378" bottom="32971"/>
                </zone>
             * 
                <zone>
                    <name>켄트 내성</name>
            <zone3desc>14</zone3desc>
                    <color id="11"/>
                    <map num="15"/>
                    <background id="119"/>
                </zone>
             */
            //xml
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmllText);
            //選擇節點
            XmlNode main = doc.SelectSingleNode("zoneinfo/zone");
            if (main == null) {
                return;
            }

            L1Zone pZone;
            foreach (XmlNode nd in doc.SelectNodes("zoneinfo/zone")) {

                XmlNode zone3descNode = nd.SelectSingleNode("zone3desc");

                string zone3desc = "";
                if (zone3descNode == null) {
                    XmlNode nameNode = nd.SelectSingleNode("name");

                    if (nameNode == null) {
                        continue;
                    }
                    zone3desc = nameNode.InnerText;
                } else {
                    zone3desc = zone3descNode.InnerText;//<zone3desc>14</zone3desc>-->14   
                }

                XmlNode mapNode = nd.SelectSingleNode("map"); // <name> <zone3desc> <color> <map>都是裡面的節點
                XmlElement element = (XmlElement)mapNode;

                string mapid = element.GetAttribute("num");
                if (Share.ZoneList.ContainsKey(mapid)) {
                    pZone = Share.ZoneList[mapid];
                } else {
                    pZone = new L1Zone(mapid);
                    Share.ZoneList.Add(mapid, pZone);
                }

                string left = element.GetAttribute("left");
                string top = element.GetAttribute("top");
                string right = element.GetAttribute("right");
                string bottom = element.GetAttribute("bottom");
                //地圖名稱有範圍性的話 記下起始坐標
                if (left == String.Empty) {
                    pZone.addZoneArea(zone3desc, 0, 0, 0, 0); //加入清單
                } else {
                    pZone.addZoneArea(zone3desc,
                       Convert.ToInt32(left), Convert.ToInt32(top), Convert.ToInt32(right), Convert.ToInt32(bottom));
                }
            }
        }

        //zone3.tbl -->地圖區塊的設定 (舊版)
        private static void LoadZoneTbl() {

            byte[] data = L1PakReader.UnPack("Text", "zone3-c.tbl");

            if (data == null) {
                data = L1PakReader.UnPack("Text", "zone2-c.tbl");
            }
            if (data == null) {
                return;
            }

            // zone3.tbl的內容
            using (StreamReader sr = new StreamReader(new MemoryStream(data), Encoding.GetEncoding("big5"))) {
                L1Zone pZone;
                string line = null;
                while ((line = sr.ReadLine()) != null) {
                    //"說話之島" 0 0 32545 32905 32606 32987 100 1 ;Talking Island villige 
                    //"北邊峽谷(初級村)" 0 2031 32700 32704 32832 32784 ;隱藏之谷-北邊峽谷 #1
                    if (line.Contains("\"")) {
                        string[] split = line.Split('"');
                        if (split.Length < 3) continue; // 跳過格式不正確的行
                        string name = split[1]; //說話之島
                        string[] info = split[2].Split(';')[0].Trim().Split(' '); //0 0 32545 32905 32606 32987 100 1

                        if (info.Length < 5) continue; // 跳過資料不足的行

                        if (info.Length == 5) { //台版天R
                            string mapid = info[0];
                            if (Share.ZoneList.ContainsKey(mapid)) {
                                pZone = Share.ZoneList[mapid];
                            } else {
                                pZone = new L1Zone(mapid);
                                Share.ZoneList.Add(mapid, pZone);
                            }
                            pZone.addZoneArea(name,
                                Convert.ToInt32(info[1]), Convert.ToInt32(info[2]), Convert.ToInt32(info[3]), Convert.ToInt32(info[4]));

                        } else { //info.Length == 6
                            string mapid = info[1];
                            if (Share.ZoneList.ContainsKey(mapid)) {
                                pZone = Share.ZoneList[mapid];
                            } else {
                                pZone = new L1Zone(mapid);
                                Share.ZoneList.Add(mapid, pZone);
                            }
                            pZone.addZoneArea(name,
                                Convert.ToInt32(info[2]), Convert.ToInt32(info[3]), Convert.ToInt32(info[4]), Convert.ToInt32(info[5]));
                        }

                    }
                }
            }
        }


        //獲得地圖的敘述
        public static string getDescribe(string szMapId) {
            string szDescribe = "";
            if (Share.ZoneList.ContainsKey(szMapId)) {
                L1Zone mapDescribe = Share.ZoneList[szMapId];

                if (mapDescribe.mZoneAreaList.Count == 1) {
                    L1ZoneArea pZoneArea = mapDescribe.mZoneAreaList[0];
                    int n;
                    if (int.TryParse(pZoneArea.szName, out n)) {
                        // 確保索引在有效範圍內
                        if (n >= 0 && n < Share.Zone3descList.Count) {
                            szDescribe = Share.Zone3descList[n]; //是數字
                        } else {
                            szDescribe = pZoneArea.szName; // 索引超出範圍，直接使用名稱
                        }
                    } else {
                        szDescribe = pZoneArea.szName; //不是數字
                    }
                } else {
                    L1ZoneArea pZoneArea = mapDescribe.mZoneAreaList[mapDescribe.mZoneAreaList.Count - 1];
                    int n;
                    if (int.TryParse(pZoneArea.szName, out n)) {
                        // 確保索引在有效範圍內
                        if (n >= 0 && n < Share.Zone3descList.Count) {
                            szDescribe = Share.Zone3descList[n]; //是數字
                        } else {
                            szDescribe = pZoneArea.szName; // 索引超出範圍，直接使用名稱
                        }
                    } else {
                        szDescribe = pZoneArea.szName; //不是數字
                    }
                }
            }
            return szDescribe;
        }
        //---------------------------------------------------------
        //顯示座標
        public static void doMouseMoveEvent(MouseEventArgs e, IMapViewer viewer) {
            if (viewer.comboBox1.SelectedItem == null) {
                return;
            }
            // 調整座標以考慮縮放
            double zoomLevel = viewer.zoomLevel;
            int ex = (int)(e.Location.X / zoomLevel);
            int ey = (int)(e.Location.Y / zoomLevel);

            LinLocation location = GetLinLoc(ex, ey);

            if (location != null) {
                viewer.toolStripStatusLabel2.Text = string.Format("{0},{1}", location.x, location.y);
            }
        }
        //繪製選定的座標
        public static void doLocTagEvent(MouseEventArgs e, IMapViewer viewer) {
            if (viewer.comboBox1.SelectedItem == null) {
                return;
            }

            Image img = viewer.pictureBox2.Image;
            if (img == null) {
                return;
            }
            // 調整座標以考慮縮放
            double zoomLevel = viewer.zoomLevel;
            int ex = (int)(e.Location.X / zoomLevel);
            int ey = (int)(e.Location.Y / zoomLevel);

            //目前滑鼠指到的座標
            LinLocation location = GetLinLoc(ex, ey);

            using (Graphics g = GraphicsHelper.FromImage(img as Bitmap ?? new Bitmap(img))) {
                //  g.Clear(Colors.Transparent);

                //目前滑鼠指到的座標 畫座標+小格子
                if (location != null) {
                    string locString = string.Format("{0},{1}", location.x, location.y);
                    DrawLocation(g, ex, ey, locString, true);

                    RectangleF boundsRect = location.region.GetBounds(g);
                    g.DrawRectangle(Pens.Red, Rectangle.Round(boundsRect));
                }
            }
            viewer.pictureBox2.Refresh();
        }

        private static LinLocation GetLinLoc(int ex, int ey) {
            //先確定位於哪個大格子 再判斷是哪個小格子
            foreach (Region key in Share.RegionList.Keys) {
                if (key.IsVisible(ex, ey)) {
                    Dictionary<Region, LinLocation> rList = Share.RegionList[key];
                    foreach (Region keys in rList.Keys) {
                        if (keys.IsVisible(ex, ey)) {
                            //用小格子當索引..座標為值
                            return rList[keys];
                        }
                    }
                }
            }
            return null;
        }

        // 公開方法：通過螢幕座標獲取天堂座標
        public static LinLocation GetLinLocation(int ex, int ey) {
            return GetLinLoc(ex, ey);
        }

        // 通過天堂座標獲取 LinLocation
        public static LinLocation GetLinLocationByCoords(int x, int y) {
            string key = string.Format("{0}-{1}", x, y);
            if (Share.LinLocList.ContainsKey(key)) {
                return Share.LinLocList[key];
            }
            return null;
        }
        public static void DrawLocation(Graphics g, int ex, int ey, string locString, bool isArrow) {
            SolidBrush sb = new SolidBrush(WinFormsColors.Gold);
            Font font = new Font("微軟正黑體", 10, FontStyle.None);
            SizeF s = g.MeasureString(locString, font);
            g.FillRectangle(new SolidBrush(Colors.Gray), ex + 24, ey - 24, (int)s.Width, (int)s.Height);
            g.DrawString(locString, font, sb, ex + 24, ey - 24);
            Pen pen = new Pen(Colors.Red, 1);
            // pen.DashStyle is solid by default in Eto.Drawing
            if (isArrow) {
                pen.SetEndCap(LineCap.ArrowAnchor);
            }
            g.DrawLine(pen, ex + 24, ey - 24, ex, ey);
        }

        //填入天堂座標
        private static void FillLinLoc(Bitmap bitmap, L1MapSeg pMapSeg, double rate, int nPage) {

            //取得bmp在bitmap的座標 (不是天堂座標)        
            int[] nsLoc = pMapSeg.GetLoc(rate);
            int mx = nsLoc[0];
            int my = nsLoc[1];

            int mWidth = 0;
            int mHeight = 0;

            if (pMapSeg.isRemastered) {
                mWidth = (int)(BMP_R_W * rate);
                mHeight = (int)(BMP_R_H * rate);
            } else if (pMapSeg.isS32) {
                mWidth = (int)(BMP_W * rate);
                mHeight = (int)(BMP_H * rate);
            } else {
                mWidth = (int)(BMP_W * rate);
                mHeight = (int)(BMP_H * rate);
            }
            //    using (Graphics g = GraphicsHelper.FromImage(tmpBmp)) {
            Point p1 = new Point(mx + 0, my + mHeight / 2);
            Point p2 = new Point(mx + mWidth / 2, my + 0);
            Point p3 = new Point(mx + mWidth, my + mHeight / 2);
            Point p4 = new Point(mx + mWidth / 2, my + mHeight);

            //用大格子當索引
            GraphicsPath gp = new GraphicsPath();
            gp.Reset();
            gp.AddPolygon(new Point[] { p1, p2, p3, p4 });
            Region region = new Region();
            region.MakeEmpty();
            region.Union(gp);
            //g.DrawPolygon(new Pen(Colors.Green, 3), new Point[] { p1, p2, p3, p4 });

            int r = (int)(24 * rate);
            if (isRemastered) {
                r = (int)(48 * rate);
            }
            Dictionary<Region, LinLocation> rList = new Dictionary<Region, LinLocation>();
            for (int y = 0; y < 64; y++) {
                for (int x = 0; x < 64; x++) {
                    int bx = 0;
                    int by = 63 * r / 2;
                    int X = bx + x * r + y * r;
                    int Y = by + y * r / 2;
                    Y -= r / 2 * (x);

                    p1 = new Point(mx + X + 0, my + Y + r / 2);
                    p2 = new Point(mx + X + 2 * r / 2, my + Y + 0);
                    p3 = new Point(mx + X + 2 * r, my + Y + r / 2);
                    p4 = new Point(mx + X + r, my + Y + r);

                    //用小格子當索引..座標為值
                    GraphicsPath sgp = new GraphicsPath();
                    sgp.Reset();
                    sgp.AddPolygon(new Point[] { p1, p2, p3, p4 });
                    Region sRegion = new Region();
                    sRegion.MakeEmpty();
                    sRegion.Union(sgp);

                    int nLinX = pMapSeg.nLinBeginX + x;
                    int nLinY = pMapSeg.nLinBeginY + y;
                    LinLocation iLinLoc = new LinLocation(nLinX, nLinY, sRegion);
                    rList.Add(sRegion, iLinLoc);
                    // g.DrawPolygon(new Pen(Colors.Red, 1), new Point[] { p1, p2, p3, p4 });  
                    if (nPage == PAGE_1) {
                        Share.LinLocList.Add(string.Format("{0}-{1}", nLinX, nLinY), iLinLoc);
                    } else if (nPage == PAGE_2) {
                        Share.LinLocList2.Add(string.Format("{0}-{1}", nLinX, nLinY), iLinLoc);
                    }

                }
            }

            if (nPage == PAGE_1) {
                Share.RegionList.Add(region, rList);
            } else if (nPage == PAGE_2) {
                Share.RegionList2.Add(region, rList);
            }

            //  }
            ApplicationHelper.DoEvents(); //系統就會暫時把頁面還給你
        }

        //畫地圖
        public static void doPaintEvent(string szSelectName, IMapViewer viewer) {
            Utils.ShowProgressBar(true, viewer);
            ((Form)viewer).Cursor = Cursors.WaitCursor;//漏斗指標

            //panel1底色須跟pictureBox1相同...為了美觀
            if (viewer.panel1.BackgroundColor != Colors.Black) {
                viewer.panel1.BackgroundColor = Colors.Black;
            }

            viewer.comboBox1.Enabled = false;

            viewer.pictureBox2.Visible = false;
            viewer.pictureBox3.Visible = false;
            viewer.pictureBox4.Visible = false;

            //重置設定 (先給圖再定位)
            if (viewer.pictureBox1.Image != null) {
                viewer.pictureBox1.Image.Dispose();
                viewer.pictureBox1.Image = new Bitmap(1, 1);
                viewer.pictureBox1.Width = 1;
                viewer.pictureBox1.Height = 1;
                viewer.pictureBox1.SetLocation(new Point(3, 3));
                viewer.pictureBox1.Refresh();
            }

            Share.RegionList.Clear();
            Share.LinLocList.Clear();

            string szTmpBmpName = null; //要保存的bmp檔名(不為null的話)
            szSelectName = szSelectName.Split('-')[0].Trim(); //地圖ID -敘述

            try {
                L1Map pMap = Share.MapDataList[szSelectName];

                if (pMap == null) {
                    WinFormsMessageBox.Show(string.Format("選擇的地圖編號:{0} 不存在", szSelectName));
                    return;
                }
                //計算縮小倍率                
                double rate = (double)1 / 2;

                if (pMap.nBlockCountX * pMap.nBlockCountY >= 4) {
                    rate = (double)1 / 6;
                }
                if (pMap.nBlockCountX * pMap.nBlockCountY >= 64) {//map 0
                    rate = (double)1 / 12;
                }
                if (pMap.nBlockCountX * pMap.nBlockCountY >= 576) {//map 4
                    rate = (double)1 / 12;

                }

                /*
                 ＿＿＿
                ▕╱╲▕
                ▕╲╱▕
                 ￣￣￣
                 */
                int blockWidth = BMP_W; //每一個區塊的寬度
                int blockHeight = BMP_H;//每一個區塊的長度

                if (isRemastered) {
                    rate *= 0.5;
                    blockWidth = BMP_R_W;
                    blockHeight = BMP_R_H;
                }

                //大地圖用sha1判斷檔案有無異動 沒有的話 用先前保存的圖檔
                if (pMap.nBlockCountX * pMap.nBlockCountY >= 64) {
                    List<byte> shaList = new List<byte>();
                    SHA1 sha1 = new SHA1CryptoServiceProvider();
                    //取得資料
                    foreach (string fileFullName in Utils.SortDesc(pMap.FullFileNameList.Keys)) {
                        byte[] data = File.ReadAllBytes(fileFullName);
                        byte[] bytes = sha1.ComputeHash(data);
                        for (int i = 0; i < bytes.Length; i++) {
                            shaList.Add(bytes[i]);
                        }
                    }
                    byte[] bytes_all = sha1.ComputeHash(shaList.ToArray());
                    sha1.Dispose();
                    szTmpBmpName = BitConverter.ToString(bytes_all).Replace("-", "");

                    string szTmpBmpFile = string.Format(@"{0}\{1}.bmp", Path.GetTempPath(), szTmpBmpName);
                    if (File.Exists(szTmpBmpFile)) {
                        Bitmap tmpBmp = new Bitmap(szTmpBmpFile);
                        viewer.pictureBox1.Image = tmpBmp;
                        viewer.pictureBox1.Width = tmpBmp.Width;
                        viewer.pictureBox1.Height = tmpBmp.Height;
                        viewer.pictureBox1.Refresh();


                        //使用vScrollBar、hScrollBar控制pictureBox控件显示图片 (1/3)
                        viewer.hScrollBar1.Maximum = Math.Max(0, viewer.pictureBox1.Width);
                        viewer.vScrollBar1.Maximum = Math.Max(0, viewer.pictureBox1.Height);
                        //滾動條置中
                        viewer.hScrollBar1.Value = viewer.hScrollBar1.Maximum / 2;
                        viewer.vScrollBar1.Value = viewer.vScrollBar1.Maximum / 2;
                        viewer.vScrollBar1_Scroll(null, null);
                        viewer.hScrollBar1_Scroll(null, null);
                        //取得資料       
                        foreach (string fileFullName in Utils.SortDesc(pMap.FullFileNameList.Keys)) {
                            //取得bmp在bitmap的座標 (不是天堂座標)
                            L1MapSeg iL1MapSeg = pMap.FullFileNameList[fileFullName];

                            //填入座標
                            FillLinLoc(tmpBmp, iL1MapSeg, rate, PAGE_1);
                        }
                        return;
                    }
                }



                // 菱形地圖的邊界取決於 (blockX + blockY)，額外加一個區塊確保完整顯示
                int width = (int)(((pMap.nBlockCountX + pMap.nBlockCountY) * blockWidth / 2 + blockWidth) * rate);
                int height = (int)(((pMap.nBlockCountX + pMap.nBlockCountY) * blockHeight / 2 + blockHeight) * rate);

                Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format16bppRgb555);

                ImageAttributes vAttr = new ImageAttributes();
                vAttr.SetColorKey(Color.FromArgb(0), Color.FromArgb(0));//透明色

                viewer.pictureBox1.Image = bitmap;
                viewer.pictureBox1.Width = bitmap.Width;
                viewer.pictureBox1.Height = bitmap.Height;


                //使用vScrollBar、hScrollBar控制pictureBox控件显示图片 (1/3)
                viewer.hScrollBar1.Maximum = Math.Max(0, viewer.pictureBox1.Width);
                viewer.vScrollBar1.Maximum = Math.Max(0, viewer.pictureBox1.Height);
                //滾動條置中
                viewer.hScrollBar1.Value = viewer.hScrollBar1.Maximum / 5;
                viewer.vScrollBar1.Value = viewer.vScrollBar1.Maximum / 5;
                viewer.vScrollBar1_Scroll(null, null);
                viewer.hScrollBar1_Scroll(null, null);
                //取得資料
                foreach (string fileFullName in Utils.SortDesc(pMap.FullFileNameList.Keys)) {
                    byte[] data = File.ReadAllBytes(fileFullName);

                    //取得bmp在bitmap的座標 (不是天堂座標)
                    L1MapSeg pMapSeg = pMap.FullFileNameList[fileFullName];
                    int[] nsLoc = pMapSeg.GetLoc(rate);
                    int mx = nsLoc[0];
                    int my = nsLoc[1];


                    Bitmap bmp;
                    if (pMapSeg.isRemastered) {
                        bmp = s32FileToBmpR(data);
                    } else if (pMapSeg.isS32) {
                        bmp = s32FileToBmp(data);
                    } else {
                        bmp = segFileToBmp(data);
                    }

                    //合併+縮圖+透明
                    using (Graphics g = GraphicsHelper.FromImage(bitmap)) {
                        int mWidth = (int)(bmp.Width * rate);
                        int mHeight = (int)(bmp.Height * rate);
                        // Simplified DrawImage - Eto doesn't support ImageAttributes directly
                        g.DrawImage(bmp, new RectangleF(0, 0, bmp.Width, bmp.Height), new RectangleF(mx, my, mWidth, mHeight));

                        //填入座標
                        FillLinLoc(bitmap, pMapSeg, rate, PAGE_1);
                    }

                    bmp.Dispose();
                    ApplicationHelper.DoEvents(); //系統就會暫時把頁面還給你

                    viewer.pictureBox1.Refresh();
                }

                //大地圖的暫存檔
                if (szTmpBmpName != null) {
                    bitmap.Save(string.Format(@"{0}\{1}.bmp", Path.GetTempPath(), szTmpBmpName), ImageFormat.Bmp);
                }
            } finally {
                //在地圖上再加一層用來畫範圍的圈圈
                Image img = viewer.pictureBox1.Image;
                if (img != null) {
                    //怪物分布的標記
                    // Note: Eto doesn't support Parent assignment, use Controls.Add instead
                    // viewer.pictureBox4.Parent = viewer.pictureBox1;
                    viewer.pictureBox4.Image = new Bitmap(img.Width, img.Height);
                    viewer.pictureBox4.Width = img.Width;
                    viewer.pictureBox4.Height = img.Height;
                    viewer.pictureBox4.SetLocation(new Point(0, 0));
                    viewer.pictureBox4.Visible = true;
                    viewer.pictureBox4.Refresh();

                    //畫額外的圖
                    // Note: Eto doesn't support Parent assignment
                    // viewer.pictureBox3.Parent = viewer.pictureBox4;
                    viewer.pictureBox3.Image = new Bitmap(img.Width, img.Height);
                    viewer.pictureBox3.Width = img.Width;
                    viewer.pictureBox3.Height = img.Height;
                    viewer.pictureBox3.SetLocation(new Point(0, 0));
                    viewer.pictureBox3.Visible = true;
                    viewer.pictureBox3.Refresh();

                    //座標顯示
                    // Note: Eto doesn't support Parent assignment
                    // viewer.pictureBox2.Parent = viewer.pictureBox3;
                    viewer.pictureBox2.Image = new Bitmap(img.Width, img.Height);
                    viewer.pictureBox2.Width = img.Width;
                    viewer.pictureBox2.Height = img.Height;
                    viewer.pictureBox2.SetLocation(new Point(0, 0));
                    viewer.pictureBox2.Visible = true;
                    viewer.pictureBox2.Refresh();

                    /*using (Graphics g = GraphicsHelper.FromImage(viewer.pictureBox2.Image)) {
                        Point p1 = new Point(0, 0);
                        Point p2 = new Point(0, viewer.pictureBox2.Height - 1);
                        Point p3 = new Point(viewer.pictureBox2.Width - 1, viewer.pictureBox2.Height - 1);
                        Point p4 = new Point(viewer.pictureBox2.Width - 1, 0);
                        g.DrawPolygon(new Pen(Colors.Red, 1), new Point[] { p1, p2, p3, p4 });
                    }*/

                }
                ((Form)viewer).Cursor = Cursors.Default;

                viewer.comboBox1.Enabled = true;

                Utils.ShowProgressBar(false, viewer);
            }
        }


        //-------------------以下簡單說就是從bytes 按照格式 轉成bmp 並使用指針加快填入速度-------------------//
        public static Bitmap s32FileToBmp(byte[] srcData) {
            Bitmap result = new Bitmap(BMP_W, BMP_H, PixelFormat.Format16bppRgb555);

            using (BinaryReader br = new BinaryReader(new MemoryStream(srcData))) {

                Rectangle rect = new Rectangle(0, 0, result.Width, result.Height);
                BitmapData bmpData = result.LockBits(rect, ImageLockMode.ReadWrite, result.PixelFormat);
                int rowpix = bmpData.Stride;

                //第一層(地板)
                for (int y = 0; y < 64; y++) {
                    for (int x = 0; x < 128; x++) {
                        int id = br.ReadByte();
                        int til = br.ReadUInt16();
                        int nk = br.ReadByte();
                        drawTilBlock(x, y, til, id, rowpix, bmpData.Scan0, result.Width, result.Height);
                    }
                }

                //第二層
                int LV2count = br.ReadUInt16();
                /* for (int i = 0; i < LV2count; i++) {
                     br.ReadByte();
                     br.ReadByte();
                     int value = br.ReadInt32();
                 }*/
                br.BaseStream.Seek(br.BaseStream.Position + LV2count * 6, SeekOrigin.Begin);
                //第三層(地圖可否穿越/安區/戰鬥/一般..等等屬性)
                /*for (int j = 0; j < 64; j++) {
                    for (int i = 0; i < 64; i++) {
                        int t1 = br.ReadInt16();
                        int t3 = br.ReadInt16();
                  }
                }*/
                br.BaseStream.Seek(br.BaseStream.Position + 64 * 64 * 4, SeekOrigin.Begin);

                // 第四層(物件)
                int LV4count = br.ReadInt32();
                List<TilBlockInfo> tilBolckList = new List<TilBlockInfo>();
                for (int i = 0; i < LV4count; i++) {
                    int index = br.ReadInt16(); // Id (辨識用)                  
                    int block = br.ReadUInt16(); // 區塊數量

                    for (int j = 0; j < block; j++) {
                        TilBlockInfo tilBolck = new TilBlockInfo();
                        tilBolck.x = br.ReadByte(); // X軸 
                        tilBolck.y = br.ReadByte(); // Y軸
                        tilBolck.nLayer = br.ReadByte();//順序 0是最低的
                        tilBolck.nIndex = br.ReadByte();
                        tilBolck.nTilId = br.ReadInt16();
                        tilBolck.uk = br.ReadByte();
                        tilBolckList.Add(tilBolck);
                    }
                }
                //排序 
                tilBolckList.Sort(
                    (fileA, fileB) => {
                        return fileA.nLayer.CompareTo(fileB.nLayer);
                    }
                );
                foreach (TilBlockInfo tilBolck in tilBolckList) {
                    drawTilBlock(tilBolck.x, tilBolck.y, tilBolck.nTilId, tilBolck.nIndex, rowpix, bmpData.Scan0, result.Width, result.Height);
                }



                // 第五層 (可透明化的圖塊)
                /*   int LV5count = br.ReadInt32();
                   for (int i = 0; i < LV5count; i++) {
                       int x = br.ReadByte(); // X
                       int y = br.ReadByte(); // Y
                       int r = br.ReadByte(); // R
                       int g = br.ReadByte(); // G
                       int b = br.ReadByte(); // B
                   }

                   // 第六層 (使用的til)
                   int LV6count = br.ReadInt32();
                   for (int i = 0; i < LV6count; i++) {
                       long position = br.BaseStream.Position;
                       int til = br.ReadInt32();
                       // Console.WriteLine("til = " + til);
                       //tilList.Add(position, til);
                   }

                   // 第七層 (傳送點、入口點)
                   int LV7count = br.ReadUInt16();
                   for (int i = 0; i < LV7count; i++) {
                       byte len = br.ReadByte(); // 傳送點、入口點長度
                       Encoding.Default.GetString(br.ReadBytes(len)); // 傳送點、入口點名稱
                       br.ReadByte(); // 傳送點、入口點的X軸
                       br.ReadByte(); // 傳送點、入口點的Y軸
                       br.ReadUInt16(); // 進入傳送點、入口點要傳送的地圖編號
                       br.ReadInt32(); // 傳送點、入口點的編號 (非地圖編號)
                   }

                   // 第八層 (特效、裝飾品)
                   int LV8count = br.ReadByte();
                   br.ReadByte();
                   for (int i = 0; i < LV8count; i++) {
                       int sprid = br.ReadUInt16(); // 特效編號
                       int x = br.ReadUInt16(); // X軸
                       int y = br.ReadUInt16(); // Y軸
                       br.ReadInt32();
                       //Console.WriteLine("sprid = " + sprid);
                   }*/


                result.UnlockBits(bmpData);
                //result.Save(newFileFullName + ".bmp");
                //result.Save(@"C:\Users\Administrator\Desktop\0\result\" + result.GetHashCode() + ".bmp");
            }
            return result;
        }

        //--------------------------------------------------------------------------------------------------------------------//
        public static Bitmap segFileToBmp(byte[] srcData) {
            Bitmap result = new Bitmap(BMP_W, BMP_H, PixelFormat.Format16bppRgb555);

            using (BinaryReader br = new BinaryReader(new MemoryStream(srcData))) {

                Rectangle rect = new Rectangle(0, 0, result.Width, result.Height);
                BitmapData bmpData = result.LockBits(rect, ImageLockMode.ReadWrite, result.PixelFormat);
                int rowpix = bmpData.Stride;

                //第一層(地板)
                for (int y = 0; y < 64; y++) {
                    for (int x = 0; x < 128; x++) {
                        int id = br.ReadByte();
                        int til = br.ReadByte();
                        drawTilBlock(x, y, til, id, rowpix, bmpData.Scan0, result.Width, result.Height);
                    }
                }
                //第二層
                int LV2count = br.ReadUInt16();
                /*for (int i = 0; i < LV2count; i++) {
                    int value = br.ReadInt32();
                }*/
                br.BaseStream.Seek(br.BaseStream.Position + LV2count * 4, SeekOrigin.Begin);

                //第三層(地圖可否穿越/安區/戰鬥/一般..等等屬性)
                /*for (int j = 0; j < 64; j++) {
                    for (int i = 0; i < 64; i++) {
                        int t1 = br.ReadByte();
                        int t3 = br.ReadByte();
                    }
                }*/
                br.BaseStream.Seek(br.BaseStream.Position + 64 * 64 * 2, SeekOrigin.Begin);

                // 第四層(物件)
                int LV4count = br.ReadInt32();
                List<TilBlockInfo> tilBolckList = new List<TilBlockInfo>();
                for (int i = 0; i < LV4count; i++) {
                    int index = br.ReadInt16(); // Id (辨識用)                  
                    int block = br.ReadUInt16(); // 區塊數量

                    for (int j = 0; j < block; j++) {
                        TilBlockInfo tilBolck = new TilBlockInfo();
                        tilBolck.x = br.ReadByte(); // X軸 
                        tilBolck.y = br.ReadByte(); // Y軸
                        tilBolck.nLayer = br.ReadByte();//順序 0是最低的
                        tilBolck.nIndex = br.ReadByte();
                        tilBolck.nTilId = br.ReadByte();
                        tilBolckList.Add(tilBolck);
                    }
                }
                //排序 
                tilBolckList.Sort(
                    (fileA, fileB) => {
                        return fileA.nLayer.CompareTo(fileB.nLayer);
                    }
                );
                foreach (TilBlockInfo tilBolck in tilBolckList) {
                    drawTilBlock(tilBolck.x, tilBolck.y, tilBolck.nTilId, tilBolck.nIndex, rowpix, bmpData.Scan0, result.Width, result.Height);
                }

                result.UnlockBits(bmpData);
            }
            // result.Save(newFileFullName + ".bmp");
            return result;
        }
        //--------------------------------------------------------------------------------------------------------------------//
        //天R..
        public static Bitmap s32FileToBmpR(byte[] srcData) {
            Bitmap result = new Bitmap(BMP_R_W, BMP_R_H, PixelFormat.Format16bppRgb555);//64 * 48 * 2, 64 * 24 * 2
            Rectangle rect = new Rectangle(0, 0, result.Width, result.Height);
            BitmapData bmpData = result.LockBits(rect, ImageLockMode.ReadWrite, result.PixelFormat);


            using (BinaryReader br = new BinaryReader(new MemoryStream(srcData))) {
                //第一層(地板)
                for (int y = 0; y < 64; y++) {
                    for (int x = 0; x < 128; x++) {
                        int id = br.ReadByte();
                        int til = br.ReadUInt16();
                        int nk = br.ReadByte();
                        drawTilBlockR(x, y, til, id, bmpData, 6144, 3072);
                    }
                }

                //第二層
                int LV2count = br.ReadUInt16();

                br.BaseStream.Seek(br.BaseStream.Position + LV2count * 6, SeekOrigin.Begin);
                //第三層(地圖可否穿越/安區/戰鬥/一般..等等屬性)

                br.BaseStream.Seek(br.BaseStream.Position + 64 * 64 * 4, SeekOrigin.Begin);

                // 第四層(物件)
                int LV4count = br.ReadInt32();
                List<TilBlockInfo> tilBolckList = new List<TilBlockInfo>();
                for (int i = 0; i < LV4count; i++) {
                    int index = br.ReadInt16(); // Id (辨識用)                  
                    int block = br.ReadUInt16(); // 區塊數量

                    for (int j = 0; j < block; j++) {
                        TilBlockInfo tilBolck = new TilBlockInfo();
                        tilBolck.x = br.ReadByte(); // X軸 
                        tilBolck.y = br.ReadByte(); // Y軸
                        tilBolck.nLayer = br.ReadByte();//順序 0是最低的
                        tilBolck.nIndex = br.ReadByte();
                        tilBolck.nTilId = br.ReadInt16();
                        tilBolck.uk = br.ReadByte();
                        tilBolckList.Add(tilBolck);
                    }
                }
                //排序 
                tilBolckList.Sort(
                    (fileA, fileB) => {
                        return fileA.nLayer.CompareTo(fileB.nLayer);
                    }
                );
                foreach (TilBlockInfo tilBolck in tilBolckList) {
                    drawTilBlockR(tilBolck.x, tilBolck.y, tilBolck.nTilId, tilBolck.nIndex, bmpData, 6144, 3072);
                }
            }
            result.UnlockBits(bmpData);
            // result = Utils.Resize(result, 0.5);
            // result.Save(newFileFullName + ".bmp");
            return result;
        }
        //--------------------------------------------------------------------------------------------------------------------//
        //暫存TIL
        private static Dictionary<string, List<byte[]>> tilList = new Dictionary<string, List<byte[]>>();
        private static List<string> OverList = new List<string>();

        //將til畫到地圖的bolck上
        private static unsafe void drawTilBlock(int x, int y, int til, int id, int rowpix, IntPtr Scan0, int maxWidth, int maxHeight) {

            string key = string.Format("{0}.til", til);

            List<byte[]> tilArray;

            if (tilList.ContainsKey(key)) {
                tilArray = tilList[key];
            } else {
                //讀取想要的檔案
                byte[] data = L1PakReader.UnPack("Tile", key);
                tilArray = L1Til.Parse(data);
                tilList.Add(key, tilArray);

            }

            // 備援機制：當 tilArray 為 null 或 id 越界時
            if (tilArray == null || id >= tilArray.Count) {
                if (til != 0) {
                    // 載入 0.til 作為預設填補
                    string fallbackKey = "0.til";
                    if (tilList.ContainsKey(fallbackKey)) {
                        tilArray = tilList[fallbackKey];
                    } else {
                        byte[] fallbackData = L1PakReader.UnPack("Tile", fallbackKey);
                        tilArray = L1Til.Parse(fallbackData);
                        if (tilArray != null)
                            tilList[fallbackKey] = tilArray;
                    }
                    if (tilArray == null || tilArray.Count == 0) return;
                    // 使用 187 或 188 作為預設 id (0x8CBB/0x8CBC 計算結果)
                    id = 187 + (x & 1);
                    if (id >= tilArray.Count)
                        id = id % tilArray.Count;
                } else {
                    // TileId=0 時，對 tilArray.Count 取模
                    if (tilArray != null && tilArray.Count > 0)
                        id = id % tilArray.Count;
                    else
                        return;
                }
            }
            if (tilArray == null || id >= tilArray.Count) return;

            int baseX = 0;
            int baseY = 63 * 12;
            baseX -= 24 * (x / 2);
            baseY -= 12 * (x / 2);





            //  byte[] tilData = tilArray[id];
            byte* til_ptr = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(tilArray[id], 0);
            byte* ptr = (byte*)Scan0;
            byte type = *(til_ptr++);

            if ((type & 0x02) == 0 && (type & 0x01) != 0) {
                for (int ty = 0; ty < 24; ty++) {
                    int n = 0;
                    //2.5D方塊的下半部
                    if (ty <= 11) {
                        n = (ty + 1) * 2;
                    } else {
                        n = (23 - ty) * 2;
                    }

                    int tx = 0;
                    for (int p = 0; p < n; p++) {
                        // ushort color = (ushort)(tilData[idx++] & 0xff | (tilData[idx++] << 8) & 0xff00);
                        ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));
                        //把rgb555的顏色填到相對位置
                        int startX = baseX + x * 24 + y * 24 + tx;
                        int startY = baseY + y * 12 + ty;

                        if (startX < 0 || startX >= maxWidth) {
                            continue;
                        }
                        if (startY < 0 || startY >= maxHeight) {
                            continue;
                        }

                        int v = (startY) * rowpix + (startX * 2);
                        *(ptr + v) = (byte)(color & 0x00FF);
                        *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                        tx++;
                    }
                }
            } else if ((type & 0x02) == 0 && (type & 0x01) == 0) {
                for (int ty = 0; ty < 24; ty++) {
                    int n = 0;
                    //2.5D方塊的上半部
                    if (ty <= 11) {
                        n = (ty + 1) * 2;
                    } else {
                        n = (23 - ty) * 2;
                    }

                    int tx = 24 - n;
                    for (int p = 0; p < n; p++) {
                        // ushort color = (ushort)(tilData[idx++] & 0xff | (tilData[idx++] << 8) & 0xff00);
                        ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));
                        int startX = baseX + x * 24 + y * 24 + tx;
                        int startY = baseY + y * 12 + ty;

                        if (startX < 0 || startX >= maxWidth) {
                            continue;
                        }
                        if (startY < 0 || startY >= maxHeight) {
                            continue;
                        }

                        int v = (startY) * rowpix + (startX * 2);
                        *(ptr + v) = (byte)(color & 0x00FF);
                        *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                        tx++;
                    }
                }
            } 
			else {
                byte x_offset = *(til_ptr++);
                byte y_offset = *(til_ptr++);
                byte xxLen = *(til_ptr++);
                byte yLen = *(til_ptr++);

                for (int ty = 0; ty < yLen; ty++) {
                    int tx = x_offset;
                    byte xSegmentCount = *(til_ptr++); // 有資料水平點數(X count), 水平區段數量
                    for (int nx = 0; nx < xSegmentCount; nx++) {
                        tx += *(til_ptr++) / 2;    // 跳過這個水平區段前的水平空白點數.
                        int xLen = *(til_ptr++);    // 讀取這一水平區段的點數.
                        for (int p = 0; p < xLen; p++) {
                            //  ushort color = (ushort)(tilData[idx++] & 0xff | (tilData[idx++] << 8) & 0xff00);
                            ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));
                            int startX = baseX + x * 24 + y * 24 + tx;
                            int startY = baseY + y * 12 + ty + y_offset;

                            if (startX < 0 || startX >= maxWidth) {
                                continue;
                            }
                            if (startY < 0 || startY >= maxHeight) {
                                continue;
                            }
                            int v = (startY) * rowpix + (startX * 2);
                            *(ptr + v) = (byte)(color & 0x00FF);
                            *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                            tx++;
                        }
                    }
                }
            }
        }
        private static unsafe void drawTilBlockR(int x, int y, int til, int id, BitmapData bmpData, int maxWidth, int maxHeight) {


            string key = string.Format("{0}.til", til);

            List<byte[]> tilArray;

            if (tilList.ContainsKey(key)) {
                tilArray = tilList[key];
            } else {
                //讀取想要的檔案
                byte[] data = L1PakReader.UnPack("Tile", key);
                tilArray = L1Til.Parse(data);
                tilList.Add(key, tilArray);

            }

            // 備援機制：當 tilArray 為 null 或 id 越界時
            if (tilArray == null || id >= tilArray.Count) {
                if (til != 0) {
                    // 載入 0.til 作為預設填補
                    string fallbackKey = "0.til";
                    if (tilList.ContainsKey(fallbackKey)) {
                        tilArray = tilList[fallbackKey];
                    } else {
                        byte[] fallbackData = L1PakReader.UnPack("Tile", fallbackKey);
                        tilArray = L1Til.Parse(fallbackData);
                        if (tilArray != null)
                            tilList[fallbackKey] = tilArray;
                    }
                    if (tilArray == null || tilArray.Count == 0) return;
                    // 使用 187 或 188 作為預設 id (0x8CBB/0x8CBC 計算結果)
                    id = 187 + (x & 1);
                    if (id >= tilArray.Count)
                        id = id % tilArray.Count;
                } else {
                    // TileId=0 時，對 tilArray.Count 取模
                    if (tilArray != null && tilArray.Count > 0)
                        id = id % tilArray.Count;
                    else
                        return;
                }
            }
            if (tilArray == null || id >= tilArray.Count) return;

            int baseX = 0;
            int baseY = 1512;//63 * 24;
            baseX -= 48 * (x / 2);
            baseY -= 24 * (x / 2);


            ushort color;
            int startX;
            int startY;
            int v;

            // byte[] tilData = tilArray[id];
            byte* til_ptr = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(tilArray[id], 0);
            int rowpix = bmpData.Stride;
            byte* ptr = (byte*)bmpData.Scan0;

            byte type = *(til_ptr++);

            if ((type & 0x02) == 0 && (type & 0x01) != 0) {
                for (int ty = 0; ty < 48; ty++) {
                    int n = 0;
                    //2.5D方塊的下半部
                    if (ty <= 23) {
                        n = (ty + 1) * 2;
                    } else {
                        n = (47 - ty) * 2;
                    }

                    int tx = 0;
                    for (int p = 0; p < n; p++) {
                        //   color = (ushort)(tilData[idx++] & 0xff | (tilData[idx++] << 8) & 0xff00);
                        color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));
                        //把rgb555的顏色填到相對位置
                        startX = baseX + x * 48 + y * 48 + tx;
                        startY = baseY + y * 24 + ty;

                        if (startX < 0 || startX >= maxWidth) {
                            continue;
                        }
                        if (startY < 0 || startY >= maxHeight) {
                            continue;
                        }

                        v = (startY) * rowpix + (startX * 2);


                        *(ptr + v) = (byte)(color & 0x00FF);
                        *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                        tx++;
                    }
                }
            } else if ((type & 0x02) == 0 && (type & 0x01) == 0) {
                for (int ty = 0; ty < 48; ty++) {
                    int n = 0;
                    //2.5D方塊的上半部
                    if (ty <= 23) {
                        n = (ty + 1) * 2;
                    } else {
                        n = (47 - ty) * 2;
                    }

                    int tx = 48 - n;
                    for (int p = 0; p < n; p++) {
                        //  color = (ushort)(tilData[idx++] & 0xff | (tilData[idx++] << 8) & 0xff00);
                        color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));
                        startX = baseX + x * 48 + y * 48 + tx;
                        startY = baseY + y * 24 + ty;

                        if (startX < 0 || startX >= maxWidth) {
                            continue;
                        }
                        if (startY < 0 || startY >= maxHeight) {
                            continue;
                        }

                        v = (startY) * rowpix + (startX * 2);

                        *(ptr + v) = (byte)(color & 0x00FF);
                        *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                        tx++;
                    }
                }
            } 
			else {
                byte x_offset = *(til_ptr++);
                byte y_offset = *(til_ptr++);
                byte xxLen = *(til_ptr++);
                byte yLen = *(til_ptr++);





                for (int ty = 0; ty < yLen; ty++) {
                    int tx = x_offset;
                    byte xSegmentCount = *(til_ptr++); // 有資料水平點數(X count), 水平區段數量
                    for (int nx = 0; nx < xSegmentCount; nx++) {
                        tx += *(til_ptr++) / 2;    // 跳過這個水平區段前的水平空白點數.
                        int xLen = *(til_ptr++);    // 讀取這一水平區段的點數.
                        for (int p = 0; p < xLen; p++) {
                            // color = (ushort)(tilData[idx++] & 0xff | (tilData[idx++] << 8) & 0xff00);
                            color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));
                            startX = baseX + x * 48 + y * 48 + tx;
                            startY = baseY + y * 24 + ty + y_offset;

                            if (startX < 0 || startX >= maxWidth) {
                                continue;
                            }
                            if (startY < 0 || startY >= maxHeight) {
                                continue;
                            }
                            v = (startY) * rowpix + (startX * 2);
                            *(ptr + v) = (byte)(color & 0x00FF);
                            *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                            tx++;
                        }

                    }
                }


            }

            ApplicationHelper.DoEvents(); //系統就會暫時把頁面還給你
        }
        //--------------------------------------------------------------------------------------------------------------------//
    }
}

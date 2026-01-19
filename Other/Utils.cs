using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using L1FlyMapViewer;

namespace L1MapViewer.Other {
    class Utils {
        //碼表
        private static Stopwatch sw = new Stopwatch();
        public static void StopWatchBegin() {
            sw.Reset();//碼表歸零
            sw.Start();//碼表開始計時
        }
        public static double StopWatchEnd() {
            sw.Stop();//碼錶停止    
            double result = sw.Elapsed.TotalMilliseconds / 1000;
            Console.WriteLine("[Total TimeSec]= " + result.ToString() + "s");
            return result;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern int GetPrivateProfileString(string lpAppName, string lpKeyName, string lpDefault, StringBuilder lpReturnedString, int nSize, string lpFileName);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern int WritePrivateProfileString(string lpAppName, string lpKeyName, string lpString, string lpFileName);


        public static string GetINI(string lpAppName, string lpKeyName, string lpDefault, string lpFileName) {
            StringBuilder sb = new StringBuilder(1024);
            GetPrivateProfileString(lpAppName, lpKeyName, lpDefault, sb, sb.Capacity, lpFileName);
            return sb.ToString();
        }

        public static int WriteINI(string lpAppName, string lpKeyName, string lpString, string lpFileName) {
            return WritePrivateProfileString(lpAppName, lpKeyName, lpString, lpFileName);
        }

        //進度條 搭配ApplicationHelper.DoEvents(); 系統就會暫時把頁面還給你
        public static void ShowProgressBar(bool b, IMapViewer viewer) {
            viewer.toolStripProgressBar1.Visible = b;
        }
        

        //獲得指定路徑的上層路徑
        public static string GetParentDirectoryPath(string folderPath, int levels) {
            string result = folderPath;
            for (int i = 0; i < levels; i++) {
                var parent = Directory.GetParent(result);
                if (parent != null) {
                    result = parent.FullName;
                } else {
                    return result;
                }
            }
            return result;
        }

        //排序 (由小到大)
        public static object[] SortAsc(ICollection collection) {
            ArrayList arrayList = new ArrayList(collection);
            try {
                arrayList.Sort(new TeamNameComparer());
            } catch {
                arrayList.Sort();
            }
            return arrayList.ToArray()!;
        }
        //排序 (由大到小)
        public static object[] SortDesc(ICollection collection) {
            ArrayList arrayList = new ArrayList(collection);
            try {
                arrayList.Sort(new TeamNameComparer2());
            } catch {
                arrayList.Sort();
            }
            return arrayList.ToArray()!;
        }
        //網路上找的
        private class TeamNameComparer : IComparer {
            public int Compare(object? x, object? y) {

                if (x == null || y == null) {
                    throw new ArgumentException("Parameters can't be null");
                }

                string nameA;
                string nameB;

                //如果是純數字
                if (x is int && y is int) {
                    nameA = Convert.ToString((int)x);
                    nameB = Convert.ToString((int)y);
                } else {
                    nameA = x as string;
                    nameB = y as string;
                }

                if (nameA == null || nameB == null) {
                    return 1;
                }
                var arr1 = nameA.ToCharArray();
                var arr2 = nameB.ToCharArray();

                var i = 0;
                var j = 0;

                while (i < arr1.Length && j < arr2.Length) {
                    if (char.IsDigit(arr1[i]) && char.IsDigit(arr2[j])) {
                        string s1 = "", s2 = "";
                        while (i < arr1.Length && char.IsDigit(arr1[i])) {
                            s1 += arr1[i];
                            i++;
                        }
                        while (j < arr2.Length && char.IsDigit(arr2[j])) {
                            s2 += arr2[j];
                            j++;
                        }
                        if (int.Parse(s1) > int.Parse(s2)) {
                            return 1;
                        }
                        if (int.Parse(s1) < int.Parse(s2)) {
                            return -1;
                        }
                    } else {
                        if (arr1[i] > arr2[j]) {
                            return 1;
                        }
                        if (arr1[i] < arr2[j]) {
                            return -1;
                        }
                        i++;
                        j++;
                    }
                }

                if (arr1.Length == arr2.Length) {
                    return 0;
                } else {
                    return arr1.Length > arr2.Length ? 1 : -1;
                }

            }
        }

        //網路上找的
        private class TeamNameComparer2 : IComparer {
            public int Compare(object? x, object? y) {

                if (x == null || y == null) {
                    throw new ArgumentException("Parameters can't be null");
                }

                string nameA;
                string nameB;

                //如果是純數字
                if (x is int && y is int) {
                    nameA = Convert.ToString((int)x);
                    nameB = Convert.ToString((int)y);
                } else {
                    nameA = x as string;
                    nameB = y as string;
                }

                if (nameA == null || nameB == null) {
                    return 1;
                }
                var arr1 = nameA.ToCharArray();
                var arr2 = nameB.ToCharArray();

                var i = 0;
                var j = 0;

                while (i < arr1.Length && j < arr2.Length) {
                    if (char.IsDigit(arr1[i]) && char.IsDigit(arr2[j])) {
                        string s1 = "", s2 = "";
                        while (i < arr1.Length && char.IsDigit(arr1[i])) {
                            s1 += arr1[i];
                            i++;
                        }
                        while (j < arr2.Length && char.IsDigit(arr2[j])) {
                            s2 += arr2[j];
                            j++;
                        }
                        if (int.Parse(s1) > int.Parse(s2)) {
                            return -1;
                        }
                        if (int.Parse(s1) < int.Parse(s2)) {
                            return 1;
                        }
                    } else {
                        if (arr1[i] > arr2[j]) {
                            return -1;
                        }
                        if (arr1[i] < arr2[j]) {
                            return 1;
                        }
                        i++;
                        j++;
                    }
                }

                if (arr1.Length == arr2.Length) {
                    return 0;
                } else {
                    return arr1.Length > arr2.Length ? -1 : 1;
                }

            }
        }


    }
}


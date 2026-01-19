using System;
using System.Collections;
using System.Collections.Generic;
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

        // Cross-platform INI file implementation (replaces Windows kernel32.dll P/Invoke)
        public static string GetINI(string lpAppName, string lpKeyName, string lpDefault, string lpFileName) {
            try {
                if (!File.Exists(lpFileName)) return lpDefault;

                string[] lines = File.ReadAllLines(lpFileName);
                string currentSection = "";

                foreach (string line in lines) {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]")) {
                        currentSection = trimmed.Substring(1, trimmed.Length - 2);
                    } else if (currentSection.Equals(lpAppName, StringComparison.OrdinalIgnoreCase)) {
                        int eqIndex = trimmed.IndexOf('=');
                        if (eqIndex > 0) {
                            string key = trimmed.Substring(0, eqIndex).Trim();
                            if (key.Equals(lpKeyName, StringComparison.OrdinalIgnoreCase)) {
                                return trimmed.Substring(eqIndex + 1).Trim();
                            }
                        }
                    }
                }
                return lpDefault;
            } catch {
                return lpDefault;
            }
        }

        public static int WriteINI(string lpAppName, string lpKeyName, string lpString, string lpFileName) {
            try {
                var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

                // Read existing file
                if (File.Exists(lpFileName)) {
                    string[] lines = File.ReadAllLines(lpFileName);
                    string currentSection = "";

                    foreach (string line in lines) {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("[") && trimmed.EndsWith("]")) {
                            currentSection = trimmed.Substring(1, trimmed.Length - 2);
                            if (!sections.ContainsKey(currentSection))
                                sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        } else if (!string.IsNullOrEmpty(currentSection)) {
                            int eqIndex = trimmed.IndexOf('=');
                            if (eqIndex > 0) {
                                string key = trimmed.Substring(0, eqIndex).Trim();
                                string value = trimmed.Substring(eqIndex + 1).Trim();
                                sections[currentSection][key] = value;
                            }
                        }
                    }
                }

                // Update value
                if (!sections.ContainsKey(lpAppName))
                    sections[lpAppName] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[lpAppName][lpKeyName] = lpString;

                // Write back
                using (var writer = new StreamWriter(lpFileName, false, Encoding.UTF8)) {
                    foreach (var section in sections) {
                        writer.WriteLine($"[{section.Key}]");
                        foreach (var kvp in section.Value) {
                            writer.WriteLine($"{kvp.Key}={kvp.Value}");
                        }
                        writer.WriteLine();
                    }
                }
                return 1;
            } catch {
                return 0;
            }
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


using System;
using System.Windows.Forms;

namespace L1MapViewer {
    static class Program {
        /// &lt;summary&gt;
        /// 應用程式的主要進入點。
        /// &lt;/summary&gt;
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}

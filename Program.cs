using L1FlyMapViewer;
using L1MapViewer;
using L1MapViewer.CLI;
using System.Text;

namespace L1MapViewerCore;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 檢查是否為 CLI 模式
        if (args.Length > 0 && args[0].ToLower() == "-cli")
        {
            return CliHandler.Execute(args);
        }

        // GUI 模式
        ApplicationConfiguration.Initialize();
        Application.Run(new MapForm());
        return 0;
    }
}

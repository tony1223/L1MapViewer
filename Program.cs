using System.Text;
using L1FlyMapViewer;
using L1MapViewer;

namespace L1MapViewerCore;

static class Program
{
    [STAThread]
    static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();
        Application.Run(new MapForm());
    }
}

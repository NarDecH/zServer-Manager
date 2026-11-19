using ZServer.Core;

namespace ZServer.Manager;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
        {
            MessageBox.Show("เกิดข้อผิดพลาดไม่คาดคิด:\n" + e.Exception.Message,
                "zServer Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                var logFile = Path.Combine(ServerPaths.Discover().LogsDir, "zserver-manager.log");
                File.AppendAllText(logFile,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERROR] Unhandled: {e.ExceptionObject}\n");
            }
            catch { }
        };
        Application.Run(new MainForm());
    }
}

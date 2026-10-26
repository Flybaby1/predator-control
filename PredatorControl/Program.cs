using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace PredatorControl
{
    internal static class Program
    {
        public static bool IsAdmin { get; private set; }
        public static bool DemoMode { get; private set; }
        public static bool StartInTray { get; private set; }
        public static string RenderPath { get; private set; }

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); } catch { }

            DemoMode = Array.IndexOf(args, "--demo") >= 0;
            StartInTray = Array.IndexOf(args, "--tray") >= 0;
            int ri = Array.IndexOf(args, "--render");
            if (ri >= 0 && ri + 1 < args.Length) RenderPath = args[ri + 1];

            IsAdmin = CheckAdmin();
            bool relaunched = Array.IndexOf(args, "--elevated") >= 0;

            using (var mutex = new Mutex(true, "PredatorControl_SingleInstance", out bool isNew))
            {
                if (!isNew)
                {
                    MessageBox.Show("掠夺者控制台已在运行中。", "掠夺者控制台",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                bool skipUac = Environment.GetEnvironmentVariable("PREDATOR_SKIP_UAC") == "1" || DemoMode || RenderPath != null;
                if (!IsAdmin && !relaunched && !skipUac)
                {
                    var res = MessageBox.Show(
                        "读取 CPU / GPU 温度与风扇转速需要管理员权限。\n\n是否以管理员身份重新启动？",
                        "掠夺者控制台", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button1);
                    if (res == DialogResult.Yes)
                    {
                        try
                        {
                            var psi = new ProcessStartInfo(Application.ExecutablePath, "--elevated")
                            {
                                UseShellExecute = true,
                                Verb = "runas"
                            };
                            Process.Start(psi);
                            return;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("提权失败：" + ex.Message, "掠夺者控制台",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }

                Application.ThreadException += (s, e) =>
                {
                    MessageBox.Show("程序发生异常：\n" + e.Exception.Message, "掠夺者控制台",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                try
                {
                    Application.Run(new MainForm());
                }
                catch (Exception ex)
                {
                    MessageBox.Show("启动失败：\n" + ex, "掠夺者控制台",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static bool CheckAdmin()
        {
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }
}

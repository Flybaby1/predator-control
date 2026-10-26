using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text;
using System.Windows.Forms;

namespace PredatorControl
{
    internal static class Diagnostics
    {
        public static string LogPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PredatorControl");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "diagnostics.log");
            }
        }

        public static string Build(MonitorService svc)
        {
            var sb = new StringBuilder();
            void W(string s = "") => sb.AppendLine(s);

            W("=========================================================");
            W(" 掠夺者控制台 · 硬件接口诊断");
            W(" 时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            W(" 管理员权限: " + Program.IsAdmin);
            W(" 系统: " + Environment.OSVersion.VersionString);
            W(" 程序版本: " + Application.ProductVersion);
            W("=========================================================");

            try
            {
                W("机型: " + GetCim("Win32_ComputerSystem", "Manufacturer") + " " + GetCim("Win32_ComputerSystem", "Model"));
                W("CPU : " + GetCim("Win32_Processor", "Name"));
            }
            catch (Exception ex) { W("机型读取失败: " + ex.Message); }

            W();
            W("[AcerGamingFunction]");
            W("  可用: " + svc.Wmi.Available);
            W("  实例: " + svc.Wmi.InstanceName);
            W("  错误: " + svc.Wmi.LastError);

            W();
            W("[传感器读数 SENSOR]");
            foreach (var kv in new (string name, byte id)[]
            {
                ("CPU 温度", AcerWmi.SENSOR_CPU_TEMP),
                ("GPU 温度", AcerWmi.SENSOR_GPU_TEMP),
                ("CPU 风扇", AcerWmi.SENSOR_CPU_FAN),
                ("GPU 风扇", AcerWmi.SENSOR_GPU_FAN),
                ("外部温度2", AcerWmi.SENSOR_EXT_TEMP2),
            })
            {
                bool ok = svc.Wmi.ReadSensor(kv.id, out int v);
                W($"  sid=0x{kv.id:X2} {kv.name,-10} -> {(ok ? v.ToString() : "不支持/失败")}");
            }

            W();
            W("[风扇转速百分比]");
            foreach (var kv in new (string name, byte id)[] { ("CPU 风扇", AcerWmi.FAN_CPU), ("GPU 风扇", AcerWmi.FAN_GPU) })
            {
                bool ok = svc.Wmi.GetFanPercent(kv.id, out int v);
                W($"  fan=0x{kv.id:X2} {kv.name,-10} -> {(ok ? v + "%" : "失败")}");
            }

            W();
            W("[风扇模式]");
            if (svc.Wmi.GetFanMode(out int cm, out int gm))
                W($"  CPU={cm} GPU={gm}  (1=自动 2=最大 3=手动)");
            else
                W("  读取失败: " + svc.Wmi.LastError);

            W();
            W("[杂项设置]");
            foreach (byte idx in new byte[] { 0x0A, 0x0B })
            {
                bool ok = svc.Wmi.GetMisc(idx, out int v);
                W($"  index=0x{idx:X2} -> {(ok ? "0x" + v.ToString("X2") : "失败")}");
            }

            W();
            W("[NVIDIA GPU]");
            W("  可用: " + svc.Gpu.Available + "  名称: " + svc.Gpu.Name);
            W($"  温度={svc.Gpu.Temp}°C 功耗={svc.Gpu.Power}W 频率={svc.Gpu.Clock}MHz 占用={svc.Gpu.Utilization}% 风扇={svc.Gpu.FanPercent}%");
            if (!string.IsNullOrEmpty(svc.Gpu.LastError)) W("  错误: " + svc.Gpu.LastError);

            W();
            W("[nvidia-smi 原始输出]");
            try { W(RunSmi()); } catch (Exception ex) { W("  失败: " + ex.Message); }

            W("=========================================================");
            return sb.ToString();
        }

        private static string GetCim(string cls, string prop)
        {
            using (var s = new ManagementObjectSearcher("SELECT " + prop + " FROM " + cls))
            using (var c = s.Get())
            {
                foreach (ManagementObject o in c)
                {
                    var v = o[prop];
                    return v?.ToString() ?? "";
                }
            }
            return "";
        }

        private static string RunSmi()
        {
            string path = Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");
            if (!File.Exists(path)) return "  未找到 nvidia-smi";
            var psi = new ProcessStartInfo(path, "--query-gpu=name,temperature.gpu,fan.speed,power.draw,clocks.sm --format=csv")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using (var p = Process.Start(psi))
            {
                string outp = p.StandardOutput.ReadToEnd();
                p.WaitForExit(4000);
                return outp;
            }
        }

        public static string Save(MonitorService svc)
        {
            string text = Build(svc);
            try { File.WriteAllText(LogPath, text, Encoding.UTF8); } catch { }
            return text;
        }

        public static void ShowDialog(MonitorService svc)
        {
            string text = Save(svc);
            var f = new Form
            {
                Text = "硬件接口诊断",
                Size = new System.Drawing.Size(780, 640),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Theme.Bg,
                ForeColor = Theme.Text,
                Font = Theme.FN(9f),
                MinimizeBox = false,
                ShowIcon = false
            };
            var tb = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 20, 26),
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.None,
                Font = Theme.FN(9f),
                Text = text
            };
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Theme.Bg };
            var btnCopy = new Button
            {
                Text = "复制",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Theme.Text,
                BackColor = Theme.CardHi,
                Size = new System.Drawing.Size(90, 30),
                Location = new System.Drawing.Point(10, 7)
            };
            btnCopy.FlatAppearance.BorderColor = Theme.Border;
            btnCopy.Click += (s, e) => { try { Clipboard.SetText(text); btnCopy.Text = "已复制"; } catch { } };

            var btnOpen = new Button
            {
                Text = "打开日志文件",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Theme.Text,
                BackColor = Theme.CardHi,
                Size = new System.Drawing.Size(120, 30),
                Location = new System.Drawing.Point(110, 7)
            };
            btnOpen.FlatAppearance.BorderColor = Theme.Border;
            btnOpen.Click += (s, e) => { try { Process.Start("notepad.exe", LogPath); } catch { } };

            bar.Controls.Add(btnOpen);
            bar.Controls.Add(btnCopy);
            f.Controls.Add(tb);
            f.Controls.Add(bar);
            f.ShowDialog();
        }
    }
}

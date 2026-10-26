using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace PredatorControl
{
    /// <summary>
    /// GPU 状态采集：优先使用 NVIDIA 驱动接口 NVML（无进程开销），
    /// 不可用时退回 nvidia-smi。
    /// </summary>
    public sealed class GpuMonitor : IDisposable
    {
        // ---------- NVML ----------
        private const int NVML_SUCCESS = 0;
        private const int NVML_TEMPERATURE_GPU = 0;
        private const int NVML_CLOCK_SM = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct NvmlUtilization { public uint Gpu; public uint Memory; }

        [StructLayout(LayoutKind.Sequential)]
        private struct NvmlMemory { public ulong Total; public ulong Free; public ulong Used; }

        [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
        private static extern int nvmlInit_v2();

        [DllImport("nvml.dll", EntryPoint = "nvmlShutdown")]
        private static extern int nvmlShutdown();

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
        private static extern int nvmlDeviceGetHandleByIndex(uint index, out IntPtr device);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetTemperature")]
        private static extern int nvmlDeviceGetTemperature(IntPtr device, int sensor, out uint temp);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetFanSpeed")]
        private static extern int nvmlDeviceGetFanSpeed(IntPtr device, out uint speed);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetPowerUsage")]
        private static extern int nvmlDeviceGetPowerUsage(IntPtr device, out uint milliwatts);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetClockInfo")]
        private static extern int nvmlDeviceGetClockInfo(IntPtr device, int type, out uint clock);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetUtilizationRates")]
        private static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out NvmlUtilization util);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetMemoryInfo")]
        private static extern int nvmlDeviceGetMemoryInfo(IntPtr device, out NvmlMemory mem);

        private IntPtr _dev = IntPtr.Zero;
        private bool _nvmlReady;
        private readonly string _smiPath;

        public bool Available { get; private set; } = true;
        public bool UsingNvml => _nvmlReady;
        public string Name { get; private set; } = "";
        public int Temp { get; private set; } = -1;
        public double Power { get; private set; } = -1;
        public double Clock { get; private set; } = -1;
        public int FanPercent { get; private set; } = -1;
        public int Utilization { get; private set; } = -1;
        public double MemUsed { get; private set; } = -1;
        public double MemTotal { get; private set; } = -1;
        public string LastError { get; private set; } = "";

        public GpuMonitor()
        {
            _smiPath = LocateSmi();

            try
            {
                if (nvmlInit_v2() == NVML_SUCCESS &&
                    nvmlDeviceGetHandleByIndex(0, out IntPtr dev) == NVML_SUCCESS &&
                    dev != IntPtr.Zero)
                {
                    _dev = dev;
                    _nvmlReady = true;
                }
            }
            catch (DllNotFoundException) { _nvmlReady = false; }
            catch (EntryPointNotFoundException) { _nvmlReady = false; }
            catch { _nvmlReady = false; }

            if (!_nvmlReady && _smiPath == null)
            {
                Available = false;
                LastError = "未找到 NVIDIA 驱动接口";
            }

            Name = QueryName();
        }

        private static string LocateSmi()
        {
            string sys = System.IO.Path.Combine(Environment.SystemDirectory ?? @"C:\Windows\System32", "nvidia-smi.exe");
            if (System.IO.File.Exists(sys)) return sys;
            string pf = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                               @"NVIDIA Corporation\NVSMI\nvidia-smi.exe");
            return System.IO.File.Exists(pf) ? pf : null;
        }

        private string QueryName()
        {
            if (_smiPath == null) return "NVIDIA GPU";
            try
            {
                var psi = new ProcessStartInfo(_smiPath, "--query-gpu=name --format=csv,noheader")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                using (var p = Process.Start(psi))
                {
                    string line = p.StandardOutput.ReadLine();
                    p.WaitForExit(3000);
                    return string.IsNullOrWhiteSpace(line) ? "NVIDIA GPU" : line.Trim();
                }
            }
            catch { return "NVIDIA GPU"; }
        }

        public void Poll()
        {
            if (_nvmlReady) { PollNvml(); return; }
            PollSmi();
        }

        private void PollNvml()
        {
            try
            {
                if (nvmlDeviceGetTemperature(_dev, NVML_TEMPERATURE_GPU, out uint t) == NVML_SUCCESS)
                    Temp = (int)t;

                if (nvmlDeviceGetPowerUsage(_dev, out uint mw) == NVML_SUCCESS)
                    Power = mw / 1000.0;

                if (nvmlDeviceGetClockInfo(_dev, NVML_CLOCK_SM, out uint clk) == NVML_SUCCESS)
                    Clock = clk;

                FanPercent = nvmlDeviceGetFanSpeed(_dev, out uint fan) == NVML_SUCCESS ? (int)fan : -1;

                if (nvmlDeviceGetUtilizationRates(_dev, out NvmlUtilization u) == NVML_SUCCESS)
                    Utilization = (int)u.Gpu;

                if (nvmlDeviceGetMemoryInfo(_dev, out NvmlMemory m) == NVML_SUCCESS)
                {
                    MemUsed = m.Used / 1048576.0;
                    MemTotal = m.Total / 1048576.0;
                }

                Available = Temp >= 0;
                LastError = "";
            }
            catch (Exception ex)
            {
                Available = false;
                LastError = ex.Message;
            }
        }

        private void PollSmi()
        {
            if (_smiPath == null) { Available = false; return; }
            try
            {
                const string q = "name,temperature.gpu,power.draw,clocks.sm,fan.speed,utilization.gpu,memory.used,memory.total";
                var psi = new ProcessStartInfo(_smiPath)
                {
                    Arguments = "--query-gpu=" + q + " --format=csv,noheader,nounits",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                using (var p = Process.Start(psi))
                {
                    string line = p.StandardOutput.ReadLine();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit(3000);
                    if (string.IsNullOrWhiteSpace(line)) { Available = false; return; }

                    var parts = line.Split(',');
                    for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();

                    if (string.IsNullOrEmpty(Name) || Name == "NVIDIA GPU") Name = parts.Length > 0 ? parts[0] : Name;
                    Temp = Num(parts, 1);
                    Power = NumF(parts, 2);
                    Clock = NumF(parts, 3);
                    FanPercent = Num(parts, 4);
                    Utilization = Num(parts, 5);
                    MemUsed = NumF(parts, 6);
                    MemTotal = NumF(parts, 7);
                    Available = true;
                    LastError = "";
                }
            }
            catch (Exception ex)
            {
                Available = false;
                LastError = ex.Message;
            }
        }

        private static int Num(string[] p, int i)
        {
            double d = NumF(p, i);
            return d < 0 ? -1 : (int)Math.Round(d);
        }

        private static double NumF(string[] p, int i)
        {
            if (i >= p.Length) return -1;
            string s = p[i];
            if (string.IsNullOrEmpty(s) || s.Equals("[N/A]", StringComparison.OrdinalIgnoreCase)
                || s.Equals("N/A", StringComparison.OrdinalIgnoreCase)) return -1;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : -1;
        }

        public void Dispose()
        {
            if (_nvmlReady)
            {
                try { nvmlShutdown(); } catch { }
                _nvmlReady = false;
                _dev = IntPtr.Zero;
            }
        }
    }
}

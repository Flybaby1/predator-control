using System;
using System.Collections.Generic;
using System.Threading;

namespace PredatorControl
{
    public enum FanMode { Auto, Max, Manual, Curve }

    public sealed class Snapshot
    {
        public long Tick;
        public DateTime Time = DateTime.Now;
        public int CpuTemp = -1;
        public int GpuTemp = -1;
        public int CpuFanRpm = -1;
        public int GpuFanRpm = -1;
        public int CpuFanPct = -1;
        public int GpuFanPct = -1;
        public int AcerFanMode = -1;

        public int GpuNvTemp = -1;
        public double GpuPower = -1;
        public double GpuClock = -1;
        public int GpuUtil = -1;
        public double GpuMemUsed = -1;
        public double GpuMemTotal = -1;
        public string GpuName = "";

        public string Error = "";
        public bool HardwareOk;
    }

    public sealed class FanCurve
    {
        // 温度 -> 转速百分比，按温度升序
        public List<KeyValuePair<int, int>> Points = new List<KeyValuePair<int, int>>
        {
            new KeyValuePair<int,int>(35, 22),
            new KeyValuePair<int,int>(50, 30),
            new KeyValuePair<int,int>(62, 42),
            new KeyValuePair<int,int>(72, 60),
            new KeyValuePair<int,int>(80, 78),
            new KeyValuePair<int,int>(88, 95),
            new KeyValuePair<int,int>(95, 100),
        };

        public int Evaluate(int temp)
        {
            var pts = Points;
            if (pts == null || pts.Count == 0) return 50;
            if (temp <= pts[0].Key) return pts[0].Value;
            if (temp >= pts[pts.Count - 1].Key) return pts[pts.Count - 1].Value;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                int t0 = pts[i].Key, t1 = pts[i + 1].Key;
                if (temp >= t0 && temp <= t1)
                {
                    int v0 = pts[i].Value, v1 = pts[i + 1].Value;
                    if (t1 == t0) return v1;
                    double k = (double)(temp - t0) / (t1 - t0);
                    return (int)Math.Round(v0 + k * (v1 - v0));
                }
            }
            return pts[pts.Count - 1].Value;
        }
    }

    /// <summary>后台轮询硬件状态，并执行风扇控制策略</summary>
    public sealed class MonitorService : IDisposable
    {
        public readonly AcerWmi Wmi = new AcerWmi();
        public readonly GpuMonitor Gpu = new GpuMonitor();
        public readonly FanCurve Curve = new FanCurve();

        private Thread _thread;
        private volatile bool _running;
        private int _intervalMs = 1000;

        public FanMode Mode = FanMode.Auto;
        public int ManualCpu = 55;
        public int ManualGpu = 55;

        private int _lastApplied = -1;
        private int _appliedMode = -1;   // 已写入硬件的模式
        private int _hystBase = -1;
        private long _tick;

        public event Action<Snapshot> Updated;
        public event Action<string> Logged;

        public int IntervalMs
        {
            get { return _intervalMs; }
            set { _intervalMs = Math.Clamp(value, 400, 5000); }
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "PredatorPoll" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _thread?.Join(1500); } catch { }
        }

        public bool OpenHardware()
        {
            bool ok = Wmi.Open();
            if (!ok) Log("硬件接口打开失败: " + Wmi.LastError);
            else Log("硬件接口已连接: " + Wmi.InstanceName);
            return ok;
        }

        private void Loop()
        {
            while (_running)
            {
                try { Poll(); }
                catch (Exception ex) { Log("轮询异常: " + ex.Message); }
                Thread.Sleep(_intervalMs);
            }
        }

        private void Poll()
        {
            var s = new Snapshot { Tick = ++_tick, Time = DateTime.Now };

            if (Wmi.Available)
            {
                s.HardwareOk = true;
                if (Wmi.ReadSensor(AcerWmi.SENSOR_CPU_TEMP, out int cpuT)) s.CpuTemp = cpuT;
                if (Wmi.ReadSensor(AcerWmi.SENSOR_GPU_TEMP, out int gpuT)) s.GpuTemp = gpuT;
                if (Wmi.ReadSensor(AcerWmi.SENSOR_CPU_FAN, out int cpuRpm)) s.CpuFanRpm = cpuRpm;
                if (Wmi.ReadSensor(AcerWmi.SENSOR_GPU_FAN, out int gpuRpm)) s.GpuFanRpm = gpuRpm;
                if (Wmi.GetFanPercent(AcerWmi.FAN_CPU, out int cpuPct)) s.CpuFanPct = cpuPct;
                if (Wmi.GetFanPercent(AcerWmi.FAN_GPU, out int gpuPct)) s.GpuFanPct = gpuPct;
                if (Wmi.GetFanMode(out int cm, out int gm)) s.AcerFanMode = cm;

                if (s.CpuTemp < 0 && s.GpuTemp < 0)
                {
                    s.Error = "Acer 传感器无读数：" + Wmi.LastError;
                }
            }
            else
            {
                s.HardwareOk = false;
                s.Error = Wmi.LastError;
            }

            Gpu.Poll();
            s.GpuNvTemp = Gpu.Temp;
            s.GpuPower = Gpu.Power;
            s.GpuClock = Gpu.Clock;
            s.GpuUtil = Gpu.Utilization;
            s.GpuMemUsed = Gpu.MemUsed;
            s.GpuMemTotal = Gpu.MemTotal;
            s.GpuName = Gpu.Name;

            if (s.HardwareOk) ApplyFanPolicy(s);

            Updated?.Invoke(s);
        }

        /// <summary>执行风扇策略</summary>
        private void ApplyFanPolicy(Snapshot s)
        {
            int targetMode;
            switch (Mode)
            {
                case FanMode.Auto: targetMode = AcerWmi.MODE_AUTO; break;
                case FanMode.Max: targetMode = AcerWmi.MODE_TURBO; break;
                default: targetMode = AcerWmi.MODE_CUSTOM; break;
            }

            if (_appliedMode != targetMode)
            {
                if (Wmi.SetFanMode(targetMode, targetMode))
                {
                    _appliedMode = targetMode;
                    _lastApplied = -1;
                    Log("风扇模式切换为 " + ModeName(targetMode));
                }
            }

            if (Mode == FanMode.Auto || Mode == FanMode.Max) return;

            int target;
            if (Mode == FanMode.Manual)
            {
                target = Math.Max(ManualCpu, ManualGpu);
                if (Wmi.SetFanPercent(AcerWmi.FAN_CPU, ManualCpu) && Wmi.SetFanPercent(AcerWmi.FAN_GPU, ManualGpu))
                    _lastApplied = target;
                return;
            }

            // 曲线模式：取 CPU / GPU 较高温度，带 3 度回差避免抖动
            int temp = Math.Max(s.CpuTemp, s.GpuTemp);
            if (temp <= 0) return;
            if (_hystBase < 0) _hystBase = temp;
            if (temp > _hystBase) _hystBase = temp;
            else if (_hystBase - temp > 3) _hystBase = temp;

            target = Curve.Evaluate(_hystBase);
            if (_lastApplied < 0 || Math.Abs(target - _lastApplied) >= 2)
            {
                if (Wmi.SetFanPercent(AcerWmi.FAN_CPU, target) && Wmi.SetFanPercent(AcerWmi.FAN_GPU, target))
                {
                    _lastApplied = target;
                    Log("曲线: " + _hystBase + "°C -> " + target + "%");
                }
            }
        }

        public static string ModeName(int mode)
        {
            switch (mode)
            {
                case AcerWmi.MODE_AUTO: return "自动";
                case AcerWmi.MODE_TURBO: return "最大";
                case AcerWmi.MODE_CUSTOM: return "手动";
                default: return "未知(" + mode + ")";
            }
        }

        /// <summary>恢复自动风扇，退出时调用</summary>
        public bool RestoreAuto()
        {
            _appliedMode = -1;
            return Wmi.Available && Wmi.SetFanMode(AcerWmi.MODE_AUTO, AcerWmi.MODE_AUTO);
        }

        private void Log(string msg)
        {
            try { Logged?.Invoke(msg); } catch { }
        }

        public void Dispose()
        {
            Stop();
            Wmi.Dispose();
        }
    }
}

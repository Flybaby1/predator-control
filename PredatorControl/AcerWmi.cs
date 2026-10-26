using System;
using System.Management;

namespace PredatorControl
{
    /// <summary>
    /// Acer Predator 硬件控制接口 (ACPI WMI: root\WMI -> AcerGamingFunction)
    /// 协议字段参考 Linux 内核 acer-wmi 驱动。
    /// </summary>
    public sealed class AcerWmi : IDisposable
    {
        // ---- 风扇 ID ----
        public const byte FAN_CPU = 0x01;
        public const byte FAN_GPU = 0x04;

        // ---- 风扇行为 bitmap ----
        public const ushort BEHAVIOR_CPU = 0x01;
        public const ushort BEHAVIOR_GPU = 0x08;
        public const ushort BEHAVIOR_BOTH = BEHAVIOR_CPU | BEHAVIOR_GPU;

        // ---- 风扇模式 ----
        public const int MODE_AUTO = 0x01;
        public const int MODE_TURBO = 0x02;
        public const int MODE_CUSTOM = 0x03;

        // ---- Predator V4 传感器 ID ----
        public const byte SENSOR_CPU_TEMP = 0x01;
        public const byte SENSOR_CPU_FAN = 0x02;
        public const byte SENSOR_EXT_TEMP2 = 0x03;
        public const byte SENSOR_GPU_FAN = 0x06;
        public const byte SENSOR_GPU_TEMP = 0x0A;

        private ManagementObject _game;
        private readonly object _lock = new object();

        public string LastError { get; private set; } = "";
        public bool Available { get { lock (_lock) return _game != null; } }
        public string InstanceName { get; private set; } = "";

        public bool Open()
        {
            lock (_lock)
            {
                try
                {
                    _game?.Dispose();
                }
                catch { }
                _game = null;

                try
                {
                    using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM AcerGamingFunction"))
                    using (var col = searcher.Get())
                    {
                        foreach (ManagementObject mo in col)
                        {
                            var path = mo.Path?.Clone() as ManagementPath;
                            if (path == null) continue;
                            _game = new ManagementObject(path);
                            try { InstanceName = (string)mo["InstanceName"]; } catch { InstanceName = ""; }
                            break;
                        }
                    }

                    if (_game == null)
                    {
                        LastError = "未找到 AcerGamingFunction 实例。请以管理员身份运行（PredatorSense 的硬件接口受权限保护）。";
                        return false;
                    }
                    return true;
                }
                catch (ManagementException ex)
                {
                    LastError = ex.ErrorCode == ManagementStatus.AccessDenied
                        ? "访问被拒绝：需要管理员权限才能读取 Acer 硬件接口。"
                        : ex.Message;
                    _game = null;
                    return false;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    _game = null;
                    return false;
                }
            }
        }

        private ulong? Call(string method, object input)
        {
            lock (_lock)
            {
                if (_game == null) { LastError = "接口未就绪"; return null; }
                try
                {
                    using (var inParams = _game.GetMethodParameters(method))
                    {
                        if (input != null) inParams["gmInput"] = input;
                        using (var outParams = _game.InvokeMethod(method, inParams, null))
                        {
                            if (outParams == null) { LastError = method + " 无返回"; return null; }
                            object v = outParams["gmOutput"];
                            if (v == null) return null;
                            return Convert.ToUInt64(v);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LastError = method + ": " + ex.Message;
                    return null;
                }
            }
        }

        // ---------------- 传感器 ----------------

        /// <summary>读取 Predator V4 传感器原始读数</summary>
        public bool ReadSensor(byte sensorId, out int value)
        {
            value = -1;
            uint cmd = (uint)(0x0001u | ((uint)sensorId << 8));
            var r = Call("GetGamingSysInfo", cmd);
            if (r == null) return false;
            ulong v = r.Value;
            if ((v & 0xFF) != 0) return false;            // status != 0 => 不支持
            value = (int)((v >> 8) & 0xFFFF);
            return true;
        }

        /// <summary>读取风扇当前转速百分比 (0-100)</summary>
        public bool GetFanPercent(byte fanId, out int percent)
        {
            percent = -1;
            var r = Call("GetGamingFanSpeed", (uint)fanId);
            if (r == null) return false;
            if ((r.Value & 0xFF) != 0) return false;
            percent = (int)((r.Value >> 8) & 0xFF);
            return true;
        }

        /// <summary>设置风扇转速百分比 (0-100)，需风扇处于 CUSTOM 模式</summary>
        public bool SetFanPercent(byte fanId, int percent)
        {
            int pct = Math.Clamp(percent, 0, 100);
            ulong input = (ulong)fanId | ((ulong)(uint)pct << 8);
            var r = Call("SetGamingFanSpeed", input);
            return r != null && (r.Value & 0xFF) == 0;
        }

        /// <summary>读取风扇模式</summary>
        public bool GetFanMode(out int cpuMode, out int gpuMode)
        {
            cpuMode = gpuMode = -1;
            var r = Call("GetGamingFanBehavior", (uint)BEHAVIOR_BOTH);
            if (r == null) return false;
            if ((r.Value & 0xFF) != 0) return false;
            cpuMode = (int)((r.Value >> 8) & 0x3);
            gpuMode = (int)((r.Value >> 14) & 0x3);
            return true;
        }

        /// <summary>设置风扇模式 (AUTO / TURBO / CUSTOM)</summary>
        public bool SetFanMode(int cpuMode, int gpuMode)
        {
            ulong input = (ulong)BEHAVIOR_BOTH
                        | ((ulong)(cpuMode & 0x3) << 16)
                        | ((ulong)(gpuMode & 0x3) << 22);
            var r = Call("SetGamingFanBehavior", input);
            return r != null && (r.Value & 0xFF) == 0;
        }

        /// <summary>读取杂项设置（0x0B = 平台性能模式）</summary>
        public bool GetMisc(byte index, out int value)
        {
            value = -1;
            var r = Call("GetGamingMiscSetting", (uint)index);
            if (r == null) return false;
            if ((r.Value & 0xFF) != 0) return false;
            value = (int)((r.Value >> 8) & 0xFF);
            return true;
        }

        public bool SetMisc(byte index, int value)
        {
            ulong input = (ulong)index | ((ulong)(uint)Math.Clamp(value, 0, 255) << 8);
            var r = Call("SetGamingMiscSetting", input);
            return r != null && (r.Value & 0xFF) == 0;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                try { _game?.Dispose(); } catch { }
                _game = null;
            }
        }
    }
}

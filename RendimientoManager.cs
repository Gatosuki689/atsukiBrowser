using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Timers;

namespace atsukibrowser
{
    public class DatosRendimiento
    {
        public int Cpu  { get; set; }
        public int Ram  { get; set; }
        public int Disco { get; set; }
        public int Red  { get; set; }
    }

    public class RendimientoManager : IDisposable
    {
        private PerformanceCounter? _cpu;
        private PerformanceCounter? _disco;
        private PerformanceCounter? _redEnv;
        private PerformanceCounter? _redRec;
        private System.Timers.Timer _timer;
        public event Action<DatosRendimiento>? DatosActualizados;

        public RendimientoManager()
        {
            try { _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _cpu.NextValue(); } catch { }
            try { _disco = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total"); _disco.NextValue(); } catch { }
            try
            {
                var cat = new PerformanceCounterCategory("Network Interface");
                var inst = cat.GetInstanceNames();
                if (inst.Length > 0)
                {
                    _redEnv = new PerformanceCounter("Network Interface", "Bytes Sent/sec", inst[0]);
                    _redRec = new PerformanceCounter("Network Interface", "Bytes Received/sec", inst[0]);
                    _redEnv.NextValue(); _redRec.NextValue();
                }
            }
            catch { }

            _timer = new System.Timers.Timer(3000);
            _timer.Elapsed += OnTick;
        }

        public void Iniciar() => _timer.Start();
        public void Detener() => _timer.Stop();

        private void OnTick(object? sender, ElapsedEventArgs e)
        {
            try
            {
                int cpu   = _cpu   != null ? Math.Clamp((int)Math.Round(_cpu.NextValue()),   0, 100) : 0;
                int disco = _disco != null ? Math.Clamp((int)Math.Round(_disco.NextValue()), 0, 100) : 0;

                var mem = new MEMORYSTATUSEX();
                mem.dwLength = (uint)Marshal.SizeOf(mem);
                GlobalMemoryStatusEx(ref mem);
                int ram = (int)mem.dwMemoryLoad;

                // Red en KB/s
                int red = 0;
                if (_redEnv != null && _redRec != null)
                    red = (int)((_redEnv.NextValue() + _redRec.NextValue()) / 1024);

                DatosActualizados?.Invoke(new DatosRendimiento { Cpu = cpu, Ram = ram, Disco = disco, Red = red });
            }
            catch { }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile;
            public ulong ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        public void Dispose() { _timer.Stop(); _timer.Dispose(); _cpu?.Dispose(); _disco?.Dispose(); _redEnv?.Dispose(); _redRec?.Dispose(); }
    }
}
// Flarial.Launcher/SystemTuning/ReachPatcher.cs
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Flarial.Launcher.SystemTuning
{
    public static class ReachPatcher
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("psapi.dll", SetLastError = true)]
        static extern bool EnumProcessModules(IntPtr hProcess, [Out] IntPtr[] lphModule, uint cb, out uint lpcbNeeded);

        [DllImport("psapi.dll", SetLastError = true)]
        static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

        const uint PROCESS_VM_READ = 0x0010;
        const uint PROCESS_VM_WRITE = 0x0020;
        const uint PROCESS_VM_OPERATION = 0x0008;
        const uint PAGE_EXECUTE_READWRITE = 0x40;

        const long REACH_OFFSET = 0xE62CEB0;

        static IntPtr GetModuleBaseAddress(IntPtr hProcess, string moduleName)
        {
            IntPtr[] hMods = new IntPtr[1024];
            uint cbNeeded;
            if (EnumProcessModules(hProcess, hMods, (uint)(hMods.Length * IntPtr.Size), out cbNeeded))
            {
                for (int i = 0; i < cbNeeded / IntPtr.Size; i++)
                {
                    StringBuilder sb = new StringBuilder(260);
                    if (GetModuleFileNameEx(hProcess, hMods[i], sb, (uint)sb.Capacity) > 0)
                    {
                        if (sb.ToString().Contains(moduleName))
                            return hMods[i];
                    }
                }
            }
            return IntPtr.Zero;
        }

        public static bool ApplyReach(float reach)
        {
            if (reach <= 0f || reach > 7.0f)
                throw new ArgumentOutOfRangeException(nameof(reach), "Reach must be between 0.1 and 7.0");

            Process[] processes = Process.GetProcessesByName("Minecraft.Windows");
            if (processes.Length == 0) return false;

            int pid = processes[0].Id;
            IntPtr hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, pid);
            if (hProcess == IntPtr.Zero) return false;

            IntPtr moduleBase = GetModuleBaseAddress(hProcess, "Minecraft.Windows.exe");
            if (moduleBase == IntPtr.Zero)
            {
                CloseHandle(hProcess);
                return false;
            }

            IntPtr targetAddr = new IntPtr(moduleBase.ToInt64() + REACH_OFFSET);

            uint oldProtect;
            if (!VirtualProtectEx(hProcess, targetAddr, 4, PAGE_EXECUTE_READWRITE, out oldProtect))
            {
                CloseHandle(hProcess);
                return false;
            }

            byte[] reachBytes = BitConverter.GetBytes(reach);
            int bytesWritten;
            bool writeOk = WriteProcessMemory(hProcess, targetAddr, reachBytes, 4, out bytesWritten);

            VirtualProtectEx(hProcess, targetAddr, 4, oldProtect, out _);
            CloseHandle(hProcess);

            return writeOk && bytesWritten == 4;
        }

        public static bool IsMinecraftRunning()
        {
            return Process.GetProcessesByName("Minecraft.Windows").Length > 0;
        }
    }
}
using System;
using System.Collections.Generic;
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
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesRead);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        const uint PROCESS_VM_READ = 0x0010;
        const uint PROCESS_VM_WRITE = 0x0020;
        const uint PROCESS_VM_OPERATION = 0x0008;
        const uint PAGE_EXECUTE_READWRITE = 0x40;
        const uint MEM_COMMIT = 0x1000;
        const uint PAGE_NOACCESS = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

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

        static (byte[] bytes, bool[] mask) ParsePattern(string pattern)
        {
            var parts = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            byte[] bytes = new byte[parts.Length];
            bool[] mask = new bool[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "?" || parts[i] == "??")
                {
                    bytes[i] = 0x00;
                    mask[i] = false;
                }
                else
                {
                    bytes[i] = Convert.ToByte(parts[i], 16);
                    mask[i] = true;
                }
            }
            return (bytes, mask);
        }

        static List<IntPtr> ScanMemory(IntPtr hProcess, byte[] patternBytes, bool[] mask)
        {
            var addresses = new List<IntPtr>();
            MEMORY_BASIC_INFORMATION mbi;
            IntPtr address = IntPtr.Zero;

            while (VirtualQueryEx(hProcess, address, out mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) != 0)
            {
                if (mbi.State == MEM_COMMIT && mbi.Protect != PAGE_NOACCESS)
                {
                    long regionSize = mbi.RegionSize.ToInt64();
                    byte[] buffer = new byte[regionSize];
                    if (ReadProcessMemory(hProcess, mbi.BaseAddress, buffer, (uint)buffer.Length, out _))
                    {
                        for (long i = 0; i <= buffer.Length - patternBytes.Length; i++)
                        {
                            bool found = true;
                            for (int j = 0; j < patternBytes.Length; j++)
                            {
                                if (mask[j] && buffer[i + j] != patternBytes[j])
                                {
                                    found = false;
                                    break;
                                }
                            }
                            if (found)
                                addresses.Add(new IntPtr(mbi.BaseAddress.ToInt64() + i));
                        }
                    }
                }
                address = new IntPtr(address.ToInt64() + mbi.RegionSize.ToInt64());
            }
            return addresses;
        }

        static void WriteBytes(IntPtr hProcess, IntPtr address, byte[] data)
        {
            uint oldProtect;
            VirtualProtectEx(hProcess, address, (uint)data.Length, PAGE_EXECUTE_READWRITE, out oldProtect);
            WriteProcessMemory(hProcess, address, data, (uint)data.Length, out _);
            VirtualProtectEx(hProcess, address, (uint)data.Length, oldProtect, out _);
        }

        static void NopBytes(IntPtr hProcess, IntPtr address, int length)
        {
            byte[] nops = new byte[length];
            for (int i = 0; i < length; i++) nops[i] = 0x90;
            WriteBytes(hProcess, address, nops);
        }

        static byte[] ReadBytes(IntPtr hProcess, IntPtr address, int size)
        {
            byte[] buffer = new byte[size];
            ReadProcessMemory(hProcess, address, buffer, (uint)size, out _);
            return buffer;
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

            var (patternBytes, mask) = ParsePattern("74 ? F3 0F 5D 35");
            var matches = ScanMemory(hProcess, patternBytes, mask);
            if (matches.Count == 0)
            {
                CloseHandle(hProcess);
                return false;
            }

            IntPtr sigAddr = matches[0];
            IntPtr addrBytesCreative = sigAddr + 2;

            byte[] ins = ReadBytes(hProcess, addrBytesCreative, 8);
            int disp = ins[4] | (ins[5] << 8) | (ins[6] << 16) | (ins[7] << 24);
            long addrReach = addrBytesCreative.ToInt64() + 8 + disp;

            NopBytes(hProcess, sigAddr, 2);

            byte[] reachBytes = BitConverter.GetBytes(reach);
            WriteBytes(hProcess, new IntPtr(addrReach), reachBytes);

            CloseHandle(hProcess);
            return true;
        }

        public static bool IsMinecraftRunning()
        {
            return Process.GetProcessesByName("Minecraft.Windows").Length > 0;
        }
    }
}
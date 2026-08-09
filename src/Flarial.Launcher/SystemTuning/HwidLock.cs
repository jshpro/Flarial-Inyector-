// Flarial.Launcher/SystemTuning/HwidLock.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Flarial.Launcher.SystemTuning
{
    public static class HwidLock
    {
        private static readonly HashSet<string> AllowedSerials = new()
        {
            "c03ca7c1",
            "82f0e752",
            "1a67550f",
            "268f7594"
        };

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GetVolumeInformation(
            string? rootPathName,
            StringBuilder? volumeNameBuffer,
            int volumeNameSize,
            out uint volumeSerialNumber,
            out uint maximumComponentLength,
            out uint fileSystemFlags,
            StringBuilder? fileSystemNameBuffer,
            int fileSystemNameSize);

        private static string GetRawSerial()
        {
            uint serial = 0;
            if (GetVolumeInformation("C:\\", null, 0, out serial, out _, out _, null, 0))
            {
                return serial.ToString("x");
            }
            return "";
        }

        private static string CleanHWID(string raw)
        {
            string cleaned = raw.Replace("lv", "", StringComparison.OrdinalIgnoreCase);
            cleaned = cleaned.Replace("-", "");
            cleaned = cleaned.Trim();
            return cleaned.ToLowerInvariant();
        }

        public static bool IsAuthorized()
        {
            try
            {
                string rawSerial = GetRawSerial();
                if (string.IsNullOrEmpty(rawSerial))
                    return false;

                string cleanedSerial = CleanHWID(rawSerial);
                return AllowedSerials.Contains(cleanedSerial);
            }
            catch
            {
                return false;
            }
        }
    }
}
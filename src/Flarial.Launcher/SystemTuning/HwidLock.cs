using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Flarial.Launcher.SystemTuning
{
    public static class HwidLock
    {
        // Coloca aquí el serial esperado (en minúsculas, sin guiones)
        private const string RequiredSerial = "c03ca7c1"; // ejemplo

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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        private static string GetRawSerial()
        {
            uint serial = 0;
            if (GetVolumeInformation("C:\\", null, 0, out serial, out _, out _, null, 0))
            {
                return serial.ToString("x"); // convierte a hexadecimal (sin "0x")
            }
            return "";
        }

        private static string CleanHWID(string raw)
        {
            // Eliminar ocurrencias de "lv"/"LV"
            string cleaned = raw.Replace("lv", "", StringComparison.OrdinalIgnoreCase);
            // Eliminar guiones
            cleaned = cleaned.Replace("-", "");
            // Eliminar espacios en blanco
            cleaned = cleaned.Trim();
            // Convertir a minúsculas
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

                // Descomenta la siguiente línea si necesitas depurar (aparecerá un MessageBox con el serial detectado)
                // MessageBox(IntPtr.Zero, $"Serial detectado: {cleanedSerial}\nSerial esperado: {RequiredSerial}", "Debug HWID", 0);

                return cleanedSerial == RequiredSerial;
            }
            catch
            {
                return false;
            }
        }
    }
}
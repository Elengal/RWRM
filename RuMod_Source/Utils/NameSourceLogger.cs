using System;
using System.IO;
using RimWorld;
using Verse;
using RuMod.Utils;

namespace RuMod
{
    /// <summary>
    /// Пишет в файл, откуда взято имя/фамилия/кличка при вызове NameBank.GetName (создание/спавн пешки).
    /// Включается в настройках мода «Логировать источники имён».
    /// </summary>
    public static class NameSourceLogger
    {
        public static bool IsEnabled = false;

        private static string _filePath;
        private static object _lock = new object();

        /// <summary>Путь к файлу лога: по умолчанию рабочий стол, при ошибке — папка Config RimWorld.</summary>
        public static string GetFilePath()
        {
            if (_filePath == null)
            {
                try
                {
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    _filePath = Path.Combine(desktopPath, "RuMod_NameSources.txt");
                }
                catch (Exception ex)
                {
                    RuModLog.NameSourceLoggerDesktopFailed(ex);
                    _filePath = Path.Combine(GenFilePaths.ConfigFolderPath, "RuMod_NameSources.txt");
                }
            }
            return _filePath;
        }

        /// <summary>
        /// Записать одну строку: слот (имя/фамилия/кличка), пол, выданное значение, файл-источник, категория банка.
        /// </summary>
        public static void Log(PawnNameSlot slot, Gender gender, string name, string fileUsed, string bankCategory)
        {
            if (!IsEnabled || string.IsNullOrEmpty(name)) return;
            try
            {
                string slotStr = slot == PawnNameSlot.First ? "имя" : (slot == PawnNameSlot.Last ? "фамилия" : "кличка");
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {slotStr} | {gender} | «{name}» | файл: {fileUsed ?? "—"} | банк: {bankCategory ?? "—"}\r\n";
                lock (_lock)
                {
                    File.AppendAllText(GetFilePath(), line, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                RuModLog.NameSourceLoggerWriteFailed(ex);
            }
        }
    }
}

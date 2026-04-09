using System;
using System.Collections.Generic;
using System.IO;
using RimWorld;
using Verse;
using LudeonTK;
using RuMod.Utils;

namespace RuMod.Utils
{
    /// <summary>
    /// Статистика переводов — команда в DevMode.
    /// </summary>
    public static class TranslationStats
    {
        // Временно отключено — снижает нагрузку при загрузке. Раскомментировать при необходимости.
        // [DebugAction("RuMod", "Статистика переводов", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Entry)]
        public static void ShowStats()
        {
            var active = LanguageDatabase.activeLanguage;
            var defLang = LanguageDatabase.defaultLanguage;
            if (active == null || defLang == null)
            {
                Messages.Message("Язык не выбран.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            defLang.LoadData();
            active.LoadData();
            int totalKeys = defLang.keyedReplacements.Count;
            int translatedKeys = 0;
            foreach (var kv in defLang.keyedReplacements)
            {
                if (active.HaveTextForKey(kv.Key, false))
                    translatedKeys++;
            }

            int missing = totalKeys - translatedKeys;
            float percent = totalKeys > 0 ? (float)translatedKeys / totalKeys * 100f : 0f;

            string msg = $"[{active.folderName}]\n" +
                         $"Всего ключей (от англ.): {totalKeys}\n" +
                         $"Переведено: {translatedKeys}\n" +
                         $"Отсутствует: {missing}\n" +
                         $"Покрытие: {percent:F1}%";

            RuModLog.TranslationStatsLog(msg);
            Messages.Message(msg.Replace("\n", " | "), MessageTypeDefOf.NeutralEvent, false);
        }

        // Временно отключено — снижает нагрузку при загрузке. Раскомментировать при необходимости.
        // [DebugAction("RuMod", "Экспорт недостающих в файл", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Entry)]
        public static void ExportMissingToFile()
        {
            var active = LanguageDatabase.activeLanguage;
            var defLang = LanguageDatabase.defaultLanguage;
            if (active == null || defLang == null)
            {
                Messages.Message("Язык не выбран.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (active == defLang)
            {
                Messages.Message("Активный язык — английский, экспорт не нужен.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            defLang.LoadData();
            active.LoadData();

            string path;
            try
            {
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RimWorld_MissingTranslations_Export.txt");
            }
            catch
            {
                path = Path.Combine(GenFilePaths.ConfigFolderPath, "MissingTranslations_RuMod_Export.txt");
            }

            var lines = new List<string>();
            foreach (var kv in defLang.keyedReplacements)
            {
                if (!active.HaveTextForKey(kv.Key, false))
                {
                    string src = defLang.GetKeySourceFileAndLine(kv.Key);
                    string val = kv.Value.value.Replace("\n", "\\n");
                    lines.Add($"{kv.Key} '{val}' (English: {src})");
                }
            }

            try
            {
                string header = $"========== Недостающие переводы [{active.folderName}] ({lines.Count}) ==========\r\n";
                File.WriteAllText(path, header + string.Join(Environment.NewLine, lines));
                Messages.Message($"Сохранено {lines.Count} ключей в:\n{path}", MessageTypeDefOf.PositiveEvent, false);
                RuModLog.TranslationStatsExportPath(path);
            }
            catch (Exception ex)
            {
                RuModLog.TranslationStatsWriteFailed(ex);
                Messages.Message($"Ошибка: {ex.Message}", MessageTypeDefOf.RejectInput, false);
            }
        }
    }
}

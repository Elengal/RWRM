using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RuMod.Utils
{
    /// <summary>
    /// Централизованный сборник сообщений и логов RuMod, чтобы мод звучал «живым».
    /// Все игровые сообщения мода стараемся оформлять через этот класс.
    /// </summary>
    public static class RuModLog
    {
        // ==== DebugLog / конфликты ====

        public static void DebugLogConflictsDirCreateFailed(string dir, Exception ex)
        {
            Log.Warning($"[RuMod] Не смог завести папку конфликтов DebugLog ('{dir}'). Похоже, мир против порядка: {ex.Message}");
        }

        public static void DebugLogConflictsReadFailed(Exception ex)
        {
            Log.Warning($"[RuMod] Читал файлы конфликтов DebugLog и зачитался до ошибки: {ex.Message}");
        }

        public static void DebugLogConflictFileWriteFailed(string owner, Exception ex)
        {
            Log.Warning($"[RuMod] Пытался записать конфликт с модом '{owner}', но перо сломалось: {ex.Message}");
        }

        public static void DebugLogPatchesInspectFailed(Exception ex)
        {
            Log.Warning($"[RuMod] Заглянул в патчи DebugLog и потерялся в кишках Harmony: {ex.Message}");
        }

        public static void DebugLogTweaksDisabledByConflicts(IEnumerable<string> newConflicts)
        {
            string list = string.Join(", ", newConflicts);
            Log.Warning($"[RuMod] Мои красивые правки окна Debug log вырублены — другие моды толкаются локтями: {list}");
        }

        public static void DebugLogAcceptConflictsFailed(Exception ex)
        {
            Log.Warning($"[RuMod] Хотел великодушно принять конфликты DebugLog, но что‑то пошло не так: {ex.Message}");
        }

        // ==== Общий патчинг / Main ====

        public static void PatchAllFailed(Exception ex)
        {
            Log.Error($"[RuMod] Патчил всё подряд и до‑патчился до ошибки: {ex}");
        }

        public static void PregnancyPatchFailed(Exception ex)
        {
            Log.Warning($"[RuMod] Патч рождения не взлетел, ребёнок — вместе с ошибкой: {ex.Message}");
        }

        public static void DialogDebugOptionClassNotFound()
        {
            Log.Warning("[RuMod] Dialog_DebugOptionListLister спрятался. Перевод списка временно ушёл покурить.");
        }

        public static void DialogDebugOptionPatched()
        {
            Log.Message("[RuMod] Нашёл Dialog_DebugOptionListLister и научил его говорить по‑русски.");
        }

        public static void DialogDebugOptionCtorNotFound()
        {
            Log.Warning("[RuMod] У Dialog_DebugOptionListLister конструктор не обнаружен. Видимо, родился без него.");
        }

        public static void DialogDebugOptionManualPatchFailed(Exception ex)
        {
            Log.Warning($"[RuMod] Пытался пропатчить Dialog_DebugOptionListLister руками — руки соскочили: {ex.Message}");
        }

        // ==== DevModeTranslator / словари ====

        public static void DevModeModDirCreateFailed(string path, Exception ex)
        {
            Log.Warning($"[RuMod] Хотел завести папку с DevMode‑словарями в моде '{path}', но файловая система сказала «нет»: {ex.Message}");
        }

        public static void DevModeDesktopDirCreateFailed(string path, Exception ex)
        {
            Log.Warning($"[RuMod] Пытался сложить DevMode‑словари на рабочий стол ('{path}'), но там уже чья‑то свалка: {ex.Message}");
        }

        public static void DevModeFallbackDirCreateFailed(string path, Exception ex)
        {
            Log.Error($"[RuMod] Даже запасной домик для DevMode‑словарей ('{path}') не построился: {ex}");
        }

        public static void DevModeDictionariesLoadFailed(Exception ex)
        {
            Log.Error($"[RuMod] Распаковывал DevMode‑словари из JSON и порвал упаковку: {ex}");
        }

        public static void DevModeDictionariesSaveFailed(Exception ex)
        {
            Log.Error($"[RuMod] Пытался сохранить DevMode‑словари, но RimWorld швырнул перо: {ex}");
        }

        // ==== WorldFactionsUIUtility ====

        public static void WorldFactionsPrimaryTargetLookupFailed(Exception ex)
        {
            Log.Warning($"[RuMod] WorldFactionsUIUtility_Patch не нашёл основную цель — фракции ушли в подполье: {ex.Message}");
        }

        public static void WorldFactionsFallbackMatched(string typeName, string methodName)
        {
            Log.Message($"[RuMod] WorldFactionsUIUtility_Patch план Б сработал: поймал метод {typeName}.{methodName} и прижал к стенке.");
        }

        public static void WorldFactionsFallbackTargetLookupFailed(Exception ex)
        {
            Log.Warning($"[RuMod] Даже запасную цель для патча фракций умыкнули: {ex.Message}");
        }

        public static void WorldFactionsNoTargetFound()
        {
            Log.Warning("[RuMod] WorldFactionsUIUtility_Patch не нашёл, куда влезть с лимитом фракций. В этот раз без моих хитростей.");
        }

        // ==== StatDef.ValueToString ====

        public static void StatValueFormatException(StatDef def, Exception ex)
        {
            Log.Warning($"[RuMod] StatDef.{def?.defName}.ValueToString поймал FormatException: {ex.Message}. Ладно, выведу число как есть, без прикрас.");
        }

        // ==== Scanner / DevMode framework ====

        public static void DevModeFrameworkInitializing()
        {
            Log.Message("[RuMod] Разворачиваю DevMode‑мозг для перевода. Пристегните ремни.");
        }

        public static void DevModeFrameworkInitialized()
        {
            Log.Message("[RuMod] DevMode‑мозг загружен, можно начинать мучить отладку по‑русски.");
        }

        public static void DevModeScanAttributesError(Exception ex)
        {
            Log.Error($"[RuMod] Рыскал по атрибутам и наступил на грабли: {ex}");
        }

        // ==== Настройки / перенос конфига ====

        public static void OldConfigFileCopiedButNotDeleted(Exception exDel)
        {
            Log.Warning($"[RuMod] Старый файл настроек перетащил, а выкинуть не смог: {exDel.Message}");
        }

        public static void OldConfigCopyFailed(string newPath, Exception ex)
        {
            Log.Warning($"[RuMod] Переезд настроек в {newPath} провалился. Чемоданы рассыпались: {ex.Message}");
        }

        // ==== Лог источников имён ====

        public static void NameSourceLoggerDesktopFailed(Exception ex)
        {
            Log.Warning($"[RuMod] NameSourceLogger: рабочий стол спрятали, беру блокнот из Config: {ex.Message}");
        }

        public static void NameSourceLoggerWriteFailed(Exception ex)
        {
            Log.Warning($"[RuMod] NameSourceLogger споткнулся: {ex.Message}");
        }

        // ==== RimHUD ====

        public static void RimHudNotFound()
        {
            Log.Message("[RuMod] RimHUD не нашёлся. Некому рассказывать, как склоняются «год/года/лет».");
        }

        public static void RimHudGetValueNotFound()
        {
            Log.Warning("[RuMod] У RimHUD пропал GenderRaceAndAgeValue.GetValue. Патч грустит в сторонке.");
        }

        public static void RimHudAgePatchApplied()
        {
            Log.Message("[RuMod] Обнаружил RimHUD, провёл мастер‑класс по «год/года/лет».");
        }

        public static void RimHudPatchFailed(Exception ex)
        {
            Log.Warning($"[RuMod] Пытался учить RimHUD русскому, но он отбивается ошибками: {ex.Message}");
        }

        public static void RimHudAgePostfixError(Exception ex)
        {
            Log.Warning($"[RuMod] В хвостовом патче RimHUD что‑то хрустнуло, но результат я трогать не стал: {ex.Message}");
        }

        // ==== Статистика переводов ====

        public static void TranslationStatsLog(string msg)
        {
            Log.Message($"[RuMod] {msg}");
        }

        public static void TranslationStatsExportPath(string path)
        {
            Log.Message($"[RuMod] Экспорт: {path}");
        }

        public static void TranslationStatsWriteFailed(Exception ex)
        {
            Log.Warning($"[RuMod] Не удалось записать файл статистики переводов: {ex.Message}");
        }

        // ==== Tales / истории ====

        public static void TaleCreateFailed(TaleDef def, object[] args, Exception ex)
        {
            string paramList = args != null
                ? string.Join(", ", args.Select(a => a?.ToString() ?? "null"))
                : "null";
            Log.Error($"[RuMod] Не удалось сочинить байку {def} с параметрами {paramList}: {ex}");
        }

        // ==== Безопасный перевод ====

        public static void FormatExceptionInKey(string key, FormatException ex)
        {
            Log.Warning($"[RuMod] FormatException в ключе '{key}': {ex.Message}. Рассказываю историю без подстановок.");
        }
    }
}


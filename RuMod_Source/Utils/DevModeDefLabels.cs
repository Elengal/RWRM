using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using RuMod.Patches;

namespace RuMod.Utils
{
    /// <summary>
    /// Подстановка перевода из базы дефов для дев-меню.
    ///
    /// ЗАЧЕМ. Подменю дев-режима перечисляют дефы по defName: «Дать черту»,
    /// «Заспавнить предмет», «Добавить состояние». Это 92 места в коде игры,
    /// и вручную это 3281 строка словаря. Но у большинства этих дефов есть
    /// поле label, которое уже переведено через DefInjected для обычной игры —
    /// 591 штука, плюс 845 биографий с полем title. Незачем переводить дважды.
    ///
    /// Подставляем только когда перевод действительно есть: если в label нет
    /// кириллицы, значит деф не переведён, и английский label ничем не лучше
    /// defName — оставляем defName, по нему хотя бы искать можно.
    ///
    /// РАЗВЯЗКА СОВПАДЕНИЙ. У 242 дефов label общий с соседями: у всех частиц
    /// он «Mote», у трёх капсул «drop pod». Подставь его как есть — и 75 разных
    /// пунктов меню станут неразличимы. Поэтому когда label носит больше одного
    /// дефа, дописываем defName в скобках: «Пылинка (Mote_ActivatorCountdownGlow)».
    /// Поиск в дев-меню идёт по показанной подписи (Dialog_Debug.cs:477),
    /// так что искать можно и по-русски, и по имени дефа.
    /// </summary>
    public static class DevModeDefLabels
    {
        private static Dictionary<string, string> map;
        private static LoadedLanguage builtFor;

        /// <summary>Сменился язык или перезагрузились дефы — собрать заново.</summary>
        public static void Reset()
        {
            map = null;
            builtFor = null;
        }

        /// <summary>Перевод для имени дефа, или null если подставлять нечего.</summary>
        public static string TryGet(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            var lang = LanguageDatabase.activeLanguage;
            if (map == null || !ReferenceEquals(lang, builtFor)) Build(lang);
            string found;
            return map.TryGetValue(defName, out found) ? found : null;
        }

        private static void Build(LoadedLanguage lang)
        {
            map = new Dictionary<string, string>();
            builtFor = lang;

            var labelOf = new List<KeyValuePair<string, string>>();
            var howMany = new Dictionary<string, int>();
            // Имя дефа уникально только внутри своего типа. В игре 1173 имени
            // носят сразу несколько типов, и по одному имени нельзя понять, чьё
            // оно: песня Research и кнопка интерфейса Research — разные вещи,
            // а подпись подставилась бы от кнопки. Такие имена пропускаем.
            var carriedBy = new Dictionary<string, int>();

            try
            {
                foreach (Type type in GenDefDatabase.AllDefTypesWithDatabases())
                {
                    foreach (Def def in GenDefDatabase.GetAllDefsInDatabaseForDef(type))
                    {
                        if (def == null || string.IsNullOrEmpty(def.defName)) continue;
                        int seen;
                        carriedBy[def.defName] = carriedBy.TryGetValue(def.defName, out seen) ? seen + 1 : 1;

                        string label = LabelOf(def);
                        if (string.IsNullOrEmpty(label)) continue;
                        // Не переведён — подставлять нечего, defName полезнее.
                        if (!NameReplacerHelper.ContainsRussianCharacters(label)) continue;

                        // Считаем совпадения по тому виду, который увидит человек:
                        // «частица» и «Частица» на экране одно и то же.
                        string shownLabel = label.CapitalizeFirst();
                        labelOf.Add(new KeyValuePair<string, string>(def.defName, shownLabel));
                        int n;
                        howMany[shownLabel] = howMany.TryGetValue(shownLabel, out n) ? n + 1 : 1;
                    }
                }

                int ambiguous = 0;
                foreach (var pair in labelOf)
                {
                    // Имя носит больше одного типа — чьё оно, из подписи не узнать.
                    if (carriedBy[pair.Key] > 1) { ambiguous++; continue; }

                    string shown = pair.Value;
                    if (howMany[shown] > 1)
                        shown = shown + " (" + pair.Key + ")";
                    map[pair.Key] = shown;
                }
                RuModLog.DevModeDefLabelsAmbiguous(ambiguous);

                RuModLog.DevModeDefLabelsBuilt(map.Count, labelOf.Count - map.Count);
            }
            catch (Exception ex)
            {
                RuModLog.DevModeDefLabelsFailed(ex);
            }
        }

        /// <summary>У биографий подпись лежит в title, а не в label.</summary>
        private static string LabelOf(Def def)
        {
            BackstoryDef bs = def as BackstoryDef;
            if (bs != null)
                return !string.IsNullOrEmpty(bs.title) ? bs.title : bs.titleShort;
            return def.label;
        }
    }
}

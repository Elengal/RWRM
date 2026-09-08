using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using RuMod.Utils;

namespace RuMod.Patches
{
    /// <summary>
    /// Русские имена пешек через языковую систему игры.
    ///
    /// ПОЧЕМУ ИМЕННО ЗДЕСЬ. У игры три двери для файлов имён:
    ///   1. Языковая (Strings/) — словники правил, namer-ы. Язык понимает.
    ///   2. GenFile -> Resources.Load — NameBank. Язык НЕ понимает.
    ///   3. Resources.LoadAll -> биографии спонсоров.
    /// Имена пешек идут через вторую. Перехватить её загрузку нельзя:
    /// NameBank.AddNamesFromFile и AddNames — однострочники, Mono встраивает
    /// их в вызывающий код, и патч на них не срабатывает никогда
    /// (проверено опытом: 0 вызовов при 26 вызовах GetName).
    /// Остаётся GetName. Он большой, не встраивается.
    ///
    /// БАНК НЕ ПОРТИМ. Раньше мы затирали его списки русскими — и выключатель
    /// становился обманом: галку снял, а в банке уже лежит наше. Теперь банк
    /// только читаем, а имя отдаём сами. Снял галку — ваниль вернулась целиком,
    /// без перезапуска.
    ///
    /// ЧУЖИЕ МОДЫ НЕ ТЕРЯЕМ. Другой мод мог дописать в банк свои русские имена
    /// через NameBank.AddNames. Поэтому выдаём из объединения: наши словники плюс
    /// то кириллическое, что уже лежит в банке. Английское ваниля отсеивается
    /// проверкой NameReplacerHelper.IsAcceptableRussianName.
    ///
    /// ДВА ШЛЮЗА ПЕРЕД НАМИ. До банка доходит меньше половины пешек:
    ///   PawnBioAndNameGenerator.cs:17  — 25% и все лидеры фракций уходят в
    ///     TryGiveSolidBioTo: имя спонсора вшито в Backstories/Solid с биографией.
    ///   PawnBioAndNameGenerator.cs:416 — ещё 50% от остатка уходят в
    ///     TryGetRandomUnusedSolidName: имя спонсора без биографии.
    /// Итого 0.25 + 0.75*0.5 = 62.5% английских имён мимо нас.
    /// Шлюзы закрывают этот путь, но только если игрок не попросил обратного
    /// (галка «Имена спонсоров Ludeon») и только если имя не внесено им
    /// в «Предпочитаемые имена» — их пропускаем всегда, см. PreferredKept.
    /// </summary>
    public static class NameReroute
    {
        private static readonly FieldInfo fNames = AccessTools.Field(typeof(NameBank), "names");
        private static readonly FieldInfo fType = AccessTools.Field(typeof(NameBank), "nameType");

        // Мод называет словники с суффиксом источника: First_Male_Core.txt.
        // Ключ в LoadedLanguage.stringFiles — точное имя файла без расширения
        // (LoadedLanguage.cs:369-375), поэтому пробуем оба написания.
        // Переименовывать файлы нельзя: на _Core ссылается DefInjected.
        private static readonly string[] suffixes = { "_Core", "" };

        private static readonly Dictionary<string, List<string>> cache =
            new Dictionary<string, List<string>>();
        private static LoadedLanguage cachedLang;
        private static Gender lastFirstGender = Gender.Male;
        private static bool shuffleWarned;

        // ==== настройки ====

        private static RuModSettings Settings
        {
            get { return RuModClass.Instance?.GetSettings<RuModSettings>(); }
        }

        /// <summary>Весь слой имён: словники, фамилии по полу, наследование в семье.</summary>
        public static bool Enabled
        {
            get { var s = Settings; return s != null && s.RussianPawnNames; }
        }

        /// <summary>Игрок попросил оставить имена спонсоров Ludeon как есть.</summary>
        public static bool KeepBackers
        {
            get { var s = Settings; return s != null && s.KeepLudeonBackerNames; }
        }

        /// <summary>Шлюзы работают, только когда есть чем заменять.</summary>
        public static bool GatesActive
        {
            get { return Enabled && !KeepBackers && HasRussianNames(); }
        }

        /// <summary>Настройки изменились или сменился язык — списки собираем заново.</summary>
        public static void ResetCache()
        {
            cache.Clear();
            cachedLang = null;
        }

        // ==== словники ====

        private static List<string> FromLang(LoadedLanguage lang, string key)
        {
            for (int i = 0; i < suffixes.Length; i++)
            {
                List<string> list;
                if (lang.TryGetStringsFromFile(key + suffixes[i], out list)
                    && list != null && list.Count > 0) return list;
            }
            return null;
        }

        /// <summary>Есть ли в активном языке наши словники. Нужно шлюзам.</summary>
        public static bool HasRussianNames()
        {
            var lang = LanguageDatabase.activeLanguage;
            return lang != null && FromLang(lang, "Names/First_Male") != null;
        }

        /// <summary>
        /// Готовый список для выдачи: наши имена плюс кириллица из банка.
        /// null — своих словников нет, пусть работает ваниль.
        /// </summary>
        public static List<string> ListFor(NameBank bank, PawnNameSlot slot, Gender gender)
        {
            var lang = LanguageDatabase.activeLanguage;
            if (lang == null || fNames == null) return null;
            if (!ReferenceEquals(lang, cachedLang)) { cache.Clear(); cachedLang = lang; }

            // Ваниль хранит фамилии без пола, имена и клички — по полу
            // (PawnNameDatabaseShuffled.cs:22-27). У нас фамилии и клички разложены,
            // поэтому недостающий пол берём от последнего запрошенного имени.
            Gender effective = gender;
            Gender bankCell = gender;
            string file;

            if (slot == PawnNameSlot.Last)
            {
                effective = (gender == Gender.None) ? lastFirstGender : gender;
                bankCell = Gender.None;
                file = "Names/Last_" + (effective == Gender.Female ? "Female" : "Male");
            }
            else if (slot == PawnNameSlot.Nick && gender == Gender.None)
            {
                // PawnBioAndNameGenerator.cs:463 в 80% случаев просит кличку без пола,
                // а безполого словника у нас нет — отдаём по полу, в русском кличка склоняется.
                effective = lastFirstGender;
                bankCell = Gender.None;
                file = "Names/Nick_" + (effective == Gender.Female ? "Female" : "Male");
            }
            else
            {
                if (slot == PawnNameSlot.First)
                    file = gender == Gender.Female ? "Names/First_Female"
                         : gender == Gender.Male ? "Names/First_Male" : null;
                else if (slot == PawnNameSlot.Nick)
                    file = gender == Gender.Female ? "Names/Nick_Female"
                         : gender == Gender.Male ? "Names/Nick_Male" : null;
                else
                    file = null;
                if (file == null) return null;
            }

            // В ключе нужен и bankCell: кличка с полом Male и кличка без пола при мужском
            // имени дают одинаковый effective, но чужие клички берут из разных ячеек банка.
            string key = slot + "|" + effective + "|" + bankCell + "|" + bank.GetHashCode();
            List<string> ready;
            if (cache.TryGetValue(key, out ready)) return ready;

            var ours = FromLang(lang, file);
            if (ours == null) { cache[key] = null; return null; }

            var merged = new List<string>(ours);
            var seen = new HashSet<string>(ours);
            MergeForeign(bank, slot, bankCell, effective, merged, seen);

            cache[key] = merged;
            return merged;
        }

        /// <summary>
        /// Добавляет русские имена, дописанные в банк другими модами.
        /// Английское ваниля и латиницу отсеиваем. Для фамилий следим за родом:
        /// ваниль держит их одним списком, поэтому мужские формы склоняем.
        /// </summary>
        private static void MergeForeign(NameBank bank, PawnNameSlot slot, Gender bankCell,
                                         Gender effective, List<string> into, HashSet<string> seen)
        {
            var arr = fNames.GetValue(bank) as List<string>[,];
            if (arr == null) return;
            var cell = arr[(int)bankCell, (int)slot];
            if (cell == null) return;

            foreach (string raw in cell)
            {
                if (!NameReplacerHelper.IsAcceptableRussianName(raw)) continue;

                string candidate = raw;
                if (slot == PawnNameSlot.Last)
                {
                    bool female = NameReplacerHelper.LooksLikeFemaleSurname(raw);
                    if (effective == Gender.Female && !female)
                    {
                        if (!NameReplacerHelper.LooksLikeMaleSurname(raw)) continue;
                        candidate = NameReplacerHelper.ToFemaleSurname(raw);
                    }
                    else if (effective != Gender.Female && female)
                    {
                        candidate = NameReplacerHelper.ToMaleSurname(raw);
                    }
                }

                if (!string.IsNullOrEmpty(candidate) && seen.Add(candidate))
                    into.Add(candidate);
            }
        }

        /// <summary>Как в ваниле: случайный элемент, до 50 перевыборов если имя занято.</summary>
        public static string Pick(List<string> list, bool checkUsed)
        {
            string text = list[Rand.Range(0, list.Count)];
            if (!checkUsed) return text;
            for (int i = 0; i < 50; i++)
            {
                if (!NameUseChecker.NameWordIsUsed(text)) return text;
                text = list[Rand.Range(0, list.Count)];
            }
            return text;
        }

        public static bool IsHumanStandard(NameBank bank)
        {
            return fType == null || "HumanStandard".Equals(fType.GetValue(bank).ToString());
        }

        public static void RememberFirstGender(Gender gender)
        {
            if (gender != Gender.None) lastFirstGender = gender;
        }

        /// <summary>
        /// Игрок сам вписал это имя в «Предпочитаемые имена» (настройки игры).
        /// Ваниль ищет их только среди спонсорских списков, поэтому закрытый шлюз
        /// молча убил бы эту возможность. Такие имена пропускаем всегда.
        /// </summary>
        public static bool PreferredKept(Name name)
        {
            if (name == null) return false;
            var preferred = Prefs.PreferredNames;
            return preferred != null && preferred.Count > 0 && preferred.Contains(name.ToString());
        }

        public static void WarnShuffleMissing()
        {
            if (shuffleWarned) return;
            shuffleWarned = true;
            RuModLog.NameShuffleMethodMissing();
        }
    }

    /// <summary>
    /// Единственная надёжная точка перехвата банка имён.
    /// </summary>
    [HarmonyPatch(typeof(NameBank), "GetName",
        new[] { typeof(PawnNameSlot), typeof(Gender), typeof(bool) })]
    public static class NameBank_GetName_Reroute
    {
        static bool Prefix(NameBank __instance, PawnNameSlot slot, Gender gender,
                           bool checkIfAlreadyUsed, ref string __result)
        {
            if (!NameReroute.Enabled) return true;
            try
            {
                if (!NameReroute.IsHumanStandard(__instance)) return true;

                if (slot == PawnNameSlot.First)
                    NameReroute.RememberFirstGender(gender);

                var list = NameReroute.ListFor(__instance, slot, gender);
                if (list == null || list.Count == 0) return true;

                __result = NameReroute.Pick(list, checkIfAlreadyUsed);
                return false;
            }
            catch (Exception ex)
            {
                RuModLog.NameRerouteFailed(ex);
                return true;
            }
        }
    }

    /// <summary>
    /// Шлюз А — имена спонсоров без биографии.
    /// Единственный вызов метода: PawnBioAndNameGenerator.cs:418.
    /// Пропускаем результат ванили, чтобы уцелели «Предпочитаемые имена»,
    /// и гасим всё остальное — тогда ваниль сама уходит в GeneratePawnName_Shuffled,
    /// а тот идёт в банк, который мы отдаём русским.
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "TryGetRandomUnusedSolidName")]
    public static class SolidName_Gate
    {
        static void Postfix(ref NameTriple __result)
        {
            if (__result == null) return;
            if (!NameReroute.GatesActive) return;
            if (NameReroute.PreferredKept(__result)) return;
            __result = null;
        }
    }

    /// <summary>
    /// Шлюз Б — имена спонсоров вместе с биографией.
    /// Биографию оставляем: она переведена через DefInjected, английское в ней только имя.
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "TryGiveSolidBioTo")]
    public static class SolidBio_Gate
    {
        private static readonly MethodInfo mShuffled =
            AccessTools.Method(typeof(PawnBioAndNameGenerator), "GeneratePawnName_Shuffled");

        static void Postfix(Pawn pawn, string requiredLastName, bool __result)
        {
            if (!__result || pawn == null) return;
            if (!NameReroute.GatesActive) return;
            if (NameReroute.PreferredKept(pawn.Name)) return;
            if (mShuffled == null) { NameReroute.WarnShuffleMissing(); return; }
            try
            {
                var cat = pawn.RaceProps.nameCategory;
                if (cat != PawnNameCategory.HumanStandard) return;

                var ru = mShuffled.Invoke(null,
                    new object[] { cat, pawn.gender, requiredLastName, false }) as NameTriple;
                if (ru != null) pawn.Name = ru;
            }
            catch (Exception ex)
            {
                RuModLog.NameRerouteFailed(ex);
            }
        }
    }
}

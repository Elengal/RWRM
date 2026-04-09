using System;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using Verse;
using RuMod.Utils;

namespace RuMod.Patches
{
    /// <summary>
    /// Патч для RimHUD: корректное склонение «год/года/лет» в возрасте.
    /// Заменяет «N года» на «N год» / «N года» / «N лет» по правилам русского языка.
    /// Применяется только при русском языке и наличии RimHUD.
    /// </summary>
    public static class RimHUD_GenderRaceAndAgeValue_Patch
    {
        private static readonly Regex YearsPattern = new Regex(@"(\d+)\s+года\b", RegexOptions.Compiled);
        private static readonly Regex DaysPattern = new Regex(@"(\d+)\s+дней\b", RegexOptions.Compiled);

        public static void Patch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RimHUD.Interface.Hud.Models.Values.GenderRaceAndAgeValue");
                if (targetType == null)
                {
                    RuModLog.RimHudNotFound();
                    return;
                }

                var getValueMethod = AccessTools.Method(targetType, "GetValue");
                if (getValueMethod == null)
                {
                    RuModLog.RimHudGetValueNotFound();
                    return;
                }

                var postfix = new HarmonyMethod(typeof(RimHUD_GenderRaceAndAgeValue_Patch).GetMethod(nameof(Postfix)));
                harmony.Patch(getValueMethod, postfix: postfix);
                RuModLog.RimHudAgePatchApplied();
            }
            catch (Exception ex)
            {
                RuModLog.RimHudPatchFailed(ex);
            }
        }

        public static void Postfix(ref string __result)
        {
            try
            {
                if (string.IsNullOrEmpty(__result)) return;
                if (LanguageDatabase.activeLanguage == null ||
                    (LanguageDatabase.activeLanguage.folderName != "Russian (Русский)" &&
                     LanguageDatabase.activeLanguage.folderName != "Russian")) return;

                var match = YearsPattern.Match(__result);
                if (!match.Success) return;

                if (!int.TryParse(match.Groups[1].Value, out var years)) return;

                var form = GetYearsForm(years);
                if (form != "года")
                    __result = __result.Replace($"{years} года", $"{years} {form}");

                // Склонение дней: N дней → N дня при 2,3,4,22,23,24...
                __result = DaysPattern.Replace(__result, m =>
                    int.TryParse(m.Groups[1].Value, out var days)
                        ? $"{days} {GetDaysForm(days)}"
                        : m.Value);
            }
            catch (Exception ex)
            {
                RuModLog.RimHudAgePostfixError(ex);
            }
        }

        /// <summary>Склонение «год»: 1,21,31... → год; 2-4,22-24... → года; 5-20,25-30... → лет.</summary>
        private static string GetYearsForm(int n)
        {
            if (n < 0) return "лет";
            var mod10 = n % 10;
            var mod100 = n % 100;
            if (mod10 == 1 && mod100 != 11) return "год";
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return "года";
            return "лет";
        }

        /// <summary>Склонение «день»: 1,21,31... → день; 2-4,22-24... → дня; 5-20,25-30... → дней.</summary>
        private static string GetDaysForm(int n)
        {
            if (n < 0) return "дней";
            var mod10 = n % 10;
            var mod100 = n % 100;
            if (mod10 == 1 && mod100 != 11) return "день";
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return "дня";
            return "дней";
        }
    }
}

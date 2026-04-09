using HarmonyLib;
using RimWorld;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// Переводит блок «Dev mode info» в подсказке возраста: метки и стадию жизни (label вместо defName).
    /// </summary>
    [HarmonyPatch(typeof(Pawn_AgeTracker))]
    [HarmonyPatch("AgeTooltipString", MethodType.Getter)]
    public static class Pawn_AgeTracker_AgeTooltipString_Patch
    {
        public static void Postfix(Pawn_AgeTracker __instance, ref string __result)
        {
            if (string.IsNullOrEmpty(__result) || !Prefs.DevMode)
                return;

            __result = __result
                .Replace("Dev mode info:", RuMod.Utils.DevModeTranslator.Translate("Dev mode info:", "AgeTooltip"))
                .Replace("age reversal demand deadline: ", RuMod.Utils.DevModeTranslator.Translate("age reversal demand deadline: ", "AgeTooltip"))
                .Replace(" in future", RuMod.Utils.DevModeTranslator.Translate(" in future", "AgeTooltip"))
                .Replace(" past deadline", RuMod.Utils.DevModeTranslator.Translate(" past deadline", "AgeTooltip"))
                .Replace("\nlife stage: ", "\n" + RuMod.Utils.DevModeTranslator.Translate("life stage: ", "AgeTooltip"))
                .Replace("\nsterile: ", "\n" + RuMod.Utils.DevModeTranslator.Translate("sterile: ", "AgeTooltip"));

            LifeStageDef stage = __instance.CurLifeStage;
            if (stage != null)
            {
                string defName = stage.ToString();
                if (!string.IsNullOrEmpty(defName) && defName != stage.LabelCap)
                    __result = __result.Replace(defName, stage.LabelCap.Resolve());
            }
        }
    }
}

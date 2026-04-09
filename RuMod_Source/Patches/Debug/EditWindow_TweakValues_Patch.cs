using HarmonyLib;
using LudeonTK;

namespace RuMod.Patches
{
    /// <summary>
    /// Переводит заголовок окна TweakValues.
    /// </summary>
    [HarmonyPatch(typeof(EditWindow_TweakValues))]
    [HarmonyPatch(MethodType.Constructor)]
    public static class EditWindow_TweakValues_Patch
    {
        public static void Postfix(EditWindow_TweakValues __instance)
        {
            if (__instance == null)
                return;
            string translated = RuMod.Utils.DevModeTranslator.Translate("TweakValues");
            if (translated != "TweakValues")
                __instance.optionalTitle = translated;
        }
    }
}

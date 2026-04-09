using HarmonyLib;
using LudeonTK;
using RuMod.Utils;

namespace RuMod.Patches.Debug
{
    /// <summary>
    /// Переводит подпись и подсказку для кнопок в окнах EditWindow (в том числе Debug log),
    /// чтобы ширина кнопки рассчитывалась по русскому тексту.
    /// </summary>
    [HarmonyPatch(typeof(EditWindow), "DoRowButton")]
    public static class EditWindow_DoRowButton_Patch
    {
        public static void Prefix(ref string text, ref string tooltip)
        {
            if (!string.IsNullOrEmpty(text))
            {
                text = DevModeTranslator.Translate(text, "DevGUI");
            }

            if (!string.IsNullOrEmpty(tooltip))
            {
                tooltip = DevModeTranslator.Translate(tooltip, "DevGUI");
            }
        }
    }
}



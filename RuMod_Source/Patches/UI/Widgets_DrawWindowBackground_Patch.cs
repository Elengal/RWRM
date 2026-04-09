using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// Пропускаем стандартный непрозрачный фон для панели переводов в главном меню —
    /// там уже рисуется 75% прозрачный фон в MainMenuDrawer_DoTranslationInfoRect_Patch.
    /// </summary>
    [HarmonyPatch(typeof(Widgets))]
    [HarmonyPatch(nameof(Widgets.DrawWindowBackground))]
    [HarmonyPatch(new[] { typeof(Rect) })]
    public static class Widgets_DrawWindowBackground_Patch
    {
        public static bool Prefix(Rect rect)
        {
            var r = MainMenuDrawer_DoTranslationInfoRect_Patch.TranslationPanelRect;
            if (Math.Abs(rect.x - r.x) < 1f && Math.Abs(rect.y - r.y) < 1f &&
                Math.Abs(rect.width - r.width) < 1f && Math.Abs(rect.height - r.height) < 1f)
                return false;
            if (Math.Abs(rect.x - 8f) < 1f && Math.Abs(rect.height - 96f) < 1f &&
                Math.Abs(rect.y - (UI.screenHeight - 104f)) < 1f)
                return false;
            return true;
        }
    }
}

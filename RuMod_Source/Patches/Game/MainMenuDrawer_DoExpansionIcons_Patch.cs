using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// Панель DLC внизу слева в главном меню — рисуем фон с 75% прозрачностью.
    /// </summary>
    [HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.DoExpansionIcons))]
    public static class MainMenuDrawer_DoExpansionIcons_Patch
    {
        public static void Prefix()
        {
            var settings = RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>();
            var allExpansions = ModLister.AllExpansions;
            int num2 = allExpansions.Count((ExpansionDef e) => !e.isCore);
            int num3 = 32 + 64 * num2 + (num2 - 1) * 8 * 2;
            var rect = new Rect(8f, (float)(UI.screenHeight - 96 - 8), (float)num3, 96f);
            float alpha = settings?.DlcPanelAlpha ?? 0.25f;
            alpha = Mathf.Clamp(alpha, 0.1f, 0.5f);
            var factor = new Color(1f, 1f, 1f, alpha);
            Widgets.DrawWindowBackground(rect, factor);
        }
    }
}

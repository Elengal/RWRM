using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// Панель DLC внизу слева в главном меню: либо скрываем целиком (галочка в настройках),
    /// либо рисуем свой фон с настраиваемой непрозрачностью (0 — без подложки, 1 — как обычное окно).
    /// Стандартный непрозрачный фон пропускает Widgets_DrawWindowBackground_Patch.
    /// </summary>
    [HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.DoExpansionIcons))]
    public static class MainMenuDrawer_DoExpansionIcons_Patch
    {
        public static bool Prefix()
        {
            var settings = RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>();
            if (settings != null && !settings.ShowDlcPanel)
                return false;

            var allExpansions = ModLister.AllExpansions;
            int num2 = allExpansions.Count((ExpansionDef e) => !e.isCore);
            int num3 = 32 + 64 * num2 + (num2 - 1) * 8 * 2;
            var rect = new Rect(8f, (float)(UI.screenHeight - 96 - 8), (float)num3, 96f);
            float alpha = settings?.DlcPanelAlpha ?? 0.25f;
            alpha = Mathf.Clamp01(alpha);
            if (alpha > 0f)
            {
                var factor = new Color(1f, 1f, 1f, alpha);
                Widgets.DrawWindowBackground(rect, factor);
            }
            return true;
        }
    }
}

using HarmonyLib;
using LudeonTK;
using UnityEngine;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// Вешает тултипы на пункты Dev-меню: при наведении показывается полный текст label.
    /// Это решает проблему, когда длинные переводы не помещаются в ширину кнопки.
    /// </summary>
    [HarmonyPatch(typeof(DevGUI))]
    public static class Dialog_Debug_Tooltips_Patch
    {
        /// <summary>Флаг, управляющий работой тултипов Dev-меню (читается из настроек мода).</summary>
        public static bool IsEnabled = true;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(DevGUI.ButtonDebugPinnable))]
        public static void ButtonDebugPinnable_Postfix(Rect rect, string label, bool highlight, bool pinned)
        {
            AttachTooltip(rect, label);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(DevGUI.CheckboxPinnable))]
        public static void CheckboxPinnable_Postfix(Rect rect, string label, ref bool checkOn, bool highlight, bool pinned)
        {
            AttachTooltip(rect, label);
        }

        private static void AttachTooltip(Rect rect, string label)
        {
            if (!IsEnabled || string.IsNullOrEmpty(label))
            {
                return;
            }

            TooltipHandler.TipRegion(rect, label.Trim());
        }
    }
}


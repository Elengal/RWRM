using HarmonyLib;
using Verse;
using LudeonTK;
using System;
using RuMod.Utils;

namespace RuMod.Patches.Debug
{
    [HarmonyPatch(typeof(DebugActionNode), "LabelNow", MethodType.Getter)]
    public static class Patch_DebugActionNode_LabelNow
    {
        public static void Postfix(ref string __result)
        {
            if (!string.IsNullOrEmpty(__result))
            {
                __result = DevModeTranslator.Translate(__result, "Actions");
            }
        }
    }

    // ==========================================
    // Патчи для WidgetRow (верхняя панель и другие элементы)
    // ==========================================

    [HarmonyPatch(typeof(WidgetRow), "ButtonIcon")]
    public static class Patch_WidgetRow_ButtonIcon
    {
        public static void Prefix(ref string tooltip)
        {
            if (Prefs.DevMode && !string.IsNullOrEmpty(tooltip))
            {
                tooltip = DevModeTranslator.Translate(tooltip, "WidgetRow");
            }
        }
    }

    [HarmonyPatch(typeof(WidgetRow), "ButtonIconWithBG")]
    public static class Patch_WidgetRow_ButtonIconWithBG
    {
        public static void Prefix(ref string tooltip)
        {
            if (Prefs.DevMode && !string.IsNullOrEmpty(tooltip))
            {
                tooltip = DevModeTranslator.Translate(tooltip, "WidgetRow");
            }
        }
    }

    [HarmonyPatch(typeof(WidgetRow), "ToggleableIcon")]
    public static class Patch_WidgetRow_ToggleableIcon
    {
        public static void Prefix(ref string tooltip)
        {
            if (Prefs.DevMode && !string.IsNullOrEmpty(tooltip))
            {
                tooltip = DevModeTranslator.Translate(tooltip, "WidgetRow");
            }
        }
    }

    [HarmonyPatch(typeof(WidgetRow), "ButtonText")]
    public static class Patch_WidgetRow_ButtonText
    {
        public static void Prefix(ref string label, ref string tooltip)
        {
            if (Prefs.DevMode)
            {
                if (!string.IsNullOrEmpty(label)) label = DevModeTranslator.Translate(label, "WidgetRow");
                if (!string.IsNullOrEmpty(tooltip)) tooltip = DevModeTranslator.Translate(tooltip, "WidgetRow");
            }
        }
    }

    [HarmonyPatch(typeof(WidgetRow), "Label")]
    public static class Patch_WidgetRow_Label
    {
        public static void Prefix(ref string text, ref string tooltip)
        {
            if (Prefs.DevMode)
            {
                if (!string.IsNullOrEmpty(text)) text = DevModeTranslator.Translate(text, "WidgetRow");
                if (!string.IsNullOrEmpty(tooltip)) tooltip = DevModeTranslator.Translate(tooltip, "WidgetRow");
            }
        }
    }

    // ==========================================
    // Патчи для DevGUI (кнопки внутри дебаг-меню)
    // ==========================================

    [HarmonyPatch(typeof(DevGUI), "Label")]
    public static class Patch_DevGUI_Label
    {
        public static void Prefix(ref string label)
        {
            if (!string.IsNullOrEmpty(label))
            {
                label = DevModeTranslator.Translate(label, "DevGUI");
            }
        }
    }

    [HarmonyPatch(typeof(DevGUI), "ButtonText")]
    public static class Patch_DevGUI_ButtonText
    {
        public static void Prefix(ref string label)
        {
            if (!string.IsNullOrEmpty(label))
            {
                label = DevModeTranslator.Translate(label, "DevGUI");
            }
        }
    }

    [HarmonyPatch(typeof(DevGUI), "CheckboxLabeled")]
    public static class Patch_DevGUI_CheckboxLabeled
    {
        public static void Prefix(ref string label)
        {
            if (!string.IsNullOrEmpty(label))
            {
                label = DevModeTranslator.Translate(label, "DevGUI");
            }
        }
    }

    // ==========================================
    // Сохранение словаря
    // ==========================================
    [HarmonyPatch(typeof(Root), "Update")]
    public static class Patch_Root_Update
    {
        private static int _lastSaveTime = 0;

        public static void Postfix()
        {
            if (Prefs.DevMode && Environment.TickCount - _lastSaveTime > 5000)
            {
                DevModeTranslator.Save();
                _lastSaveTime = Environment.TickCount;
            }
        }
    }
}
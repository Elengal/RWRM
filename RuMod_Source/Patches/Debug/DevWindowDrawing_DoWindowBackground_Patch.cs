using HarmonyLib;
using UnityEngine;
using Verse;
using LudeonTK;
using RuMod.Utils;

/// <summary>
/// Делает фон окна Debug log полупрозрачным в соответствии с настройками RuMod.
/// </summary>
[HarmonyPatch(typeof(DevWindowDrawing), nameof(DevWindowDrawing.DoWindowBackground))]
public static class DevWindowDrawing_DoWindowBackground_Patch
{
    public static bool Prefix(Rect rect)
    {
        var window = Find.WindowStack?.currentlyDrawnWindow;
        if (window is LudeonTK.EditWindow_Log logWindow)
        {
            var settings = RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>();
            if (!DebugLogTweakManager.ShouldApplyTweaks(settings))
                return true;

            float alpha = settings?.DebugLogAlpha ?? 0.25f;
            alpha = Mathf.Clamp(alpha, 0.1f, 0.5f);

            var baseColor = DevGUI.WindowBGFillColor;
            var c = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            GUI.color = c;
            GUI.DrawTexture(rect, BaseContent.WhiteTex);
            GUI.color = Color.white;
            return false;
        }

        return true;
    }
}


using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// Подставляет перевод для кнопки «Debug: Finish now» и других жёстко заданных подписей Dev.
    /// </summary>
    [HarmonyPatch(typeof(Widgets))]
    public static class Widgets_ButtonText_Patch
    {
        /// <summary>Перегрузка с Color (Rect, string, bool, bool, Color, bool, TextAnchor?). Через неё проходят все вызовы ButtonText.</summary>
        public static MethodBase TargetMethod()
        {
            return typeof(Widgets)
                .GetMethods(AccessTools.allDeclared)
                .First(m => m.Name == "ButtonText" && m.GetParameters().Length == 7
                    && m.GetParameters()[4].ParameterType == typeof(Color));
        }

        public static void Prefix(ref string label)
        {
            if (string.IsNullOrEmpty(label))
                return;
            if (label == "Debug: Finish now" || Prefs.DevMode)
            {
                label = RuMod.Utils.DevModeTranslator.Translate(label, "Widgets");
                return;
            }
        }
    }
}

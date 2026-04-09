using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// Переводит подписи контекстного меню Dev Mode (FloatMenuOption):
    /// «Dev tool...», «Set primary», «Wear», «Add to inventory», «Add hediff» и т.д.
    /// </summary>
    [HarmonyPatch(typeof(FloatMenuOption))]
    public static class FloatMenuOption_Patch
    {
        /// <summary>Базовый конструктор (string label, Action action, ... 10 параметров). Остальные перегрузки вызывают его через : this().</summary>
        public static MethodBase TargetMethod()
        {
            return typeof(FloatMenuOption)
                .GetConstructors(AccessTools.allDeclared)
                .First(c =>
                {
                    var p = c.GetParameters();
                    return p.Length == 10 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(Action);
                });
        }

        public static void Postfix(FloatMenuOption __instance)
        {
            if (__instance == null || string.IsNullOrEmpty(__instance.Label))
                return;
            __instance.Label = RuMod.Utils.DevModeTranslator.Translate(__instance.Label, "FloatMenu");
        }
    }
}

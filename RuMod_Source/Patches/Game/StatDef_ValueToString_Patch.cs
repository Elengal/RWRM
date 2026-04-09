using System;
using HarmonyLib;
using RimWorld;
using Verse;
using RuMod.Utils;

namespace RuMod.Patches
{
    /// <summary>
    /// Защита от FormatException при форматировании StatDef (неверный formatString в переводах).
    /// При ошибке возвращается значение без кастомного формата.
    /// </summary>
    [HarmonyPatch(typeof(StatDef), "ValueToString", new Type[] { typeof(float), typeof(ToStringNumberSense), typeof(bool) })]
    public static class StatDef_ValueToString_Patch
    {
        static Exception Finalizer(Exception __exception, StatDef __instance, float val, ToStringNumberSense numberSense, bool finalized, ref string __result)
        {
            if (__exception == null)
                return null;

            if (__exception is FormatException)
            {
                __result = val.ToStringByStyle(__instance.toStringStyle, numberSense);
                RuModLog.StatValueFormatException(__instance, __exception);
                return null; // Подавить исключение
            }
            return __exception;
        }
    }
}

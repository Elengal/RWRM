using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using RuMod.Utils;

namespace RuMod.Patches
{
    /// <summary>
    /// Исправляет ошибку "Accessing TicksAbs but gameStartAbsTick is not set yet".
    /// TaleFactory.MakeRawTale вызывает Find.TickManager.TicksAbs до старта игры;
    /// используем GenTicks.TicksAbs, который безопасен в setup.
    /// </summary>
    [HarmonyPatch(typeof(TaleFactory), nameof(TaleFactory.MakeRawTale))]
    public static class TaleFactory_MakeRawTale_Patch
    {
        static bool Prefix(TaleDef def, object[] args, ref Tale __result)
        {
            try
            {
                Tale tale = (Tale)Activator.CreateInstance(def.taleClass, args);
                tale.def = def;
                tale.id = Find.UniqueIDsManager.GetNextTaleID();
                tale.date = GenTicks.TicksAbs;
                __result = tale;
            }
            catch (Exception ex)
            {
                RuModLog.TaleCreateFailed(def, args, ex);
                __result = null;
            }
            return false;
        }
    }
}

using HarmonyLib;
using RimWorld;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// После рождения ребёнка родители назначаются уже после GeneratePawn, поэтому
    /// при выдаче имени у младенца ещё нет связей. Здесь мы исправляем фамилию новорождённого
    /// по родителям (мать/отец уже добавлены в ApplyBirthOutcome к моменту выхода из метода).
    /// Патч применяется вручную из Main.cs после загрузки, чтобы не триггерить раннюю инициализацию PregnancyUtility.
    /// </summary>
    public static class PregnancyUtility_ApplyBirthOutcome_Patch
    {
        public static void Postfix(Thing __result)
        {
            if (RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>()?.NameBankPatchesEnabled != true)
                return;
            if (__result is Pawn baby)
                NameReplacerHelper.TryApplyFamilySurname(baby);
        }
    }
}

using HarmonyLib;
using RimWorld;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// Второй путь появления младенца, мимо ApplyBirthOutcome — Hediff_Pregnant.DoBirthSpawn.
    /// Через него рожают животные (Hediff_Pregnant.cs:132) и работает отладочное
    /// «Give birth» (DebugToolsPawns.cs:371), которым удобно проверять наследование.
    ///
    /// Беда та же, что и при настоящих родах: младенец генерируется раньше,
    /// чем ему прописывают родителя — Hediff_Pregnant.cs:223 против 232, — поэтому
    /// имя ему выдаётся, когда матери у него ещё нет.
    ///
    /// Метод ничего не возвращает и младенца наружу не отдаёт, так что заходим
    /// со стороны матери: правим её детей, которые только что появились на свет.
    /// Взрослых детей не трогаем — у замужней дочери фамилия своя по праву.
    /// </summary>
    [HarmonyPatch(typeof(Hediff_Pregnant), "DoBirthSpawn")]
    public static class Hediff_Pregnant_DoBirthSpawn_Patch
    {
        static void Postfix(Pawn mother)
        {
            if (RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>()?.RussianPawnNames != true)
                return;
            if (mother == null || mother.relations == null || !mother.RaceProps.Humanlike)
                return;

            foreach (Pawn child in mother.relations.Children)
            {
                if (child == null) continue;
                var stage = child.DevelopmentalStage;
                if (!stage.Newborn() && !stage.Baby()) continue;
                NameReplacerHelper.TryApplyFamilySurname(child);
            }
        }
    }
}

using HarmonyLib;
using RimWorld;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// После назначения связей (GeneratePawnRelations) подставляем пешке фамилию кровного родственника,
    /// если она была сгенерирована без учёта семьи (например странник-брат получил другую фамилию).
    /// Игра выставляет FixedLastName в request только внутри CreateRelation, т.е. после генерации имени.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawnRelations")]
    public static class PawnGenerator_GeneratePawnRelations_Patch
    {
        static void Postfix(Pawn pawn)
        {
            if (RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>()?.NameBankPatchesEnabled != true)
                return;
            NameReplacerHelper.TryApplyFamilySurname(pawn);
        }
    }
}

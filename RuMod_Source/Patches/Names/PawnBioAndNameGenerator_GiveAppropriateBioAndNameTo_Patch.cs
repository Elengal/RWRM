using HarmonyLib;
using RimWorld;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// После выдачи имени пешке подставляем фамилию кровного родственника, если он уже есть.
    /// Нужно для новорождённых: у них не вызывается GeneratePawnRelations, поэтому ребёнок
    /// получал случайную фамилию вместо фамилии родителя.
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "GiveAppropriateBioAndNameTo")]
    public static class PawnBioAndNameGenerator_GiveAppropriateBioAndNameTo_Patch
    {
        static void Postfix(Pawn pawn)
        {
            if (RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>()?.NameBankPatchesEnabled != true)
                return;
            NameReplacerHelper.TryApplyFamilySurname(pawn);
        }
    }
}

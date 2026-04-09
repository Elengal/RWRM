using HarmonyLib;
using RimWorld;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// Патч для TryGiveSolidBioTo - заменяет английские имена из предопределённых биографий
    /// Это покрывает случаи, когда используется "solid bio" (25% шанс)
    /// ВАЖНО: Когда используется solid bio, имя берётся напрямую из PawnBio.name,
    /// и GenerateFullPawnName НЕ вызывается, поэтому нужен отдельный патч!
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "TryGiveSolidBioTo")]
    public static class PawnBioAndNameGenerator_TryGiveSolidBioTo_Patch
    {
        /// <summary>
        /// Postfix - проверяем имя после назначения из solid bio и заменяем на русское
        /// </summary>
        static void Postfix(Pawn pawn, string requiredLastName, bool __result)
        {
            if (RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>()?.NameBankPatchesEnabled != true)
                return;
            // Если solid bio не был применён, пропускаем
            if (!__result || pawn == null || pawn.Name == null)
            {
                return;
            }

            NameTriple nameTriple = pawn.Name as NameTriple;

            // Если имя английское, заменяем на русское
            // ВАЖНО: Используем pawn.gender напрямую, так как это самый надёжный источник гендера
            if (nameTriple != null)
            {
                Gender gender = pawn.gender;
                PawnNameCategory nameCategory = pawn.RaceProps?.nameCategory ?? PawnNameCategory.HumanStandard;
                
                NameTriple russianName = NameReplacerHelper.TryReplaceWithRussianName(
                    nameTriple, gender, nameCategory, requiredLastName, false, "");
                
                if (russianName != null)
                {
                    pawn.Name = russianName;
                }
                // Родственник: имя уже русское (из solid bio), но фамилия могла прийти в мужской форме — исправляем для женщин.
                else if (gender == Gender.Female && !string.IsNullOrEmpty(nameTriple.Last) && NameReplacerHelper.LooksLikeMaleSurname(nameTriple.Last))
                {
                    pawn.Name = new NameTriple(nameTriple.First, nameTriple.Nick, NameReplacerHelper.ToFemaleSurname(nameTriple.Last));
                }
            }
        }
    }
}


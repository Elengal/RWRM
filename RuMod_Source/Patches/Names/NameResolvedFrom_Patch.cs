using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.Grammar;

namespace RuMod.Patches
{
    /// <summary>
    /// Патч для NameResolvedFrom - заменяет английские имена, сгенерированные через RulePackDef
    /// Это покрывает случаи: xenotype, pawnKindNameMaker, backstory nameMaker, culture nameMaker
    /// </summary>
    // Вспомогательный класс для сохранения гендера из контекста вызова
    public static class GenderContextHelper
    {
        // Сохраняем гендер из контекста вызова GenerateFullPawnName или GeneratePawnName
        public static Gender? lastKnownGender = null;
    }

    /// <summary>
    /// Prefix патч для GeneratePawnName - сохраняем гендер пешки
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "GeneratePawnName")]
    public static class GeneratePawnName_GenderContext_Patch
    {
        static void Prefix(Pawn pawn)
        {
            if (RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>()?.NameBankPatchesEnabled != true)
                return;
            if (pawn != null)
            {
                GenderContextHelper.lastKnownGender = pawn.gender;
            }
        }
    }

    /// <summary>
    /// Prefix патч для GenerateFullPawnName - сохраняем гендер
    /// ВАЖНО: Всегда обновляем гендер, если он не None, чтобы не использовать старый гендер от предыдущей пешки
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "GenerateFullPawnName")]
    public static class GenerateFullPawnName_GenderContext_Patch
    {
        static void Prefix(Gender gender)
        {
            if (RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>()?.NameBankPatchesEnabled != true)
                return;
            // ВАЖНО: Всегда обновляем гендер, если он не None
            if (gender != Gender.None)
            {
                GenderContextHelper.lastKnownGender = gender;
            }
        }
    }

    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "NameResolvedFrom", new Type[] { typeof(RulePackDef), typeof(bool), typeof(List<Rule>) })]
    public static class NameResolvedFrom_Patch
    {

        /// <summary>
        /// Проверяет результат генерации имени через RulePackDef и заменяет английские имена на русские
        /// </summary>
        static void Postfix(RulePackDef nameMaker, bool forceNoNick, List<Rule> extraRules, ref Name __result)
        {
            if (RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>()?.NameBankPatchesEnabled != true)
                return;

            NameTriple nameTriple = __result as NameTriple;

            // Проверяем, активен ли русский язык
            if (LanguageDatabase.activeLanguage == null || 
                (LanguageDatabase.activeLanguage.folderName != "Russian (Русский)" && 
                 LanguageDatabase.activeLanguage.folderName != "Russian"))
            {
                return;
            }

            // Если имя не NameTriple, пропускаем
            if (nameTriple == null)
            {
                return;
            }

            // Если имя английское, заменяем на русское из NameBank
            PawnNameCategory nameCategory = PawnNameCategory.HumanStandard;
            
            // Используем сохранённый гендер из GenerateFullPawnName или GeneratePawnName, если он есть
            // Если нет - пытаемся определить по английскому имени
            Gender genderToUse = GenderContextHelper.lastKnownGender ?? TryDetectGenderFromEnglishName(nameTriple);
            
            // Если гендер всё ещё неизвестен, пробуем все варианты
            Gender[] gendersToTry = genderToUse != Gender.None 
                ? new Gender[] { genderToUse }
                : new Gender[] { Gender.Male, Gender.Female, Gender.None };
            
            foreach (Gender tryGender in gendersToTry)
            {
                NameTriple russianName = NameReplacerHelper.TryReplaceWithRussianName(
                    nameTriple, tryGender, nameCategory, null, forceNoNick, "");
                
                if (russianName != null)
                {
                    __result = russianName;
                    // НЕ очищаем гендер здесь - он может понадобиться для других вызовов NameResolvedFrom в рамках одной пешки
                    // Очистка происходит в Postfix GenerateFullPawnName
                    return;
                }
            }
        }

        /// <summary>
        /// Пытается определить гендер по английскому имени (эвристика по окончаниям)
        /// </summary>
        private static Gender TryDetectGenderFromEnglishName(NameTriple name)
        {
            string first = name.First?.ToLowerInvariant() ?? "";
            
            if (string.IsNullOrEmpty(first))
            {
                return Gender.None;
            }
            
            // Типичные женские окончания в английских именах
            string[] femaleEndings = { "a", "ia", "ella", "ette", "ine", "y", "ie", "ey", "elle", "ette" };
            foreach (string ending in femaleEndings)
            {
                if (first.EndsWith(ending) && first.Length > 2)
                {
                    return Gender.Female;
                }
            }
            
            // Типичные мужские окончания
            string[] maleEndings = { "er", "or", "en", "on", "us", "is", "an", "in" };
            foreach (string ending in maleEndings)
            {
                if (first.EndsWith(ending) && first.Length > 2)
                {
                    return Gender.Male;
                }
            }
            
            return Gender.None;
        }

    }
}


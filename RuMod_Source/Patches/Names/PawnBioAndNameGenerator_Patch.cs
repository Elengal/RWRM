using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using static RuMod.Patches.NameResolvedFrom_Patch;

namespace RuMod.Patches
{
    /// <summary>
    /// Патч для принудительного использования русских имён при генерации пешек
    /// Перехватывает GenerateFullPawnName и проверяет результат
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "GenerateFullPawnName")]
    public static class PawnBioAndNameGenerator_GenerateFullPawnName_Patch
    {
        /// <summary>
        /// Проверяет результат генерации имени и заменяет английские имена на русские
        /// ВАЖНО: После завершения генерации очищаем сохранённый гендер
        /// </summary>
        static void Postfix(ThingDef genFor, RulePackDef pawnKindNameMaker, Pawn_StoryTracker story, 
            XenotypeDef xenotype, RulePackDef nameGenner, CultureDef primaryCulture, 
            bool creepjoiner, Gender gender, PawnNameCategory nameCategory, 
            string forcedLastName, bool forceNoNick, ref Name __result)
        {
            if (RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>()?.NameBankPatchesEnabled != true)
                return;

            NameTriple nameTriple = __result as NameTriple;
            NameSingle nameSingle = __result as NameSingle;

            // Проверяем, активен ли русский язык
            if (LanguageDatabase.activeLanguage == null || 
                (LanguageDatabase.activeLanguage.folderName != "Russian (Русский)" && 
                 LanguageDatabase.activeLanguage.folderName != "Russian"))
            {
                return;
            }

            // Если имя не NameTriple и не NameSingle, пропускаем
            if (nameTriple == null && nameSingle == null)
            {
                return;
            }

            // Если это NameSingle (создан через nameGenner), пытаемся заменить на NameTriple
            if (nameSingle != null)
            {
                string singleName = nameSingle.Name;
                bool singleHasRussianChars = NameReplacerHelper.ContainsRussianCharacters(singleName);
                
                if (!singleHasRussianChars)
                {
                    // Создаём временный NameTriple для использования общего метода замены
                    NameTriple tempNameTriple = new NameTriple(singleName, null, "");
                    NameTriple russianNameForSingle = NameReplacerHelper.TryReplaceWithRussianName(
                        tempNameTriple, gender, nameCategory, forcedLastName, forceNoNick, "");
                    
                    if (russianNameForSingle != null)
                    {
                        __result = russianNameForSingle;
                    }
                }
                return;
            }

            // Если имя английское, заменяем на русское из NameBank
            NameTriple russianNameForTriple = NameReplacerHelper.TryReplaceWithRussianName(
                nameTriple, gender, nameCategory, forcedLastName, forceNoNick, "");
            
            if (russianNameForTriple != null)
            {
                __result = russianNameForTriple;
            }
            // Родственник: имя уже русское, но фамилия пришла в мужской форме (forcedLastName от родителя) — женскую форму подставляем.
            else if (gender == Gender.Female && nameTriple != null && !string.IsNullOrEmpty(nameTriple.Last) && NameReplacerHelper.LooksLikeMaleSurname(nameTriple.Last))
            {
                __result = new NameTriple(nameTriple.First, nameTriple.Nick, NameReplacerHelper.ToFemaleSurname(nameTriple.Last));
            }
            
            // Очищаем сохранённый гендер после завершения генерации имени для этой пешки
            // Это гарантирует, что гендер от предыдущей пешки не будет использован для следующей
            GenderContextHelper.lastKnownGender = null;
        }

    }

}

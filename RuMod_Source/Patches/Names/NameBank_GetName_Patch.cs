using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// Патч для GetName - загружает русские имена на лету и при русском языке выбирает ТОЛЬКО русские имена.
    /// Без этого NameBank возвращал бы случайный выбор из смеси английских + русских.
    /// </summary>
    [HarmonyPatch(typeof(NameBank), "GetName")]
    public static class NameBank_GetName_Patch
    {
        private static MethodInfo namesForMethod;
        /// <summary>Пол из последнего GetName(First, …) — игра часто вызывает First, затем Last для одной пешки; при пачковой генерации lastKnownGender уже от следующей.</summary>
        private static Gender? lastFirstGenderInGetName = null;

        /// <summary>
        /// Prefix - загружает русские имена при необходимости и при русском языке выбирает только из русских.
        /// </summary>
        static bool Prefix(NameBank __instance, PawnNameSlot slot, Gender gender, bool checkIfAlreadyUsed, ref string __result)
        {
            if (RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>()?.NameBankPatchesEnabled != true)
                return true;

            if (LanguageDatabase.activeLanguage == null ||
                (LanguageDatabase.activeLanguage.folderName != "Russian (Русский)" &&
                 LanguageDatabase.activeLanguage.folderName != "Russian"))
            {
                return true;
            }

            try
            {
                if (namesForMethod == null)
                    namesForMethod = typeof(NameBank).GetMethod("NamesFor", BindingFlags.NonPublic | BindingFlags.Instance);

                if (namesForMethod == null)
                    return true;

                // Запоминаем пол при запросе имени (First), чтобы для фамилии (Last) брать его, а не lastKnownGender — при пачковой генерации тот уже от следующей пешки.
                if (slot == PawnNameSlot.First && (gender == Gender.Male || gender == Gender.Female))
                    lastFirstGenderInGetName = gender;

                // Фамилия — ВСЕГДА из файла по полу, не из банка: игра при загрузке могла положить в банк один список (Last) на оба пола.
                if (slot == PawnNameSlot.Last)
                {
                    Gender effectiveGender = gender == Gender.None
                        ? (lastFirstGenderInGetName ?? GenderContextHelper.lastKnownGender ?? Gender.Male)
                        : gender;
                    string lastFileName = GetFileNameForSlot(__instance, slot, effectiveGender);
                    if (!string.IsNullOrEmpty(lastFileName))
                    {
                        List<string> fromFile = NameLoaderHelper.LoadNamesFromMods(lastFileName);
                        if (fromFile != null && fromFile.Count > 0)
                        {
                            List<string> acceptableLast = fromFile.Where(NameReplacerHelper.IsAcceptableRussianName).ToList();
                            if (acceptableLast.Count > 0)
                            {
                                string pickedLast = null;
                                int lastAttempts = 0;
                                do
                                {
                                    pickedLast = acceptableLast.RandomElement();
                                    if (!checkIfAlreadyUsed || !NameUseChecker.NameWordIsUsed(pickedLast))
                                        break;
                                    lastAttempts++;
                                }
                                while (lastAttempts <= 50);
                                if (pickedLast != null)
                                {
                                    __result = pickedLast;
                                    if (RuMod.NameSourceLogger.IsEnabled)
                                    {
                                        string bankCat = TryGetNameCategoryForBank(__instance)?.ToString();
                                        RuMod.NameSourceLogger.Log(slot, gender, pickedLast, lastFileName, bankCat);
                                    }
                                    return false;
                                }
                            }
                        }
                    }
                }

                List<string> nameList = (List<string>)namesForMethod.Invoke(__instance, new object[] { slot, gender });
                if (nameList == null || nameList.Count == 0)
                    return true;

                // Допускаются только имена без английских букв (кириллица)
                bool hasAcceptableNames = nameList.Any(name => NameReplacerHelper.IsAcceptableRussianName(name));
                if (!hasAcceptableNames)
                {
                    string fileName = GetFileNameForSlot(__instance, slot, gender);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        List<string> names = NameLoaderHelper.LoadNamesFromMods(fileName);
                        if (names != null && names.Count > 0)
                        {
                            List<string> acceptable = names.Where(NameReplacerHelper.IsAcceptableRussianName).ToList();
                            if (acceptable.Count > 0)
                                __instance.AddNames(slot, gender, acceptable);
                            nameList = (List<string>)namesForMethod.Invoke(__instance, new object[] { slot, gender });
                            hasAcceptableNames = nameList != null && nameList.Any(name => NameReplacerHelper.IsAcceptableRussianName(name));
                        }
                    }
                }

                if (slot == PawnNameSlot.Nick && gender == Gender.None)
                {
                    foreach (Gender tryGender in new[] { Gender.Male, Gender.Female })
                    {
                        List<string> nickList = (List<string>)namesForMethod.Invoke(__instance, new object[] { slot, tryGender });
                        if (nickList != null && !nickList.Any(name => NameReplacerHelper.IsAcceptableRussianName(name)))
                        {
                            string nickFileName = GetFileNameForSlot(__instance, slot, tryGender);
                            if (!string.IsNullOrEmpty(nickFileName))
                            {
                                List<string> nicks = NameLoaderHelper.LoadNamesFromMods(nickFileName);
                                if (nicks != null && nicks.Count > 0)
                                {
                                    List<string> acceptableNicks = nicks.Where(NameReplacerHelper.IsAcceptableRussianName).ToList();
                                    if (acceptableNicks.Count > 0)
                                        __instance.AddNames(slot, tryGender, acceptableNicks);
                                }
                            }
                        }
                    }
                }

                if (!hasAcceptableNames)
                    return true;

                List<string> russianOnly = nameList.Where(name => NameReplacerHelper.IsAcceptableRussianName(name)).ToList();
                if (russianOnly.Count == 0)
                    return true;

                int attempts = 0;
                string picked;
                do
                {
                    picked = russianOnly.RandomElement();
                    if (!checkIfAlreadyUsed || !NameUseChecker.NameWordIsUsed(picked))
                    {
                        __result = picked;
                        if (RuMod.NameSourceLogger.IsEnabled)
                        {
                            string fileUsed = GetFileNameForSlot(__instance, slot, gender);
                            string bankCat = TryGetNameCategoryForBank(__instance)?.ToString();
                            RuMod.NameSourceLogger.Log(slot, gender, picked, fileUsed, bankCat);
                        }
                        return false;
                    }
                    attempts++;
                }
                while (attempts <= 50);

                __result = picked;
                if (RuMod.NameSourceLogger.IsEnabled)
                {
                    string fileUsed = GetFileNameForSlot(__instance, slot, gender);
                    string bankCat = TryGetNameCategoryForBank(__instance)?.ToString();
                    RuMod.NameSourceLogger.Log(slot, gender, picked, fileUsed, bankCat);
                }
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }


        /// <summary>
        /// Пытается определить категорию имён по банку (империя, аутлендеры, племя и т.д.).
        /// Нужно для выбора правильных файлов: Imperial_Last_Female для империи, Last_Female для остальных.
        /// </summary>
        private static PawnNameCategory? TryGetNameCategoryForBank(NameBank bank)
        {
            if (bank == null) return null;
            try
            {
                foreach (PawnNameCategory cat in Enum.GetValues(typeof(PawnNameCategory)))
                {
                    if (cat == PawnNameCategory.NoName) continue;
                    NameBank other = PawnNameDatabaseShuffled.BankOf(cat);
                    if (other == bank) return cat;
                }
            }
            catch (Exception) { /* игнорируем */ }
            return null;
        }

        /// <summary>
        /// Определяет имя файла на основе банка (категории), slot и gender.
        /// Учитывает империю (Imperial_*), аутлендеров/племя (First_*, Last_Male/Last_Female, Nick_*).
        /// </summary>
        private static string GetFileNameForSlot(NameBank bank, PawnNameSlot slot, Gender gender)
        {
            // Империя в 1.6 может быть отдельной категорией; проверяем по имени enum
            PawnNameCategory? cat = TryGetNameCategoryForBank(bank);
            bool isImperial = cat.HasValue && cat.Value.ToString().IndexOf("Imperial", StringComparison.OrdinalIgnoreCase) >= 0;
            string lastPrefix = isImperial ? "Imperial_Last" : "Last";
            string firstPrefix = isImperial ? "Imperial_First" : "First";
            // Nick для империи в Core нет отдельного списка — используется общий Nick или имя из First
            string nickPrefix = "Nick";

            if (slot == PawnNameSlot.First)
            {
                if (gender == Gender.Female) return firstPrefix + "_Female";
                if (gender == Gender.Male) return firstPrefix + "_Male";
                return firstPrefix + "_Male";
            }
            if (slot == PawnNameSlot.Nick)
            {
                if (gender == Gender.Female) return nickPrefix + "_Female";
                if (gender == Gender.Male) return nickPrefix + "_Male";
                return nickPrefix + "_Unisex";
            }
            if (slot == PawnNameSlot.Last)
            {
                if (gender == Gender.Female) return lastPrefix + "_Female";
                if (gender == Gender.Male) return lastPrefix + "_Male";
                // Игра часто запрашивает фамилию с Gender.None — берём пол из контекста генерации имени
                Gender contextGender = GenderContextHelper.lastKnownGender ?? Gender.Male;
                return contextGender == Gender.Female ? lastPrefix + "_Female" : lastPrefix + "_Male";
            }
            return null;
        }

    }
}

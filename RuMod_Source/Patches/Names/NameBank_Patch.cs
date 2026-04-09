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
    /// Патч для загрузки русских имён из папок мода вместо Unity Resources
    /// </summary>
    [HarmonyPatch(typeof(NameBank), "AddNamesFromFile")]
    public static class NameBank_AddNamesFromFile_Patch
    {
        /// <summary>
        /// Перехватывает загрузку имён и загружает их из папок мода
        /// Работает всегда, так как статический конструктор вызывается до установки activeLanguage
        /// </summary>
        static bool Prefix(NameBank __instance, PawnNameSlot slot, Gender gender, string fileName)
        {
            if (RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>()?.NameBankPatchesEnabled != true)
                return true;

            try
            {
                // Для фамилий (Last) игра передаёт один и тот же fileName "Last" для обоих полов —
                // подставляем файл по полу, иначе в слот Female попадут мужские формы (Давыдов вместо Давыдова).
                string fileToLoad = fileName;
                if (slot == PawnNameSlot.Last && (gender == Gender.Male || gender == Gender.Female))
                {
                    if (fileName == "Last")
                        fileToLoad = gender == Gender.Female ? "Last_Female" : "Last_Male";
                    else if (fileName != null && fileName.StartsWith("Imperial_Last", StringComparison.OrdinalIgnoreCase))
                        fileToLoad = gender == Gender.Female ? "Imperial_Last_Female" : "Imperial_Last_Male";
                }
                List<string> names = NameLoaderHelper.LoadNamesFromMods(fileToLoad);
                // В банк попадают только имена без английских букв (только кириллица)
                if (names != null && names.Count > 0)
                {
                    List<string> acceptable = names.Where(NameReplacerHelper.IsAcceptableRussianName).ToList();
                    if (acceptable.Count > 0)
                    {
                        __instance.AddNames(slot, gender, acceptable);
                        return false; // Пропускаем оригинальный метод
                    }
                }
                // Нет имён из мода или все отфильтрованы — не вызываем ванильную загрузку,
                // чтобы в банк не попали английские имена. Банк заполнится при первом GetName.
                return false;
            }
            catch (Exception)
            {
                // При ошибке не подмешиваем ванильные имена
                return false;
            }
        }
    }
}


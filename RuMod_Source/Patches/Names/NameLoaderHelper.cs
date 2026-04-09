using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// Вспомогательный класс для загрузки имён из файлов модов
    /// </summary>
    public static class NameLoaderHelper
    {
        private static Dictionary<string, List<string>> cachedNames = new Dictionary<string, List<string>>();

        /// <summary>
        /// Загружает имена из папок модов
        /// </summary>
        public static List<string> LoadNamesFromMods(string fileName)
        {
            List<string> allNames = new List<string>();
            HashSet<string> seenPaths = new HashSet<string>();

            // Проверяем кэш
            string cacheKey = fileName;
            if (cachedNames.ContainsKey(cacheKey))
            {
                return cachedNames[cacheKey];
            }

            // Проверяем, инициализирован ли LoadedModManager
            if (LoadedModManager.RunningMods == null)
            {
                return null;
            }

            foreach (ModContentPack mod in LoadedModManager.RunningMods)
            {
                string modRoot = mod.RootDir;
                if (string.IsNullOrEmpty(modRoot) || !Directory.Exists(modRoot))
                    continue;
                // Ищем в папках загрузки мода (loadFolder относительный: 1.6/Core, 1.6/Ideology и т.д.)
                foreach (string loadFolder in mod.foldersToLoadDescendingOrder)
                {
                    string loadPath = Path.Combine(modRoot, loadFolder);
                    if (!Directory.Exists(loadPath))
                        continue;
                    string folderName = Path.GetFileName(loadFolder.TrimEnd(Path.DirectorySeparatorChar, '/'));
                    // Вариант 1: Text/Names/ (старый способ), с поддержкой имени с суффиксом (FileName_Core.txt)
                    string textNamesDir = Path.Combine(loadPath, "Text", "Names");
                    foreach (string nameToTry in new[] { fileName + ".txt", fileName + "_" + folderName + ".txt" })
                    {
                        string textNamesPath = Path.Combine(textNamesDir, nameToTry);
                        if (File.Exists(textNamesPath))
                        {
                            string pathKey = textNamesPath.ToLowerInvariant();
                            if (!seenPaths.Contains(pathKey))
                            {
                                seenPaths.Add(pathKey);
                                try
                                {
                                    List<string> names = File.ReadAllLines(textNamesPath, Encoding.UTF8)
                                        .Where(line => !string.IsNullOrWhiteSpace(line))
                                        .Select(line => line.Trim())
                                        .ToList();
                                    if (names.Count > 0) allNames.AddRange(names);
                                }
                                catch (Exception) { /* игнорируем */ }
                            }
                            break;
                        }
                    }

                    // Вариант 2: Languages/.../Strings/Names/ — поддержка Last_Male.txt и Last_Male_Core.txt (суффикс по папке)
                    string[] languageFolders = { "Russian (Русский)", "Russian" };
                    string[] fileNamesToTry = new[] { fileName + ".txt", fileName + "_" + folderName + ".txt" };
                    foreach (string langFolder in languageFolders)
                    {
                        string namesDir = Path.Combine(loadPath, "Languages", langFolder, "Strings", "Names");
                        if (!Directory.Exists(namesDir)) continue;
                        foreach (string nameToTry in fileNamesToTry)
                        {
                            string langNamesPath = Path.Combine(namesDir, nameToTry);
                            if (File.Exists(langNamesPath))
                            {
                                string pathKey = langNamesPath.ToLowerInvariant();
                                if (!seenPaths.Contains(pathKey))
                                {
                                    seenPaths.Add(pathKey);
                                    try
                                    {
                                        List<string> names = File.ReadAllLines(langNamesPath, Encoding.UTF8)
                                            .Where(line => !string.IsNullOrWhiteSpace(line))
                                            .Select(line => line.Trim())
                                            .ToList();
                                        if (names.Count > 0)
                                            allNames.AddRange(names);
                                    }
                                    catch (Exception) { /* игнорируем */ }
                                }
                                break; // нашли файл для этой папки языка
                            }
                        }
                    }
                }
            }

            // Кэшируем результат
            if (allNames.Count > 0)
            {
                cachedNames[cacheKey] = allNames;
            }

            return allNames.Count > 0 ? allNames : null;
        }
    }
}


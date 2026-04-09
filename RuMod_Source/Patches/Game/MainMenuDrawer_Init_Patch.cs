using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// По настройке RuMod подменяем фон главного меню на текстуры из 1.6/Textures/UI/HeroArt/.
    /// В настройках показывается имя файла; при загрузке если файл не найден — подставляется Случайно.
    /// </summary>
    [HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.Init))]
    public static class MainMenuDrawer_Init_Patch
    {
        private const string ChoiceRandom = "Random";
        private const string ChoiceDefault = "Default";
        private const string DefaultPathRimWorldRu = "UI/HeroArt/RimWorldRu";
        private static List<Texture2D> _cachedTextures;
        private static List<string> _cachedContentPaths;

        private static void EnsureCache()
        {
            if (_cachedTextures != null)
                return;
            _cachedTextures = new List<Texture2D>();
            _cachedContentPaths = new List<string>();
            var mod = RuModClass.Instance?.Content;
            if (mod?.foldersToLoadDescendingOrder == null)
                return;
            var contentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pathList = new List<string>();
            string heroArtRelative = Path.Combine("Textures", "UI", "HeroArt");
            foreach (string loadFolder in mod.foldersToLoadDescendingOrder)
            {
                string dir = Path.Combine(loadFolder, heroArtRelative);
                if (!Directory.Exists(dir))
                    continue;
                foreach (string file in Directory.EnumerateFiles(dir, "*.png"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrEmpty(name))
                        continue;
                    string contentPath = "UI/HeroArt/" + name;
                    if (contentPaths.Add(contentPath))
                        pathList.Add(contentPath);
                }
            }
            pathList.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string path in pathList)
            {
                var tex = ContentFinder<Texture2D>.Get(path, false);
                if (tex != null)
                {
                    _cachedContentPaths.Add(path);
                    _cachedTextures.Add(tex);
                }
            }
        }

        private static List<Texture2D> GetRimWorldRuMenuTextures()
        {
            EnsureCache();
            return _cachedTextures;
        }

        /// <summary>Список content-путей фонов (для меню в настройках; порядок как у текстур).</summary>
        public static List<string> GetRimWorldRuMenuContentPaths()
        {
            EnsureCache();
            return _cachedContentPaths;
        }

        /// <summary>Имя для отображения по content-пути (имя файла без расширения).</summary>
        public static string GetDisplayNameFromContentPath(string contentPath)
        {
            if (string.IsNullOrEmpty(contentPath))
                return "";
            int last = contentPath.LastIndexOf('/');
            return last >= 0 ? contentPath.Substring(last + 1) : contentPath;
        }

        /// <summary>Подпись для режима «По умолчанию»: RimWorldRu, если есть; иначе Случайно; иначе Выкл.</summary>
        public static string GetDefaultChoiceDisplayName()
        {
            var list = GetRimWorldRuMenuTextures();
            var paths = _cachedContentPaths;
            int idx = paths != null ? paths.IndexOf(DefaultPathRimWorldRu) : -1;
            if (idx >= 0 && list != null && idx < list.Count)
                return GetDisplayNameFromContentPath(DefaultPathRimWorldRu);
            if (list != null && list.Count > 0)
                return "Случайно";
            return "Выкл";
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            ApplyRimWorldRuBackground();
        }

        /// <summary>
        /// Применяет фон главного меню по текущей настройке. Вызывается из Init и из настроек мода при смене выбора (сразу меняет картинку).
        /// </summary>
        public static void ApplyRimWorldRuBackground()
        {
            var bg = (UI_BackgroundMain)UIMenuBackgroundManager.background;
            if (bg == null)
                return;
            string choice = RuModClass.Instance?.GetSettings<RuModSettings>()?.MenuBackgroundRimWorldRu ?? "";
            if (string.IsNullOrEmpty(choice))
            {
                SetGameDefaultBackground(bg);
                return;
            }
            var list = GetRimWorldRuMenuTextures();
            var paths = _cachedContentPaths;
            if (list == null || list.Count == 0)
            {
                SetGameDefaultBackground(bg);
                return;
            }
            Texture2D tex = null;
            if (choice == ChoiceRandom)
            {
                tex = list.RandomElement();
            }
            else if (choice == ChoiceDefault)
            {
                int idxDef = paths != null ? paths.IndexOf(DefaultPathRimWorldRu) : -1;
                if (idxDef >= 0 && idxDef < list.Count)
                    tex = list[idxDef];
                else
                    tex = ContentFinder<Texture2D>.Get(DefaultPathRimWorldRu, false);
                if (tex == null && list.Count > 0)
                    tex = list.RandomElement();
            }
            else
            {
                int idx = paths != null ? paths.IndexOf(choice) : -1;
                if (idx >= 0 && idx < list.Count)
                    tex = list[idx];
                else
                    tex = ContentFinder<Texture2D>.Get(choice, false);
                if (tex == null)
                {
                    int idxDef = paths != null ? paths.IndexOf(DefaultPathRimWorldRu) : -1;
                    if (idxDef >= 0 && idxDef < list.Count)
                        tex = list[idxDef];
                    else
                        tex = ContentFinder<Texture2D>.Get(DefaultPathRimWorldRu, false);
                    if (tex == null && list.Count > 0)
                        tex = list.RandomElement();
                }
            }
            if (tex != null)
                bg.overrideBGImage = tex;
            else
                SetGameDefaultBackground(bg);
        }

        private static void SetGameDefaultBackground(UI_BackgroundMain bg)
        {
            if (Prefs.RandomBackgroundImage)
                bg.overrideBGImage = (from exp in ModLister.AllExpansions where exp.Status == ExpansionStatus.Active select exp).RandomElement<ExpansionDef>().BackgroundImage;
            else
                bg.overrideBGImage = Prefs.BackgroundImageExpansion.BackgroundImage;
        }
    }
}

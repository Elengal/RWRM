using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RuMod.Patches
{
    /// <summary>
    /// Левая панель «RimWorld переводчика»: фон 75% прозрачный; кредиты и надписи до/после ника всегда из перевода RimWorldRu (Elengal); кнопки Elengal и «Мод в Steam».
    /// </summary>
    [HarmonyPatch(typeof(MainMenuDrawer))]
    [HarmonyPatch("DoTranslationInfoRect")]
    [HarmonyPatch(new[] { typeof(Rect) })]
    public static class MainMenuDrawer_DoTranslationInfoRect_Patch
    {
        private const string SteamWorkshopUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=3615283148";
        private const string ElengalSteamProfileUrl = "https://steamcommunity.com/id/Elengal/";
        /// <summary>Прямоугольник панели переводов в главном меню (8, 100, 300, 400) — для прозрачности и пропуска стандартного фона.</summary>
        internal static readonly Rect TranslationPanelRect = new Rect(8f, 100f, 300f, 400f);
        /// <summary>Базовый сдвиг панели вправо (пикселей). Стандартная позиция = эта константа + сохранённый TranslationPanelDragOffsetX.</summary>
        private const float PanelOffsetX = 262f; // 275 + (-13) — сохранённая позиция как стандарт
        /// <summary>Базовый сдвиг панели вниз (пикселей).</summary>
        private const float PanelOffsetY = 235f; // 225 + 10 — сохранённая позиция как стандарт
        private const float DefaultTransparencyAlpha = 0.25f;
        private const float Padding = 8f;
        /// <summary>Зазор между кнопкой «Мод в Steam» и тремя кнопками ниже (пикселей).</summary>
        private const float GapAfterModBtn = 30f;

        private static LanguageInfo _savedLanguageInfo;
        private static bool _weDrawPanelOurselves;

        // Перетаскивание окна мышью
        private static float _dragOffsetX;
        private static float _dragOffsetY;
        private static bool _isDragging;
        private static Vector2 _dragStartPos;
        private static Vector2 _dragStartOffset;
        private const float DragHandleHeight = 24f;

        private const float BtnH = 25f;
        private const float BtnGap = 29f;      // между блоками (текст/кнопка)
        private const float BtnGapSmall = 4f;  // между тремя нижними кнопками
        private const float TextGap = 8f;

        public static bool Prefix(Rect outRect)
        {
            _weDrawPanelOurselves = false;
            if (Math.Abs(outRect.x - TranslationPanelRect.x) > 1f || Math.Abs(outRect.y - TranslationPanelRect.y) > 1f ||
                Math.Abs(outRect.width - TranslationPanelRect.width) > 1f || Math.Abs(outRect.height - TranslationPanelRect.height) > 1f)
                return true;

            var active = LanguageDatabase.activeLanguage;
            if (active != null && (active.folderName == "Russian (Русский)" || active.folderName == "Russian"))
            {
                _savedLanguageInfo = active.info;
                _weDrawPanelOurselves = true;
                // Фон рисуем в Postfix с динамической высотой
                return false;
            }

            return true;
        }

        public static void Postfix(Rect outRect)
        {
            if (_savedLanguageInfo != null && LanguageDatabase.activeLanguage != null)
            {
                LanguageDatabase.activeLanguage.info = _savedLanguageInfo;
                _savedLanguageInfo = null;
            }

            if (!_weDrawPanelOurselves)
                return;

            var settings = RuMod.RuModClass.Instance?.GetSettings<RuMod.RuModSettings>();
            if (settings != null && !_isDragging)
            {
                _dragOffsetX = settings.TranslationPanelDragOffsetX;
                _dragOffsetY = settings.TranslationPanelDragOffsetY;
            }

            float contentWidth = outRect.width - Padding * 2f - 10f;
            float hThanks = Text.CalcHeight("RuMod_TranslationThanks".Translate(), contentWidth);
            float hContribute = Text.CalcHeight("RuMod_TranslationHowToContribute".Translate(), contentWidth);
            float step = BtnH + BtnGapSmall;
            // Высота по содержимому: отступы + тексты + кнопки + зазор после «Мод в Steam» + 3 кнопки
            float totalHeight = Padding * 2f
                + hThanks + TextGap
                + BtnH + TextGap
                + hContribute + TextGap
                + BtnH + TextGap + GapAfterModBtn
                + step * 3f;

            Rect shifted = new Rect(outRect.x + PanelOffsetX + _dragOffsetX, outRect.y + PanelOffsetY + _dragOffsetY, outRect.width, totalHeight);
            Rect dragHandle = new Rect(shifted.x, shifted.y, shifted.width, DragHandleHeight);

            bool draggable = settings?.TranslationPanelDraggable ?? false;
            Event e = Event.current;
            if (e != null && draggable)
            {
                if (e.type == EventType.MouseDown && dragHandle.Contains(e.mousePosition))
                {
                    _isDragging = true;
                    _dragStartPos = e.mousePosition;
                    _dragStartOffset = new Vector2(_dragOffsetX, _dragOffsetY);
                    e.Use();
                }
                else if (_isDragging && e.type == EventType.MouseDrag)
                {
                    _dragOffsetX = _dragStartOffset.x + (e.mousePosition.x - _dragStartPos.x);
                    _dragOffsetY = _dragStartOffset.y + (e.mousePosition.y - _dragStartPos.y);
                    e.Use();
                }
                else if (e.type == EventType.MouseUp || e.type == EventType.MouseLeaveWindow)
                {
                    if (_isDragging && settings != null)
                    {
                        settings.TranslationPanelDragOffsetX = _dragOffsetX;
                        settings.TranslationPanelDragOffsetY = _dragOffsetY;
                        RuMod.RuModClass.Instance?.WriteSettings();
                    }
                    _isDragging = false;
                }
            }
            else if (e != null && (e.type == EventType.MouseUp || e.type == EventType.MouseLeaveWindow))
                _isDragging = false;

            float alpha = settings?.TranslationPanelAlpha ?? DefaultTransparencyAlpha;
            alpha = Mathf.Clamp(alpha, 0.1f, 0.5f);
            var factor = new Color(1f, 1f, 1f, alpha);
            Widgets.DrawWindowBackground(shifted, factor);

            Rect rect = shifted.ContractedBy(Padding);
            Widgets.BeginGroup(rect);
            rect = rect.AtZero();

            float y = 0f;

            // «Приветствую в RimWorld. Для вас старался:»
            Widgets.Label(new Rect(5f, y, contentWidth, hThanks), "RuMod_TranslationThanks".Translate());
            y += hThanks + TextGap;

            // Кнопка Elengal
            Rect rectElengal = new Rect(5f, y, contentWidth, BtnH);
            TooltipHandler.TipRegion(rectElengal, "RuMod_ElengalSteamTooltip".Translate());
            if (Widgets.ButtonText(rectElengal, "Elengal", true, true, true, null))
                Application.OpenURL(ElengalSteamProfileUrl);
            y += BtnH + TextGap;

            // «По любым вопросам обращайтесь через Steam...»
            Widgets.Label(new Rect(5f, y, contentWidth, hContribute), "RuMod_TranslationHowToContribute".Translate());
            y += hContribute + TextGap;

            // Кнопка «Мод в Steam»
            Rect rectMod = new Rect(5f, y, contentWidth, BtnH);
            if (Widgets.ButtonText(rectMod, "RuMod_ModSteamPage".Translate()))
                Application.OpenURL(SteamWorkshopUrl);
            y += BtnH + TextGap + GapAfterModBtn;

            // Три кнопки: между ними 4 px
            Rect rect4 = new Rect(5f, y, contentWidth, BtnH);
            y += step;
            Rect rect3 = new Rect(5f, y, contentWidth, BtnH);
            y += step;
            Rect rect2 = new Rect(5f, y, contentWidth, BtnH);
            if (Widgets.ButtonText(rect4, "LearnMore".Translate(), true, true, true, null))
                Application.OpenURL("https://rimworldgame.com/helptranslate");
            if (Widgets.ButtonText(rect3, "SaveTranslationReport".Translate(), true, true, true, null))
                LanguageReportGenerator.SaveTranslationReport();
            if (Widgets.ButtonText(rect2, "CleanupTranslationFiles".Translate(), true, true, true, null))
                TranslationFilesCleaner.CleanupTranslationFiles();

            Widgets.EndGroup();
        }
    }
}

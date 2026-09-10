using System;
using RimWorld;
using Verse;
using RuMod.Utils;

namespace RuMod.Patches
{
    /// <summary>
    /// Имядатели для ксенотипов, которым Людеон их не дал.
    ///
    /// ЗАЧЕМ. Свой генератор имён есть только у пятерых: свинолюди, мусорщики,
    /// импиды, иттакины и кротолюди. Гемофаги, ангелы, гусары, инжи и
    /// неандертальцы падают в общий банк и получают «Алексея Кузнецова» —
    /// от обычного человека их не отличить. Дефы имядателей лежат рядом,
    /// в Biotech/Defs/RulePackDefs/Namers_Xenotypes_RuMod.xml.
    ///
    /// ПОЧЕМУ КОДОМ, А НЕ ПАТЧЕМ XML. Это добавка, а не перевод, и она обязана
    /// подчиняться галочке русских имён: снял галку — ваниль вернулась целиком.
    /// XML-патч так не умеет, он прописывает поле навсегда.
    ///
    /// ПОЧЕМУ НЕ ПАТЧИМ GetNameMaker. Он маленький (XenotypeDef.cs:71-78),
    /// Mono такие встраивает в вызывающий код, и патч молча не сработает —
    /// на этом уже обожглись с NameBank.AddNames. Поля же присвоить можно
    /// напрямую: игра читает их каждый раз заново.
    ///
    /// ЧУЖИЕ МОДЫ НЕ ТРОГАЕМ. Если у ксенотипа имядатель уже есть — свой или
    /// от другого мода — проходим мимо. Возвращаем только то, что сами поставили.
    /// </summary>
    public static class XenotypeNamers
    {
        private struct Pair
        {
            public string xenotype;
            public string male;
            public string female;
            public float chance;

            public Pair(string x, string m, string f, float c)
            { xenotype = x; male = m; female = f; chance = c; }
        }

        private static readonly Pair[] table =
        {
            // Доля не всем одинаковая. Гемофаги, ангелы и неандертальцы — обособленные
            // сообщества, у них имя своё всегда. Гусар и инжей выращивают внутри обычных
            // человеческих обществ, поэтому четверть из них носит обычные имена — так же,
            // как ваниль поступает с мусорщиками (chanceToUseNameMaker 0.75).
            new Pair("Sanguophage",  "RuMod_NamerSanguophage_Male",  "RuMod_NamerSanguophage_Female", 1f),
            new Pair("Highmate",     "RuMod_NamerHighmate_Male",     "RuMod_NamerHighmate_Female",    1f),
            new Pair("Hussar",       "RuMod_NamerHussar_Male",       "RuMod_NamerHussar_Female",      0.75f),
            new Pair("Genie",        "RuMod_NamerGenie_Male",        "RuMod_NamerGenie_Female",       0.75f),
            new Pair("Neanderthal",  "RuMod_NamerNeanderthal_Male",  "RuMod_NamerNeanderthal_Female", 1f),
        };

        /// <summary>Что мы прописали сами — только это и снимаем обратно.</summary>
        private static readonly System.Collections.Generic.List<XenotypeDef> ours =
            new System.Collections.Generic.List<XenotypeDef>();

        private static bool applied;

        /// <summary>
        /// Привести состояние в соответствие с галочкой. Вызывать можно сколько угодно:
        /// повторный вызов при том же состоянии ничего не делает.
        /// </summary>
        public static void Apply()
        {
            bool want = NameReroute.Enabled && NameReroute.HasRussianNames();
            if (want == applied) return;

            try
            {
                if (want) Attach();
                else Detach();
                applied = want;
            }
            catch (Exception ex)
            {
                RuModLog.XenotypeNamersFailed(ex);
            }
        }

        private static void Attach()
        {
            int n = 0;
            for (int i = 0; i < table.Length; i++)
            {
                var x = DefDatabase<XenotypeDef>.GetNamedSilentFail(table[i].xenotype);
                // Нет Biotech — нет и ксенотипов, это не ошибка.
                if (x == null) continue;
                // Имядатель уже есть: свой или от чужого мода. Не наше дело.
                if (x.nameMaker != null || x.nameMakerFemale != null) continue;

                var m = DefDatabase<RulePackDef>.GetNamedSilentFail(table[i].male);
                var f = DefDatabase<RulePackDef>.GetNamedSilentFail(table[i].female);
                if (m == null || f == null) continue;

                x.nameMaker = m;
                x.nameMakerFemale = f;
                // Поле по умолчанию 0, и без него имядатель не выстрелит ни разу:
                // ваниль проверяет Rand.Value < chanceToUseNameMaker.
                x.chanceToUseNameMaker = table[i].chance;
                ours.Add(x);
                n++;
            }
            RuModLog.XenotypeNamersAttached(n);
        }

        private static void Detach()
        {
            for (int i = 0; i < ours.Count; i++)
            {
                var x = ours[i];
                if (x == null) continue;
                x.nameMaker = null;
                x.nameMakerFemale = null;
                x.chanceToUseNameMaker = 0f;
            }
            int n = ours.Count;
            ours.Clear();
            RuModLog.XenotypeNamersDetached(n);
        }
    }
}

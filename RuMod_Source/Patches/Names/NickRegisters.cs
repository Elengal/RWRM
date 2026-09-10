using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using RuMod.Utils;

namespace RuMod.Patches
{
    /// <summary>
    /// Кличка по нраву: пирату жёсткую, племени звериную, поселенцу насмешливую.
    ///
    /// ЗАЧЕМ. Раньше кличка бралась из одного общего списка, и колонистка-повар
    /// могла оказаться Гюрзой, а налётчица — Няшкой. Слова разложены по шести
    /// регистрам (файлы Nick_&lt;пол&gt;_&lt;регистр&gt;_Core.txt), здесь мы решаем,
    /// из какого брать.
    ///
    /// ОТКУДА КОНТЕКСТ. NameBank.GetName не знает, кого называет: у него на руках
    /// только слот и пол. Зато PawnBioAndNameGenerator.GiveAppropriateBioAndNameTo
    /// знает и пешку, и фракцию — и вызывается прямо перед выдачей имени.
    /// Поэтому запоминаем пешку там и забываем сразу после.
    ///
    /// ИРОНИЯ НЕ ВЫЧИЩЕНА НАРОЧНО. У пирата ласковый регистр весит 5, а не 0:
    /// именно эти пять процентов дают головореза по кличке Добряк. Кличка чаще
    /// даётся по контрасту, чем по сходству, и убирать это было бы обеднением.
    ///
    /// ЗАПАСНОЙ ПУТЬ. Не удалось определить фракцию, нет файла регистра, пешки
    /// вообще нет — берём общий плоский список, как раньше. Никаких исключений
    /// наружу: имя пешке нужно в любом случае.
    /// </summary>
    public static class NickRegisters
    {
        public const string Hard = "Hard";
        public const string Beast = "Beast";
        public const string Craft = "Craft";
        public const string Mock = "Mock";
        public const string Tender = "Tender";
        public const string Hidden = "Hidden";

        private static readonly string[] all = { Hard, Beast, Craft, Mock, Tender, Hidden };

        /// <summary>Веса по нраву. Порядок тот же, что в all.</summary>
        private sealed class Weights
        {
            public readonly int[] w;
            public readonly int total;

            public Weights(int hard, int beast, int craft, int mock, int tender, int hidden)
            {
                w = new[] { hard, beast, craft, mock, tender, hidden };
                total = hard + beast + craft + mock + tender + hidden;
            }
        }

        //                                    жёст звер пром насм ласк скрыт
        private static readonly Weights Raiders   = new Weights(55, 15,  0, 10,  5, 15);
        private static readonly Weights Tribal    = new Weights(25, 45, 15,  5,  5,  5);
        private static readonly Weights Outlander = new Weights(10, 20, 30, 25, 10,  5);
        private static readonly Weights Imperial  = new Weights(30, 25, 15, 10, 15,  5);
        private static readonly Weights Peaceful  = new Weights( 5, 20, 30, 20, 20,  5);
        private static readonly Weights Even      = new Weights(20, 20, 15, 15, 20, 10);

        /// <summary>Кого сейчас называют. Живёт ровно на время выдачи имени.</summary>
        [ThreadStatic] private static Pawn current;

        public static void Remember(Pawn pawn) { current = pawn; }
        public static void Forget() { current = null; }

        /// <summary>Регистр для клички. null — брать общий список, как раньше.</summary>
        public static string Choose()
        {
            var pawn = current;
            if (pawn == null) return null;
            try
            {
                var wts = WeightsFor(pawn);
                int roll = Rand.Range(0, wts.total);
                for (int i = 0; i < all.Length; i++)
                {
                    roll -= wts.w[i];
                    if (roll < 0) return all[i];
                }
                return all[0];
            }
            catch (Exception ex)
            {
                RuModLog.NickRegisterFailed(ex);
                return null;
            }
        }

        private static Weights WeightsFor(Pawn pawn)
        {
            string faction = FactionName(pawn);
            if (faction == null) return Even;

            // Разбойный люд: пираты, каннибалы, культисты, наёмники-утилизаторы.
            if (faction.StartsWith("Pirate", StringComparison.Ordinal)
                || faction == "CannibalPirate" || faction == "HoraxCult"
                || faction == "Salvagers" || faction == "AncientsHostile")
                return Raiders;

            // Гемофаги живут веками и держатся особняком — им скрытное и жёсткое.
            if (faction == "Sanguophages") return Raiders;

            if (faction.StartsWith("Tribe", StringComparison.Ordinal)
                || faction == "NudistTribe" || faction == "PlayerTribe")
                return Tribal;

            if (faction == "Empire") return Imperial;

            if (faction.StartsWith("Outlander", StringComparison.Ordinal))
                return Outlander;

            // Мирные приходящие: торговцы, паломники, попрошайки, экспедиции.
            if (faction == "TradersGuild" || faction == "Pilgrims" || faction == "Beggars"
                || faction == "ResearchExpedition" || faction == "GravshipCrew"
                || faction == "Ancients")
                return Peaceful;

            return Even;
        }

        private static string FactionName(Pawn pawn)
        {
            var f = pawn.Faction;
            if (f != null && f.def != null) return f.def.defName;
            // У пешки фракции может ещё не быть: на момент выдачи имени её иногда
            // только собираются присвоить. Тогда судим по виду пешки.
            var kind = pawn.kindDef;
            if (kind != null && kind.defaultFactionDef != null)
                return kind.defaultFactionDef.defName;
            return null;
        }
    }

    /// <summary>
    /// Запоминаем пешку на время выдачи ей имени.
    /// Метод большой, Mono его не встраивает — патч надёжен.
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "GiveAppropriateBioAndNameTo")]
    public static class GiveAppropriateBioAndNameTo_Context
    {
        static void Prefix(Pawn pawn)
        {
            NickRegisters.Remember(pawn);
        }

        // Finalizer, а не Postfix: если ваниль бросит исключение, забыть всё равно надо.
        static void Finalizer()
        {
            NickRegisters.Forget();
        }
    }
}

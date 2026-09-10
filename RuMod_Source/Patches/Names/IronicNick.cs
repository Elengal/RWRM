using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using RuMod.Utils;

namespace RuMod.Patches
{
    /// <summary>
    /// Кличка по контрасту: толстяка зовут Пёрышком, головореза Добряком.
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫМ ЗАХОДОМ. В момент выдачи имени признаков ещё нет.
    /// Порядок в PawnGenerator: имя на строке 770, черты на 786, телосложение
    /// на 787, навыки на 789, увечья на 893. Поэтому смотрим на готовую пешку.
    ///
    /// ТРОГАЕМ ТОЛЬКО КЛИЧКУ. Фамилию менять поздно: на 779-й игра уже запомнила
    /// из неё story.birthLastName, и подмена развела бы девичью фамилию
    /// с настоящей. Кличка ни на что не завязана, она только для показа.
    ///
    /// РЕДКО. Ваниль даёт отдельную кличку лишь в 15% случаев, в остальных ставит
    /// туда имя или фамилию. Если раздать прозвища всем, игра изменится на вид.
    /// Поэтому берём только пешек, у которых кличка и так своя, и даже им
    /// подменяем не всегда: ирония тем и хороша, что редка.
    ///
    /// КОНТРАСТ ДОЛЖЕН БЫТЬ ВИДЕН. Признаки взяты только те, что игрок видит на
    /// пешке сам: телосложение, черта, увечье, провальный навык. Иначе шутка
    /// читается не как шутка, а как промах генератора.
    /// </summary>
    public static class IronicNick
    {
        /// <summary>Доля пешек со своей кличкой, которым её подменят на ироничную.</summary>
        internal const float Chance = 0.35f;

        private const int LowSkill = 2;

        private static Dictionary<string, List<string>> maleBySignal;
        private static Dictionary<string, List<string>> femaleBySignal;
        private static LoadedLanguage builtFor;

        public static void ResetCache()
        {
            maleBySignal = null;
            femaleBySignal = null;
            builtFor = null;
        }

        private static Dictionary<string, List<string>> Load(LoadedLanguage lang, string file)
        {
            var map = new Dictionary<string, List<string>>();
            var lines = NameReroute.FromLang(lang, file);
            if (lines == null) return map;
            foreach (string raw in lines)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                int sp = raw.IndexOf(' ');
                if (sp <= 0 || sp >= raw.Length - 1) continue;
                string key = raw.Substring(0, sp);
                string word = raw.Substring(sp + 1).Trim();
                if (word.Length == 0) continue;
                List<string> bucket;
                if (!map.TryGetValue(key, out bucket)) map[key] = bucket = new List<string>();
                bucket.Add(word);
            }
            return map;
        }

        private static void EnsureBuilt()
        {
            var lang = LanguageDatabase.activeLanguage;
            if (maleBySignal != null && ReferenceEquals(lang, builtFor)) return;
            builtFor = lang;
            maleBySignal = Load(lang, "Names/Nick_Male_Irony");
            femaleBySignal = Load(lang, "Names/Nick_Female_Irony");
        }

        /// <summary>Признаки, которые видно на пешке. Пусто — иронии не будет.</summary>
        private static List<string> SignalsOf(Pawn pawn)
        {
            var found = new List<string>();

            var body = pawn.story != null ? pawn.story.bodyType : null;
            if (body != null)
            {
                if (body.defName == "Fat") found.Add("Fat");
                else if (body.defName == "Hulk") found.Add("Hulk");
                else if (body.defName == "Thin") found.Add("Thin");
            }

            var traits = pawn.story != null ? pawn.story.traits : null;
            if (traits != null)
            {
                if (HasTrait(traits, "Kind")) found.Add("Kind");
                if (HasTrait(traits, "Bloodlust") || HasTrait(traits, "Psychopath")) found.Add("Cruel");
                if (HasTrait(traits, "Wimp")) found.Add("Wimp");
                if (HasTrait(traits, "Tough")) found.Add("Tough");
                if (HasTrait(traits, "Abrasive")) found.Add("Abrasive");
                if (HasTrait(traits, "Gourmand")) found.Add("Gourmand");
                if (HasTrait(traits, "Nudist")) found.Add("Nudist");
                if (HasTrait(traits, "Pyromaniac")) found.Add("Pyromaniac");

                int beauty = DegreeOf(traits, "Beauty");
                if (beauty < 0) found.Add("Ugly");
                else if (beauty > 0) found.Add("Beautiful");

                // У усердия отрицательные степени — это лень.
                if (DegreeOf(traits, "Industriousness") < 0) found.Add("Lazy");
                if (DegreeOf(traits, "SpeedOffset") < 0) found.Add("Slow");
            }

            var skills = pawn.skills;
            if (skills != null)
            {
                AddIfWeak(skills, SkillDefOf.Shooting, "BadShooting", found);
                AddIfWeak(skills, SkillDefOf.Intellectual, "BadIntellectual", found);
                AddIfWeak(skills, SkillDefOf.Cooking, "BadCooking", found);
                AddIfWeak(skills, SkillDefOf.Melee, "BadMelee", found);
                AddIfWeak(skills, SkillDefOf.Social, "BadSocial", found);
                AddIfWeak(skills, SkillDefOf.Medicine, "BadMedicine", found);
                AddIfWeak(skills, SkillDefOf.Construction, "BadConstruction", found);
                AddIfWeak(skills, SkillDefOf.Plants, "BadPlants", found);
            }

            if (MissingEye(pawn)) found.Add("NoEye");

            return found;
        }

        private static bool HasTrait(TraitSet traits, string defName)
        {
            var def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            return def != null && traits.HasTrait(def);
        }

        private static int DegreeOf(TraitSet traits, string defName)
        {
            var def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            if (def == null) return 0;
            var t = traits.GetTrait(def);
            return t == null ? 0 : t.Degree;
        }

        private static void AddIfWeak(Pawn_SkillTracker skills, SkillDef skill, string signal, List<string> into)
        {
            if (skill == null) return;
            var rec = skills.GetSkill(skill);
            if (rec != null && !rec.TotallyDisabled && rec.Level <= LowSkill) into.Add(signal);
        }

        private static bool MissingEye(Pawn pawn)
        {
            var health = pawn.health;
            if (health == null || health.hediffSet == null) return false;
            foreach (var h in health.hediffSet.hediffs)
            {
                var missing = h as Hediff_MissingPart;
                if (missing == null || missing.Part == null || missing.Part.def == null) continue;
                if (missing.Part.def.defName == "Eye") return true;
            }
            return false;
        }

        /// <summary>Подобрать ироничную кличку. null — не нашлось, оставить как есть.</summary>
        public static string TryPick(Pawn pawn)
        {
            EnsureBuilt();
            var map = pawn.gender == Gender.Female ? femaleBySignal : maleBySignal;
            if (map == null || map.Count == 0) return null;

            var signals = SignalsOf(pawn);
            if (signals.Count == 0) return null;

            // Перебираем признаки вразнобой: иначе телосложение всегда било бы навык.
            // Список укорачивается на каждом шаге, поэтому while, а не for со счётчиком:
            // тот вышел бы на шаг раньше и последний признак остался бы непробованным.
            while (signals.Count > 0)
            {
                int i = Rand.Range(0, signals.Count);
                List<string> words;
                if (map.TryGetValue(signals[i], out words) && words.Count > 0)
                    return words[Rand.Range(0, words.Count)];
                signals.RemoveAt(i);
            }
            return null;
        }
    }

    /// <summary>
    /// Пешка сгенерирована целиком — теперь видно и телосложение, и черты,
    /// и увечья. Самое время посмотреть, не просится ли кличка по контрасту.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn",
        new[] { typeof(PawnGenerationRequest) })]
    public static class PawnGenerator_IronicNick
    {
        static void Postfix(Pawn __result)
        {
            if (__result == null) return;
            if (!NameReroute.Enabled) return;
            try
            {
                if (__result.RaceProps == null || !__result.RaceProps.Humanlike) return;

                var name = __result.Name as NameTriple;
                if (name == null) return;

                // Своей клички нет — ваниль поставила туда имя или фамилию.
                // Не навязываем прозвище тем, кому его не полагалось.
                if (string.IsNullOrEmpty(name.Nick)) return;
                if (name.Nick == name.First || name.Nick == name.Last) return;

                if (Rand.Value >= IronicNick.Chance) return;

                string nick = IronicNick.TryPick(__result);
                if (string.IsNullOrEmpty(nick) || nick == name.Nick) return;

                __result.Name = new NameTriple(name.First, nick, name.Last);
            }
            catch (Exception ex)
            {
                RuModLog.IronicNickFailed(ex);
            }
        }
    }
}

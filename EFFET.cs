#nullable disable
// LimbusInjector (ver.2.3.0)
// F8 to toggle panel
// Tabs: Buff | Ability | Inspect
// Multi-unit selection via checkboxes
// Category filter: Sys / Canto I~X / Mirror Dungeon / E.G.O / Shin / Boss
// All buff names in English
//
// [2.1.0] Persistent buff system:
//   Stack [effect value] / Turn [effect duration] / Persist [rounds to re-apply]
//   Persist=0 → apply once
//   Persist=N → apply immediately, then re-apply every round start for N rounds
//
// [2.2.0] Inspect tab + Persist reset button:
//   - Inspect tab: view and remove active persistent buff schedules
//   - ↺ button next to Persist input: instantly resets Persist to 0
//
// [2.3.0] Canto X effects + C10 category:
//   - Added C10 filter category
//   - Added 10 new Canto X buff IDs (DriftingDimensionYisang, FourDimensionYisang,
//     RougeIshmaelCocoon, RougeIshmaelEclose, NoirSuitAlly, RealisationAlly,
//     ChargeRougeAlly, ChargeNoirAlly, DuelSousTemoinsEast, DianxueHongLu)

using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace LimbusInjector
{
    [BepInPlugin("com.mod.limbusinjector", "LimbusInjector", "2.3.0")]
    public class LimbusInjectorPlugin : BasePlugin
    {
        internal static new ManualLogSource Log;
        public override void Load()
        {
            Log = base.Log;
            Log.LogInfo("LimbusInjector v2.3.0 loaded | F8 to toggle");
            AddComponent<InjectorUI>();
            // StageController.StartRound 후킹으로 라운드 변화 감지
            new Harmony("com.mod.limbusinjector").PatchAll(typeof(LimbusInjectorPlugin).Assembly);
        }
    }

    // ── Harmony Patch: StageController.StartRound ────────────────────────
    // 로그 근거: "Postfix_StageController_StartRound | Round: N"
    [HarmonyPatch(typeof(StageController), "StartRound")]
    static class Patch_StartRound
    {
        static void Postfix()
        {
            // InjectorUI 인스턴스에 라운드 시작을 통보
            InjectorUI._instance?.OnRoundStarted();
        }
    }

    public class InjectorUI : MonoBehaviour
    {
        // ── State ────────────────────────────────────────────────────────
        internal static InjectorUI _instance;   // Harmony 패치에서 접근
        private bool _showPanel = false;
        private int  _activeTab = 0; // 0=Buff 1=Ability

        private string _status = "";

        // Multi-unit selection
        private UNIT_FACTION _faction = UNIT_FACTION.PLAYER;
        private Dictionary<int, object> _selectedUnits = new Dictionary<int, object>();
        private Dictionary<int, string> _selectedNames  = new Dictionary<int, string>();

        // Buff tab
        private string _buffSearch   = "";
        private int    _buffPage     = 0;
        private int    _buffTypeIdx  = 0;
        private int    _buffCatIdx   = 0;
        private string _stackInput   = "1";
        private string _turnInput    = "3";
        private string _persistInput = "0";  // [NEW] 지속할 턴

        private readonly string[] _typeLabels = { "All", "Positive", "Negative", "Sin", "Other" };
        private readonly string[] _catLabels  = { "All","Sys","C1","C2","C3","C4","C5","C6","C7","C8","C9","C10","MD","EGO","Shin","Boss" };
        private readonly string[] _catKeys    = { "","Sys","C1","C2","C3","C4","C5","C6","C7","C8","C9","C10","MD","EGO","Shin","Boss" };

        // ── [NEW] Persistent Buff System ─────────────────────────────────
        private struct PersistEntry
        {
            public string       buffId;
            public bool         isBuff;       // true=Buff / false=Ability
            public int          stack;
            public int          turn;
            public int          remainTurns;  // 남은 재부여 횟수
            public UNIT_FACTION faction;
            public HashSet<int> instanceIDs;  // 대상 유닛 InstanceID 집합
        }
        private List<PersistEntry> _persistList   = new List<PersistEntry>();
        private Vector2            _persistScroll = Vector2.zero;

        // Inspect tab
        private Vector2 _inspectScroll = Vector2.zero;

        // (id, English name, buffType, buffClass, category)
        private readonly (string id, string en, string buffType, string buffClass, string cat)[] _allBuffKeywords = {
            ("Enhancement", "Power Up", "Positive", "None", "Sys"),
            ("Agility", "Haste", "Positive", "None", "Sys"),
            ("Endurance", "Endurance", "None", "None", "Sys"),
            ("Reduction", "Power Down", "Negative", "None", "Sys"),
            ("Binding", "Bind", "Negative", "None", "Sys"),
            ("Vulnerable", "Fragile", "Negative", "None", "Sys"),
            ("Charge", "Charge", "None", "SinBuff", "Sys"),
            ("EgoErode", "E.G.O Corrosion", "None", "None", "EGO"),
            ("Overwhelm_LowMorale", "Overwhelm: Low Morale", "None", "None", "Sys"),
            ("Overwhelm_Panic", "Overwhelm: Panic", "None", "None", "Sys"),
            ("Daunted_LowMorale", "Daunted: Low Morale", "None", "None", "Sys"),
            ("Daunted_Panic", "Daunted: Panic", "None", "None", "Sys"),
            ("Anger_LowMorale", "Anger: Low Morale", "None", "None", "Sys"),
            ("Anger_Panic", "Anger: Panic", "None", "None", "Sys"),
            ("Run_LowMorale", "Flee: Low Morale", "None", "None", "Sys"),
            ("Run_Panic", "Flee: Panic", "None", "None", "Sys"),
            ("ResultEnhancement", "Clash Power Up", "Positive", "None", "Sys"),
            ("Protection", "Protection", "Positive", "None", "Sys"),
            ("AttackDmgUp", "Damage Up", "Positive", "None", "Sys"),
            ("DefenseUp", "Defense Level Up", "Positive", "VolatileBuff", "Sys"),
            ("PlusCoinValueUp", "Plus Coin Boost", "Positive", "None", "Sys"),
            ("MinusCoinValueUp", "Minus Coin Boost", "Positive", "None", "Sys"),
            ("Disarming", "Guard Power Down", "Negative", "None", "Sys"),
            ("ResultReduction", "Clash Power Down", "Negative", "None", "Sys"),
            ("AttackDmgDown", "Damage Down", "Negative", "None", "Sys"),
            ("DefenseDown", "Defense Level Down", "Negative", "None", "Sys"),
            ("Paralysis", "Paralysis", "Negative", "None", "Sys"),
            ("ElectricShock", "Electric Shock", "Negative", "None", "Sys"),
            ("PlusCoinValueDown", "Plus Coin Debuff", "Negative", "None", "Sys"),
            ("MinusCoinValueDown", "Minus Coin Debuff", "Negative", "None", "Sys"),
            ("Inactible", "Inaction", "Negative", "None", "Sys"),
            ("BeingGolden", "Golden State", "None", "None", "C3"),
            ("Operation", "Operation", "None", "None", "C3"),
            ("BabayagaTimeLimit", "Baba Yaga Approaches", "None", "None", "Boss"),
            ("Enrage", "Enrage", "None", "None", "C2"),
            ("Aggressive", "Aggressive", "None", "None", "C2"),
            ("Nail", "N Corp. Nail", "None", "None", "C3"),
            ("LittleCourage", "A Little Courage", "None", "None", "Sys"),
            ("Combustion", "Burn", "None", "SinBuff", "Sys"),
            ("Laceration", "Bleed", "None", "SinBuff", "Sys"),
            ("Vibration", "Tremor", "None", "SinBuff", "Sys"),
            ("Burst", "Rupture", "None", "SinBuff", "Sys"),
            ("Sinking", "Sinking", "None", "SinBuff", "Sys"),
            ("Breath", "Poise", "None", "SinBuff", "Sys"),
            ("BloodPocket", "Blood Pocket", "None", "None", "C3"),
            ("WaterPocket", "Water Pocket", "None", "None", "C3"),
            ("DogThunder", "Dog Thunder", "None", "None", "C3"),
            ("AttackUp", "Attack Up", "None", "None", "Sys"),
            ("AttackDown", "Attack Down", "None", "None", "Sys"),
            ("SlashResistUp", "Slash Resist Up", "None", "None", "Sys"),
            ("PenetrateResistUp", "Pierce Resist Up", "None", "None", "Sys"),
            ("HitResistUp", "Blunt Resist Up", "None", "None", "Sys"),
            ("SlashDamageUp", "Slash Damage Up", "None", "None", "Sys"),
            ("PenetrateDamageUp", "Pierce Damage Up", "None", "None", "Sys"),
            ("HitDamageUp", "Blunt Damage Up", "None", "None", "Sys"),
            ("Bullet", "Ammo", "None", "None", "Sys"),
            ("SlashResistDown", "Slash Resist Down", "None", "None", "Sys"),
            ("PenetrateResistDown", "Pierce Resist Down", "None", "None", "Sys"),
            ("HitResistDown", "Blunt Resist Down", "None", "None", "Sys"),
            ("SlashDamageDown", "Slash Damage Down", "None", "None", "Sys"),
            ("PenetrateDamageDown", "Pierce Damage Down", "None", "None", "Sys"),
            ("HitDamageDown", "Blunt Damage Down", "None", "None", "Sys"),
            ("CrimsonResistDown", "Wrath Resist Down", "None", "None", "Sys"),
            ("ScarletResistDown", "Lust Resist Down", "None", "None", "Sys"),
            ("AmberResistDown", "Sloth Resist Down", "None", "None", "Sys"),
            ("ShamrockResistDown", "Gluttony Resist Down", "None", "None", "Sys"),
            ("AzureResistDown", "Envy Resist Down", "None", "None", "Sys"),
            ("IndigoResistDown", "Pride Resist Down", "None", "None", "Sys"),
            ("VioletResistDown", "Gloom Resist Down", "None", "None", "Sys"),
            ("CrimsonResistUp", "Wrath Resist Up", "None", "None", "Sys"),
            ("ScarletResistUp", "Lust Resist Up", "None", "None", "Sys"),
            ("AmberResistUp", "Sloth Resist Up", "None", "None", "Sys"),
            ("ShamrockResistUp", "Gluttony Resist Up", "None", "None", "Sys"),
            ("AzureResistUp", "Envy Resist Up", "None", "None", "Sys"),
            ("IndigoResistUp", "Pride Resist Up", "None", "None", "Sys"),
            ("VioletResistUp", "Gloom Resist Up", "None", "None", "Sys"),
            ("CrimsonDamageDown", "Wrath Damage Down", "None", "None", "Sys"),
            ("ScarletDamageDown", "Lust Damage Down", "None", "None", "Sys"),
            ("AmberDamageDown", "Sloth Damage Down", "None", "None", "Sys"),
            ("ShamrockDamageDown", "Gluttony Damage Down", "None", "None", "Sys"),
            ("AzureDamageDown", "Envy Damage Down", "None", "None", "Sys"),
            ("IndigoDamageDown", "Pride Damage Down", "None", "None", "Sys"),
            ("VioletDamageDown", "Gloom Damage Down", "None", "None", "Sys"),
            ("CrimsonDamageUp", "Wrath Damage Up", "None", "None", "Sys"),
            ("ScarletDamageUp", "Lust Damage Up", "None", "None", "Sys"),
            ("AmberDamageUp", "Sloth Damage Up", "None", "None", "Sys"),
            ("ShamrockDamageUp", "Gluttony Damage Up", "None", "None", "Sys"),
            ("AzureDamageUp", "Envy Damage Up", "None", "None", "Sys"),
            ("IndigoDamageUp", "Pride Damage Up", "None", "None", "Sys"),
            ("VioletDamageUp", "Gloom Damage Up", "None", "None", "Sys"),
            ("Curse", "Curse", "None", "None", "Sys"),
            ("Muckworm", "Maggots", "None", "None", "C1"),
            ("PinkRibbon_Ishmael", "Pink Ribbon", "None", "None", "C5"),
            ("AttackDmgUp_Weak", "Damage Up (Weak)", "None", "None", "Sys"),
            ("Meursault_Last_Remodeling", "Last Remodeling", "None", "None", "Sys"),
            ("Assemble", "Fanaticism", "None", "None", "C3"),
            ("ReinforcedAssemble", "Reinforced Fanaticism", "None", "None", "C3"),
            ("Greedy", "Greed", "None", "None", "Sys"),
            ("Decay", "Decay", "None", "None", "Sys"),
            ("Poison", "Poison", "None", "None", "Sys"),
            ("Choice_90103301", "Choice Effect", "None", "None", "Sys"),
            ("Choice_901001", "Choice Effect I", "None", "None", "Sys"),
            ("Choice_901007", "Choice Effect VII", "None", "None", "Sys"),
            ("Choice_901009", "Choice Effect IX", "None", "None", "Sys"),
            ("Choice_901010", "Choice Effect X", "None", "None", "Sys"),
            ("Choice_901019", "Choice Effect XIX", "None", "None", "Sys"),
            ("Choice_90103402", "Choice Effect II", "None", "None", "Sys"),
            ("Choice_1030301", "Choice Effect III", "None", "None", "Sys"),
            ("Choice_1030501", "Choice Effect IV", "None", "None", "Sys"),
            ("Choice_1031001", "Choice Effect V", "None", "None", "Sys"),
            ("Whistle_Fear", "Kromer's Fear", "None", "None", "Boss"),
            ("Whistle_Courage", "Kromer's Courage", "None", "None", "Boss"),
            ("Cromer_Target", "Gripped Gaze", "None", "None", "Boss"),
            ("Cromer_Boredom", "Kromer's Boredom", "None", "None", "Boss"),
            ("Cromer_Ecstasy", "Kromer's Ecstasy", "None", "None", "Boss"),
            ("Cromer_Madness", "Purity", "None", "None", "Boss"),
            ("SlashTakeDamageDown", "Slash Take Damage Down", "None", "None", "Sys"),
            ("PenetrateTakeDamageDown", "Pierce Take Damage Down", "None", "None", "Sys"),
            ("HitTakeDamageDown", "Blunt Take Damage Down", "None", "None", "Sys"),
            ("SlashTakeDamageUp", "Slash Take Damage Up", "None", "None", "Sys"),
            ("PenetrateTakeDamageUp", "Pierce Take Damage Up", "None", "None", "Sys"),
            ("HitTakeDamageUp", "Blunt Take Damage Up", "None", "None", "Sys"),
            ("CrimsonTakeDamageDown", "Wrath Take Damage Down", "None", "None", "Sys"),
            ("ScarletTakeDamageDown", "Lust Take Damage Down", "None", "None", "Sys"),
            ("AmberTakeDamageDown", "Sloth Take Damage Down", "None", "None", "Sys"),
            ("ShamrockTakeDamageDown", "Gluttony Take Damage Down", "None", "None", "Sys"),
            ("AzureTakeDamageDown", "Envy Take Damage Down", "None", "None", "Sys"),
            ("IndigoTakeDamageDown", "Pride Take Damage Down", "None", "None", "Sys"),
            ("VioletTakeDamageDown", "Gloom Take Damage Down", "None", "None", "Sys"),
            ("CrimsonTakeDamageUp", "Wrath Take Damage Up", "None", "None", "Sys"),
            ("ScarletTakeDamageUp", "Lust Take Damage Up", "None", "None", "Sys"),
            ("AmberTakeDamageUp", "Sloth Take Damage Up", "None", "None", "Sys"),
            ("ShamrockTakeDamageUp", "Gluttony Take Damage Up", "None", "None", "Sys"),
            ("AzureTakeDamageUp", "Envy Take Damage Up", "None", "None", "Sys"),
            ("IndigoTakeDamageUp", "Pride Take Damage Up", "None", "None", "Sys"),
            ("VioletTakeDamageUp", "Gloom Take Damage Up", "None", "None", "Sys"),
            ("Desire", "Desire", "None", "None", "C7"),
            ("NailPersonality", "Nail (Personality)", "None", "None", "C1"),
            ("AssemblePersonality", "Fanaticism (Personality)", "None", "None", "C3"),
            ("MarkOfHeresy", "Mark of Heresy", "None", "None", "C3"),
            ("SlashResultUp", "Slash Clash Power Up", "None", "None", "Sys"),
            ("PenetrateResultUp", "Pierce Clash Power Up", "None", "None", "Sys"),
            ("HitResultUp", "Blunt Clash Power Up", "None", "None", "Sys"),
            ("SlashResultDown", "Slash Clash Power Down", "None", "None", "Sys"),
            ("PenetrateResultDown", "Pierce Clash Power Down", "None", "None", "Sys"),
            ("HitResultDown", "Blunt Clash Power Down", "None", "None", "Sys"),
            ("CrimsonResultUp", "Wrath Clash Power Up", "None", "None", "Sys"),
            ("ScarletResultUp", "Lust Clash Power Up", "None", "None", "Sys"),
            ("AmberResultUp", "Sloth Clash Power Up", "None", "None", "Sys"),
            ("ShamrockResultUp", "Gluttony Clash Power Up", "None", "None", "Sys"),
            ("AzureResultUp", "Envy Clash Power Up", "None", "None", "Sys"),
            ("IndigoResultUp", "Pride Clash Power Up", "None", "None", "Sys"),
            ("VioletResultUp", "Gloom Clash Power Up", "None", "None", "Sys"),
            ("CrimsonResultDown", "Wrath Clash Power Down", "None", "None", "Sys"),
            ("ScarletResultDown", "Lust Clash Power Down", "None", "None", "Sys"),
            ("AmberResultDown", "Sloth Clash Power Down", "None", "None", "Sys"),
            ("ShamrockResultDown", "Gluttony Clash Power Down", "None", "None", "Sys"),
            ("AzureResultDown", "Envy Clash Power Down", "None", "None", "Sys"),
            ("IndigoResultDown", "Pride Clash Power Down", "None", "None", "Sys"),
            ("VioletResultDown", "Gloom Clash Power Down", "None", "None", "Sys"),
            ("Cycle", "Cycle", "None", "None", "C3"),
            ("Duress", "Duress", "None", "None", "C3"),
            ("WeaknessAnalysis", "Weakness Analysis", "None", "None", "C3"),
            ("CyclingKarma", "Cycling Karma", "None", "None", "C3"),
            ("TakeHpHealReduce", "Healing Received Down", "None", "None", "C3"),
            ("AttackLevelAdder", "Attack Level Adder", "None", "None", "C3"),
            ("Thirst", "Thirst", "None", "None", "C7"),
            ("GainSinStockAdder", "E.G.O Resource Gain Up", "None", "None", "C3"),
            ("Weak_Chicken", "Weak Chicken", "None", "None", "C3"),
            ("Strong_Chicken", "Strong Chicken", "None", "None", "C3"),
            ("DimensionRift", "Dimension Rift", "None", "None", "C2"),
            ("VibrationExplosion", "Tremor Explosion", "None", "None", "C2"),
            ("ATL_Agility", "ATL: Haste", "None", "None", "C2"),
            ("ATL_Breath", "ATL: Poise", "None", "None", "C2"),
            ("ATL_Target", "ATL: Target", "None", "None", "C2"),
            ("ElectricStorage", "Electric Storage", "None", "None", "EGO"),
            ("SelfCharge", "Self Charge", "None", "None", "EGO"),
            ("Exalted", "Exalted", "None", "None", "Sys"),
            ("Tipsiness", "Tipsiness", "None", "None", "C2"),
            ("Predation", "Predation", "None", "None", "C1"),
            ("Bull_FadedHeat", "Faded Heat", "None", "None", "C2"),
            ("Bull_Fever", "Fever", "None", "None", "C2"),
            ("Bull_Sadness", "Sadness", "None", "None", "C2"),
            ("Bull_ReinforcedSadness", "Reinforced Sadness", "None", "None", "C2"),
            ("Bull_BuzzingEmotion", "Buzzing Emotion", "None", "None", "C2"),
            ("VibrationAssimilation", "Tremor Assimilation", "None", "None", "C2"),
            ("Resonate", "Resonate", "None", "None", "C2"),
            ("WrappedCurseTag", "Curse Tag (Wrapped)", "None", "None", "C2"),
            ("ReattachedCurseTag", "Curse Tag (Reattached)", "None", "None", "C2"),
            ("Scared", "Scared", "None", "None", "C3"),
            ("Stressed", "Stressed", "None", "None", "C3"),
            ("Nervous", "Nervous", "None", "None", "C3"),
            ("ShardOfUmbrella", "Umbrella Shard", "None", "None", "C2"),
            ("Crazy_LowMorale", "Frenzy: Low Morale", "None", "None", "C3"),
            ("Crazy_Panic", "Frenzy: Panic", "None", "None", "C3"),
            ("Suicide_LowMorale", "Suicidal: Low Morale", "None", "None", "C3"),
            ("Suicide_Panic", "Suicidal: Panic", "None", "None", "C3"),
            ("Prowl_LowMorale", "Prowl: Low Morale", "None", "None", "C3"),
            ("Prowl_Panic", "Prowl: Panic", "None", "None", "C3"),
            ("Fragile_Mind_LowMorale", "Fragile Mind: Low Morale", "None", "None", "C3"),
            ("Fragile_Mind_Panic", "Fragile Mind: Panic", "None", "None", "C3"),
            ("ErodingMind", "Eroding Mind", "None", "None", "C3"),
            ("Thorn", "Thorn", "None", "None", "C3"),
            ("CollapseAmpoule", "Collapse Ampoule", "None", "None", "C4"),
            ("DongbaekGrow", "Dongbaek: Growth", "None", "None", "Boss"),
            ("DongbaekFlorescence", "Dongbaek: Florescence", "None", "None", "Boss"),
            ("DongbaekFullBloom", "Dongbaek: Full Bloom", "None", "None", "Boss"),
            ("DongbaekFascination", "Dongbaek: Fascination", "None", "None", "Boss"),
            ("DongbaekShowdown", "Dongbaek: Showdown", "None", "None", "Boss"),
            ("DongbaekScatterPetal", "Dongbaek: Scatter Petal", "None", "None", "Boss"),
            ("Blue_LowMorale", "Blue: Low Morale", "None", "None", "C3"),
            ("Blue_Panic", "Blue: Panic", "None", "None", "C3"),
            ("InterlockingTime", "Interlocking Time", "None", "None", "C2"),
            ("TimeRental", "Time Rental", "None", "None", "C2"),
            ("ATL_EndureLaceration", "ATL: Endure Bleed", "None", "None", "C2"),
            ("EmittedCurrent", "Emitted Current", "None", "None", "C4"),
            ("Choice_1040301", "Choice Effect VI", "None", "None", "Sys"),
            ("DistortedDongrangRadiantVanity", "Distorted Dongrang: Radiant Vanity", "None", "None", "Boss"),
            ("DistortedDongrangEmptyMark", "Distorted Dongrang: Empty Mark", "None", "None", "Boss"),
            ("DistortedDongrangEarnestAchievement", "Distorted Dongrang: Earnest Achievement", "None", "None", "Boss"),
            ("DistortedDongrangEmptyHonor", "Distorted Dongrang: Empty Honor", "None", "None", "Boss"),
            ("DistortedDongrangMomentaryGlory", "Distorted Dongrang: Momentary Glory", "None", "None", "Boss"),
            ("DistortedDongrangEmptiness", "Distorted Dongrang: Emptiness", "None", "None", "Boss"),
            ("DistortedDongrangFruition", "Distorted Dongrang: Fruition", "None", "None", "Boss"),
            ("MpTakeDamageDown", "SP Damage Received Down", "None", "None", "C3"),
            ("EgoAwakenDongrangRadiantDesire", "Ego-Awakened Dongrang: Radiant Desire", "None", "None", "EGO"),
            ("EgoAwakenDongrangSeed", "Ego-Awakened Dongrang: Seed", "None", "None", "EGO"),
            ("EgoAwakenDongrangTree", "Ego-Awakened Dongrang: Nutrient Absorption", "None", "None", "EGO"),
            ("EgoAwakenDongrangOverHeal", "Ego-Awakened Dongrang: Overflow", "None", "None", "EGO"),
            ("EgoAwakenDongrangTreeDisplay", "Ego-Awakened Dongrang: Tree", "None", "None", "EGO"),
            ("DongbaekFullBloomDisplay", "Dongbaek: Full Bloom (Display)", "None", "None", "Boss"),
            ("SinkingSurge", "Sinking Surge", "None", "None", "C3"),
            ("Blue", "Tear", "None", "None", "C3"),
            ("EgoAwakenDongrangShardOfBrokenConnection", "Ego-Awakened Dongrang: Severed Bond", "None", "None", "EGO"),
            ("KCorpSerum", "K Corp. Ampule", "None", "None", "C4"),
            ("Talisman", "Talisman", "None", "None", "C7"),
            ("TakeHpHealIncrease", "Healing Received Up", "None", "None", "C3"),
            ("Choice_90104001", "Choice Effect VII-A", "None", "None", "Sys"),
            ("Choice_9010400101", "Choice Effect VII-B", "None", "None", "Sys"),
            ("Choice_90104002", "Choice Effect VIII", "None", "None", "Sys"),
            ("SkillPowerUp", "Skill Power Up", "None", "None", "C3"),
            ("MaxHpMultiplier", "Max HP Multiplier", "None", "None", "C3"),
            ("S2Mirror2ndFloor", "Mirror Dungeon II: 2F", "None", "None", "MD"),
            ("S2Mirror3rdFloor", "Mirror Dungeon II: 3F", "None", "None", "MD"),
            ("S2Mirror4thFloor", "Mirror Dungeon II: 4F", "None", "None", "MD"),
            ("S2Mirror5thFloor", "Mirror Dungeon II: 5F", "None", "None", "MD"),
            ("S2Mirror2ndFloor_Hard", "Mirror Dungeon II: 2F (Hard)", "None", "None", "MD"),
            ("S2Mirror3rdFloor_Hard", "Mirror Dungeon II: 3F (Hard)", "None", "None", "MD"),
            ("S2Mirror4thFloor_Hard", "Mirror Dungeon II: 4F (Hard)", "None", "None", "MD"),
            ("S2Mirror5thFloor_Hard", "Mirror Dungeon II: 5F (Hard)", "None", "None", "MD"),
            ("S2Mirror1stFloor", "Mirror Dungeon II: 1F", "None", "None", "MD"),
            ("S2Mirror1stFloor_Hard", "Mirror Dungeon II: 1F (Hard)", "None", "None", "MD"),
            ("MentalSystemResultIncrease_Typo", "SP Recovery Up (Legacy)", "None", "None", "C3"),
            ("MentalSystemResultDecrease_Typo", "SP Loss Up (Legacy)", "None", "None", "C3"),
            ("DuelDeclaration_DonQuixote", "Duel Declaration - Don Quixote", "None", "None", "C3"),
            ("Aggro", "Aggro", "None", "None", "C3"),
            ("BurstVulnerable", "Rupture Vulnerable", "None", "None", "C3"),
            ("EmergencyFeed", "Emergency Feed", "None", "None", "C3"),
            ("Bullet_Crab", "Crab Ammo", "None", "None", "EGO"),
            ("SinkingVulnerable", "Sinking Vulnerable", "None", "None", "C3"),
            ("BurstProtection", "Rupture Protection", "None", "None", "C3"),
            ("ChargeForceField", "Charge Force Field", "None", "None", "C3"),
            ("RailLine2Buff", "Rail Line 2 Buff", "None", "None", "C2"),
            ("TipsinessRail", "Tipsiness (Rail)", "None", "None", "C2"),
            ("FairyCharm", "Fairy Charm", "None", "None", "C2"),
            ("ReattachedCurseTag_Re", "Curse Tag (Re-attached)", "None", "None", "C2"),
            ("PassedPortal_Red", "Passed Portal: Red", "None", "None", "C2"),
            ("PassedPortal_Green", "Passed Portal: Green", "None", "None", "C2"),
            ("PassedPortal_Yellow", "Passed Portal: Yellow", "None", "None", "C2"),
            ("PassedPortal_Blue", "Passed Portal: Blue", "None", "None", "C2"),
            ("InterlockingTime_Re", "Interlocking Time (Reset)", "None", "None", "C2"),
            ("TimeRental_Re", "Time Rental (Reset)", "None", "None", "C2"),
            ("WaveFoxUmbrella", "Wave Fox Umbrella", "None", "None", "C2"),
            ("AccumulatedPast", "Accumulated Past (Mirror)", "None", "None", "MD"),
            ("PrepareSinRose", "Prepare Sin Rose", "None", "None", "Sys"),
            ("BloomingRose", "Blooming Rose", "None", "None", "Sys"),
            ("EatingSin", "Consuming Sin", "None", "None", "Sys"),
            ("EnhanceRose", "Enhanced Rose", "None", "None", "Sys"),
            ("DecoyRegenerated", "Decoy Regenerated", "None", "None", "Sys"),
            ("FullBloomRose_Crimson", "Full Bloom: Wrath", "None", "None", "Sys"),
            ("FullBloomRose_Scarlet", "Full Bloom: Lust", "None", "None", "Sys"),
            ("FullBloomRose_Amber", "Full Bloom: Sloth", "None", "None", "Sys"),
            ("FullBloomRose_Shamrock", "Full Bloom: Gluttony", "None", "None", "Sys"),
            ("FullBloomRose_Azure", "Full Bloom: Envy", "None", "None", "Sys"),
            ("FullBloomRose_Indigo", "Full Bloom: Pride", "None", "None", "Sys"),
            ("FullBloomRose_Violet", "Full Bloom: Gloom", "None", "None", "Sys"),
            ("KalpaVine", "Kalpa Vine", "None", "None", "Sys"),
            ("WrappedCurseTagRe", "Curse Tag (Re-wrapped)", "None", "None", "C2"),
            ("FullBloomRose", "Full Bloom Rose", "None", "None", "Sys"),
            ("KnowledgeExplored", "Knowledge Explored", "None", "None", "MD"),
            ("Discard", "Discard", "None", "None", "MD"),
            ("AaCePaBa", "Formation [E][A]-A", "None", "None", "MD"),
            ("AaCePbBa", "Formation [E][B]-A", "None", "None", "MD"),
            ("AaCePbBb", "Formation [E][B]-B", "None", "None", "MD"),
            ("AaCePbBi", "Formation [E][B]-I", "None", "None", "MD"),
            ("AaCePcBa", "Formation [E][C]-A", "None", "None", "MD"),
            ("AaCePcBb", "Formation [E][C]-B", "None", "None", "MD"),
            ("AaCePcBc", "Formation [E][C]-C", "None", "None", "MD"),
            ("AaCePcBg", "Formation [E][C]-G", "None", "None", "MD"),
            ("AaCePcBh", "Formation [E][C]-H", "None", "None", "MD"),
            ("AaCePcBi", "Formation [E][C]-I", "None", "None", "MD"),
            ("AaCePcBj", "Formation [E][C]-J", "None", "None", "MD"),
            ("AaCePcBk", "Formation [E][C]-K", "None", "None", "MD"),
            ("AaCePcBl", "Formation [E][C]-L", "None", "None", "MD"),
            ("AaCePcBm", "Formation [E][C]-M", "None", "None", "MD"),
            ("AaCePcBn", "Formation [E][C]-N", "None", "None", "MD"),
            ("AaCePcBo", "Formation [E][C]-O", "None", "None", "MD"),
            ("AaCePcBp", "Formation [E][C]-P", "None", "None", "MD"),
            ("AaCePcBq", "Formation [E][C]-Q", "None", "None", "MD"),
            ("AaCePcBr", "Formation [E][C]-R", "None", "None", "MD"),
            ("AaCePcBs", "Formation [E][C]-S", "None", "None", "MD"),
            ("AaCePcBt", "Formation [E][C]-T", "None", "None", "MD"),
            ("AaCePbBc", "Formation [E][B]-C", "None", "None", "MD"),
            ("AaCePbBd", "Formation [E][B]-D", "None", "None", "MD"),
            ("AaCePbBe", "Formation [E][B]-E", "None", "None", "MD"),
            ("AaCePbBf", "Formation [E][B]-F", "None", "None", "MD"),
            ("AaCePbBg", "Formation [E][B]-G", "None", "None", "MD"),
            ("AaCePbBh", "Formation [E][B]-H", "None", "None", "MD"),
            ("AaCePcBe", "Formation [E][C]-E", "None", "None", "MD"),
            ("AaCePcBf", "Formation [E][C]-F", "None", "None", "MD"),
            ("NoneMp_LowMorale", "No SP: Low Morale", "None", "None", "C2"),
            ("NoneMp_Panic", "No SP: Panic", "None", "None", "C2"),
            ("Unjust_Enrichment", "Unjust Enrichment", "None", "None", "Sys"),
            ("Resentment", "Resentment", "None", "None", "C3"),
            ("RetaliationBook", "Retaliation Target", "None", "None", "C3"),
            ("MDcFaBa", "MD: Burn (Extra)", "None", "None", "MD"),
            ("MDcFaBb", "MD: Bleed (Extra)", "None", "None", "MD"),
            ("MDcFaBc", "MD: Tremor (Extra)", "None", "None", "MD"),
            ("MDcFaBd", "MD: Rupture (Extra)", "None", "None", "MD"),
            ("MDcFaBe", "MD: Sinking (Extra)", "None", "None", "MD"),
            ("MDcFaBf", "MD: Slash Enhance", "None", "None", "MD"),
            ("MDcFaBg", "MD: Pierce Enhance", "None", "None", "MD"),
            ("MDcFaBh", "MD: Blunt Enhance", "None", "None", "MD"),
            ("MDcFbBa", "MD: Attr Resist Up (ex. Wrath)", "None", "None", "MD"),
            ("MDcFbBb", "MD: Attr Resist Up (ex. Lust)", "None", "None", "MD"),
            ("MDcFbBc", "MD: Attr Resist Up (ex. Sloth)", "None", "None", "MD"),
            ("MDcFbBd", "MD: Attr Resist Up (ex. Gluttony)", "None", "None", "MD"),
            ("MDcFbBe", "MD: Attr Resist Up (ex. Gloom)", "None", "None", "MD"),
            ("MDcFbBf", "MD: Attr Resist Up (ex. Pride)", "None", "None", "MD"),
            ("MDcFbBg", "MD: Attr Resist Up (ex. Envy)", "None", "None", "MD"),
            ("MDcFbBh", "MD: HP Boost", "None", "None", "MD"),
            ("MDcFcBa", "MD: Slot Weight Up", "None", "None", "MD"),
            ("MDcFcBb", "MD: Damage Absorb", "None", "None", "MD"),
            ("MDcFcBc", "MD: Speed Up", "None", "None", "MD"),
            ("MDcFcBd", "MD: Mental Attack", "None", "None", "MD"),
            ("MDcFcBe", "MD: Sharpness", "None", "None", "MD"),
            ("MDcFcBf", "MD: Regroup", "None", "None", "MD"),
            ("MDcFcBg", "MD: Unclashed Atk Enhance", "None", "None", "MD"),
            ("MDcFcBh", "MD: Clashed Atk Enhance", "None", "None", "MD"),
            ("MDHcFaBa", "MD: Defense Level Up", "None", "None", "MD"),
            ("MDHcFaBb", "MD: Guard Power Up", "None", "None", "MD"),
            ("MDHcFaBc", "MD: Max HP Up", "None", "None", "MD"),
            ("MDHcFaBd", "MD: Dmg Taken Down", "None", "None", "MD"),
            ("MDHcFbBa", "MD: Attack Level Up", "None", "None", "MD"),
            ("MDHcFbBb", "MD: Final Power Up", "None", "None", "MD"),
            ("MDHcFbBc", "MD: Base Power Up", "None", "None", "MD"),
            ("MDHcFbBd", "MD: Dmg Dealt Up", "None", "None", "MD"),
            ("MDHcFcBa", "MD: Final Power Amp I", "None", "None", "MD"),
            ("MDHcFcBb", "MD: Final Power Amp II", "None", "None", "MD"),
            ("MDHcFcBc", "MD: Base Power Amp I", "None", "None", "MD"),
            ("MDHcFcBd", "MD: Base Power Amp II", "None", "None", "MD"),
            ("MDHcFcBe", "MD: Coin Power Amp I", "None", "None", "MD"),
            ("MDHcFcBf", "MD: Overload I", "None", "None", "MD"),
            ("MDHcFdBa", "MD: Final Power Amp III", "None", "None", "MD"),
            ("MDHcFdBb", "MD: Final Power Amp IV", "None", "None", "MD"),
            ("MDHcFdBc", "MD: Coin Power Amp II", "None", "None", "MD"),
            ("MDHcFdBd", "MD: Base Power Amp III", "None", "None", "MD"),
            ("MDHcFdBe", "MD: Overload II", "None", "None", "MD"),
            ("MDHcFdBf", "MD: Tenacity", "None", "None", "MD"),
            ("MDHcFdBg", "MD: Fortitude", "None", "None", "MD"),
            ("DuelDeclaration_Outis", "Duel Declaration - Outis", "None", "None", "C3"),
            ("DuelDeclaration_Sinclair", "Duel Declaration - Sinclair", "None", "None", "C3"),
            ("OneOnOneDuel", "One on One Duel", "None", "None", "C3"),
            ("AaCeSeBa", "Formation [SE]-B", "None", "None", "MD"),
            ("AaCeSeBb", "Formation [SE]-B", "None", "None", "MD"),
            ("CtrlTeamCaptain", "Control Team Captain", "None", "None", "MD"),
            ("FreischutzShotCount", "Magic Bullet Count", "None", "None", "EGO"),
            ("DarkFlame", "Dark Flame", "None", "None", "Sys"),
            ("MRcAiBa", "MR Constraint: Ai-A", "None", "None", "C9"),
            ("MRcAiBb", "MR Constraint: Ai-B", "None", "None", "C9"),
            ("MRcAiBc", "MR Constraint: Ai-C", "None", "None", "C9"),
            ("MRcAfBa", "MR Constraint: Af-A", "None", "None", "C9"),
            ("MRcAfBb", "MR Constraint: Af-B", "None", "None", "C9"),
            ("MRcAmBa", "MR Constraint: Am-A", "None", "None", "C9"),
            ("MRcAmBb", "MR Constraint: Am-B", "None", "None", "C9"),
            ("MRcAmBc", "MR Constraint: Am-C", "None", "None", "C9"),
            ("MRcAmBd", "MR Constraint: Am-D", "None", "None", "C9"),
            ("MRcAmBbDisplay", "MR Constraint: Am-B Display", "None", "None", "C9"),
            ("HeatedGasHarpoon", "Heated Gas Harpoon", "None", "None", "C6"),
            ("OverHeatedGasHarpoon", "Overheated Gas Harpoon", "None", "None", "C6"),
            ("CoverAttack", "Cover Attack", "None", "None", "C6"),
            ("ReCompulsion", "Recompulsion", "None", "None", "C6"),
            ("Grudge", "Grudge", "None", "None", "C6"),
            ("BladeResultUpTier1", "Blade Clash Power Up I", "None", "None", "C6"),
            ("BladeResultUpTier2", "Blade Clash Power Up II", "None", "None", "C6"),
            ("RedApricotBlossom", "Red Plum Blossom (Bleed)", "None", "None", "Sys"),
            ("SwordPlayOfTheHomeland", "Swordplay of the Homeland", "None", "None", "C6"),
            ("SwordPlayOfTheMemorial", "Swordplay of Memory", "None", "None", "C6"),
            ("VibrationCollapse", "Tremor Collapse", "None", "None", "C6"),
            ("AaCfPaBa", "Formation [F][A]-A", "None", "None", "MD"),
            ("CleanUp_LowMorale", "Clean Up: Low Morale", "None", "None", "C6"),
            ("CleanUp_Panic", "Clean Up: Panic", "None", "None", "C6"),
            ("Acclamation_LowMorale", "Acclamation: Low Morale", "None", "None", "C6"),
            ("Acclamation_Panic", "Acclamation: Panic", "None", "None", "C6"),
            ("Unstable_LowMorale", "Unstable: Low Morale", "None", "None", "C6"),
            ("Unstable_Panic", "Unstable: Panic", "None", "None", "C6"),
            ("EchoOfMansion", "Echo of the Mansion", "None", "None", "Boss"),
            ("EchoOfMansion_Main", "Echo of the Mansion (Main)", "None", "None", "Boss"),
            ("EchoOfMansion_Sub", "Echo of the Mansion (Sub)", "None", "None", "Boss"),
            ("Switch_Vibration", "Tremor Switch", "None", "None", "Boss"),
            ("Vengeance_LowMorale", "Vengeance: Low Morale", "None", "None", "C6"),
            ("Vengeance_Panic", "Vengeance: Panic", "None", "None", "C6"),
            ("AaCfPbBa", "Formation [F][B]-A", "None", "None", "MD"),
            ("AaCfPbBb", "Formation [F][B]-B", "None", "None", "MD"),
            ("AaCfPbBc", "Formation [F][B]-C", "None", "None", "MD"),
            ("AaCfPbBd", "Formation [F][B]-D", "None", "None", "MD"),
            ("AaCfPbBe", "Formation [F][B]-E", "None", "None", "MD"),
            ("AaCfPbBf", "Formation [F][B]-F", "None", "None", "MD"),
            ("AaCfPbBg", "Formation [F][B]-G", "None", "None", "MD"),
            ("AaCfPbBh", "Formation [F][B]-H", "None", "None", "MD"),
            ("AaCfPbBi", "Formation [F][B]-I", "None", "None", "MD"),
            ("AaCfPbBj", "Formation [F][B]-J", "None", "None", "MD"),
            ("AaCfPcBb", "Formation [F][C]-B", "None", "None", "MD"),
            ("AaCfPcBc", "Formation [F][C]-C", "None", "None", "MD"),
            ("AaCfPcBa", "Formation [F][C]-A", "None", "None", "MD"),
            ("AaCfPcBe", "Formation [F][C]-E", "None", "None", "MD"),
            ("AaCfPcBf", "Formation [F][C]-F", "None", "None", "MD"),
            ("AaCfPcBg", "Formation [F][C]-G", "None", "None", "MD"),
            ("AaCfPcBh", "Formation [F][C]-H", "None", "None", "MD"),
            ("AaCfPcBi", "Formation [F][C]-I", "None", "None", "MD"),
            ("AaCfPcBj", "Formation [F][C]-J", "None", "None", "MD"),
            ("AaCfPcBk", "Formation [F][C]-K", "None", "None", "MD"),
            ("AaCfPcBl", "Formation [F][C]-L", "None", "None", "MD"),
            ("AaCfPcBm", "Formation [F][C]-M", "None", "None", "MD"),
            ("AaCfPcBn", "Formation [F][C]-N", "None", "None", "MD"),
            ("AaCfPcBo", "Formation [F][C]-O", "None", "None", "MD"),
            ("AaCfPcBp", "Formation [F][C]-P", "None", "None", "MD"),
            ("AaCfPcBq", "Formation [F][C]-Q", "None", "None", "MD"),
            ("AaCfPcBr", "Formation [F][C]-R", "None", "None", "MD"),
            ("AaCfPcBs", "Formation [F][C]-S", "None", "None", "MD"),
            ("Loss_LowMorale", "Loss: Low Morale", "None", "None", "C6"),
            ("Loss_Panic", "Loss: Panic", "None", "None", "C6"),
            ("ForwardToTheKing", "March to the King", "None", "None", "Boss"),
            ("ForwardToTheBoundKing", "March to the Bound King", "None", "None", "Boss"),
            ("VibrationCrack", "Tremor Crack", "None", "None", "C6"),
            ("MarkOfButler", "Butler's Mark", "None", "None", "C7"),
            ("LibrarianOfHistoryNormal", "Librarian of History", "None", "None", "MD"),
            ("PreparedMeat", "Prepared Meat", "None", "None", "C7"),
            ("Hunger", "Hunger", "None", "None", "C7"),
            ("UnstableFeeling", "Unstable Feeling", "None", "None", "C7"),
            ("MDEMaa", "MD-EM: Defense Level Up", "None", "None", "MD"),
            ("MDEMab", "MD-EM: Guard Skill Up", "None", "None", "MD"),
            ("MDEMac", "MD-EM: Attack Level Up", "None", "None", "MD"),
            ("MDEMad", "MD-EM: Max HP Up", "None", "None", "MD"),
            ("MDEMae", "MD-EM: Dmg Taken Down+", "None", "None", "MD"),
            ("MDEMba", "MDEMba", "None", "None", "MD"),
            ("MDEMbb", "MDEMbb", "None", "None", "MD"),
            ("MDEMbc", "MDEMbc", "None", "None", "MD"),
            ("MDEMbd", "MDEMbd", "None", "None", "MD"),
            ("MDEMbe", "MDEMbe", "None", "None", "MD"),
            ("MDEMbf", "MDEMbf", "None", "None", "MD"),
            ("MDEMca", "MD-EM: Body Enlargement", "None", "None", "MD"),
            ("MDEMcb", "MD-EM: Body Empowerment", "None", "None", "MD"),
            ("MDEMcc", "MD-EM: Max HP Up", "None", "None", "MD"),
            ("MDEMcd", "MD-EM: Final Atk Boost", "None", "None", "MD"),
            ("MDEMce", "MD-EM: Base Atk Boost", "None", "None", "MD"),
            ("MDEMcf", "MD-EM: Clash Atk Boost", "None", "None", "MD"),
            ("MDEMcg", "MD-EM: Final Power Up", "None", "None", "MD"),
            ("MDEMda", "MD-EM: Body Growth", "None", "None", "MD"),
            ("MDEMdb", "MD-EM: Body Reinforcement", "None", "None", "MD"),
            ("MDEMdc", "MD-EM: Basic Combat Gear", "None", "None", "MD"),
            ("MDEMdd", "MD-EM: Final Combat Gear", "None", "None", "MD"),
            ("MDEMde", "MD-EM: Defensive Gear", "None", "None", "MD"),
            ("MDEMdf", "MD-EM: Coin Atk Boost", "None", "None", "MD"),
            ("MDEMdg", "MD-EM: Peerless Might", "None", "None", "MD"),
            ("MDEMdh", "MD-EM: Give and Take", "None", "None", "MD"),
            ("MDHMaa", "MD-HM: Body Amp I", "None", "None", "MD"),
            ("MDHMab", "MD-HM: Hardness", "None", "None", "MD"),
            ("MDHMac", "MD-HM: Body Expand I", "None", "None", "MD"),
            ("MDHMad", "MD-HM: Max HP Boost I", "None", "None", "MD"),
            ("MDHMae", "MD-HM: Dmg Taken Down", "None", "None", "MD"),
            ("MDHMba", "MD-HM: Body Amp II", "None", "None", "MD"),
            ("MDHMbb", "MD-HM: Body Expand II", "None", "None", "MD"),
            ("MDHMbc", "MD-HM: Final Power Boost I", "None", "None", "MD"),
            ("MDHMbd", "MD-HM: Base Power Boost I", "None", "None", "MD"),
            ("MDHMbe", "MD-HM: Tenacity I", "None", "None", "MD"),
            ("MDHMbf", "MD-HM: Give I", "None", "None", "MD"),
            ("MDHMca", "MD-HM: Body Amp III", "None", "None", "MD"),
            ("MDHMcb", "MD-HM: Body Expand III", "None", "None", "MD"),
            ("MDHMcc", "MD-HM: Final Power Boost II", "None", "None", "MD"),
            ("MDHMcd", "MD-HM: Base Power Boost II", "None", "None", "MD"),
            ("MDHMce", "MD-HM: Final Power Boost III", "None", "None", "MD"),
            ("MDHMcf", "MD-HM: Max HP Boost II", "None", "None", "MD"),
            ("MDHMcg", "MD-HM: Clash Atk Boost I", "None", "None", "MD"),
            ("MDHMda", "MD-HM: Body Amp IV", "None", "None", "MD"),
            ("MDHMdb", "MD-HM: Body Expand IV", "None", "None", "MD"),
            ("MDHMdc", "MD-HM: Final Power Boost IV", "None", "None", "MD"),
            ("MDHMdd", "MD-HM: Base Power Boost III", "None", "None", "MD"),
            ("MDHMde", "MD-HM: Tenacity II", "None", "None", "MD"),
            ("MDHMdf", "MD-HM: Coin Power Boost", "None", "None", "MD"),
            ("MDHMdg", "MD-HM: Clash Atk Boost II", "None", "None", "MD"),
            ("MDHMdh", "MD-HM: Fortitude", "None", "None", "MD"),
            ("AccumulatedPastMirror", "Accumulated Past (Mirror)", "None", "None", "MD"),
            ("LibrarianOfHistoryHard", "Librarian of History (Hard)", "None", "None", "MD"),
            ("PriceOfCare", "Price of Care", "None", "None", "MD"),
            ("CanvasA", "Canvas", "None", "None", "C7"),
            ("CompletedCanvasA", "Completed Canvas", "None", "None", "C7"),
            ("VibrationEcho", "Tremor Echo", "None", "None", "C6"),
            ("ShieldManagerCryingToad", "Crying Toad Shield", "None", "None", "C7"),
            ("ParryingResultUp", "Clash Power Up", "None", "None", "C6"),
            ("ParryingResultDown", "Clash Power Down", "None", "None", "C6"),
            ("FusionVibration", "Fusion Tremor", "None", "None", "C6"),
            ("VibrationNesting", "Tremor: Nest", "None", "CollapsableSinBuff", "Boss"),
            ("VibrationDistribution", "Tremor Distribution", "None", "None", "C6"),
            ("VibrationChain", "Tremor Chain", "None", "None", "C6"),
            ("TimeRentalTwo", "Time Rental II", "None", "None", "C2"),
            ("TimeAccumulation", "Time Accumulation", "None", "None", "C2"),
            ("Yurodivy_LowMorale", "Yurodiviy: Low Morale", "None", "None", "C6"),
            ("Yurodivy_Panic", "Yurodiviy: Panic", "None", "None", "C6"),
            ("TcorpSpecialInvestigator", "T Corp. Special Investigator", "None", "None", "C2"),
            ("TimeAcceleration", "Time Acceleration", "None", "None", "C2"),
            ("LimitedTime", "Limited Time", "None", "None", "C2"),
            ("OwnTime", "Own Time", "None", "None", "C2"),
            ("EquitableDistribution", "Equitable Distribution", "None", "None", "C2"),
            ("UnfairDistribution", "Unfair Distribution", "None", "None", "C2"),
            ("TimeKillerWatch", "Time Killer Watch", "None", "None", "C2"),
            ("VibrationContinue", "Tremor: Continue", "None", "None", "C6"),
            ("TimeSuspend", "Time Suspend", "None", "None", "C2"),
            ("VibrationChainPersonality", "Tremor Chain (Personality)", "None", "None", "C6"),
            ("TimeRentalTwoPersonality", "Time Rental II (Personality)", "None", "None", "C2"),
            ("GazePersonality", "Gaze (Personality)", "None", "None", "C6"),
            ("ContemptPersonality", "Contempt (Personality)", "None", "None", "C6"),
            ("KnowledgeTraining", "Knowledge Training", "None", "None", "Sys"),
            ("UninvitedGuest", "Uninvited Guest", "None", "None", "Sys"),
            ("ConnectedPlug", "Connected Plug", "None", "None", "Sys"),
            ("AntiSheepGround", "Anti-Sheep Ground", "None", "None", "Sys"),
            ("ThundercloudFormation", "Thundercloud Formation", "None", "None", "Sys"),
            ("BandageOfTheBoundKing", "Bound King's Bandage", "None", "None", "Boss"),
            ("ComeForwardToTheKing", "Come Forward to the King", "None", "None", "Boss"),
            ("Refraction4A", "Formation Effect I", "None", "None", "MD"),
            ("Refraction4B", "Formation Effect II", "None", "None", "MD"),
            ("Refraction4C", "Formation Effect III", "None", "None", "MD"),
            ("Refraction4D", "Formation Effect IV", "None", "None", "MD"),
            ("Refraction4E", "Formation Effect V", "None", "None", "MD"),
            ("Refraction4F", "Formation Effect VI", "None", "None", "MD"),
            ("Refraction4G", "Formation Effect VII", "None", "None", "MD"),
            ("Refraction4H", "Formation Effect VIII", "None", "None", "MD"),
            ("Refraction4I", "Formation Effect IX", "None", "None", "MD"),
            ("Refraction4J", "Formation Effect X", "None", "None", "MD"),
            ("Refraction4K", "Formation Effect XI", "None", "None", "MD"),
            ("Refraction4L", "Formation Effect XII", "None", "None", "MD"),
            ("VioletPeccatulumTwo", "Gloom Peccatum II", "None", "None", "Boss"),
            ("BigWelcome", "Big Welcome", "None", "None", "Boss"),
            ("RefractedWill", "Refracted Will", "None", "None", "Boss"),
            ("BreathSupport", "Poise Support", "None", "None", "Boss"),
            ("FirmWill", "Firm Will", "None", "None", "Boss"),
            ("ChargeLoad", "Charge Load", "None", "None", "Sys"),
            ("BoseProjektil", "Torn Memories", "None", "None", "C6"),
            ("KeptBlood", "Kept Blood", "None", "None", "C6"),
            ("BloodyCrave", "Bloody Craving", "None", "None", "C6"),
            ("BloodPocket_LowMorale", "Blood Pocket: Low Morale", "None", "None", "C3"),
            ("BloodPocket_Panic", "Blood Pocket: Panic", "None", "None", "C3"),
            ("PhotoElectricity", "Photoelectricity", "None", "None", "EGO"),
            ("HardenedBlood", "Hardened Blood", "None", "None", "C6"),
            ("Coffin", "Coffin", "None", "None", "Boss"),
            ("WildHunt", "Wild Hunt", "None", "None", "Boss"),
            ("NightPathfinding", "Dullahan Pathfinding", "None", "None", "Boss"),
            ("WanderingFootsteps", "Approaching Ruin", "None", "None", "Boss"),
            ("WanderingFootsteps_Main", "Approaching Ruin (Main)", "None", "None", "Boss"),
            ("WanderingFootsteps_Sub", "Approaching Ruin (Sub)", "None", "None", "Boss"),
            ("WanderingFootsteps_LowMorale", "Approaching Ruin: Low Morale", "None", "None", "Boss"),
            ("WanderingFootsteps_Panic", "Approaching Ruin: Panic", "None", "None", "Boss"),
            ("AaCfPaBa_Alt1", "Formation [F][A]-A", "None", "None", "MD"),
            ("AaCfPaBa_Alt2", "Formation [F][A]-A", "None", "None", "MD"),
            ("AaCfPcBa_Alt1", "Formation [F][C]-A", "None", "None", "MD"),
            ("AaCfPcBa_Alt2", "Formation [F][C]-A", "None", "None", "MD"),
            ("AaCfPcBa_Alt3", "Formation [F][C]-A", "None", "None", "MD"),
            ("AaCfPcBa_Alt4", "Formation [F][C]-A", "None", "None", "MD"),
            ("AaCfPcBa_Alt5", "Formation [F][C]-A", "None", "None", "MD"),
            ("TrainTeamCaptain", "Train Team Captain", "None", "None", "MD"),
            ("VioletUnderstand", "Gloom Understanding", "None", "None", "C2"),
            ("MentalCrack", "Mental Crack", "None", "None", "C2"),
            ("ImpendingCollapse", "Impending Collapse", "None", "None", "C2"),
            ("SinkingWhite", "Butterfly (Sinking)", "None", "None", "Sys"),
            ("BulletLament", "Bullet of Lament", "None", "None", "EGO"),
            ("ReloadLament", "Reload of Lament", "None", "None", "EGO"),
            ("RedEyeFirst", "Red Eye I", "None", "None", "Sys"),
            ("RedEyeSecond", "Red Eye II", "None", "None", "Sys"),
            ("RedEyeThird", "Red Eye III", "None", "None", "Sys"),
            ("PenanceFirst", "Penance I", "None", "None", "Sys"),
            ("PenanceSecond", "Penance II", "None", "None", "Sys"),
            ("PenanceThird", "Penance III", "None", "None", "Sys"),
            ("Emptiness", "Emptiness", "None", "None", "C7"),
            ("UninvitedGuestPersonality", "Uninvited Guest (Personality)", "None", "None", "Sys"),
            ("DevyatDimensionalSack", "Dimensional Bag (Rodion)", "None", "None", "C2"),
            ("Retreat", "Retreat", "None", "None", "C1"),
            ("DefensiveStance", "Defensive Stance", "None", "None", "Sys"),
            ("CanDuelGuard", "Can Guard in Duel", "None", "None", "C3"),
            ("SuperCoin", "Super Coin", "None", "None", "C3"),
            ("DuelDeclaration_Camille", "Duel Declaration - Camille", "None", "None", "C3"),
            ("RecklessDuel", "Reckless Duel", "None", "None", "C3"),
            ("ConcentratedAttack", "Concentrated Attack", "None", "None", "C3"),
            ("BloodScissor", "Blood-Red Scissors", "None", "None", "Boss"),
            ("LineCutting", "Tailoring Target", "None", "None", "Boss"),
            ("BloodScissorScars", "Scissor Scars", "None", "None", "Boss"),
            ("BloodDinner", "Blood Banquet", "None", "None", "C7"),
            ("FamineBlood_LowMorale", "Blood Famine: Low Morale", "None", "None", "Boss"),
            ("FamineBlood_Panic", "Blood Famine: Panic", "None", "None", "Boss"),
            ("Duello_LowMorale", "Duello: Low Morale", "None", "None", "Boss"),
            ("Duello_Panic", "Duello: Panic", "None", "None", "Boss"),
            ("ScissorCutting", "Scissor Cutting", "None", "None", "Boss"),
            ("TrulyWeak", "Truly Weak", "None", "None", "Boss"),
            ("RealPaperBear", "Real Paper Bear", "None", "None", "Boss"),
            ("BloodScissorTwo", "Blood-Red Scissors II", "None", "None", "Boss"),
            ("BloodScissorThree", "Blood-Red Scissors III", "None", "None", "Boss"),
            ("StarvingBarberOne", "Starving Barber I", "None", "None", "Boss"),
            ("StarvingBarberTwo", "Starving Barber II", "None", "None", "Boss"),
            ("ConcentratedAttackMeursault", "Concentrated Attack (Meursault)", "None", "None", "C3"),
            ("BloodDinner_Accumulation", "Blood Banquet (Accumulated)", "None", "None", "C7"),
            ("BloodShooting", "Blood Shooting", "None", "None", "C7"),
            ("RighteousFeeling", "Righteous Feeling", "None", "None", "C7"),
            ("BloomingThorns", "Blooming Thorns", "None", "None", "C7"),
            ("BloomingThorns_2nd", "Blooming Thorns II", "None", "None", "C7"),
            ("BloomingThorns_3rd", "Blooming Thorns III", "None", "None", "C7"),
            ("FestivalFever", "Festival Fever", "None", "None", "C7"),
            ("IncompleteParade", "Incomplete Parade", "None", "None", "C7"),
            ("BloodyHand", "Bloody Hand", "None", "None", "C7"),
            ("FamineBloodDolciServant_LowMorale", "Starving Dolcinea's Servant: Low Morale", "None", "None", "Boss"),
            ("FamineBloodDolciServant_Panic", "Starving Dolcinea's Servant: Panic", "None", "None", "Boss"),
            ("StarvingDolcineaServant", "Starving Dolcinea's Servant", "None", "None", "Boss"),
            ("FamineBloodDolci_LowMorale", "Blood Famine (Dolcinea): Low Morale", "None", "None", "Boss"),
            ("FamineBloodDolci_Panic", "Blood Famine (Dolcinea): Panic", "None", "None", "Boss"),
            ("StarvingDolcineaOne", "Starving Dolcinea I", "None", "None", "Boss"),
            ("StarvingPriestOne", "Senescence I", "None", "None", "Boss"),
            ("HonorableDuel_Don", "Honorable Duel (Don Quixote)", "None", "None", "Boss"),
            ("HonorableDuel_Knight", "Honorable Duel (Knight)", "None", "None", "Boss"),
            ("Snare", "Snare", "None", "None", "C7"),
            ("ThornyFall_LowMorale", "Thorny Fall: Low Morale", "None", "None", "C7"),
            ("ThornyFall_Panic", "Thorny Fall: Panic", "None", "None", "C7"),
            ("LineCuttingPersonality", "Tailoring Target (Personality)", "None", "None", "C7"),
            ("BloodScissorPersonalityFirst", "Blood Scissors I (Personality)", "None", "None", "C7"),
            ("BloodScissorPersonalitySecond", "Blood Scissors II (Personality)", "None", "None", "C7"),
            ("BloodScissorPersonalityThird", "Blood Scissors III (Personality)", "None", "None", "C7"),
            ("UnstoppableFunny", "Unstoppable Comedy", "None", "None", "C7"),
            ("ThornNoose", "Thorn Noose", "None", "None", "C7"),
            ("TinyCarmilla", "Tiny Carmilla", "None", "None", "C7"),
            ("OneDropNutrition", "One Drop Nutrition", "None", "None", "C7"),
            ("BloodringUp_LowMorale", "Blood Ring Up: Low Morale", "None", "None", "C7"),
            ("BloodringUp_Panic", "Blood Ring Up: Panic", "None", "None", "C7"),
            ("Guilt_LowMorale", "Guilt: Low Morale", "None", "None", "C7"),
            ("Guilt_Panic", "Guilt: Panic", "None", "None", "C7"),
            ("WornHeart", "Worn Heart", "None", "None", "C7"),
            ("BloodyHand_2nd", "Bloody Hand II", "None", "None", "C7"),
            ("BloodyHand_3rd", "Bloody Hand III", "None", "None", "C7"),
            ("StarvingPriestTwo", "Senescence II", "None", "None", "Boss"),
            ("MentalIncreaseDown", "SP Recovery Down", "None", "None", "C7"),
            ("AffectionTeddy", "Teddy's Affection", "None", "None", "C2"),
            ("FaintMemory", "Faint Memory", "None", "None", "C2"),
            ("CursePackage", "Curse Package", "None", "None", "C2"),
            ("BloodArmor", "Hardened Blood I", "None", "None", "C7"),
            ("BloodArmor_2nd", "Hardened Blood II", "None", "None", "C7"),
            ("BloodArmor_3rd", "Hardened Blood III", "None", "None", "C7"),
            ("Dreamy", "Dreamy", "None", "None", "C2"),
            ("SwirlingBlood", "Swirling Blood", "None", "None", "C7"),
            ("SanchoMind_LowMorale", "Sancho's Heart: Low Morale", "None", "None", "Boss"),
            ("SanchoMind_Panic", "Sancho's Heart: Panic", "None", "None", "Boss"),
            ("RoseWedge", "Rose Wedge", "None", "None", "C7"),
            ("ThirstyRose", "Thirsty Rose", "None", "None", "C7"),
            ("FerrisWheel", "Ferris Wheel", "None", "None", "Boss"),
            ("StarvingDonqui", "Starving Don Quixote", "None", "None", "Boss"),
            ("RegainedStrength", "Regained Strength", "None", "None", "C7"),
            ("RegainedStrength_2nd", "Regained Strength II", "None", "None", "C7"),
            ("RegainedStrength_3rd", "Regained Strength III", "None", "None", "C7"),
            ("Precarious", "Precarious", "None", "None", "C7"),
            ("Penetration", "Penetration", "None", "None", "Boss"),
            ("WeightOfResponsibility_LowMorale", "Weight of Responsibility: Low Morale", "None", "None", "Boss"),
            ("WeightOfResponsibility_Panic", "Weight of Responsibility: Panic", "None", "None", "Boss"),
            ("LacerationSurge", "Bleed Surge", "None", "None", "C7"),
            ("BloodDinner_Common_Accumulation", "Blood Banquet (Common)", "None", "None", "C7"),
            ("BloodyHandGregFirst", "Bloody Hand I (Greg)", "None", "None", "C1"),
            ("BloodyHandGregSecond", "Bloody Hand II (Greg)", "None", "None", "C1"),
            ("BloodyHandGregThird", "Bloody Hand III (Greg)", "None", "None", "C1"),
            ("WornHeartGreg", "Worn Heart (Greg)", "None", "None", "C1"),
            ("BloomingThornsRodionFirst", "Blooming Thorns I (Rodion)", "None", "None", "C7"),
            ("BloomingThornsRodionSecond", "Blooming Thorns II (Rodion)", "None", "None", "C7"),
            ("BloomingThornsRodionThird", "Blooming Thorns III (Rodion)", "None", "None", "C7"),
            ("FestivalFeverRodion", "Festival Fever (Rodion)", "None", "None", "C7"),
            ("DecreamentalDefense", "Decremental Defense", "None", "None", "C7"),
            ("UnfinishedDream", "Unfinished Dream", "None", "None", "C7"),
            ("UnfinishedDreamTwo", "Unfinished Dream II", "None", "None", "C7"),
            ("FragmentOfHope", "Fragment of Hope", "None", "None", "C7"),
            ("FragmentOfHopeTwo", "Fragment of Hope II", "None", "None", "C7"),
            ("ConfinementOfGoldenBranch", "Confinement of the Golden Bough", "None", "None", "C7"),
            ("DevyatDimensionalSackSinclair", "Dimensional Bag (Sinclair)", "None", "None", "C2"),
            ("VibrationSpring", "Tremor: Spring", "None", "None", "C6"),
            ("MD5Base", "MD5: Base Affliction", "None", "None", "MD"),
            ("MD511", "MD5 Offense I", "None", "None", "MD"),
            ("MD512", "MD5 Offense II", "None", "None", "MD"),
            ("MD513", "MD5 Offense III", "None", "None", "MD"),
            ("MD514", "MD5 Offense IV", "None", "None", "MD"),
            ("MD515", "MD5 Offense V", "None", "None", "MD"),
            ("MD516", "MD5 Offense VI", "None", "None", "MD"),
            ("MD521", "MD5 Defense I", "None", "None", "MD"),
            ("MD522", "MD5 Defense II", "None", "None", "MD"),
            ("MD523", "MD5 Defense III", "None", "None", "MD"),
            ("MD524", "MD5 Defense IV", "None", "None", "MD"),
            ("MD525", "MD5 Defense V", "None", "None", "MD"),
            ("MD526", "MD5 Defense VI", "None", "None", "MD"),
            ("MD527", "MD5 Defense VII", "None", "None", "MD"),
            ("MD531", "MD5 Special I", "None", "None", "MD"),
            ("MD532", "MD5 Special II", "None", "None", "MD"),
            ("MD533", "MD5 Special III", "None", "None", "MD"),
            ("MD534", "MD5 Special IV", "None", "None", "MD"),
            ("MD535", "MD5 Special V", "None", "None", "MD"),
            ("MD536", "MD5 Special VI", "None", "None", "MD"),
            ("MD537", "MD5 Special VII", "None", "None", "MD"),
            ("MD538", "MD5 Special VIII", "None", "None", "MD"),
            ("MD541", "MD5 Affliction I", "None", "None", "MD"),
            ("MD542", "MD5 Affliction II", "None", "None", "MD"),
            ("MD543", "MD5 Affliction III", "None", "None", "MD"),
            ("MD544", "MD5 Affliction IV", "None", "None", "MD"),
            ("MD545", "MD5 Affliction V", "None", "None", "MD"),
            ("MD546", "MD5 Affliction VI", "None", "None", "MD"),
            ("MD547", "MD5 Affliction VII", "None", "None", "MD"),
            ("MD548", "MD5 Affliction VIII", "None", "None", "MD"),
            ("MD549", "MD5 Affliction IX", "None", "None", "MD"),
            ("MD551", "MD5 Floor II-I", "None", "None", "MD"),
            ("MD552", "MD5 Floor II-II", "None", "None", "MD"),
            ("MD553", "MD5 Floor II-III", "None", "None", "MD"),
            ("MD554", "MD5 Floor II-IV", "None", "None", "MD"),
            ("MD555", "MD5 Floor II-V", "None", "None", "MD"),
            ("MD556", "MD5 Floor II-VI", "None", "None", "MD"),
            ("MD557", "MD5 Floor II-VII", "None", "None", "MD"),
            ("MD558", "MD5 Floor II-VIII", "None", "None", "MD"),
            ("MD561", "MD5 Floor III-I", "None", "None", "MD"),
            ("MD562", "MD5 Floor III-II", "None", "None", "MD"),
            ("MD563", "MD5 Floor III-III", "None", "None", "MD"),
            ("MD564", "MD5 Floor III-IV", "None", "None", "MD"),
            ("MD565", "MD5 Floor III-V", "None", "None", "MD"),
            ("MD566", "MD5 Floor III-VI", "None", "None", "MD"),
            ("MD567", "MD5 Floor III-VII", "None", "None", "MD"),
            ("MD568", "MD5 Floor III-VIII", "None", "None", "MD"),
            ("MD571", "MD5 Floor IV-I", "None", "None", "MD"),
            ("MD572", "MD5 Floor IV-II", "None", "None", "MD"),
            ("MD573", "MD5 Floor IV-III", "None", "None", "MD"),
            ("MD574", "MD5 Floor IV-IV", "None", "None", "MD"),
            ("MD575", "MD5 Floor IV-V", "None", "None", "MD"),
            ("MD576", "MD5 Floor IV-VI", "None", "None", "MD"),
            ("MD577", "MD5 Floor IV-VII", "None", "None", "MD"),
            ("MD578", "MD5 Floor IV-VIII", "None", "None", "MD"),
            ("MD581", "MD5 Floor V-I", "None", "None", "MD"),
            ("MD582", "MD5 Floor V-II", "None", "None", "MD"),
            ("MD583", "MD5 Floor V-III", "None", "None", "MD"),
            ("MD584", "MD5 Floor V-IV", "None", "None", "MD"),
            ("MD585", "MD5 Floor V-V", "None", "None", "MD"),
            ("MD586", "MD5 Floor V-VI", "None", "None", "MD"),
            ("MD587", "MD5 Floor V-VII", "None", "None", "MD"),
            ("MD588", "MD5 Floor V-VIII", "None", "None", "MD"),
            ("MD591", "MD5 Floor VI-I", "None", "None", "MD"),
            ("MD592", "MD5 Floor VI-II", "None", "None", "MD"),
            ("MD593", "MD5 Floor VI-III", "None", "None", "MD"),
            ("MD594", "MD5 Floor VI-IV", "None", "None", "MD"),
            ("MD595", "MD5 Floor VI-V", "None", "None", "MD"),
            ("MD596", "MD5 Floor VI-VI", "None", "None", "MD"),
            ("MD597", "MD5 Floor VI-VII", "None", "None", "MD"),
            ("MD598", "MD5 Floor VI-VIII", "None", "None", "MD"),
            ("Zazen", "Zazen", "None", "None", "C3"),
            ("SwirlingBloodPersonality", "Swirling Blood (Personality)", "None", "None", "C7"),
            ("BloodArmorPersonalityFirst", "Meridian Armor I (Personality)", "None", "None", "C7"),
            ("BloodArmorPersonalitySecond", "Meridian Armor II (Personality)", "None", "None", "C7"),
            ("BloodArmorPersonalityThird", "Meridian Armor III (Personality)", "None", "None", "C7"),
            ("RighteousFeelingSancho", "Righteous Feeling (Sancho)", "None", "None", "Boss"),
            ("UnfinishedDreamSancho", "Unfinished Dream (Sancho)", "None", "None", "Boss"),
            ("FragmentOfHopeSancho", "Fragment of Hope (Sancho)", "None", "None", "Boss"),
            ("FragmentOfHopeTwoSancho", "Fragment of Hope II (Sancho)", "None", "None", "Boss"),
            ("SadLamanchaland", "Weight of Responsibility", "None", "None", "C7"),
            ("FreishutzOutisEgoBullet_1st", "Magic Bullet I", "None", "None", "EGO"),
            ("FreishutzOutisEgoBullet_2nd", "Magic Bullet II", "None", "None", "EGO"),
            ("FreishutzOutisEgoBullet_3rd", "Magic Bullet III", "None", "None", "EGO"),
            ("FreishutzOutisEgoBullet_4th", "Magic Bullet IV", "None", "None", "EGO"),
            ("FreishutzOutisEgoBullet_5th", "Magic Bullet V", "None", "None", "EGO"),
            ("FreishutzOutisEgoBullet_6th", "Magic Bullet VI", "None", "None", "EGO"),
            ("FreishutzOutisEgoBullet_7th", "Magic Bullet VII", "None", "None", "EGO"),
            ("FreishutzOutisEgoBulletCnt", "Magic Bullet Count", "None", "None", "EGO"),
            ("Smoke", "Smoke", "None", "None", "C1"),
            ("Church_LowMorale", "Church: Low Morale", "None", "None", "C1"),
            ("Church_Panic", "Church: Panic", "None", "None", "C1"),
            ("MeatGearForce", "Meat Gear Force", "None", "None", "C1"),
            ("AllSetForShooting", "All Set for Shooting", "None", "None", "C1"),
            ("CoveringFire", "Covering Fire", "None", "None", "C1"),
            ("FullReload", "Full Reload", "None", "None", "C1"),
            ("RequestedTarget", "Requested Target", "None", "None", "C1"),
            ("Bullet_LogicAtelier", "Logic Atelier Ammo", "None", "None", "EGO"),
            ("LogicAtelierAM", "Logic Atelier AM", "None", "None", "EGO"),
            ("MeleeCover", "Melee Cover", "None", "None", "EGO"),
            ("Retreat_FullStop", "Full Stop Retreat", "None", "None", "C1"),
            ("ObservedPerson", "Observed Person", "None", "None", "C1"),
            ("Hohenheim_LowMorale", "Hohenheim: Low Morale", "None", "None", "C1"),
            ("Hohenheim_Panic", "Hohenheim: Panic", "None", "None", "C1"),
            ("Bloodthirst", "Bloodthirst", "None", "None", "C1"),
            ("GlowingLantern", "Glowing Lantern", "None", "None", "C1"),
            ("A1c971a", "A1C9: Phase A", "None", "None", "C1"),
            ("A1c971b", "A1C9: Phase B", "None", "None", "C1"),
            ("A1c971c", "A1C9: Phase C", "None", "None", "C1"),
            ("A1c971d", "A1C9: Phase D", "None", "None", "C1"),
            ("A1c971e", "A1C9: Phase E", "None", "None", "C1"),
            ("HoldingBreath", "Holding Breath", "None", "None", "C1"),
            ("NebulizerInhale", "Nebulizer: Inhale", "None", "None", "C1"),
            ("A1c971f", "A1C9: Phase F", "None", "None", "C1"),
            ("BlackCloud", "Black Cloud", "None", "None", "C1"),
            ("BlackCloudBlade", "Black Cloud Blade", "None", "None", "C1"),
            ("EngageToBattle", "Engage to Battle", "None", "None", "C1"),
            ("CloudWall", "Cloud Wall", "None", "None", "C1"),
            ("FellBulletMark", "Fell Bullet Mark", "None", "None", "EGO"),
            ("FellBulletGroggy", "Fell Bullet: Groggy", "None", "None", "EGO"),
            ("NebulizerExhale", "Nebulizer: Exhale", "None", "None", "C1"),
            ("ReleaseBreath", "Release Breath", "None", "None", "C1"),
            ("DianxueDonQuixote", "Dianxue (Don Quixote)", "None", "None", "EGO"),
            ("FirePunchFuel", "Fire Punch Fuel", "None", "None", "EGO"),
            ("FirePunchFuelOverheated", "Fire Punch Fuel: Overheated", "None", "None", "EGO"),
            ("CandyForCharon", "Candy for Charon", "None", "None", "EGO"),
            ("TickTockTickTock", "Tick-Tock", "None", "None", "EGO"),
            ("CandyForCharon_LowMorale", "Candy: Low Morale", "None", "None", "EGO"),
            ("CandyForCharon_Panic", "Candy: Panic", "None", "None", "EGO"),
            ("MiddleFingerBook", "Middle Finger Book", "None", "None", "EGO"),
            ("DarkBeast_LowMorale", "Dark Beast: Low Morale", "None", "None", "C5"),
            ("DarkBeast_Panic", "Dark Beast: Panic", "None", "None", "C5"),
            ("BurstAgility", "Rupture: Haste", "None", "None", "EGO"),
            ("RepressedMurderousIntend", "Repressed Killing Intent", "None", "None", "EGO"),
            ("WeighedMurderousIntend", "Measured Killing Intent", "None", "None", "EGO"),
            ("LegStrength", "Leg Strength", "None", "None", "C8"),
            ("Persistent", "Persistent", "None", "None", "C8"),
            ("BackstreetsNight_LowMorale", "Backstreets Night: Low Morale", "None", "None", "C5"),
            ("BackstreetsNight_Panic", "Backstreets Night: Panic", "None", "None", "C5"),
            ("SweeperA", "Sweeper A", "None", "None", "EGO"),
            ("SweeperB", "Sweeper B", "None", "None", "EGO"),
            ("SweeperC", "Sweeper C", "None", "None", "EGO"),
            ("VengeanceBookSinclair", "Vengeance Book (Sinclair)", "None", "None", "EGO"),
            ("VendettaMark", "Vendetta Mark", "None", "None", "EGO"),
            ("RetaliationBookFamily", "Retaliation (Family)", "None", "None", "EGO"),
            ("BirdCage", "Bird Cage", "None", "None", "EGO"),
            ("WideAreaRampage", "Wide Area Rampage", "None", "None", "EGO"),
            ("ActivatedEgoPassive", "E.G.O Passive Activated", "None", "None", "EGO"),
            ("TheDrifter_LowMorale", "Drifter: Low Morale", "None", "None", "C5"),
            ("TheDrifter_Panic", "Drifter: Panic", "None", "None", "C5"),
            ("SipOfAlcohol", "Sip of Alcohol", "None", "None", "EGO"),
            ("BurstPoison", "Rupture Poison", "None", "None", "EGO"),
            ("SnakeStance", "Snake Stance", "None", "None", "EGO"),
            ("EntangledCurseTalisman", "Entangled Curse Talisman", "None", "None", "EGO"),
            ("CentipedePoison", "Centipede Poison", "None", "None", "EGO"),
            ("WitheredWood_LowMorale", "Withered Wood: Low Morale", "None", "None", "C5"),
            ("WitheredWood_Panic", "Withered Wood: Panic", "None", "None", "C5"),
            ("EvilHeart_LowMorale", "Evil Heart: Low Morale", "None", "None", "C5"),
            ("EvilHeart_Panic", "Evil Heart: Panic", "None", "None", "C5"),
            ("DeepEvilHeart_LowMorale", "Deep Evil Heart: Low Morale", "None", "None", "C5"),
            ("DeepEvilHeart_Panic", "Deep Evil Heart: Panic", "None", "None", "C5"),
            ("OminousTalisman", "Ominous Talisman", "None", "None", "EGO"),
            ("ProtectStance", "Protect Stance", "None", "None", "EGO"),
            ("TakeBreath_LowMorale", "Take Breath: Low Morale", "None", "None", "C5"),
            ("TakeBreath_Panic", "Take Breath: Panic", "None", "None", "C5"),
            ("RepressedMurderousIntendTwo", "Repressed Killing Intent II", "None", "None", "EGO"),
            ("BurstWeakness", "Rupture Weakness", "None", "None", "EGO"),
            ("ZiluDebuff", "Zilu Debuff", "None", "None", "EGO"),
            ("UncontrolledChargeAxe_LowMorale", "Uncontrolled Axe: Low Morale", "None", "None", "C8"),
            ("UncontrolledChargeAxe_Panic", "Uncontrolled Axe: Panic", "None", "None", "C8"),
            ("Qiu_LowMorale", "Qiu: Low Morale", "None", "None", "C5"),
            ("Qiu_Panic", "Qiu: Panic", "None", "None", "C5"),
            ("ExaminationOfQiu", "Examination of Qiu", "None", "None", "EGO"),
            ("BurstStop", "Rupture Stop", "None", "None", "EGO"),
            ("BurstSuppress", "Rupture Suppression", "None", "None", "EGO"),
            ("BurstWave", "Rupture Wave", "None", "None", "EGO"),
            ("Teaching", "Teaching", "None", "None", "EGO"),
            ("JarMaster_LowMorale", "Jar Master: Low Morale", "None", "None", "C5"),
            ("JarMaster_Panic", "Jar Master: Panic", "None", "None", "C5"),
            ("BurstZilu", "Rupture (Zilu)", "None", "None", "EGO"),
            ("SupportProtect", "Support Protect", "None", "None", "EGO"),
            ("SupportProtectTypo", "Support Protect (Legacy)", "None", "None", "EGO"),
            ("EnhanceZilu", "Enhance (Zilu)", "None", "None", "EGO"),
            ("BurstVulnerableZilu", "Rupture Vulnerable (Zilu)", "None", "None", "EGO"),
            ("Irritation", "Tiantu Star (General)", "None", "None", "Shin"),
            ("HugeIrritation", "Shin: Tiantu Star", "None", "None", "Shin"),
            ("Obedient", "Obedience", "None", "None", "C7"),
            ("FamilyTreasure", "Explosive Amusement", "None", "None", "C8"),
            ("OutpouringAnger", "Outpouring Anger", "None", "None", "C8"),
            ("Honglu_Xi", "Joy (Xi)", "None", "None", "C8"),
            ("Honglu_Le", "Pleasure (Le)", "None", "None", "C8"),
            ("Honglu_Ai", "Sorrow (Ai)", "None", "None", "C8"),
            ("Honglu_Nu", "Anger (Nu)", "None", "None", "C8"),
            ("SupremeEternalLife", "Supreme Eternal Life", "None", "None", "C8"),
            ("ImperfectEternalLife", "Imperfect Eternal Life", "None", "None", "C8"),
            ("FaintEternalLife", "Faint Eternal Life", "None", "None", "C8"),
            ("DisengageCombat", "Disengage Combat", "None", "None", "C8"),
            ("ReloadKeepAmmo", "Reload & Keep Ammo", "None", "None", "C8"),
            ("MadFeather", "Mad Feather", "None", "None", "EGO"),
            ("Chesed_Mercy", "Chesed's Mercy", "None", "None", "EGO"),
            ("Ryoshu_Attackup", "Ryoshu: Attack Up", "None", "None", "EGO"),
            ("Honglu_EGOResourceup", "Hong Lu: EGO Resource Up", "None", "None", "EGO"),
            ("WaveSinking", "Wave Sinking", "None", "None", "C4"),
            ("HystericGauge", "Hysteric Gauge", "None", "None", "EGO"),
            ("MagicalGirlAppear", "Magical Girl Appears!", "None", "None", "EGO"),
            ("NoVillain", "No Villain", "None", "None", "EGO"),
            ("VillainMark", "Villain Mark", "None", "None", "EGO"),
            ("ArcanaQueenOfHate", "Magical Arcana", "None", "None", "EGO"),
            ("ThePowerOfLoveAndHate", "Power of Love and Hate", "None", "None", "EGO"),
            ("ColdBlackTear", "Cold Black Tear", "None", "None", "EGO"),
            ("HelplessTear", "Helpless Tear", "None", "None", "EGO"),
            ("WornOutKnight", "Worn-Out Knight", "None", "None", "EGO"),
            ("SignOfDespair", "Sign of Despair", "None", "None", "EGO"),
            ("CollapsedPride", "Collapsed Pride", "None", "None", "EGO"),
            ("PowerOfLoveAndJustice", "Power of Love and Justice", "None", "None", "EGO"),
            ("BloodArmorMeursault", "Meridian Armor (Meursault)", "None", "None", "C6"),
            ("FocusOnActing", "Performance Focus", "None", "None", "C6"),
            ("ParadeConcentration", "La Mancha Parade", "None", "None", "C6"),
            ("HanafudaOne", "Hanafuda: Pine-Crane", "None", "None", "EGO"),
            ("HanafudaTwo", "Hanafuda: Pampas", "None", "None", "EGO"),
            ("HanafudaThree", "Hanafuda: Blue Cherry", "None", "None", "EGO"),
            ("HanafudaCombo", "Hikari (Light)", "None", "None", "EGO"),
            ("VibrationIgnition", "Tremor Ignition", "None", "None", "C6"),
            ("BulletPropellant", "Bullet Propellant", "None", "None", "EGO"),
            ("BulletSpent", "Spent Ammo", "None", "None", "Sys"),
            ("Prey", "Prey", "None", "None", "C1"),
            ("Complacency", "Complacency", "None", "None", "C1"),
            ("BattleSense", "Battle Sense", "None", "None", "C1"),
            ("RestoredBattleSense", "Restored Battle Sense", "None", "None", "C1"),
            ("ResolveRyoshu", "Resolve (Ryoshu)", "None", "None", "C6"),
            ("CutoffRyoshu", "Cutoff (Ryoshu)", "None", "None", "C6"),
            ("CutbondRyoshu", "Cut Bond (Ryoshu)", "None", "None", "C6"),
            ("DukkhaRyoshu_LowMorale", "Dukkha (Ryoshu): Low Morale", "None", "None", "C6"),
            ("DukkhaRyoshu_Panic", "Dukkha (Ryoshu): Panic", "None", "None", "C6"),
            ("CriticalDamageUp", "Critical Damage Up", "None", "None", "C4"),
            ("BlackNightmare", "Black Nightmare", "None", "None", "C4"),
            ("DeliciousSauce", "Delicious Sauce", "None", "None", "Sys"),
            // ── 누락 복원 (DarkBeastSnake ~ EagleClawWoundAlly) ──────────
            ("DarkBeastSnake_LowMorale", "Dark Beast Snake: Low Morale", "None", "None", "C5"),
            ("DarkBeastSnake_Panic", "Dark Beast Snake: Panic", "None", "None", "C5"),
            ("NEgoFleshSpatula_LowMorale", "N EGO Flesh Spatula: Low Morale", "None", "None", "C5"),
            ("NEgoFleshSpatula_Panic", "N EGO Flesh Spatula: Panic", "None", "None", "C5"),
            ("EgoErodeContempt_LowMorale", "EGO Erosion Contempt: Low Morale", "None", "None", "EGO"),
            ("EgoErodeContempt_Panic", "EGO Erosion Contempt: Panic", "None", "None", "EGO"),
            ("GazeReplica", "Gaze Replica", "None", "None", "EGO"),
            ("ContemptReplica", "Contempt Replica", "None", "None", "EGO"),
            ("GraspReplica", "Grasp Replica", "None", "None", "EGO"),
            ("EgoErodeMemory_LowMorale", "EGO Erosion Memory: Low Morale", "None", "None", "EGO"),
            ("EgoErodeMemory_Panic", "EGO Erosion Memory: Panic", "None", "None", "EGO"),
            ("EgoErodeReplica", "EGO Erosion Replica", "None", "None", "EGO"),
            ("BoseProjektilReplica", "Torn Memories Replica", "None", "None", "EGO"),
            ("LeRegole_LowMorale", "Le Regole: Low Morale", "None", "None", "C5"),
            ("LeRegole_Panic", "Le Regole: Panic", "None", "None", "C5"),
            ("BudgetBulletPropellant", "Budget Bullet Propellant", "None", "None", "EGO"),
            ("YisangParryGubo", "Yi Sang Parry (Gubo)", "None", "None", "C5"),
            ("HongluParryGahwan", "Hong Lu Parry (Gahwan)", "None", "None", "C5"),
            ("GahwanEgoContempt", "Gahwan EGO Contempt", "None", "None", "C5"),
            ("GuboEgoShooter", "Gubo EGO Shooter", "None", "None", "C5"),
            ("FellBulletMarkReplica", "Fell Bullet Mark Replica", "None", "None", "EGO"),
            ("DragonLance", "Dragon Lance", "None", "None", "EGO"),
            ("CondensedBlood", "Condensed Blood", "None", "None", "EGO"),
            ("YesdragonSelf", "Yedragon: Self", "None", "None", "EGO"),
            ("YesdragonNodie", "Yedragon: No Die", "None", "None", "EGO"),
            ("YesdragonBurst", "Yedragon: Rupture", "None", "None", "EGO"),
            ("QiuAndHonglu", "Qiu and Hong Lu", "None", "None", "EGO"),
            ("BeastEyes_LowMorale", "Beast Eyes: Low Morale", "None", "None", "C5"),
            ("BeastEyes_Panic", "Beast Eyes: Panic", "None", "None", "C5"),
            ("BulletPropellantSpecial", "Bullet Propellant (Special)", "None", "None", "EGO"),
            ("LastingHongwonWill_LowMorale", "Hongwon's Will: Low Morale", "None", "None", "Boss"),
            ("LastingHongwonWill_Panic", "Hongwon's Will: Panic", "None", "None", "Boss"),
            ("PileObedient", "Piled Obedience", "None", "None", "C7"),
            ("ExpensiveJade", "Precious Jade", "None", "None", "C7"),
            ("LongLastingHongwonWill_LowMorale", "Long-lasting Hongwon's Will: Low Morale", "None", "None", "Boss"),
            ("LongLastingHongwonWill_Panic", "Long-lasting Hongwon's Will: Panic", "None", "None", "Boss"),
            ("GoldenBoughSync", "Golden Bough Sync", "None", "None", "Boss"),
            ("GoldenBoughSyncDistorted", "Golden Bough Sync (Distorted)", "None", "None", "Boss"),
            ("MadFeather_LowMorale", "Mad Feather: Low Morale", "None", "None", "EGO"),
            ("MadFeather_Panic", "Mad Feather: Panic", "None", "None", "EGO"),
            ("DeepAngry_LowMorale", "Deep Anger: Low Morale", "None", "None", "EGO"),
            ("DeepAngry_Panic", "Deep Anger: Panic", "None", "None", "EGO"),
            ("WaitingXichun", "Waiting (Xichun)", "None", "None", "C8"),
            ("StartXichun", "Start (Xichun)", "None", "None", "C8"),
            ("CheerUpXichun", "Cheer Up (Xichun)", "None", "None", "C8"),
            ("RetreatForCommon", "Retreat (Common)", "None", "None", "C1"),
            ("BulletPropellantAlly", "Bullet Propellant (Ally)", "None", "None", "EGO"),
            ("BulletPropellantSpecialAlly", "Bullet Propellant Special (Ally)", "None", "None", "EGO"),
            ("IrritationAlly", "Tiantu Star (Ally)", "None", "None", "Shin"),
            ("HugeIrritationAlly", "Shin: Tiantu Star (Ally)", "None", "None", "Shin"),
            ("FireBulletPropellant", "Fire Bullet Propellant", "None", "None", "EGO"),
            ("SingBulletSupport", "Bullet Support", "None", "None", "EGO"),
            ("BeastEyesAlly", "Beast Eyes (Ally)", "None", "None", "C8"),
            ("FellBulletPersonality", "Fell Bullet (Personality)", "None", "None", "EGO"),
            ("Honglu_Xi_Mirror", "Joy Mirror", "None", "None", "C8"),
            ("Honglu_Le_Mirror", "Pleasure Mirror", "None", "None", "C8"),
            ("Honglu_Ai_Mirror", "Sorrow Mirror", "None", "None", "C8"),
            ("Honglu_Nu_Mirror", "Anger Mirror", "None", "None", "C8"),
            ("RoseThorn", "Rose Thorn", "None", "None", "EGO"),
            ("ChoSuperCharge", "Super Charge (Cho)", "None", "None", "EGO"),
            ("ShareCharge", "Share Charge", "None", "None", "EGO"),
            ("FailedToAssistQueen", "Failed to Assist the Queen", "None", "None", "EGO"),
            ("UsedTooMuchPower", "Used Too Much Power", "None", "None", "EGO"),
            ("ChasingArcana", "Arcana Slave", "None", "None", "EGO"),
            ("CentralCommandTeamCaptain", "Central Command Captain", "None", "None", "MD"),
            ("BestWelfareTeamMember", "Best Welfare Member", "None", "None", "MD"),
            ("BlessingAlly", "Blessing (Ally)", "None", "None", "EGO"),
            ("ProtectiveSword", "Protective Sword", "None", "None", "EGO"),
            ("DespairAlly", "Despair (Ally)", "None", "None", "EGO"),
            ("PenetratingSword", "Penetrating Sword", "None", "None", "EGO"),
            ("SwordCutwithTear", "Sword Cut with Tear", "None", "None", "EGO"),
            ("MagicalGirlResponse", "Magical Girl Response", "None", "None", "EGO"),
            ("KnightBless", "Knight Bless", "None", "None", "EGO"),
            ("TraumaShield", "Trauma Shield", "None", "None", "EGO"),
            ("BlackTearsAlly", "Black Tears (Ally)", "None", "None", "EGO"),
            ("ChasingArcanahard", "Arcana Slave (Hard)", "None", "None", "EGO"),
            ("UltraPrecisionTimeAcceleration", "Ultra Precision Time Acceleration", "None", "None", "C2"),
            ("OutisNoHat", "Outis (No Hat)", "None", "None", "C8"),
            ("AccumulatedPastSinner", "Accumulated Past (Sinner)", "None", "None", "C8"),
            ("HeishouDeathCount", "Heishou: Death Count", "None", "None", "Boss"),
            ("HeishouCombo", "Heishou: Combo", "None", "None", "Boss"),
            ("HeishouComboCount", "Heishou: Combo Count", "None", "None", "Boss"),
            ("HeishouAttack", "Heishou: Attack", "None", "None", "Boss"),
            ("HeishouSupportProtect", "Heishou: Support Protect", "None", "None", "Boss"),
            ("HeishouSupportProtectTypo", "Heishou: Support Protect (Legacy)", "None", "None", "Boss"),
            ("BurstHonglu", "Rupture (Hong Lu)", "None", "None", "Boss"),
            ("HeishouSynergy", "Heishou: Synergy", "None", "None", "Boss"),
            ("HeishouComboCountHonglu", "Heishou: Hong Lu Combo", "None", "None", "Boss"),
            ("DarkHongluQiuAndHonglu", "Dark Hong Lu: Qiu and Hong Lu", "None", "None", "Boss"),
            ("DarkHongluTeaching", "Dark Hong Lu: Teaching", "None", "None", "Boss"),
            ("DarkHongluParryGahwan", "Dark Hong Lu: Parry Gahwan", "None", "None", "Boss"),
            ("DarkHonglu_EGOResourceup", "Dark Hong Lu: EGO Resource Up", "None", "None", "Boss"),
            ("DarkHonglu_Xi", "Dark Hong Lu: Joy", "None", "None", "Boss"),
            ("DarkHonglu_Le", "Dark Hong Lu: Pleasure", "None", "None", "Boss"),
            ("DarkHonglu_Ai", "Dark Hong Lu: Sorrow", "None", "None", "Boss"),
            ("DarkHonglu_Nu", "Dark Hong Lu: Anger", "None", "None", "Boss"),
            ("MD6Test101", "MD6 Test: 101", "None", "None", "MD"),
            ("MD6Test102", "MD6 Test: 102", "None", "None", "MD"),
            ("MD6Test103", "MD6 Test: 103", "None", "None", "MD"),
            ("MD6Test104", "MD6 Test: 104", "None", "None", "MD"),
            ("MD6Test105", "MD6 Test: 105", "None", "None", "MD"),
            ("MD6Test106", "MD6 Test: 106", "None", "None", "MD"),
            ("MD6Test107", "MD6 Test: 107", "None", "None", "MD"),
            ("MD6Test108", "MD6 Test: 108", "None", "None", "MD"),
            ("MD6Test111", "MD6 Test: 111", "None", "None", "MD"),
            ("MD6Test112", "MD6 Test: 112", "None", "None", "MD"),
            ("MD6Test113", "MD6 Test: 113", "None", "None", "MD"),
            ("MD6Test114", "MD6 Test: 114", "None", "None", "MD"),
            ("MD6Test115", "MD6 Test: 115", "None", "None", "MD"),
            ("MD6Test116", "MD6 Test: 116", "None", "None", "MD"),
            ("MD6Test117", "MD6 Test: 117", "None", "None", "MD"),
            ("MD6Test118", "MD6 Test: 118", "None", "None", "MD"),
            ("MD6LimitTest101", "MD6 Limit: Test101", "None", "None", "MD"),
            ("MD6LimitTest102", "MD6 Limit: Test102", "None", "None", "MD"),
            ("MD6LimitTest103", "MD6 Limit: Test103", "None", "None", "MD"),
            ("MD6LimitTest104", "MD6 Limit: Test104", "None", "None", "MD"),
            ("MD6LimitTest105", "MD6 Limit: Test105", "None", "None", "MD"),
            ("MD6LimitTest111", "MD6 Limit: Test111", "None", "None", "MD"),
            ("MD6LimitTest112", "MD6 Limit: Test112", "None", "None", "MD"),
            ("MD6LimitTest113", "MD6 Limit: Test113", "None", "None", "MD"),
            ("MD6LimitTest114", "MD6 Limit: Test114", "None", "None", "MD"),
            ("MD6LimitTest115", "MD6 Limit: Test115", "None", "None", "MD"),
            ("ChickenStance", "Chicken Stance", "None", "None", "Boss"),
            ("LegStrengthHorseYisang", "Leg Strength (Yi Sang's Horse)", "None", "None", "Boss"),
            ("ConcussionYisang", "Concussion (Yi Sang)", "None", "None", "Boss"),
            ("BreakthroughHorse", "Breakthrough Horse", "None", "None", "Boss"),
            ("BattlefieldHorse", "Battlefield Horse", "None", "None", "Boss"),
            ("LegStrengthHorse", "Horse Leg Strength", "None", "None", "Boss"),
            ("ConcussionWei", "Concussion (Wei)", "None", "None", "Boss"),
            ("SweptAwayWei", "Swept Away (Wei)", "None", "None", "Boss"),
            ("ReboundWei", "Rebound (Wei)", "None", "None", "Boss"),
            ("RunawayHorseWei_LowMorale", "Runaway Horse: Low Morale", "None", "None", "Boss"),
            ("RunawayHorseWei_Panic", "Runaway Horse: Panic", "None", "None", "Boss"),
            ("StudyStatSinclair_A", "Struggle to Hatch I", "None", "None", "C3"),
            ("StudyStatSinclair_B", "Struggle to Hatch II", "None", "None", "C3"),
            ("StudyStatSinclair", "Struggle to Hatch", "None", "None", "C3"),
            ("StudyStatSinclair_D", "Struggle to Hatch IV", "None", "None", "C3"),
            ("StudyStatSinclair_E", "Struggle to Hatch V", "None", "None", "C3"),
            ("StudySignSinclair", "Sign of Awakening", "None", "None", "C3"),
            ("SipOfAlcoholJin", "Jin's Sip of Alcohol", "None", "None", "EGO"),
            ("DrunkBlood", "Drunk Blood", "None", "None", "C3"),
            ("DrunkAwakening", "Drunken Awakening", "None", "None", "C3"),
            ("DrunkDrifter_LowMorale", "Drunk Drifter: Low Morale", "None", "None", "C3"),
            ("DrunkDrifter_Panic", "Drunk Drifter: Panic", "None", "None", "C3"),
            ("NightDrifterGuilty", "Night Drifter Guilty", "None", "None", "C3"),
            ("CrazyWei", "Crazy Wei", "None", "None", "Boss"),
            ("SnakePoisonJin", "Snake Poison (Jin)", "None", "None", "EGO"),
            ("SnakePoisonJinWeak", "Snake Poison (Jin, Weak)", "None", "None", "EGO"),
            ("NervousImpairment", "Nervous Impairment", "None", "None", "C3"),
            ("MlynarWaiting", "Mlynar: Waiting", "None", "None", "C5"),
            ("MlynarFury", "Mlynar: Fury", "None", "None", "C5"),
            ("CuredFilm", "Cured Film", "None", "None", "C5"),
            ("GazeRyoshu", "Gaze (Ryoshu)", "None", "None", "C6"),
            ("ContemptRyoshu", "Contempt (Ryoshu)", "None", "None", "C6"),
            ("VibrationBleeding", "Tremor: Hemorrhage", "None", "None", "C6"),
            ("NervousImpairmentEffect", "Nervous Impairment (Effect)", "None", "None", "C3"),
            ("UnfinishedDreamMirror", "Unfinished Dream (Mirror)", "None", "None", "Boss"),
            ("UnfinishedDreamTwoMirror", "Unfinished Dream II (Mirror)", "None", "None", "Boss"),
            ("FragmentOfHopeFamilyMirror", "Fragment of Hope (Mirror)", "None", "None", "Boss"),
            ("FragmentOfHopeTwoFamilyMirror", "Fragment of Hope II (Mirror)", "None", "None", "Boss"),
            ("DerivativeHugeIrritation", "Shin: Tiantu Star (Derived)", "None", "None", "Shin"),
            ("MD6101", "MD: 6101", "None", "None", "MD"),
            ("MD6102", "MD: 6102", "None", "None", "MD"),
            ("MD6103", "MD: 6103", "None", "None", "MD"),
            ("MD6104", "MD: 6104", "None", "None", "MD"),
            ("MD6105", "MD: 6105", "None", "None", "MD"),
            ("MD6106", "MD: 6106", "None", "None", "MD"),
            ("MD6107", "MD: 6107", "None", "None", "MD"),
            ("MD6111", "MD: 6111", "None", "None", "MD"),
            ("MD6112", "MD: 6112", "None", "None", "MD"),
            ("MD6113", "MD: 6113", "None", "None", "MD"),
            ("MD6114", "MD: 6114", "None", "None", "MD"),
            ("MD6115", "MD: 6115", "None", "None", "MD"),
            ("MD6116", "MD: 6116", "None", "None", "MD"),
            ("MD6117", "MD: 6117", "None", "None", "MD"),
            ("MD6121", "MD: 6121", "None", "None", "MD"),
            ("MD6122", "MD: 6122", "None", "None", "MD"),
            ("MD6123", "MD: 6123", "None", "None", "MD"),
            ("MD6124", "MD: 6124", "None", "None", "MD"),
            ("MD6125", "MD: 6125", "None", "None", "MD"),
            ("MD6126", "MD: 6126", "None", "None", "MD"),
            ("MD6127", "MD: 6127", "None", "None", "MD"),
            ("MD6131", "MD: 6131", "None", "None", "MD"),
            ("MD6132", "MD: 6132", "None", "None", "MD"),
            ("MD6133", "MD: 6133", "None", "None", "MD"),
            ("MD6134", "MD: 6134", "None", "None", "MD"),
            ("MD6135", "MD: 6135", "None", "None", "MD"),
            ("MD6136", "MD: 6136", "None", "None", "MD"),
            ("MD6137", "MD: 6137", "None", "None", "MD"),
            ("MD6141", "MD: 6141", "None", "None", "MD"),
            ("MD6142", "MD: 6142", "None", "None", "MD"),
            ("MD6143", "MD: 6143", "None", "None", "MD"),
            ("MD6144", "MD: 6144", "None", "None", "MD"),
            ("MD6145", "MD: 6145", "None", "None", "MD"),
            ("MD6146", "MD: 6146", "None", "None", "MD"),
            ("MD6147", "MD: 6147", "None", "None", "MD"),
            ("MD6LimitBaseN", "MD6 Limit: BaseN", "None", "None", "MD"),
            ("MD6Limit101", "MD6 Limit: 101", "None", "None", "MD"),
            ("MD6Limit102", "MD6 Limit: 102", "None", "None", "MD"),
            ("MD6Limit103", "MD6 Limit: 103", "None", "None", "MD"),
            ("MD6Limit104", "MD6 Limit: 104", "None", "None", "MD"),
            ("MD6Limit105", "MD6 Limit: 105", "None", "None", "MD"),
            ("MD6Limit111", "MD6 Limit: 111", "None", "None", "MD"),
            ("MD6Limit112", "MD6 Limit: 112", "None", "None", "MD"),
            ("MD6Limit113", "MD6 Limit: 113", "None", "None", "MD"),
            ("MD6Limit114", "MD6 Limit: 114", "None", "None", "MD"),
            ("MD6Limit115", "MD6 Limit: 115", "None", "None", "MD"),
            ("MD6Limit121", "MD6 Limit: 121", "None", "None", "MD"),
            ("MD6Limit122", "MD6 Limit: 122", "None", "None", "MD"),
            ("MD6Limit123", "MD6 Limit: 123", "None", "None", "MD"),
            ("MD6Limit124", "MD6 Limit: 124", "None", "None", "MD"),
            ("MD6Limit125", "MD6 Limit: 125", "None", "None", "MD"),
            ("MD6Limit131", "MD6 Limit: 131", "None", "None", "MD"),
            ("MD6Limit132", "MD6 Limit: 132", "None", "None", "MD"),
            ("MD6Limit133", "MD6 Limit: 133", "None", "None", "MD"),
            ("MD6Limit134", "MD6 Limit: 134", "None", "None", "MD"),
            ("MD6Limit135", "MD6 Limit: 135", "None", "None", "MD"),
            ("MD6Limit141", "MD6 Limit: 141", "None", "None", "MD"),
            ("MD6Limit142", "MD6 Limit: 142", "None", "None", "MD"),
            ("MD6Limit143", "MD6 Limit: 143", "None", "None", "MD"),
            ("MD6Limit144", "MD6 Limit: 144", "None", "None", "MD"),
            ("MD6Limit145", "MD6 Limit: 145", "None", "None", "MD"),
            ("GiveMeCandy", "Give Me Candy", "None", "None", "EGO"),
            ("HungryCharon", "Charon: Hungry", "None", "None", "EGO"),
            ("FullCharon", "Charon: Full", "None", "None", "EGO"),
            ("GiveMeCandy_LowMorale", "Give Me Candy: Low Morale", "None", "None", "EGO"),
            ("GiveMeCandy_Panic", "Give Me Candy: Panic", "None", "None", "EGO"),
            ("VerEmergencyCandy", "Emergency Candy", "None", "None", "EGO"),
            ("VerHunger", "Charon: Hunger", "None", "None", "EGO"),
            ("VerShiningFullness", "Shining Fullness", "None", "None", "EGO"),
            ("DampSwordCase", "Damp Sword Case", "None", "None", "EGO"),
            ("BrokenSwordCase", "Broken Sword Case", "None", "None", "EGO"),
            ("Cianjing", "Qianjing", "None", "None", "C5"),
            ("SeniorCianjing", "Senior Qianjing", "None", "None", "C5"),
            ("SeaTerrorCell", "Sea Terror Cell", "None", "None", "Boss"),
            ("NetherseaBrand", "Nethersea Brand", "None", "None", "Boss"),
            ("CallOfSea_LowMorale", "Call of the Sea: Low Morale", "None", "None", "Boss"),
            ("CallOfSea_Panic", "Call of the Sea: Panic", "None", "None", "Boss"),
            ("WaitingforCommandMod", "Awaiting Command (Mod)", "None", "None", "Boss"),
            ("ParticipationMod", "Participation (Mod)", "None", "None", "Boss"),
            ("AbyssalVitality", "Abyssal Vitality", "None", "None", "Boss"),
            ("IndelibleGoodwill", "Indelible Goodwill", "None", "None", "Boss"),
            ("DesireToSave_LowMorale", "Desire to Save: Low Morale", "None", "None", "C5"),
            ("DesireToSave_Panic", "Desire to Save: Panic", "None", "None", "C5"),
            ("DOQ_LowMorale", "DOQ: Low Morale", "None", "None", "Boss"),
            ("DOQ_Panic", "DOQ: Panic", "None", "None", "Boss"),
            ("ColdAirDOQ", "Cold Air", "None", "None", "Boss"),
            ("FreezingDOQ", "Freezing", "None", "None", "Boss"),
            ("BloodborneDOQ", "Bloodborne Knight", "None", "None", "Boss"),
            ("WeakenDOQ", "Trials and Tribulations", "None", "None", "Boss"),
            ("BitOfEmotion", "A Bit of Emotion", "None", "None", "Boss"),
            ("WildernessSurvivalModule", "Assistant: Wilderness Survival", "None", "None", "Boss"),
            ("HousekeepingAssistantModule", "Assistant: Housekeeping", "None", "None", "Boss"),
            ("IncrementalMotionFirmware", "Assistant: Motion Firmware", "None", "None", "Boss"),
            ("MiniDecontaminationKit", "Assistant: Decontamination Kit", "None", "None", "Boss"),
            ("ExternalUpgradeModule", "Assistant: External Upgrade", "None", "None", "Boss"),
            ("NetherseaBrandUnit", "Nethersea Brand (Unit)", "None", "None", "Boss"),
            ("CommonExit", "Exit Battlefield", "None", "None", "Boss"),
            ("RageResonance", "Rage Resonance", "None", "None", "Boss"),
            ("ArrowShiFau", "Arrow (Shifau)", "None", "None", "EGO"),
            ("ArrowInTheEyeFau", "Arrow in the Eye (Fau)", "None", "None", "EGO"),
            ("AimForTheGoal", "Aim for the Goal", "None", "None", "EGO"),
            ("SnipingArrowMode", "Sniping Arrow Mode", "None", "None", "EGO"),
            ("AStrokeOfDeath", "Stroke of Death", "None", "None", "EGO"),
            ("WOverCharge", "W Overcharge", "None", "None", "EGO"),
            ("BloodArmorMeursaultDrainEffect", "Meridian Armor Drain", "None", "None", "C6"),
            ("EmergencyChargeForceField", "Emergency Force Field", "None", "None", "C3"),
            ("BloodArmorCasting", "Forged Meridian", "None", "None", "C6"),
            ("HurtNightStiletto", "Wound", "None", "None", "C6"),
            ("CriHurtNightStiletto", "Deep Wound", "None", "None", "C6"),
            ("HorrHurtNightStiletto", "Fatal Wound", "None", "None", "C6"),
            ("GrownHorns", "Sprouted Horns", "None", "None", "C6"),
            ("CrushMarks", "Crush Marks", "None", "None", "C6"),
            ("CrushMarks_Main", "Crush Marks (Main)", "None", "None", "C6"),
            ("CrushMarks_Sub", "Crush Marks (Sub)", "None", "None", "C6"),
            ("CrushMarks_LowMorale", "Crush Marks: Low Morale", "None", "None", "C6"),
            ("CrushMarks_Panic", "Crush Marks: Panic", "None", "None", "C6"),
            ("AggressiveBokgak", "Primal Instinct", "None", "None", "Boss"),
            ("DeadEGOResource", "Remnant Will", "None", "None", "C4"),
            ("ProtectStanceRyoshu", "Guard Stance (Ryoshu)", "None", "None", "C6"),
            ("ChickenFightlust", "Fighting Spirit", "None", "None", "Boss"),
            ("HowDareYouApple", "How Dare You...!", "None", "None", "Boss"),
            ("RapidGrowthApple", "Rapid Growth", "None", "None", "Boss"),
            ("ObservationMoses", "Keen Observation", "None", "None", "Boss"),
            ("CalmAnalysisMoses", "Calm Analysis", "None", "None", "Boss"),
            ("MuscleContraction", "Muscle Contraction", "None", "None", "C5"),
            ("DuelEdge", "Duel Edge", "None", "None", "C5"),
            ("HostageCharon", "Hostage Crisis", "None", "None", "EGO"),
            ("DicipleLittleFinger", "Moonlit Blue Path", "None", "None", "Boss"),
            ("HumanFleshTheRings", "Biomaterial (Rings)", "None", "None", "C5"),
            ("BoneBladeTheRings", "Artwork: Fascia (Rings)", "None", "None", "C5"),
            ("UnsteadyTheRings", "Violently Resonating Armor", "None", "None", "C5"),
            ("BreathLoss_LowMorale", "Disrupted Poise: Low Morale", "None", "None", "C6"),
            ("BreathLoss_Panic", "Disrupted Poise: Panic", "None", "None", "C6"),
            ("IronMaiden_LowMorale", "Iron Maiden: Low Morale", "None", "None", "C6"),
            ("IronMaiden_Panic", "Iron Maiden: Panic", "None", "None", "C6"),
            ("RyoshuSlotPlusA1C9", "Stench of the Past", "None", "None", "C6"),
            ("ElseRyoshuSlotPlusA1C9", "ElseRyoshuSlotPlusA1C9", "None", "None", "C6"),
            ("IndexPrescriptTargetMarkToEnemy", "Prescript Mark", "None", "None", "C9"),
            ("IndexPrescript_Base", "Prescript", "None", "None", "C9"),
            ("IndexPrescriptFaust_0", "Prescript [Note] I", "None", "None", "C9"),
            ("IndexPrescriptFaust_1", "Prescript [Note] II", "None", "None", "C9"),
            ("IndexPrescriptFaust_2", "Prescript [Note] III", "None", "None", "C9"),
            ("IndexPrescriptFaust_3", "Prescript [Note] IV", "None", "None", "C9"),
            ("BlessingOfIndexPrescriptAlly", "Prescript Blessing (Ally)", "None", "None", "C9"),
            ("KarmaOfIndexAlly", "Karma (Ally)", "None", "None", "C9"),
            ("TeachersPet_LowMorale", "Teacher's Pet: Low Morale", "None", "None", "Boss"),
            ("TeachersPet_Panic", "Teacher's Pet: Panic", "None", "None", "Boss"),
            ("SeveredTendon", "Severed Tendon", "None", "None", "C9"),
            ("TheUdjatOutis", "Udjat Eye (Outis)", "None", "None", "Boss"),
            ("SheutFracture", "Sheut Fracture", "None", "None", "Boss"),
            ("BlueSand", "Blue Sand", "None", "None", "C5"),
            ("BlueSand_Main", "Blue Sand (Main)", "None", "None", "C5"),
            ("BlueSand_Sub", "Blue Sand (Sub)", "None", "None", "C5"),
            ("BlueSand_LowMorale", "Blue Sand: Low Morale", "None", "None", "C5"),
            ("BlueSand_Panic", "Blue Sand: Panic", "None", "None", "C5"),
            ("IndexPrescriptTargetToEnemy", "Prescript Target", "None", "None", "C9"),
            ("LCA_Bullet", "LCA Fissure Bullet", "None", "None", "EGO"),
            ("IndexCommonUnlock_1", "Common Unlock I", "None", "None", "C9"),
            ("IndexCommonUnlock_2", "Common Unlock II", "None", "None", "C9"),
            ("IndexCommonUnlock_3", "Common Unlock III", "None", "None", "C9"),
            ("IndexCommonUnlock_4", "Common Unlock IV", "None", "None", "C9"),
            ("IndexCommonPrescriptTarget", "Common Prescript Target", "None", "None", "C9"),
            ("IndexCommonBlessing", "Common Blessing", "None", "None", "C9"),
            ("IndexFaustMissionEffect", "Faust Mission Effect", "None", "None", "C9"),
            ("SheutFractureMaxStackEffect", "Sheut Fracture Max Stack", "None", "None", "Boss"),
            ("IDMicroChip", "Resident Microchip", "None", "None", "C4"),
            ("NiddleEGO", "Needle (EGO)", "None", "None", "EGO"),
            ("Vespa_LowMorale", "Vespa: Low Morale", "None", "None", "C5"),
            ("Vespa_Panic", "Vespa: Panic", "None", "None", "C5"),
            ("LCEFireFly_LowMorale", "LCE Firefly: Low Morale", "None", "None", "C5"),
            ("LCEFireFly_Panic", "LCE Firefly: Panic", "None", "None", "C5"),
            ("YellowHarpoon", "Yellow Harpoon", "None", "None", "C5"),
            ("YellowHarpoonSin", "Shin: Yellow Harpoon", "None", "None", "Shin"),
            ("BeeSting", "Embedded Harpoon [Bee Sting]", "None", "None", "C5"),
            ("ArtifactBeeSting", "Embedded Harpoon [Hornet Sting]", "None", "None", "C5"),
            ("HornetPoison", "Gungnir Resonance", "None", "None", "C5"),
            ("CriticalDmgUpVespa", "Seomgwang Sword Art", "None", "None", "C5"),
            ("IndexPrescriptStudent_0", "Prescript [Terminal] I", "None", "None", "C9"),
            ("IndexPrescriptStudent_1", "Prescript [Terminal] II", "None", "None", "C9"),
            ("IndexPrescriptStudent_2", "Prescript [Terminal] III", "None", "None", "C9"),
            ("IndexPrescriptStudent_3", "Prescript [Terminal] IV", "None", "None", "C9"),
            ("IndexPrescriptTargetToPersonality", "Index Prescript Target", "None", "None", "C9"),
            ("Blandishment_Enemy", "Natural Faith", "None", "None", "C9"),
            ("UnlockBuff_Base", "Unlock", "None", "None", "C9"),
            ("UnlockBuff_1", "Unlock I", "None", "None", "C9"),
            ("UnlockBuff_2", "Unlock II", "None", "None", "C9"),
            ("UnlockBuff_3", "Unlock III", "None", "None", "C9"),
            ("PressureOfPrescript", "Pressure of Prescript", "None", "None", "C9"),
            ("PressureOfPrescript_2nd", "Pressure of Prescript II", "None", "None", "C9"),
            ("KarmaOfIndexStudent", "Karma (Student)", "None", "None", "C9"),
            ("Ryoshu_IndexFightBuff", "Ryoshu Index Fight", "None", "None", "C9"),
            ("ScarBygoneDays_LowMorale", "Scars of Bygone Days: Low Morale", "None", "None", "C9"),
            ("ScarBygoneDays_Panic", "Scars of Bygone Days: Panic", "None", "None", "C9"),
            ("PhantomIncision", "Phantom Incision", "None", "None", "Boss"),
            ("PhantomIncisionTotal", "Phantom Incision (Total)", "None", "None", "Boss"),
            ("LvDownLittleFingerBoss", "Little Finger Boss Level Down", "None", "None", "Boss"),
            ("TimeGapAb", "Time Gap", "None", "None", "Boss"),
            ("TimeEntangleFour", "4-Layer Afterimage Entanglement", "None", "None", "Boss"),
            ("TimeEntangleThree", "3-Layer Afterimage Entanglement", "None", "None", "Boss"),
            ("TimeEntangleTwo", "2-Layer Afterimage Entanglement", "None", "None", "Boss"),
            ("TimeEntangleOne", "1-Layer Afterimage Entanglement", "None", "None", "Boss"),
            ("LittleFingerBoss_Shin", "Shin: Star of Wisdom", "None", "None", "Shin"),
            ("BlessingOfIndexPrescriptEnemy", "Prescript Blessing (Enemy)", "None", "None", "C9"),
            ("BlindFaith_LowMorale", "Blind Faith: Low Morale", "None", "None", "C9"),
            ("BlindFaith_Panic", "Blind Faith: Panic", "None", "None", "C9"),
            ("IndexPrescript_Base_2nd", "Prescript II", "None", "None", "C9"),
            ("IndexPrescriptDon_0", "Prescript [D] I", "None", "None", "C9"),
            ("IndexPrescriptDon_1", "Prescript [D] II", "None", "None", "C9"),
            ("IndexPrescriptDon_2", "Prescript [D] III", "None", "None", "C9"),
            ("IndexPrescriptDon_3", "Prescript [D] IV", "None", "None", "C9"),
            ("UnlockBuffAlly_1", "Unlock Ally I", "None", "None", "C9"),
            ("UnlockBuffAlly_2", "Unlock Ally II", "None", "None", "C9"),
            ("UnlockBuffAlly_3", "Unlock Ally III", "None", "None", "C9"),
            ("Blandishment", "Blandishment", "None", "None", "C9"),
            ("Blandishment_Shin", "Shin: Proxy", "None", "None", "Shin"),
            ("LvUpFireFly", "Firefly Level Up", "None", "None", "C5"),
            ("IndexPrescript_0", "Prescript I", "None", "None", "C9"),
            ("IndexPrescript_1", "Prescript II", "None", "None", "C9"),
            ("IndexPrescript_2", "Prescript III", "None", "None", "C9"),
            ("IndexPrescript_3", "Prescript IV", "None", "None", "C9"),
            ("KarmaOfIndexEnemy", "Karma (Enemy)", "None", "None", "C9"),
            ("Cubism_LowMorale", "Cubism: Low Morale", "None", "None", "C7"),
            ("Cubism_Panic", "Cubism: Panic", "None", "None", "C7"),
            ("LanternHohenheimBigBird", "Lantern (Big Bird)", "None", "None", "C1"),
            ("DelusionHohenheimBigBird", "Delusion (Big Bird)", "None", "None", "C1"),
            ("LvUpHohenheimBigBird", "E.G.O Resonance Up", "None", "None", "C1"),
            ("HohenheimBigBird_LowMorale", "Big Bird: Low Morale", "None", "None", "C1"),
            ("HohenheimBigBird_Panic", "Big Bird: Panic", "None", "None", "C1"),
            ("HumanBoneTheRings", "Human Bone (Rings)", "None", "None", "C5"),
            ("HumanBloodTheRings", "Human Blood (Rings)", "None", "None", "C5"),
            ("LivingSpecimenTheRings", "Living Specimen (Rings)", "None", "None", "C5"),
            ("MineTheRings", "Mine (Rings)", "None", "None", "C5"),
            ("ObservationTheRings", "Observation (Rings)", "None", "None", "C5"),
            ("MosesWhiteBreathMental", "Moses: White Breath (Mind)", "None", "None", "Boss"),
            ("MosesRedBreathBody", "Moses: Red Breath (Body)", "None", "None", "Boss"),
            ("MosesRedBreathShield", "Moses: Red Breath (Shield)", "None", "None", "Boss"),
            ("MosesPurpleBreathBind", "Moses: Purple Breath (Bind)", "None", "None", "Boss"),
            ("MosesRedBreathCharge", "Moses: Red Breath (Charge)", "None", "None", "Boss"),
            ("MosesSin", "Shin: Moses", "None", "None", "Shin"),
            ("MosesMiddle_LowMorale", "Moses: Low Morale", "None", "None", "Boss"),
            ("MosesMiddle_Panic", "Moses: Panic", "None", "None", "Boss"),
            ("VengeanceBookSpider", "Vengeance Book (Spider)", "None", "None", "Boss"),
            ("ResentmentSpider", "Resentment (Spider)", "None", "None", "Boss"),
            ("GotACompliment", "Got a Compliment", "None", "None", "Boss"),
            ("DaughterEducationSpider", "Daughter's Education (Spider)", "None", "None", "Boss"),
            ("ReinforcedTattooSpider", "Reinforced Tattoo (Spider)", "None", "None", "Boss"),
            ("HeatingWireOnSpider", "Heating Wire ON (Spider)", "None", "None", "Boss"),
            ("HeatingWireOffSpider", "Heating Wire OFF (Spider)", "None", "None", "Boss"),
            ("MiddleFatherGuts", "Middle Father's Guts", "None", "None", "Boss"),
            ("MiddleFingerDaddy_LowMorale", "Middle Finger Dad: Low Morale", "None", "None", "Boss"),
            ("MiddleFingerDaddy_Panic", "Middle Finger Dad: Panic", "None", "None", "Boss"),
            ("MiddleFingerDaughter_LowMorale", "Middle Finger Daughter: Low Morale", "None", "None", "Boss"),
            ("MiddleFingerDaughter_Panic", "Middle Finger Daughter: Panic", "None", "None", "Boss"),
            ("OffenseBug", "Offensive Bug", "None", "None", "C1"),
            ("DefenseBug", "Defensive Bug", "None", "None", "C1"),
            ("AgainstMyWill_LowMorale", "Against My Will: Low Morale", "None", "None", "C9"),
            ("AgainstMyWill_Panic", "Against My Will: Panic", "None", "None", "C9"),
            ("AaCfPcBiv2", "Formation Effect (Alt)", "None", "None", "MD"),
            ("VergiliusSin", "Shin: Red Gaze", "None", "None", "Shin"),
            ("BloodBranch", "Blood Branch", "None", "None", "C7"),
            ("SpecimenTheRings", "Specimen (Rings)", "None", "None", "C5"),
            ("KAmpouleA1C9", "K Corp. Ampule (A1C9)", "None", "None", "EGO"),
            ("RyoshuEGOPF", "Ryoshu EGO PF", "None", "None", "EGO"),
            ("EsteemNeeds", "Esteem Needs", "None", "None", "C4"),
            ("Fugacious", "Fleeting Feeling", "None", "None", "C4"),
            ("LvUpHohenheimBigBird_Over", "E.G.O Resonance Overflow", "None", "None", "C1"),
            ("LvUpFireFly_Over", "Firefly Level Overflow", "None", "None", "C5"),
            ("DimensionalBagEzraA", "Dimensional Bag: Alas Workshop", "None", "None", "C9"),
            ("DimensionalBagEzraB", "Dimensional Bag: Nestor Workshop", "None", "None", "C9"),
            ("DimensionalBagEzraC", "Dimensional Bag: Screw Workshop", "None", "None", "C9"),
            ("DimensionalBagEzraD", "Dimensional Bag: Namir Workshop", "None", "None", "C9"),
            ("DimensionalBagEzraE", "Dimensional Bag: Yuria Workshop", "None", "None", "C9"),
            ("MiddleFingerEzraSin", "Shin: Ezra", "None", "None", "Shin"),
            ("EzraMiddle_LowMorale", "Ezra Middle: Low Morale", "None", "None", "Boss"),
            ("EzraMiddle_Panic", "Ezra Middle: Panic", "None", "None", "Boss"),
            ("Gebura_Reinforcement", "Gebura Reinforcement", "None", "None", "Boss"),
            ("ObsessedTeacher_LowMorale", "Obsessed Teacher: Low Morale", "None", "None", "Boss"),
            ("ObsessedTeacher_Panic", "Obsessed Teacher: Panic", "None", "None", "Boss"),
            ("FutureEyeOn", "Future Eye: On", "None", "None", "C2"),
            ("FutureEyeOff", "Future Eye: Off", "None", "None", "C2"),
            ("LookingFuture", "Looking to the Future", "None", "None", "C2"),
            ("ObsessionAndGreed", "Shin: Valentina", "None", "None", "Shin"),
            ("AccelBullet", "Accelerated Bullet", "None", "None", "EGO"),
            ("Sword2ndTheRings", "Second Sword (Rings)", "None", "None", "C5"),
            ("RedemptionTheRings", "Redemption (Rings)", "None", "None", "C5"),
            ("MosesWarPTSD", "Shin: Moses (War PTSD)", "None", "None", "Shin"),
            ("ThreeMirrorpartYiSang", "Three-Mirror Yi Sang", "None", "None", "C4"),
            ("WillAwakenSinclair", "Shin: Sinclair", "None", "None", "Shin"),
            ("SignAwakenSinclair", "Awakening Sign (Sinclair)", "None", "None", "Shin"),
            ("Veteran_LowMorale", "Veteran: Low Morale", "None", "None", "C9"),
            ("Veteran_Panic", "Veteran: Panic", "None", "None", "C9"),
            ("IndexPrescriptRien_0", "Prescript [Rien] I", "None", "None", "C9"),
            ("IndexPrescriptRien_1", "Prescript [Rien] II", "None", "None", "C9"),
            ("IndexPrescriptRien_2", "Prescript [Rien] III", "None", "None", "C9"),
            ("IndexPrescriptRien_3", "Prescript [Rien] IV", "None", "None", "C9"),
            ("IndexPrescript_RienSecondPhase", "Prescript [Rien Phase 2]", "None", "None", "C9"),
            ("KarmaOfIndexRien", "Karma (Rien)", "None", "None", "C9"),
            ("FanaticismRien", "Fanaticism (Rien)", "None", "None", "C9"),
            ("Shin_Rien", "Shin: Rien", "None", "None", "Shin"),
            ("PainfulScar_LowMorale", "Painful Scar: Low Morale", "None", "None", "C9"),
            ("PainfulScar_Panic", "Painful Scar: Panic", "None", "None", "C9"),
            ("Ryoshu_RienBattle_1Phase", "Ryoshu vs Rien (Phase 1)", "None", "None", "Sys"),
            ("Ryoshu_RienBattle_2Phase", "Ryoshu vs Rien (Phase 2)", "None", "None", "Sys"),
            ("SojiAbiBuffOne", "Soji Ability I", "None", "None", "Boss"),
            ("SojiAbiBuffTwo", "Soji Ability II", "None", "None", "Boss"),
            ("SojiAbiBuffThree", "Soji Ability III", "None", "None", "Boss"),
            ("SojiAbiBuffFour", "Soji Ability IV", "None", "None", "Boss"),
            ("TeachersCommand", "Teacher's Command", "None", "None", "Boss"),
            ("RyoshuStartA1C9", "Ryoshu Start (A1C9)", "None", "None", "EGO"),
            ("RyoshuParrySoji", "Ryoshu Parry (Soji)", "None", "None", "Boss"),
            ("Blandishment_Shin_Enemy", "Shin: Sora", "None", "None", "Shin"),
            ("RyoshuStartA1C9P2", "Ryoshu Start (A1C9 Phase 2)", "None", "None", "EGO"),
            ("RyoshuParrySojiTwo", "Ryoshu Parry (Soji) II", "None", "None", "Boss"),
            ("ResentmentSpiderDaughter", "Resentment (Spider Daughter)", "None", "None", "Boss"),
            ("ReinforcedTattooSpiderDaughter", "Reinforced Tattoo (Spider Daughter)", "None", "None", "Boss"),
            ("LimitedAwakenSinclair", "Limited Awakening (Sinclair)", "None", "None", "Shin"),
            ("HeavenlyKillerStar", "Tiansha Star - Arayashiki", "None", "None", "Boss"),
            ("TeachersPrey", "Teacher's Prey", "None", "None", "Boss"),
            ("LvDownLittleFingerBossTwo", "Little Finger Boss Level Down II", "None", "None", "Boss"),
            ("SojiAbiAgePast", "Soji: Past Age", "None", "None", "Boss"),
            ("SojiAbiAgeFuture", "Soji: Future Age", "None", "None", "Boss"),
            ("TimeEntangleUnstable", "Unstable Afterimage", "None", "None", "Boss"),
            ("RyoshuParrySojiThree", "Ryoshu Parry (Soji) III", "None", "None", "Boss"),
            ("MiddleFatherSwordOne", "Middle Father: Sword I", "None", "None", "Boss"),
            ("MiddleFatherSwordTwo", "Middle Father: Sword II", "None", "None", "Boss"),
            ("MiddleFatherSwordThree", "Middle Father: Sword III", "None", "None", "Boss"),
            ("MiddleFatherSwordFour", "Middle Father: Sword IV", "None", "None", "Boss"),
            ("Malkuth_Imperfect", "Imperfect Malkuth", "None", "None", "Boss"),
            ("KAmpouleA1C92ND", "K Corp. Ampule II", "None", "None", "EGO"),
            ("PhantomIncisionSuccessEffect", "Phantom Incision Success", "None", "None", "Boss"),
            ("BurningWoundRien_Mask", "Rien's Mask", "None", "None", "Boss"),
            ("BurningWoundRien", "Rien: Burning Wound", "None", "None", "Boss"),
            ("StackRienSpecialSkill", "Rien: Special Skill Stack", "None", "None", "Boss"),
            ("KAmpouleA1C93RD", "K Corp. Ampule III", "None", "None", "EGO"),
            ("KAmpouleA1C94TH", "K Corp. Ampule IV", "None", "None", "EGO"),
            ("TimeEntangleDesc", "Afterimage Entanglement", "None", "None", "Boss"),
            ("CutoffRyoshuOnDie", "Cutoff on Death (Ryoshu)", "None", "None", "C6"),
            ("MosesRedBreathAttack", "Moses: Red Breath (Attack)", "None", "None", "Boss"),
            ("KAA1C9ActivateEffect", "K Corp. A1C9 Activate", "None", "None", "EGO"),
            ("RienWeaponBase", "Rien: Weapon Base", "None", "None", "Boss"),
            ("RienWeapon01Hatchet", "Rien: Hatchet", "None", "None", "Boss"),
            ("RienWeapon01Hatchet2phase", "Rien: Hatchet (Phase 2)", "None", "None", "Boss"),
            ("RienWeapon02Stiletto", "Rien: Stiletto", "None", "None", "Boss"),
            ("RienWeapon02Stiletto2phase", "Rien: Stiletto (Phase 2)", "None", "None", "Boss"),
            ("RienWeapon03Greatsword", "Rien: Greatsword", "None", "None", "Boss"),
            ("RienWeapon03Greatsword2phase", "Rien: Greatsword (Phase 2)", "None", "None", "Boss"),
            ("RienWeapon04Rapier", "Rien: Rapier", "None", "None", "Boss"),
            ("RienWeapon04Rapier2phase", "Rien: Rapier (Phase 2)", "None", "None", "Boss"),
            ("RienWeapon05Sledgehammer", "Rien: Sledgehammer", "None", "None", "Boss"),
            ("RienWeapon05Sledgehammer2phase", "Rien: Sledgehammer (Phase 2)", "None", "None", "Boss"),
            ("RienWeapon06Ultragreatsword", "Rien: Ultragreatsword", "None", "None", "Boss"),
            ("RienWeapon06Ultragreatsword2phase", "Rien: Ultragreatsword (Phase 2)", "None", "None", "Boss"),
            ("RienWeapon07Lance", "Rien: Lance", "None", "None", "Boss"),
            ("RienWeapon07Lance2phase", "Rien: Lance (Phase 2)", "None", "None", "Boss"),
            ("RienWeapon08Chain", "Rien: Chain", "None", "None", "Boss"),
            ("RienWeapon08Chain2phase", "Rien: Chain (Phase 2)", "None", "None", "Boss"),
            ("RienWeapon09Scythe", "Rien: Scythe", "None", "None", "Boss"),
            ("RienWeapon09Scythe2phase", "Rien: Scythe (Phase 2)", "None", "None", "Boss"),
            ("DefenseBugEffect", "Defensive Bug Effect", "None", "None", "C9"),
            ("RyoshuParrySojiWe", "Ryoshu Parry (Soji): We", "None", "None", "Boss"),
            ("RyoshuParrySojiThey", "Ryoshu Parry (Soji): They", "None", "None", "Boss"),
            ("UnlockBuff_Rien1", "Unlock (Rien) I", "None", "None", "C9"),
            ("UnlockBuff_Rien2", "Unlock (Rien) II", "None", "None", "C9"),
            ("UnlockBuff_Rien3", "Unlock (Rien) III", "None", "None", "C9"),
            ("KarmaOfIndexRien_2Phase", "Karma (Rien) Phase 2", "None", "None", "C9"),
            ("OffenseBugEffect", "Offensive Bug Effect", "None", "None", "C9"),
            ("RyoshuParryIndexFingerWe", "Ryoshu Parry (Index): We", "None", "None", "Boss"),
            ("RyoshuParryIndexFingerThey", "Ryoshu Parry (Index): They", "None", "None", "Boss"),
            ("ReinforcedTattooIshmael", "Reinforced Tattoo (Ishmael)", "None", "None", "C5"),
            ("ResentmentIshmael", "Resentment (Ishmael)", "None", "None", "C5"),
            ("HeatingWireIshmael", "Heating Wire (Ishmael)", "None", "None", "C5"),
            ("LanternGregBigBird", "Lantern (Greg Big Bird)", "None", "None", "C1"),
            ("ObedienceGregBigBird", "Obedience (Greg Big Bird)", "None", "None", "C1"),
            ("VigilanceGregBigBird", "Vigilance (Greg Big Bird)", "None", "None", "C1"),
            ("DelusionGregBigBird", "Delusion (Greg Big Bird)", "None", "None", "C1"),
            ("DelusionGregBigBird_LowMorale", "Delusion: Low Morale", "None", "None", "C1"),
            ("DelusionGregBigBird_Panic", "Delusion: Panic", "None", "None", "C1"),
            ("DelusionGregBigBird_Main", "Delusion (Main)", "None", "None", "C1"),
            ("DelusionGregBigBird_Sub", "Delusion (Sub)", "None", "None", "C1"),
            ("ExtractCoin", "Extract Coin", "None", "None", "C1"),
            ("LittleFingerID", "Little Finger ID", "None", "None", "C9"),
            ("IndexPrescriptYi_0", "Prescript [Yi] I", "None", "None", "C4"),
            ("IndexPrescriptYi_1", "Prescript [Yi] II", "None", "None", "C4"),
            ("IndexPrescriptYi_2", "Prescript [Yi] III", "None", "None", "C4"),
            ("IndexPrescriptYi_3", "Prescript [Yi] IV", "None", "None", "C4"),
            ("SatisfyingEsteemNeeds", "Satisfying Esteem Needs", "None", "None", "C4"),
            ("StackYisangSpecialSkill", "Yi Sang Special Skill Stack", "None", "None", "C4"),
            ("Shin_IndexFingerYisang", "Shin: Fate", "None", "None", "Shin"),
            ("BurningWoundYisangMask", "Yi Sang's Mask", "None", "None", "C4"),
            ("BurningWoundYisang", "Yi Sang: Burning Wound", "None", "None", "C4"),
            ("BlackNightmareYisang", "Yi Sang: Black Nightmare", "None", "None", "C4"),
            ("YisangWeapon01Hatchet", "Yi Sang: Hatchet", "None", "None", "C4"),
            ("YisangWeapon02Stiletto", "Yi Sang: Stiletto", "None", "None", "C4"),
            ("YisangWeapon03Greatsword", "Yi Sang: Greatsword", "None", "None", "C4"),
            ("YisangWeapon04Rapier", "Yi Sang: Rapier", "None", "None", "C4"),
            ("YisangWeapon05Sledgehammer", "Yi Sang: Sledgehammer", "None", "None", "C4"),
            ("YisangWeapon06Ultragreatsword", "Yi Sang: Ultragreatsword", "None", "None", "C4"),
            ("YisangWeapon07Lance", "Yi Sang: Lance", "None", "None", "C4"),
            ("YisangWeapon08Chain", "Yi Sang: Chain", "None", "None", "C4"),
            ("YisangWeapon09Scythe", "Yi Sang: Scythe", "None", "None", "C4"),
            ("YisangWeaponBase", "Yi Sang: Weapon Base", "None", "None", "C4"),
            ("ObjectOfExploration", "Object of Exploration", "None", "None", "C4"),
            ("Inspire", "Inspiration", "None", "None", "C4"),
            ("PaintingMaterial", "Painting Material", "None", "None", "C4"),
            ("GreatAesthetics", "Great Aesthetics", "None", "None", "C4"),
            ("GiftCannon", "Gift Cannon", "None", "None", "C4"),
            ("GiftGlass", "Gift Glass", "None", "None", "C4"),
            ("EnhanceRoseSign", "Enhanced Rose Sign", "None", "None", "C4"),
            ("BlandishmentShinEnemyDelete", "Shin: ??? (Deleted)", "None", "None", "Shin"),
            ("LeakedOutSauce", "Leaked Sauce", "None", "None", "C4"),
            ("MeursaultBeeSpore", "Meursault: Bee Spore", "None", "None", "EGO"),
            ("MeursaultWorkerBee", "Meursault: Worker Bee", "None", "None", "EGO"),
            ("MeursaultSporeBulletLong", "Spore Bullet (Long)", "None", "None", "EGO"),
            ("MeursaultSporeBulletShort", "Spore Bullet (Short)", "None", "None", "EGO"),
            ("MeursaultSporeBulletReloading", "Spore Bullet (Reloading)", "None", "None", "EGO"),
            ("ThatIsRhythm", "That is Rhythm", "None", "None", "EGO"),
            ("BulletGodok", "Bullet (Godok)", "None", "None", "EGO"),
            ("AlriuneEGOThey", "Alriun EGO: They", "None", "None", "EGO"),
            ("AlriuneEGOWe", "Alriun EGO: We", "None", "None", "EGO"),
            ("GodokPanicType", "Godok: Panic Type", "None", "None", "EGO"),
            ("Godok_Lowmorale", "Godok: Low Morale", "None", "None", "EGO"),
            ("Godok_Panic", "Godok: Panic", "None", "None", "EGO"),
            ("GodokPanicType_Main", "Godok: Panic Type (Main)", "None", "None", "EGO"),
            ("GodokPanicType_Sub", "Godok: Panic Type (Sub)", "None", "None", "EGO"),
            ("BestWelfareTeamCaptain", "Best Welfare Captain", "None", "None", "MD"),
            ("RunHallway", "Run Hallway", "None", "None", "C4"),
            ("NextHallway", "Next Hallway", "None", "None", "C4"),
            ("PumpkinJelly", "Pumpkin Jelly", "None", "None", "C4"),
            ("DesperadoBuff", "Desperado", "None", "None", "C4"),
            ("TensionUp", "Tension Up", "None", "None", "C4"),
            ("PinkPetals", "Pink Petals", "None", "None", "C4"),
            ("QueenBeepheromone", "Queen Bee Pheromone", "None", "None", "C4"),
            ("QueenBeeMark", "Queen Bee Mark", "None", "None", "C4"),
            ("MeursaultBeeGunLong", "Bee Gun (Long)", "None", "None", "EGO"),
            ("MeursaultBeeGunShort", "Bee Gun (Short)", "None", "None", "EGO"),
            ("AddBullet", "Add Ammo", "None", "None", "EGO"),
            ("CommonReload", "Common Reload", "None", "None", "EGO"),
            ("PhotoElectricityFlux_Dummy", "Photoelectric Flux", "None", "None", "EGO"),
            ("SelfChargeAlly", "Self Charge (Ally)", "None", "None", "EGO"),
            ("HighVoltageExoshell", "High Voltage Exoshell", "None", "None", "EGO"),
            ("ChargedSting", "Charged Sting", "None", "None", "EGO"),
            ("FaustFlameMothEmber", "Faust: Flame Moth Ember", "None", "None", "EGO"),
            ("BloodthirstHard", "Bloodthirst (Hard)", "None", "None", "C1"),
            ("Yummy_Lowmorale", "Yummy: Low Morale", "None", "None", "Sys"),
            ("Yummy_Panic", "Yummy: Panic", "None", "None", "Sys"),
            ("ArtworkBodyArt01", "Artwork: Body Art I", "None", "None", "C7"),
            ("ArtworkBodyArt02", "Artwork: Body Art II", "None", "None", "C7"),
            ("ArtworkBodyArt03", "Artwork: Body Art III", "None", "None", "C7"),
            ("TouchBodyArt01", "Touch: Body Art I", "None", "None", "C7"),
            ("ChargeBodyArt", "Biomaterial (Charge)", "None", "None", "EGO"),
            ("TibiaPersonality", "Tibia (Personality)", "None", "None", "C7"),
            ("MelodyBodyArt", "Melody: Body Art", "None", "None", "C7"),
            ("IronMaidenPersonality", "Iron Maiden (Personality)", "None", "None", "C6"),
            ("SilverOpportunity", "Silver Opportunity", "None", "None", "C7"),
            ("FasciaPersonality", "Fascia (Personality)", "None", "None", "C7"),
            ("ArtworkBodyArt01Effect", "Body Art I Effect", "None", "None", "C7"),
            ("ArtworkBodyArt02Effect", "Body Art II Effect", "None", "None", "C7"),
            ("ArtworkBodyArt03Effect", "Body Art III Effect", "None", "None", "C7"),
            ("LivingSpecimenPersonality", "Living Specimen (Personality)", "None", "None", "C7"),
            ("GoldenOpportunity", "Golden Opportunity", "None", "None", "C7"),
            ("MelodyBodyArtChebello", "Melody: Body Art (Chebello)", "None", "None", "C7"),
            ("MiddleFatherGutsMirror", "Middle Father's Guts (Mirror)", "None", "None", "Boss"),
            ("TeachersPreyMirror", "Teacher's Prey (Mirror)", "None", "None", "Boss"),
            ("ObservationTheRingsHidden", "Observation: Rings (Hidden)", "None", "None", "C5"),
            ("LookingFutureMirror", "Looking to the Future (Mirror)", "None", "None", "C2"),
            ("FutureEyeOnMirror", "Future Eye: On (Mirror)", "None", "None", "C2"),
            ("FutureEyeOffMirror", "Future Eye: Off (Mirror)", "None", "None", "C2"),
            ("MD7LimitBaseN", "MD7 Limit: BaseN", "None", "None", "MD"),
            ("MD7Limit101", "MD7 Limit: 101", "None", "None", "MD"),
            ("MD7Limit111", "MD7 Limit: 111", "None", "None", "MD"),
            ("MD7Limit121", "MD7 Limit: 121", "None", "None", "MD"),
            ("MD7Limit131", "MD7 Limit: 131", "None", "None", "MD"),
            ("MD7Limit141", "MD7 Limit: 141", "None", "None", "MD"),
            ("BearClawWound", "Bear Claw Wound", "None", "None", "C7"),
            ("EagleClawWound", "Eagle Claw Wound", "None", "None", "C7"),
            ("FaubismWolfMaskRodion", "Fauvism Wolf Mask (Rodion)", "None", "None", "C7"),
            ("BoldBrushstrokesRodion", "Bold Brushstrokes (Rodion)", "None", "None", "C7"),
            ("IntenseColorsRodion", "Intense Colors (Rodion)", "None", "None", "C7"),
            ("ThankyouDocentRodion", "Thank You Docent (Rodion)", "None", "None", "C7"),
            ("TestWaitDocentRodion", "Wait, Docent (Rodion)", "None", "None", "C7"),
            ("FaubismMaskMeursault", "Fauvism Mask (Meursault)", "None", "None", "C7"),
            ("TestWaitingMeursault", "Waiting (Meursault)", "None", "None", "C7"),
            ("Fauvism_Lowmorale", "Fauvism: Low Morale", "None", "None", "C7"),
            ("Fauvism_Panic", "Fauvism: Panic", "None", "None", "C7"),
            ("BoldBrushstrokes", "Bold Brushstrokes", "None", "None", "C7"),
            ("IntenseColors", "Intense Colors", "None", "None", "C7"),
            ("FaubismWolfMask", "Fauvism Wolf Mask", "None", "None", "C7"),
            ("FaubismWolfMaskBlooded", "Fauvism Wolf Mask (Blooded)", "None", "None", "C7"),
            ("PanicChangeLock", "Panic Change Lock", "None", "None", "C7"),
            ("IndexPrescriptEnemy_0", "Prescript [Enemy] I", "None", "None", "C9"),
            ("IndexPrescriptEnemy_1", "Prescript [Enemy] II", "None", "None", "C9"),
            ("IndexPrescriptEnemy_2", "Prescript [Enemy] III", "None", "None", "C9"),
            ("IndexPrescriptEnemy_3", "Prescript [Enemy] IV", "None", "None", "C9"),
            ("FauvismDocent_Lowmorale", "Fauvism Docent: Low Morale", "None", "None", "C7"),
            ("FauvismDocent_Panic", "Fauvism Docent: Panic", "None", "None", "C7"),
            ("RingFingerPhysical", "Physical Beauty (Ring Finger)", "None", "None", "C5"),
            ("RingFingerFauvism", "Wild Beauty (Ring Finger)", "None", "None", "C5"),
            ("ResentmentSpiderDaughterTwo", "Resentment (Spider Daughter II)", "None", "None", "Boss"),
            ("ReinforcedTattooSpiderDaughterTwo", "Reinforced Tattoo (Spider Daughter II)", "None", "None", "Boss"),
            ("HeatingWireOnSpiderTwo", "Heating Wire ON (Spider II)", "None", "None", "Boss"),
            ("BearClawWoundAlly", "Bear Claw Wound (Ally)", "None", "None", "C7"),
            ("EagleClawWoundAlly", "Eagle Claw Wound (Ally)", "None", "None", "C7"),
            // ── MRR (Refracted Mirror) entries ────────────────────────────
            ("MRR5BaseP", "Refracted: 5Base (Pos)", "None", "None", "MD"),
            ("MRR5BaseN", "Refracted: 5BaseN", "None", "None", "MD"),
            ("MRR501", "Refracted: 501", "None", "None", "MD"),
            ("MRR502", "Refracted: 502", "None", "None", "MD"),
            ("MRR503", "Refracted: 503", "None", "None", "MD"),
            ("MRR504", "Refracted: 504", "None", "None", "MD"),
            ("MRR505", "Refracted: 505", "None", "None", "MD"),
            ("MRR506", "Refracted: 506", "None", "None", "MD"),
            ("MRR507", "Refracted: 507", "None", "None", "MD"),
            ("MRR508", "Refracted: 508", "None", "None", "MD"),
            ("MRR509", "Refracted: 509", "None", "None", "MD"),
            ("MRR509P", "Refracted: 509 (Positive)", "None", "None", "MD"),
            ("MRR509E", "Refracted: 509 (Effect)", "None", "None", "MD"),
            ("MRR510", "Refracted: 510", "None", "None", "MD"),
            ("MRR510P", "Refracted: 510 (Positive)", "None", "None", "MD"),
            ("MRR510E", "Refracted: 510 (Effect)", "None", "None", "MD"),
            ("MRR511", "Refracted: 511", "None", "None", "MD"),
            ("MRR511P", "Refracted: 511 (Positive)", "None", "None", "MD"),
            ("MRR511E", "Refracted: 511 (Effect)", "None", "None", "MD"),
            ("MRR512", "Refracted: 512", "None", "None", "MD"),
            ("MRR513", "Refracted: 513", "None", "None", "MD"),
            ("MRR514", "Refracted: 514", "None", "None", "MD"),
            ("MRR515", "Refracted: 515", "None", "None", "MD"),
            ("MRR516", "Refracted: 516", "None", "None", "MD"),
            ("MRR517", "Refracted: 517", "None", "None", "MD"),
            ("MRR518", "Refracted: 518", "None", "None", "MD"),
            ("MRR518P", "Refracted: 518 (Positive)", "None", "None", "MD"),
            ("MRR518E", "Refracted: 518 (Effect)", "None", "None", "MD"),
            ("MRR519", "Refracted: 519", "None", "None", "MD"),
            ("MRR519P", "Refracted: 519 (Positive)", "None", "None", "MD"),
            ("MRR519E", "Refracted: 519 (Effect)", "None", "None", "MD"),
            ("MRR520", "Refracted: 520", "None", "None", "MD"),
            ("MRR520P", "Refracted: 520 (Positive)", "None", "None", "MD"),
            ("MRR520E", "Refracted: 520 (Effect)", "None", "None", "MD"),
            ("MRR521", "Refracted: 521", "None", "None", "MD"),
            ("MRR522", "Refracted: 522", "None", "None", "MD"),
            ("MRR523", "Refracted: 523", "None", "None", "MD"),
            ("MRR524", "Refracted: 524", "None", "None", "MD"),
            ("MRR524P", "Refracted: 524 (Positive)", "None", "None", "MD"),
            ("MRR524E", "Refracted: 524 (Effect)", "None", "None", "MD"),
            ("MRR525", "Refracted: 525", "None", "None", "MD"),
            ("MRR526", "Refracted: 526", "None", "None", "MD"),
            ("MRR526P", "Refracted: 526 (Positive)", "None", "None", "MD"),
            ("MRR526E", "Refracted: 526 (Effect)", "None", "None", "MD"),
            ("MRR527", "Refracted: 527", "None", "None", "MD"),
            ("MRR527P", "Refracted: 527 (Positive)", "None", "None", "MD"),
            ("MRR527E", "Refracted: 527 (Effect)", "None", "None", "MD"),
            ("MRR528", "Refracted: 528", "None", "None", "MD"),
            ("MRR528P", "Refracted: 528 (Positive)", "None", "None", "MD"),
            ("MRR528E", "Refracted: 528 (Effect)", "None", "None", "MD"),
            ("MRR529", "Refracted: 529", "None", "None", "MD"),
            ("MRR530", "Refracted: 530", "None", "None", "MD"),
            ("MRR531", "Refracted: 531", "None", "None", "MD"),
            ("MRR532", "Refracted: 532", "None", "None", "MD"),
            ("MRR533", "Refracted: 533", "None", "None", "MD"),
            ("MRR534", "Refracted: 534", "None", "None", "MD"),
            ("MRR535", "Refracted: 535", "None", "None", "MD"),
            ("MRR536", "Refracted: 536", "None", "None", "MD"),
            ("MRR537", "Refracted: 537", "None", "None", "MD"),
            ("MRR538", "Refracted: 538", "None", "None", "MD"),
            ("MRR539", "Refracted: 539", "None", "None", "MD"),
            ("MRR540", "Refracted: 540", "None", "None", "MD"),
            ("MRR541", "Refracted: 541", "None", "None", "MD"),
            ("MRR542", "Refracted: 542", "None", "None", "MD"),
            ("MRR543", "Refracted: 543", "None", "None", "MD"),
            ("MRR544", "Refracted: 544", "None", "None", "MD"),
            ("MRR545", "Refracted: 545", "None", "None", "MD"),
            ("MRR546", "Refracted: 546", "None", "None", "MD"),
            ("MRR547", "Refracted: 547", "None", "None", "MD"),
            ("MRR548", "Refracted: 548", "None", "None", "MD"),
            ("MRR549", "Refracted: 549", "None", "None", "MD"),
            ("MRR550", "Refracted: 550", "None", "None", "MD"),
            ("MRR551", "Refracted: 551", "None", "None", "MD"),
            ("MRR552", "Refracted: 552", "None", "None", "MD"),
            ("MRR553", "Refracted: 553", "None", "None", "MD"),
            ("MRR553P", "Refracted: 553 (Positive)", "None", "None", "MD"),
            ("MRR553E", "Refracted: 553 (Effect)", "None", "None", "MD"),
            ("MRR554", "Refracted: 554", "None", "None", "MD"),
            ("MRR555", "Refracted: 555", "None", "None", "MD"),
            ("MRR556", "Refracted: 556", "None", "None", "MD"),
            ("MRR557", "Refracted: 557", "None", "None", "MD"),
            ("MRR558", "Refracted: 558", "None", "None", "MD"),
            ("MRR559", "Refracted: 559", "None", "None", "MD"),
            ("MRR560", "Refracted: 560", "None", "None", "MD"),
            ("MRR561", "Refracted: 561", "None", "None", "MD"),
            ("MRR562", "Refracted: 562", "None", "None", "MD"),
            ("MRR563", "Refracted: 563", "None", "None", "MD"),
            ("MRR564", "Refracted: 564", "None", "None", "MD"),
            ("MRR565", "Refracted: 565", "None", "None", "MD"),
            ("MRR566", "Refracted: 566", "None", "None", "MD"),
            ("MRR567", "Refracted: 567", "None", "None", "MD"),
            ("MRR568", "Refracted: 568", "None", "None", "MD"),
            ("MRR569", "Refracted: 569", "None", "None", "MD"),
            // --- NEW (July 2026 update) ---
            // Sys
            ("DamageOverTime", "Ongoing Damage", "None", "None", "Sys"),
            ("Emergency_Operation", "Makeshift Surgery", "None", "None", "Sys"),
            ("FireField", "Scorchfield", "None", "None", "Sys"),
            ("Firstlight", "Battle Instinct", "None", "None", "Sys"),
            ("GreedyReady", "Constricting Desires", "None", "None", "Sys"),
            ("Hardening", "Hardening", "None", "None", "Sys"),
            ("MatchesPersonality", "Blazing", "None", "None", "Sys"),
            ("MoonLight", "Moonlight", "None", "None", "Sys"),
            ("Perfected", "Perfect Shell", "None", "None", "Sys"),
            ("RemoveBreakSection", "Stalwart Spirit", "None", "None", "Sys"),
            ("SelfHarm_LowMorale", "Compulsion: Low Morale", "None", "None", "Sys"),
            ("SelfHarm_Panic", "Compulsion: Panic", "None", "None", "Sys"),
            ("StickCoinHeadAndTailToMaxPer", "Meshed Possibilities", "None", "None", "Sys"),
            ("Target", "Marked", "None", "None", "Sys"),
            ("Taunt", "Taunt", "None", "None", "Sys"),
            ("TimesCoinValueDown", "Multiply Coin Drop", "None", "None", "Sys"),
            ("TimesCoinValueUp", "Multiply Coin Boost", "None", "None", "Sys"),
            ("DawnFaust", "High Noon", "None", "None", "Sys"),
            ("DawnFaustDying", "Loneliness at High Noon", "None", "None", "Sys"),
            ("DawnGregor", "From Dawnbreak to Sunset", "None", "None", "Sys"),
            ("DawnGregorDying", "Blazing Sunset", "None", "None", "Sys"),
            ("DawnLight", "Dawnherald", "None", "None", "Sys"),
            ("DawnTeam", "Dawn Office", "None", "None", "Sys"),
            // C2
            ("ATL_EndureCombustion", "Burn Resistance", "None", "None", "C2"),
            ("FutureEyeOffRodion", "Eye of Precognition - Overheat (Rodion)", "None", "None", "C2"),
            ("FutureEyeOnRodion", "Eye of Precognition (Rodion)", "None", "None", "C2"),
            ("LookingFutureRodion", "Accelerating Future (Rodion)", "None", "None", "C2"),
            ("RailBuffForLine2_A_ALLY", "Railway L2: Slash Boost", "None", "None", "C2"),
            ("RailBuffForLine2_A_ENEMY", "Railway L2: Cycle", "None", "None", "C2"),
            ("RailBuffForLine2_B_ALLY", "Railway L2: Pierce Boost", "None", "None", "C2"),
            ("RailBuffForLine2_B_ENEMY", "Railway L2: Opportunistic", "None", "None", "C2"),
            ("RailBuffForLine2_C_ALLY", "Railway L2: Blunt Boost", "None", "None", "C2"),
            ("RailBuffForLine2_C_ENEMY", "Railway L2: Hardening", "None", "None", "C2"),
            ("RailBuffForLine2_D_ALLY", "Railway L2: Wrath Boost", "None", "None", "C2"),
            ("RailBuffForLine2_D_ENEMY", "Railway L2: Accelerating", "None", "None", "C2"),
            ("RailBuffForLine2_E_ALLY", "Railway L2: Lust Boost", "None", "None", "C2"),
            ("RailBuffForLine2_E_ENEMY", "Railway L2: Inhaling", "None", "None", "C2"),
            ("RailBuffForLine2_F_ALLY", "Railway L2: Sloth Boost", "None", "None", "C2"),
            ("RailBuffForLine2_F_ENEMY", "Railway L2: Thirsting", "None", "None", "C2"),
            ("RailBuffForLine2_G_ALLY", "Railway L2: Gluttony Boost", "None", "None", "C2"),
            ("RailBuffForLine2_H_ALLY", "Railway L2: Gloom Boost", "None", "None", "C2"),
            ("RailBuffForLine2_I_ALLY", "Railway L2: Pride Boost", "None", "None", "C2"),
            ("RailBuffForLine2_J_ALLY", "Railway L2: Envy Boost", "None", "None", "C2"),
            ("RailBuffForLine2_K_ALLY", "Railway L2: Heal All", "None", "None", "C2"),
            ("RailBuffForLine2_L_ALLY", "Railway L2: Heal Selected", "None", "None", "C2"),
            ("RailBuffForLine2_M_ALLY", "Railway L2: Burn Boost", "None", "None", "C2"),
            ("RailBuffForLine2_N_ALLY", "Railway L2: Bleed Boost", "None", "None", "C2"),
            ("RailBuffForLine2_O_ALLY", "Railway L2: Tremor Boost", "None", "None", "C2"),
            ("RailBuffForLine2_P_ALLY", "Railway L2: Rupture Boost", "None", "None", "C2"),
            ("RailBuffForLine2_Q_ALLY", "Railway L2: Sinking Boost", "None", "None", "C2"),
            ("RailBuffForLine2_R_ALLY", "Railway L2: Poise Boost", "None", "None", "C2"),
            ("RailBuffForLine2_S_ALLY", "Railway L2: Charge Boost", "None", "None", "C2"),
            ("RailBuffForLine2_T_ALLY", "Railway L2: Resurrect", "None", "None", "C2"),
            // C3
            ("DuelDeclaration", "Declared Duel", "None", "None", "C3"),
            ("RunSinclair", "Escape Apparatus", "None", "None", "C3"),
            ("StudyStatSinclair_C", "Thrashing to Hatch III", "None", "None", "C3"),
            // C4
            ("CaseOverheating", "Overheated Coffin", "None", "None", "C4"),
            ("StigmaWorkshopCelloCaseOne", "Fire Butterfly Coffin I", "None", "None", "C4"),
            ("StigmaWorkshopCelloCaseTwo", "Fire Butterfly Coffin II", "None", "None", "C4"),
            ("StigmaWorkshopCelloCaseThree", "Fire Butterfly Coffin III", "None", "None", "C4"),
            ("StigmaWorkshopHandStep1", "Light of Daybreak I", "None", "None", "C4"),
            ("StigmaWorkshopHandStep2", "Light of Daybreak II", "None", "None", "C4"),
            ("StigmaWorkshopHandStep3", "Light of Daybreak III", "None", "None", "C4"),
            // C5
            ("DuelEdgeAlly", "The Duel Escalates", "None", "None", "C5"),
            ("SeveredTendonAlly", "Severed Tendon (Ally)", "None", "None", "C5"),
            // C6
            ("AlcoholKimPersonal", "Drink to the Fallen", "None", "None", "C6"),
            ("BladelineagePride", "Bearing of a Salsu", "None", "None", "C6"),
            ("BongukGiftEnhance", "Sword of the Homeland - Striking", "None", "None", "C6"),
            ("BrandedKimSatgat", "Brand - No", "None", "None", "C6"),
            ("ChoiSword_LowMorale", "Developed Memories: Low Morale", "None", "None", "C6"),
            ("ChoiSword_Panic", "Developed Memories: Panic", "None", "None", "C6"),
            ("ChoiSwordsmanship", "Swordplay of the Homeland [Sal]", "None", "None", "C6"),
            ("ChoiSwordsmanshipMany", "Battleform of the Homeland - Tigerhunt", "None", "None", "C6"),
            ("ChoiSwordsmanshipOne", "Battleform of the Homeland - Bearhunt", "None", "None", "C6"),
            ("ChoiSwordsmanshipShield", "Dujeong Brigandine [Sal]", "None", "None", "C6"),
            ("GhostKimPersonal", "Resentment (Kim)", "None", "None", "C6"),
            ("GrudgePersonal", "Growing Resentment", "None", "None", "C6"),
            ("PhantomIncisionID", "Bladetrail [Afterimage]", "None", "None", "C6"),
            ("PhantomIncisionTotalID", "Dihui Star's Blade", "None", "None", "C6"),
            ("PhotoTransition", "Memory Transition", "None", "None", "C6"),
            ("PhotoTransitionChoi", "Memory Transition (Choi)", "None", "None", "C6"),
            ("PhotoTransitionChoiCamera", "Memory Transition (Camera)", "None", "None", "C6"),
            ("RestingKimSatgat_LowMorale", "Deep-rooted Regret: Low Morale", "None", "None", "C6"),
            ("RestingKimSatgat_Panic", "Deep-rooted Regret: Panic", "None", "None", "C6"),
            ("RootSoupKimPersonal", "Resentful Scabbard", "None", "None", "C6"),
            ("RootSoupKimSatgat", "Sword of the Homeland - The True Essence", "None", "None", "C6"),
            // C7
            ("Bothersome", "Severing Slash", "None", "None", "C7"),
            ("Collage_LowMorale", "Jumbled Memories: Low Morale", "None", "None", "C7"),
            ("Collage_Panic", "Jumbled Memories: Panic", "None", "None", "C7"),
            ("DuelDeclarationMeursault", "Declared Duel - Meursault", "None", "None", "C7"),
            ("FamineBloodPriestServant_LowMorale", "Hunger: Low Morale", "None", "None", "C7"),
            ("FamineBloodPriestServant_Panic", "Hunger: Panic", "None", "None", "C7"),
            ("MiddleFingerTattooID", "Vengeance Tattoo (ID)", "None", "None", "C7"),
            ("ScarletMothRodion", "Scarlet Moth (Rodion)", "None", "None", "C7"),
            // C8
            ("Aengdu_Lowmorale", "Resentment (Aengdu): Low Morale", "None", "None", "C8"),
            ("Aengdu_Panic", "Resentment (Aengdu): Panic", "None", "None", "C8"),
            ("AengduNewArm", "Dokkaebi Arms", "None", "None", "C8"),
            ("AengduNewArmIshmael", "Dokkaebi Arms (Ishmael)", "None", "None", "C8"),
            ("Blame_LowMorale", "Outlash: Low Morale", "None", "None", "C8"),
            ("Blame_Panic", "Outlash: Panic", "None", "None", "C8"),
            ("Liu_Meursault_Guard_Buff", "Great Mountain", "None", "None", "C8"),
            // C9
            ("CondensedBloodRefraction", "Dragon's Blood", "None", "None", "C9"),
            ("DimensionalBagEzraDTwo", "Dimensional Bag - Namir Workshop II", "None", "None", "C9"),
            ("DimensionalBagEzraETwo", "Dimensional Bag - YuRia Atelier II", "None", "None", "C9"),
            ("DragonLanceRefraction", "Flower-burying Wedge", "None", "None", "C9"),
            ("EvolvingManual", "Maturing Textbook", "None", "None", "C9"),
            ("HeatedWingScales", "Incandescent Scale Dust", "None", "None", "C9"),
            ("HeatedWingScalesExplain", "Incandescent Scale Dust (Info)", "None", "None", "C9"),
            ("Lensman_LowMorale", "Intelligence Obsession: Low Morale", "None", "None", "C9"),
            ("Lensman_Panic", "Intelligence Obsession: Panic", "None", "None", "C9"),
            ("LensmanNail", "Pinning Nail", "None", "None", "C9"),
            ("LensmanPower", "Investigation Order", "None", "None", "C9"),
            ("MiddleFingerLeashBuff", "Suppressed Might", "None", "None", "C9"),
            ("NoeulNageuneBuffA3", "Sunset Wayfarer's Sap 3", "None", "None", "C9"),
            ("NoeulNageuneBuffA4", "Sunset Wayfarer's Sap 4", "None", "None", "C9"),
            ("NoeulNageuneBuffA5", "Sunset Wayfarer's Sap 5", "None", "None", "C9"),
            ("NoeulNageuneBuffB3", "Sunset Wayfarer's Moth 3", "None", "None", "C9"),
            ("NoeulNageuneBuffB4", "Sunset Wayfarer's Moth 4", "None", "None", "C9"),
            ("NoeulNageuneBuffB5", "Sunset Wayfarer's Moth 5", "None", "None", "C9"),
            ("ObservationTheRingsRail", "Viewing the Tableau (Rail)", "None", "None", "C9"),
            ("PainfulScarRailway_LowMorale", "Imitation of a Father: Low Morale", "None", "None", "C9"),
            ("PainfulScarRailway_Panic", "Imitation of a Father: Panic", "None", "None", "C9"),
            ("RegretSCorp", "Headsplitting Memory", "None", "None", "C9"),
            ("RustedWingScales", "Rusted Scale Dust", "None", "None", "C9"),
            ("RustedWingScalesExplain", "Rusted Scale Dust (Info)", "None", "None", "C9"),
            ("ScarBygoneDaysRefraction_LowMorale", "Affection and Resentment: Low Morale", "None", "None", "C9"),
            ("ScarBygoneDaysRefraction_Panic", "Affection and Resentment: Panic", "None", "None", "C9"),
            ("ScratchesHurt", "Avulsion Wound", "None", "None", "C9"),
            ("ScratchesHurtID", "Avulsion Wound (ID)", "None", "None", "C9"),
            ("SlaveHunersPower", "Prisoner Transport Order", "None", "None", "C9"),
            ("StackFuture", "In the Future (Deactivated)", "None", "None", "C9"),
            ("StackFutureActivate", "In the Future", "None", "None", "C9"),
            ("StackPast", "In the Past (Deactivated)", "None", "None", "C9"),
            ("StackPastActivate", "In the Past", "None", "None", "C9"),
            ("StackPresent", "In the Present (Deactivated)", "None", "None", "C9"),
            ("StackPresentActivate", "In the Present", "None", "None", "C9"),
            ("TagGameBuff", "\"It\"", "None", "None", "C9"),
            ("TakePicture", "Intelligence Gathering", "None", "None", "C9"),
            ("TiedRope", "Trussed", "None", "None", "C9"),
            ("TiedRopeHonglu", "Trussed [Hong Lu]", "None", "None", "C9"),
            ("TiedWithRope", "Prisoner Transport Arrangements", "None", "None", "C9"),
            ("TiedWithRopeID", "Prisoner Transport Arrangements (ID)", "None", "None", "C9"),
            ("TimeGap", "Temporal Disjunction", "None", "None", "C9"),
            ("TransparentWingScales", "Acuate Scale Dust", "None", "None", "C9"),
            ("TransparentWingScalesExplain", "Acuate Scale Dust (Info)", "None", "None", "C9"),
            ("WindBlade", "Mook Workshop Bladedraw [Ye]", "None", "None", "C9"),
            ("WindBladeIshmael", "Mook Workshop Bladedraw [Ye] (Ishmael)", "None", "None", "C9"),
            ("YesdragonPower", "Dragon's Spirit", "None", "None", "C9"),
            ("YesdragonSelfRefraction", "Flower-burying Pin", "None", "None", "C9"),
            ("YesdragonScale", "Scaleclad", "None", "None", "C9"),
            ("YesdragonWind", "Capricious Mercy", "None", "None", "C9"),
            // EGO
            ("RepressedMurderousIntendHard", "Repressed Bloodthirst", "None", "None", "EGO"),
            ("VengeanceBookHeathcliff", "Book of Vengeance [Heathcliff]", "None", "None", "EGO"),
            ("WeighedMurderousIntendHard", "Crushed Bloodthirst", "None", "None", "EGO"),
            // Shin
            ("ObsessionAndGreedRodion", "Shin - Disgrace (Rodion)", "None", "None", "Shin"),
            ("SojiRyoshuShin", "Shin - Dihui Star", "None", "None", "Shin"),
            // Boss
            ("CountSkillMidOutis", "Rising Fever", "None", "None", "Boss"),
            ("DaughterEducationSpiderOutis", "Check This Out, Kiddo!", "None", "None", "Boss"),
            ("FavorBuff5001AllyFirst", "Shade of Reflection [The Middle]", "None", "None", "Boss"),
            ("FavorBuff5001AllyTwo", "Shade of Reflection [Ambuscade]", "None", "None", "Boss"),
            ("FavorBuff5001EnemyFirst", "Shade of Reflection [The Middle]", "None", "None", "Boss"),
            ("FavorBuff5001EnemySecond", "Shade of Reflection [Ambuscade]", "None", "None", "Boss"),
            ("FavorBuff5001FirstPhaseForUI", "Shade of Reflection [The Middle] UI", "None", "None", "Boss"),
            ("FavorBuff5001Hard", "Tribulation", "None", "None", "Boss"),
            ("FavorBuff5001HardForUI", "Tribulation UI", "None", "None", "Boss"),
            ("FavorBuff5001HardTwo", "Tribulation 2", "None", "None", "Boss"),
            ("FavorBuff5001SecondPhaseForUI", "Shade of Reflection [Ambuscade] UI", "None", "None", "Boss"),
            ("FavorBuff5012AllyOne", "Shade of Reflection [Bearhunt]", "None", "None", "Boss"),
            ("FavorBuff5012AllyOneForUI", "Shade of Reflection [Bearhunt] UI", "None", "None", "Boss"),
            ("FavorBuff5012AllyTwo", "Shade of Reflection [Tigerhunt]", "None", "None", "Boss"),
            ("FavorBuff5012AllyTwoForUI", "Shade of Reflection [Tigerhunt] UI", "None", "None", "Boss"),
            ("FavorBuff5012EnemyOne", "Shade of Reflection [Bearhunt] (Enemy)", "None", "None", "Boss"),
            ("FavorBuff5012HardOne", "Tribulation", "None", "None", "Boss"),
            ("FavorBuff5012HardTwo", "Tribulation 2", "None", "None", "Boss"),
            ("FavorBuff5012HardTwoForUI", "Tribulation 2 UI", "None", "None", "Boss"),
            ("FavorBuffTwth", "Shade of Reflection [Rouge]", "None", "None", "Boss"),
            ("FavorBuffTwth1stPhaseForUI", "Shade of Reflection [Rouge] UI", "None", "None", "Boss"),
            ("FavorBuffTwth2ndPhaseForUI", "Shade of Reflection [Bleu] UI", "None", "None", "Boss"),
            ("FavorBuffTwthEnemy", "Shade of Reflection [Rouge] (Enemy)", "None", "None", "Boss"),
            ("FavorBuffTwthEnemyTwo", "Shade of Reflection [Bleu] (Enemy)", "None", "None", "Boss"),
            ("FavorBuffTwthHard", "Tribulation", "None", "None", "Boss"),
            ("FavorBuffTwthHardForUI", "Tribulation UI", "None", "None", "Boss"),
            ("FavorBuffTwthHardTwo", "Tribulation 2", "None", "None", "Boss"),
            ("FavorBuffTwthTwo", "Shade of Reflection [Bleu]", "None", "None", "Boss"),
            ("GotAComplimentIshmael", "I Got Praised! (Ishmael)", "None", "None", "Boss"),
            ("MiddleFatherGutsOutis", "Ridiculous Grit (Outis)", "None", "None", "Boss"),
            ("MiddleFatherSwordFourOutis", "Laevateinn (Outis)", "None", "None", "Boss"),
            ("MiddleFatherSwordOneOutis", "Sealed Sword (Outis)", "None", "None", "Boss"),
            ("MiddleFatherSwordThreeOutis", "Second Seal Removed (Outis)", "None", "None", "Boss"),
            ("MiddleFatherSwordTwoOutis", "First Seal Removed (Outis)", "None", "None", "Boss"),
            ("MidFamilyOutis", "Avenging My Family (Outis)", "None", "None", "Boss"),
            ("PseudoLastBoss_KingCrap", "Crab Rage", "None", "None", "Boss"),
            ("ReinforcedTattooSpiderOutis", "The Middle - Vengeance Tattoo", "None", "None", "Boss"),
            ("ResentmentSpiderOutis", "The Middle - Grudge", "None", "None", "Boss"),
            ("RetaliationBookSpiderOutis", "Book of Vengeance [House of Spiders]", "None", "None", "Boss"),
            ("RicardoBookRaid", "Written in the Book", "None", "None", "Boss"),
            ("RicardoJokeRaid", "Test of the Big Brother", "None", "None", "Boss"),
            ("RicardoTattooRaid", "Vengeance Tattoo (Raid)", "None", "None", "Boss"),
            ("SojiRyoshuEchoFuture", "Phantom Trace", "None", "None", "Boss"),
            ("SojiRyoshuEchoPast", "Entanglement", "None", "None", "Boss"),
            ("SojiRyoshuEntangle", "String Severance", "None", "None", "Boss"),
            ("TeachersPreyRodion", "Game Target (Rodion)", "None", "None", "Boss"),
            // MD
            ("KAmpouleMirror", "K Corp. Emergency Ampule", "None", "None", "MD"),
            ("PlayingWithWaterTogetherMirror", "Splish Splash", "None", "None", "MD"),
            ("RedButterfly", "Torn-out Butterfly", "None", "None", "MD"),
            ("UnfinishedDreamSanchoMirror", "Unfinished Dream (Sancho, Mirror)", "None", "None", "MD"),
            // ── Canto X (2026-09) ─────────────────────────────────────────
            // C10 — player / neutral effects
            ("DriftingDimensionYisang", "Dimensional Drift (Yi Sang)", "Positive", "None", "C10"),
            ("FourDimensionYisang", "Four Dimensions (Yi Sang)", "Positive", "None", "C10"),
            ("RougeIshmaelCocoon", "Rouge: Cocoon (Ishmael)", "None", "None", "C10"),
            ("RougeIshmaelEclose", "Rouge: Eclosion (Ishmael)", "None", "None", "C10"),
            ("NoirSuitAlly", "Noir Suit (Ally)", "Positive", "None", "C10"),
            ("RealisationAlly", "Réalisation (Ally)", "Positive", "None", "C10"),
            ("ChargeRougeAlly", "Charge: Rouge (Ally)", "Positive", "SinBuff", "C10"),
            ("ChargeNoirAlly", "Charge: Noir (Ally)", "Positive", "SinBuff", "C10"),
            ("DuelSousTemoinsEast", "Witnessed Duel: East", "Positive", "None", "C10"),
            ("NoirSuit", "Noir Suit", "Positive", "None", "C10"),
            ("Realisation", "Réalisation", "Positive", "None", "C10"),
            ("ChargeNoir", "Charge: Noir", "Positive", "SinBuff", "C10"),
            ("ChargeRouge", "Charge: Rouge", "Positive", "SinBuff", "C10"),
            ("BlackSwanAwaken", "Black Swan: Awakening", "Positive", "None", "C10"),
            ("DeterioratingBody", "Deteriorating Body", "Positive", "None", "C10"),
            ("Gourmandise", "Gourmandise", "Positive", "None", "C10"),
            ("HowlingCocoon", "Howling Cocoon", "Positive", "None", "C10"),
            ("HowlingCocoonTwo", "Howling Cocoon II", "Positive", "None", "C10"),
            ("MeursaultBuffet", "Meursault: Buffet", "Positive", "None", "C10"),
            ("NoirBindArmor", "Noir: Binding Armor", "None", "None", "C10"),
            ("NoirNoComply", "Noir: Non-Compliance", "Negative", "None", "C10"),
            ("Nutrition", "Nutrition", "Positive", "None", "C10"),
            ("ReliefSense", "Relief Sense", "Positive", "None", "C10"),
            ("SavePlacenta", "Save Placenta", "Positive", "None", "C10"),
            ("UnresolvedFeelings", "Unresolved Feelings", "None", "None", "C10"),
            ("Wariness", "Wariness", "Negative", "None", "C10"),
            // C10 — food/buffet effects
            ("BloodyMucus", "Bloody Mucus", "Negative", "None", "C10"),
            ("BuffetFailed", "Buffet: Failed", "Negative", "None", "C10"),
            ("BuffetSick", "Buffet: Sick", "Negative", "None", "C10"),
            ("HorribleEat", "Horrible Eating", "Positive", "None", "C10"),
            ("HorribleTerror", "Horrible Terror", "None", "None", "C10"),
            // C10 — Rouge Three (enemy boss states)
            ("RougeThreeCocoon", "Rouge Three: Cocoon", "None", "None", "C10"),
            ("RougeThreeCocoonHit", "Rouge Three: Cocoon Hit", "None", "None", "C10"),
            ("RougeThreeCocoonShield", "Rouge Three: Cocoon Shield", "Positive", "None", "C10"),
            ("RougeThreeNormal", "Rouge Three: Normal", "None", "None", "C10"),
            // C10 — Rising Madness
            ("RisingMadnessNoir", "Rising Madness: Noir", "None", "None", "C10"),
            ("RisingMadnessNoir2nd", "Rising Madness: Noir II", "None", "None", "C10"),
            ("RisingMadnessRouge", "Rising Madness: Rouge", "None", "None", "C10"),
            ("RisingMadnessRouge2nd", "Rising Madness: Rouge II", "None", "None", "C10"),
            ("RisingMadnessSisyphe", "Rising Madness: Sisyphe", "None", "None", "C10"),
            ("RisingMadnessSisyphe2nd", "Rising Madness: Sisyphe II", "None", "None", "C10"),
            // C10 — Sapsaree
            ("SapsareeCooking", "Sapsaree: Cooking", "Positive", "None", "C10"),
            ("SapsareeHungry", "Sapsaree: Hungry", "Positive", "None", "C10"),
            ("SapsareeShield", "Sapsaree: Shield", "Positive", "None", "C10"),
            ("SapsareeSpices", "Sapsaree: Spices", "Negative", "None", "C10"),
            ("SapsareeYammi", "Sapsaree: Yummy", "Positive", "None", "C10"),
            // C10 — Sisyphus gems
            ("SisyphusAmber", "Sisyphus: Amber", "Positive", "None", "C10"),
            ("SisyphusAmethyst", "Sisyphus: Amethyst", "Positive", "None", "C10"),
            ("SisyphusEmerald", "Sisyphus: Emerald", "Positive", "None", "C10"),
            ("SisyphusGold", "Sisyphus: Gold", "Positive", "None", "C10"),
            ("SisyphusRuby", "Sisyphus: Ruby", "Positive", "None", "C10"),
            ("SisyphusTopaz", "Sisyphus: Topaz", "Positive", "None", "C10"),
            // EGO (Needle extensions)
            ("DianxueHongLu", "Dianxue (Hong Lu)", "Negative", "None", "EGO"),
            ("NiddleExpected", "Needle: Expected", "Negative", "None", "EGO"),
            ("NiddlePin", "Needle: Pin", "Negative", "None", "EGO"),
            ("NiddlePinned", "Needle: Pinned", "Negative", "None", "EGO"),
            // Boss — Leiheng (Canto X boss)
            ("FavorBuffLeiheng", "Shade of Reflection [Leiheng]", "Positive", "None", "Boss"),
            ("FavorBuffLeiheng1stPhaseForUI", "Shade of Reflection [Leiheng] UI I", "None", "None", "Boss"),
            ("FavorBuffLeiheng2ndPhaseForUI", "Shade of Reflection [Leiheng] UI II", "None", "None", "Boss"),
            ("FavorBuffLeihengHard", "Tribulation [Leiheng]", "Positive", "None", "Boss"),
            ("FavorBuffLeihengHardForUI", "Tribulation [Leiheng] UI", "None", "None", "Boss"),
            ("FavorBuffLeihengHardTwo", "Tribulation [Leiheng] II", "Positive", "None", "Boss"),
            ("FavorBuffLeihengTwo", "Shade of Reflection [Leiheng] II", "Positive", "None", "Boss"),
            ("Prey_leiheng", "Prey (Leiheng)", "Negative", "None", "Boss"),
            ("FavorBuff5012HardOneForUI", "Shade [Bearhunt] Hard UI", "None", "None", "Boss"),
            // Shin
            ("HugeIrritationReflectrial", "Shin: Tiantu Star (Retrial)", "Positive", "None", "Shin"),
            // C2 — BokGak stage
            ("InterlockingTimeBokGak", "Interlocking Time (BokGak)", "Negative", "None", "C2"),
            ("PassedPortal_GreenBokGak", "Passed Portal: Boiling Poison", "None", "None", "C2"),
            // MD — Refracted Mirror additional E/P variants
            ("MRR546E", "Refracted: 546 (Effect)", "Positive", "None", "MD"),
            ("MRR546P", "Refracted: 546 (Positive)", "Positive", "None", "MD"),
            ("MRR558E", "Refracted: 558 (Effect)", "Positive", "None", "MD"),
            ("MRR558P", "Refracted: 558 (Positive)", "Positive", "None", "MD"),
            ("MRR559E", "Refracted: 559 (Effect)", "Positive", "None", "MD"),
            ("MRR559P", "Refracted: 559 (Positive)", "Positive", "None", "MD"),
            ("MRR560E", "Refracted: 560 (Effect)", "Positive", "None", "MD"),
            ("MRR560P", "Refracted: 560 (Positive)", "Positive", "None", "MD"),
            ("MRR561E", "Refracted: 561 (Effect)", "Positive", "None", "MD"),
            ("MRR561P", "Refracted: 561 (Positive)", "Positive", "None", "MD"),
            ("MRR562E", "Refracted: 562 (Effect)", "Positive", "None", "MD"),
            ("MRR562P", "Refracted: 562 (Positive)", "Positive", "None", "MD"),
            ("MRR563E", "Refracted: 563 (Effect)", "Positive", "None", "MD"),
            ("MRR563P", "Refracted: 563 (Positive)", "Positive", "None", "MD"),
            ("MRR564E", "Refracted: 564 (Effect)", "Positive", "None", "MD"),
            ("MRR564P", "Refracted: 564 (Positive)", "Positive", "None", "MD"),
        };

        // Ability tab
        private string _abilitySearch = "";
        private int    _abilityPage   = 0;

        private readonly (string id, string desc)[] _allAbilities = {
            ("DefenseAdder",                          "Defense Level +stack"),
            ("ParryingResultAdder",                   "Clash Power +stack"),
            ("ParryingResultAdderIfFasterThanTarget", "Clash Power +stack if faster"),
            ("MaxHpUpMultiplier",                     "Max HP multiplier +stack%"),
            ("MaxHpUpAdder",                          "Max HP +stack"),
            ("MaxSpeedAdder",                         "Max Speed +stack"),
            ("MinSpeedAdder",                         "Min Speed +stack"),
            ("EgoResourceAdder",                      "E.G.O Resource +stack"),
            ("MpUsageByEgoDown",                      "E.G.O Resource Cost Down"),
            ("MpUsageByEgoUp",                        "E.G.O Resource Cost Up"),
            ("MentalSystemResultIncreaseUp",          "SP Recovery Amount Up"),
            ("MentalSystemResultIncreaseDown",        "SP Recovery Amount Down"),
            ("MentalSystemResultDecreaseUp",          "SP Loss Amount Up"),
            ("MentalSystemResultDecreaseDown",        "SP Loss Amount Down"),
            ("ForceHeadOnAllCoinInAllSlots",          "Force All Coins Heads"),
            ("ForceTailOnParrying",                   "Force Clash Coin Tails"),
            ("ForceHeadOnParrying",                   "Force Clash Coin Heads"),
            ("ForceOpponentHeadOnParrying",           "Force Opponent Clash Heads"),
            ("ForceOpponentTailOnParrying",           "Force Opponent Clash Tails"),
            ("AttackFastestEnemy",                    "Attack Fastest Enemy"),
            ("AttackSlowestEnemy",                    "Attack Slowest Enemy"),
            ("Shield_NextTurn",                       "Shield Next Turn +stack"),
            ("Immortal",                              "Immortal (instant kill immunity)"),
            ("Immortal_If_Not_Alone",                 "Immortal if not alone"),
            ("TakeBsDmgMultiplier",                   "Stagger Damage Multiplier"),
            ("AttackDmgupByStackRatio",               "Atk Dmg Up by Stack Ratio"),
            ("SystemAbility_TakeDamageMultiplier",    "Damage Taken Multiplier"),
            ("BlockMentalCorrision",                  "Block Corrosion"),
            ("SystemAbility_CantRetreat",             "Cannot Retreat"),
            ("IsTargetableFalse",                     "Cannot be Targeted"),
            ("IsActionableFalse",                     "Cannot Act"),
            ("BreakOnRoundEnd",                       "Stagger on Round End"),
            ("ReactiveShield_VibrationExplosion",     "Shield on Tremor Explosion"),
            ("ReactiveShield_SinkingTurn",            "Shield on Sinking Stack"),
            ("KCorpHongluPassive",                    "K Corp. Honglu Passive"),
            ("RCorpMeursaultDefense",                 "R Corp. Meursault Defense"),
            ("CumulativeLacerationSystem",            "Cumulative Bleed System"),
        };

        private const int PAGE_SIZE = 12;

        // ── Window ───────────────────────────────────────────────────────
        private Rect    _windowRect = new Rect(20, 20, 660, 820);
        private bool    _isDragging = false;
        private Vector2 _dragOffset = Vector2.zero;

        // ── Init ─────────────────────────────────────────────────────────
        public InjectorUI(IntPtr ptr) : base(ptr) { }
        private void Start() { _instance = this; }

        // ── [NEW] Harmony 콜백: StartRound Postfix에서 호출 ─────────────
        /// <summary>StageController.StartRound가 실행될 때마다 Harmony Postfix에서 호출됩니다.</summary>
        internal void OnRoundStarted()
        {
            if (_persistList.Count == 0) return;
            LimbusInjectorPlugin.Log?.LogInfo("[LimbusInjector] OnRoundStarted → TickPersistentBuffs");
            TickPersistentBuffs();
        }

        // ── Update ───────────────────────────────────────────────────────
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
                _showPanel = !_showPanel;
            // 라운드 감지는 Harmony 패치(OnRoundStarted)가 담당하므로
            // Update에서는 폴링을 수행하지 않습니다.
        }

        // ── GUI ──────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!_showPanel) return;

            GUI.Box(_windowRect, "LimbusInjector ver.2.2.0");

            var titleBar = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, 20);
            var e = Event.current;
            if (e.type == EventType.MouseDown && titleBar.Contains(e.mousePosition))
            { _isDragging = true; _dragOffset = new Vector2(_windowRect.x - e.mousePosition.x, _windowRect.y - e.mousePosition.y); e.Use(); }
            else if (e.type == EventType.MouseDrag && _isDragging)
            { _windowRect.x = e.mousePosition.x + _dragOffset.x; _windowRect.y = e.mousePosition.y + _dragOffset.y; e.Use(); }
            else if (e.type == EventType.MouseUp) { _isDragging = false; }

            GUILayout.BeginArea(new Rect(_windowRect.x + 5, _windowRect.y + 22, _windowRect.width - 10, _windowRect.height - 27));

            DrawFactionAndUnits();
            GUILayout.Space(3);
            DrawTabBar();
            GUILayout.Space(3);

            if      (_activeTab == 0) DrawBuffTab();
            else if (_activeTab == 1) DrawAbilityTab();
            else                      DrawInspectTab();

            GUILayout.Space(4);
            // Inspect 탭 밖에서는 등록 수만 표시 (상세 관리는 Inspect 탭으로)
            if (_activeTab != 2 && _persistList.Count > 0)
                GUILayout.Label($"⚠  {_persistList.Count} active persist schedule(s) — manage in Inspect tab");
            GUILayout.Space(2);
            GUILayout.Label(string.IsNullOrEmpty(_status) ? " " : _status);
            GUILayout.EndArea();
        }

        // ── Faction + Multi-Unit Selection ───────────────────────────────
        private void DrawFactionAndUnits()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Faction:", GUILayout.Width(55));
            if (GUILayout.Toggle(_faction == UNIT_FACTION.PLAYER, "Player", "Button", GUILayout.Width(95)))
            {
                if (_faction != UNIT_FACTION.PLAYER)
                { _faction = UNIT_FACTION.PLAYER; _selectedUnits.Clear(); _selectedNames.Clear(); }
            }
            if (GUILayout.Toggle(_faction == UNIT_FACTION.ENEMY, "Enemy", "Button", GUILayout.Width(95)))
            {
                if (_faction != UNIT_FACTION.ENEMY)
                { _faction = UNIT_FACTION.ENEMY; _selectedUnits.Clear(); _selectedNames.Clear(); }
            }
            GUILayout.Label("", GUILayout.Width(20));
            if (GUILayout.Button("All",  GUILayout.Width(48))) SelectAll();
            if (GUILayout.Button("None", GUILayout.Width(50))) { _selectedUnits.Clear(); _selectedNames.Clear(); }
            GUILayout.EndHorizontal();

            GUILayout.Label("── Units ──");
            var bom = FindObjectOfType<BattleObjectManager>();
            if (bom == null) { GUILayout.Label("[Not in battle]"); return; }

            var listMethod = typeof(BattleObjectManager).GetMethod("GetModelList",
                new System.Type[]{ typeof(UNIT_FACTION), typeof(bool) });
            if (listMethod == null) { GUILayout.Label("[GetModelList missing]"); return; }

            var result    = listMethod.Invoke(bom, new object[]{ _faction, false })!;
            var countProp = result.GetType().GetProperty("Count")!;
            var indexer   = result.GetType().GetProperty("Item")!;
            int count     = (int)countProp.GetValue(result)!;

            if (count == 0) { GUILayout.Label("[No units]"); return; }

            GUILayout.BeginHorizontal();
            for (int i = 0; i < count; i++)
            {
                var unit    = indexer.GetValue(result, new object[]{ i })!;
                var wasProp = unit.GetType().GetProperty("WasCollected")!;
                if ((bool)wasProp.GetValue(unit)!) continue;

                var idProp     = unit.GetType().GetProperty("InstanceID")!;
                int instanceID = (int)idProp.GetValue(unit)!;
                var nameMethod = unit.GetType().GetMethod("GetUniqueName");
                string name    = nameMethod?.Invoke(unit, null) as string ?? $"ID:{instanceID}";

                bool isSel = _selectedUnits.ContainsKey(instanceID);
                string label = isSel ? $"[{name}]" : name;

                if (GUILayout.Button(label, GUILayout.MaxWidth(105)))
                {
                    if (isSel) { _selectedUnits.Remove(instanceID); _selectedNames.Remove(instanceID); }
                    else       { _selectedUnits[instanceID] = unit;  _selectedNames[instanceID] = name; }
                }
            }
            GUILayout.EndHorizontal();

            int selCount = _selectedUnits.Count;
            GUILayout.Label(selCount == 0 ? "No units selected" : $"{selCount} selected: {string.Join(", ", _selectedNames.Values)}");
        }

        private void SelectAll()
        {
            var bom = FindObjectOfType<BattleObjectManager>();
            if (bom == null) return;
            var listMethod = typeof(BattleObjectManager).GetMethod("GetModelList",
                new System.Type[]{ typeof(UNIT_FACTION), typeof(bool) });
            if (listMethod == null) return;
            var result  = listMethod.Invoke(bom, new object[]{ _faction, false })!;
            var countProp = result.GetType().GetProperty("Count")!;
            var indexer   = result.GetType().GetProperty("Item")!;
            int count = (int)countProp.GetValue(result)!;
            for (int i = 0; i < count; i++)
            {
                var unit = indexer.GetValue(result, new object[]{ i })!;
                var wasProp = unit.GetType().GetProperty("WasCollected")!;
                if ((bool)wasProp.GetValue(unit)!) continue;
                var idProp = unit.GetType().GetProperty("InstanceID")!;
                int instanceID = (int)idProp.GetValue(unit)!;
                var nameMethod = unit.GetType().GetMethod("GetUniqueName");
                string name = nameMethod?.Invoke(unit, null) as string ?? $"ID:{instanceID}";
                _selectedUnits[instanceID] = unit;
                _selectedNames[instanceID] = name;
            }
        }

        // ── Tab Bar ──────────────────────────────────────────────────────
        private void DrawTabBar()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_activeTab == 0, "Buff",    "Button", GUILayout.Width(150))) _activeTab = 0;
            if (GUILayout.Toggle(_activeTab == 1, "Ability", "Button", GUILayout.Width(150))) _activeTab = 1;
            if (GUILayout.Toggle(_activeTab == 2, "Inspect", "Button", GUILayout.Width(150))) _activeTab = 2;
            GUILayout.EndHorizontal();
        }

        // ── [NEW] Stack / Turn / Persist 입력 공통 행 ────────────────────
        private void DrawParamRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Stack:",   GUILayout.Width(45));
            _stackInput   = GUILayout.TextField(_stackInput,   GUILayout.Width(44));
            GUILayout.Label("Turn:",    GUILayout.Width(38));
            _turnInput    = GUILayout.TextField(_turnInput,    GUILayout.Width(44));
            // [NEW] Persist 입력: 0이면 1회 부여, N이면 매 라운드 N번 재부여
            GUILayout.Label("Persist:", GUILayout.Width(52));
            _persistInput = GUILayout.TextField(_persistInput, GUILayout.Width(44));
            if (GUILayout.Button("↺", GUILayout.Width(26))) _persistInput = "0";
            GUILayout.Label("(rounds to re-apply, 0=once)", GUILayout.Width(185));
            GUILayout.EndHorizontal();
        }

        // ── Buff Tab ─────────────────────────────────────────────────────
        private void DrawBuffTab()
        {
            DrawParamRow();  // [CHANGED] 공통 파라미터 행
            GUILayout.Space(2);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < _typeLabels.Length; i++)
            {
                int idx = i;
                if (GUILayout.Toggle(_buffTypeIdx == idx, _typeLabels[i], "Button", GUILayout.Width(100)))
                    if (_buffTypeIdx != idx) { _buffTypeIdx = idx; _buffPage = 0; }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(1);

            GUILayout.BeginHorizontal();
            for (int i = 0; i <= 6; i++)
            {
                int idx = i;
                float w = (i == 0 || i == 1) ? 40f : 36f;
                if (GUILayout.Toggle(_buffCatIdx == idx, _catLabels[i], "Button", GUILayout.Width(w)))
                    if (_buffCatIdx != idx) { _buffCatIdx = idx; _buffPage = 0; }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for (int i = 7; i < _catLabels.Length; i++)
            {
                int idx = i;
                float w = (i <= 10) ? 36f : (i == 11) ? 44f : (i == 12) ? 44f : (i == 13) ? 42f : 54f;
                if (GUILayout.Toggle(_buffCatIdx == idx, _catLabels[i], "Button", GUILayout.Width(w)))
                    if (_buffCatIdx != idx) { _buffCatIdx = idx; _buffPage = 0; }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50));
            string ns = GUILayout.TextField(_buffSearch, GUILayout.Width(360));
            if (ns != _buffSearch) { _buffSearch = ns; _buffPage = 0; }
            if (GUILayout.Button("X", GUILayout.Width(28))) { _buffSearch = ""; _buffPage = 0; }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);

            string q = _buffSearch.Trim().ToLowerInvariant();
            string catKey = _catKeys[_buffCatIdx];
            var filtered = new List<(string id, string en, string buffType, string buffClass, string cat)>();
            foreach (var kw in _allBuffKeywords)
            {
                bool typeMatch = _buffTypeIdx == 0
                    || (_buffTypeIdx == 1 && kw.buffType == "Positive")
                    || (_buffTypeIdx == 2 && kw.buffType == "Negative")
                    || (_buffTypeIdx == 3 && (kw.buffClass == "SinBuff" || kw.buffClass == "CollapsableSinBuff"))
                    || (_buffTypeIdx == 4 && kw.buffType == "None" && kw.buffClass != "SinBuff" && kw.buffClass != "CollapsableSinBuff");
                bool catMatch  = string.IsNullOrEmpty(catKey) || kw.cat == catKey;
                bool srchMatch = string.IsNullOrEmpty(q)
                    || kw.id.ToLowerInvariant().Contains(q)
                    || kw.en.ToLowerInvariant().Contains(q);
                if (typeMatch && catMatch && srchMatch) filtered.Add(kw);
            }

            GUILayout.Label($"Results: {filtered.Count}");
            GUILayout.Space(2);

            int start = _buffPage * PAGE_SIZE;
            int end   = Math.Min(start + PAGE_SIZE, filtered.Count);
            for (int i = start; i < end; i++)
            {
                var kw = filtered[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"[{kw.cat}]", GUILayout.Width(42));
                GUILayout.Label(kw.en,          GUILayout.Width(210));
                GUILayout.Label($"[{kw.id}]",   GUILayout.Width(240));
                if (GUILayout.Button("+", GUILayout.Width(32))) InjectBuff(kw.id);
                GUILayout.EndHorizontal();
            }

            DrawPageNav(filtered.Count, ref _buffPage);
        }

        // ── Ability Tab ──────────────────────────────────────────────────
        private void DrawAbilityTab()
        {
            DrawParamRow();  // [CHANGED] 공통 파라미터 행
            GUILayout.Space(2);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50));
            string ns = GUILayout.TextField(_abilitySearch, GUILayout.Width(360));
            if (ns != _abilitySearch) { _abilitySearch = ns; _abilityPage = 0; }
            if (GUILayout.Button("X", GUILayout.Width(28))) { _abilitySearch = ""; _abilityPage = 0; }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);

            string q = _abilitySearch.Trim().ToLowerInvariant();
            var filtered = new List<(string id, string desc)>();
            foreach (var e in _allAbilities)
                if (string.IsNullOrEmpty(q) || e.id.ToLowerInvariant().Contains(q) || e.desc.ToLowerInvariant().Contains(q))
                    filtered.Add(e);

            GUILayout.Label($"Results: {filtered.Count}");
            GUILayout.Space(2);

            int start = _abilityPage * PAGE_SIZE;
            int end   = Math.Min(start + PAGE_SIZE, filtered.Count);
            for (int i = start; i < end; i++)
            {
                var entry = filtered[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(entry.desc,         GUILayout.Width(320));
                GUILayout.Label($"[{entry.id}]",    GUILayout.Width(260));
                if (GUILayout.Button("+", GUILayout.Width(32))) InjectAbility(entry.id);
                GUILayout.EndHorizontal();
            }

            DrawPageNav(filtered.Count, ref _abilityPage);
        }

        // ── Inspect Tab ──────────────────────────────────────────────────
        /// <summary>
        /// Shows all active persistent buff schedules and allows removing them individually or all at once.
        /// </summary>
        private void DrawInspectTab()
        {
            if (_persistList.Count == 0)
            {
                GUILayout.Label("[No active persist schedules]");
                GUILayout.Label("Apply a buff/ability with Persist > 0 in the Buff or Ability tab to register one.");
                return;
            }

            GUILayout.Label($"── Persistent Buff Schedule [{_persistList.Count}] (auto re-applied every round start) ──");
            GUILayout.Space(2);

            // Column headers
            GUILayout.BeginHorizontal();
            GUILayout.Label("Type",         GUILayout.Width(50));
            GUILayout.Label("Buff / Ability", GUILayout.Width(200));
            GUILayout.Label("Value",         GUILayout.Width(80));
            GUILayout.Label("Rounds Left",   GUILayout.Width(90));
            GUILayout.Label("Targets",       GUILayout.Width(55));
            GUILayout.EndHorizontal();

            // Render entries directly (no ScrollView to avoid IMGUI clipping issues)
            bool removed = false;
            for (int i = _persistList.Count - 1; i >= 0; i--)
            {
                if (removed) break;
                var    pe      = _persistList[i];
                string tag     = pe.isBuff ? "[Buff]" : "[Abil]";
                string targets = $"{pe.instanceIDs.Count} unit(s)";

                GUILayout.BeginHorizontal();
                GUILayout.Label(tag,                             GUILayout.Width(50));
                GUILayout.Label(pe.buffId,                       GUILayout.Width(200));
                GUILayout.Label($"x{pe.stack} / {pe.turn}t",    GUILayout.Width(80));
                GUILayout.Label($"{pe.remainTurns} rounds left", GUILayout.Width(90));
                GUILayout.Label(targets,                         GUILayout.Width(55));
                GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
                if (GUILayout.Button("Remove", GUILayout.Width(58)))
                {
                    _persistList.RemoveAt(i);
                    _status = $"[Inspect] '{pe.buffId}' removed from schedule";
                    removed = true;
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
            if (GUILayout.Button("Clear All", GUILayout.Width(80)))
            {
                int cnt = _persistList.Count;
                _persistList.Clear();
                _status = $"[Inspect] {cnt} schedule(s) cleared";
            }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Tick Now", GUILayout.Width(80)))
                TickPersistentBuffs();
            GUILayout.EndHorizontal();
        }

        // ── Page Nav ─────────────────────────────────────────────────────
        private void DrawPageNav(int total, ref int page)
        {
            int totalPages = Math.Max(1, (total + PAGE_SIZE - 1) / PAGE_SIZE);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(42)) && page > 0) page--;
            GUILayout.Label($"{page + 1} / {totalPages}", GUILayout.Width(72));
            if (GUILayout.Button(">", GUILayout.Width(42)) && page < totalPages - 1) page++;
            GUILayout.EndHorizontal();
        }

        // ── [NEW] 지속 버프 목록 표시 ────────────────────────────────────
        private void DrawPersistList()
        {
            if (_persistList.Count == 0) return;

            GUILayout.Label($"── Active Persistent Buffs [{_persistList.Count}] ──");
            _persistScroll = GUILayout.BeginScrollView(_persistScroll, GUILayout.Height(72));
            for (int i = _persistList.Count - 1; i >= 0; i--)
            {
                var pe = _persistList[i];
                GUILayout.BeginHorizontal();
                string tag = pe.isBuff ? "B" : "A";
                GUILayout.Label($"[{tag}] {pe.buffId}  x{pe.stack}/{pe.turn}t  残{pe.remainTurns}R", GUILayout.Width(480));
                if (GUILayout.Button("X", GUILayout.Width(28)))
                { _persistList.RemoveAt(i); GUILayout.EndHorizontal(); break; }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Tick Now", GUILayout.Width(80)))
                TickPersistentBuffs();
            if (GUILayout.Button("Clear All", GUILayout.Width(80)))
                _persistList.Clear();
            if (GUILayout.Button("Dump BOM", GUILayout.Width(80)))
                DumpBomIntMembers();
            // Harmony 패치 동작 여부 표시 (라운드 시작마다 자동 갱신)
            GUILayout.Label("[Auto] StageController.StartRound hook active");
            GUILayout.EndHorizontal();
        }

        // ── [NEW] 현재 라운드 번호 조회 (리플렉션) ──────────────────────
        /// <summary>
        /// BattleObjectManager 에서 현재 라운드 번호를 반사적으로 조회합니다.
        /// 해당 프로퍼티/필드명이 패치로 변경된 경우 -1을 반환합니다.
        /// </summary>
        // 라운드 프로퍼티명 캐시 (한 번 찾으면 재스캔 생략)
        private string _cachedRoundMemberName = null;
        private bool    _cachedRoundIsProp     = true;

        private int GetCurrentRound()
        {
            var bom = FindObjectOfType<BattleObjectManager>();
            if (bom == null) return -1;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type  = bom.GetType();

            // ── 캐시 히트 ─────────────────────────────────────────────
            if (_cachedRoundMemberName != null)
            {
                try
                {
                    if (_cachedRoundIsProp)
                    {
                        var p = type.GetProperty(_cachedRoundMemberName, flags);
                        if (p != null) return (int)p.GetValue(bom)!;
                    }
                    else
                    {
                        var f = type.GetField(_cachedRoundMemberName, flags);
                        if (f != null) return (int)f.GetValue(bom)!;
                    }
                }
                catch { _cachedRoundMemberName = null; }
            }

            // ── 우선순위 1: 고정 후보명 ───────────────────────────────
            foreach (var name in new[]
            {
                "CurrentRound","currentRound","CurrentTurn","currentTurn",
                "RoundCount","roundCount","Round","_round","_currentRound",
                "TurnCount","turnCount","WaveCount","waveCount","_turn","_turnCount"
            })
            {
                var prop = type.GetProperty(name, flags);
                if (prop != null && prop.PropertyType == typeof(int))
                    try
                    {
                        int v = (int)prop.GetValue(bom)!;
                        if (v >= 0) { _cachedRoundMemberName = name; _cachedRoundIsProp = true; return v; }
                    } catch { }

                var field = type.GetField(name, flags);
                if (field != null && field.FieldType == typeof(int))
                    try
                    {
                        int v = (int)field.GetValue(bom)!;
                        if (v >= 0) { _cachedRoundMemberName = name; _cachedRoundIsProp = false; return v; }
                    } catch { }
            }

            // ── 우선순위 2: "round/turn/wave" 포함 이름 전체 스캔 ─────
            string[] kws = { "round", "turn", "wave" };
            foreach (var prop in type.GetProperties(flags))
            {
                if (prop.PropertyType != typeof(int) || !prop.CanRead) continue;
                string n = prop.Name.ToLowerInvariant();
                if (System.Array.Exists(kws, k => n.Contains(k)))
                    try
                    {
                        int v = (int)prop.GetValue(bom)!;
                        if (v > 0) { _cachedRoundMemberName = prop.Name; _cachedRoundIsProp = true; return v; }
                    } catch { }
            }
            foreach (var field in type.GetFields(flags))
            {
                if (field.FieldType != typeof(int)) continue;
                string n = field.Name.ToLowerInvariant();
                if (System.Array.Exists(kws, k => n.Contains(k)))
                    try
                    {
                        int v = (int)field.GetValue(bom)!;
                        if (v > 0) { _cachedRoundMemberName = field.Name; _cachedRoundIsProp = false; return v; }
                    } catch { }
            }
            return -1;
        }

        /// <summary>
        /// BattleObjectManager의 모든 int 멤버 이름+현재값을 BepInEx 로그에 출력합니다.
        /// GetCurrentRound()가 -1을 반환할 때 실제 프로퍼티명 파악에 사용합니다.
        /// </summary>
        private void DumpBomIntMembers()
        {
            var bom = FindObjectOfType<BattleObjectManager>();
            if (bom == null) { _status = "[Dump] BOM not found"; return; }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type  = bom.GetType();
            var sb    = new System.Text.StringBuilder("[Dump] BOM int members:\n");
            foreach (var p in type.GetProperties(flags))
            {
                if (p.PropertyType != typeof(int) || !p.CanRead) continue;
                try { sb.AppendLine($"  PROP  {p.Name} = {p.GetValue(bom)}"); } catch { }
            }
            foreach (var f in type.GetFields(flags))
            {
                if (f.FieldType != typeof(int)) continue;
                try { sb.AppendLine($"  FIELD {f.Name} = {f.GetValue(bom)}"); } catch { }
            }
            LimbusInjectorPlugin.Log?.LogInfo(sb.ToString());
            _status = "[Dump] BOM int members → check BepInEx console";
        }

        // ── [NEW] 지속 버프 틱 ──────────────────────────────────────────
        /// <summary>
        /// 라운드가 바뀔 때마다 호출되어 지속 버프를 재부여하고 카운터를 감소시킵니다.
        /// </summary>
        private void TickPersistentBuffs()
        {
            if (_persistList.Count == 0) return;

            LimbusInjectorPlugin.Log?.LogInfo($"[PersistTick] Round changed → ticking {_persistList.Count} entries");

            for (int i = _persistList.Count - 1; i >= 0; i--)
            {
                var pe = _persistList[i];
                ApplyPersistEntry(pe);

                pe.remainTurns--;
                if (pe.remainTurns <= 0)
                {
                    LimbusInjectorPlugin.Log?.LogInfo($"[PersistTick] Expired: {pe.buffId}");
                    _persistList.RemoveAt(i);
                }
                else
                {
                    _persistList[i] = pe;
                }
            }
        }

        // ── [NEW] PersistEntry 실제 적용 ────────────────────────────────
        /// <summary>
        /// BattleObjectManager 에서 유닛을 새로 조회하여 지속 버프/어빌리티를 부여합니다.
        /// (유닛 오브젝트가 라운드 사이에 교체될 수 있으므로 InstanceID로 재식별)
        /// </summary>
        private void ApplyPersistEntry(PersistEntry pe)
        {
            var bom = FindObjectOfType<BattleObjectManager>();
            if (bom == null) return;

            var listMethod = typeof(BattleObjectManager).GetMethod("GetModelList",
                new System.Type[]{ typeof(UNIT_FACTION), typeof(bool) });
            if (listMethod == null) return;

            var result    = listMethod.Invoke(bom, new object[]{ pe.faction, false })!;
            var countProp = result.GetType().GetProperty("Count")!;
            var indexer   = result.GetType().GetProperty("Item")!;
            int count     = (int)countProp.GetValue(result)!;

            for (int i = 0; i < count; i++)
            {
                var unit = indexer.GetValue(result, new object[]{ i })!;

                var wasProp = unit.GetType().GetProperty("WasCollected");
                if (wasProp != null && (bool)wasProp.GetValue(unit)!) continue;

                var idProp = unit.GetType().GetProperty("InstanceID");
                if (idProp == null) continue;
                int instanceID = (int)idProp.GetValue(unit)!;

                if (!pe.instanceIDs.Contains(instanceID)) continue;

                try
                {
                    if (pe.isBuff)
                    {
                        if (!Enum.TryParse(typeof(BUFF_UNIQUE_KEYWORD), pe.buffId, out var boxed)) continue;
                        var buffMethod = unit.GetType().GetMethod("AddBuff_NonGiver");
                        object addedStack = 0, addedTurn = 0, overStack = 0, overTurn = 0;
                        var args = new object?[]{ boxed, pe.stack, pe.turn, 0,
                            (ABILITY_SOURCE_TYPE)0, (BATTLE_EVENT_TIMING)0, null,
                            addedStack, addedTurn, overStack, overTurn };
                        buffMethod!.Invoke(unit, args);
                    }
                    else
                    {
                        if (!Enum.TryParse(typeof(SYSTEM_ABILITY_KEYWORD), pe.buffId, out var boxed)) continue;
                        var addMethod = unit.GetType().GetMethod("AddAbilityThisRound")!;
                        addMethod.Invoke(unit, new object[]{ boxed, pe.stack, pe.turn });
                    }
                }
                catch (Exception ex)
                {
                    LimbusInjectorPlugin.Log?.LogError($"[PersistApply] {pe.buffId} → ID:{instanceID} | {ex}");
                }
            }
        }

        // ── Inject ───────────────────────────────────────────────────────
        private void InjectBuff(string keywordName)
        {
            if (_selectedUnits.Count == 0)
            { _status = "[Error] No units selected."; return; }

            if (!Enum.TryParse(typeof(BUFF_UNIQUE_KEYWORD), keywordName, out var boxed))
            { _status = $"[Error] Unknown keyword: {keywordName}"; return; }

            if (!int.TryParse(_stackInput,   out int stack)   || stack   <= 0) stack   = 1;
            if (!int.TryParse(_turnInput,    out int turn)    || turn    <= 0) turn    = 3;
            if (!int.TryParse(_persistInput, out int persist) || persist < 0)  persist = 0;

            int successCount = 0;
            var failList = new List<string>();

            foreach (var kvp in _selectedUnits)
            {
                try
                {
                    var unit = kvp.Value;
                    var wasProp = unit.GetType().GetProperty("WasCollected");
                    if (wasProp != null && (bool)wasProp.GetValue(unit)!) continue;

                    var buffMethod = unit.GetType().GetMethod("AddBuff_NonGiver");
                    object addedStack = 0, addedTurn = 0, overStack = 0, overTurn = 0;
                    var args = new object?[]{ boxed, stack, turn, 0,
                        (ABILITY_SOURCE_TYPE)0, (BATTLE_EVENT_TIMING)0, null,
                        addedStack, addedTurn, overStack, overTurn };
                    buffMethod!.Invoke(unit, args);
                    successCount++;
                }
                catch (Exception ex)
                {
                    failList.Add(_selectedNames.TryGetValue(kvp.Key, out var n) ? n : $"ID:{kvp.Key}");
                    LimbusInjectorPlugin.Log?.LogError(ex.ToString());
                }
            }

            // [NEW] persist > 0 이면 지속 목록에 등록
            if (persist > 0 && successCount > 0)
            {
                _persistList.Add(new PersistEntry
                {
                    buffId      = keywordName,
                    isBuff      = true,
                    stack       = stack,
                    turn        = turn,
                    remainTurns = persist,
                    faction     = _faction,
                    instanceIDs = new HashSet<int>(_selectedUnits.Keys)
                });
                LimbusInjectorPlugin.Log?.LogInfo($"[Persist+] {keywordName} persist={persist}R registered");
            }

            _status = persist > 0
                ? $"[Buff+Persist] {keywordName} x{stack}/{turn}t ×{persist}R → {successCount}/{_selectedUnits.Count}"
                : $"[Buff] {keywordName} x{stack}/{turn}t → {successCount}/{_selectedUnits.Count}";
            if (failList.Count > 0) _status += $" | Failed: {string.Join(", ", failList)}";
            LimbusInjectorPlugin.Log?.LogInfo(_status);
        }

        private void InjectAbility(string abilityId)
        {
            if (_selectedUnits.Count == 0)
            { _status = "[Error] No units selected."; return; }

            if (!Enum.TryParse(typeof(SYSTEM_ABILITY_KEYWORD), abilityId, out var boxed))
            { _status = $"[Error] Unknown keyword: {abilityId}"; return; }

            if (!int.TryParse(_stackInput,   out int stack)   || stack   <= 0) stack   = 1;
            if (!int.TryParse(_turnInput,    out int turn)    || turn    <= 0) turn    = 3;
            if (!int.TryParse(_persistInput, out int persist) || persist < 0)  persist = 0;

            int successCount = 0;
            foreach (var kvp in _selectedUnits)
            {
                try
                {
                    var unit = kvp.Value;
                    var wasProp = unit.GetType().GetProperty("WasCollected");
                    if (wasProp != null && (bool)wasProp.GetValue(unit)!) continue;
                    var addMethod = unit.GetType().GetMethod("AddAbilityThisRound")!;
                    addMethod.Invoke(unit, new object[]{ boxed, stack, turn });
                    successCount++;
                }
                catch (Exception ex) { LimbusInjectorPlugin.Log?.LogError(ex.ToString()); }
            }

            // [NEW] persist > 0 이면 지속 목록에 등록
            if (persist > 0 && successCount > 0)
            {
                _persistList.Add(new PersistEntry
                {
                    buffId      = abilityId,
                    isBuff      = false,
                    stack       = stack,
                    turn        = turn,
                    remainTurns = persist,
                    faction     = _faction,
                    instanceIDs = new HashSet<int>(_selectedUnits.Keys)
                });
            }

            _status = persist > 0
                ? $"[Abil+Persist] {abilityId} x{stack}/{turn}t ×{persist}R → {successCount}/{_selectedUnits.Count}"
                : $"[Ability] {abilityId} x{stack}/{turn}t → {successCount}/{_selectedUnits.Count}";
            LimbusInjectorPlugin.Log?.LogInfo(_status);
        }
    }
}
using System;
using System.Collections;
using System.Reflection;
using SandBox.Missions.MissionLogics;
using SandBox.Missions.MissionLogics.Hideout;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ReinforcementTracker
{
    /// <summary>
    /// Hideout assaults don't use the normal battle spawn logic, so their second stage is read from the hideout
    /// controllers instead (SandBox, game v1.5.7, read from the IL):
    ///
    /// Classic assault (HideoutMissionController):
    ///  - Only some bandits spawn at first. Once every bandit on the field is down, the game waits 4 s, plays the
    ///    boss cutscene and spawns the boss plus every bandit that hasn't spawned yet (the supplier's
    ///    NumTroopsNotSupplied). Your side gets no reinforcements.
    ///
    /// Stealth hideout (HideoutAmbushMissionController):
    ///  - Sentries are drawn from the bandit pool. When no bandits are left on the field, the second stage spawns
    ///    the rest of the pool, topped up with random bandits to at least
    ///    clamp(initial population / 2, 4, 20), plus the boss.
    ///  - Your selected troops wait outside, split between the hideout's areas, and come in when you use an
    ///    area's signal point while that area isn't in combat (StealthAreaMissionLogic.AllyTroops lists every
    ///    ally that has come in).
    /// </summary>
    public sealed class HideoutForecast
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;

        // Classic
        private static readonly Type ClassicType = typeof(HideoutMissionController);
        private static readonly FieldInfo ClassicState = ClassicType.GetField("_hideoutMissionState", Private);
        private static readonly FieldInfo ClassicSides = ClassicType.GetField("_missionSides", Private);
        private static readonly FieldInfo ClassicBoss = ClassicType.GetField("_bossAgent", Private);
        private static readonly Type ClassicSideType = ClassicType.GetNestedType("MissionSide", BindingFlags.NonPublic);
        private static readonly FieldInfo SideIsPlayer = ClassicSideType?.GetField("IsPlayerSide", Public);
        private static readonly PropertyInfo SideNotSupplied = ClassicSideType?.GetProperty("NumberOfTroopsNotSupplied", Public);

        // Stealth
        private static readonly Type AmbushType = typeof(HideoutAmbushMissionController);
        private static readonly FieldInfo AmbushState = AmbushType.GetField("_currentHideoutMissionState", Private);
        private static readonly FieldInfo AmbushPool = AmbushType.GetField("_allEnemyTroops", Private);
        private static readonly FieldInfo AmbushBossOrigin = AmbushType.GetField("_overriddenHideoutBossAgentOrigin", Private);
        private static readonly FieldInfo AmbushPopulation = AmbushType.GetField("_initialHideoutPopulation", Private);
        private static readonly FieldInfo AmbushPlayerTroops = AmbushType.GetField("_playerPriorTroops", Private);
        private static readonly FieldInfo AmbushBoss = AmbushType.GetField("_bossAgent", Private);

        // Values of the controllers' private HideoutMissionState enums.
        private const int ClassicNotDecided = 0, ClassicWithoutBossFight = 1, ClassicCutScene = 3, ClassicConversation = 4;
        private const int StealthNotDecided = 0, StealthCutScene = 4, StealthConversation = 5;

        private readonly HideoutMissionController _classic;
        private readonly HideoutAmbushMissionController _ambush;
        private readonly StealthAreaMissionLogic _stealthAreas;

        private int _playerMaxTotal, _banditMaxTotal;
        private int _firstPhaseMaxOnField;

        private HideoutForecast(HideoutMissionController classic, HideoutAmbushMissionController ambush, StealthAreaMissionLogic areas)
        {
            _classic = classic;
            _ambush = ambush;
            _stealthAreas = areas;
        }

        public ForecastKind HideoutKind => _classic != null ? ForecastKind.ClassicHideout : ForecastKind.StealthHideout;

        /// <summary>False if a game update renamed the private fields this relies on.</summary>
        public bool ReflectionOk => _classic != null
            ? ClassicState != null && ClassicSides != null && SideIsPlayer != null && SideNotSupplied != null
            : AmbushState != null && AmbushPool != null && AmbushPopulation != null;

        public static HideoutForecast TryCreate(Mission mission)
        {
            if (mission == null) return null;
            var classic = mission.GetMissionBehavior<HideoutMissionController>();
            var ambush = classic == null ? mission.GetMissionBehavior<HideoutAmbushMissionController>() : null;
            if (classic == null && ambush == null) return null;
            return new HideoutForecast(classic, ambush, mission.GetMissionBehavior<StealthAreaMissionLogic>());
        }

        public SideForecast Compute(Mission mission, BattleSideEnum side, bool isPlayerSide)
        {
            var forecast = new SideForecast { Side = side, Kind = HideoutKind };
            forecast.OnField = BattleSides.CountActiveHumans(mission, side);
            if (!ReflectionOk)
            {
                forecast.Kind = ForecastKind.LiveCountOnly;
                forecast.Status = WaveStatus.NoSpawnLogic;
                forecast.Starting = forecast.OnField;
                return forecast;
            }

            if (_classic != null) ComputeClassic(forecast, isPlayerSide);
            else ComputeStealth(forecast, isPlayerSide);

            // Hideouts don't report a starting strength, so use the largest total seen so far.
            if (isPlayerSide) forecast.Starting = _playerMaxTotal = Math.Max(_playerMaxTotal, forecast.Total);
            else forecast.Starting = _banditMaxTotal = Math.Max(_banditMaxTotal, forecast.Total);
            return forecast;
        }

        private void ComputeClassic(SideForecast forecast, bool isPlayerSide)
        {
            int state = ToInt(ClassicState.GetValue(_classic));
            if (isPlayerSide)
            {
                // Only the troops you picked fight; the rest of your party never joins a hideout fight.
                forecast.Status = state == ClassicNotDecided ? WaveStatus.Deploying : WaveStatus.NoReinforcements;
                return;
            }

            bool bossSpawned = ClassicBoss?.GetValue(_classic) != null;
            int waiting = 0;
            if (!bossSpawned && state != ClassicWithoutBossFight && ClassicSides.GetValue(_classic) is Array sides)
            {
                foreach (object s in sides)
                {
                    if (s == null || (bool)SideIsPlayer.GetValue(s)) continue;
                    waiting += Math.Max(0, (int)SideNotSupplied.GetValue(s));
                }
            }
            forecast.Reserves = waiting;
            forecast.NextWave = waiting;
            forecast.WavesLeft = waiting > 0 ? 1 : 0;

            if (state == ClassicNotDecided) forecast.Status = WaveStatus.Deploying;
            else if (waiting <= 0) forecast.Status = WaveStatus.NoneLeft;
            else if (state == ClassicCutScene || state == ClassicConversation) forecast.Status = WaveStatus.Arriving;
            else SetFirstPhaseCountdown(forecast);
        }

        private void ComputeStealth(SideForecast forecast, bool isPlayerSide)
        {
            int state = ToInt(AmbushState.GetValue(_ambush));
            if (isPlayerSide)
            {
                int selected = (AmbushPlayerTroops?.GetValue(_ambush) as ICollection)?.Count ?? 0;
                int arrived = _stealthAreas?.AllyTroops?.Count ?? 0;
                int waiting = Math.Max(0, selected - arrived);
                forecast.Reserves = waiting;
                forecast.NextWave = waiting;
                forecast.Status = state == StealthNotDecided ? WaveStatus.Deploying
                    : waiting > 0 ? WaveStatus.CallTroops
                    : WaveStatus.NoneLeft;
                return;
            }

            bool bossSpawned = AmbushBoss?.GetValue(_ambush) != null;
            int waitingBandits = 0;
            if (!bossSpawned)
            {
                int pool = (AmbushPool.GetValue(_ambush) as ICollection)?.Count ?? 0;
                int population = (int)AmbushPopulation.GetValue(_ambush);
                int minimum = (int)Math.Max(4f, Math.Min(20f, (float)(population / 2)));
                waitingBandits = Math.Max(pool, minimum) + (AmbushBossOrigin?.GetValue(_ambush) != null ? 1 : 0);
            }
            forecast.Reserves = waitingBandits;
            forecast.NextWave = waitingBandits;
            forecast.WavesLeft = waitingBandits > 0 ? 1 : 0;

            if (state == StealthNotDecided) forecast.Status = WaveStatus.Deploying;
            else if (waitingBandits <= 0) forecast.Status = WaveStatus.NoneLeft;
            else if (state == StealthCutScene || state == StealthConversation) forecast.Status = WaveStatus.Arriving;
            else SetFirstPhaseCountdown(forecast); // stealth phase, call-troops cutscene or the fight before the boss
        }

        /// <summary>The second stage needs every bandit on the field down.</summary>
        private void SetFirstPhaseCountdown(SideForecast forecast)
        {
            _firstPhaseMaxOnField = Math.Max(_firstPhaseMaxOnField, forecast.OnField);
            forecast.WaveSize = _firstPhaseMaxOnField;
            forecast.CasualtiesNeeded = forecast.OnField;
            forecast.Status = forecast.OnField > 0 ? WaveStatus.NeedsCasualties : WaveStatus.Arriving;
        }

        private static int ToInt(object enumValue) => enumValue == null ? -1 : Convert.ToInt32(enumValue);
    }
}

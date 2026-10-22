using System;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ReinforcementTracker
{
    /// <summary>
    /// Predicts reinforcements by re-applying the rules of DefaultBattleMissionAgentSpawnLogic (game v1.4.8,
    /// read from its IL). Read-only: nothing in the game is changed.
    ///
    /// Every GlobalReinforcementInterval seconds (3 s in campaign battles, 10 s by default) each side works out a batch:
    ///  - Wave (campaign field battles and sieges): only when nothing is queued and the side has lost at least
    ///    waveSize = max(1, InitialSpawned * ReinforcementWavePercentage) troops; then min(waveSize, Remaining)
    ///    troops are queued. The game's wave-count option already trimmed Remaining at the start.
    ///  - Balanced (custom battles): min(desired, InitialSpawned - active, Remaining), released once at least
    ///    batchSize = BattleSize * ReinforcementBatchPercentage troops are queued (or fewer are left).
    ///  - Fixed: max(1, TotalSpawnNumber * side %) troops every check.
    /// Queued troops spawn one per tick while the engine has room for both sides' queued agents.
    /// </summary>
    public static class ReinforcementForecast
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly Type LogicType = typeof(DefaultBattleMissionAgentSpawnLogic);
        private static readonly FieldInfo ContextsField = LogicType.GetField("_battleSideSpawnContexts", Private);
        private static readonly FieldInfo TimerField = LogicType.GetField("_globalReinforcementSpawnTimer", Private);
        private static readonly FieldInfo EnabledField = LogicType.GetField("_reinforcementSpawnEnabled", Private);
        private static readonly FieldInfo SpawningField = LogicType.GetField("_spawningReinforcements", Private);

        /// <summary>False if a game update renamed the private fields this relies on.</summary>
        public static bool ReflectionOk => ContextsField != null;

        public static SideForecast Compute(Mission mission, DefaultBattleMissionAgentSpawnLogic logic, BattleSideEnum side)
        {
            var f = new SideForecast { Side = side };

            MissionBattleSideSpawnContext[] contexts = logic != null && ReflectionOk
                ? ContextsField.GetValue(logic) as MissionBattleSideSpawnContext[]
                : null;
            MissionSpawnPhase phase = logic == null ? null
                : side == BattleSideEnum.Defender ? logic.DefenderActivePhase : logic.AttackerActivePhase;
            int index = (int)side;
            MissionBattleSideSpawnContext context = contexts != null && index >= 0 && index < contexts.Length ? contexts[index] : null;

            if (phase == null || context == null)
            {
                f.Kind = ForecastKind.LiveCountOnly;
                f.Status = WaveStatus.NoSpawnLogic;
                f.OnField = f.Starting = BattleSides.CountActiveHumans(mission, side);
                return f;
            }

            f.Starting = logic.GetTotalNumberOfTroopsForSide(side);
            f.OnField = Math.Max(0, context.NumberOfActiveTroops);
            f.Reserves = Math.Max(0, phase.RemainingSpawnNumber);
            f.Queued = context.ReservedTroopsCount;
            f.InitialSpawned = phase.InitialSpawnedNumber;

            MissionSpawnSettings settings = logic.SpawnSettings;
            bool customTimer = settings.ReinforcementTroopsTimingMethod == MissionSpawnSettings.ReinforcementTimingMethod.CustomTimer;
            if (!customTimer && TimerField?.GetValue(logic) is BasicMissionTimer timer)
                f.SecondsToCheck = Math.Max(0f, logic.GetReinforcementInterval() - timer.ElapsedTime);

            switch (settings.ReinforcementTroopsSpawnMethod)
            {
                case MissionSpawnSettings.ReinforcementSpawnMethod.Wave:
                    ForecastWave(f, settings);
                    break;
                case MissionSpawnSettings.ReinforcementSpawnMethod.Fixed:
                    ForecastFixed(f, settings, logic);
                    break;
                default:
                    ForecastBalanced(f, settings, logic, phase);
                    break;
            }

            f.Status = GetStatus(f, logic, context, contexts);
            if (f.Status == WaveStatus.NoneLeft)
            {
                f.NextWave = 0;
                f.CasualtiesNeeded = 0;
                f.WavesLeft = 0;
            }
            return f;
        }

        private static void ForecastWave(SideForecast f, MissionSpawnSettings settings)
        {
            f.Kind = ForecastKind.Wave;
            f.WaveSize = (int)Math.Max(1f, f.InitialSpawned * settings.ReinforcementWavePercentage);
            if (f.Queued > 0)
            {
                f.NextWave = f.Queued;
            }
            else
            {
                // Even the last, smaller wave needs the full loss before it's called.
                int lost = f.InitialSpawned - f.OnField;
                f.NextWave = Math.Min(f.WaveSize, f.Reserves);
                f.CasualtiesNeeded = Math.Max(0, f.WaveSize - lost);
            }
            f.WavesLeft = (f.Reserves + f.WaveSize - 1) / f.WaveSize;
        }

        private static void ForecastFixed(SideForecast f, MissionSpawnSettings settings, DefaultBattleMissionAgentSpawnLogic logic)
        {
            f.Kind = ForecastKind.Fixed;
            float share = f.Side == BattleSideEnum.Attacker
                ? settings.AttackerReinforcementBatchPercentage
                : settings.DefenderReinforcementBatchPercentage;
            f.WaveSize = Math.Max(1, (int)(logic.TotalSpawnNumber * share));
            f.NextWave = Math.Max(f.Queued, Math.Min(f.WaveSize, f.Reserves));
            f.WavesLeft = (f.Reserves + f.WaveSize - 1) / f.WaveSize;
        }

        private static void ForecastBalanced(SideForecast f, MissionSpawnSettings settings,
            DefaultBattleMissionAgentSpawnLogic logic, MissionSpawnPhase phase)
        {
            f.Kind = ForecastKind.Balanced;
            f.WaveSize = (int)(logic.BattleSize * settings.ReinforcementBatchPercentage);

            int freeSlots = f.InitialSpawned - f.OnField;
            int desired = Math.Max(1, f.Queued + (int)((phase.TotalSpawnNumber - f.InitialSpawned) * settings.DesiredReinforcementPercentage));
            int canQueue = Math.Min(desired, freeSlots);
            int queuedAfterCheck = Math.Max(f.Queued, Math.Min(canQueue, f.Reserves));

            bool released = queuedAfterCheck > 0 && (queuedAfterCheck >= f.WaveSize || f.Reserves <= f.WaveSize);
            if (released)
            {
                f.NextWave = queuedAfterCheck;
                return;
            }
            int threshold = f.Reserves <= f.WaveSize ? 1 : f.WaveSize;
            f.NextWave = Math.Min(Math.Max(1, threshold), f.Reserves);
            f.CasualtiesNeeded = Math.Max(1, threshold - Math.Max(f.Queued, canQueue));
        }

        private static WaveStatus GetStatus(SideForecast f, DefaultBattleMissionAgentSpawnLogic logic,
            MissionBattleSideSpawnContext context, MissionBattleSideSpawnContext[] contexts)
        {
            if (!logic.IsInitialSpawnOver) return WaveStatus.Deploying;
            if (f.Reserves <= 0) return WaveStatus.NoneLeft;

            bool enabled = !(EnabledField?.GetValue(logic) is bool e) || e;
            if (!enabled || !context.TroopSpawnActive) return WaveStatus.Paused;

            if (f.Queued > 0)
            {
                if (SpawningField?.GetValue(logic) is bool spawning && spawning) return WaveStatus.Arriving;

                int room = DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission - logic.NumberOfAgents;
                int needed = 0;
                foreach (MissionBattleSideSpawnContext c in contexts)
                    if (c != null) needed += c.ReinforcementQuotaRequirement;
                return room < needed ? WaveStatus.WaitingForSpace : WaveStatus.NextCheck;
            }
            return f.CasualtiesNeeded > 0 ? WaveStatus.NeedsCasualties : WaveStatus.NextCheck;
        }
    }
}

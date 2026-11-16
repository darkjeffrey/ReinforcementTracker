using System;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ReinforcementTracker
{
    /// <summary>
    /// Predicts reinforcements by re-applying the rules of DefaultBattleMissionAgentSpawnLogic (game v1.5.7,
    /// read from its IL). Read-only: nothing in the game is changed.
    ///
    /// Every GlobalReinforcementInterval seconds (3 s in campaign and custom battles, 10 s by default) each side
    /// works out a batch:
    ///  - Wave (campaign and custom field battles and sieges): only when nothing is queued and the side has lost at
    ///    least waveSize = max(1, InitialSpawned * ReinforcementWavePercentage) troops; then min(waveSize, Remaining)
    ///    troops are queued. In the campaign the game's wave-count option already trimmed Remaining at the start;
    ///    custom battles have no wave limit.
    ///  - Balanced (the default settings, used by missions that don't set their own): min(desired,
    ///    InitialSpawned - active, Remaining), released once at least batchSize = BattleSize *
    ///    ReinforcementBatchPercentage troops are queued (or fewer are left).
    ///  - Fixed (sally-outs, on a custom timer): max(1, TotalSpawnNumber * side %) troops every check.
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
            var forecast = new SideForecast { Side = side };

            MissionBattleSideSpawnContext[] contexts = logic != null && ReflectionOk
                ? ContextsField.GetValue(logic) as MissionBattleSideSpawnContext[]
                : null;
            MissionSpawnPhase phase = logic == null ? null
                : side == BattleSideEnum.Defender ? logic.DefenderActivePhase : logic.AttackerActivePhase;
            int index = (int)side;
            MissionBattleSideSpawnContext context = contexts != null && index >= 0 && index < contexts.Length ? contexts[index] : null;

            if (phase == null || context == null)
            {
                forecast.Kind = ForecastKind.LiveCountOnly;
                forecast.Status = WaveStatus.NoSpawnLogic;
                forecast.OnField = forecast.Starting = BattleSides.CountActiveHumans(mission, side);
                return forecast;
            }

            forecast.Starting = logic.GetTotalNumberOfTroopsForSide(side);
            forecast.OnField = Math.Max(0, context.NumberOfActiveTroops);
            forecast.Reserves = Math.Max(0, phase.RemainingSpawnNumber);
            forecast.Queued = context.ReservedTroopsCount;
            forecast.InitialSpawned = phase.InitialSpawnedNumber;

            MissionSpawnSettings settings = logic.SpawnSettings;
            bool customTimer = settings.ReinforcementTroopsTimingMethod == MissionSpawnSettings.ReinforcementTimingMethod.CustomTimer;
            if (!customTimer && TimerField?.GetValue(logic) is BasicMissionTimer timer)
                forecast.SecondsToCheck = Math.Max(0f, logic.GetReinforcementInterval() - timer.ElapsedTime);

            switch (settings.ReinforcementTroopsSpawnMethod)
            {
                case MissionSpawnSettings.ReinforcementSpawnMethod.Wave:
                    ForecastWave(forecast, settings);
                    break;
                case MissionSpawnSettings.ReinforcementSpawnMethod.Fixed:
                    ForecastFixed(forecast, settings, logic);
                    break;
                default:
                    ForecastBalanced(forecast, settings, logic, phase);
                    break;
            }

            forecast.Status = GetStatus(forecast, logic, context, contexts);
            if (forecast.Status == WaveStatus.NoneLeft)
            {
                forecast.NextWave = 0;
                forecast.CasualtiesNeeded = 0;
                forecast.WavesLeft = 0;
            }
            return forecast;
        }

        private static void ForecastWave(SideForecast forecast, MissionSpawnSettings settings)
        {
            forecast.Kind = ForecastKind.Wave;
            forecast.WaveSize = (int)Math.Max(1f, forecast.InitialSpawned * settings.ReinforcementWavePercentage);
            if (forecast.Queued > 0)
            {
                forecast.NextWave = forecast.Queued;
            }
            else
            {
                // Even the last, smaller wave needs the full loss before it's called.
                int lost = forecast.InitialSpawned - forecast.OnField;
                forecast.NextWave = Math.Min(forecast.WaveSize, forecast.Reserves);
                forecast.CasualtiesNeeded = Math.Max(0, forecast.WaveSize - lost);
            }
            forecast.WavesLeft = (forecast.Reserves + forecast.WaveSize - 1) / forecast.WaveSize;
        }

        private static void ForecastFixed(SideForecast forecast, MissionSpawnSettings settings, DefaultBattleMissionAgentSpawnLogic logic)
        {
            forecast.Kind = ForecastKind.Fixed;
            float share = forecast.Side == BattleSideEnum.Attacker
                ? settings.AttackerReinforcementBatchPercentage
                : settings.DefenderReinforcementBatchPercentage;
            forecast.WaveSize = Math.Max(1, (int)(logic.TotalSpawnNumber * share));
            forecast.NextWave = Math.Max(forecast.Queued, Math.Min(forecast.WaveSize, forecast.Reserves));
            forecast.WavesLeft = (forecast.Reserves + forecast.WaveSize - 1) / forecast.WaveSize;
        }

        private static void ForecastBalanced(SideForecast forecast, MissionSpawnSettings settings,
            DefaultBattleMissionAgentSpawnLogic logic, MissionSpawnPhase phase)
        {
            forecast.Kind = ForecastKind.Balanced;
            forecast.WaveSize = (int)(logic.BattleSize * settings.ReinforcementBatchPercentage);

            int freeSlots = forecast.InitialSpawned - forecast.OnField;
            int desired = Math.Max(1, forecast.Queued + (int)((phase.TotalSpawnNumber - forecast.InitialSpawned) * settings.DesiredReinforcementPercentage));
            int canQueue = Math.Min(desired, freeSlots);
            int queuedAfterCheck = Math.Max(forecast.Queued, Math.Min(canQueue, forecast.Reserves));

            bool released = queuedAfterCheck > 0 && (queuedAfterCheck >= forecast.WaveSize || forecast.Reserves <= forecast.WaveSize);
            if (released)
            {
                forecast.NextWave = queuedAfterCheck;
                return;
            }
            int threshold = forecast.Reserves <= forecast.WaveSize ? 1 : forecast.WaveSize;
            forecast.NextWave = Math.Min(Math.Max(1, threshold), forecast.Reserves);
            forecast.CasualtiesNeeded = Math.Max(1, threshold - Math.Max(forecast.Queued, canQueue));
        }

        private static WaveStatus GetStatus(SideForecast forecast, DefaultBattleMissionAgentSpawnLogic logic,
            MissionBattleSideSpawnContext context, MissionBattleSideSpawnContext[] contexts)
        {
            if (!logic.IsInitialSpawnOver) return WaveStatus.Deploying;
            if (forecast.Reserves <= 0) return WaveStatus.NoneLeft;

            bool enabled = !(EnabledField?.GetValue(logic) is bool e) || e;
            if (!enabled || !context.TroopSpawnActive) return WaveStatus.Paused;

            if (forecast.Queued > 0)
            {
                if (SpawningField?.GetValue(logic) is bool spawning && spawning) return WaveStatus.Arriving;

                int room = DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission - logic.NumberOfAgents;
                int needed = 0;
                foreach (MissionBattleSideSpawnContext c in contexts)
                    if (c != null) needed += c.ReinforcementQuotaRequirement;
                return room < needed ? WaveStatus.WaitingForSpace : WaveStatus.NextCheck;
            }
            return forecast.CasualtiesNeeded > 0 ? WaveStatus.NeedsCasualties : WaveStatus.NextCheck;
        }
    }
}

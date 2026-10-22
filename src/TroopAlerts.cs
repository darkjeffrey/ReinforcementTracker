using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ReinforcementTracker
{
    /// <summary>
    /// The optional messages: "Enemies are halfway to getting reinforcements" (once per wave) and
    /// "Enemy reinforcements have arrived: 100 troops".
    /// </summary>
    internal sealed class TroopAlerts
    {
        private const int PlayerIndex = 0, EnemyIndex = 1;
        private static readonly Color PlayerColor = Color.ConvertStringToColor("#8FD16EFF");
        private static readonly Color EnemyColor = Color.ConvertStringToColor("#E0705CFF");

        // True while a side is below the alert threshold, so the "halfway" alert fires once per wave.
        private readonly bool[] _progressArmed = new bool[2];
        // Hideouts raise no "reinforcements spawned" event, so arrivals are spotted by the waiting count dropping.
        private readonly int[] _lastHideoutReserves = { -1, -1 };

        /// <summary>Called a couple of times a second with fresh forecasts.</summary>
        public void Update(SideForecast player, SideForecast enemy)
        {
            if (RtSettings.AlertProgress)
            {
                CheckProgress(player, PlayerIndex);
                CheckProgress(enemy, EnemyIndex);
            }
            CheckHideoutArrival(player, PlayerIndex);
            CheckHideoutArrival(enemy, EnemyIndex);
        }

        /// <summary>A wave has spawned. Battles report this through the game's own event.</summary>
        public static void ShowArrival(bool isPlayer, int count, bool isHideout)
        {
            if (!RtSettings.Enabled || !RtSettings.AlertArrival || count <= 0 || !AlertsWantedFor(isPlayer)) return;

            TextObject text;
            if (isPlayer)
                text = new TextObject("{=rt_al_arrived_player}Your reinforcements have arrived: {COUNT} troops");
            else if (isHideout)
                text = new TextObject("{=rt_al_stage2}The second stage has begun: {COUNT} more bandits");
            else
                text = new TextObject("{=rt_al_arrived_enemy}Enemy reinforcements have arrived: {COUNT} troops");
            text.SetTextVariable("COUNT", count);
            Show(text, isPlayer);
        }

        private void CheckProgress(SideForecast f, int index)
        {
            bool trackable = f.HasReinforcementData && f.WaveSize > 0 && f.Kind != ForecastKind.Fixed
                             && !(f.Kind == ForecastKind.Balanced && f.Reserves <= f.WaveSize);

            if (!trackable || f.Status != WaveStatus.NeedsCasualties)
            {
                // The wave is due, arriving, finished or not tracked: wait for the side to start a new count.
                _progressArmed[index] = trackable && f.Status == WaveStatus.Deploying;
                return;
            }

            float progress = 1f - (float)f.CasualtiesNeeded / f.WaveSize;
            if (progress < RtSettings.AlertProgressPercent / 100f)
            {
                _progressArmed[index] = true;
                return;
            }
            if (!_progressArmed[index]) return;
            _progressArmed[index] = false;

            bool isPlayer = index == PlayerIndex;
            if (!AlertsWantedFor(isPlayer)) return;

            TextObject text = ProgressText(f, isPlayer, RtSettings.AlertProgressPercent);
            text.SetTextVariable("COUNT", f.CasualtiesNeeded);
            text.SetTextVariable("PERCENT", RtSettings.AlertProgressPercent);
            Show(text, isPlayer);
        }

        private static TextObject ProgressText(SideForecast f, bool isPlayer, int percent)
        {
            bool halfway = percent == 50;
            if (f.IsHideout)
                return halfway
                    ? new TextObject("{=rt_al_half_stage2}The bandits are halfway to their second stage ({COUNT} left on the field)")
                    : new TextObject("{=rt_al_pct_stage2}The bandits are {PERCENT}% of the way to their second stage ({COUNT} left on the field)");
            if (isPlayer)
                return halfway
                    ? new TextObject("{=rt_al_half_player}Your side is halfway to getting reinforcements ({COUNT} more casualties)")
                    : new TextObject("{=rt_al_pct_player}Your side is {PERCENT}% of the way to getting reinforcements ({COUNT} more casualties)");
            return halfway
                ? new TextObject("{=rt_al_half_enemy}Enemies are halfway to getting reinforcements ({COUNT} more casualties)")
                : new TextObject("{=rt_al_pct_enemy}Enemies are {PERCENT}% of the way to getting reinforcements ({COUNT} more casualties)");
        }

        private void CheckHideoutArrival(SideForecast f, int index)
        {
            if (!f.IsHideout) return;
            int last = _lastHideoutReserves[index];
            _lastHideoutReserves[index] = f.Reserves;
            if (last <= 0 || f.Reserves >= last || f.Status == WaveStatus.Deploying) return;

            int arrived = last - f.Reserves;
            ShowArrival(index == PlayerIndex, arrived, true);
            Log.Debug($"{(index == PlayerIndex ? "Your side" : "Enemy")}: {arrived} arrived in the hideout ({f.Reserves} still waiting)");
        }

        private static bool AlertsWantedFor(bool isPlayer) => isPlayer ? RtSettings.AlertsForYourSide : RtSettings.AlertsForEnemy;

        private static void Show(TextObject text, bool isPlayer)
        {
            if (RtSettings.AlertsAsBanner)
                MBInformationManager.AddQuickInformation(text);
            else
                Log.Info(text, isPlayer ? PlayerColor : EnemyColor);
        }
    }
}

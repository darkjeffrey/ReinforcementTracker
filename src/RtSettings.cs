using System;

namespace ReinforcementTracker
{
    /// <summary>
    /// Read-only view of the current settings. Values come from the MCM page (ReinforcementTrackerMcmSettings);
    /// the defaults there are used if MCM isn't loaded yet.
    /// </summary>
    public static class RtSettings
    {
        private static readonly ReinforcementTrackerMcmSettings Defaults = new ReinforcementTrackerMcmSettings();

        private static ReinforcementTrackerMcmSettings S
        {
            get
            {
                try { return ReinforcementTrackerMcmSettings.Instance ?? Defaults; }
                catch (Exception) { return Defaults; }
            }
        }

        public static bool Enabled => S.Enabled;
        public static bool ShowEnemy => S.ShowEnemy;
        public static bool AlertProgress => S.AlertProgress;
        public static int AlertProgressPercent => Math.Max(10, Math.Min(90, S.AlertProgressPercent));
        public static bool AlertArrival => S.AlertArrival;
        public static bool AlertsForYourSide => S.AlertsForYourSide;
        public static bool AlertsForEnemy => S.AlertsForEnemy;
        public static bool AlertsAsBanner => S.AlertsAsBanner;
        public static bool DebugMessages => S.DebugMessages;
    }
}

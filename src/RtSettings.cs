using System;

namespace ReinforcementTracker
{
    /// <summary>
    /// The current settings. These are the defaults; when Mod Configuration Menu is installed, its page
    /// (McmPage) loads the saved values into these properties and writes every change back here.
    /// </summary>
    public static class RtSettings
    {
        private static int _alertProgressPercent = 50;

        public static bool Enabled { get; set; } = true;
        public static bool ShowEnemy { get; set; } = true;
        public static bool AlertProgress { get; set; } = true;
        public static int AlertProgressPercent
        {
            get => _alertProgressPercent;
            set => _alertProgressPercent = Math.Max(10, Math.Min(90, value));
        }
        public static bool AlertArrival { get; set; } = true;
        public static bool AlertsForYourSide { get; set; } = true;
        public static bool AlertsForEnemy { get; set; } = true;
        public static bool AlertsAsBanner { get; set; } = false;
        public static bool DebugMessages { get; set; } = false;
    }
}

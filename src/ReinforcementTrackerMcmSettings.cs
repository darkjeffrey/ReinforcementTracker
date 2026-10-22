using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Localization;

namespace ReinforcementTracker
{
    /// <summary>The "Reinforcement Tracker" page in Mod Options (MCM). Saved to Documents\Mount and Blade II Bannerlord\Configs\ModSettings.</summary>
    public sealed class ReinforcementTrackerMcmSettings : AttributeGlobalSettings<ReinforcementTrackerMcmSettings>
    {
        public override string Id => "ReinforcementTracker";
        // MCM shows DisplayName as-is (it doesn't translate it like setting names and hints) and stores it the first
        // time it asks, so resolve it here. If the game's text system isn't ready yet, TextObject hands the raw
        // "{=id}text" back; drop the id then so the English name still shows.
        public override string DisplayName
        {
            get
            {
                string name = new TextObject("{=rt_mcm_title}Reinforcement Tracker").ToString();
                if (name.StartsWith("{=", System.StringComparison.Ordinal))
                {
                    int close = name.IndexOf('}');
                    if (close >= 0) name = name.Substring(close + 1);
                }
                return name;
            }
        }
        public override string FolderName => "ReinforcementTracker";
        public override string FormatType => "json2";

        // ---------------- General ----------------
        private const string General = "{=rt_mcm_group_general}General";

        [SettingPropertyBool("{=rt_mcm_Enabled}Enable", Order = 0, RequireRestart = false,
            HintText = "{=rt_mcm_Enabled_hint}While the battle scoreboard (Tab) is open, show each side's total troops including reinforcements still to come, the size of the next wave and how many more casualties it takes before it arrives. Turning this off also turns off the alerts.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyBool("{=rt_mcm_ShowEnemy}Show the enemy panel", Order = 1, RequireRestart = false,
            HintText = "{=rt_mcm_ShowEnemy_hint}Off: only your own side is shown, if you'd rather not know how many enemies are still coming.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool ShowEnemy { get; set; } = true;

        // ---------------- Alerts ----------------
        private const string Alerts = "{=rt_mcm_group_alerts}Alerts";

        [SettingPropertyBool("{=rt_mcm_AlertProgress}Alert on the way to reinforcements", Order = 0, RequireRestart = false,
            HintText = "{=rt_mcm_AlertProgress_hint}Show a message once per wave when a side has taken enough casualties to be partway to its next reinforcements, for example \"Enemies are halfway to getting reinforcements\".")]
        [SettingPropertyGroup(Alerts, GroupOrder = 1)]
        public bool AlertProgress { get; set; } = true;

        [SettingPropertyInteger("{=rt_mcm_AlertProgressPercent}Alert at", 10, 90, "0'%'", Order = 1, RequireRestart = false,
            HintText = "{=rt_mcm_AlertProgressPercent_hint}How far a side has to be towards its next wave before the alert shows. 50% means half of the casualties the wave needs.")]
        [SettingPropertyGroup(Alerts, GroupOrder = 1)]
        public int AlertProgressPercent { get; set; } = 50;

        [SettingPropertyBool("{=rt_mcm_AlertArrival}Alert when reinforcements arrive", Order = 2, RequireRestart = false,
            HintText = "{=rt_mcm_AlertArrival_hint}Show a message each time a reinforcement wave has arrived, with how many troops it brought.")]
        [SettingPropertyGroup(Alerts, GroupOrder = 1)]
        public bool AlertArrival { get; set; } = true;

        [SettingPropertyBool("{=rt_mcm_AlertsForYourSide}Alerts for your side", Order = 3, RequireRestart = false,
            HintText = "{=rt_mcm_AlertsForYourSide_hint}Show the alerts for your own side, including allies.")]
        [SettingPropertyGroup(Alerts, GroupOrder = 1)]
        public bool AlertsForYourSide { get; set; } = true;

        [SettingPropertyBool("{=rt_mcm_AlertsForEnemy}Alerts for the enemy", Order = 4, RequireRestart = false,
            HintText = "{=rt_mcm_AlertsForEnemy_hint}Show the alerts for the enemy side.")]
        [SettingPropertyGroup(Alerts, GroupOrder = 1)]
        public bool AlertsForEnemy { get; set; } = true;

        [SettingPropertyBool("{=rt_mcm_AlertsAsBanner}Show alerts as a banner", Order = 5, RequireRestart = false,
            HintText = "{=rt_mcm_AlertsAsBanner_hint}Off: alerts appear as coloured lines in the chat log (green for your side, red for the enemy). On: they appear as the large notification at the top of the screen.")]
        [SettingPropertyGroup(Alerts, GroupOrder = 1)]
        public bool AlertsAsBanner { get; set; } = false;

        // ---------------- Testing ----------------
        private const string Testing = "{=rt_mcm_group_testing}Testing";

        [SettingPropertyBool("{=rt_mcm_DebugMessages}Debug messages", Order = 0, RequireRestart = false,
            HintText = "{=rt_mcm_DebugMessages_hint}Show the raw numbers in the chat log each time you open the scoreboard, and a message each time a reinforcement wave arrives with the size that was predicted. Useful for checking the mod; turn it off for normal play.")]
        [SettingPropertyGroup(Testing, GroupOrder = 2)]
        public bool DebugMessages { get; set; } = false;
    }
}

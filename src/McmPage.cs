using System;
using System.Linq;
using System.Runtime.CompilerServices;
using MCM.Abstractions.Base.Global;
using MCM.Abstractions.FluentBuilder;
using MCM.Common;
using TaleWorlds.Localization;

namespace ReinforcementTracker
{
    /// <summary>
    /// The "Reinforcement Tracker" page in Mod Options, shown only when Mod Configuration Menu (MCM) is installed.
    /// Without MCM the mod runs on the defaults in RtSettings.
    ///
    /// MCM is optional, so no type in this assembly may derive from an MCM type: the page is built at runtime
    /// with MCM's fluent builder, and only Build() touches MCM, after checking that MCM is loaded.
    /// Saved to Documents\Mount and Blade II Bannerlord\Configs\ModSettings\Global\ReinforcementTracker.
    /// </summary>
    internal static class McmPage
    {
        private const string Id = "ReinforcementTracker";
        private const string McmAssembly = "MCMv5";

        private static object _settings; // FluentGlobalSettings, typed as object so this class loads without MCM

        public static void TryRegister()
        {
            if (_settings != null) return;
            if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == McmAssembly)) return;
            try
            {
                _settings = Build();
            }
            catch (Exception e)
            {
                Log.Error("Mod Options page", e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Build()
        {
            const string general = "{=rt_mcm_group_general}General";
            const string alerts = "{=rt_mcm_group_alerts}Alerts";
            const string testing = "{=rt_mcm_group_testing}Testing";

            FluentGlobalSettings settings = BaseSettingsBuilder.Create(Id, DisplayName())
                .SetFolderName(Id)
                .SetFormat("json2")
                .CreateGroup(general, g => g
                    .SetGroupOrder(0)
                    .AddBool("Enabled", "{=rt_mcm_Enabled}Enable",
                        new ProxyRef<bool>(() => RtSettings.Enabled, v => RtSettings.Enabled = v),
                        b => b.SetOrder(0).SetRequireRestart(false).SetHintText("{=rt_mcm_Enabled_hint}While the battle scoreboard (Tab) is open, show each side's total troops including reinforcements still to come, the size of the next wave and how many more casualties it takes before it arrives. Turning this off also turns off the alerts."))
                    .AddBool("ShowEnemy", "{=rt_mcm_ShowEnemy}Show the enemy panel",
                        new ProxyRef<bool>(() => RtSettings.ShowEnemy, v => RtSettings.ShowEnemy = v),
                        b => b.SetOrder(1).SetRequireRestart(false).SetHintText("{=rt_mcm_ShowEnemy_hint}Off: only your own side is shown, if you'd rather not know how many enemies are still coming.")))
                .CreateGroup(alerts, g => g
                    .SetGroupOrder(1)
                    .AddBool("AlertProgress", "{=rt_mcm_AlertProgress}Alert on the way to reinforcements",
                        new ProxyRef<bool>(() => RtSettings.AlertProgress, v => RtSettings.AlertProgress = v),
                        b => b.SetOrder(0).SetRequireRestart(false).SetHintText("{=rt_mcm_AlertProgress_hint}Show a message once per wave when a side has taken enough casualties to be partway to its next reinforcements, for example \"Enemies are halfway to getting reinforcements\"."))
                    .AddInteger("AlertProgressPercent", "{=rt_mcm_AlertProgressPercent}Alert at", 10, 90,
                        new ProxyRef<int>(() => RtSettings.AlertProgressPercent, v => RtSettings.AlertProgressPercent = v),
                        b => b.SetOrder(1).SetRequireRestart(false).SetHintText("{=rt_mcm_AlertProgressPercent_hint}How far a side has to be towards its next wave before the alert shows. 50% means half of the casualties the wave needs.").AddValueFormat("0'%'"))
                    .AddBool("AlertArrival", "{=rt_mcm_AlertArrival}Alert when reinforcements arrive",
                        new ProxyRef<bool>(() => RtSettings.AlertArrival, v => RtSettings.AlertArrival = v),
                        b => b.SetOrder(2).SetRequireRestart(false).SetHintText("{=rt_mcm_AlertArrival_hint}Show a message each time a reinforcement wave has arrived, with how many troops it brought."))
                    .AddBool("AlertsForYourSide", "{=rt_mcm_AlertsForYourSide}Alerts for your side",
                        new ProxyRef<bool>(() => RtSettings.AlertsForYourSide, v => RtSettings.AlertsForYourSide = v),
                        b => b.SetOrder(3).SetRequireRestart(false).SetHintText("{=rt_mcm_AlertsForYourSide_hint}Show the alerts for your own side, including allies."))
                    .AddBool("AlertsForEnemy", "{=rt_mcm_AlertsForEnemy}Alerts for the enemy",
                        new ProxyRef<bool>(() => RtSettings.AlertsForEnemy, v => RtSettings.AlertsForEnemy = v),
                        b => b.SetOrder(4).SetRequireRestart(false).SetHintText("{=rt_mcm_AlertsForEnemy_hint}Show the alerts for the enemy side."))
                    .AddBool("AlertsAsBanner", "{=rt_mcm_AlertsAsBanner}Show alerts as a banner",
                        new ProxyRef<bool>(() => RtSettings.AlertsAsBanner, v => RtSettings.AlertsAsBanner = v),
                        b => b.SetOrder(5).SetRequireRestart(false).SetHintText("{=rt_mcm_AlertsAsBanner_hint}Off: alerts appear as coloured lines in the chat log (green for your side, red for the enemy). On: they appear as the large notification at the top of the screen.")))
                .CreateGroup(testing, g => g
                    .SetGroupOrder(2)
                    .AddBool("DebugMessages", "{=rt_mcm_DebugMessages}Debug messages",
                        new ProxyRef<bool>(() => RtSettings.DebugMessages, v => RtSettings.DebugMessages = v),
                        b => b.SetOrder(0).SetRequireRestart(false).SetHintText("{=rt_mcm_DebugMessages_hint}Show the raw numbers in the chat log each time you open the scoreboard, and a message each time a reinforcement wave arrives with the size that was predicted. Useful for checking the mod; turn it off for normal play.")))
                .BuildAsGlobal();

            settings.Register(); // loads the saved values into RtSettings
            return settings;
        }

        /// <summary>
        /// MCM shows the page name as-is (it doesn't translate it like setting names and hints), so resolve it
        /// here. If the game's text system isn't ready, TextObject hands back "{=id}text"; drop the id then.
        /// </summary>
        private static string DisplayName()
        {
            string name = new TextObject("{=rt_mcm_title}Reinforcement Tracker").ToString();
            if (name.StartsWith("{=", StringComparison.Ordinal))
            {
                int close = name.IndexOf('}');
                if (close >= 0) name = name.Substring(close + 1);
            }
            return name;
        }
    }
}

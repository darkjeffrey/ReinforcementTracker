using System;
using SandBox.Missions.MissionLogics.Hideout;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;

namespace ReinforcementTracker
{
    public class SubModule : MBSubModuleBase
    {
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            try
            {
                if (mission == null)
                    return;

                // Battles and hideout assaults only; this skips towns, taverns, arenas and conversations.
                // Custom battles are included on purpose, so no Campaign.Current check.
                if (mission.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>() == null
                    && mission.GetMissionBehavior<MissionGauntletBattleScore>() == null
                    && mission.GetMissionBehavior<HideoutMissionController>() == null
                    && mission.GetMissionBehavior<HideoutAmbushMissionController>() == null)
                    return;

                mission.AddMissionBehavior(new ReinforcementTrackerMissionLogic());
            }
            catch (Exception e)
            {
                Log.Error("OnMissionBehaviorInitialize", e);
            }
        }
    }
}

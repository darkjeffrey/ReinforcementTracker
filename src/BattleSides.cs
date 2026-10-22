using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ReinforcementTracker
{
    internal static class BattleSides
    {
        public static BattleSideEnum Opposite(BattleSideEnum side) =>
            side == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker;

        /// <summary>Humans still fighting on a side (mounts are agents too, so they're skipped).</summary>
        public static int CountActiveHumans(Mission mission, BattleSideEnum side)
        {
            if (mission == null) return 0;
            int count = 0;
            foreach (Team team in mission.Teams)
            {
                if (team.Side != side) continue;
                foreach (Agent agent in team.ActiveAgents)
                    if (agent.IsHuman) count++;
            }
            return count;
        }
    }
}

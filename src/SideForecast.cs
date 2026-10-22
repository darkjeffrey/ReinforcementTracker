using TaleWorlds.Core;

namespace ReinforcementTracker
{
    /// <summary>Which rules decide a side's reinforcements.</summary>
    public enum ForecastKind
    {
        LiveCountOnly,  // no reinforcement data: only the troops on the field are counted
        Wave,           // campaign field battles and sieges
        Balanced,       // custom battles
        Fixed,
        ClassicHideout,
        StealthHideout,
    }

    /// <summary>What the panel says about the next wave.</summary>
    public enum WaveStatus
    {
        NoSpawnLogic,     // no reinforcement data for this fight
        Deploying,        // initial troops not spawned yet
        NoneLeft,         // no reinforcements remain
        Paused,           // spawning is switched off for this side right now
        NeedsCasualties,  // the side has to lose more troops before a wave is called
        NextCheck,        // a wave will be called at the next reinforcement check
        WaitingForSpace,  // a wave is queued but the engine's agent limit is full
        Arriving,         // a wave is spawning right now
        NoReinforcements, // nothing ever comes for this side (your side in a classic hideout)
        CallTroops,       // stealth hideout: your troops come in when you use a signal point
    }

    /// <summary>Snapshot of one side, worked out from the game's own spawn state.</summary>
    public sealed class SideForecast
    {
        public BattleSideEnum Side;
        public ForecastKind Kind;
        public WaveStatus Status;

        public int Starting;          // troops this side brings into the fight
        public int OnField;           // spawned and still fighting
        public int Reserves;          // not spawned yet, including a queued wave
        public int NextWave;          // troops in the next wave
        public int CasualtiesNeeded;  // more losses before the next wave is called
        public int WavesLeft = -1;    // -1 when waves don't apply
        public float SecondsToCheck = -1f; // -1 when unknown

        // Inputs to the forecast, kept for the alerts and the debug messages.
        public int Queued;            // troops already picked for the next wave
        public int InitialSpawned;    // the side's first deployment
        public int WaveSize;          // losses a wave needs (wave/batch size, or first-phase bandits in hideouts)

        public int Total => OnField + Reserves;
        public bool HasReinforcementData => Kind != ForecastKind.LiveCountOnly;
        public bool IsHideout => Kind == ForecastKind.ClassicHideout || Kind == ForecastKind.StealthHideout;
    }
}

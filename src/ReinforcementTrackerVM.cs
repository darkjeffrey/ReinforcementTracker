using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ReinforcementTracker
{
    /// <summary>Data source for GUI/Prefabs/ReinforcementTracker.xml.</summary>
    public class ReinforcementTrackerVM : ViewModel
    {
        private bool _isVisible;
        private bool _showEnemy = true;

        public ReinforcementTrackerVM()
        {
            PlayerSide = new SideVM(new TextObject("{=rt_title_player}Your side").ToString());
            EnemySide = new SideVM(new TextObject("{=rt_title_enemy}Enemy").ToString());
        }

        [DataSourceProperty] public SideVM PlayerSide { get; }
        [DataSourceProperty] public SideVM EnemySide { get; }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set => ViewModelFields.Set(this, ref _isVisible, value, nameof(IsVisible));
        }

        [DataSourceProperty]
        public bool ShowEnemy
        {
            get => _showEnemy;
            set => ViewModelFields.Set(this, ref _showEnemy, value, nameof(ShowEnemy));
        }

        public override void OnFinalize()
        {
            base.OnFinalize();
            PlayerSide.OnFinalize();
            EnemySide.OnFinalize();
        }
    }

    /// <summary>One side's panel.</summary>
    public class SideVM : ViewModel
    {
        private string _total = "", _starting = "", _onField = "", _reserves = "", _nextWave = "", _wavesLeft = "", _status = "";
        private bool _hasSpawnData, _showWavesLeft;

        public SideVM(string title)
        {
            Title = title;
            TotalLabel = new TextObject("{=rt_lbl_total}Total troops").ToString();
            StartingLabel = new TextObject("{=rt_lbl_starting}At the start").ToString();
            OnFieldLabel = new TextObject("{=rt_lbl_onfield}On the field").ToString();
            ReservesLabel = new TextObject("{=rt_lbl_reserves}Still to come").ToString();
            NextWaveLabel = new TextObject("{=rt_lbl_nextwave}Next wave").ToString();
            WavesLeftLabel = new TextObject("{=rt_lbl_wavesleft}Waves left").ToString();
        }

        [DataSourceProperty] public string Title { get; }
        [DataSourceProperty] public string TotalLabel { get; }
        [DataSourceProperty] public string StartingLabel { get; }
        [DataSourceProperty] public string OnFieldLabel { get; }
        [DataSourceProperty] public string ReservesLabel { get; }
        [DataSourceProperty] public string NextWaveLabel { get; }
        [DataSourceProperty] public string WavesLeftLabel { get; }

        [DataSourceProperty] public string Total { get => _total; set => ViewModelFields.Set(this, ref _total, value, nameof(Total)); }
        [DataSourceProperty] public string Starting { get => _starting; set => ViewModelFields.Set(this, ref _starting, value, nameof(Starting)); }
        [DataSourceProperty] public string OnField { get => _onField; set => ViewModelFields.Set(this, ref _onField, value, nameof(OnField)); }
        [DataSourceProperty] public string Reserves { get => _reserves; set => ViewModelFields.Set(this, ref _reserves, value, nameof(Reserves)); }
        [DataSourceProperty] public string NextWave { get => _nextWave; set => ViewModelFields.Set(this, ref _nextWave, value, nameof(NextWave)); }
        [DataSourceProperty] public string WavesLeft { get => _wavesLeft; set => ViewModelFields.Set(this, ref _wavesLeft, value, nameof(WavesLeft)); }
        [DataSourceProperty] public string Status { get => _status; set => ViewModelFields.Set(this, ref _status, value, nameof(Status)); }

        /// <summary>False in fights without reinforcement data: only "On the field" is shown.</summary>
        [DataSourceProperty]
        public bool HasSpawnData
        {
            get => _hasSpawnData;
            set => ViewModelFields.Set(this, ref _hasSpawnData, value, nameof(HasSpawnData));
        }

        [DataSourceProperty]
        public bool ShowWavesLeft
        {
            get => _showWavesLeft;
            set => ViewModelFields.Set(this, ref _showWavesLeft, value, nameof(ShowWavesLeft));
        }

        public void Update(SideForecast f)
        {
            HasSpawnData = f.HasReinforcementData;
            OnField = f.OnField.ToString();
            Total = f.Total.ToString();
            Starting = f.Starting.ToString();
            Reserves = f.Reserves.ToString();
            NextWave = f.Status == WaveStatus.NoneLeft || f.Status == WaveStatus.NoReinforcements ? "-" : f.NextWave.ToString();
            ShowWavesLeft = f.HasReinforcementData && f.WavesLeft >= 0;
            WavesLeft = f.WavesLeft.ToString();
            Status = StatusText(f);
        }

        private static string StatusText(SideForecast f)
        {
            switch (f.Status)
            {
                case WaveStatus.NoSpawnLogic:
                    return new TextObject("{=rt_st_nodata}No reinforcement data in this fight").ToString();
                case WaveStatus.Deploying:
                    return new TextObject("{=rt_st_deploying}Troops are still deploying").ToString();
                case WaveStatus.NoneLeft:
                    return new TextObject("{=rt_st_noneleft}No reinforcements left").ToString();
                case WaveStatus.Paused:
                    return new TextObject("{=rt_st_paused}Reinforcements are paused").ToString();
                case WaveStatus.Arriving:
                    if (f.IsHideout)
                        return new TextObject("{=rt_st_stage2_now}The second stage is starting").ToString();
                    return new TextObject("{=rt_st_arriving}Reinforcements arriving now").ToString();
                case WaveStatus.WaitingForSpace:
                    return new TextObject("{=rt_st_space}Next wave is waiting for room on the battlefield").ToString();
                case WaveStatus.NoReinforcements:
                    return new TextObject("{=rt_st_none_here}No reinforcements in this fight").ToString();
                case WaveStatus.CallTroops:
                    return new TextObject("{=rt_st_call}Your troops come in when you use a signal point").ToString();
                case WaveStatus.NeedsCasualties:
                    if (f.IsHideout)
                    {
                        if (f.CasualtiesNeeded == 1)
                            return new TextObject("{=rt_st_stage2_one}Second stage after 1 more casualty").ToString();
                        return new TextObject("{=rt_st_stage2_more}Second stage after {COUNT} more casualties")
                            .SetTextVariable("COUNT", f.CasualtiesNeeded).ToString();
                    }
                    if (f.CasualtiesNeeded == 1)
                        return new TextObject("{=rt_st_one_more}Next wave after 1 more casualty").ToString();
                    return new TextObject("{=rt_st_more}Next wave after {COUNT} more casualties")
                        .SetTextVariable("COUNT", f.CasualtiesNeeded).ToString();
                default: // NextCheck
                    if (f.SecondsToCheck < 0f)
                        return new TextObject("{=rt_st_nextcheck}Next wave at the next reinforcement check").ToString();
                    return new TextObject("{=rt_st_seconds}Next wave in {SECONDS}s")
                        .SetTextVariable("SECONDS", Math.Max(1, (int)Math.Ceiling(f.SecondsToCheck))).ToString();
            }
        }
    }

    internal static class ViewModelFields
    {
        // Set a backing field and notify the UI, only when the value changed. (The game's generic
        // OnPropertyChangedWithValue<T> is limited to reference types, so bool needs its own overload.)
        public static void Set(ViewModel vm, ref string field, string value, string propertyName)
        {
            if (field == value) return;
            field = value;
            vm.OnPropertyChangedWithValue(value, propertyName);
        }

        public static void Set(ViewModel vm, ref bool field, bool value, string propertyName)
        {
            if (field == value) return;
            field = value;
            vm.OnPropertyChangedWithValue(value, propertyName);
        }
    }
}

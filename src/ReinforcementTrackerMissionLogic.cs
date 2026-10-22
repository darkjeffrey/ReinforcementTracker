using System;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace ReinforcementTracker
{
    /// <summary>
    /// Added to every battle and hideout assault. Shows the overlay while the vanilla scoreboard (Tab) is open
    /// and drives the alerts. Read-only: it never changes how the game spawns troops.
    /// </summary>
    public class ReinforcementTrackerMissionLogic : MissionLogic
    {
        private const float PanelRefreshInterval = 0.25f;
        private const float AlertCheckInterval = 0.5f;

        private readonly TroopAlerts _alerts = new TroopAlerts();
        private DefaultBattleMissionAgentSpawnLogic _spawnLogic;
        private HideoutForecast _hideout;
        private MissionGauntletBattleScore _scoreboard;
        private MissionScreen _screen;
        private OverlayLayer _layer;
        private ReinforcementTrackerVM _vm;
        private SideForecast _lastPlayer, _lastEnemy;
        private bool _setupDone, _stopped, _wasVisible;
        private float _panelTimer, _alertTimer;

        private bool HasForecast => _spawnLogic != null || _hideout != null;

        public override void OnMissionTick(float dt)
        {
            if (_stopped) return;
            try
            {
                if (!_setupDone) Setup();
                if (_layer == null && ScreenManager.TopScreen is MissionScreen screen)
                    CreateOverlay(screen);

                _alertTimer -= dt;
                if (_alertTimer <= 0f)
                {
                    _alertTimer = AlertCheckInterval;
                    if (RtSettings.Enabled && HasForecast)
                        _alerts.Update(Forecast(GetPlayerSide()), Forecast(BattleSides.Opposite(GetPlayerSide())));
                }
            }
            catch (Exception ex)
            {
                Stop(ex);
            }
        }

        private void Setup()
        {
            _setupDone = true;
            _spawnLogic = Mission.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>();
            _scoreboard = Mission.GetMissionBehavior<MissionGauntletBattleScore>();
            if (_spawnLogic != null)
                _spawnLogic.OnReinforcementsSpawned += OnReinforcementsSpawned;
            else
                _hideout = HideoutForecast.TryCreate(Mission);

            if (!RtSettings.DebugMessages) return;
            string hideout = _hideout == null ? ""
                : $", {_hideout.HideoutKind}" + (_hideout.ReflectionOk ? "" : " (hideout fields NOT found, game update?)");
            Log.Debug($"spawn logic {(_spawnLogic != null ? "found" : "not found")}, " +
                      $"scoreboard {(_scoreboard != null ? "found" : "not found (using the Tab key)")}" +
                      (ReinforcementForecast.ReflectionOk ? "" : ", reinforcement fields NOT found (game update?)") +
                      hideout);
        }

        private void CreateOverlay(MissionScreen screen)
        {
            _screen = screen;
            _vm = new ReinforcementTrackerVM();
            _layer = new OverlayLayer(this);
            _layer.LoadMovie("ReinforcementTracker", _vm);
            _screen.AddLayer(_layer);
        }

        /// <summary>Called every frame by the overlay layer, also while the game is paused.</summary>
        private void OnLayerTick(float dt)
        {
            if (_stopped || _vm == null) return;
            try
            {
                bool show = RtSettings.Enabled && IsScoreboardOpen();
                if (show)
                {
                    _panelTimer -= dt;
                    if (!_wasVisible || _panelTimer <= 0f)
                    {
                        RefreshPanels();
                        _panelTimer = PanelRefreshInterval;
                        if (!_wasVisible) LogForecasts();
                    }
                }
                _vm.IsVisible = show;
                _wasVisible = show;
            }
            catch (Exception ex)
            {
                Stop(ex);
            }
        }

        private bool IsScoreboardOpen()
        {
            if (Mission == null || Mission.MissionEnded) return false;
            if (_scoreboard != null)
                return _scoreboard.DataSource != null && _scoreboard.DataSource.ShowScoreboard;
            return Input.IsKeyDown(InputKey.Tab);
        }

        private void RefreshPanels()
        {
            BattleSideEnum playerSide = GetPlayerSide();
            _lastPlayer = Forecast(playerSide);
            _lastEnemy = Forecast(BattleSides.Opposite(playerSide));
            _vm.PlayerSide.Update(_lastPlayer);
            _vm.EnemySide.Update(_lastEnemy);
            _vm.ShowEnemy = RtSettings.ShowEnemy;
        }

        private SideForecast Forecast(BattleSideEnum side)
        {
            if (_hideout != null)
                return _hideout.Compute(Mission, side, side == GetPlayerSide());
            return ReinforcementForecast.Compute(Mission, _spawnLogic, side);
        }

        private BattleSideEnum GetPlayerSide()
        {
            if (_spawnLogic != null && _spawnLogic.PlayerSide != BattleSideEnum.None)
                return _spawnLogic.PlayerSide;
            if (Mission.PlayerTeam != null && Mission.PlayerTeam.Side != BattleSideEnum.None)
                return Mission.PlayerTeam.Side;
            return BattleSideEnum.Attacker;
        }

        /// <summary>Vanilla event, raised once per reinforcement batch after it has spawned.</summary>
        private void OnReinforcementsSpawned(BattleSideEnum side, int count)
        {
            try
            {
                bool isPlayer = side == GetPlayerSide();
                TroopAlerts.ShowArrival(isPlayer, count, false);

                SideForecast predicted = isPlayer ? _lastPlayer : _lastEnemy;
                Log.Debug($"{(isPlayer ? "Your side" : "Enemy")}: {count} reinforcements arrived" +
                          (predicted != null ? $" (last predicted next wave: {predicted.NextWave})" : ""));
            }
            catch (Exception ex)
            {
                Log.Error("reinforcement alert", ex);
            }
        }

        private void LogForecasts()
        {
            if (!RtSettings.DebugMessages) return;
            foreach (SideForecast f in new[] { _lastPlayer, _lastEnemy })
            {
                Log.Debug($"{(f == _lastPlayer ? "You" : "Enemy")} ({f.Side}, {f.Kind}): field {f.OnField}, " +
                          $"to come {f.Reserves} (queued {f.Queued}), start {f.Starting}, initial {f.InitialSpawned}, " +
                          $"wave size {f.WaveSize}, next {f.NextWave}, needs {f.CasualtiesNeeded}, " +
                          $"check in {f.SecondsToCheck:0.0}s, {f.Status}");
            }
        }

        private void Stop(Exception ex)
        {
            _stopped = true;
            if (_vm != null) _vm.IsVisible = false;
            Log.Error("stopped for this battle", ex);
        }

        public override void OnEndMissionInternal()
        {
            base.OnEndMissionInternal();
            Cleanup();
        }

        public override void OnRemoveBehavior()
        {
            base.OnRemoveBehavior();
            Cleanup();
        }

        private void Cleanup()
        {
            _stopped = true; // never recreate the overlay after cleanup
            if (_spawnLogic != null)
            {
                _spawnLogic.OnReinforcementsSpawned -= OnReinforcementsSpawned;
                _spawnLogic = null;
            }
            if (_layer != null)
            {
                try { _screen?.RemoveLayer(_layer); }
                catch (Exception) { /* the screen is already gone */ }
                _layer = null;
            }
            _vm?.OnFinalize();
            _vm = null;
            _screen = null;
        }

        /// <summary>The overlay's Gauntlet layer. Layers tick every frame, even while the mission is paused.</summary>
        private sealed class OverlayLayer : GauntletLayer
        {
            private readonly ReinforcementTrackerMissionLogic _owner;

            public OverlayLayer(ReinforcementTrackerMissionLogic owner) : base("ReinforcementTracker", 300)
            {
                _owner = owner;
            }

            protected override void Tick(float dt)
            {
                base.Tick(dt);
                _owner.OnLayerTick(dt);
            }
        }
    }
}

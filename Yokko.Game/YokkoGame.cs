using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osuTK.Input;
using Yokko.Game.Diagnostics;
using Yokko.Game.Configuration;
using Yokko.Game.Input;
using Yokko.Game.Gameplay;
using Yokko.Game.Audio;
using Yokko.Game.Presentation;
using Yokko.Game.Resources;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Main;
using Yokko.Game.Screens.SongSelect;
using Yokko.Core.Beatmaps;

namespace Yokko.Game
{
    public partial class YokkoGame : YokkoGameBase
    {
        private ScreenStack screenStack;
        private YokkoPerformanceReadout performanceReadout;
        private YokkoDebugConsoleOverlay debugConsole;
        private BindableBool showPerformanceReadout;
        private YokkoGameplaySettings gameplaySettings;
        private Bindable<WindowMode> windowMode;
        private IBindable<DisplayMode> currentDisplayMode;
        private WindowModeNotificationOverlay windowModeNotification;
        private VolumeNotificationOverlay volumeNotification;
        private YokkoAudioSettings audioSettings;
        private readonly Action<Storage> storageReady;
        private readonly string[] startupFiles;
        private readonly IDebugConsoleWindow externalDebugConsole;
        private readonly string persistentStorageRoot;

        internal bool DebugConsoleVisible => Diagnostics.ConsoleVisible.Value;
        internal bool DebugConsoleContains(string text) =>
            debugConsole?.ContainsRenderedText(text) == true;
        internal void SetDebugConsoleVisible(bool visible) =>
            Diagnostics.ConsoleVisible.Value = visible;
        internal bool PerformanceTrackingEnabled =>
            performanceReadout?.TrackingEnabled == true;

        public YokkoGame(
            IKeyInputTimestampBackend keyInputTimestampBackend = null,
            Action<Storage> storageReady = null,
            IEnumerable<string> startupFiles = null,
            IResourceDirectoryPicker resourceDirectoryPicker = null,
            IDesktopDisplayModeController displayModeController = null,
            IDebugConsoleWindow externalDebugConsole = null,
            string persistentStorageRoot = null)
            : base(
                keyInputTimestampBackend,
                resourceDirectoryPicker,
                displayModeController)
        {
            this.storageReady = storageReady;
            this.startupFiles = startupFiles?.ToArray() ?? [];
            this.externalDebugConsole = externalDebugConsole;
            this.persistentStorageRoot = persistentStorageRoot;
        }

        protected override Storage CreateStorage(
            GameHost host,
            Storage defaultStorage)
        {
            if (string.IsNullOrWhiteSpace(persistentStorageRoot))
                return defaultStorage;

            return host.GetStorage(persistentStorageRoot);
        }

        public override void SetupLogging(
            Storage gameStorage,
            Storage cacheStorage)
        {
            storageReady?.Invoke(gameStorage);
            base.SetupLogging(gameStorage, cacheStorage);
        }

        [BackgroundDependencyLoader]
        private void load(
            FrameworkConfigManager frameworkConfig,
            GameHost host)
        {
            gameplaySettings = GameplaySettings;
            audioSettings = AudioSettings;
            windowMode = frameworkConfig.GetBindable<WindowMode>(
                FrameworkSetting.WindowMode);
            currentDisplayMode = host.Window?.CurrentDisplayMode;
            Content.Children = new Drawable[]
            {
                screenStack = new ScreenStack
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new YokkoAdaptiveFrameRateMonitor(),
                performanceReadout = new YokkoPerformanceReadout(
                    diagnostics: Diagnostics)
                {
                    Depth = float.MinValue,
                },
                externalDebugConsole == null
                    ? debugConsole = new YokkoDebugConsoleOverlay(Diagnostics)
                    : new Container(),
                windowModeNotification = new WindowModeNotificationOverlay(),
                volumeNotification = new VolumeNotificationOverlay(),
            };

            screenStack.ScreenPushed += onScreenPushed;
            screenStack.ScreenExited += onScreenExited;

            showPerformanceReadout = DisplaySettings.ShowPerformanceReadout;
            showPerformanceReadout.BindValueChanged(
                onShowPerformanceReadoutChanged,
                true);
            Diagnostics.ConsoleVisible.BindValueChanged(
                onDebugConsoleVisibleChanged,
                true);
            if (externalDebugConsole != null)
            {
                externalDebugConsole.CloseRequested +=
                    onExternalDebugConsoleCloseRequested;
            }
            windowMode.BindValueChanged(onWindowModeChanged);
        }

        protected override void Update()
        {
            base.Update();

            if (performanceReadout == null || gameplaySettings == null)
                return;

            performanceReadout.Position =
                YokkoPerformanceReadout.GetLayoutPosition(
                    Content.DrawSize,
                    gameplaySettings.LayoutPerformanceReadoutOffsetX.Value,
                    gameplaySettings.LayoutPerformanceReadoutOffsetY.Value);
            performanceReadout.Alpha = showPerformanceReadout.Value
                                       && gameplaySettings
                                           .LayoutPerformanceReadoutVisible
                                           .Value >= 0.5
                ? 1
                : 0;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            screenStack.Push(new MainScreen(RequestExit));

            Diagnostics.Trace(
                "STARTUP",
                "main-screen-ready",
                $"startup-files={startupFiles.Length}");

            foreach (string path in startupFiles)
            {
                Diagnostics.Trace("STARTUP", "opening-argument", path);
                OpenExternalPath(path);
            }
        }

        private protected override void OpenImportedReplay(
            YokkoBeatmap beatmap,
            GameplayReplay replay)
        {
            screenStack.Push(new GameplaySessionScreen(
                new GameplayScreen(
                    beatmap,
                    null,
                    null,
                    null,
                    replay)));
        }

        private protected override void OnReplayImported(
            string chartId)
        {
            if (screenStack.CurrentScreen is SongSelectScreen songSelect)
                songSelect.RefreshImportedReplayScores(chartId);
        }

        private void onShowPerformanceReadoutChanged(
            ValueChangedEvent<bool> change)
        {
            performanceReadout.Alpha = change.NewValue ? 1 : 0;
            updatePerformanceTracking();
        }

        private void onDebugConsoleVisibleChanged(
            ValueChangedEvent<bool> change)
        {
            externalDebugConsole?.SetVisible(change.NewValue);
            updatePerformanceTracking();
        }

        private void onExternalDebugConsoleCloseRequested() =>
            Scheduler.Add(() => Diagnostics.ConsoleVisible.Value = false);

        private void updatePerformanceTracking() =>
            performanceReadout.SetTrackingEnabled(
                showPerformanceReadout.Value
                || Diagnostics.ConsoleVisible.Value);

        [Resolved]
        private YokkoConfigManager yokkoConfig { get; set; }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (HandleDesktopShortcut(e.Key, e.Repeat))
                return true;

            if (e.Key == Key.F12 && !e.Repeat)
            {
                Diagnostics.Toggle();
                return true;
            }

            return base.OnKeyDown(e);
        }

        internal bool HandleDesktopShortcut(Key key, bool repeat)
        {
            if (key is Key.VolumeDown or Key.VolumeUp)
            {
                double volume = audioSettings.AdjustMasterVolume(
                    key == Key.VolumeUp ? 1 : -1);
                volumeNotification?.Show(volume, key == Key.VolumeUp);
                return true;
            }

            if (key == yokkoConfig.GetBossKeyBindable().Value
                && !repeat
                && Host.Window != null)
            {
                Host.Window.WindowState = WindowState.Minimised;
                return true;
            }

            return false;
        }

        private void onWindowModeChanged(ValueChangedEvent<WindowMode> change) =>
            windowModeNotification?.Show(
                change.NewValue,
                currentDisplayMode?.Value ?? default);

        private void onScreenPushed(IScreen previous, IScreen current) =>
            Diagnostics.Trace(
                "NAVIGATION",
                "screen-pushed",
                $"from={screenName(previous)} | to={screenName(current)}");

        private void onScreenExited(IScreen previous, IScreen current) =>
            Diagnostics.Trace(
                "NAVIGATION",
                "screen-exited",
                $"from={screenName(previous)} | to={screenName(current)}");

        private static string screenName(IScreen screen) =>
            screen?.GetType().Name ?? "none";

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && showPerformanceReadout != null)
                showPerformanceReadout.ValueChanged -=
                    onShowPerformanceReadoutChanged;

            if (isDisposing)
                Diagnostics.ConsoleVisible.ValueChanged -=
                    onDebugConsoleVisibleChanged;

            if (isDisposing && externalDebugConsole != null)
            {
                externalDebugConsole.CloseRequested -=
                    onExternalDebugConsoleCloseRequested;
                externalDebugConsole.SetVisible(false);
            }

            if (isDisposing && windowMode != null)
                windowMode.ValueChanged -= onWindowModeChanged;

            if (isDisposing && screenStack != null)
            {
                screenStack.ScreenPushed -= onScreenPushed;
                screenStack.ScreenExited -= onScreenExited;
            }

            base.Dispose(isDisposing);
        }
    }
}

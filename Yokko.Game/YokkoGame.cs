using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Framework.Screens;
using Yokko.Game.Input;
using Yokko.Game.Gameplay;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Main;
using Yokko.Core.Beatmaps;

namespace Yokko.Game
{
    public partial class YokkoGame : YokkoGameBase
    {
        private ScreenStack screenStack;
        private YokkoPerformanceReadout performanceReadout;
        private BindableBool showPerformanceReadout;
        private readonly Action<Storage> storageReady;
        private readonly string[] startupFiles;

        public YokkoGame(
            IKeyInputTimestampBackend keyInputTimestampBackend = null,
            Action<Storage> storageReady = null,
            IEnumerable<string> startupFiles = null)
            : base(keyInputTimestampBackend)
        {
            this.storageReady = storageReady;
            this.startupFiles = startupFiles?.ToArray() ?? [];
        }

        public override void SetupLogging(
            Storage gameStorage,
            Storage cacheStorage)
        {
            storageReady?.Invoke(gameStorage);
            base.SetupLogging(gameStorage, cacheStorage);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Content.Children = new Drawable[]
            {
                screenStack = new ScreenStack
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new YokkoAdaptiveFrameRateMonitor(),
                performanceReadout = new YokkoPerformanceReadout
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Position = new osuTK.Vector2(0, 12),
                    Depth = float.MinValue,
                },
            };

            showPerformanceReadout = DisplaySettings.ShowPerformanceReadout;
            showPerformanceReadout.BindValueChanged(
                onShowPerformanceReadoutChanged,
                true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            screenStack.Push(new MainScreen(RequestExit));

            foreach (string path in startupFiles)
                OpenExternalPath(path);
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

        private void onShowPerformanceReadoutChanged(
            ValueChangedEvent<bool> change)
        {
            performanceReadout.SetTrackingEnabled(change.NewValue);
            performanceReadout.Alpha = change.NewValue ? 1 : 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && showPerformanceReadout != null)
                showPerformanceReadout.ValueChanged -=
                    onShowPerformanceReadoutChanged;

            base.Dispose(isDisposing);
        }
    }
}

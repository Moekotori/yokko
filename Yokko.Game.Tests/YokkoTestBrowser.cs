using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests
{
    public partial class YokkoTestBrowser : YokkoGameBase
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_MODS_PREVIEW") == "1")
            {
                ManiaModSet mods = ManiaModSet.Empty
                    .With(ManiaModId.HalfTime, true)
                    .With(ManiaModId.Hidden, true);
                Add(new ScreenStack(new GameplayModsScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    mods,
                    _ => { }))
                {
                    RelativeSizeAxes = Axes.Both,
                });
                Add(new CursorContainer());
                return;
            }

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_RESULT_PREVIEW") == "1")
            {
                YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
                {
                    Title = "Afterimage",
                    DifficultyName = "Insane",
                };

                Add(new GameplayResultOverlay(
                    beatmap,
                    new ManiaScoreResult(
                        537_761,
                        0.8251,
                        20,
                        ScoreRank.B,
                        8,
                        12,
                        0,
                        0,
                        0,
                        4),
                    true,
                    () => { },
                    () => { },
                    () => { }));
                return;
            }

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_PAUSE_PREVIEW") == "1")
            {
                var gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo() with
                    {
                        Title = "Pulse Bloom",
                        DifficultyName = "4K Normal",
                    });

                Add(new ScreenStack(gameplay)
                {
                    RelativeSizeAxes = Axes.Both,
                });
                Add(new CursorContainer());
                Scheduler.AddDelayed(gameplay.TogglePause, 500);
                return;
            }

            AddRange(new Drawable[]
            {
                new TestBrowser("Yokko"),
                new CursorContainer()
            });
        }

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);
            host.Window.CursorState |= CursorState.Hidden;
        }
    }
}

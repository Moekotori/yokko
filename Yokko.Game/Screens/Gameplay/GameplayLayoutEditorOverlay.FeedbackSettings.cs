using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Gameplay;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayLayoutEditorOverlay
{
    private Drawable createFeedbackSettingsCard() =>
        new FeedbackSettingsPanel(liveSettings);

    private partial class FeedbackSettingsPanel : CompositeDrawable
    {
        private readonly GameplayLayoutEditorLiveSettings settings;
        private readonly SpriteText durationValue;
        private readonly SpriteText opacityValue;
        private readonly CompactTextButton hitErrorButton;
        private readonly CompactTextButton timingBarButton;

        public FeedbackSettingsPanel(
            GameplayLayoutEditorLiveSettings settings)
        {
            this.settings = settings;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            Position = new Vector2(18, -18);
            Scale = new Vector2(1.08f);
            Size = new Vector2(420, 144);
            Depth = -100;
            Masking = true;
            CornerRadius = 11;
            BorderThickness = 1.25f;
            BorderColour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.72f);

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.Ivory,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 4,
                    Colour = HomeControlColours.Pink,
                },
                new SpriteText
                {
                    Position = new Vector2(12, 8),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.feedback_settings"),
                    Font = LayoutEditorTypography.Bold(12),
                    Colour = HomeControlColours.Navy,
                },
                createLabel(
                    "gameplay.layout_editor.judgement_duration",
                    48),
                new CompactIconButton(
                    FontAwesome.Solid.Minus,
                    () => adjustDuration(-1))
                {
                    Position = new Vector2(126, 32),
                    Size = new Vector2(28),
                },
                createValueBox(
                    durationValue = new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = LayoutEditorTypography.Bold(10),
                        Colour = HomeControlColours.Navy,
                    },
                    new Vector2(160, 32),
                    new Vector2(94, 28)),
                new CompactIconButton(
                    FontAwesome.Solid.Plus,
                    () => adjustDuration(1))
                {
                    Position = new Vector2(260, 32),
                    Size = new Vector2(28),
                },
                createLabel(
                    "gameplay.layout_editor.judgement_opacity",
                    84),
                new CompactIconButton(
                    FontAwesome.Solid.Minus,
                    () => adjustOpacity(-1))
                {
                    Position = new Vector2(126, 68),
                    Size = new Vector2(28),
                },
                createValueBox(
                    opacityValue = new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = LayoutEditorTypography.Bold(10),
                        Colour = HomeControlColours.Navy,
                    },
                    new Vector2(160, 68),
                    new Vector2(94, 28)),
                new CompactIconButton(
                    FontAwesome.Solid.Plus,
                    () => adjustOpacity(1))
                {
                    Position = new Vector2(260, 68),
                    Size = new Vector2(28),
                },
                createLabel(
                    "gameplay.layout_editor.hit_error",
                    120),
                hitErrorButton = new CompactTextButton(
                    string.Empty,
                    () => settings.SetShowJudgementHitError(
                        !settings.ShowJudgementHitError()))
                {
                    Position = new Vector2(104, 104),
                    Size = new Vector2(68, 28),
                },
                new SpriteText
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.CentreLeft,
                    Position = new Vector2(190, 120),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.timing_bar_visibility"),
                    Font = LayoutEditorTypography.Bold(10),
                    Colour = HomeControlColours.Navy,
                },
                timingBarButton = new CompactTextButton(
                    string.Empty,
                    () => settings.SetShowTimingBar(
                        !settings.ShowTimingBar()))
                {
                    Position = new Vector2(300, 104),
                    Size = new Vector2(78, 28),
                },
            };
        }

        protected override void Update()
        {
            base.Update();
            refresh();
        }

        private SpriteText createLabel(string key, float y) => new()
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.CentreLeft,
            Position = new Vector2(12, y),
            Text = YokkoStrings.Get(key),
            Font = LayoutEditorTypography.Bold(10),
            Colour = HomeControlColours.Navy,
        };

        private static Container createValueBox(
            SpriteText value,
            Vector2 position,
            Vector2 size) => new()
        {
            Position = position,
            Size = size,
            Masking = true,
            CornerRadius = 7,
            BorderThickness = 1,
            BorderColour = HomeControlColours.Navy,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                value,
            },
        };

        private void adjustDuration(int direction)
        {
            settings.SetJudgementDisplayDuration(
                settings.JudgementDisplayDuration()
                + direction * YokkoGameplaySettings
                    .JudgementDisplayDurationStepMilliseconds);
            refresh();
        }

        private void adjustOpacity(int direction)
        {
            settings.SetJudgementOpacity(
                settings.JudgementOpacity()
                + direction * YokkoGameplaySettings
                    .JudgementOpacityStep);
            refresh();
        }

        private void refresh()
        {
            durationValue.Text =
                $"{settings.JudgementDisplayDuration():0} ms";
            opacityValue.Text =
                $"{Math.Round(settings.JudgementOpacity() * 100):0}%";
            setToggle(
                hitErrorButton,
                settings.ShowJudgementHitError());
            setToggle(timingBarButton, settings.ShowTimingBar());
        }

        private static void setToggle(
            CompactTextButton button,
            bool enabled)
        {
            button.SetSelected(enabled);
            button.SetText(YokkoStrings.Get(
                enabled
                    ? "gameplay.layout_editor.show"
                    : "gameplay.layout_editor.hide"));
        }
    }
}

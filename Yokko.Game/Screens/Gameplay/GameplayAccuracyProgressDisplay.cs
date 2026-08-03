using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Scoring;
using Yokko.Game.Presentation;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayAccuracyProgressDisplay : CompositeDrawable
{
    private const float content_width = 320;
    private const float progress_height = 6;

    private readonly double progressStartTimeMilliseconds;
    private readonly double progressEndTimeMilliseconds;
    private readonly OsuScoreFontText accuracyValue;
    private readonly Box progressFill;
    private readonly SpriteText progressTime;
    private readonly Container accuracyContainer;
    private readonly Container progressContainer;

    internal double DisplayedAccuracy { get; private set; } = 1;
    internal double DisplayedProgress { get; private set; }
    internal bool UsesSkinAccuracyFont => accuracyValue.UsesSkinFont;
    internal string DisplayedProgressTime => progressTime.Text.ToString();
    internal Drawable AccuracyLayoutDrawable => accuracyContainer;
    internal Drawable ProgressLayoutDrawable => progressContainer;

    internal GameplayAccuracyProgressDisplay(
        OsuManiaSkin skin,
        double progressStartTimeMilliseconds,
        double progressEndTimeMilliseconds)
    {
        this.progressStartTimeMilliseconds = Math.Max(
            0,
            progressStartTimeMilliseconds);
        this.progressEndTimeMilliseconds = Math.Max(
            this.progressStartTimeMilliseconds,
            progressEndTimeMilliseconds);

        Size = new Vector2(content_width, 112);

        InternalChildren = new Drawable[]
        {
            accuracyContainer = new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Size = new Vector2(content_width, 67),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Text = "ACCURACY",
                        Font = new FontUsage("PlusJakartaSans", 14),
                        Colour = new Color4(1f, 1f, 1f, 0.72f),
                    },
                    accuracyValue = new OsuScoreFontText(skin)
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Y = 17,
                    },
                },
            },
            progressContainer = new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Y = 76,
                Size = new Vector2(content_width, 36),
                Children = new Drawable[]
                {
                    new CircularContainer
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Size = new Vector2(content_width, progress_height),
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = new Color4(1f, 1f, 1f, 0.18f),
                            },
                            progressFill = new Box
                            {
                                RelativeSizeAxes = Axes.Y,
                                Width = 0,
                                Colour = YokkoPalette.Cyan,
                            },
                        },
                    },
                    progressTime = new SpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Y = 12,
                        Font = new FontUsage("PlusJakartaSans", 13),
                        Colour = new Color4(1f, 1f, 1f, 0.72f),
                    },
                },
            },
        };

        UpdateState(0, null);
    }

    internal void UpdateState(
        double gameplayTimeMilliseconds,
        BeatmapJudgementState state)
    {
        DisplayedAccuracy = Math.Clamp(state?.Accuracy ?? 1, 0, 1);
        accuracyValue.SetText(
            $"{DisplayedAccuracy * 100:0.00}%");

        // Mirrors ppy/osu SongProgress's playable-bounds behaviour: the
        // lead-in stays at 0% instead of pretending that the chart has begun.
        // Reference: ppy/osu @ 83b8a64, osu.Game/Screens/Play/HUD/SongProgress.cs.
        double duration = progressEndTimeMilliseconds
                          - progressStartTimeMilliseconds;
        double elapsed = Math.Max(
            0,
            gameplayTimeMilliseconds - progressStartTimeMilliseconds);
        DisplayedProgress = duration <= 0
            ? 0
            : Math.Clamp(elapsed / duration, 0, 1);
        progressFill.Width = content_width * (float)DisplayedProgress;
        progressTime.Text =
            $"PROGRESS  {formatTime(elapsed)} / {formatTime(duration)}";
    }

    private static string formatTime(double milliseconds)
    {
        int totalSeconds = (int)Math.Floor(
            Math.Max(0, milliseconds) / 1000);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{totalSeconds / 60:00}:{totalSeconds % 60:00}");
    }
}

internal partial class OsuScoreFontText : CompositeDrawable
{
    private const float content_width = 320;
    private const float target_height = 50;

    private readonly Texture[] digitTextures = new Texture[10];
    private readonly Texture dotTexture;
    private readonly Texture percentTexture;
    private readonly int overlap;
    private readonly Container skinText;
    private readonly SpriteText fallbackText;

    internal bool UsesSkinFont { get; }
    internal string DisplayedText { get; private set; } = string.Empty;

    internal OsuScoreFontText(OsuManiaSkin skin)
    {
        Size = new Vector2(content_width, target_height);

        if (skin != null)
        {
            overlap = skin.Info.ScoreOverlap;
            for (int digit = 0; digit < digitTextures.Length; digit++)
            {
                digitTextures[digit] = getScoreTexture(
                    skin,
                    digit.ToString(CultureInfo.InvariantCulture));
            }

            dotTexture = getScoreTexture(skin, "dot");
            percentTexture = getScoreTexture(skin, "percent");
            UsesSkinFont = Array.TrueForAll(
                               digitTextures,
                               static texture => texture != null)
                           && dotTexture != null
                           && percentTexture != null
                           && hasUsableGlyphDimensions();
        }

        InternalChildren = new Drawable[]
        {
            skinText = new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Height = target_height,
                Alpha = 0,
            },
            fallbackText = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Font = new FontUsage("PlusJakartaSans", 46),
                Colour = Color4.White,
            },
        };
    }

    internal void SetText(string text)
    {
        DisplayedText = text ?? string.Empty;
        if (!UsesSkinFont || !tryPopulateSkinText(DisplayedText))
        {
            skinText.Clear();
            skinText.Alpha = 0;
            fallbackText.Text = DisplayedText;
            fallbackText.Alpha = 1;
            return;
        }

        fallbackText.Alpha = 0;
        skinText.Alpha = 1;
    }

    private bool tryPopulateSkinText(string text)
    {
        var textures = new List<Texture>(text.Length);
        foreach (char character in text)
        {
            Texture texture = character switch
            {
                >= '0' and <= '9' => digitTextures[character - '0'],
                '.' => dotTexture,
                '%' => percentTexture,
                _ => null,
            };

            if (texture == null)
                return false;

            textures.Add(texture);
        }

        float sourceHeight = 0;
        float sourceWidth = 0;
        foreach (Texture texture in textures)
        {
            sourceHeight = Math.Max(sourceHeight, texture.DisplayHeight);
            sourceWidth += Math.Max(
                1,
                texture.DisplayWidth - overlap);
        }

        if (textures.Count > 0)
            sourceWidth += overlap;
        if (sourceHeight <= 0 || sourceWidth <= 0)
            return false;

        float scale = Math.Min(
            target_height / sourceHeight,
            content_width / sourceWidth);
        float x = 0;
        var sprites = new List<Drawable>(textures.Count);
        foreach (Texture texture in textures)
        {
            float width = texture.DisplayWidth * scale;
            float height = texture.DisplayHeight * scale;
            sprites.Add(new Sprite
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = x,
                Size = new Vector2(width, height),
                Texture = texture,
            });
            x += Math.Max(1, width - overlap * scale);
        }

        skinText.Clear();
        skinText.Size = new Vector2(
            Math.Min(content_width, x + overlap * scale),
            target_height);
        skinText.AddRange(sprites);
        return true;
    }

    private bool hasUsableGlyphDimensions()
    {
        float maximumDigitWidth = 0;
        float maximumDigitHeight = 0;
        foreach (Texture texture in digitTextures)
        {
            maximumDigitWidth = Math.Max(
                maximumDigitWidth,
                texture.DisplayWidth);
            maximumDigitHeight = Math.Max(
                maximumDigitHeight,
                texture.DisplayHeight);
        }

        if (maximumDigitWidth <= 0 || maximumDigitHeight <= 0)
            return false;

        // Some skins contain unrelated or broken score-percent assets with an
        // enormous transparent canvas. Scaling the complete readout against
        // one of those makes otherwise valid digits effectively invisible.
        const float maximum_symbol_scale = 6;
        return dotTexture.DisplayWidth > 0
               && dotTexture.DisplayHeight > 0
               && percentTexture.DisplayWidth > 0
               && percentTexture.DisplayHeight > 0
               && dotTexture.DisplayWidth
               <= maximumDigitWidth * maximum_symbol_scale
               && dotTexture.DisplayHeight
               <= maximumDigitHeight * maximum_symbol_scale
               && percentTexture.DisplayWidth
               <= maximumDigitWidth * maximum_symbol_scale
               && percentTexture.DisplayHeight
               <= maximumDigitHeight * maximum_symbol_scale;
    }

    private static Texture getScoreTexture(
        OsuManiaSkin skin,
        string suffix) =>
        skin.GetTexture(
            $"{skin.Info.ScorePrefix}-{suffix}",
            $"score-{suffix}");
}

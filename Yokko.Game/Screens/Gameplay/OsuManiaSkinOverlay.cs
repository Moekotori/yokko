using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;
using Yokko.Core.Scoring;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Gameplay;

internal partial class OsuManiaSkinOverlay : CompositeDrawable
{
    // Mirrors osu! legacy mania's 20ms fade-in, 160ms hold and 40ms fade-out.
    // Reference: ppy/osu LegacyManiaJudgementPiece.cs @ 9f227ed.
    internal const double JudgementAnimationDuration = 220;

    private readonly OsuManiaSkin skin;
    private readonly Container judgementContainer;
    private readonly LegacyManiaAnimatedSprite judgementSprite;
    private readonly SpriteText judgementFallback;
    private readonly Container comboDigits;
    private readonly SpriteText comboFallback;
    private readonly Texture[] digitTextures = new Texture[10];
    private readonly float overlayScale;
    private int displayedCombo = -1;

    public OsuManiaSkinOverlay(OsuManiaSkin skin)
    {
        this.skin = skin ?? throw new ArgumentNullException(nameof(skin));
        OsuManiaSkinConfiguration configuration = skin.Configuration;
        overlayScale = usesScaledOverlays(skin.Info.Version)
            ? 1 / OsuManiaSkinConfiguration.LegacyPositionScaleFactor
            : 1;
        (Anchor judgementAnchor, float judgementY) =
            judgementPlacement(configuration);
        Anchor comboAnchor = configuration.UpsideDown
            ? Anchor.BottomCentre
            : Anchor.TopCentre;
        float comboY = configuration.UpsideDown
            ? -configuration.ComboPosition
            : configuration.ComboPosition;

        RelativeSizeAxes = Axes.Y;

        for (int digit = 0; digit < digitTextures.Length; digit++)
        {
            digitTextures[digit] = skin.GetTexture(
                $"{skin.Info.ComboPrefix}-{digit}",
                $"score-{digit}");
        }

        InternalChildren = new Drawable[]
        {
            judgementContainer = new Container
            {
                Anchor = judgementAnchor,
                Origin = Anchor.Centre,
                Y = judgementY,
                Scale = new Vector2(overlayScale),
                Children = new Drawable[]
                {
                    judgementSprite = new LegacyManiaAnimatedSprite(
                        Array.Empty<Texture>())
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Alpha = 0,
                    },
                    judgementFallback = new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = FontUsage.Default.With(size: 44),
                        Alpha = 0,
                    },
                },
            },
            comboDigits = new Container
            {
                Anchor = comboAnchor,
                Origin = Anchor.Centre,
                Y = comboY,
                Scale = new Vector2(overlayScale),
                Alpha = 0,
            },
            comboFallback = new SpriteText
            {
                Anchor = comboAnchor,
                Origin = Anchor.Centre,
                Y = comboY,
                Scale = new Vector2(overlayScale),
                Font = FontUsage.Default.With(size: 44),
                Alpha = 0,
            },
        };
        judgementSprite.FrameChanged += texture =>
        {
            if (texture != null)
            {
                judgementSprite.Size = new Vector2(
                    texture.DisplayWidth,
                    texture.DisplayHeight);
            }
        };
    }

    public void ShowJudgement(JudgementEvent judgement)
    {
        string assetName = judgement.Rating switch
        {
            JudgementRating.Perfect => skin.Configuration.Hit300g,
            JudgementRating.Great => skin.Configuration.Hit300,
            JudgementRating.Good => skin.Configuration.Hit200,
            JudgementRating.Ok => skin.Configuration.Hit100,
            JudgementRating.Meh => skin.Configuration.Hit50,
            JudgementRating.Miss or JudgementRating.ComboBreak => skin.Configuration.Hit0,
            _ => null,
        };
        string fallbackAssetName = judgement.Rating switch
        {
            JudgementRating.Perfect =>
                skin.FallbackConfiguration.Hit300g,
            JudgementRating.Great =>
                skin.FallbackConfiguration.Hit300,
            JudgementRating.Good =>
                skin.FallbackConfiguration.Hit200,
            JudgementRating.Ok =>
                skin.FallbackConfiguration.Hit100,
            JudgementRating.Meh =>
                skin.FallbackConfiguration.Hit50,
            JudgementRating.Miss or JudgementRating.ComboBreak =>
                skin.FallbackConfiguration.Hit0,
            _ => null,
        };

        if (assetName == null)
            return;

        IReadOnlyList<Texture> frames =
            skin.GetAnimationFrames(
                assetName,
                fallbackAssetName);
        Texture texture = frames.Count > 0 ? frames[0] : null;
        judgementSprite.SetFrames(frames, 1000.0 / 20);

        if (texture != null)
        {
            judgementSprite.Size = new Vector2(
                texture.DisplayWidth,
                texture.DisplayHeight);
        }
        else
        {
            judgementFallback.Text = judgement.Rating switch
            {
                JudgementRating.ComboBreak => "MISS",
                _ => judgement.Rating.ToString().ToUpperInvariant(),
            };
            judgementFallback.Colour = RatingColours.For(judgement.Rating);
        }

        judgementSprite.FinishTransforms();
        judgementFallback.FinishTransforms();
        judgementSprite.Alpha = 0;
        judgementFallback.Alpha = 0;

        Drawable activeJudgement = texture != null
            ? judgementSprite
            : judgementFallback;
        if (texture != null)
            judgementSprite.Restart();
        activeJudgement.Scale = Vector2.One;
        activeJudgement.FadeInFromZero(20, Easing.Out);
        activeJudgement.Delay(JudgementAnimationDuration - 40)
                       .FadeOut(40, Easing.In);

        if (judgement.Rating is JudgementRating.Miss or JudgementRating.ComboBreak)
        {
            activeJudgement.Scale = new Vector2(1.2f);
            activeJudgement.ScaleTo(1, 100, Easing.Out);
        }
        else
        {
            activeJudgement.Scale = new Vector2(0.8f);
            activeJudgement.ScaleTo(1, 40, Easing.Out)
                           .Then()
                           .ScaleTo(0.85f)
                           .ScaleTo(0.7f, 40, Easing.In)
                           .Then()
                           .Delay(100)
                           .ScaleTo(0.4f, 40, Easing.In);
        }
    }

    public void SetCombo(int combo)
    {
        if (combo == displayedCombo)
            return;

        displayedCombo = combo;

        if (combo <= 0)
        {
            comboDigits.Alpha = 0;
            comboFallback.Alpha = 0;
            return;
        }

        string text = combo.ToString(CultureInfo.InvariantCulture);
        var sprites = new List<Drawable>(text.Length);
        float x = 0;
        float height = 0;
        bool hasAllDigits = true;

        foreach (char character in text)
        {
            Texture texture = digitTextures[character - '0'];

            if (texture == null)
            {
                hasAllDigits = false;
                break;
            }

            sprites.Add(new Sprite
            {
                X = x,
                Size = new Vector2(texture.DisplayWidth, texture.DisplayHeight),
                Texture = texture,
            });
            x += texture.DisplayWidth - skin.Info.ComboOverlap;
            height = Math.Max(height, texture.DisplayHeight);
        }

        comboDigits.Clear();

        if (!hasAllDigits)
        {
            comboDigits.Alpha = 0;
            comboFallback.Text = text;
            comboFallback.Alpha = 1;
            return;
        }

        comboDigits.Size = new Vector2(
            Math.Max(1, x + skin.Info.ComboOverlap),
            Math.Max(1, height));
        comboDigits.AddRange(sprites);
        comboDigits.Alpha = 1;
        comboFallback.Alpha = 0;
    }

    public void SetPlayfieldScale(float value)
    {
        Vector2 scale = new(overlayScale * Math.Max(0.01f, value));
        judgementContainer.Scale = scale;
        comboDigits.Scale = scale;
        comboFallback.Scale = scale;
    }

    private static bool usesScaledOverlays(string version) =>
        version.Equals("latest", StringComparison.OrdinalIgnoreCase)
        || double.TryParse(
            version,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double parsed)
        && parsed >= 2.4;

    private static (Anchor Anchor, float Y) judgementPlacement(
        OsuManiaSkinConfiguration configuration)
    {
        float hitPosition = Math.Clamp(configuration.HitPosition, 240, 480);
        float scorePosition = configuration.ScorePosition;

        if (scorePosition > hitPosition / 2)
        {
            return configuration.UpsideDown
                ? (Anchor.TopCentre, hitPosition - scorePosition)
                : (Anchor.BottomCentre, scorePosition - hitPosition);
        }

        return configuration.UpsideDown
            ? (Anchor.BottomCentre, -scorePosition)
            : (Anchor.TopCentre, scorePosition);
    }
}

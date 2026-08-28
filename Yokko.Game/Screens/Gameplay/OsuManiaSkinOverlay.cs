using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Scoring;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Gameplay;

internal partial class OsuManiaSkinOverlay : CompositeDrawable
{
    // Mirrors osu! legacy mania's 20ms fade-in, 160ms hold and 40ms fade-out.
    // Reference: ppy/osu LegacyManiaJudgementPiece.cs @ 9f227ed.
    internal const double JudgementAnimationDuration = 220;

    // int.MaxValue has 10 decimal digits, so a fixed pool of this size can
    // display any combo without allocating sprites per combo change.
    private const int maxComboDigitCount = 10;

    private readonly OsuManiaSkin skin;
    private readonly JudgementConfiguration judgementConfiguration;
    private readonly Container judgementContainer;
    private readonly LegacyManiaAnimatedSprite judgementSprite;
    private readonly SpriteText judgementFallback;
    private readonly Container comboContainer;
    private readonly Container comboDigits;
    private readonly SpriteText comboFallback;
    private readonly Container comboBreakDigits;
    private readonly SpriteText comboBreakFallback;
    private readonly Sprite[] comboDigitSprites;
    private readonly Sprite[] comboBreakDigitSprites;
    private readonly Texture[] digitTextures = new Texture[10];
    private readonly float overlayScale;
    private readonly float baseJudgementY;
    private readonly float baseComboY;
    private float playfieldScale = 1;
    private Vector2 comboLayoutOffset;
    private Vector2 judgementLayoutOffset;
    private Vector2 comboLayoutScale = Vector2.One;
    private Vector2 judgementLayoutScale = Vector2.One;
    private double judgementDisplayDuration = JudgementAnimationDuration;
    private float judgementOpacity = 1;
    private bool judgementVisible = true;
    private bool editorPreview;
    private int displayedCombo = -1;

    internal bool EditorPreviewUsesTexture =>
        editorPreview && judgementSprite.Alpha > 0;

    internal bool EditorComboPreviewVisible =>
        comboDigits.Alpha > 0 || comboFallback.Alpha > 0;

    internal bool ComboVisibleForTest => comboContainer.Alpha > 0;

    internal IReadOnlyList<Sprite> ComboDigitSpritesForTest =>
        comboDigitSprites;

    internal IReadOnlyList<Sprite> ComboBreakDigitSpritesForTest =>
        comboBreakDigitSprites;

    internal bool JudgementVisibleForTest => judgementContainer.Alpha > 0;

    internal Drawable ComboLayoutDrawable =>
        comboDigits.Alpha > 0 ? comboDigits : comboFallback;

    internal Drawable JudgementLayoutDrawable =>
        judgementSprite.Alpha > 0 ? judgementSprite : judgementFallback;

    public OsuManiaSkinOverlay(
        OsuManiaSkin skin,
        bool upscroll = false,
        JudgementConfiguration? judgementConfiguration = null)
    {
        this.skin = skin ?? throw new ArgumentNullException(nameof(skin));
        this.judgementConfiguration =
            judgementConfiguration ?? JudgementConfiguration.YokkoDefault;
        OsuManiaSkinConfiguration configuration = skin.Configuration;
        overlayScale = usesScaledOverlays(skin.Info.Version)
            ? 1 / OsuManiaSkinConfiguration.LegacyPositionScaleFactor
            : 1;
        (Anchor judgementAnchor, float judgementY) =
            judgementPlacement(configuration, upscroll);
        Anchor comboAnchor = upscroll
            ? Anchor.BottomCentre
            : Anchor.TopCentre;
        float comboY = upscroll
            ? -configuration.ComboPosition
            : configuration.ComboPosition;
        baseJudgementY = judgementY;
        baseComboY = comboY;

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
                        Font = new FontUsage("PlusJakartaSans").With(size: 44),
                        Alpha = 0,
                    },
                },
            },
            comboContainer = new Container
            {
                Anchor = comboAnchor,
                Origin = Anchor.Centre,
                Y = comboY,
                Scale = new Vector2(overlayScale),
                Children = new Drawable[]
                {
                    comboBreakDigits = new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Alpha = 0,
                        Blending = BlendingParameters.Additive,
                        Colour = configuration.ComboBreakColour,
                    },
                    comboBreakFallback = new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = new FontUsage("PlusJakartaSans").With(size: 44),
                        Alpha = 0,
                        Blending = BlendingParameters.Additive,
                        Colour = configuration.ComboBreakColour,
                    },
                    comboDigits = new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Alpha = 0,
                    },
                    comboFallback = new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = new FontUsage("PlusJakartaSans").With(size: 44),
                        Alpha = 0,
                    },
                },
            },
        };
        comboDigitSprites = createDigitSpritePool(comboDigits);
        comboBreakDigitSprites = createDigitSpritePool(comboBreakDigits);
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
            judgementFallback.Text =
                this.judgementConfiguration.RatingLabel(judgement.Rating);
            judgementFallback.Colour = RatingColours.ForDisplay(
                judgement.Rating,
                this.judgementConfiguration);
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
        activeJudgement.Rotation = 0;
        activeJudgement.Scale = Vector2.One;

        if (editorPreview)
        {
            activeJudgement.Alpha = 1;
            return;
        }

        activeJudgement.FadeInFromZero(20, Easing.Out);
        activeJudgement.Delay(Math.Max(20, judgementDisplayDuration - 40))
                       .FadeOut(40, Easing.In);

        if (judgement.Rating is JudgementRating.Miss or JudgementRating.ComboBreak)
        {
            activeJudgement.Scale = new Vector2(1.2f);
            activeJudgement.ScaleTo(1, 100, Easing.Out);
            activeJudgement.RotateTo(
                Random.Shared.NextSingle() * 11.46f - 5.73f,
                100,
                Easing.Out);
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

        int previousCombo = displayedCombo;
        displayedCombo = combo;

        if (combo <= 0)
        {
            if (previousCombo > 0)
                showComboBreak(previousCombo);

            comboDigits.Alpha = 0;
            comboFallback.Alpha = 0;
            return;
        }

        Drawable activeCombo = populateCombo(
            combo,
            comboDigits,
            comboDigitSprites,
            comboFallback);

        activeCombo.FinishTransforms();
        activeCombo.Scale = Vector2.One;

        if (previousCombo + 1 == combo)
        {
            activeCombo.Scale = new Vector2(1, 1.4f);
            activeCombo.ScaleTo(Vector2.One, 300, Easing.Out)
                       .FadeIn(120);
        }
        else
            activeCombo.Alpha = 1;
    }

    internal void ClearTransientFeedback()
    {
        judgementSprite.FinishTransforms();
        judgementFallback.FinishTransforms();
        comboDigits.FinishTransforms();
        comboFallback.FinishTransforms();
        comboBreakDigits.FinishTransforms();
        comboBreakFallback.FinishTransforms();
        judgementSprite.Alpha = 0;
        judgementFallback.Alpha = 0;
        comboDigits.Alpha = 0;
        comboFallback.Alpha = 0;
        comboBreakDigits.Alpha = 0;
        comboBreakFallback.Alpha = 0;
        displayedCombo = -1;
    }

    private static Sprite[] createDigitSpritePool(Container digits)
    {
        var sprites = new Sprite[maxComboDigitCount];

        for (int index = 0; index < sprites.Length; index++)
            sprites[index] = new Sprite { Alpha = 0 };

        digits.AddRange(sprites);
        return sprites;
    }

    private Drawable populateCombo(
        int combo,
        Container digits,
        Sprite[] digitSprites,
        SpriteText fallback)
    {
        string text = combo.ToString(CultureInfo.InvariantCulture);
        float x = 0;
        float height = 0;
        bool hasAllDigits = true;

        for (int index = 0; index < text.Length; index++)
        {
            Texture texture = digitTextures[text[index] - '0'];

            if (texture == null)
            {
                hasAllDigits = false;
                break;
            }

            Sprite sprite = digitSprites[index];
            sprite.X = x;
            sprite.Size = new Vector2(
                texture.DisplayWidth,
                texture.DisplayHeight);
            sprite.Texture = texture;
            sprite.Alpha = 1;
            x += texture.DisplayWidth - skin.Info.ComboOverlap;
            height = Math.Max(height, texture.DisplayHeight);
        }

        if (!hasAllDigits)
        {
            digits.Alpha = 0;
            fallback.Text = text;
            fallback.Alpha = 1;
            return fallback;
        }

        for (int index = text.Length; index < digitSprites.Length; index++)
            digitSprites[index].Alpha = 0;

        digits.Size = new Vector2(
            Math.Max(1, x + skin.Info.ComboOverlap),
            Math.Max(1, height));
        digits.Alpha = 1;
        fallback.Alpha = 0;
        return digits;
    }

    private void showComboBreak(int combo)
    {
        Drawable activeBreak = populateCombo(
            combo,
            comboBreakDigits,
            comboBreakDigitSprites,
            comboBreakFallback);
        Drawable inactiveBreak = ReferenceEquals(
            activeBreak,
            comboBreakDigits)
            ? comboBreakFallback
            : comboBreakDigits;

        inactiveBreak.FinishTransforms();
        inactiveBreak.Alpha = 0;
        activeBreak.FinishTransforms();
        activeBreak.Scale = Vector2.One;
        activeBreak.Alpha = 0.8f;
        activeBreak.FadeOut(200)
                   .ScaleTo(4, 200);
    }

    public void SetPlayfieldScale(float value)
    {
        playfieldScale = Math.Max(0.01f, value);
        applyFeedbackLayout();
    }

    internal void SetFeedbackLayout(
        Vector2 comboOffset,
        Vector2 comboScale,
        Vector2 judgementOffset,
        Vector2 judgementScale)
    {
        comboLayoutOffset = comboOffset;
        comboLayoutScale = comboScale;
        judgementLayoutOffset = judgementOffset;
        judgementLayoutScale = judgementScale;
        applyFeedbackLayout();
    }

    private void applyFeedbackLayout()
    {
        float baseScale = overlayScale * playfieldScale;
        comboContainer.Position =
            new Vector2(0, baseComboY) + comboLayoutOffset;
        comboContainer.Scale = comboLayoutScale * baseScale;
        judgementContainer.Position =
            new Vector2(0, baseJudgementY) + judgementLayoutOffset;
        judgementContainer.Scale = judgementLayoutScale * baseScale;
    }

    public void ConfigureJudgementFeedback(
        double displayDuration,
        double opacity)
    {
        judgementDisplayDuration = Math.Max(60, displayDuration);
        judgementOpacity = (float)Math.Clamp(opacity, 0, 1);
        updateJudgementVisibility();
    }

    public void SetComboVisible(bool visible) =>
        comboContainer.Alpha = visible ? 1 : 0;

    public void SetJudgementVisible(bool visible)
    {
        judgementVisible = visible;
        updateJudgementVisibility();
    }

    private void updateJudgementVisibility() =>
        judgementContainer.Alpha = judgementVisible ? judgementOpacity : 0;

    public void SetEditorPreview(bool preview)
    {
        editorPreview = preview;
        judgementSprite.FinishTransforms();
        judgementFallback.FinishTransforms();
        judgementSprite.Alpha = 0;
        judgementFallback.Alpha = 0;

        if (!preview)
            return;

        ShowJudgement(new JudgementEvent(
            -1,
            0,
            0,
            0,
            0,
            JudgementRating.Great));
    }

    internal void SetComboEditorPreview(bool preview)
    {
        comboDigits.FinishTransforms();
        comboFallback.FinishTransforms();
        comboBreakDigits.FinishTransforms();
        comboBreakFallback.FinishTransforms();
        comboDigits.Alpha = 0;
        comboFallback.Alpha = 0;
        comboBreakDigits.Alpha = 0;
        comboBreakFallback.Alpha = 0;
        displayedCombo = -1;

        if (preview)
            SetCombo(128);
    }

    public void SetHoldActive(bool active)
    {
        Color4 colour = active
            ? skin.Configuration.HoldColour
            : Color4.White;
        comboDigits.Colour = colour;
        comboFallback.Colour = colour;
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
        OsuManiaSkinConfiguration configuration,
        bool upscroll)
    {
        float hitPosition = Math.Clamp(configuration.HitPosition, 240, 480);
        float scorePosition = configuration.ScorePosition;

        if (scorePosition > hitPosition / 2)
        {
            return upscroll
                ? (Anchor.TopCentre, hitPosition - scorePosition)
                : (Anchor.BottomCentre, scorePosition - hitPosition);
        }

        return upscroll
            ? (Anchor.BottomCentre, -scorePosition)
            : (Anchor.TopCentre, scorePosition);
    }
}

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
    private const double judgement_visible_duration = 420;

    private readonly OsuManiaSkin skin;
    private readonly Sprite judgementSprite;
    private readonly SpriteText judgementFallback;
    private readonly Container comboDigits;
    private readonly SpriteText comboFallback;
    private readonly Texture[] digitTextures = new Texture[10];
    private readonly float overlayScale;
    private bool showingJudgementTexture;
    private double hideJudgementAt;
    private int displayedCombo = -1;

    public OsuManiaSkinOverlay(OsuManiaSkin skin)
    {
        this.skin = skin ?? throw new ArgumentNullException(nameof(skin));
        OsuManiaSkinConfiguration configuration = skin.Configuration;
        overlayScale = usesScaledOverlays(skin.Info.Version)
            ? 1 / OsuManiaSkinConfiguration.LegacyPositionScaleFactor
            : 1;

        RelativeSizeAxes = Axes.Both;

        for (int digit = 0; digit < digitTextures.Length; digit++)
            digitTextures[digit] = skin.GetTexture($"{skin.Info.ComboPrefix}-{digit}");

        InternalChildren = new Drawable[]
        {
            judgementSprite = new Sprite
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                Y = configuration.ScorePosition,
                Scale = new Vector2(overlayScale),
                Alpha = 0,
            },
            judgementFallback = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                Y = configuration.ScorePosition,
                Scale = new Vector2(overlayScale),
                Font = FontUsage.Default.With(size: 44),
                Alpha = 0,
            },
            comboDigits = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                Y = configuration.ComboPosition,
                Scale = new Vector2(overlayScale),
                Alpha = 0,
            },
            comboFallback = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                Y = configuration.ComboPosition,
                Scale = new Vector2(overlayScale),
                Font = FontUsage.Default.With(size: 44),
                Alpha = 0,
            },
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

        if (assetName == null)
            return;

        Texture texture = skin.GetTexture(assetName);
        showingJudgementTexture = texture != null;
        judgementSprite.Texture = texture;

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

        hideJudgementAt = Time.Current + judgement_visible_duration;
        updateJudgementAlpha(1);
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
        judgementSprite.Scale = scale;
        judgementFallback.Scale = scale;
        comboDigits.Scale = scale;
        comboFallback.Scale = scale;
    }

    protected override void Update()
    {
        base.Update();

        if (Time.Current >= hideJudgementAt)
        {
            updateJudgementAlpha(0);
            return;
        }

        updateJudgementAlpha(Math.Clamp(
            (float)((hideJudgementAt - Time.Current) / 180),
            0,
            1));
    }

    private void updateJudgementAlpha(float alpha)
    {
        judgementSprite.Alpha = showingJudgementTexture ? alpha : 0;
        judgementFallback.Alpha = showingJudgementTexture ? 0 : alpha;
    }

    private static bool usesScaledOverlays(string version) =>
        version.Equals("latest", StringComparison.OrdinalIgnoreCase)
        || double.TryParse(
            version,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double parsed)
        && parsed >= 2.4;
}

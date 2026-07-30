using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Presentation;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Gameplay;

public partial class LaneColumn : CompositeDrawable
{
    // osu! pools legacy hit explosions so dense jacks can overlap for 200ms.
    // Reference: ppy/osu PoolableHitExplosion.cs @ 9f227ed.
    private const int hit_explosion_pool_size = 10;
    private const double key_release_delay = 80;

    private readonly Sprite idleKey;
    private readonly Sprite pressedKey;
    private readonly TextureAnimation laneLight;
    private readonly TextureAnimation holdLight;
    private readonly TextureAnimation[] hitExplosions = [];
    private readonly Container mineExplosion;
    private readonly SpriteText keyLabel;
    private readonly SpriteText skinKeyWarning;
    private readonly float baseLaneWidth;
    private readonly float baseLaneLightHeight;
    private readonly bool idleKeyFlipped;
    private readonly bool pressedKeyFlipped;
    private readonly bool showPressFeedback;
    private bool holdLightActive;
    private int nextHitExplosion;
    private float columnScale = 1;

    internal Container ReceptorLayer { get; }

    internal float IdleKeyHeight => idleKey?.Height ?? 0;

    internal bool HasHoldLight => holdLight != null;

    internal SpriteText SkinKeyWarning => skinKeyWarning;

    internal LaneColumn(
        int lane,
        string keyLabel,
        float laneWidth,
        OsuManiaSkin skin = null,
        bool showPressFeedback = true,
        bool isLastLaneInStage = false)
    {
        this.showPressFeedback = showPressFeedback;
        baseLaneWidth = laneWidth;
        RelativeSizeAxes = Axes.Y;

        if (skin == null)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.04f, 0.052f, 0.07f, 0.9f),
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 1,
                    Colour = new Color4(1f, 1f, 1f, 0.08f),
                },
            };
            ReceptorLayer = new Container
            {
                RelativeSizeAxes = Axes.Y,
                Width = laneWidth,
                Children = new Drawable[]
                {
                    this.keyLabel = new SpriteText
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Y = -26,
                        Text = keyLabel,
                        Font = FontUsage.Default.With(size: 18),
                        Colour = YokkoPalette.TextMuted,
                    },
                    mineExplosion = createMineExplosion(
                        Anchor.BottomCentre,
                        -92,
                        54),
                },
            };
            return;
        }

        OsuManiaSkinConfiguration configuration = skin.Configuration;
        OsuManiaSkinConfiguration fallback =
            skin.FallbackConfiguration;
        float hitPosition = System.Math.Clamp(
            configuration.HitPosition,
            0,
            480);
        float lightPosition = System.Math.Clamp(
            configuration.LightPosition,
            0,
            480);
        Texture idleTexture = skin.GetTexture(
            configuration.KeyImages[lane],
            fallback.KeyImages[lane]);
        Texture pressedTexture = skin.GetTexture(
            configuration.PressedKeyImages[lane],
            fallback.PressedKeyImages[lane]);
        var laneBackground =
            LegacyManiaColourCompatibility.ApplyWithDoubledAlpha(
                new Box
            {
                RelativeSizeAxes = Axes.Both,
            },
                configuration.LaneColours[lane]);
        var leftLine =
            LegacyManiaColourCompatibility.ApplyWithDoubledAlpha(
                new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = configuration.ColumnLineWidths[lane] * 0.74f,
            },
                configuration.ColumnLineColour);
        var backgroundChildren = new List<Drawable>
        {
            laneBackground,
            leftLine,
        };
        float rightLineWidth = configuration.ColumnLineWidths[lane + 1];
        bool showRightLine = isLastLaneInStage
                             || lane == configuration.Keys - 1
                             || configuration.SkinVersion >= 2.4;
        if (showRightLine && rightLineWidth > 0)
        {
            backgroundChildren.Add(
                LegacyManiaColourCompatibility.ApplyWithDoubledAlpha(
                    new Box
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        RelativeSizeAxes = Axes.Y,
                        Width = rightLineWidth * 0.74f,
                    },
                    configuration.ColumnLineColour));
        }
        var receptorChildren = new List<Drawable>();
        IReadOnlyList<Texture> lightTextures =
            skin.GetAnimationFrames(
                configuration.LightImage,
                fallback.LightImage);
        IReadOnlyList<Texture> explosionTextures =
            skin.GetAnimationFrames(
                configuration.ExplosionImage,
                fallback.ExplosionImage);
        IReadOnlyList<Texture> holdLightTextures =
            skin.GetAnimationFrames(
                configuration.HoldNoteLightImage,
                fallback.HoldNoteLightImage);

        if (lightTextures.Count > 0)
        {
            Texture firstLightTexture = lightTextures[0];
            baseLaneLightHeight =
                firstLightTexture.DisplayHeight
                / OsuManiaSkinConfiguration
                    .LegacyPositionScaleFactor;
            backgroundChildren.Add(laneLight = new TextureAnimation
            {
                Name = "Lane light",
                Anchor = configuration.UpsideDown
                    ? Anchor.TopCentre
                    : Anchor.BottomCentre,
                Origin = configuration.UpsideDown
                    ? Anchor.TopCentre
                    : Anchor.BottomCentre,
                Y = configuration.UpsideDown
                    ? 480 - lightPosition
                    : -(480 - lightPosition),
                Size = new Vector2(
                    laneWidth,
                    baseLaneLightHeight),
                Colour =
                    LegacyManiaColourCompatibility.DisallowZeroAlpha(
                        configuration.LaneLightColours[lane]),
                Alpha = 0,
            });
            addFrames(
                laneLight,
                lightTextures,
                1000.0 / configuration.LightFramePerSecond);
        }

        if (idleTexture != null)
        {
            idleKeyFlipped = configuration.UpsideDown
                             && configuration.KeyFlipWhenUpsideDown[lane];
            receptorChildren.Add(idleKey = createKeySprite(
                idleTexture,
                laneWidth,
                configuration.UpsideDown,
                idleKeyFlipped));

            if (pressedTexture != null)
            {
                pressedKeyFlipped = configuration.UpsideDown
                                    && configuration.PressedKeyFlipWhenUpsideDown[lane];
                receptorChildren.Add(pressedKey = createKeySprite(
                    pressedTexture,
                    laneWidth,
                    configuration.UpsideDown,
                    pressedKeyFlipped));
                pressedKey.Alpha = 0;
            }

            float keyHeight = idleKey.Height;
            receptorChildren.Add(skinKeyWarning = new SpriteText
            {
                Name = "Legacy key binding reminder",
                Anchor = configuration.UpsideDown
                    ? Anchor.TopCentre
                    : Anchor.BottomCentre,
                Origin = Anchor.Centre,
                Y = configuration.UpsideDown
                    ? keyHeight / 2
                    : -keyHeight / 2,
                Text = keyLabel,
                Font = FontUsage.Default.With(size: 16),
                Colour = configuration.KeyWarningColour,
            });
            skinKeyWarning.Delay(500).FadeOut(3000);
        }
        else
        {
            receptorChildren.Add(this.keyLabel = new SpriteText
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -26,
                Text = keyLabel,
                Font = FontUsage.Default.With(size: 18),
                Colour = YokkoPalette.TextMuted,
            });
        }

        if (explosionTextures.Count > 0)
        {
            Texture firstExplosionTexture = explosionTextures[0];
            Vector2 explosionSize = legacyLightingSize(
                firstExplosionTexture,
                configuration.ExplosionWidths[lane],
                laneWidth,
                configuration.SkinVersion);
            hitExplosions = new TextureAnimation[hit_explosion_pool_size];
            double frameDuration = Math.Max(
                1000.0 / 60,
                170.0 / explosionTextures.Count);

            for (int index = 0; index < hitExplosions.Length; index++)
            {
                var explosion = new TextureAnimation
                {
                    Name = "Hit explosion",
                    Anchor = configuration.UpsideDown
                        ? Anchor.TopCentre
                        : Anchor.BottomCentre,
                    Origin = Anchor.Centre,
                    Y = configuration.UpsideDown
                        ? 480 - hitPosition
                        : -(480 - hitPosition),
                    Size = explosionSize,
                    Alpha = 0,
                    Blending = BlendingParameters.Additive,
                };
                addFrames(explosion, explosionTextures, frameDuration);
                hitExplosions[index] = explosion;
                receptorChildren.Add(explosion);
            }
        }

        if (holdLightTextures.Count > 0)
        {
            Texture firstHoldLightTexture = holdLightTextures[0];
            Vector2 holdLightSize = legacyLightingSize(
                firstHoldLightTexture,
                configuration.HoldNoteLightWidths[lane],
                laneWidth,
                configuration.SkinVersion);
            holdLight = new TextureAnimation
            {
                Name = "Hold light",
                Anchor = configuration.UpsideDown
                    ? Anchor.TopCentre
                    : Anchor.BottomCentre,
                Origin = Anchor.Centre,
                Y = configuration.UpsideDown
                    ? 480 - hitPosition
                    : -(480 - hitPosition),
                Size = holdLightSize,
                Alpha = 0,
                Blending = BlendingParameters.Additive,
            };
            addFrames(
                holdLight,
                holdLightTextures,
                Math.Max(1000.0 / 60, 170.0 / holdLightTextures.Count));
            receptorChildren.Add(holdLight);
        }

        receptorChildren.Add(mineExplosion = createMineExplosion(
            configuration.UpsideDown
                ? Anchor.TopCentre
                : Anchor.BottomCentre,
            configuration.UpsideDown
                ? 480 - hitPosition
                : -(480 - hitPosition),
            Math.Clamp(laneWidth * 1.25f, 28, 54)));

        InternalChildren = backgroundChildren.ToArray();
        ReceptorLayer = new Container
        {
            RelativeSizeAxes = Axes.Y,
            Width = laneWidth,
            Children = receptorChildren.ToArray(),
        };
    }

    public void SetWidthScale(float value)
    {
        value = System.Math.Max(0.01f, value);
        columnScale = value;
        Width = baseLaneWidth * value;
        ReceptorLayer.Width = baseLaneWidth * value;
        if (idleKey != null)
        {
            idleKey.Scale = new Vector2(
                value,
                idleKeyFlipped ? -1 : 1);
        }

        if (pressedKey != null)
        {
            pressedKey.Scale = new Vector2(
                value,
                pressedKeyFlipped ? -1 : 1);
        }

        if (keyLabel != null)
        {
            keyLabel.Y = -26 * value;
            keyLabel.Scale = new Vector2(value);
        }

        if (skinKeyWarning != null)
            skinKeyWarning.Scale = new Vector2(value);

        foreach (TextureAnimation hitExplosion in hitExplosions)
            hitExplosion.Scale = new Vector2(value);

        if (holdLight != null)
            holdLight.Scale = new Vector2(value);

        mineExplosion.Scale = new Vector2(value);

        if (laneLight != null)
        {
            laneLight.Width = baseLaneWidth * value;
            laneLight.Height = baseLaneLightHeight;
        }
    }

    public void SetPressed(bool pressed)
    {
        if (!showPressFeedback)
            return;

        if (pressedKey != null)
        {
            idleKey.FinishTransforms();
            pressedKey.FinishTransforms();

            if (pressed)
            {
                pressedKey.Alpha = 1;
                idleKey.Alpha = 0;
            }
            else
            {
                idleKey.Delay(key_release_delay).FadeTo(1);
                pressedKey.Delay(key_release_delay).FadeTo(0);
            }
        }

        if (keyLabel != null)
        {
            keyLabel.Colour = pressed
                ? YokkoPalette.Cyan
                : YokkoPalette.TextMuted;
        }

        if (laneLight == null)
            return;

        laneLight.FinishTransforms();

        if (pressed)
        {
            laneLight.Alpha = 1;
            laneLight.Scale = Vector2.One;
        }
        else
        {
            laneLight.FadeTo(0, 250);
            laneLight.ScaleTo(new Vector2(1, 0), 250);
        }
    }

    public void ShowHitExplosion()
    {
        if (hitExplosions.Length == 0)
            return;

        TextureAnimation hitExplosion =
            hitExplosions[nextHitExplosion++ % hitExplosions.Length];
        hitExplosion.FinishTransforms();
        hitExplosion.GotoFrame(0);
        hitExplosion.FadeInFromZero(80)
                    .Then()
                    .FadeOut(120);
    }

    public void ShowMineExplosion()
    {
        mineExplosion.FinishTransforms();
        mineExplosion.Alpha = 1;
        mineExplosion.Scale = new Vector2(columnScale * 0.55f);
        mineExplosion.FlashColour(
            new Color4(1f, 0.91f, 0.42f, 1f),
            150,
            Easing.OutQuint);
        mineExplosion.ScaleTo(
                         new Vector2(columnScale * 1.5f),
                         190,
                         Easing.OutQuint)
                     .FadeOut(220, Easing.OutQuint);
    }

    public void SetHoldActive(bool active)
    {
        if (holdLight == null || holdLightActive == active)
            return;

        holdLightActive = active;
        holdLight.FinishTransforms();

        if (active)
        {
            holdLight.GotoFrame(0);
            holdLight.FadeInFromZero(80);
        }
        else
        {
            holdLight.FadeOut(120);
        }
    }

    private static Sprite createKeySprite(
        Texture texture,
        float laneWidth,
        bool upsideDown,
        bool flip) => new()
    {
        Anchor = upsideDown ? Anchor.TopLeft : Anchor.BottomLeft,
        Origin = flip
            ? upsideDown ? Anchor.BottomLeft : Anchor.TopLeft
            : upsideDown ? Anchor.TopLeft : Anchor.BottomLeft,
        Size = new Vector2(
            laneWidth,
            texture.DisplayHeight
            / OsuManiaSkinConfiguration
                .LegacyPositionScaleFactor),
        Scale = new Vector2(1, flip ? -1 : 1),
        Texture = texture,
    };

    private static Container createMineExplosion(
        Anchor anchor,
        float y,
        float size) => new()
    {
        Name = "Mine explosion",
        Anchor = anchor,
        Origin = Anchor.Centre,
        Y = y,
        Size = new Vector2(size),
        Alpha = 0,
        Children = new Drawable[]
        {
            new Circle
            {
                RelativeSizeAxes = Axes.Both,
                Colour = YokkoPalette.Rose,
            },
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(size * 0.52f),
                Icon = FontAwesome.Solid.Bomb,
                Colour = Color4.White,
            },
        },
    };

    private static void addFrames(
        TextureAnimation animation,
        IReadOnlyList<Texture> frames,
        double frameDuration)
    {
        foreach (Texture texture in frames)
            animation.AddFrame(texture, frameDuration);
    }

    private static Vector2 legacyLightingSize(
        Texture texture,
        float configuredWidth,
        float columnWidth,
        double skinVersion)
    {
        float scale = 1;
        if (skinVersion >= 2.5)
        {
            float width = configuredWidth > 0
                ? configuredWidth
                : columnWidth;
            scale = width / 30;
        }

        return new Vector2(
            Math.Max(
                1,
                texture.DisplayWidth
                / OsuManiaSkinConfiguration.LegacyPositionScaleFactor
                * scale),
            Math.Max(
                1,
                texture.DisplayHeight
                / OsuManiaSkinConfiguration.LegacyPositionScaleFactor
                * scale));
    }
}

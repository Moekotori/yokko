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

    private readonly Sprite idleKey;
    private readonly Sprite pressedKey;
    private readonly TextureAnimation laneLight;
    private readonly TextureAnimation[] hitExplosions = [];
    private readonly SpriteText keyLabel;
    private readonly float baseLaneWidth;
    private readonly bool idleKeyFlipped;
    private readonly bool pressedKeyFlipped;
    private readonly bool showPressFeedback;
    private int nextHitExplosion;

    internal Container ReceptorLayer { get; }

    internal LaneColumn(
        int lane,
        string keyLabel,
        float laneWidth,
        OsuManiaSkin skin = null,
        bool showPressFeedback = true)
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
                Child = this.keyLabel = new SpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -26,
                    Text = keyLabel,
                    Font = FontUsage.Default.With(size: 18),
                    Colour = YokkoPalette.TextMuted,
                },
            };
            return;
        }

        OsuManiaSkinConfiguration configuration = skin.Configuration;
        float hitPosition = System.Math.Clamp(
            configuration.HitPosition,
            0,
            480);
        float lightPosition = System.Math.Clamp(
            configuration.LightPosition,
            0,
            480);
        Texture idleTexture = skin.GetTexture(configuration.KeyImages[lane]);
        Texture pressedTexture = skin.GetTexture(configuration.PressedKeyImages[lane]);
        var backgroundChildren = new List<Drawable>
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = configuration.LaneColours[lane],
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = configuration.ColumnLineWidths[lane],
                Colour = configuration.ColumnLineColour,
            },
        };
        var receptorChildren = new List<Drawable>();
        IReadOnlyList<Texture> lightTextures =
            skin.GetAnimationFrames(configuration.LightImage);
        IReadOnlyList<Texture> explosionTextures =
            skin.GetAnimationFrames(configuration.ExplosionImage);

        if (lightTextures.Count > 0)
        {
            Texture firstLightTexture = lightTextures[0];
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
                    firstLightTexture.DisplayHeight
                    / OsuManiaSkinConfiguration.LegacyPositionScaleFactor),
                Colour = configuration.LaneLightColours[lane],
                Alpha = 0,
                Blending = BlendingParameters.Additive,
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
            float explosionWidth = configuration.ExplosionWidth > 0
                ? configuration.ExplosionWidth
                  / OsuManiaSkinConfiguration.LegacyPositionScaleFactor
                : firstExplosionTexture.DisplayWidth
                  / OsuManiaSkinConfiguration.LegacyPositionScaleFactor;
            float explosionHeight = firstExplosionTexture.DisplayWidth > 0
                ? explosionWidth
                  * firstExplosionTexture.DisplayHeight
                  / firstExplosionTexture.DisplayWidth
                : firstExplosionTexture.DisplayHeight
                  / OsuManiaSkinConfiguration.LegacyPositionScaleFactor;
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
                    Size = new Vector2(explosionWidth, explosionHeight),
                    Alpha = 0,
                    Blending = BlendingParameters.Additive,
                };
                addFrames(explosion, explosionTextures, frameDuration);
                hitExplosions[index] = explosion;
                receptorChildren.Add(explosion);
            }
        }

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
        Width = baseLaneWidth * value;
        ReceptorLayer.Width = baseLaneWidth * value;
        if (idleKey != null)
        {
            idleKey.Scale = new Vector2(
                value,
                idleKeyFlipped ? -value : value);
        }

        if (pressedKey != null)
        {
            pressedKey.Scale = new Vector2(
                value,
                pressedKeyFlipped ? -value : value);
        }

        if (keyLabel != null)
        {
            keyLabel.Y = -26 * value;
            keyLabel.Scale = new Vector2(value);
        }

        foreach (TextureAnimation hitExplosion in hitExplosions)
            hitExplosion.Scale = new Vector2(value);

        if (laneLight != null)
            laneLight.Width = baseLaneWidth * value;
    }

    public void SetPressed(bool pressed)
    {
        if (!showPressFeedback)
            return;

        if (pressedKey != null)
        {
            pressedKey.Alpha = pressed ? 1 : 0;
            idleKey.Alpha = pressed ? 0 : 1;
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
            texture.DisplayHeight > 0
                ? texture.DisplayHeight /
                  OsuManiaSkinConfiguration.LegacyPositionScaleFactor
                : 1),
        Scale = new Vector2(1, flip ? -1 : 1),
        Texture = texture,
    };

    private static void addFrames(
        TextureAnimation animation,
        IReadOnlyList<Texture> frames,
        double frameDuration)
    {
        foreach (Texture texture in frames)
            animation.AddFrame(texture, frameDuration);
    }
}

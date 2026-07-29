using osu.Framework.Graphics;
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
    private readonly Sprite idleKey;
    private readonly Sprite pressedKey;
    private readonly Sprite laneLight;
    private readonly Sprite hitExplosion;
    private readonly SpriteText keyLabel;
    private readonly float baseLaneWidth;
    private readonly bool idleKeyFlipped;
    private readonly bool pressedKeyFlipped;
    private readonly bool showPressFeedback;

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
        var backgroundChildren = new System.Collections.Generic.List<Drawable>
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
        var receptorChildren = new System.Collections.Generic.List<Drawable>();
        Texture lightTexture = skin.GetTexture(configuration.LightImage);
        Texture explosionTexture = skin.GetTexture(configuration.ExplosionImage);

        if (lightTexture != null)
        {
            backgroundChildren.Add(laneLight = new Sprite
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
                    lightTexture.DisplayHeight
                    / OsuManiaSkinConfiguration.LegacyPositionScaleFactor),
                Texture = lightTexture,
                Alpha = 0,
                Blending = BlendingParameters.Additive,
            });
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

        if (explosionTexture != null)
        {
            receptorChildren.Add(hitExplosion = new Sprite
            {
                Name = "Hit explosion",
                Anchor = configuration.UpsideDown
                    ? Anchor.TopCentre
                    : Anchor.BottomCentre,
                Origin = Anchor.Centre,
                Y = configuration.UpsideDown
                    ? 480 - hitPosition
                    : -(480 - hitPosition),
                Size = new Vector2(
                    explosionTexture.DisplayWidth,
                    explosionTexture.DisplayHeight)
                       / OsuManiaSkinConfiguration.LegacyPositionScaleFactor,
                Texture = explosionTexture,
                Alpha = 0,
                Blending = BlendingParameters.Additive,
            });
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

        if (hitExplosion != null)
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
        if (hitExplosion == null)
            return;

        hitExplosion.FinishTransforms();
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
}

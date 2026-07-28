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
    private readonly Box pressedOverlay;
    private readonly Sprite idleKey;
    private readonly Sprite pressedKey;

    internal LaneColumn(
        int lane,
        string keyLabel,
        float laneWidth,
        OsuManiaSkin skin = null)
    {
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
                pressedOverlay = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(YokkoPalette.Cyan.R, YokkoPalette.Cyan.G, YokkoPalette.Cyan.B, 0.22f),
                    Alpha = 0,
                },
                new SpriteText
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
        Texture idleTexture = skin.GetTexture(configuration.KeyImages[lane]);
        Texture pressedTexture = skin.GetTexture(configuration.PressedKeyImages[lane]);
        pressedOverlay = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = new Color4(1f, 1f, 1f, 0.12f),
            Alpha = 0,
        };
        var children = new System.Collections.Generic.List<Drawable>
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
            pressedOverlay,
        };

        if (idleTexture != null)
        {
            children.Add(idleKey = createKeySprite(idleTexture, laneWidth));

            if (pressedTexture != null)
            {
                children.Add(pressedKey = createKeySprite(pressedTexture, laneWidth));
                pressedKey.Alpha = 0;
            }
        }
        else
        {
            children.Add(new SpriteText
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -26,
                Text = keyLabel,
                Font = FontUsage.Default.With(size: 18),
                Colour = YokkoPalette.TextMuted,
            });
        }

        InternalChildren = children.ToArray();
    }

    public void SetPressed(bool pressed)
    {
        pressedOverlay.Alpha = pressed ? 1 : 0;

        if (pressedKey != null)
        {
            pressedKey.Alpha = pressed ? 1 : 0;
            idleKey.Alpha = pressed ? 0 : 1;
        }
    }

    private static Sprite createKeySprite(Texture texture, float laneWidth) => new()
    {
        Anchor = Anchor.BottomLeft,
        Origin = Anchor.BottomLeft,
        Size = new Vector2(
            laneWidth,
            texture.DisplayWidth > 0 ? texture.DisplayHeight * laneWidth / texture.DisplayWidth : 1),
        Texture = texture,
    };
}

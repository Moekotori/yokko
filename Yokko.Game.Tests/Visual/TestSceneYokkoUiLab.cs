using System.IO;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Tests.Visual;

/// <summary>
/// Internal component gallery for developing Yokko-owned UI without entering
/// a complete game flow.
/// </summary>
[TestFixture]
public partial class TestSceneYokkoUiLab : YokkoTestScene
{
    private static readonly YokkoUiTheme preview_theme = createPreviewTheme();

    [Resolved]
    private YokkoUiThemeStore themeStore { get; set; }

    private readonly BindableBool toggleValue = new(true);
    private readonly YokkoCard surfaceCard;
    private readonly YokkoButton secondaryButton;
    private readonly YokkoButton disabledButton;
    private readonly YokkoText labTitle;
    private readonly YokkoText themeStatus;
    private readonly YokkoToggleSwitch toggle;
    private IBindable<string> activeName;
    private IBindable<string> sourcePath;
    private IBindable<string> lastError;

    public TestSceneYokkoUiLab()
    {
        Add(new YokkoThemeBox(YokkoThemeBoxRole.Background)
        {
            RelativeSizeAxes = Axes.Both,
        });

        Add(new FillFlowContainer
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Width = 1050,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 22),
            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Children = new Drawable[]
                    {
                        labTitle = new YokkoText(
                            "Yokko UI Lab",
                            36,
                            YokkoTextStyle.Display),
                        new YokkoText(
                            "Theme tokens, interaction states, and shared components",
                            14,
                            YokkoTextStyle.Body,
                            YokkoTextColourRole.Muted),
                        themeStatus = new YokkoText(
                            size: 12,
                            style: YokkoTextStyle.Caption,
                            colour: YokkoTextColourRole.Accent),
                    },
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(18, 0),
                    Children = new Drawable[]
                    {
                        surfaceCard = createCard(
                            "Surface",
                            "Default grouping surface",
                            YokkoCardStyle.Surface),
                        createCard(
                            "Elevated",
                            "Raised content and focus",
                            YokkoCardStyle.Elevated),
                        createCard(
                            "Panel",
                            "Dense secondary content",
                            YokkoCardStyle.Panel),
                    },
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(12, 0),
                    Children = new Drawable[]
                    {
                        new YokkoButton(
                            "Quiet",
                            FontAwesome.Solid.Eye,
                            () => { },
                            style: YokkoButtonStyle.Quiet),
                        secondaryButton = new YokkoButton(
                            "Secondary",
                            FontAwesome.Solid.LayerGroup,
                            () => { },
                            148,
                            style: YokkoButtonStyle.Secondary),
                        new YokkoButton(
                            "Primary",
                            FontAwesome.Solid.Play,
                            () => { },
                            132,
                            style: YokkoButtonStyle.Primary),
                        new YokkoButton(
                            "Accent",
                            () => { },
                            120,
                            style: YokkoButtonStyle.Accent),
                        disabledButton = new YokkoButton(
                            "Disabled",
                            () => { },
                            128)
                        {
                            IsEnabled = false,
                        },
                    },
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(14, 0),
                    Children = new Drawable[]
                    {
                        toggle = new YokkoToggleSwitch(toggleValue),
                        new YokkoText(
                            "Bindable theme-aware switch",
                            12,
                            YokkoTextStyle.Label,
                            YokkoTextColourRole.Muted)
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        new YokkoButton(
                            "Toggle",
                            () => toggleValue.Value = !toggleValue.Value,
                            104,
                            36,
                            YokkoButtonStyle.Quiet),
                    },
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(12, 0),
                    Children = new Drawable[]
                    {
                        new YokkoButton(
                            "Default theme",
                            () => themeStore.Reset(),
                            154),
                        new YokkoButton(
                            "Preview theme",
                            () => themeStore.Apply(
                                preview_theme,
                                "UI Lab preview"),
                            154,
                            style: YokkoButtonStyle.Primary),
                    },
                },
                new YokkoText(
                    "Hot reload: set YOKKO_UI_THEME_FILE to a theme JSON file before opening the Test Browser.",
                    11,
                    YokkoTextStyle.Caption,
                    YokkoTextColourRole.Dim),
            },
        });
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        activeName = themeStore.ActiveName.GetBoundCopy();
        sourcePath = themeStore.SourcePath.GetBoundCopy();
        lastError = themeStore.LastError.GetBoundCopy();
        activeName.BindValueChanged(_ => refreshThemeStatus(), true);
        sourcePath.BindValueChanged(_ => refreshThemeStatus());
        lastError.BindValueChanged(_ => refreshThemeStatus());
    }

    [Test]
    public void TestThemeReplacementUpdatesMigratedComponents()
    {
        AddStep("reset theme", () => themeStore.Reset());
        AddAssert(
            "default surface applied",
            () => surfaceCard.CurrentBackgroundColour
                  == YokkoUiTheme.Default.Colours.Dark.Surface);
        AddAssert(
            "shared buttons support keyboard focus",
            () => secondaryButton.AcceptsFocus
                  && !disabledButton.AcceptsFocus);
        AddStep(
            "apply preview theme",
            () => themeStore.Apply(preview_theme, "UI Lab preview"));
        AddAssert(
            "preview surface applied live",
            () => surfaceCard.CurrentBackgroundColour
                  == preview_theme.Colours.Dark.Surface);
        AddAssert(
            "button surface applied live",
            () => secondaryButton.CurrentBackgroundColour
                  == preview_theme.Colours.Dark.SurfaceElevated);
        AddAssert(
            "text colour applied live",
            () => labTitle.Colour == preview_theme.Colours.Dark.Text);
        AddAssert(
            "toggle colour applied live",
            () => toggle.CurrentTrackColour
                  == preview_theme.Colours.Brand.Ink);
        AddStep("restore default theme", () => themeStore.Reset());
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            activeName?.UnbindAll();
            sourcePath?.UnbindAll();
            lastError?.UnbindAll();
            themeStore?.Reset();
        }

        base.Dispose(isDisposing);
    }

    private void refreshThemeStatus()
    {
        if (!string.IsNullOrWhiteSpace(lastError?.Value))
        {
            themeStatus.ColourRole = YokkoTextColourRole.Danger;
            themeStatus.Text = $"Theme error: {lastError.Value}";
            return;
        }

        themeStatus.ColourRole = YokkoTextColourRole.Accent;
        themeStatus.Text = string.IsNullOrWhiteSpace(sourcePath?.Value)
            ? $"Active theme: {activeName?.Value ?? "Default"}"
            : $"Watching {Path.GetFileName(sourcePath.Value)} · {activeName.Value}";
    }

    private static YokkoCard createCard(
        string title,
        string description,
        YokkoCardStyle style) => new(style)
    {
        Size = new Vector2(338, 160),
        ContentPadding = new MarginPadding(24),
        CardContent = new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 10),
            Children = new Drawable[]
            {
                new YokkoText(
                    title,
                    19,
                    YokkoTextStyle.Heading),
                new YokkoText(
                    description,
                    12,
                    YokkoTextStyle.Body,
                    YokkoTextColourRole.Muted),
            },
        },
    };

    private static YokkoUiTheme createPreviewTheme()
    {
        YokkoUiTheme theme = YokkoUiTheme.Default;
        return theme with
        {
            Colours = theme.Colours with
            {
                Dark = theme.Colours.Dark with
                {
                    Surface = new Color4(0.11f, 0.055f, 0.16f, 0.98f),
                    SurfaceElevated = new Color4(0.16f, 0.075f, 0.22f, 1f),
                    SurfaceHover = new Color4(0.22f, 0.1f, 0.29f, 1f),
                    PanelAlt = new Color4(0.075f, 0.035f, 0.11f, 0.98f),
                    Text = new Color4(1f, 0.9f, 0.97f, 1f),
                    Cyan = new Color4(1f, 0.46f, 0.78f, 1f),
                },
                Brand = theme.Colours.Brand with
                {
                    Ink = new Color4(0.5f, 0.12f, 0.42f, 1f),
                },
            },
        };
    }
}

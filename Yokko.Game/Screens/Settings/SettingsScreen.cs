using System;
using System.Drawing;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osuTK;
using osuTK.Input;
using Yokko.Game.Audio;
using Yokko.Game.Configuration;
using Yokko.Game.Gameplay;
using Yokko.Game.Importing;
using Yokko.Game.Presentation;
using Yokko.Game.Resources;
using Yokko.Game.Screens.Main;
using Yokko.Game.Skinning.OsuMania;
using RectangleF = osu.Framework.Graphics.Primitives.RectangleF;

namespace Yokko.Game.Screens.Settings;

/// <summary>
/// Owns navigation into and out of settings. Individual settings areas own their
/// own layout and interactions so future categories do not inflate this screen.
/// </summary>
public partial class SettingsScreen : Screen
{
    private const float designedWidth = 1280;
    private const float designedHeight = 720;

    [Resolved]
    private FrameworkConfigManager frameworkConfig { get; set; }

    [Resolved]
    private YokkoConfigManager yokkoConfig { get; set; }

    [Resolved]
    private YokkoDisplaySettings displaySettings { get; set; }
    [Resolved]
    private YokkoAudioSettings audioSettings { get; set; }
    [Resolved]
    private YokkoImportSettings importSettings { get; set; }
    [Resolved]
    private YokkoGameplaySettings gameplaySettings { get; set; }
    [Resolved]
    private OsuManiaSkinLibrary skinLibrary { get; set; }
    [Resolved]
    private YokkoResourceStorage resourceStorage { get; set; }

    private Bindable<Size> windowedSize;
    private Bindable<WindowMode> windowMode;
    private IBindable<DisplayMode> currentDisplayMode;
    private Bindable<string> locale;
    private Texture mascotTexture;
    private SettingsSidebar sidebar;
    private Container contentHost;
    private Drawable activePanel;

    [Resolved]
    private GameHost host { get; set; }

    internal SettingsPageKind CurrentPage { get; private set; } = SettingsPageKind.Display;
    internal Drawable ActivePanel => activePanel;

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        windowedSize = frameworkConfig.GetBindable<Size>(FrameworkSetting.WindowedSize);
        windowMode = frameworkConfig.GetBindable<WindowMode>(FrameworkSetting.WindowMode);
        currentDisplayMode = host.Window?.CurrentDisplayMode
                             ?? new Bindable<DisplayMode>(new DisplayMode(
                                 null,
                                 windowedSize.Value,
                                 0,
                                 60,
                                 0));
        locale = frameworkConfig.GetBindable<string>(FrameworkSetting.Locale);
        mascotTexture = textures.Get("yokko")
                                .Crop(new RectangleF(80, 1840, 1200, 1360));
        CurrentPage = parseRememberedPage(yokkoConfig.GetLastSettingsPage());

        sidebar = new SettingsSidebar(
            textures.Get("home-logo"),
            this.Exit,
            CurrentPage,
            OpenPage);
        contentHost = new Container { RelativeSizeAxes = Axes.Both };

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = HomeControlColours.Ivory,
            },
            new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(designedWidth, designedHeight),
                Children = new Drawable[]
                {
                    sidebar,
                    contentHost,
                },
            },
        };

        OpenPage(CurrentPage);
    }

    internal void OpenPage(SettingsPageKind page)
    {
        if (contentHost == null)
        {
            CurrentPage = page;
            return;
        }

        if (CurrentPage == page && activePanel != null)
            return;

        CurrentPage = page;
        yokkoConfig.SetLastSettingsPage(page.ToString());

        sidebar.SetSelected(page);
        activePanel = page switch
        {
            SettingsPageKind.General => new GeneralSettingsPanel(
                locale,
                gameplaySettings),
            SettingsPageKind.Display => new DisplaySettingsPanel(
                mascotTexture,
                windowedSize,
                windowMode,
                displaySettings.UiScale,
                displaySettings.FrameLimit,
                displaySettings.ShowPerformanceReadout,
                currentDisplayMode,
                size => frameworkConfig.SetValue(FrameworkSetting.WindowedSize, size),
                mode => frameworkConfig.SetValue(FrameworkSetting.WindowMode, mode)),
            SettingsPageKind.Audio => new AudioSettingsPanel(
                audioSettings,
                gameplaySettings),
            SettingsPageKind.Gameplay => new GameplaySettingsPanel(
                gameplaySettings,
                audioSettings),
            SettingsPageKind.Skins => new SkinSettingsPanel(skinLibrary),
            SettingsPageKind.Import => new ImportSettingsPanel(
                importSettings,
                resourceStorage,
                yokkoConfig),
            _ => new SettingsPlaceholderPanel(SettingsPages.Get(page)),
        };

        activePanel.Alpha = 0;
        activePanel.X = 10;
        contentHost.Child = activePanel;
        activePanel.FadeIn(180, Easing.OutQuint);
        activePanel.MoveToX(0, 180, Easing.OutQuint);
    }

    internal bool DismissTransientUi()
    {
        if (activePanel is ISettingsTransientUi transientUi && transientUi.DismissTransientUi())
            return true;

        return sidebar?.DismissTransientUi() == true;
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        yokkoConfig.Save();
        frameworkConfig.Save();
        return base.OnExiting(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (activePanel is GameplaySettingsPanel gameplayPanel &&
            gameplayPanel.HandleKeyDown(e.Key))
            return true;

        if (e.Key != Key.Escape)
            return base.OnKeyDown(e);

        if (DismissTransientUi())
            return true;

        this.Exit();
        return true;
    }

    private static SettingsPageKind parseRememberedPage(string page) =>
        Enum.TryParse(page, out SettingsPageKind remembered)
            ? remembered
            : SettingsPageKind.Display;
}

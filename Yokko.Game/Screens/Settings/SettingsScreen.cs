using System.Drawing;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osuTK;
using osuTK.Input;
using Yokko.Game.Audio;
using Yokko.Game.Importing;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;
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
    private YokkoDisplaySettings displaySettings { get; set; }
    [Resolved]
    private YokkoAudioSettings audioSettings { get; set; }
    [Resolved]
    private YokkoImportSettings importSettings { get; set; }

    private Bindable<Size> windowedSize;
    private Bindable<WindowMode> windowMode;
    private Bindable<string> locale;
    private Texture mascotTexture;
    private SettingsSidebar sidebar;
    private Container contentHost;
    private Drawable activePanel;

    internal SettingsPageKind CurrentPage { get; private set; } = SettingsPageKind.Display;
    internal Drawable ActivePanel => activePanel;

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        windowedSize = frameworkConfig.GetBindable<Size>(FrameworkSetting.WindowedSize);
        windowMode = frameworkConfig.GetBindable<WindowMode>(FrameworkSetting.WindowMode);
        locale = frameworkConfig.GetBindable<string>(FrameworkSetting.Locale);
        mascotTexture = textures.Get("yokko")
                                .Crop(new RectangleF(80, 1840, 1200, 1360));

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

        sidebar.SetSelected(page);
        activePanel = page switch
        {
            SettingsPageKind.General => new GeneralSettingsPanel(locale),
            SettingsPageKind.Display => new DisplaySettingsPanel(
                mascotTexture,
                windowedSize,
                windowMode,
                displaySettings.UiScale,
                size => frameworkConfig.SetValue(FrameworkSetting.WindowedSize, size),
                mode => frameworkConfig.SetValue(FrameworkSetting.WindowMode, mode)),
            SettingsPageKind.Audio => new AudioSettingsPanel(audioSettings),
            SettingsPageKind.Import => new ImportSettingsPanel(importSettings),
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

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key != Key.Escape)
            return base.OnKeyDown(e);

        if (DismissTransientUi())
            return true;

        this.Exit();
        return true;
    }
}

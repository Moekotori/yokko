using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Configuration;
using Yokko.Game.Importing;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Resources;
using Yokko.Game.Screens.Main;
using Yokko.Import;

namespace Yokko.Game.Screens.Settings;

/// <summary>
/// Presents importer capabilities from the registry and owns the preferences
/// that affect package selection, keysound preservation and warning display.
/// </summary>
internal partial class ImportSettingsPanel : CompositeDrawable, ISettingsTransientUi
{
    private readonly YokkoImportSettings settings;
    private readonly YokkoResourceStorage resourceStorage;
    private readonly YokkoConfigManager yokkoConfig;
    private readonly IResourceDirectoryPicker resourceDirectoryPicker;
    private readonly YokkoExternalOsuSettings externalOsuSettings;
    private readonly ImportedChartLibrary importedChartLibrary;
    private SpriteText locationPathText;
    private SpriteText migrationStatusText;
    private SpriteText externalOsuPathText;
    private SpriteText externalOsuStatusText;
    private SpriteText watchFolderPathText;
    private readonly ResourceDirectorySelectorOverlay directorySelector;
    private readonly ResourceDirectorySelectorOverlay externalDirectorySelector;
    private readonly ResourceDirectorySelectorOverlay watchFolderSelector;
    private bool migrationInProgress;
    private bool externalScanInProgress;
    private bool directoryPickerOpen;

    internal int FormatFamilyCount => KnownChartImporters.Capabilities.Count;
    internal int FileTypeCount => KnownChartImporters.FileExtensions.Length;
    internal bool PreferKeysounds => settings.PreferKeysounds.Value;
    internal bool PreferSscSimfiles => settings.PreferSscSimfiles.Value;
    internal bool EnableBmsScratch => settings.EnableBmsScratch.Value;
    internal bool ShowCompatibilityWarnings => settings.ShowCompatibilityWarnings.Value;

    internal bool WatchFolderEnabled => settings.WatchFolderEnabled.Value;
    internal string WatchFolderPath => settings.WatchFolderPath.Value;

    public ImportSettingsPanel(
        YokkoImportSettings settings,
        YokkoResourceStorage resourceStorage,
        YokkoConfigManager yokkoConfig,
        IResourceDirectoryPicker resourceDirectoryPicker,
        YokkoExternalOsuSettings externalOsuSettings,
        ImportedChartLibrary importedChartLibrary)
    {
        this.settings = settings;
        this.resourceStorage = resourceStorage;
        this.yokkoConfig = yokkoConfig;
        this.resourceDirectoryPicker = resourceDirectoryPicker;
        this.externalOsuSettings = externalOsuSettings;
        this.importedChartLibrary = importedChartLibrary;
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            SettingsChrome.CreateHeader(
                YokkoStrings.Get("settings.import.title"),
                YokkoStrings.Get("settings.import.subtitle"),
                FontAwesome.Solid.FolderOpen,
                8),
            createImporterStatus(),
            new SpriteText
            {
                Position = new Vector2(378, 251),
                Text = YokkoStrings.Get("settings.import.section_formats"),
                Font = HomeTypography.Display(23),
                Colour = HomeControlColours.Navy,
            },
            createFormatCards(),
            new SpriteText
            {
                Position = new Vector2(378, 380),
                Text = YokkoStrings.Get("settings.import.section_behaviour"),
                Font = HomeTypography.Display(23),
                Colour = HomeControlColours.Navy,
            },
            createBehaviourCards(),
            createWatchFolderCard(),
            createLocationCard(),
            createExternalOsuCard(),
            directorySelector = new ResourceDirectorySelectorOverlay(
                migrateTo,
                migrateToDefault),
            externalDirectorySelector = new ResourceDirectorySelectorOverlay(
                setExternalOsuPath,
                disableExternalOsu),
            watchFolderSelector = new ResourceDirectorySelectorOverlay(
                setWatchFolderPath,
                disableWatchFolder),
        };

        locationPathText.Text = resourceStorage.RootPath;
        migrationStatusText.Text = YokkoStrings.Get(
            "settings.import.resource_change");
        refreshWatchFolderStatus();
        refreshExternalOsuStatus();
    }

    internal void SetWatchFolderEnabled(bool value) =>
        settings.WatchFolderEnabled.Value = value;

    internal void SetWatchFolderPath(string path) => setWatchFolderPath(path);

    internal void SetPreferKeysounds(bool value) => settings.PreferKeysounds.Value = value;

    internal void SetPreferSscSimfiles(bool value) => settings.PreferSscSimfiles.Value = value;

    internal void SetEnableBmsScratch(bool value) => settings.EnableBmsScratch.Value = value;

    internal void SetShowCompatibilityWarnings(bool value) => settings.ShowCompatibilityWarnings.Value = value;

    private Drawable createImporterStatus() => SettingsChrome.CreateStickerFrame(new Container
    {
        Position = new Vector2(378, 157),
        Size = new Vector2(840, 72),
        Masking = true,
        CornerRadius = 8,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.StatusCyan,
            },
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 45,
                Size = new Vector2(50),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 45,
                Size = new Vector2(23),
                Icon = FontAwesome.Solid.Check,
                Colour = HomeControlColours.Navy,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 88,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.import.status_title"),
                        Font = HomeTypography.Display(21),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Text = YokkoStrings.Get(
                            "settings.import.status_metadata",
                            FormatFamilyCount,
                            FileTypeCount),
                        Font = HomeTypography.Body(16),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            createStatusBadge(),
        },
    });

    private static Drawable createStatusBadge() => new Container
    {
        Anchor = Anchor.CentreRight,
        Origin = Anchor.CentreRight,
        X = -22,
        Size = new Vector2(116, 30),
        Masking = true,
        CornerRadius = 15,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = YokkoStrings.Get("settings.import.ready"),
                Font = HomeTypography.Display(14),
                Spacing = new Vector2(0.8f, 0),
                Colour = HomeControlColours.Navy,
            },
        },
    };

    private static Drawable createFormatCards()
    {
        var flow = new FillFlowContainer
        {
            Position = new Vector2(378, 284),
            Size = new Vector2(840, 70),
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(10, 0),
        };

        float[] widths = [140, 140, 140, 210, 170];
        string[] names = ["osu!mania", "Quaver", "Malody", "Etterna / StepMania", "BMS"];
        ChartImportCapability[] capabilities = KnownChartImporters.Capabilities.ToArray();

        for (int index = 0; index < capabilities.Length; index++)
        {
            ChartImportCapability capability = capabilities[index];
            flow.Add(new ImportFormatCard(
                names[Math.Min(index, names.Length - 1)],
                string.Join("  ", capability.FileExtensions),
                capability.Format == Yokko.Core.Beatmaps.ChartSourceFormat.Bms,
                widths[Math.Min(index, widths.Length - 1)]));
        }

        return flow;
    }

    private Drawable createBehaviourCards() => new FillFlowContainer
    {
        Position = new Vector2(378, 413),
        Size = new Vector2(840, 106),
        Direction = FillDirection.Horizontal,
        Spacing = new Vector2(15, 0),
        Children = new Drawable[]
        {
            new ImportPreferenceCard(
                YokkoStrings.Get("settings.import.prefer_keysounds"),
                YokkoStrings.Get("settings.import.prefer_keysounds_note"),
                FontAwesome.Solid.Keyboard,
                settings.PreferKeysounds,
                198.75f),
            new ImportPreferenceCard(
                YokkoStrings.Get("settings.import.prefer_ssc"),
                YokkoStrings.Get("settings.import.prefer_ssc_note"),
                FontAwesome.Solid.Bars,
                settings.PreferSscSimfiles,
                198.75f),
            new ImportPreferenceCard(
                YokkoStrings.Get("settings.import.bms_scratch"),
                YokkoStrings.Get("settings.import.bms_scratch_note"),
                FontAwesome.Solid.CompactDisc,
                settings.EnableBmsScratch,
                198.75f),
            new ImportPreferenceCard(
                YokkoStrings.Get("settings.import.show_warnings"),
                YokkoStrings.Get("settings.import.show_warnings_note"),
                FontAwesome.Solid.InfoCircle,
                settings.ShowCompatibilityWarnings,
                198.75f),
        },
    };

    public bool DismissTransientUi() =>
        watchFolderSelector.Dismiss()
        || externalDirectorySelector.Dismiss()
        || directorySelector.Dismiss();

    private Drawable createWatchFolderCard() => new ClickableContainer
    {
        Position = new Vector2(378, 520),
        Size = new Vector2(840, 54),
        Action = openWatchFolderSelector,
        Masking = true,
        CornerRadius = 7,
        BorderThickness = 1,
        BorderColour = SettingsTheme.Divider,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 28,
                Size = new Vector2(20),
                Icon = FontAwesome.Solid.Eye,
                Colour = HomeControlColours.Cyan,
            },
            new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 58,
                Size = new Vector2(430, 44),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.import.watch_folder_title"),
                        Font = HomeTypography.Display(17),
                        Colour = HomeControlColours.Navy,
                    },
                    watchFolderPathText = new SpriteText
                    {
                        Y = 22,
                        Width = 430,
                        Truncate = true,
                        Font = HomeTypography.Body(14),
                        Colour = SettingsTheme.MutedNavy,
                    },
                },
            },
            new YokkoToggleSwitch(settings.WatchFolderEnabled)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -150,
            },
            new SettingsSkinActionButton(
                YokkoStrings.Get("settings.import.watch_folder_select"),
                FontAwesome.Solid.FolderOpen,
                openWatchFolderSelector,
                false)
            {
                Position = new Vector2(726, 8),
            },
        },
    };

    private Drawable createLocationCard() => new ClickableContainer
    {
        Position = new Vector2(378, 578),
        Size = new Vector2(840, 54),
        Action = openDirectorySelector,
        Masking = true,
        CornerRadius = 7,
        BorderThickness = 1,
        BorderColour = SettingsTheme.Divider,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.PaleCyan,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 28,
                Size = new Vector2(20),
                Icon = FontAwesome.Solid.FolderOpen,
                Colour = HomeControlColours.Pink,
            },
            new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 58,
                Size = new Vector2(635, 44),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.import.section_locations"),
                        Font = HomeTypography.Display(17),
                        Colour = HomeControlColours.Navy,
                    },
                    locationPathText = new SpriteText
                    {
                        Y = 22,
                        Width = 635,
                        Truncate = true,
                        Font = HomeTypography.Body(14),
                        Colour = SettingsTheme.MutedNavy,
                    },
                },
            },
            migrationStatusText = new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -24,
                Width = 135,
                Truncate = true,
                Text = YokkoStrings.Get("settings.import.resource_change"),
                Font = HomeTypography.Display(14),
                Colour = HomeControlColours.Pink,
            },
        },
    };

    private Drawable createExternalOsuCard() => new ClickableContainer
    {
        Position = new Vector2(378, 636),
        Size = new Vector2(840, 54),
        Action = openExternalOsuSelector,
        Masking = true,
        CornerRadius = 7,
        BorderThickness = 1,
        BorderColour = SettingsTheme.Divider,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.93f, 0.97f, 1f, 1f),
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 28,
                Size = new Vector2(20),
                Icon = FontAwesome.Solid.BookOpen,
                Colour = HomeControlColours.Cyan,
            },
            new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 58,
                Size = new Vector2(280, 44),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get(
                            "settings.import.external_osu_title"),
                        Font = HomeTypography.Display(17),
                        Colour = HomeControlColours.Navy,
                    },
                    externalOsuPathText = new SpriteText
                    {
                        Y = 22,
                        Width = 280,
                        Truncate = true,
                        Font = HomeTypography.Body(14),
                        Colour = SettingsTheme.MutedNavy,
                    },
                },
            },
            externalOsuStatusText = new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -350,
                Width = 132,
                Truncate = true,
                Font = HomeTypography.Display(13),
                Colour = HomeControlColours.Pink,
            },
            new SettingsSkinActionButton(
                YokkoStrings.Get("chart_library.disable_osu"),
                FontAwesome.Solid.PowerOff,
                disableExternalOsu,
                false)
            {
                Position = new Vector2(502, 8),
            },
            new SettingsSkinActionButton(
                YokkoStrings.Get("settings.import.external_osu_auto_find"),
                FontAwesome.Solid.Search,
                autoFindExternalOsu,
                true)
            {
                Position = new Vector2(614, 8),
            },
            new SettingsSkinActionButton(
                YokkoStrings.Get("settings.import.external_osu_manual_select"),
                FontAwesome.Solid.FolderOpen,
                openExternalOsuSelector,
                false)
            {
                Position = new Vector2(726, 8),
            },
        },
    };

    private void disableWatchFolder()
    {
        settings.WatchFolderEnabled.Value = false;
        settings.WatchFolderPath.Value = string.Empty;
        yokkoConfig.Save();
        refreshWatchFolderStatus();
    }

    private void setWatchFolderPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        settings.WatchFolderPath.Value = path;
        settings.WatchFolderEnabled.Value = true;
        yokkoConfig.Save();
        refreshWatchFolderStatus();
    }

    private async void openWatchFolderSelector()
    {
        if (directoryPickerOpen)
            return;

        string initialPath = settings.WatchFolderPath.Value;
        if (string.IsNullOrWhiteSpace(initialPath))
            initialPath = resourceStorage.RootPath;

        if (!resourceDirectoryPicker.IsAvailable)
        {
            watchFolderSelector.Open(initialPath);
            return;
        }

        directoryPickerOpen = true;
        string selectedPath;
        try
        {
            selectedPath = await resourceDirectoryPicker.PickAsync(initialPath);
        }
        catch
        {
            return;
        }
        finally
        {
            directoryPickerOpen = false;
        }

        if (!string.IsNullOrWhiteSpace(selectedPath))
            setWatchFolderPath(selectedPath);
    }

    private void refreshWatchFolderStatus()
    {
        string path = settings.WatchFolderPath.Value;
        watchFolderPathText.Text = string.IsNullOrWhiteSpace(path)
            ? YokkoStrings.Get("settings.import.watch_folder_unconfigured")
            : path;
    }

    private void autoFindExternalOsu()
    {
        if (externalScanInProgress || directoryPickerOpen)
            return;

        string detected = ExternalOsuSongsLocator.Find(
            externalOsuSettings.SongsPath.Value);
        if (detected == null)
        {
            externalOsuStatusText.Text = YokkoStrings.Get(
                "settings.import.external_osu_not_found");
            return;
        }

        setExternalOsuPath(detected);
    }

    private async void openDirectorySelector()
    {
        if (migrationInProgress || directoryPickerOpen)
            return;

        if (!resourceDirectoryPicker.IsAvailable)
        {
            directorySelector.Open(resourceStorage.RootPath);
            return;
        }

        directoryPickerOpen = true;
        string selectedPath;

        try
        {
            selectedPath = await resourceDirectoryPicker.PickAsync(
                resourceStorage.RootPath);
        }
        catch
        {
            migrationStatusText.Text = YokkoStrings.Get(
                "settings.import.resource_failed");
            return;
        }
        finally
        {
            directoryPickerOpen = false;
        }

        if (!string.IsNullOrWhiteSpace(selectedPath))
            migrateTo(selectedPath);
    }

    private async void openExternalOsuSelector()
    {
        if (externalScanInProgress || directoryPickerOpen)
            return;

        string initialPath = externalOsuSettings.SongsPath.Value;
        if (string.IsNullOrWhiteSpace(initialPath))
        {
            string detected = ExternalOsuSongsLocator.Find();
            initialPath = detected ?? resourceStorage.RootPath;
        }

        if (!resourceDirectoryPicker.IsAvailable)
        {
            externalDirectorySelector.Open(initialPath);
            return;
        }

        directoryPickerOpen = true;
        string selectedPath;
        try
        {
            selectedPath = await resourceDirectoryPicker.PickAsync(initialPath);
        }
        catch
        {
            externalOsuStatusText.Text = YokkoStrings.Get(
                "settings.import.external_osu_failed");
            return;
        }
        finally
        {
            directoryPickerOpen = false;
        }

        if (!string.IsNullOrWhiteSpace(selectedPath))
            setExternalOsuPath(selectedPath);
    }

    private void setExternalOsuPath(string path)
    {
        if (externalScanInProgress)
            return;

        beginExternalScan(System.Threading.Tasks.Task.Run(
            () => importedChartLibrary.SetExternalOsuSongsPathAsync(path)));
    }

    private void disableExternalOsu()
    {
        importedChartLibrary.DisableExternalOsu();
        yokkoConfig.Save();
        refreshExternalOsuStatus();
    }

    private void beginExternalScan(
        System.Threading.Tasks.Task<ExternalOsuLibraryResult> scan)
    {
        if (externalScanInProgress)
            return;

        externalScanInProgress = true;
        externalOsuStatusText.Text = YokkoStrings.Get(
            "settings.import.external_osu_scanning");
        _ = scan.ContinueWith(task => Schedule(() =>
        {
            externalScanInProgress = false;
            if (!task.IsCompletedSuccessfully || !task.Result.Success)
            {
                externalOsuStatusText.Text = YokkoStrings.Get(
                    "settings.import.external_osu_failed");
                return;
            }

            yokkoConfig.Save();
            refreshExternalOsuStatus();
        }));
    }

    private void refreshExternalOsuStatus()
    {
        string path = externalOsuSettings.SongsPath.Value;
        externalOsuPathText.Text = string.IsNullOrWhiteSpace(path)
            ? YokkoStrings.Get("settings.import.external_osu_unconfigured")
            : path;
        externalOsuStatusText.Text = string.IsNullOrWhiteSpace(path)
            ? YokkoStrings.Get("settings.import.resource_change")
            : YokkoStrings.Get(
                "settings.import.external_osu_count",
                importedChartLibrary.ExternalOsuChartCount);
    }

    private void migrateTo(string path) => beginMigration(
        resourceStorage.MigrateAsync(path));

    private void migrateToDefault() => beginMigration(
        resourceStorage.MigrateToDefaultAsync());

    private void beginMigration(System.Threading.Tasks.Task<ResourceMigrationResult> migration)
    {
        if (migrationInProgress)
            return;

        migrationInProgress = true;
        migrationStatusText.Text = YokkoStrings.Get(
            "settings.import.resource_migrating");

        _ = migration.ContinueWith(task => Schedule(() =>
        {
            migrationInProgress = false;

            if (!task.IsCompletedSuccessfully)
            {
                migrationStatusText.Text = YokkoStrings.Get(
                    "settings.import.resource_failed");
                return;
            }

            ResourceMigrationResult result = task.Result;
            locationPathText.Text = result.RootPath;
            if (result.Success)
                yokkoConfig.Save();

            migrationStatusText.Text = result.Success
                ? result.PreviousDataRetained
                    ? YokkoStrings.Get(
                        "settings.import.resource_migrated_retained")
                    : YokkoStrings.Get(
                        "settings.import.resource_migrated")
                : YokkoStrings.Get("settings.import.resource_failed");
        }));
    }

}

internal partial class ImportFormatCard : CompositeDrawable
{
    public ImportFormatCard(string name, string extensions, bool partialSupport, float width)
    {
        Size = new Vector2(width, 70);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1;
        BorderColour = SettingsTheme.Divider;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteText
            {
                Position = new Vector2(12, 10),
                Text = name,
                Font = HomeTypography.Display(16),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(12, 37),
                Text = extensions,
                Font = HomeTypography.Body(14),
                Colour = SettingsTheme.MutedNavy,
            },
            new Circle
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-13, 13),
                Size = new Vector2(19),
                Colour = partialSupport ? HomeControlColours.Yellow : SettingsTheme.StatusCyan,
            },
            new SpriteIcon
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-13, 13),
                Size = new Vector2(10),
                Icon = partialSupport ? FontAwesome.Solid.InfoCircle : FontAwesome.Solid.Check,
                Colour = HomeControlColours.Navy,
            },
        };
    }
}

internal partial class ImportPreferenceCard : ClickableContainer
{
    private readonly BindableBool value;
    private readonly Box background;
    private readonly SpriteText state;

    public override bool AcceptsFocus => true;

    public ImportPreferenceCard(
        LocalisableString title,
        LocalisableString note,
        IconUsage icon,
        BindableBool value,
        float width = 270)
    {
        this.value = value;
        Action = () => value.Value = !value.Value;
        Size = new Vector2(width, 106);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.Divider;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Position = new Vector2(15, 14),
                Size = new Vector2(18),
                Icon = icon,
                Colour = HomeControlColours.Pink,
            },
            new SpriteText
            {
                Position = new Vector2(43, 12),
                Width = width - 58,
                Truncate = true,
                Text = title,
                Font = HomeTypography.Display(17),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(15, 43),
                Width = width - 30,
                Truncate = true,
                Text = note,
                Font = HomeTypography.Body(14),
                Colour = SettingsTheme.MutedNavy,
            },
            new YokkoToggleSwitch(value)
            {
                Position = new Vector2(15, 74),
            },
            state = new SpriteText
            {
                Position = new Vector2(74, 76),
                Width = width - 89,
                Truncate = true,
                Font = HomeTypography.Display(14),
                Colour = HomeControlColours.Navy,
            },
        };

        value.BindValueChanged(onValueChanged, true);
    }

    private void onValueChanged(ValueChangedEvent<bool> change)
    {
        state.Text = YokkoStrings.Get(change.NewValue
            ? "settings.import.enabled"
            : "settings.import.disabled");
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(SettingsTheme.PaleCyan, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.FadeColour(Color4.White, 140, Easing.OutQuint);

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            Action?.Invoke();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        BorderColour = HomeControlColours.Pink;
        BorderThickness = 2.4f;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderColour = SettingsTheme.Divider;
        BorderThickness = 1.2f;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}

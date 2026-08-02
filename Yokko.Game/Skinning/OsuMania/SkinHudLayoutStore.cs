using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Skinning.OsuMania;

/// <summary>
/// Projects the live gameplay HUD bindables onto one durable profile per skin.
/// The files deliberately live in Yokko's settings storage rather than inside
/// imported skin packages, so replacing or moving the resource library cannot
/// discard the user's layout.
/// </summary>
internal sealed class SkinHudLayoutStore : IDisposable
{
    private const int schema_version = 1;
    private const int save_delay_milliseconds = 250;
    private const string profile_directory_name = "skin-hud-layouts";

    private static readonly JsonSerializerOptions json_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly object sync = new();
    private readonly Timer saveTimer;

    private YokkoGameplaySettings gameplaySettings;
    private YokkoSkinSettings skinSettings;
    private OsuManiaSkinLibrary skinLibrary;
    private string profileDirectory;
    private string activeSkinId = string.Empty;
    private string pendingSkinId;
    private HudLayoutSnapshot pendingLayout;
    private Dictionary<string, HudLayoutSnapshot> editSessionLayouts;
    private string editSessionInitialSkinId;
    private bool applying;
    private bool suppressSkinChange;
    private bool disposed;

    public SkinHudLayoutStore()
    {
        saveTimer = new Timer(_ => savePending(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Initialise(
        Storage storage,
        YokkoGameplaySettings gameplay,
        YokkoSkinSettings skins,
        OsuManiaSkinLibrary library = null)
    {
        ArgumentNullException.ThrowIfNull(storage);
        gameplaySettings = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
        skinSettings = skins ?? throw new ArgumentNullException(nameof(skins));
        skinLibrary = library;
        profileDirectory = storage.GetFullPath(profile_directory_name, true);
        Directory.CreateDirectory(profileDirectory);

        lock (sync)
        {
            activeSkinId = normaliseSkinId(skinSettings.SelectedSkinId.Value);
            string path = getProfilePath(activeSkinId);

            if (tryLoad(path, activeSkinId, out HudLayoutSnapshot saved))
            {
                applyLocked(saved);
            }
            else if (!Directory.EnumerateFiles(profileDirectory, "*.json").Any())
            {
                // The old flat yokko.ini layout is already present in the live
                // bindables here. Capture it once as the first skin profile.
                writeLocked(activeSkinId, captureLocked());
            }
            else
            {
                HudLayoutSnapshot defaults = resetAndCaptureLocked();
                writeLocked(activeSkinId, defaults);
            }
        }

        foreach (GameplayHudLayoutSetting setting in gameplaySettings.HudLayoutSettings)
            setting.Bindable.ValueChanged += onLayoutChanged;

        skinSettings.SelectedSkinId.ValueChanged += onSkinChanged;
        if (skinLibrary != null)
            skinLibrary.SkinDeleted += Forget;
    }

    /// <summary>
    /// Starts an editor transaction. Skin switches remain live and retain their
    /// in-session values, but no profile file is changed until Save is chosen.
    /// </summary>
    public void BeginEditSession()
    {
        lock (sync)
        {
            ensureInitialised();
            if (editSessionLayouts != null)
                return;

            flushLocked();
            editSessionInitialSkinId = activeSkinId;
            editSessionLayouts = new Dictionary<string, HudLayoutSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                [activeSkinId] = captureLocked(),
            };
        }
    }

    public void CommitEditSession()
    {
        lock (sync)
        {
            if (editSessionLayouts == null)
                return;

            editSessionLayouts[activeSkinId] = captureLocked();
            foreach ((string skinId, HudLayoutSnapshot layout) in editSessionLayouts)
                writeLocked(skinId, layout);

            editSessionLayouts = null;
            editSessionInitialSkinId = null;
        }
    }

    public void CancelEditSession()
    {
        lock (sync)
        {
            cancelEditSessionLocked();
        }
    }

    private void cancelEditSessionLocked()
    {
        if (editSessionLayouts == null)
            return;

        string initialSkinId = resolveRollbackSkinIdLocked(
            editSessionInitialSkinId);
        editSessionLayouts = null;
        editSessionInitialSkinId = null;
        suppressSkinChange = true;

        try
        {
            skinSettings.SelectedSkinId.Value = initialSkinId;
            activeSkinId = initialSkinId;

            if (tryLoad(getProfilePath(activeSkinId), activeSkinId, out HudLayoutSnapshot saved))
                applyLocked(saved);
            else
                applyLocked(resetAndCaptureLocked());
        }
        finally
        {
            suppressSkinChange = false;
        }
    }

    private string resolveRollbackSkinIdLocked(string skinId)
    {
        string normalised = normaliseSkinId(skinId);
        if (normalised.Length == 0 || skinLibrary == null)
            return normalised;

        return skinLibrary.GetInstalledSkins().Any(
            installed => idsEqual(installed.Id, normalised))
            ? normalised
            : string.Empty;
    }

    public void Flush()
    {
        lock (sync)
        {
            ensureInitialised();
            if (editSessionLayouts == null)
                flushLocked();
        }
    }

    public void Forget(string skinId)
    {
        lock (sync)
        {
            if (profileDirectory == null)
                return;

            string normalised = normaliseSkinId(skinId);
            if (editSessionLayouts != null)
                editSessionLayouts.Remove(normalised);
            if (idsEqual(editSessionInitialSkinId, normalised))
                editSessionInitialSkinId = string.Empty;

            if (pendingSkinId != null && idsEqual(pendingSkinId, normalised))
                clearPendingLocked();

            deleteIfExists(getProfilePath(normalised));
            deleteIfExists(getProfilePath(normalised) + ".tmp");
        }
    }

    internal string GetProfilePathForTesting(string skinId)
    {
        lock (sync)
        {
            ensureInitialised();
            return getProfilePath(normaliseSkinId(skinId));
        }
    }

    private void onLayoutChanged(ValueChangedEvent<double> _)
    {
        lock (sync)
        {
            if (applying || disposed)
                return;

            HudLayoutSnapshot layout = captureLocked();
            if (editSessionLayouts != null)
            {
                editSessionLayouts[activeSkinId] = layout;
                return;
            }

            pendingSkinId = activeSkinId;
            pendingLayout = layout;
            saveTimer.Change(save_delay_milliseconds, Timeout.Infinite);
        }
    }

    private void onSkinChanged(ValueChangedEvent<string> change)
    {
        lock (sync)
        {
            if (suppressSkinChange || disposed)
                return;

            string nextSkinId = normaliseSkinId(change.NewValue);
            if (idsEqual(activeSkinId, nextSkinId))
                return;

            if (editSessionLayouts != null)
                editSessionLayouts[activeSkinId] = captureLocked();
            else
                flushLocked();

            activeSkinId = nextSkinId;
            HudLayoutSnapshot nextLayout;

            if (editSessionLayouts?.TryGetValue(activeSkinId, out nextLayout) != true)
            {
                if (!tryLoad(getProfilePath(activeSkinId), activeSkinId, out nextLayout))
                {
                    nextLayout = resetAndCaptureLocked();
                    if (editSessionLayouts == null)
                        writeLocked(activeSkinId, nextLayout);
                }

                if (editSessionLayouts != null)
                    editSessionLayouts[activeSkinId] = nextLayout;
            }

            applyLocked(nextLayout);
        }
    }

    private void savePending()
    {
        lock (sync)
        {
            if (disposed || pendingSkinId == null)
                return;

            writeLocked(pendingSkinId, pendingLayout);
            clearPendingLocked();
        }
    }

    private void flushLocked()
    {
        clearPendingLocked();
        writeLocked(activeSkinId, captureLocked());
    }

    private void clearPendingLocked()
    {
        saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
        pendingSkinId = null;
        pendingLayout = null;
    }

    private HudLayoutSnapshot resetAndCaptureLocked()
    {
        applying = true;
        try
        {
            gameplaySettings.ResetGameplayLayout();
            return captureLocked();
        }
        finally
        {
            applying = false;
        }
    }

    private HudLayoutSnapshot captureLocked() =>
        new(gameplaySettings.HudLayoutSettings.ToDictionary(
            setting => setting.Name,
            setting => setting.Bindable.Value,
            StringComparer.Ordinal));

    private void applyLocked(HudLayoutSnapshot layout)
    {
        applying = true;
        try
        {
            gameplaySettings.ResetGameplayLayout();
            foreach (GameplayHudLayoutSetting setting in gameplaySettings.HudLayoutSettings)
            {
                if (!layout.Values.TryGetValue(setting.Name, out double value)
                    || !double.IsFinite(value))
                {
                    continue;
                }

                setting.Bindable.Value = Math.Clamp(value, setting.Minimum, setting.Maximum);
            }
        }
        finally
        {
            applying = false;
        }
    }

    private bool tryLoad(string path, string skinId, out HudLayoutSnapshot layout)
    {
        layout = null;
        if (!File.Exists(path))
            return false;

        try
        {
            ProfileDocument document = JsonSerializer.Deserialize<ProfileDocument>(
                File.ReadAllText(path),
                json_options);

            if (document?.SchemaVersion != schema_version
                || document.Layout == null
                || !idsEqual(normaliseSkinId(document.SkinId), skinId))
            {
                return false;
            }

            layout = new HudLayoutSnapshot(
                new Dictionary<string, double>(document.Layout, StringComparer.Ordinal));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Logger.Error(ex, $"Could not load the HUD layout for skin '{skinId}'.");
            return false;
        }
    }

    private void writeLocked(string skinId, HudLayoutSnapshot layout)
    {
        string path = getProfilePath(skinId);
        string temporaryPath = path + ".tmp";
        var document = new ProfileDocument
        {
            SchemaVersion = schema_version,
            SkinId = skinId,
            Layout = layout.Values,
        };

        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(document, json_options));
            File.Move(temporaryPath, path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Error(ex, $"Could not save the HUD layout for skin '{skinId}'.");
            deleteIfExists(temporaryPath);
        }
    }

    private string getProfilePath(string skinId)
    {
        string key = string.IsNullOrEmpty(skinId)
            ? "default"
            : Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(skinId.ToUpperInvariant()))).ToLowerInvariant();
        return Path.Combine(profileDirectory, key + ".json");
    }

    private static string normaliseSkinId(string skinId) =>
        skinId?.Trim() ?? string.Empty;

    private static bool idsEqual(string first, string second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    private static void deleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Error(ex, $"Could not remove HUD layout file '{path}'.");
        }
    }

    private void ensureInitialised()
    {
        if (gameplaySettings == null || profileDirectory == null)
            throw new InvalidOperationException("The skin HUD layout store is not initialised.");
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
                return;

            if (editSessionLayouts != null)
                cancelEditSessionLocked();
            else if (gameplaySettings != null)
                flushLocked();

            disposed = true;
            if (gameplaySettings != null)
            {
                foreach (GameplayHudLayoutSetting setting in gameplaySettings.HudLayoutSettings)
                    setting.Bindable.ValueChanged -= onLayoutChanged;
            }

            if (skinSettings != null)
                skinSettings.SelectedSkinId.ValueChanged -= onSkinChanged;
            if (skinLibrary != null)
                skinLibrary.SkinDeleted -= Forget;
        }

        saveTimer.Dispose();
    }

    private sealed record HudLayoutSnapshot(IReadOnlyDictionary<string, double> Values);

    private sealed class ProfileDocument
    {
        public int SchemaVersion { get; set; }
        public string SkinId { get; set; }
        public IReadOnlyDictionary<string, double> Layout { get; set; }
    }
}

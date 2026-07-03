namespace Yokko.Audio;

public sealed record AudioBackendCapabilities(
    AudioBackendKind Kind,
    bool SupportsExclusiveMode,
    bool SupportsLowLatencyBuffers,
    bool RequiresNativeDriver,
    string Description);

namespace Yokko.Audio;

public sealed record AudioDeviceInfo(
    string Id,
    string Name,
    AudioBackendKind Backend,
    int[] SupportedSampleRates,
    int[] SupportedBufferSizes,
    bool IsDefault);

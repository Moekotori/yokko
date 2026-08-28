namespace Yokko.Audio.Native;

internal sealed record NativeWasapiDevice(
    string Id,
    string Name,
    bool IsDefault);

internal static class NativeWasapiDevices
{
    private const uint initialCapacity = 16;

    internal static unsafe IReadOnlyList<NativeWasapiDevice> Enumerate()
    {
        NativeAudioLibrary.EnsureLoaded();

        uint capacity = initialCapacity;
        while (true)
        {
            var entries = new NativeWasapiDeviceInfo[capacity];
            uint activeCount;
            fixed (NativeWasapiDeviceInfo* entriesPointer = entries)
            {
                NativeAudioResult result =
                    NativeAudioInterop.EnumerateWasapiDevices(
                        entriesPointer,
                        capacity,
                        out uint writtenCount,
                        out activeCount);
                if (result != NativeAudioResult.Ok)
                    throw new NativeAudioException(
                        $"WASAPI device enumeration failed with {result}.");

                if (activeCount <= capacity)
                {
                    var devices = new List<NativeWasapiDevice>(
                        (int)writtenCount);
                    for (uint index = 0; index < writtenCount; index++)
                    {
                        NativeWasapiDeviceInfo* entry = entriesPointer + index;
                        devices.Add(new NativeWasapiDevice(
                            terminatedString(
                                entry->Id,
                                NativeWasapiDeviceInfo.IdCapacity),
                            terminatedString(
                                entry->Name,
                                NativeWasapiDeviceInfo.NameCapacity),
                            entry->IsDefault != 0));
                    }

                    return devices;
                }
            }

            capacity = activeCount;
        }
    }

    private static unsafe string terminatedString(
        char* characters,
        int capacity)
    {
        var span = new ReadOnlySpan<char>(characters, capacity);
        int terminator = span.IndexOf('\0');
        return new string(terminator < 0 ? span : span[..terminator]);
    }
}

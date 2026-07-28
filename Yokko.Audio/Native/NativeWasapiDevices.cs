namespace Yokko.Audio.Native;

internal sealed record NativeWasapiDevice(
    string Id,
    string Name,
    bool IsDefault);

internal static class NativeWasapiDevices
{
    internal static unsafe IReadOnlyList<NativeWasapiDevice> Enumerate()
    {
        NativeAudioLibrary.EnsureLoaded();
        NativeAudioResult countResult =
            NativeAudioInterop.GetWasapiDeviceCount(out uint deviceCount);
        if (countResult != NativeAudioResult.Ok)
            throw new NativeAudioException(
                $"WASAPI device enumeration failed with {countResult}.");

        var devices = new List<NativeWasapiDevice>((int)deviceCount);
        for (uint index = 0; index < deviceCount; index++)
        {
            const int capacity = 512;
            var id = new char[capacity];
            var name = new char[capacity];
            fixed (char* idPointer = id)
            fixed (char* namePointer = name)
            {
                NativeAudioResult infoResult =
                    NativeAudioInterop.GetWasapiDeviceInfo(
                        index,
                        idPointer,
                        capacity,
                        namePointer,
                        capacity,
                        out uint isDefault);
                if (infoResult != NativeAudioResult.Ok)
                    continue;

                devices.Add(new NativeWasapiDevice(
                    terminatedString(id),
                    terminatedString(name),
                    isDefault != 0));
            }
        }

        return devices;
    }

    private static string terminatedString(char[] characters)
    {
        int terminator = Array.IndexOf(characters, '\0');
        return new string(
            characters,
            0,
            terminator < 0 ? characters.Length : terminator);
    }
}

namespace Yokko.Audio.Native;

internal sealed record NativeAsioDevice(
    string Id,
    string Name,
    bool IsDefault);

internal static class NativeAsioDevices
{
    internal static bool IsBackendAvailable
    {
        get
        {
            if (!NativeAudioLibrary.IsAvailable)
                return false;

            try
            {
                return NativeAudioInterop.GetAsioDeviceCount(
                           out uint deviceCount)
                       == NativeAudioResult.Ok
                       && deviceCount > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    internal static unsafe IReadOnlyList<NativeAsioDevice> Enumerate()
    {
        NativeAudioLibrary.EnsureLoaded();
        NativeAudioResult countResult =
            NativeAudioInterop.GetAsioDeviceCount(out uint deviceCount);
        if (countResult == NativeAudioResult.BackendUnavailable)
            return [];
        if (countResult != NativeAudioResult.Ok)
        {
            throw new NativeAudioException(
                $"ASIO device enumeration failed with {countResult}.");
        }

        var devices = new List<NativeAsioDevice>((int)deviceCount);
        for (uint index = 0; index < deviceCount; index++)
        {
            const int capacity = 512;
            var id = new char[capacity];
            var name = new char[capacity];
            fixed (char* idPointer = id)
            fixed (char* namePointer = name)
            {
                NativeAudioResult infoResult =
                    NativeAudioInterop.GetAsioDeviceInfo(
                        index,
                        idPointer,
                        capacity,
                        namePointer,
                        capacity,
                        out uint isDefault);
                if (infoResult != NativeAudioResult.Ok)
                    continue;

                devices.Add(new NativeAsioDevice(
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

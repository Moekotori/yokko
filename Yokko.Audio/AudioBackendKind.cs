namespace Yokko.Audio;

public enum AudioBackendKind
{
    SharedWasapi,
    WasapiExclusive,
    Asio,
    Fallback,
}

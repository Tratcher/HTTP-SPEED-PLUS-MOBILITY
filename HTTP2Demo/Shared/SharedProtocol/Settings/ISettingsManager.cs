using SharedProtocol.Framing;

namespace SharedProtocol
{
    public interface ISettingsManager
    {
        void ProcessSettings(SettingsFrame settingsFrame);
    }
}

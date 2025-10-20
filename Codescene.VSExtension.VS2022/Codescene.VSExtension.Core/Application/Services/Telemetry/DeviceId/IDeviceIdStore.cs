namespace Codescene.VSExtension.Core.Application.Services.Util
{
    /// <summary>
    /// Provides access to a unique device identifier.
    /// </summary>
    /// <remarks>
    /// The device ID is used to help identify individual users when sending telemetry events.
    /// It should remain consistent across extension sessions on the same machine.
    /// </remarks>
    public interface IDeviceIdStore
    {
        string GetDeviceId();
    }
}
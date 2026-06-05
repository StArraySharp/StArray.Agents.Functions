using System.Runtime.InteropServices;
using StArray.Agents.Abstraction;
using StArray.Agents.Resources;

namespace StArray.Agents.Extensions;

public static class ToolsExtensions
{
    extension<T>(T tools) where T : ITools
    {
        public void CheckPlatform(OSPlatform targetPlatform)
        {
            if (!RuntimeInformation.IsOSPlatform(targetPlatform))
                throw new PlatformNotSupportedException(
                    string.Format(Localization.Platform_NotSupported, RuntimeInformation.OSDescription));
        }
    }
}
using System;

namespace DynLock.Core
{
    /// <summary>
    /// Provides the built-in encryption key used by BIMLab Studio and BIMLab Player.
    /// Environment variables or secrets.json can still override this for advanced deployments.
    /// </summary>
    public static class Secrets
    {
        internal const string BuiltInMasterKeyBase64 = "GLFkhHAY2jd2k4LCPOKY1jM3y7/BiDTSKQhtTIMh1e8=";

        public static string MasterKeyBase64 => DynLockRuntimeConfig.GetRequiredMasterKeyBase64();

        public static bool TryGetMasterKeyBytes(out byte[] key, out string error)
        {
            return DynLockRuntimeConfig.TryGetMasterKeyBytes(out key, out error);
        }

        public static byte[] GetMasterKeyBytes()
        {
            return DynLockRuntimeConfig.GetRequiredMasterKeyBytes();
        }
    }
}

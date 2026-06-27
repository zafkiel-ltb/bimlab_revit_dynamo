using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DynLock.Core
{
    public sealed class AuthServerSettings
    {
        public string AuthServerUrl { get; set; }
        public string SuperAdminEmail { get; set; }
    }

    internal sealed class SecretsFile
    {
        public string MasterKeyBase64 { get; set; }
    }

    internal sealed class AuthServerSettingsFile
    {
        public string AuthServerUrl { get; set; }
        public string SuperAdminEmail { get; set; }
    }

    public static class DynLockRuntimeConfig
    {
        public const string MasterKeyEnvVar = "DYNLOCK_MASTER_KEY_BASE64";
        public const string AuthServerUrlEnvVar = "DYNLOCK_AUTH_SERVER_URL";
        public const string AuthServerAdminEmailEnvVar = "DYNLOCK_AUTH_SERVER_ADMIN_EMAIL";

        public static string ConfigRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "BIMLab", "DynLock");

        public static string SecretsConfigPath => Path.Combine(ConfigRoot, "secrets.json");
        public static string AuthServerConfigPath => Path.Combine(ConfigRoot, "authserver.json");
        public static string AuthDatabasePath => Path.Combine(ConfigRoot, "auth.db");

        public static string GetRequiredMasterKeyBase64()
        {
            string value = ResolveMasterKeyBase64();
            if (!IsConfiguredValue(value))
            {
                value = Secrets.BuiltInMasterKeyBase64;
            }

            return value.Trim();
        }

        public static byte[] GetRequiredMasterKeyBytes()
        {
            if (!TryGetMasterKeyBytes(out byte[] key, out string error))
                throw new InvalidOperationException(error);

            return key;
        }

        public static bool TryGetMasterKeyBytes(out byte[] key, out string error)
        {
            key = null;
            error = null;

            string base64 = ResolveMasterKeyBase64();
            if (!IsConfiguredValue(base64))
            {
                base64 = Secrets.BuiltInMasterKeyBase64;
            }

            try
            {
                key = Convert.FromBase64String(base64.Trim());
                return true;
            }
            catch (FormatException ex)
            {
                error = "Invalid DynLock master key in environment or " + SecretsConfigPath + ": " + ex.Message;
                return false;
            }
        }

        public static AuthServerSettings GetRequiredAuthServerSettings()
        {
            if (!TryLoadAuthServerSettings(out AuthServerSettings settings, out string error))
                throw new InvalidOperationException(error);

            return settings;
        }

        public static AuthServerSettings GetRequiredAuthClientSettings()
        {
            if (!TryLoadAuthServerSettings(out AuthServerSettings settings, out string error, false))
                throw new InvalidOperationException(error);

            return settings;
        }

        public static bool TryLoadAuthServerSettings(out AuthServerSettings settings, out string error)
        {
            return TryLoadAuthServerSettings(out settings, out error, true);
        }

        public static bool TryLoadAuthClientSettings(out AuthServerSettings settings, out string error)
        {
            return TryLoadAuthServerSettings(out settings, out error, false);
        }

        private static bool TryLoadAuthServerSettings(
            out AuthServerSettings settings,
            out string error,
            bool requireSuperAdminEmail)
        {
            settings = LoadAuthServerSettingsCore();
            var missing = GetMissingAuthServerFields(settings, requireSuperAdminEmail).ToList();
            if (missing.Count > 0)
            {
                error =
                    "Missing DynLock auth server settings: " + string.Join(", ", missing) +
                    ". Set the corresponding environment variables or create " + AuthServerConfigPath + ".";
                return false;
            }

            error = null;
            return true;
        }

        public static IEnumerable<string> GetMissingAuthServerFields(AuthServerSettings settings)
        {
            return GetMissingAuthServerFields(settings, true);
        }

        public static IEnumerable<string> GetMissingAuthServerFields(
            AuthServerSettings settings,
            bool requireSuperAdminEmail)
        {
            if (settings == null)
            {
                yield return nameof(AuthServerSettings.AuthServerUrl);
                if (requireSuperAdminEmail)
                    yield return nameof(AuthServerSettings.SuperAdminEmail);
                yield break;
            }

            if (!IsConfiguredValue(settings.AuthServerUrl)) yield return nameof(AuthServerSettings.AuthServerUrl);
            if (requireSuperAdminEmail && !IsConfiguredValue(settings.SuperAdminEmail))
                yield return nameof(AuthServerSettings.SuperAdminEmail);
        }

        private static AuthServerSettings LoadAuthServerSettingsCore()
        {
            var file = LoadJson<AuthServerSettingsFile>(AuthServerConfigPath) ?? new AuthServerSettingsFile();

            return new AuthServerSettings
            {
                AuthServerUrl = NormalizeBaseUrl(FirstConfigured(
                    Environment.GetEnvironmentVariable(AuthServerUrlEnvVar),
                    file.AuthServerUrl)),
                SuperAdminEmail = FirstConfigured(
                    Environment.GetEnvironmentVariable(AuthServerAdminEmailEnvVar),
                    file.SuperAdminEmail),
            };
        }

        private static string NormalizeBaseUrl(string value)
        {
            if (!IsConfiguredValue(value))
                return value;

            return value.Trim().TrimEnd('/');
        }

        private static string ResolveMasterKeyBase64()
        {
            string env = Environment.GetEnvironmentVariable(MasterKeyEnvVar);
            if (IsConfiguredValue(env))
                return env.Trim();

            var file = LoadJson<SecretsFile>(SecretsConfigPath);
            return file?.MasterKeyBase64;
        }

        private static string FirstConfigured(params string[] values)
        {
            foreach (string value in values)
            {
                if (IsConfiguredValue(value))
                    return value.Trim();
            }

            return null;
        }

        private static bool IsConfiguredValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();
            if (value.StartsWith("<", StringComparison.Ordinal) ||
                value.StartsWith("YOUR", StringComparison.OrdinalIgnoreCase) ||
                value.IndexOf("CHANGE_ME", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return true;
        }

        private static T LoadJson<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}

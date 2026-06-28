using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AssetInventory.Core
{
    public static class EncryptionService
    {
        private const string KeyFile = "key.dat";
        
        // Custom entropy payload to bind key decryption strictly
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("KSAU_HS_SOVEREIGN_ENTROPY_0xAB42");

        public static string GetDatabasePassword()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    if (File.Exists(KeyFile))
                    {
                        byte[] encryptedData = File.ReadAllBytes(KeyFile);
                        byte[] decryptedData = ProtectedData.Unprotect(encryptedData, Entropy, DataProtectionScope.CurrentUser);
                        return Encoding.UTF8.GetString(decryptedData);
                    }
                    else
                    {
                        // Generate a secure random password cryptographically using RandomNumberGenerator
                        byte[] passwordBytes = new byte[32];
                        using (var rng = RandomNumberGenerator.Create())
                        {
                            rng.GetBytes(passwordBytes);
                        }
                        string password = Convert.ToBase64String(passwordBytes);
                        byte[] encryptedData = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.CurrentUser);
                        File.WriteAllBytes(KeyFile, encryptedData);
                        return password;
                    }
                }
            }
            catch (Exception ex)
            {
                if (Environment.GetEnvironmentVariable("ASSET_INVENTORY_DEV_MODE") == "1")
                {
                    return ""; // Safe fallback allowed in dev mode
                }
                throw new CryptographicException("DPAPI decryption failed. Database encryption cannot be initialized.", ex);
            }

            if (Environment.GetEnvironmentVariable("ASSET_INVENTORY_DEV_MODE") == "1")
            {
                return ""; // Safe cross-platform fallback for testing on Linux/Mac
            }

            throw new PlatformNotSupportedException("DPAPI requires Windows. Database encryption cannot be initialized on this OS.");
        }
    }
}

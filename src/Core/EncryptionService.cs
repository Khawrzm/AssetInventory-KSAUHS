using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

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
                    return GetWindowsPassword();
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

            return GetNonWindowsPassword();
        }

        [SupportedOSPlatform("windows")]
        private static string GetWindowsPassword()
        {
            byte[] masterSeed;
            if (File.Exists(KeyFile))
            {
                byte[] encryptedData = File.ReadAllBytes(KeyFile);
                masterSeed = ProtectedData.Unprotect(encryptedData, Entropy, DataProtectionScope.CurrentUser);
            }
            else
            {
                masterSeed = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(masterSeed);
                }
                byte[] encryptedData = ProtectedData.Protect(masterSeed, Entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(KeyFile, encryptedData);
            }

            try
            {
                return DeriveKey(masterSeed);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(masterSeed);
            }
        }

        private static string GetNonWindowsPassword()
        {
            // Non-Windows: Read from environment variable as SecureString if available
            string envKey = Environment.GetEnvironmentVariable("ASSET_INVENTORY_KEY") ?? "FallbackKsaUhSInventoryKey2026!";
            
            using (var secureStr = new SecureString())
            {
                foreach (char c in envKey)
                {
                    secureStr.AppendChar(c);
                }
                secureStr.MakeReadOnly();

                IntPtr valuePtr = IntPtr.Zero;
                byte[]? passwordBytes = null;
                try
                {
                    valuePtr = Marshal.SecureStringToGlobalAllocAnsi(secureStr);
                    int length = secureStr.Length;
                    passwordBytes = new byte[length];
                    for (int i = 0; i < length; i++)
                    {
                        passwordBytes[i] = Marshal.ReadByte(valuePtr, i);
                    }
                    
                    return DeriveKey(passwordBytes);
                }
                finally
                {
                    if (passwordBytes != null)
                    {
                        CryptographicOperations.ZeroMemory(passwordBytes);
                    }
                    if (valuePtr != IntPtr.Zero)
                    {
                        Marshal.ZeroFreeGlobalAllocAnsi(valuePtr);
                    }
                }
            }
        }

        private static string DeriveKey(byte[] seed)
        {
            // Derive a 32-byte (256-bit) SQLCipher encryption key from the seed using PBKDF2
            using (var pbkdf2 = new Rfc2898DeriveBytes(seed, Entropy, 100000, HashAlgorithmName.SHA256))
            {
                byte[] keyBytes = pbkdf2.GetBytes(32);
                try
                {
                    return Convert.ToHexString(keyBytes);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(keyBytes);
                }
            }
        }
    }
}

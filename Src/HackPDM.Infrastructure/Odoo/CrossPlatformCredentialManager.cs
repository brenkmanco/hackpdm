using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Meziantou.Framework.Win32;

namespace HackPDM.Infrastructure.Odoo;

public static class CrossPlatformCredentialManager
{
    private static readonly string FallbackDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".hackpdm");
    private static readonly string FallbackFile = Path.Combine(FallbackDir, "credentials.dat");

    public static (string? UserName, string? Password)? ReadCredential(string target)
        => ReadCredential(target, forceFallback: false);

    internal static (string? UserName, string? Password)? ReadCredential(string target, bool forceFallback, string? customFilePath = null)
    {
        if (!forceFallback && OperatingSystem.IsWindows())
        {
            try
            {
                var cm = CredentialManager.ReadCredential(target, CredentialType.Generic);
                if (cm != null)
                {
                    return (cm.UserName, cm.Password);
                }
            }
            catch
            {
                // Fall back if Windows Credential Manager fails
            }
        }

        return ReadFallback(target, customFilePath);
    }

    public static void WriteCredential(string target, string username, string password)
        => WriteCredential(target, username, password, forceFallback: false);

    internal static void WriteCredential(string target, string username, string password, bool forceFallback, string? customFilePath = null)
    {
        if (!forceFallback && OperatingSystem.IsWindows())
        {
            try
            {
                CredentialManager.WriteCredential(target, username, password, CredentialPersistence.LocalMachine);
                return;
            }
            catch
            {
                // Fall back if Windows Credential Manager fails
            }
        }

        WriteFallback(target, username, password, customFilePath);
    }

    internal static (string? UserName, string? Password)? ReadFallback(string target, string? customFilePath = null)
    {
        string filePath = customFilePath ?? FallbackFile;
        try
        {
            if (!File.Exists(filePath)) return null;
            byte[] encrypted = File.ReadAllBytes(filePath);
            string json = Decrypt(encrypted);
            var dict = JsonSerializer.Deserialize<Dictionary<string, CredentialEntry>>(json);
            if (dict != null && dict.TryGetValue(target, out var entry))
            {
                return (entry.UserName, entry.Password);
            }
        }
        catch
        {
            // Ignore decryption or read errors
        }
        return null;
    }

    internal static void WriteFallback(string target, string username, string password, string? customFilePath = null)
    {
        string filePath = customFilePath ?? FallbackFile;
        string dirPath = Path.GetDirectoryName(filePath) ?? FallbackDir;
        try
        {
            if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            Dictionary<string, CredentialEntry> dict = [];
            if (File.Exists(filePath))
            {
                try
                {
                    byte[] existingEncrypted = File.ReadAllBytes(filePath);
                    string existingJson = Decrypt(existingEncrypted);
                    dict = JsonSerializer.Deserialize<Dictionary<string, CredentialEntry>>(existingJson) ?? [];
                }
                catch { }
            }

            dict[target] = new CredentialEntry(username, password);
            string newJson = JsonSerializer.Serialize(dict);
            byte[] encrypted = Encrypt(newJson);
            File.WriteAllBytes(filePath, encrypted);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write fallback credential: {ex.Message}");
        }
    }

    internal static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HackPDM-Secure-Key-Entropy-v1");

    internal static byte[] Encrypt(string plainText)
    {
        byte[] data = Encoding.UTF8.GetBytes(plainText);
        using var aes = Aes.Create();
        using var sha = SHA256.Create();
        aes.Key = sha.ComputeHash(Entropy);
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        byte[] encryptedData = encryptor.TransformFinalBlock(data, 0, data.Length);
        byte[] result = new byte[aes.IV.Length + encryptedData.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encryptedData, 0, result, aes.IV.Length, encryptedData.Length);
        return result;
    }

    internal static string Decrypt(byte[] encrypted)
    {
        using var aes = Aes.Create();
        using var sha = SHA256.Create();
        aes.Key = sha.ComputeHash(Entropy);
        byte[] iv = new byte[aes.BlockSize / 8];
        Buffer.BlockCopy(encrypted, 0, iv, 0, iv.Length);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        int cipherLength = encrypted.Length - iv.Length;
        byte[] decrypted = decryptor.TransformFinalBlock(encrypted, iv.Length, cipherLength);
        return Encoding.UTF8.GetString(decrypted);
    }

    private record CredentialEntry(string UserName, string Password);
}

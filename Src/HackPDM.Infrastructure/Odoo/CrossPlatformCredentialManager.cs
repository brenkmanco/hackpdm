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
    {
        if (OperatingSystem.IsWindows())
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

        return ReadFallback(target);
    }

    public static void WriteCredential(string target, string username, string password)
    {
        if (OperatingSystem.IsWindows())
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

        WriteFallback(target, username, password);
    }

    private static (string? UserName, string? Password)? ReadFallback(string target)
    {
        try
        {
            if (!File.Exists(FallbackFile)) return null;
            byte[] encrypted = File.ReadAllBytes(FallbackFile);
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

    private static void WriteFallback(string target, string username, string password)
    {
        try
        {
            if (!Directory.Exists(FallbackDir))
            {
                Directory.CreateDirectory(FallbackDir);
            }

            Dictionary<string, CredentialEntry> dict = [];
            if (File.Exists(FallbackFile))
            {
                try
                {
                    byte[] existingEncrypted = File.ReadAllBytes(FallbackFile);
                    string existingJson = Decrypt(existingEncrypted);
                    dict = JsonSerializer.Deserialize<Dictionary<string, CredentialEntry>>(existingJson) ?? [];
                }
                catch { }
            }

            dict[target] = new CredentialEntry(username, password);
            string newJson = JsonSerializer.Serialize(dict);
            byte[] encrypted = Encrypt(newJson);
            File.WriteAllBytes(FallbackFile, encrypted);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write fallback credential: {ex.Message}");
        }
    }

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HackPDM-Secure-Key-Entropy-v1");

    private static byte[] Encrypt(string plainText)
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

    private static string Decrypt(byte[] encrypted)
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

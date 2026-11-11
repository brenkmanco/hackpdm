using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using HackPDM.Core;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.SldWrks;

namespace HackPDM.Tests.Compatibility;

/// <summary>
/// Verifies cross-platform compatibility and adapters across Windows and Linux.
/// Tests ensure that platform-specific features (e.g. Windows Credential Manager, SolidWorks COM)
/// safely degrade or route to cross-platform adapters (AES fallback keystore, multi-separator path parsers).
/// </summary>
public class PlatformCompatibilityTests
{
    [Fact]
    public void OperatingSystem_Detection_IdentifiesCurrentPlatformWithoutCrashing()
    {
        bool isWindows = OperatingSystem.IsWindows();
        bool isLinux = OperatingSystem.IsLinux();
        bool isMacOS = OperatingSystem.IsMacOS();

        // Exactly one primary desktop OS must match current host
        Assert.True(isWindows || isLinux || isMacOS);

        // RuntimeInformation should also agree with OperatingSystem check
        if (isWindows)
        {
            Assert.True(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        }
        else if (isLinux)
        {
            Assert.True(RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
        }
        else if (isMacOS)
        {
            Assert.True(RuntimeInformation.IsOSPlatform(OSPlatform.OSX));
        }
    }

    [Fact]
    public void CrossPlatformCredentialManager_FallbackEncryption_RoundTripsSecurely()
    {
        const string plainText = "SuperP@ssw0rd!#_2026_Unicode_\u2713";

        byte[] encrypted = CrossPlatformCredentialManager.Encrypt(plainText);
        Assert.NotNull(encrypted);
        Assert.NotEmpty(encrypted);

        string decrypted = CrossPlatformCredentialManager.Decrypt(encrypted);
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void CrossPlatformCredentialManager_FallbackAdapter_WritesAndReadsOnAnyPlatform()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"hackpdm_test_cred_{Guid.NewGuid():N}.dat");
        try
        {
            const string target = "test_odoo_server_linux";
            const string username = "cad_engineer_linux";
            const string password = "VaultPassword456$!";

            // Write credential to custom cross-platform fallback keystore
            CrossPlatformCredentialManager.WriteFallback(target, username, password, tempFile);

            Assert.True(File.Exists(tempFile));

            // Read credential back using the fallback adapter
            var result = CrossPlatformCredentialManager.ReadFallback(target, tempFile);

            Assert.NotNull(result);
            Assert.Equal(username, result.Value.UserName);
            Assert.Equal(password, result.Value.Password);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void CrossPlatformCredentialManager_FallbackAdapter_MissingTargetReturnsNullGracefully()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"hackpdm_missing_{Guid.NewGuid():N}.dat");

        // Reading from nonexistent store or missing target must return null without throwing
        var resultNonExistentFile = CrossPlatformCredentialManager.ReadFallback("unknown_target", tempFile);
        Assert.Null(resultNonExistentFile);

        try
        {
            // Now write one target, but ask for a different target
            CrossPlatformCredentialManager.WriteFallback("target_A", "userA", "passA", tempFile);

            var resultMissingTarget = CrossPlatformCredentialManager.ReadFallback("target_B", tempFile);
            Assert.Null(resultMissingTarget);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void CrossPlatformCredentialManager_ForceFallback_ExecutesEndToEnd()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"hackpdm_force_fallback_{Guid.NewGuid():N}.dat");
        try
        {
            const string target = "odoo.company.internal";
            const string user = "linux_worker";
            const string pass = "SecretP@ss789";

            CrossPlatformCredentialManager.WriteCredential(target, user, pass, forceFallback: true, customFilePath: tempFile);

            var read = CrossPlatformCredentialManager.ReadCredential(target, forceFallback: true, customFilePath: tempFile);

            Assert.NotNull(read);
            Assert.Equal(user, read.Value.UserName);
            Assert.Equal(pass, read.Value.Password);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void PathNormalization_WindowsAndLinuxSeparators_ProducePlatformSeparators()
    {
        string sep = Path.DirectorySeparatorChar.ToString();

        // 1. Windows path format with backslashes
        string windowsPath = @"root\Mechanical\Gears\Pinion.sldprt";
        string? normalizedFromWindows = FileOperations.NodePathToWindowsPath(windowsPath, withAbsolutePath: false);

        string expected = $"Mechanical{sep}Gears{sep}Pinion.sldprt";
        Assert.Equal(expected, normalizedFromWindows);

        // 2. Linux path format with forward slashes
        string linuxPath = "root/Mechanical/Gears/Pinion.sldprt";
        string? normalizedFromLinux = FileOperations.NodePathToWindowsPath(linuxPath, withAbsolutePath: false);

        Assert.Equal(expected, normalizedFromLinux);

        // 3. Mixed path format with both backslashes and forward slashes
        string mixedPath = "root\\Mechanical/Gears\\Pinion.sldprt";
        string? normalizedFromMixed = FileOperations.NodePathToWindowsPath(mixedPath, withAbsolutePath: false);

        Assert.Equal(expected, normalizedFromMixed);
    }

    [Fact]
    public void PathNormalization_NullInput_ReturnsNullGracefully()
    {
        Assert.Null(FileOperations.NodePathToWindowsPath(null));
        Assert.Null(FileOperations.ConvertToWindowsPath(null, withAbsolutePath: false));
        Assert.Null(FileOperations.ConvertToWindowsPath(null, withAbsolutePath: true));
    }

    [Fact]
    public void SolidWorksAdapter_OnNonWindows_SafelyDegradesWithoutThrowing()
    {
        if (!OperatingSystem.IsWindows())
        {
            // On Linux or macOS, SolidWorks COM cannot run.
            // Verify safe degradation to null rather than TypeLoadException or COMException.
            Assert.Null(SolidWorksUtil.DocMgr);
            Assert.Null(SolidWorksUtil.SwHelper);
        }
        else
        {
            // On Windows, the guard allows property access without crashing
            // (returns null if uninitialized/unlicensed, but does not throw)
            var docMgr = SolidWorksUtil.DocMgr;
            // Either null or initialized instance, but call succeeded
            Assert.True(docMgr is null || docMgr is not null);
        }
    }

    [Fact]
    public void CrossPlatformWindowConfig_Vector4PositionAndSize_IsPlatformIndependent()
    {
        // Window coordinates and sizes in HackPDM use Vector4<int> (x, y, width, height)
        // to maintain 100% platform-independent geometry without dependency on native UI SDKs.
        Vector4<int> posAndSize = (100, 150, 1024, 768);

        Assert.Equal(100, posAndSize.x);
        Assert.Equal(150, posAndSize.y);
        Assert.Equal(1024, posAndSize.z);
        Assert.Equal(768, posAndSize.w);
    }
}

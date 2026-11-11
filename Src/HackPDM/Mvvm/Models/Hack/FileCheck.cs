using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HackPDM.Core.Hack;

namespace HackPDM.Mvvm.Models.Hack;

public struct FileCheck
{
    public readonly WatcherChangeTypes ChangeType { get; }
    public readonly string Name { get; }
    public readonly string CurrentPath { get; }
    public readonly string? OldPath { get; }

    public HackFile? Hack
    {
        get
        {
            if (CurrentPath is not null && ChangeType != (WatcherChangeTypes.Deleted | WatcherChangeTypes.All))
            {
                field ??= new HackFile(CurrentPath);
            }
            return field;
        }
    }

    public FileCheck(string name, string path, string? oldPath = null, WatcherChangeTypes type = WatcherChangeTypes.All)
    {
        Name = name;
        ChangeType = type;
        CurrentPath = path;
        OldPath = oldPath;
    }

    public FileCheck(EventArgs e)
    {
        switch (e)
        {
            case RenamedEventArgs renamedEvent:
            {
                Name = renamedEvent.Name;
                ChangeType = WatcherChangeTypes.Renamed;
                CurrentPath = renamedEvent.FullPath;
                OldPath = renamedEvent.OldFullPath;
                break;
            }
            case FileSystemEventArgs fileEvent:
            {
                Name = fileEvent.Name;
                ChangeType = fileEvent.ChangeType;
                CurrentPath = fileEvent.FullPath;
                OldPath = null;
                break;
            }
            default:
            {
                Name = string.Empty;
                ChangeType = WatcherChangeTypes.All;
                CurrentPath = string.Empty;
                OldPath = string.Empty;
                break;
            }
        }
    }
        
    public override bool Equals(object? obj)
    {
        if (obj is FileCheck other)
        {
            return Name == other.Name && CurrentPath == other.CurrentPath;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, CurrentPath);
    }

    public static bool operator ==(FileCheck left, FileCheck right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(FileCheck left, FileCheck right)
    {
        return !(left == right);
    }

    public static string FindCommonPath(IEnumerable<FileCheck>? fileChecks)
    {
        if (fileChecks == null || !fileChecks.Any())
            return string.Empty;

        var paths = fileChecks.Select(fc => fc.CurrentPath).Where(p => !string.IsNullOrEmpty(p)).ToList();
            
        if (!paths.Any())
            return string.Empty;

        var commonPath = paths[0];
        foreach (var path in paths.Skip(1))
        {
            while (!path.StartsWith(commonPath, StringComparison.OrdinalIgnoreCase))
            {
                commonPath = Path.GetDirectoryName(commonPath);
                if (commonPath == null)
                    return string.Empty;
            }
        }
        return commonPath;
    }
}

using System;

namespace HackPDM.Shared.GlobalData;

public enum FileStatus
{
    Lo, // Local Only
    Ro, // Remote Only
    Ok, // Latest
    Nv, // Newer Remote Version
    Lm, // Local Modification
    Dt, // Deleted
    Ds, // Destroyed
    If, // Ignore Filter
    Ft, // Filter Type
    Cm, // Checked Out To Me
    Co, // Checked Out To Other
    NoVersions,
    Unknown,
}

public enum EntryReturnType
{
	Created,
	GotExisting,
	Failed,
	InvalidType,
    Staged,
}

public enum HackVersionType
{
    Latest,
    Older,
    Unique,
    New,
}

public enum EntryReprType
{
	Remote,
	Local,
	Both,
}

public enum ChecksumType
{
    Sha1,
    Md5,
    Sha256,
    Sha512,
}

public enum ExtType
{
	Svg,
	Png,
	Other,
}

[Flags]
public enum HackTestDepth : ushort
{
	NoTest              = 0,
	InPWATest           = 1,
	FileExistsTest      = 2,
	FileIsCorruptTest   = 4,
}

[Flags]
public enum HackResult : ushort
{
	NotTested           = 0,
	Clean               = 1,
	OutOfPWA            = 2,
	PropogatedFailure   = 4,
	CorruptDepFile      = 8,
	MissingFile         = 16,
	MissingDepFile      = 32,
}

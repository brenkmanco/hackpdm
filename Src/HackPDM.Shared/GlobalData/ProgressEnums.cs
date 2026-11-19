using System;

namespace HackPDM.Shared.GlobalData;

public enum StatusMessage
{
    PROCESSING,
    SKIP,
    FOUND,
    INFO,
    SUCCESS,
    WARNING,
    ERROR,
    OTHER,
}

/// <summary>Logical area for status entries shown in the Profile Manager.</summary>
public static class ProfileStatusCategory
{
    public const string Login = "Login";
    public const string Connection = "Connection";
    public const string Configuration = "Configuration";
    public const string Settings = "Settings";
}

public enum TaskProgress
{
    InProgress,
    Finished,
    Cancelling,
}



public enum ChangeType
{
	Added,
	Removed,
	Updated,
	Selected,
	Clicked,
	Rendering,
	Focused,
	Hovered,
}


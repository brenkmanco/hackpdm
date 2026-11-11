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


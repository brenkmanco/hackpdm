namespace HackPDM.Domain.Data;

public class StatusData
{
    public static StatusData StaticData = new();
    public static long SessionDownloadBytes;
    public int TotalProcess;
    public int SkipCounter;
    public int ProcessCounter;
    public int MaxCount;
    public long DownloadBytes;
}
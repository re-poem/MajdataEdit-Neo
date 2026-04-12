namespace MajdataEdit_Neo.Types.MajWs;

internal readonly struct MajWsRequestPlay
{
    public PlaybackMode Mode { get; init; }
    public double StartAt { get; init; }
    public double Offset { get; init; }
    public float Speed { get; init; }
    public string SimaiFumen { get; init; }
    public string Title { get; init; }
    public string Artist { get; init; }
    public int Difficulty { get; init; }
    public string? MaidataPath { get; init; }
}
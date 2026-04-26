using MajSimai;
using System.Collections.Generic;

namespace MajdataEdit_Neo.Types.MajWs;

internal readonly struct MajWsRequestPlay
{
    public PlaybackMode Mode { get; init; }
    public double StartAt { get; init; }
    public float Speed { get; init; }
    public string Title { get; init; }
    public string Artist { get; init; }
    public float Offset { get; init; }
    public string Designer { get; init; }
    public string Level { get; init; }
    public string Fumen { get; init; }
    public IList<SimaiCommand> Commands { get; init; }
    public int Difficulty { get; init; }
    public string? MaidataPath { get; init; }
}
namespace MajdataEdit_Neo.Types.MajWs;

internal struct ViewSummary
{
    public ViewStatus State { get; init; }
    public string ErrMsg { get; init; }
    public float Timeline { get; init; }
}

using MajdataEdit_Neo.Types.MajSetting;

namespace MajdataEdit_Neo.Types.MajWs;

internal readonly struct MajWsRequestSetting
{
    public MajViewSetting ViewSetting { get; init; }
    public MajVolumeSetting VolumeSetting { get; init; }
}
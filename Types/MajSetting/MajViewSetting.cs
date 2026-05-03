namespace MajdataEdit_Neo.Types.MajSetting;

public class MajViewSetting
{
    [SettingControl(SettingControlType.Numeric, Max = 20, Min = -20, Step = 0.25)]
    public float TapSpeed { get; set; } = 7.5f;

    [SettingControl(SettingControlType.Numeric, Max = 20, Min = -20, Step = 0.25)]
    public float TouchSpeed { get; set; } = 7.5f;

    [SettingControl(SettingControlType.Toggle)]
    public bool SmoothSlideAnime { get; set; } = true;

    [SettingControl(SettingControlType.Numeric, Max = 1, Min = 0, Step = 0.1)]
    public float BackgroundDim { get; set; } = 0.7f;

    [SettingControl(SettingControlType.Selection,
        Values = new object[] { BgInfoDisplay.None, 
                                BgInfoDisplay.Combo, 
                                BgInfoDisplay.Achievement, 
                                BgInfoDisplay.Achievement_100, 
                                BgInfoDisplay.Achievement_101, 
                                BgInfoDisplay.AchievementClassical, 
                                BgInfoDisplay.AchievementClassical_100, 
                                BgInfoDisplay.DXScore,
                                BgInfoDisplay.S_Border,
                                BgInfoDisplay.SS_Border,
                                BgInfoDisplay.SSS_Border},
        Labels = new[] {        "None", 
                                "Combo",
                                "Achievement + (Deluxe)",
                                "Achievement - (Deluxe, 100)",
                                "Achievement - (Deluxe, 101)",
                                "Achievement + (Classic)",
                                "Achievement - (Classic, 100)",
                                "Deluxe Score",
                                "S Border",
                                "SS Border",
                                "SSS Border"})]
    public BgInfoDisplay ComboStatusType { get; set; } = BgInfoDisplay.Combo;


    [SettingControl(SettingControlType.Selection,
        Values = new object[] { JudgeDisplayMode.None, 
                                JudgeDisplayMode.FastLate, 
                                JudgeDisplayMode.Level, 
                                JudgeDisplayMode.Both },
        Labels = new[] {        "None", 
                                "Fast/Late Only", 
                                "Level Only", 
                                "Both" })]
    public JudgeDisplayMode JudgeDisplayMode { get; set; } = JudgeDisplayMode.Both;


    [SettingControl(SettingControlType.Selection,
        Values = new object[] { AutoPlayMode.Enable, 
                                AutoPlayMode.DJAuto, 
                                AutoPlayMode.Random, 
                                AutoPlayMode.Disable },
        Labels = new[] {        "Enable", 
                                "DJAuto", 
                                "Random", 
                                "Disable" })]
    public AutoPlayMode AutoMode { get; set; } = AutoPlayMode.Enable;


    [SettingControl(SettingControlType.Numeric, Max = 1000, Min = 0, Step = 30)]
    public int OutputFps { get; set; } = 60;

    [SettingUnbrowsable]
    public UIType UIType { get; set; } = UIType.Legacy;
}

public enum BgInfoDisplay
{
    None,
    Combo,
    Achievement_101,
    Achievement_100,
    Achievement,
    AchievementClassical,
    AchievementClassical_100,
    DXScore,
    S_Border,
    SS_Border,
    SSS_Border,
}

public enum UIType
{
    Legacy,
    TrgUI
}
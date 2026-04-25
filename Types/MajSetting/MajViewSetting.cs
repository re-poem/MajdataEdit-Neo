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
        Values = new object[] { EditorComboIndicator.None, 
                                EditorComboIndicator.Combo, 
                                EditorComboIndicator.ScoreClassic, 
                                EditorComboIndicator.AchievementClassic, 
                                EditorComboIndicator.AchievementDownClassic, 
                                EditorComboIndicator.AchievementDeluxe, 
                                EditorComboIndicator.AchievementDownDeluxe, 
                                EditorComboIndicator.ScoreDeluxe,
                                EditorComboIndicator.CScoreDedeluxe,
                                EditorComboIndicator.CScoreDownDedeluxe},
        Labels = new[] {        "None", 
                                "Combo",
                                "Score (Classic)",
                                "Achievement + (Classic)",
                                "Achievement - (Classic)", 
                                "Achievement + (Deluxe)", 
                                "Achievement - (Deluxe)", 
                                "Deluxe Score",
                                "Normalized Score +",
                                "Normalized Score -"})]
    public EditorComboIndicator ComboStatusType { get; set; } = EditorComboIndicator.Combo;


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
                                AutoPlayMode.DjAuto, 
                                AutoPlayMode.Random, 
                                AutoPlayMode.Disable },
        Labels = new[] {        "Enable", 
                                "DJAuto", 
                                "Random", 
                                "Disable" })]
    public AutoPlayMode AutoMode { get; set; } = AutoPlayMode.Enable;


    [SettingControl(SettingControlType.Numeric, Max = 1000, Min = 0, Step = 30)]
    public int OutputFps { get; set; } = 60;
}

public enum EditorComboIndicator
{
    None,

    // List of viable indicators that won't be a static content.
    // ScoreBorder, AchievementMaxDown, ScoreDownDeluxe are static.
    Combo,
    ScoreClassic,
    AchievementClassic,
    AchievementDownClassic,
    AchievementDeluxe = 11,
    AchievementDownDeluxe,
    ScoreDeluxe,

    // Please prefix custom indicator with C
    CScoreDedeluxe = 101,
    CScoreDownDedeluxe,
    MAX
}
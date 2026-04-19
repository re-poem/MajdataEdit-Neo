namespace MajdataEdit_Neo.Types.MajSetting;

public class MajViewSetting
{
    public float TapSpeed { get; set; } = 7.5f;
    public float TouchSpeed { get; set; } = 7.5f;
    public bool SmoothSlideAnime { get; set; } = true;
    public float BackgroundDim { get; set; } = 0.7f;
    public EditorComboIndicator ComboStatusType { get; set; } = EditorComboIndicator.Combo;
    public JudgeDisplayMode JudgeDisplayMode { get; set; } = JudgeDisplayMode.Both;
    public AutoPlayMode AutoMode { get; set; } = AutoPlayMode.Enable;
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
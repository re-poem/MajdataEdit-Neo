using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MajdataEdit_Neo.Types
{
    public class MajViewSetting
    {
        public float TapSpeed { get; set; }
        public float TouchSpeed { get; set; }
        public bool SmoothSlideAnime { get; set; }
        public float BackgroundDim { get; set; }
        public EditorComboIndicator ComboStatusType { get; set; }
        public JudgeDisplayMode JudgeDisplayMode { get; set; }
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

    public enum JudgeDisplayMode
    {
        None,
        FastLate,
        Level,
        Both
    }
}

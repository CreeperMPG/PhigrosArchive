using PhigrosArchive.Abstractions;
using PhigrosArchive.Utils;
using System.Text;

namespace PhigrosArchive.Save.Data
{
    public enum PhiLevelType
    {
        F,
        C,
        B,
        A,
        S,
        V,
        FC,
        Phi,
        六百六十六这个入开桂了
    }

    public class PhiLevelRecord
    {
        public int Score { get; set; }
        public float Acc { get; set; }
        public bool Fc { get; set; }
        public float? Difficulty { get; private set; }

        public PhiLevelRecord() { Score = 0; Acc = 0f; Fc = false; Difficulty = null; }
        public PhiLevelRecord(float? difficulty) { Score = 0; Acc = 0f; Fc = false; Difficulty = difficulty; }
        public PhiLevelRecord(int score, float acc, bool fc = false, float? difficulty = null)
        {
            Score = score; Acc = acc; Fc = fc; Difficulty = difficulty;
        }

        public PhiLevelType Rank
        {
            get
            {
                if (Score > 1000000) return PhiLevelType.六百六十六这个入开桂了;
                if (Score == 1000000) return PhiLevelType.Phi;
                if (Fc) return PhiLevelType.FC;
                if (Score >= 960000) return PhiLevelType.V;
                if (Score >= 920000) return PhiLevelType.S;
                if (Score >= 880000) return PhiLevelType.A;
                if (Score >= 820000) return PhiLevelType.B;
                if (Score >= 700000) return PhiLevelType.C;
                return PhiLevelType.F;
            }
        }

        public void SetDifficulty(float? difficulty) { Difficulty = difficulty; }

        public float? RankingScore
        {
            get => Difficulty != null ? CalculateRankingScoreWithoutDifficulty() * Difficulty : null;
        }

        private float CalculateRankingScoreWithoutDifficulty()
        {
            if (Acc < 70) return 0;
            return (float)Math.Pow((Acc - 55) / 45, 2);
        }

        public override string ToString()
        {
            if (Difficulty != null)
                return $"{Score} {Acc:F2} {Rank} - {Difficulty:F1}, RKS {RankingScore:F2}";
            return $"{Score} {Acc:F2} {Rank}";
        }
    }
}

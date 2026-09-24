using System.Text;
using PhigrosArchive.Save.Data;
using PhigrosArchive.Utils;

namespace PhigrosArchive.Save
{
    public class Achievement
    {
        public ushort Cleared { get; set; }
        public ushort FullCombo { get; set; }
        public ushort Phi { get; set; }

        public Achievement() { }
        public Achievement(ushort cleared, ushort fullCombo, ushort phi)
        {
            Cleared = cleared; FullCombo = fullCombo; Phi = phi;
        }

        public override string ToString() => $"{Cleared}/{FullCombo}/{Phi}";
    }

    public class SaveSummary
    {
        public byte SaveVersion { get; set; }
        public ushort Challenge { get; set; }
        public float RankingScore { get; set; }
        public int GameVersion { get; set; }
        public string Avatar { get; set; } = string.Empty;
        public PhiDifficultyInfo<Achievement> Achievements { get; set; } = default!;

        public SaveSummary() { }

        public SaveSummary(byte saveVersion, ushort challenge, float rankingScore,
                           byte gameVersion, string avatar, PhiDifficultyInfo<Achievement> achievements)
        {
            SaveVersion = saveVersion; Challenge = challenge; RankingScore = rankingScore;
            GameVersion = gameVersion; Avatar = avatar; Achievements = achievements;
        }

        /// <summary>从 Base64 摘要二进制反序列化</summary>
        public SaveSummary(string base64Summary)
        {
            try
            {
                byte[] data = Convert.FromBase64String(base64Summary);
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);

                SaveVersion = reader.ReadByte();
                Challenge = reader.ReadUInt16();
                RankingScore = reader.ReadSingle();
                GameVersion = BitUtils.ReadProtobufVarInt(reader);
                Avatar = BitUtils.ReadString(reader);

                var achievements = new Achievement[4];
                for (int i = 0; i < 4; i++)
                {
                    achievements[i] = new Achievement(
                        reader.ReadUInt16(),
                        reader.ReadUInt16(),
                        reader.ReadUInt16()
                    );
                }
                Achievements = new PhiDifficultyInfo<Achievement>(achievements);
            }
            catch { }
        }

        /// <summary>序列化为 Base64 字符串</summary>
        public string ToBase64String()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(SaveVersion);
            writer.Write(Challenge);
            writer.Write(RankingScore);
            //writer.Write(GameVersion);
            //writer.Write(MythByte);
            writer.Write(BitUtils.WriteProtobufVarInt(GameVersion));

            byte[] avatarBytes = Encoding.UTF8.GetBytes(Avatar);
            writer.Write((byte)avatarBytes.Length);
            writer.Write(avatarBytes);

            try
            {
                if (Achievements != null)
                {
                    foreach (var a in Achievements.GetArray())
                    {
                        writer.Write(a.Cleared);
                        writer.Write(a.FullCombo);
                        writer.Write(a.Phi);
                    }
                }
                else
                {
                    writer.Write(114);
                    writer.Write(514);
                    writer.Write(1919);
                }
            }
            catch { }
            return Convert.ToBase64String(ms.ToArray());
        }

        public override string ToString() => ToBase64String();
    }
}

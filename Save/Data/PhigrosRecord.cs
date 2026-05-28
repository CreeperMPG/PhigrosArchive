using PhigrosArchive.Abstractions;
using PhigrosArchive.Utils;

namespace PhigrosArchive.Save.Data
{
    public class PhigrosRecord
    {
        private readonly IDifficultyProvider? _provider;

        public Dictionary<string, PhiDifficultyInfo<PhiLevelRecord?>> Records { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);

        public int SongsNum => Records.Count;

        public float? RankingScore
        {
            get
            {
                if (_provider == null || !_provider.IsLoaded)
                    return null;

                return Records
                    .SelectMany(kvp =>
                        kvp.Value.GetDictionary()
                        .Where(songkvp => songkvp.Value != null)
                        .Where(songkvp => songkvp.Value?.Score == 1000000 && songkvp.Value?.Acc == 100)
                    )
                    .OrderByDescending(kvp => kvp.Value?.Difficulty ?? 0)
                    .Select(kvp => kvp.Value?.RankingScore)
                    .Where(val => val != null)
                    .Take(3)
                    .Concat(
                        Records
                            .SelectMany(kvp =>
                                kvp.Value.GetDictionary()
                                .Where(songkvp => songkvp.Value != null)
                                .Where(songkvp => songkvp.Value?.RankingScore != null)
                                .Select(kvp => kvp.Value?.RankingScore)
                                .Where(val => val != null)
                             )
                            .OrderByDescending(val => val)
                            .Take(27)
                    )
                    .Sum() / 30.0f;
            }
        }

        public PhigrosRecord() { }

        public PhigrosRecord(byte[] data, IDifficultyProvider? difficultyProvider = null)
        {
            _provider = difficultyProvider;
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            Records = new(StringComparer.OrdinalIgnoreCase);
            int songsNum = BitUtils.ReadProtobufVarInt(reader);

            while (ms.Length - ms.Position > 0)
            {
                string songID = BitUtils.ReadString(reader);
                reader.ReadByte(); // non-null count byte (ignored on read)
                byte availableDifficulties = reader.ReadByte();
                byte fc = reader.ReadByte();

                var levels = new PhiLevelRecord?[5] { null, null, null, null, null, };

                // 从 provider 获取定数（如果已加载）
                float?[] diffFromProvider = new float?[5];
                if (_provider != null)
                {
                    for (int i = 0; i < 5; i++)
                        diffFromProvider[i] = _provider.GetDifficulty(songID, i);
                }

                for (int i = 0; i < 5; i++)
                {
                    if (BitUtils.GetBit(availableDifficulties, i))
                    {
                        levels[i] = new PhiLevelRecord(diffFromProvider[i])
                        {
                            Score = (int)reader.ReadUInt32(),
                            Acc = reader.ReadSingle(),
                            Fc = BitUtils.GetBit(fc, i),
                        };
                    }
                }
                Records[songID] = new PhiDifficultyInfo<PhiLevelRecord?>(levels);
            }
        }

        public void RefreshDifficulties(IDifficultyProvider provider)
        {
            foreach (var (songId, diffInfo) in Records)
            {
                for (int i = 0; i < 5; i++)
                {
                    float? diff = provider.GetDifficulty(songId, i);
                    PhiLevelRecord? record = diffInfo.GetByIndex(i);
                    if (record != null && diff != null)
                        record.SetDifficulty(diff);
                }
            }
        }

        public byte[] ToByteArray()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(BitUtils.WriteProtobufVarInt(SongsNum));

            foreach (var (key, diffInfo) in Records)
            {
                writer.Write(BitUtils.WriteString(key));

                // 固定5位置: 0=EZ 1=HD 2=IN 3=AT 4=Legacy
                int nonNullCount = 0;
                byte avail = 0;
                byte fc = 0;
                for (int i = 0; i < 5; i++)
                {
                    var r = diffInfo.GetByIndex(i);
                    if (r != null)
                    {
                        nonNullCount++;
                        avail |= (byte)(1 << i);
                        if (r.Fc) fc |= (byte)(1 << i);
                    }
                }
                writer.Write(Convert.ToByte(nonNullCount * 8 + 2));
                writer.Write(avail);
                writer.Write(fc);

                // 固定顺序写出数据
                for (int i = 0; i < 5; i++)
                {
                    var r = diffInfo.GetByIndex(i);
                    if (r != null)
                    {
                        writer.Write((uint)r.Score);
                        writer.Write(r.Acc);
                    }
                }
            }
            return ms.ToArray();
        }
    }
}

using PhigrosArchive.Utils;

namespace PhigrosArchive.Save.Data
{
    public class PhigrosProgress
    {
        public bool IsFirstRun { get; set; }
        public bool LegacyChapterFinished { get; set; }
        public bool AlreadyShowCollectionTip { get; set; }
        public bool AlreadyShowAutoUnlockINTip { get; set; }
        public string Completed { get; set; } = string.Empty;
        public int SongUpdateInfo { get; set; }
        public short ChallengeModeRank { get; set; }
        public PhiMoney Money { get; set; } = new();
        public byte UnlockFlagOfSpasmodic { get; set; }
        public byte UnlockFlagOfIgallta { get; set; }
        public byte UnlockFlagOfRrharil { get; set; }
        public byte FlagOfSongRecordKey { get; set; }
        public byte RandomVersionUnlocked { get; set; }
        public bool Chapter8UnlockBegin { get; set; }
        public bool Chapter8UnlockSecondPhase { get; set; }
        public bool Chapter8Passed { get; set; }
        public byte Chapter8SongUnlocked { get; set; }
        public byte[] OverflowData { get; set; } = Array.Empty<byte>();

        public PhigrosProgress() { }

        public PhigrosProgress(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            byte flags = reader.ReadByte();
            IsFirstRun = BitUtils.GetBit(flags, 0);
            LegacyChapterFinished = BitUtils.GetBit(flags, 1);
            AlreadyShowCollectionTip = BitUtils.GetBit(flags, 2);
            AlreadyShowAutoUnlockINTip = BitUtils.GetBit(flags, 3);
            Completed = BitUtils.ReadString(reader);
            SongUpdateInfo = BitUtils.ReadProtobufVarInt(reader);
            ChallengeModeRank = reader.ReadInt16();
            Money = new PhiMoney(new[]
            {
                BitUtils.ReadProtobufVarInt(reader),
                BitUtils.ReadProtobufVarInt(reader),
                BitUtils.ReadProtobufVarInt(reader),
                BitUtils.ReadProtobufVarInt(reader),
                BitUtils.ReadProtobufVarInt(reader),
            });
            UnlockFlagOfSpasmodic = reader.ReadByte();
            UnlockFlagOfIgallta = reader.ReadByte();
            UnlockFlagOfRrharil = reader.ReadByte();
            FlagOfSongRecordKey = reader.ReadByte();
            RandomVersionUnlocked = reader.ReadByte();

            byte chapter8Info = reader.ReadByte();
            Chapter8UnlockBegin = BitUtils.GetBit(chapter8Info, 0);
            Chapter8UnlockSecondPhase = BitUtils.GetBit(chapter8Info, 1);
            Chapter8Passed = BitUtils.GetBit(chapter8Info, 2);
            Chapter8SongUnlocked = reader.ReadByte();
            OverflowData = reader.ReadBytes((int)(ms.Length - ms.Position));
        }

        public byte[] ToByteArray()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            byte flags = 0;
            if (IsFirstRun) flags |= 1 << 0;
            if (LegacyChapterFinished) flags |= 1 << 1;
            if (AlreadyShowCollectionTip) flags |= 1 << 2;
            if (AlreadyShowAutoUnlockINTip) flags |= 1 << 3;
            writer.Write(flags);
            writer.Write(BitUtils.WriteString(Completed));
            writer.Write(BitUtils.WriteProtobufVarInt(SongUpdateInfo));
            writer.Write(ChallengeModeRank);

            foreach (int m in Money.GetMoneyArray())
                writer.Write(BitUtils.WriteProtobufVarInt(m));

            writer.Write(UnlockFlagOfSpasmodic);
            writer.Write(UnlockFlagOfIgallta);
            writer.Write(UnlockFlagOfRrharil);
            writer.Write(FlagOfSongRecordKey);
            writer.Write(RandomVersionUnlocked);

            byte chapter8Info = 0;
            if (Chapter8UnlockBegin) chapter8Info |= 1 << 0;
            if (Chapter8UnlockSecondPhase) chapter8Info |= 1 << 1;
            if (Chapter8Passed) chapter8Info |= 1 << 2;
            writer.Write(chapter8Info);
            writer.Write(Chapter8SongUnlocked);
            writer.Write(OverflowData);
            return ms.ToArray();
        }
    }
}

using PhigrosArchive.Utils;

namespace PhigrosArchive.Save.Data
{
    public class PhigrosUser
    {
        public bool ShowPlayerId { get; set; }
        public string SelfIntro { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public string Background { get; set; } = string.Empty;
        public byte[] OverflowData { get; set; } = Array.Empty<byte>();

        public PhigrosUser() { }

        public PhigrosUser(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            ShowPlayerId = BitUtils.GetBit(reader.ReadByte(), 0);
            SelfIntro = BitUtils.ReadString(reader);
            Avatar = BitUtils.ReadString(reader);
            Background = BitUtils.ReadString(reader);
            OverflowData = reader.ReadBytes((int)(ms.Length - ms.Position));
        }

        public byte[] ToByteArray()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            byte flags = 0;
            if (ShowPlayerId) flags |= 1 << 0;
            writer.Write(flags);
            writer.Write(BitUtils.WriteString(SelfIntro));
            writer.Write(BitUtils.WriteString(Avatar));
            writer.Write(BitUtils.WriteString(Background));
            writer.Write(OverflowData);
            return ms.ToArray();
        }
    }
}

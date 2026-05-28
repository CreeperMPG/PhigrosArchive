using PhigrosArchive.Utils;

namespace PhigrosArchive.Save.Data
{
    /// <summary>值类型枚举（对应 C 版 ValueType）</summary>
    public enum ValueType : byte
    {
        U8 = 0,
        Bool = 1,
    }

    /// <summary>叶子节点：一个具体的键（对应 C 版 struct LeafNode）</summary>
    public readonly struct LeafNode
    {
        public readonly ValueType Type;
        public readonly string Name;

        public LeafNode(ValueType type, string name)
        {
            Type = type;
            Name = name;
        }
    }

    /// <summary>节点组：一组同属一个逻辑模块的键（对应 C 版 struct Nodes）</summary>
    public readonly struct Nodes
    {
        public readonly LeafNode[] Items;

        public Nodes(LeafNode[] items) => Items = items;
    }

    public class PhigrosKey
    {
        public byte[] FileData = Array.Empty<byte>();

        // ── 解析后的结构化数据 ──

        /// <summary>二进制地图数据（deserializationMap 产出，每个条目包含 5 级值）</summary>
        public Dictionary<string, double[]> Map { get; set; } = new();

        // ── GameKey schema 驱动的叶子节点 ──
        public byte LanotaReadKeys { get; set; }
        public bool CamelliaReadKey { get; set; }

        /// <summary>二进制流中未被解析的剩余数据（对应 C 版的 overflow）</summary>
        public byte[] OverflowData { get; set; } = Array.Empty<byte>();

        // ── GameKey schema（对应 C 版的 GameKey[]） ──

        private static readonly LeafNode[] GameKey1 = { new(ValueType.U8, "lanotaReadKeys") };
        private static readonly LeafNode[] GameKey2 = { new(ValueType.Bool, "camelliaReadKey") };

        private static readonly Nodes[] GameKey = { new(GameKey1), new(GameKey2) };

        // ── 构造器 ──

        public PhigrosKey() { }

        public PhigrosKey(byte[] data)
        {
            FileData = data.ToArray();
            Deserialize();
        }

        // ── 序列化 / 反序列化 ──

        /// <summary>将解析后的属性重新序列化为二进制数据</summary>
        public byte[] ToByteArray()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // 1. read_version → 版本号
            //writer.Write(BitUtils.WriteProtobufVarInt(Version));

            // 2. deserializationMap
            WriteMap(writer);

            // 3. deserializationNodes (GameKey schema)
            WriteNodes(writer);

            // 4. overflow
            writer.Write(OverflowData);

            return ms.ToArray();
        }

        // ── 反序列化 ──

        private void Deserialize()
        {
            if (FileData.Length == 0) return;

            using var ms = new MemoryStream(FileData);
            using var reader = new BinaryReader(ms);

            // 1. read_version → 先读一个 varint 版本号
            //Version = BitUtils.ReadProtobufVarInt(reader);

            // 2. deserializationMap(&ptr, 0) → end=0 的简化 5 级编码
            Map = DeserializeMap(reader);

            // 3. deserializationNodes(gameKey, &ptr, GameKey) → schema 驱动
            DeserializeNodes(reader);

            // 4. overflow = 剩余未消耗字节
            OverflowData = reader.ReadBytes((int)(ms.Length - ms.Position));
        }

        /// <summary>解析二进制地图（对应 C 版 deserializationMap）</summary>
        private Dictionary<string, double[]> DeserializeMap(BinaryReader reader)
        {
            var map = new Dictionary<string, double[]>();
            int count = BitUtils.ReadProtobufVarInt(reader);

            for (int i = 0; i < count; i++)
            {

                // 读 key 字符串
                string key = BitUtils.ReadString(reader);
                long entryStart = reader.BaseStream.Position;

                // 偏移字节（从 entryStart 到下一个 entry 的绝对偏移）
                byte entryOffset = reader.ReadByte();

                // bitfield：5 位，每位标记对应位置是否有值
                byte len = reader.ReadByte();

                // 5 级（对应 C 版 for ii = 0; ii < 5; ii++）
                double[] values = new double[5];
                for (int ii = 0; ii < 5; ii++)
                {
                    if ((len >> ii & 1) != 0)
                        values[ii] = reader.ReadByte();
                    // else: 默认 0
                }

                map[key] = values;

                // 用偏移字节跳到下一个 entry 的起始位置
                reader.BaseStream.Position = entryStart + entryOffset + 1;
            }

            return map;
        }

        /// <summary>按 GameKey schema 解析线性键值（对应 C 版 deserializationNodes）</summary>
        private void DeserializeNodes(BinaryReader reader)
        {
            foreach (var node in GameKey)
            {
                foreach (var leaf in node.Items)
                {
                    switch (leaf.Type)
                    {
                        case ValueType.U8:
                            LanotaReadKeys = reader.ReadByte();
                            break;
                        case ValueType.Bool:
                            CamelliaReadKey = reader.ReadByte() != 0;
                            break;
                    }
                }
            }
        }

        // ── 序列化 ──

        /// <summary>将 Map 重新编码为二进制格式</summary>
        private void WriteMap(BinaryWriter writer)
        {
            // 条目数
            writer.Write(BitUtils.WriteProtobufVarInt(Map.Count));

            foreach (var kvp in Map)
            {

                // key 字符串
                writer.Write(BitUtils.WriteString(kvp.Key));
                long entryStart = writer.BaseStream.Position;

                // 占位：偏移字节（稍后回填）
                long offsetPos = writer.BaseStream.Position;
                writer.Write((byte)0);

                // 计算 bitfield：哪些位置有非零值
                byte len = 0;
                for (int i = 0; i < 5; i++)
                {
                    if (kvp.Value.Length > i && kvp.Value[i] != 0)
                        len |= (byte)(1 << i);
                }
                writer.Write(len);

                // 写入有值的字节
                for (int i = 0; i < 5 && i < kvp.Value.Length; i++)
                {
                    if ((len >> i & 1) != 0)
                        writer.Write((byte)kvp.Value[i]);
                }

                // 回填偏移字节
                long endPos = writer.BaseStream.Position;
                long entrySize = endPos - entryStart - 1;
                writer.BaseStream.Position = offsetPos;
                writer.Write((byte)entrySize);
                writer.BaseStream.Position = endPos;
            }
        }

        /// <summary>按 GameKey schema 写出叶子节点值</summary>
        private void WriteNodes(BinaryWriter writer)
        {
            foreach (var node in GameKey)
            {
                foreach (var leaf in node.Items)
                {
                    switch (leaf.Type)
                    {
                        case ValueType.U8:
                            writer.Write(LanotaReadKeys);
                            break;
                        case ValueType.Bool:
                            writer.Write((byte)(CamelliaReadKey ? 1 : 0));
                            break;
                    }
                }
            }
        }
    }
}

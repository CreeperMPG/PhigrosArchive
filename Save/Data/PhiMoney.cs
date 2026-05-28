namespace PhigrosArchive.Save.Data
{
    /// <summary>
    /// 表示 Phigros 游戏金币（存储单位为 KB）。
    /// 各单位的计算属性和 get/set 封装了对底层 TotalKB 的读写，自动处理进位。
    /// </summary>
    public class PhiMoney
    {
        public long TotalKB { get; set; }

        // ── 计算属性 ──
        public int KB   { get => (int)(TotalKB % 1024);          set => TotalKB = TotalKB / 1024 * 1024 + value; }
        public int MB   { get => (int)(TotalKB / 1024 % 1024);   set => TotalKB = TotalKB - (long)MB * 1024 + (long)value * 1024; }
        public int GB   { get => (int)(TotalKB / (1024L * 1024) % 1024);
            set => TotalKB = TotalKB - (long)GB * 1024L * 1024 + (long)value * 1024L * 1024; }
        public int TB   { get => (int)(TotalKB / (1024L * 1024 * 1024) % 1024);
            set => TotalKB = TotalKB - (long)TB * 1024L * 1024 * 1024 + (long)value * 1024L * 1024 * 1024; }
        public int PB   { get => (int)(TotalKB / (1024L * 1024 * 1024 * 1024));
            set => TotalKB = TotalKB - (long)PB * 1024L * 1024 * 1024 * 1024 + (long)value * 1024L * 1024 * 1024 * 1024; }

        public PhiMoney() { }

        /// <summary>从 5 个分量（KB, MB, GB, TB, PB）构造（用于反序列化）</summary>
        public PhiMoney(int[] components)
        {
            if (components.Length < 5)
                throw new ArgumentException("Money array must have exactly 5 elements.", nameof(components));
            TotalKB = components[0]
                    + (long)components[1] * 1024
                    + (long)components[2] * 1024 * 1024
                    + (long)components[3] * 1024L * 1024 * 1024
                    + (long)components[4] * 1024L * 1024 * 1024 * 1024;
        }

        /// <summary>从裸 TotalKB 构造</summary>
        public PhiMoney(long totalKB) { TotalKB = totalKB; }

        /// <summary>返回 5 个分量（KB, MB, GB, TB, PB）用于序列化</summary>
        public int[] GetComponents() => new[] { KB, MB, GB, TB, PB };

        /// <summary>返回 5 个分量（接口兼容旧代码）</summary>
        public IEnumerable<int> GetMoneyArray() => GetComponents();

        public override string ToString() => $"{PB}PB {TB}TB {GB}GB {MB}MB {KB}KB";
    }
}

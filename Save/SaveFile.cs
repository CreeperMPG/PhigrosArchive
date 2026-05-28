using PhigrosArchive.Abstractions;
using PhigrosArchive.Save.Data;
using System.IO.Compression;

namespace PhigrosArchive.Save
{
    public class SaveFile
    {
        private readonly IDifficultyProvider? _provider;
        private readonly IReadOnlyDictionary<string, byte> _magicBytes;

        public PhigrosProgress GameProgress { get; set; } = default!;
        public PhigrosUser User { get; set; } = default!;
        public PhigrosSettings Settings { get; set; } = default!;
        public PhigrosRecord GameRecord { get; set; } = default!;
        public PhigrosKey GameKey { get; set; } = default!;

        private static readonly byte[] AESKey = new byte[]
        {
            232, 150, 154, 210, 165, 64, 37, 155, 151, 145,
            144, 139, 136, 230, 191, 3, 30, 109, 33, 149,
            110, 250, 214, 138, 80, 221, 85, 214, 122, 176, 146, 75
        };
        private static readonly byte[] AESIV = new byte[]
        {
            42, 79, 240, 138, 200, 13, 99, 7, 0, 87, 197, 149, 24, 200, 50, 83
        };

        // ── 构造器 ──

        /// <summary>从磁盘文件夹路径构造（已解压的存档目录）</summary>
        public SaveFile(string filesPath, IDifficultyProvider? difficultyProvider = null)
            : this(LoadEncryptedFiles(filesPath), difficultyProvider)
        {
        }

        /// <summary>从内存文件字典构造（key: 文件名, value: 加密字节）</summary>
        public SaveFile(Dictionary<string, byte[]> encryptedFiles, IDifficultyProvider? difficultyProvider = null)
        {
            _provider = difficultyProvider;

            // 构造时一次性提取所有文件的 magic byte，不再在 InitBytes 中副作用累积
            var magicBuilder = new Dictionary<string, byte>(5);
            foreach (var (name, type) in s_fileEntries)
            {
                if (encryptedFiles.TryGetValue(name, out var content) && content.Length > 0)
                    magicBuilder[type.FullName!] = content[0];
            }
            _magicBytes = magicBuilder;

            GameProgress = InitBytes(encryptedFiles, "gameProgress", typeof(PhigrosProgress)) as PhigrosProgress
                ?? throw new InvalidOperationException("Failed to initialize GameProgress.");
            User = InitBytes(encryptedFiles, "user", typeof(PhigrosUser)) as PhigrosUser
                ?? throw new InvalidOperationException("Failed to initialize User.");
            Settings = InitBytes(encryptedFiles, "settings", typeof(PhigrosSettings)) as PhigrosSettings
                ?? throw new InvalidOperationException("Failed to initialize Settings.");
            GameRecord = InitBytes(encryptedFiles, "gameRecord", typeof(PhigrosRecord)) as PhigrosRecord
                ?? throw new InvalidOperationException("Failed to initialize GameRecord.");
            GameKey = InitBytes(encryptedFiles, "gameKey", typeof(PhigrosKey)) as PhigrosKey
                ?? throw new InvalidOperationException("Failed to initialize GameKey.");
        }

        // 文件名 → 类型映射表（集中管理，避免散落在各处）
        private static readonly (string Name, Type Type)[] s_fileEntries = new[]
        {
            ("gameProgress", typeof(PhigrosProgress)),
            ("user", typeof(PhigrosUser)),
            ("settings", typeof(PhigrosSettings)),
            ("gameRecord", typeof(PhigrosRecord)),
            ("gameKey", typeof(PhigrosKey)),
        };

        // ── 构造：从 .save zip 字节直接加载 ──

        /// <summary>从 .save zip 文件字节直接加载，内部解压并解密</summary>
        public SaveFile(byte[] zipContent, IDifficultyProvider? difficultyProvider = null)
            : this(ExtractZipFiles(zipContent), difficultyProvider)
        {
        }

        /// <summary>从 .save zip 文件字节加载（工厂方法，语义明确）</summary>
        public static SaveFile FromZipBytes(byte[] zipData, IDifficultyProvider? provider = null)
        {
            return new SaveFile(zipData, provider);
        }

        private static Dictionary<string, byte[]> ExtractZipFiles(byte[] zipData)
        {
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            using var zip = new ZipArchive(new MemoryStream(zipData), ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                using var s = entry.Open();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                files[entry.Name] = ms.ToArray();
            }
            return files;
        }

        // ── 内部 ──

        private static Dictionary<string, byte[]> LoadEncryptedFiles(string filesPath)
        {
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in new[] { "gameProgress", "user", "settings", "gameRecord", "gameKey" })
            {
                string path = System.IO.Path.Combine(filesPath, name);
                if (File.Exists(path))
                    files[name] = File.ReadAllBytes(path);
            }
            return files;
        }

        private object? InitBytes(Dictionary<string, byte[]> files, string filename, Type contentType)
        {
            if (!files.TryGetValue(filename, out var fileContent))
                return null;

            byte[] decrypted = DecryptData(fileContent);

            if (contentType == typeof(PhigrosRecord))
                return new PhigrosRecord(decrypted, _provider);
            return Activator.CreateInstance(contentType, decrypted)!;
        }

        // ── 加密/解密 ──

        public static byte[] DecryptData(byte[] data)
        {
            if (data.Length < 2) throw new ArgumentException("Invalid data length");
            byte[] trimmed = new byte[data.Length - 1];
            Array.Copy(data, 1, trimmed, 0, trimmed.Length);

            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = AESKey;
            aes.IV = AESIV;
            aes.Mode = System.Security.Cryptography.CipherMode.CBC;
            aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(trimmed, 0, trimmed.Length);
        }

        public byte[] EncryptData(byte[] data, Type contentType)
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = AESKey;
            aes.IV = AESIV;
            aes.Mode = System.Security.Cryptography.CipherMode.CBC;
            aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            byte[] encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);
            byte[] result = new byte[encrypted.Length + 1];
            result[0] = _magicBytes.GetValueOrDefault(contentType.FullName ?? "", (byte)0);
            Array.Copy(encrypted, 0, result, 1, encrypted.Length);
            return result;
        }

        // ── 打包 ──

        /// <summary>将存档保存到临时目录并打包为 .zip，返回字节内容</summary>
        public byte[] PackToZip()
        {
            string tempDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "PhiShellTemp",
                Guid.NewGuid().ToString());

            Save(tempDir);

            string zipPath = System.IO.Path.Combine(tempDir, "save");
            byte[] zipData = File.ReadAllBytes(zipPath);

            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);

            return zipData;
        }

        // ── 生成摘要 ──

        /// <summary>
        /// 根据当前存档数据生成完整的 <see cref="SaveSummary"/> 对象。
        /// 逻辑与 SyncSaveToSummary 相同，但直接从存档数据生成新对象而非合并到已有对象。
        /// </summary>
        public SaveSummary GenerateSummary()
        {
            var summary = new SaveSummary();

            summary.Challenge = (ushort)GameProgress.ChallengeModeRank;
            summary.RankingScore = GameRecord.RankingScore ?? 0f;
            summary.Avatar = User.Avatar;

            int ezClr = 0, ezFC = 0, ezPhi = 0;
            int hdClr = 0, hdFC = 0, hdPhi = 0;
            int inClr = 0, inFC = 0, inPhi = 0;
            int atClr = 0, atFC = 0, atPhi = 0;

            foreach (var record in GameRecord.Records.Values)
            {
                CountStats(record.EZ,   ref ezClr, ref ezFC, ref ezPhi);
                CountStats(record.HD,   ref hdClr, ref hdFC, ref hdPhi);
                CountStats(record.IN,   ref inClr, ref inFC, ref inPhi);
                CountStats(record.AT,   ref atClr, ref atFC, ref atPhi);
            }

            summary.Achievements = new PhiDifficultyInfo<Achievement>(
                new Achievement((ushort)ezClr, (ushort)ezFC, (ushort)ezPhi),
                new Achievement((ushort)hdClr, (ushort)hdFC, (ushort)hdPhi),
                new Achievement((ushort)inClr, (ushort)inFC, (ushort)inPhi),
                new Achievement((ushort)atClr, (ushort)atFC, (ushort)atPhi),
                null);

            return summary;
        }

        private static void CountStats(PhiLevelRecord? r, ref int cleared, ref int fc, ref int phi)
        {
            if (r == null) return;
            if (r.Score >= 700000) cleared++;
            if (r.Rank == PhiLevelType.FC || r.Rank == PhiLevelType.Phi) fc++;
            if (r.Rank == PhiLevelType.Phi) phi++;
        }

        // ── 数据校验 ──

        /// <summary>校验存档数据的完整性</summary>
        public List<SaveDataIssue> CheckSaveData(SaveSummary? summary = null)
        {
            var issues = new List<SaveDataIssue>();

            if (_provider == null || !_provider.IsLoaded)
            {
                issues.Add(new(IssueSeverity.Warning, IssueType.DifficultyTSVNotLoaded,
                    "Difficulty TSV not loaded; some checks skipped."));
            }

            // GameKey
            if (GameKey.FileData.Length <= 50)
            {
                issues.Add(new(IssueSeverity.Warning, IssueType.GameKeyTooSmall,
                    data: GameKey.FileData.Length,
                    path: "/GameKey"));
            }

            // GameRecord
            foreach (var (songId, diffInfo) in GameRecord.Records)
            {
                if (_provider != null && _provider.IsLoaded)
                {
                    float? ez = _provider.GetDifficulty(songId, 0);
                    if (ez == null)
                    {
                        issues.Add(new(IssueSeverity.Warning, IssueType.RecordNotInTSV,
                            data: songId, path: $"/GameRecord/{songId}"));
                    }
                    else
                    {
                        float? at = _provider.GetDifficulty(songId, 3);
                        if ((at == null || at == 0) && diffInfo.AT != null)
                        {
                            issues.Add(new(IssueSeverity.Warning, IssueType.RecordHasATButNoAT,
                                data: songId, path: $"/GameRecord/{songId}"));
                        }
                    }
                }

                foreach (var (diff, level) in diffInfo.GetDictionary(true))
                {
                    if (level == null) continue;
                    if (level.Score < 0 || level.Score > 1000000)
                        issues.Add(new(IssueSeverity.Warning, IssueType.InvalidScore,
                            data: new { Song = songId, Diff = diff, Score = level.Score },
                            path: $"/GameRecord/{songId}/{diff}"));
                    if (level.Acc < 0 || level.Acc > 100)
                        issues.Add(new(IssueSeverity.Warning, IssueType.InvalidAccuracy,
                            data: new { Song = songId, Diff = diff, Acc = level.Acc },
                            path: $"/GameRecord/{songId}/{diff}"));
                }
            }

            // GameProgress
            var challenge = GameProgress.ChallengeModeRank;
            if (challenge / 100 > 5)
                issues.Add(new(IssueSeverity.Warning, IssueType.ChallengeTypeTooHigh,
                    data: challenge / 100));
            if (challenge % 100 > 51)
                issues.Add(new(IssueSeverity.Warning, IssueType.ChallengeRankTooHigh,
                    data: challenge % 100));

            // Save vs Summary consistency
            if (summary != null)
            {
                if (GameProgress.ChallengeModeRank != summary.Challenge)
                    issues.Add(new(IssueSeverity.Info, IssueType.ChallengeRankDifferent,
                        data: new { Save = GameProgress.ChallengeModeRank, Summary = summary.Challenge }));
                if (User.Avatar != summary.Avatar)
                    issues.Add(new(IssueSeverity.Info, IssueType.AvatarDifferent,
                        data: new { Save = User.Avatar, Summary = summary.Avatar }));

                if (summary.RankingScore < 0 || summary.RankingScore > 17f)
                    issues.Add(new(IssueSeverity.Warning, IssueType.SummaryRankingScoreInvalid,
                        data: summary.RankingScore));
            }

            return issues;
        }

        // ── 保存 ──

        public void SaveSingleFile(string filesPath, string filename, object data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Type type = data.GetType();
            byte[] raw = type.GetMethod("ToByteArray")?.Invoke(data, null) as byte[]
                ?? throw new InvalidOperationException($"Type {type.FullName} has no ToByteArray method.");
            byte[] encrypted = EncryptData(raw, type);
            string path = System.IO.Path.Combine(filesPath, filename);
            File.WriteAllBytes(path, encrypted);
        }

        public void Save(string filesPath)
        {
            string savePath = System.IO.Path.Combine(filesPath, "save_files");
            if (Directory.Exists(savePath)) Directory.Delete(savePath, true);
            Directory.CreateDirectory(savePath);

            SaveSingleFile(savePath, "gameProgress", GameProgress);
            SaveSingleFile(savePath, "user", User);
            SaveSingleFile(savePath, "settings", Settings);
            SaveSingleFile(savePath, "gameRecord", GameRecord);
            SaveSingleFile(savePath, "gameKey", GameKey);

            string zipPath = System.IO.Path.Combine(filesPath, "save");
            if (File.Exists(zipPath)) File.Delete(zipPath);

            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (var f in Directory.GetFiles(savePath))
                zip.CreateEntryFromFile(f, System.IO.Path.GetFileName(f), CompressionLevel.Optimal);
        }
    }
}

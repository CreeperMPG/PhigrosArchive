using PhigrosArchive.Abstractions;
using PhigrosArchive.Save.Data;
using PhigrosArchive.Utils;

namespace PhigrosArchive.Save
{
    /// <summary>
    /// 存档云元数据。
    /// </summary>
    public class SaveCloudInfo
    {
        public string FileUrl { get; set; }
        public string FileObjectID { get; set; }
        public string SaveInfoObjectID { get; set; }
        public string SaveUpdateTime { get; set; }

        public SaveCloudInfo(string fileUrl, string fileObjectID, string saveInfoObjectID, string saveUpdateTime)
        {
            FileUrl = fileUrl; FileObjectID = fileObjectID;
            SaveInfoObjectID = saveInfoObjectID; SaveUpdateTime = saveUpdateTime;
        }
    }

    /// <summary>
    /// 存档元数据 + 存档文件操作（不依赖 sessionToken）。
    /// SaveFile 由 FetchSaveAsync 独立获取，不作为属性持有。
    /// </summary>
    public class SaveFileInfo
    {
        public SaveCloudInfo? CloudInfo { get; set; }
        public SaveSummary Summary { get; set; }

        public SaveFileInfo(SaveCloudInfo cloudInfo, string summary)
        {
            CloudInfo = cloudInfo;
            Summary = new SaveSummary(summary);
        }

        /// <summary>当存档数据被修改时触发</summary>
        public event EventHandler<SaveModifiedEventArgs>? SaveModified;

        internal void NotifyModified(string path, string propertyName)
        {
            SaveModified?.Invoke(this, new SaveModifiedEventArgs(path, propertyName));
        }

        // ── 下载存档（普通 HTTP，不需要 sessionToken） ──

        /// <summary>
        /// 从 CloudInfo.FileUrl 下载 .save zip 文件的原始字节。
        /// 全内存模式，不产生临时文件。
        /// </summary>
        public async Task<byte[]> DownloadSaveZipAsync()
        {
            if (CloudInfo == null)
                throw new InvalidOperationException("CloudInfo is null.");

            using var client = new HttpClient();
            var response = await client.GetAsync(CloudInfo.FileUrl).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 从 CloudInfo.FileUrl 下载并解析存档文件。
        /// 全内存模式，不产生临时文件。
        /// </summary>
        public async Task<SaveFile> FetchSaveAsync(IDifficultyProvider? difficultyProvider = null)
        {
            byte[] zipData = await DownloadSaveZipAsync().ConfigureAwait(false);
            return SaveFile.FromZipBytes(zipData, difficultyProvider);
        }

        // ── 同步存档到摘要 ──

        /// <summary>将 SaveFile 的游戏数据同步回 Summary</summary>
        public void SyncSaveToSummary(SaveFile file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            var generated = file.GenerateSummary();

            Summary.Challenge = generated.Challenge;
            Summary.RankingScore = generated.RankingScore;
            Summary.Avatar = generated.Avatar;

            // 仅在原有 Summary 有 Achievements 对象时才更新
            if (Summary.Achievements?.EZ != null)
                Summary.Achievements.EZ = generated.Achievements.EZ;
            if (Summary.Achievements?.HD != null)
                Summary.Achievements.HD = generated.Achievements.HD;
            if (Summary.Achievements?.IN != null)
                Summary.Achievements.IN = generated.Achievements.IN;
            if (Summary.Achievements?.AT != null)
                Summary.Achievements.AT = generated.Achievements.AT;
        }
    }

    public class SaveModifiedEventArgs : EventArgs
    {
        public string Path { get; }
        public string PropertyName { get; }
        public SaveModifiedEventArgs(string path, string propertyName)
        {
            Path = path; PropertyName = propertyName;
        }
    }
}

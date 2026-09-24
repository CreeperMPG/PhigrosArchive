using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using PhigrosArchive.Abstractions;
using PhigrosArchive.Save;
using PhigrosArchive.Utils;

namespace PhigrosArchive
{
    /// <summary>
    /// 玩家信息类，持有 sessionToken，提供云端 API 调用。
    /// </summary>
    public class PhigrosPlayerInfo
    {
        public string Nickname { get; init; }
        public string ShortID { get; init; }
        public string UserObjectID { get; init; }
        public string CreateTime { get; init; }
        public string SessionToken { get; internal set; }

        public PhigrosPlayerInfo()
        {
            Nickname = string.Empty;
            ShortID = string.Empty;
            UserObjectID = string.Empty;
            CreateTime = string.Empty;
            SessionToken = string.Empty;
        }

        public PhigrosPlayerInfo(string nickname, string shortId, string objectId, string createTime, string sessionToken = "")
        {
            Nickname = nickname;
            ShortID = shortId;
            UserObjectID = objectId;
            CreateTime = createTime;
            SessionToken = sessionToken;
        }

        // ── 工厂方法 ──

        public static PhigrosPlayerInfo FromJson(JsonElement userInfo, string sessionToken = "")
        {
            return new PhigrosPlayerInfo(
                userInfo.GetProperty("nickname").GetString() ?? string.Empty,
                userInfo.GetProperty("shortId").GetString() ?? string.Empty,
                userInfo.GetProperty("objectId").GetString() ?? string.Empty,
                userInfo.GetProperty("createdAt").GetString() ?? string.Empty,
                sessionToken
            );
        }

        public static async Task<PhigrosPlayerInfo> FetchAsync(string sessionToken)
        {
            var json = await HttpUtils.GetPigeonHttpJsonAsync(
                HttpUtils.PigeonUserApiUrl, sessionToken).ConfigureAwait(false);
            return FromJson(json.RootElement, sessionToken);
        }

        public static PhigrosPlayerInfo Fetch(string sessionToken)
            => FetchAsync(sessionToken).GetAwaiter().GetResult();

        // ── 存档信息 ──

        /// <summary>从云端获取所有存档槽位的摘要和云元信息</summary>
        public async Task<SaveFileInfo[]> FetchSaveInfoAsync()
        {
            var saveJson = await HttpUtils.GetPigeonHttpJsonAsync(
                HttpUtils.PigeonSaveApiUrl, SessionToken).ConfigureAwait(false);
            var results = saveJson.RootElement.GetProperty("results").EnumerateArray();

            var list = new List<SaveFileInfo>();
            foreach (var info in results)
            {
                try
                {
                    string saveUrl = info.GetProperty("gameFile").GetProperty("url").GetString()!;
                    string saveUpdateTime = info.GetProperty("gameFile").GetProperty("updatedAt").GetString()!;
                    string summary = info.GetProperty("summary").GetString()!;
                    string saveFileObjectID = info.GetProperty("gameFile").GetProperty("objectId").GetString()!;
                    string saveInfoObjectID = info.GetProperty("objectId").GetString()!;

                    var cloudInfo = new SaveCloudInfo(saveUrl, saveFileObjectID, saveInfoObjectID, saveUpdateTime);
                    list.Add(new SaveFileInfo(cloudInfo, summary));
                }
                catch { }
            }
            return list.ToArray();
        }

        // ── 上传存档 ──

        /// <summary>
        /// 上传存档文件到七牛云 + LeanCloud，并更新摘要。
        /// 返回更新后的 CloudInfo（含新的 FileObjectID）。
        /// </summary>
        public async Task<SaveCloudInfo> UploadSaveAsync(
            byte[] data, SaveFileInfo? saveInfo = null,
            ISaveLogger? logger = null, bool output = false)
        {
            var cloudInfo = saveInfo?.CloudInfo;
            var newCloudInfo = new SaveCloudInfo(
                cloudInfo?.FileUrl ?? "",
                cloudInfo?.FileObjectID ?? "",
                cloudInfo?.SaveInfoObjectID ?? "",
                DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "Z");
            string fileMd5 = GetMD5String(data);

            void Progress(string msg) { if (output) { logger?.Progress(msg); Console.Write(msg); } }
            string oldFileObjectId = cloudInfo?.FileObjectID ?? "";

            // 1. 构造元数据
            Progress("\nConstructing metadata ......");
            var fileMeta = new
            {
                name = ".save",
                __type = "File",
                ACL = new Dictionary<string, object> { [UserObjectID] = new { read = true, write = true } },
                prefix = "gamesaves",
                metaData = new { size = data.Length, _checksum = fileMd5, prefix = "gamesaves" }
            };

            // 2. 申请上传 token
            Progress("\nApplying upload token ......");
            using var leanCloud = HttpUtils.CreatePigeonHttpClient(SessionToken);
            var tokenReq = new HttpRequestMessage(HttpMethod.Post, "https://rak3ffdi.cloud.tds1.tapapis.cn/1.1/fileTokens")
            {
                Content = new StringContent(JsonSerializer.Serialize(fileMeta), Encoding.UTF8, "application/json")
            };
            tokenReq.Headers.Host = "rak3ffdi.cloud.tds1.tapapis.cn";
            tokenReq.Headers.ConnectionClose = true;

            var tokenResp = await leanCloud.SendAsync(tokenReq).ConfigureAwait(false);
            tokenResp.EnsureSuccessStatusCode();
            using var tokenDoc = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync().ConfigureAwait(false));
            var tokenRoot = tokenDoc.RootElement;
            string fileKey = tokenRoot.GetProperty("key").GetString()!;
            newCloudInfo.FileObjectID = tokenRoot.GetProperty("objectId").GetString()!;
            string uploadToken = tokenRoot.GetProperty("token").GetString()!;
            Progress($"\n  fileKey: {fileKey}");
            Progress($"\n  newFileObjectID: {newCloudInfo.FileObjectID}");
            Progress($"\n  uploadToken: {uploadToken}");

            // 3. 七牛云初始化
            Progress("\nInitializing upload ......");
            string b64Key = Convert.ToBase64String(Encoding.UTF8.GetBytes(fileKey));
            string initUrl = $"http://upload.qiniup.com/buckets/rAK3Ffdi/objects/{b64Key}/uploads";
            using var qiniu = new HttpClient();

            var initReq = new HttpRequestMessage(HttpMethod.Post, initUrl);
            initReq.Headers.Add("Authorization", $"UpToken {uploadToken}");
            initReq.Headers.Add("Host", "upload.qiniup.com");
            initReq.Headers.ConnectionClose = false;
            initReq.Content = new StringContent("", Encoding.UTF8, "application/octet-stream");

            var initResp = await qiniu.SendAsync(initReq).ConfigureAwait(false);
            initResp.EnsureSuccessStatusCode();
            var initDoc = JsonDocument.Parse(await initResp.Content.ReadAsStringAsync().ConfigureAwait(false));
            string uploadId = initDoc.RootElement.GetProperty("uploadId").GetString()!;
            Progress($"\n  uploadId: {uploadId}");

            // 4. 上传
            Progress("\nUploading file ......");
            string uploadUrl = $"http://upload.qiniup.com/buckets/rAK3Ffdi/objects/{b64Key}/uploads/{uploadId}/1";
            var upReq = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
            upReq.Headers.Add("Authorization", $"UpToken {uploadToken}");
            upReq.Headers.Add("Host", "upload.qiniup.com");
            upReq.Content = new ByteArrayContent(data);

            var upResp = await qiniu.SendAsync(upReq).ConfigureAwait(false);
            upResp.EnsureSuccessStatusCode();
            var upDoc = JsonDocument.Parse(await upResp.Content.ReadAsStringAsync().ConfigureAwait(false));
            string etag = upDoc.RootElement.GetProperty("etag").GetString()!;
            Progress($"\n  etag: {etag}");

            // 5. 合并分片
            Progress("\nMerging data ......");
            string mergeUrl = $"http://upload.qiniup.com/buckets/rAK3Ffdi/objects/{b64Key}/uploads/{uploadId}";
            var mergeReq = new HttpRequestMessage(HttpMethod.Post, mergeUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { parts = new[] { new { partNumber = 1, etag } } }),
                    Encoding.UTF8, "application/json")
            };
            mergeReq.Headers.Add("Authorization", $"UpToken {uploadToken}");
            mergeReq.Headers.Add("Host", "upload.qiniup.com");

            var mergeResp = await qiniu.SendAsync(mergeReq).ConfigureAwait(false);
            mergeResp.EnsureSuccessStatusCode();
            Progress($"\n  merge response: {await mergeResp.Content.ReadAsStringAsync().ConfigureAwait(false)}");

            // 6. 回调
            Progress("\nRequesting callback ......");
            var cbReq = new HttpRequestMessage(HttpMethod.Post, "https://rak3ffdi.cloud.tds1.tapapis.cn/1.1/fileCallback")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { result = true, token = uploadToken }),
                    Encoding.UTF8, "application/json")
            };
            cbReq.Headers.Host = "rak3ffdi.cloud.tds1.tapapis.cn";
            var cbResp = await leanCloud.SendAsync(cbReq).ConfigureAwait(false);
            cbResp.EnsureSuccessStatusCode();
            Progress($"\n  callback: {await cbResp.Content.ReadAsStringAsync().ConfigureAwait(false)}");
            //await PhigrosPlayerInfoExtensions.UploadSummaryAsync(
            //    SessionToken, ObjectID, newFileObjectID, ObjectID, "123").ConfigureAwait(false);

            // 7. 更新摘要（如果传入了 saveInfo）
            if (saveInfo != null)
            {
                Progress("\nUploading summary ......");
                await PhigrosPlayerInfoExtensions.UploadSaveAsync(
                    SessionToken, UserObjectID, saveInfo, newCloudInfo.FileObjectID).ConfigureAwait(false);
            }

            return newCloudInfo;
        }

        // ── Token 刷新 ──

        /// <summary>刷新 session token，内部更新 SessionToken 属性</summary>
        public async Task<bool> RefreshTokenAsync()
        {
            try
            {
                using var client = HttpUtils.CreatePigeonHttpClient(SessionToken);
                string url = $"{HttpUtils.PigeonApiRoot}/users/{UserObjectID}/refreshSessionToken";
                var response = await client.PutAsync(url, null).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var json = JsonDocument.Parse(content);
                    SessionToken = json.RootElement.GetProperty("sessionToken").GetString()
                        ?? throw new InvalidOperationException("Response missing sessionToken");
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // ── 删除存档 ──

        /// <summary>从云端删除指定 FileObjectID 的存档文件</summary>
        public async Task DeleteSaveAsync(string fileObjectId)
        {
            var deleteFileUrl = $"https://rak3ffdi.cloud.tds1.tapapis.cn/1.1/files/{fileObjectId}";
            using var client = HttpUtils.CreatePigeonHttpClient(SessionToken);
            var request = new HttpRequestMessage(HttpMethod.Delete, deleteFileUrl);
            request.Headers.Host = "rak3ffdi.cloud.tds1.tapapis.cn";
            request.Headers.ConnectionClose = true;
            request.Content = new StringContent("", Encoding.UTF8, "application/json");
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        // ── 工具 ──

        private static string GetMD5String(byte[] data)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(data);
            var sb = new StringBuilder();
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    /// <summary>
    /// 与 PhigrosPlayerInfo 配合使用的扩展方法（避免主类过长）。
    /// </summary>
    internal static class PhigrosPlayerInfoExtensions
    {
        /// <summary>上传摘要到 LeanCloud</summary>
        public static async Task UploadSummaryAsync(
            string sessionToken, string userObjectID, SaveFileInfo saveInfo, string newFileObjectID)
        {
            if (saveInfo.CloudInfo == null)
                throw new InvalidOperationException("Cloud info is missing.");

            var dateTime = DateTime.Now;
            string iso = dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "Z";
            var saveData = new
            {
                summary = saveInfo.Summary?.ToBase64String() ?? "",
                modifiedAt = new { __type = "Date", iso },
                gameFile = new { __type = "Pointer", className = "_File", objectId = newFileObjectID },
                ACL = new Dictionary<string, object>
                {
                    [userObjectID] = new { read = true, write = true }
                },
                user = new { __type = "Pointer", className = "_User", objectId = userObjectID }
            };

            var content = new StringContent(JsonSerializer.Serialize(saveData), Encoding.UTF8, "application/json");
            using var client = HttpUtils.CreatePigeonHttpClient(sessionToken);
            var response = await client.PutAsync(
                $"{HttpUtils.PigeonSaveApiUrl}/{saveInfo.CloudInfo.SaveInfoObjectID}", content)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to upload summary: {response.ReasonPhrase}");
        }
        public static async Task UploadSaveAsync(
            string sessionToken, string userObjectID, SaveFileInfo saveInfo, string newFileObjectID)
        {
            if (saveInfo.CloudInfo == null)
                throw new InvalidOperationException("Cloud info is missing.");

            var dateTime = DateTime.Now;
            string iso = dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "Z";
            var saveData = new
            {
                summary = saveInfo.Summary?.ToBase64String() ?? "",
                modifiedAt = new { __type = "Date", iso },
                gameFile = new { __type = "Pointer", className = "_File", objectId = newFileObjectID },
                ACL = new Dictionary<string, object>
                {
                    [userObjectID] = new { read = true, write = true }
                },
                user = new { __type = "Pointer", className = "_User", objectId = userObjectID },
                name = ".save"
            };

            var content = new StringContent(JsonSerializer.Serialize(saveData), Encoding.UTF8, "application/json");
            using var client = HttpUtils.CreatePigeonHttpClient(sessionToken);
            var response = await client.PostAsync(
                $"{HttpUtils.PigeonSaveApiUrl}", content)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to upload summary: {response.ReasonPhrase}");
        }
    }
}

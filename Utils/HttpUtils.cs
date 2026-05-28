using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PhigrosArchive.Utils
{
    internal static class HttpUtils
    {
        public static readonly string PigeonApiRoot = "https://rak3ffdi.cloud.tds1.tapapis.cn/1.1";
        public static readonly string PigeonUserApiUrl = PigeonApiRoot  + "/users/me";
        public static readonly string PigeonSaveApiUrl = PigeonApiRoot  + "/classes/_GameSave";
        public static HttpClient CreatePigeonHttpClient(string token)
        {
            var client = new HttpClient(new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            });
            client.DefaultRequestHeaders.Add("X-LC-Id", "rAK3FfdieFob2Nn8Am");
            client.DefaultRequestHeaders.Add("X-LC-Key", "Qr9AEqtuoSVS3zeD6iVbM4ZC0AtkJcQ89tywVyi0");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LeanCloud-CSharp-SDK/1.0.3");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.Add("X-LC-Session", token);
            return client;
        }
        
        // ── 同步版（旧接口，暂保留兼容） ──

        public static JsonDocument GetPigeonHttpJson(string url, string token)
        {
            return GetPigeonHttpJsonAsync(url, token).GetAwaiter().GetResult();
        }

        // ── 异步版 ──

        public static async Task<JsonDocument> GetPigeonHttpJsonAsync(string url, string token)
        {
            using var client = CreatePigeonHttpClient(token);
            var response = await client.GetAsync(url).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonDocument.Parse(content);
            }
            else
            {
                throw new HttpRequestException($"Failed to fetch data from {url}: {response.ReasonPhrase}");
            }
        }
    }
}

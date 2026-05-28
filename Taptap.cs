using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PhigrosArchive;

public struct QRCodeResponse
{
    public string device_code;
    public string qrcode_url;
    public int request_time;
    public int expires_in;
    public int interval;
}
public struct QRCodeResult
{
    public string kid;
    public string access_token;
    public string mac_key;
    public string mac_algorithm;
}
public struct UserProfile
{
    public string openid;
    public string unionid;
    public string name;
}
public enum QRCodeStatus
{
    AuthorizationPending, // authorization_pending 未扫码
    AuthorizationWaiting, // authorization_waiting 已扫码，等待授权
    Success,
    InvalidGrantCode,
    Error
}
public static class Taptap
{
    public static readonly string TapSDKVersion = "2.1";
    public static readonly string WebHost = "https://accounts.tapapis.com";
    public static readonly string ChinaWebHost = "https://accounts.tapapis.cn";
    public static readonly string ApiHost = "https://open.tapapis.com";
    public static readonly string ChinaApiHost = "https://open.tapapis.cn";
    public static readonly string CodeUrl = $"{WebHost}/oauth2/v1/device/code";
    public static readonly string ChinaCodeUrl = $"{ChinaWebHost}/oauth2/v1/device/code";
    public static readonly string TokenUrl = $"{WebHost}/oauth2/v1/token";
    public static readonly string ChinaTokenUrl = $"{ChinaWebHost}/oauth2/v1/token";
    public static readonly string ClientID = "rAK3FfdieFob2Nn8Am";
    private static string GetHost(string host) => host.Replace("https://", "");
    public static async Task<KeyValuePair<QRCodeStatus, QRCodeResult?>> PollQRCode(string device_code, bool china = true)
    {
        try
        {
            var httpClient = new HttpClient();
            var url = china ? ChinaTokenUrl : TokenUrl;
            var parameters = new Dictionary<string, string>
            {
                { "client_id", ClientID },
                { "grant_type", "device_token" },
                { "code", device_code },
                { "version", TapSDKVersion },
                { "platform", "unity" },
                { "secret_type", "hmac-sha-1" }
            };
            var formData = new FormUrlEncodedContent(parameters);
            HttpResponseMessage response = await httpClient.PostAsync(url, formData);
            var content = response.Content.ReadAsStringAsync().Result;
            var resultJsonElement = JsonDocument.Parse(content).RootElement;
            resultJsonElement.TryGetProperty("success", out JsonElement successElement);
            bool success = successElement.GetBoolean();
            resultJsonElement.TryGetProperty("data", out JsonElement dataElement);
            if (success)
            {
                dataElement.TryGetProperty("access_token", out JsonElement accessTokenElement);
                dataElement.TryGetProperty("kid", out JsonElement kidElement);
                dataElement.TryGetProperty("mac_key", out JsonElement macKeyElement);
                dataElement.TryGetProperty("mac_algorithm", out JsonElement macAlgorithmElement);
                var result = new QRCodeResult
                {
                    access_token = accessTokenElement.GetString() ?? "",
                    kid = kidElement.GetString() ?? "",
                    mac_key = macKeyElement.GetString() ?? "",
                    mac_algorithm = macAlgorithmElement.GetString() ?? ""
                };
                return new KeyValuePair<QRCodeStatus, QRCodeResult?>(QRCodeStatus.Success, result);
            }
            else
            {
                dataElement.TryGetProperty("error", out JsonElement errorElement);
                var error = errorElement.GetString() ?? "";
                return error switch
                {
                    "authorization_pending" => new KeyValuePair<QRCodeStatus, QRCodeResult?>(QRCodeStatus.AuthorizationPending, null),
                    "authorization_waiting" => new KeyValuePair<QRCodeStatus, QRCodeResult?>(QRCodeStatus.AuthorizationWaiting, null),
                    "invalid_grant_code" => new KeyValuePair<QRCodeStatus, QRCodeResult?>(QRCodeStatus.InvalidGrantCode, null),
                    _ => new KeyValuePair<QRCodeStatus, QRCodeResult?>(QRCodeStatus.Error, null),
                };
            }
        }
        catch
        {
            return new KeyValuePair<QRCodeStatus, QRCodeResult?>(QRCodeStatus.Error, null);
        }
    }
    public static async Task<QRCodeResponse?> GetLoginQRCode(bool china = true)
    {
        var httpClient = new HttpClient();
        var url = china ? ChinaCodeUrl : CodeUrl;
        var parameters = new Dictionary<string, string>
        {
            { "client_id", ClientID },
            { "response_type", "device_code" },
            { "scope", "public_profile" },
            { "version", TapSDKVersion },
            { "platform", "unity" }
        };
        var formData = new FormUrlEncodedContent(parameters);
        HttpResponseMessage response = await httpClient.PostAsync(url, formData);
        if (!response.IsSuccessStatusCode) return null;
        var content = response.Content.ReadAsStringAsync().Result;
        var resultJsonElement = JsonDocument.Parse(content).RootElement;
        resultJsonElement.TryGetProperty("data", out JsonElement dataElement);
        resultJsonElement.TryGetProperty("success", out JsonElement successElement);
        resultJsonElement.TryGetProperty("now", out JsonElement requestTimeElement);
        if (successElement.GetBoolean() == false) return null;
        dataElement.TryGetProperty("device_code", out JsonElement deviceCodeElement);
        dataElement.TryGetProperty("qrcode_url", out JsonElement qrcodeUrlElement);
        dataElement.TryGetProperty("expires_in", out JsonElement expiresInElement);
        dataElement.TryGetProperty("interval", out JsonElement intervalElement);
        var result = new QRCodeResponse
        {
            device_code = deviceCodeElement.GetString() ?? "",
            qrcode_url = qrcodeUrlElement.GetString() ?? "",
            request_time = requestTimeElement.GetInt32(),
            expires_in = expiresInElement.GetInt32(),
            interval = intervalElement.GetInt32()
        };
        return result;
    }
    public static UserProfile? FetchUserProfile(QRCodeResult qrResult)
    {
        string requestUrl = $"/account/profile/v1?client_id={ClientID}";
        string signature = GetMacTokenSignature(GetHost(ChinaApiHost), requestUrl, "GET", qrResult.mac_key, qrResult.kid);
        using (var httpClient = new HttpClient())
        {
            var uri = new Uri($"{ChinaApiHost}{requestUrl}");
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("Authorization", signature);
            var response = httpClient.SendAsync(request).Result;
            var responseBody = response.Content.ReadAsStringAsync().Result;
            var resultJsonElement = JsonDocument.Parse(responseBody).RootElement;
            resultJsonElement.TryGetProperty("data", out JsonElement dataElement);
            resultJsonElement.TryGetProperty("success", out JsonElement successElement);
            if (successElement.GetBoolean() == false) return null;
            dataElement.TryGetProperty("openid", out JsonElement OpenIDElement);
            dataElement.TryGetProperty("unionid", out JsonElement UnionIDElement);
            dataElement.TryGetProperty("name", out JsonElement NameElement);
            var result = new UserProfile
            {
                openid = OpenIDElement.GetString() ?? "",
                unionid = UnionIDElement.GetString() ?? "",
                name = NameElement.GetString() ?? "",
            };
            return result;
        }
    }
    public static string GetMacTokenSignature(string host, string requestUrl, string method, string macKey, string kid)
    {
        string macTokenPattern = "MAC id=\"{0}\",ts=\"{1}\",nonce=\"{2}\",mac=\"{3}\"";
        string timestamp = ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
        string nonce = GenerateNonce(16);
        string[] signArray = { timestamp, nonce, method, requestUrl, host, "443", "" };
        string separator = "\n";
        string signInput = string.Join(separator, signArray) + separator;

        using (var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(macKey)))
        {
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signInput));
            string macStr = Convert.ToBase64String(hash);
            return string.Format(macTokenPattern, kid, timestamp, nonce, macStr);
        }
    }

    public static string GenerateNonce(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var random = new Random();
        var nonce = new char[length];

        for (int i = 0; i < length; i++)
        {
            nonce[i] = chars[random.Next(chars.Length)];
        }

        return new string(nonce);
    }
    public static async Task<JsonDocument> GetPhiPlayerInfoByTaptap(QRCodeResult qrResult, UserProfile userProfile)
    {
        var httpClient = new HttpClient();
        var url = "https://rak3ffdi.cloud.tds1.tapapis.cn/1.1/users";
        // Sign
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var data = timestamp + "Qr9AEqtuoSVS3zeD6iVbM4ZC0AtkJcQ89tywVyi0";
        using var md5 = MD5.Create();
        var hash = Convert.ToHexString(md5.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLower();
        var sign = hash + "," + timestamp;
        object parameters = new
        {
            authData = new
            {
                taptap = new Dictionary<string, string>
                {
                    { "access_token", qrResult.access_token },
                    { "kid", qrResult.kid },
                    { "mac_key", qrResult.mac_key },
                    { "mac_algorithm", qrResult.mac_algorithm },
                    { "openid", userProfile.openid },
                    { "unionid", userProfile.unionid },
                    { "name", userProfile.name }
                }
            }
        };
        string jsonData = JsonSerializer.Serialize(parameters);
        var requestContent = new StringContent(jsonData, Encoding.UTF8, "application/json");
        httpClient.DefaultRequestHeaders.Add("X-LC-Id", ClientID);
        httpClient.DefaultRequestHeaders.Add("X-LC-Sign", sign);
        HttpResponseMessage response = await httpClient.PostAsync(url, requestContent);
        var content = response.Content.ReadAsStringAsync().Result;
        return JsonDocument.Parse(content);
    }
}

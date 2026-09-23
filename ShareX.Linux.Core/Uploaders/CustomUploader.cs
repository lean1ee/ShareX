using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ShareX.Linux.Core.Uploaders;

public class CustomUploaderConfig
{
    public string Name { get; set; } = "Custom Uploader";
    public string DestinationType { get; set; } = "ImageUploader";
    public string RequestMethod { get; set; } = "POST";
    public string RequestURL { get; set; } = string.Empty;
    public Dictionary<string, string>? Headers { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
    public Dictionary<string, string>? Arguments { get; set; }
    public string Body { get; set; } = "MultipartFormData"; // MultipartFormData, JSON, Binary
    public string FileFormName { get; set; } = "file";
    public string? URL { get; set; }
    public string? ThumbnailURL { get; set; }
    public string? DeletionURL { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CustomUploader : IUploadService
{
    public string ServiceName => Config.Name;
    public CustomUploaderConfig Config { get; }
    private static readonly HttpClient _httpClient = new();

    public CustomUploader(CustomUploaderConfig config)
    {
        Config = config;
    }

    public static CustomUploader FromFile(string sxcuFilePath)
    {
        var json = File.ReadAllText(sxcuFilePath);
        var config = JsonConvert.DeserializeObject<CustomUploaderConfig>(json)
            ?? throw new InvalidOperationException("Failed to deserialize SXCU file");
        return new CustomUploader(config);
    }

    public async Task<UploadResult> UploadAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var method = new HttpMethod(Config.RequestMethod.ToUpperInvariant());
            var url = Config.RequestURL;

            // Handle URL query parameters
            if (Config.Parameters != null && Config.Parameters.Count > 0)
            {
                var queryParams = new List<string>();
                foreach (var (k, v) in Config.Parameters)
                {
                    queryParams.Add($"{Uri.EscapeDataString(k)}={Uri.EscapeDataString(v)}");
                }
                url += (url.Contains('?') ? "&" : "?") + string.Join("&", queryParams);
            }

            using var request = new HttpRequestMessage(method, url);

            // Add headers
            if (Config.Headers != null)
            {
                foreach (var (k, v) in Config.Headers)
                {
                    request.Headers.TryAddWithoutValidation(k, v);
                }
            }

            // Build body
            if (string.Equals(Config.Body, "MultipartFormData", StringComparison.OrdinalIgnoreCase))
            {
                var multipart = new MultipartFormDataContent();
                if (Config.Arguments != null)
                {
                    foreach (var (k, v) in Config.Arguments)
                    {
                        multipart.Add(new StringContent(v), k);
                    }
                }

                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                multipart.Add(fileContent, Config.FileFormName, fileName);
                request.Content = multipart;
            }
            else
            {
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                request.Content = fileContent;
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return UploadResult.Failed($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {responseString}");
            }

            var parsedUrl = ParseResponseTokens(Config.URL, responseString);
            var deleteUrl = ParseResponseTokens(Config.DeletionURL, responseString);

            if (string.IsNullOrWhiteSpace(parsedUrl))
            {
                // Fallback: If URL token isn't specified, check if response is plain text URL
                if (Uri.TryCreate(responseString.Trim(), UriKind.Absolute, out var uri))
                {
                    parsedUrl = uri.ToString();
                }
                else
                {
                    parsedUrl = responseString.Trim();
                }
            }

            return UploadResult.Succeeded(parsedUrl, deleteUrl, responseString);
        }
        catch (Exception ex)
        {
            return UploadResult.Failed($"Custom uploader exception: {ex.Message}");
        }
    }

    private static string? ParseResponseTokens(string? template, string response)
    {
        if (string.IsNullOrWhiteSpace(template)) return null;

        var result = template;

        // Parse {json:path.to.prop}
        result = Regex.Replace(result, @"\{json:([^\}]+)\}", m =>
        {
            try
            {
                var tokenPath = m.Groups[1].Value;
                var jtoken = JToken.Parse(response);
                var val = jtoken.SelectToken(tokenPath);
                return val?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        });

        // Parse $json:path$
        result = Regex.Replace(result, @"\$json:([^\$]+)\$", m =>
        {
            try
            {
                var tokenPath = m.Groups[1].Value;
                var jtoken = JToken.Parse(response);
                var val = jtoken.SelectToken(tokenPath);
                return val?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        });

        // Parse {response}
        result = result.Replace("{response}", response.Trim());

        return result;
    }
}

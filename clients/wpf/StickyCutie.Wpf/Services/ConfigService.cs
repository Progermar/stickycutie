using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StickyCutie.Wpf.Services;

static class ConfigService
{
    public const string DefaultApiUrl = "https://stickycutie.dexaweb.com.br/";
    static readonly Lazy<string> _apiUrl = new(Normalize);

    public static string GetApiUrl() => _apiUrl.Value;

    static string Normalize()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "stickycutie_settings.json");
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var cfg = JsonSerializer.Deserialize<StickyCutieSettings>(json);
                if (!string.IsNullOrWhiteSpace(cfg?.ApiUrl))
                {
                    return EnsureTrailingSlash(cfg.ApiUrl.Trim());
                }
            }
            catch
            {
                // ignore malformed file and fallback
            }
        }

        return DefaultApiUrl;
    }

    static string EnsureTrailingSlash(string url)
        => url.EndsWith("/") ? url : url + "/";

    sealed class StickyCutieSettings
    {
        [JsonPropertyName("api_url")]
        public string? ApiUrl { get; set; }
    }
}

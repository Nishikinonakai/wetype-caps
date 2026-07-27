using System.Text.Json;
using System.Text.Json.Serialization;

namespace WeTypeCaps;

public sealed class AppConfig
{
    public int HoldThresholdMilliseconds { get; set; } = 450;

    public int StateRefreshMilliseconds { get; set; } = 120;

    public bool BypassFullscreen { get; set; } = true;

    public string[] FullscreenProcessNames { get; set; } = [];

    public string[] AlwaysBypassProcessNames { get; set; } = [];

    public string WeTypeTipClsid { get; set; } = "{86598FB9-66A2-463E-B9C2-AEB906D477AD}";

    public string WeTypeProfileGuid { get; set; } = "{607FDF85-FCC8-4DBD-A365-41296F980C9C}";

    public bool EnableDebugLog { get; set; }

    [JsonIgnore]
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static AppConfig LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            var created = new AppConfig();
            created.Validate();
            File.WriteAllText(path, JsonSerializer.Serialize(created, JsonOptions));
            return created;
        }

        var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("配置内容为空。");
        config.Validate();
        return config;
    }

    public void Validate()
    {
        HoldThresholdMilliseconds = Math.Clamp(HoldThresholdMilliseconds, 200, 2000);
        StateRefreshMilliseconds = Math.Clamp(StateRefreshMilliseconds, 50, 2000);
        FullscreenProcessNames ??= [];
        AlwaysBypassProcessNames ??= [];

        if (!Guid.TryParse(WeTypeTipClsid, out _))
        {
            throw new InvalidDataException("WeTypeTipClsid 不是有效 GUID。");
        }

        if (!Guid.TryParse(WeTypeProfileGuid, out _))
        {
            throw new InvalidDataException("WeTypeProfileGuid 不是有效 GUID。");
        }
    }
}


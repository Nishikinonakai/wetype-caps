using System.Text.Json;
using System.Text.Json.Serialization;

namespace WeTypeCaps;

public sealed class AppConfig
{
    public List<ImeRule> ImeRules { get; set; } =
    [
        new ImeRule
        {
            Name = "微信输入法",
            Clsid = "{86598FB9-66A2-463E-B9C2-AEB906D477AD}",
            ProfileGuid = "{607FDF85-FCC8-4DBD-A365-41296F980C9C}"
        }
    ];
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
        ImeRules ??= [];
        foreach (var rule in ImeRules)
        {
            if (!Guid.TryParse(rule.Clsid, out _) || !Guid.TryParse(rule.ProfileGuid, out _))
                throw new InvalidDataException("输入法规则包含无效 GUID。");
            if (!Enum.IsDefined(rule.Shortcut))
                throw new InvalidDataException("输入法规则包含不支持的快捷键。");
        }

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

public enum ImeShortcut { CtrlSpace, Shift }

public sealed class ImeRule
{
    public string Name { get; set; } = "";
    public string Clsid { get; set; } = "";
    public string ProfileGuid { get; set; } = "";
    public ImeShortcut Shortcut { get; set; } = ImeShortcut.CtrlSpace;
    public bool Enabled { get; set; } = true;

    public bool Matches(ActiveProfile profile) => Enabled && profile.IsMatch(
        new TsfProfileIdentity(Guid.Parse(Clsid), Guid.Parse(ProfileGuid), "config"));
}

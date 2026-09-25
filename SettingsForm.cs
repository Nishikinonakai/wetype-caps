namespace WeTypeCaps;

internal sealed class SettingsForm : Form
{
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };
    private readonly NumericUpDown _hold = new() { Minimum = 200, Maximum = 2000, Increment = 50, Width = 90 };
    private readonly CheckBox _fullscreen = new() { Text = "全屏窗口中暂停映射", AutoSize = true };
    private readonly TextBox _bypass = new() { Width = 340 };
    private readonly CheckBox _startup = new() { Text = "登录 Windows 后自动启动", AutoSize = true };
    private readonly TextBox _search = new() { Dock = DockStyle.Fill, PlaceholderText = "搜索输入法名称或 GUID" };
    private readonly AppConfig _config;

    internal SettingsForm(AppConfig config, bool startup, ActiveProfile active)
    {
        _config = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(
            System.Text.Json.JsonSerializer.Serialize(config, AppConfig.JsonOptions), AppConfig.JsonOptions)!;
        Text = "Caps 输入法切换 · 设置";
        Width = 770; Height = 590; MinimumSize = new Size(650, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);

        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "启用", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "输入法", ReadOnly = true, FillWeight = 53 });
        _grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "Shortcut", HeaderText = "短按发送", FillWeight = 25,
            DataSource = new[] { "Ctrl+Space", "Shift" }
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "Profile GUID", ReadOnly = true, FillWeight = 40 });
        _grid.DataError += (_, e) => e.ThrowException = false;

        var installed = TsfProfileDetector.DiscoverInstalledImes().ToList();
        foreach (var rule in _config.ImeRules)
            if (!installed.Any(i => Same(i.Clsid, rule.Clsid) && Same(i.ProfileGuid, rule.ProfileGuid)))
                installed.Add(new InstalledIme(rule.Name + " (未在本机发现)", Guid.Parse(rule.Clsid), Guid.Parse(rule.ProfileGuid)));
        if (active.HResult == 0 && active.ProfileType == 1 &&
            !installed.Any(i => i.Clsid == active.Clsid && i.ProfileGuid == active.ProfileGuid))
            installed.Add(new InstalledIme("当前活动输入法", active.Clsid, active.ProfileGuid));

        foreach (var ime in installed.OrderBy(i => i.Name))
        {
            var rule = _config.ImeRules.FirstOrDefault(r => Same(ime.Clsid, r.Clsid) && Same(ime.ProfileGuid, r.ProfileGuid));
            int row = _grid.Rows.Add(rule?.Enabled ?? false, ime.Name,
                rule?.Shortcut == ImeShortcut.Shift ? "Shift" : "Ctrl+Space", ime.ProfileGuid.ToString("B"));
            _grid.Rows[row].Tag = ime;
        }

        var help = new Label
        {
            Dock = DockStyle.Top, Height = 52,
            Text = "勾选要使用 Caps 切换的输入法。短按发送所选快捷键，长按切换大写锁定。\n请先在对应输入法中确认该快捷键确实用于中英文切换。"
        };
        var searchBar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 34, ColumnCount = 2 };
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchBar.Controls.Add(new Label { Text = "搜索", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        searchBar.Controls.Add(_search, 1, 0);
        _search.TextChanged += (_, _) =>
        {
            _grid.CurrentCell = null;
            foreach (DataGridViewRow row in _grid.Rows)
                row.Visible = row.Cells[1].Value?.ToString()?.Contains(_search.Text, StringComparison.OrdinalIgnoreCase) == true ||
                    row.Cells[3].Value?.ToString()?.Contains(_search.Text, StringComparison.OrdinalIgnoreCase) == true;
        };
        var options = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 156, ColumnCount = 2, Padding = new Padding(12) };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        options.Controls.Add(new Label { Text = "长按阈值 (毫秒)", AutoSize = true }, 0, 0);
        options.Controls.Add(_hold, 1, 0);
        options.Controls.Add(_fullscreen, 1, 1);
        options.Controls.Add(new Label { Text = "始终绕过的进程", AutoSize = true }, 0, 2);
        options.Controls.Add(_bypass, 1, 2);
        options.Controls.Add(_startup, 1, 3);
        _hold.Value = _config.HoldThresholdMilliseconds;
        _fullscreen.Checked = _config.BypassFullscreen;
        _bypass.Text = string.Join(", ", _config.AlwaysBypassProcessNames);
        _startup.Checked = startup;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var save = new Button { Text = "保存", Width = 90, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "取消", Width = 90, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(save); buttons.Controls.Add(cancel);
        AcceptButton = save; CancelButton = cancel;
        Controls.Add(_grid); Controls.Add(searchBar); Controls.Add(help); Controls.Add(options); Controls.Add(buttons);
    }

    internal AppConfig GetConfig()
    {
        _grid.EndEdit();
        _config.ImeRules = _grid.Rows.Cast<DataGridViewRow>().Select(row =>
        {
            var ime = (InstalledIme)row.Tag!;
            return new ImeRule
            {
                Name = ime.Name, Clsid = ime.Clsid.ToString("B"),
                ProfileGuid = ime.ProfileGuid.ToString("B"),
                Enabled = row.Cells[0].Value is true,
                Shortcut = Equals(row.Cells[2].Value, "Shift") ? ImeShortcut.Shift : ImeShortcut.CtrlSpace
            };
        }).Where(r => r.Enabled).ToList();
        _config.HoldThresholdMilliseconds = (int)_hold.Value;
        _config.BypassFullscreen = _fullscreen.Checked;
        _config.AlwaysBypassProcessNames = _bypass.Text.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _config.Validate();
        return _config;
    }

    internal bool Startup => _startup.Checked;
    private static bool Same(Guid guid, string text) => Guid.TryParse(text, out var other) && guid == other;
}

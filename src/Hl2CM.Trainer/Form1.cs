using System.Diagnostics;
using Hl2CM.Trainer.Game;

namespace Hl2CM.Trainer;

public partial class Form1 : Form
{
    private Hl2Trainer? _trainer;

    private readonly ComboBox _processCombo = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _refreshProcessesButton = new() { Text = "Atualizar", Width = 80 };
    private readonly Button _connectButton = new() { Text = "Conectar", Width = 90 };
    private readonly Label _statusLabel = new() { Text = "Desconectado", AutoSize = true, ForeColor = Color.Firebrick };

    private readonly List<Process> _candidateProcesses = new();

    private readonly CheckBox _infAmmoPrimary = new() { Text = "Munição infinita (arma primária)", AutoSize = true };
    private readonly CheckBox _infAmmoSecondary = new() { Text = "Munição infinita (arma secundária)", AutoSize = true };
    private readonly CheckBox _infHealth = new() { Text = "Vida infinita", AutoSize = true };
    private readonly CheckBox _infSuitArmor = new() { Text = "Armadura (Suit) infinita", AutoSize = true };

    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 300 };

    private readonly List<(TextBox Box, Func<int> Read)> _stats = new();

    public Form1()
    {
        InitializeComponent();
        BuildUi();
    }

    private void BuildUi()
    {
        Text = "HL2 Cheat Menu (estudo/offline)";
        ClientSize = new Size(560, 620);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8) };
        top.Controls.Add(new Label { Text = "Processo:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) });
        top.Controls.Add(_processCombo);
        top.Controls.Add(_refreshProcessesButton);
        top.Controls.Add(_connectButton);
        top.Controls.Add(new Label { Text = "  " });
        top.Controls.Add(_statusLabel);
        _statusLabel.Padding = new Padding(0, 8, 0, 0);
        Controls.Add(top);

        var cheatsGroup = new GroupBox { Text = "Cheats (patch de código no server.dll)", Dock = DockStyle.Top, Height = 150, Padding = new Padding(10) };
        var cheatsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        cheatsPanel.Controls.Add(_infHealth);
        cheatsPanel.Controls.Add(_infSuitArmor);
        cheatsPanel.Controls.Add(_infAmmoPrimary);
        cheatsPanel.Controls.Add(_infAmmoSecondary);
        cheatsGroup.Controls.Add(cheatsPanel);
        Controls.Add(cheatsGroup);
        cheatsGroup.BringToFront();

        var statsGroup = new GroupBox { Text = "Stats do jogador (lê a cada 300ms; 'Definir' escreve o valor)", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var statsPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoScroll = true };
        statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        statsGroup.Controls.Add(statsPanel);
        Controls.Add(statsGroup);
        statsGroup.BringToFront();
        top.BringToFront();

        AddIntStat(statsPanel, "Vida atual", () => _trainer!.GetHealthCurrent(), v => _trainer!.SetHealthCurrent(v));
        AddIntStat(statsPanel, "Suit (armadura) atual", () => _trainer!.GetSuitCurrent(), v => _trainer!.SetSuitCurrent(v));
        AddIntStat(statsPanel, "Munição no pente atual", () => _trainer!.GetAmmoClipCurrent(), v => _trainer!.SetAmmoClipCurrent(v));
        AddIntStat(statsPanel, "Munição - Pistola", () => _trainer!.GetAmmo(PlayerOffsets.AmmoPistol), v => _trainer!.SetAmmo(PlayerOffsets.AmmoPistol, v));
        AddIntStat(statsPanel, "Munição - SMG1", () => _trainer!.GetAmmo(PlayerOffsets.AmmoSmg1), v => _trainer!.SetAmmo(PlayerOffsets.AmmoSmg1, v));
        AddIntStat(statsPanel, "Munição - SMG1 (granada)", () => _trainer!.GetAmmo(PlayerOffsets.AmmoSmg1Alt), v => _trainer!.SetAmmo(PlayerOffsets.AmmoSmg1Alt, v));
        AddIntStat(statsPanel, "Munição - Magnum", () => _trainer!.GetAmmo(PlayerOffsets.AmmoMagnum), v => _trainer!.SetAmmo(PlayerOffsets.AmmoMagnum, v));
        AddIntStat(statsPanel, "Munição - Besta (Crossbow)", () => _trainer!.GetAmmo(PlayerOffsets.AmmoCrossbow), v => _trainer!.SetAmmo(PlayerOffsets.AmmoCrossbow, v));
        AddIntStat(statsPanel, "Munição - Escopeta", () => _trainer!.GetAmmo(PlayerOffsets.AmmoShotgun), v => _trainer!.SetAmmo(PlayerOffsets.AmmoShotgun, v));
        AddIntStat(statsPanel, "Munição - RPG", () => _trainer!.GetAmmo(PlayerOffsets.AmmoRpg), v => _trainer!.SetAmmo(PlayerOffsets.AmmoRpg, v));
        AddIntStat(statsPanel, "Munição - Granadas", () => _trainer!.GetAmmo(PlayerOffsets.AmmoGrenades), v => _trainer!.SetAmmo(PlayerOffsets.AmmoGrenades, v));
        AddIntStat(statsPanel, "Munição - Fuzil de Impulso", () => _trainer!.GetAmmo(PlayerOffsets.AmmoImpulseRifle), v => _trainer!.SetAmmo(PlayerOffsets.AmmoImpulseRifle, v));

        _refreshProcessesButton.Click += (_, _) => RefreshProcessList();
        _connectButton.Click += OnConnectClick;
        _infHealth.CheckedChanged += (_, _) => ToggleCheat(_infHealth, v => _trainer!.InfiniteHealth = v);
        _infSuitArmor.CheckedChanged += (_, _) => ToggleCheat(_infSuitArmor, v => _trainer!.InfiniteSuitArmor = v);
        _infAmmoPrimary.CheckedChanged += (_, _) => ToggleCheat(_infAmmoPrimary, v => _trainer!.InfiniteAmmoPrimary = v);
        _infAmmoSecondary.CheckedChanged += (_, _) => ToggleCheat(_infAmmoSecondary, v => _trainer!.InfiniteAmmoSecondary = v);

        _refreshTimer.Tick += (_, _) => RefreshStats();
        SetCheatsEnabled(false);
        RefreshProcessList();
    }

    /// <summary>Lists running processes that have a visible window (games/apps), so the user picks
    /// one instead of typing a name. Prefers pre-selecting anything named "hl2".</summary>
    private void RefreshProcessList()
    {
        foreach (var p in _candidateProcesses) p.Dispose();
        _candidateProcesses.Clear();
        _processCombo.Items.Clear();

        var processes = Process.GetProcesses()
            .Where(p =>
            {
                try { return p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle); }
                catch { return false; }
            })
            .OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int preselect = -1;
        foreach (var p in processes)
        {
            _candidateProcesses.Add(p);
            _processCombo.Items.Add($"{p.ProcessName} (PID {p.Id}) — {p.MainWindowTitle}");
            if (preselect == -1 && p.ProcessName.Equals("hl2", StringComparison.OrdinalIgnoreCase))
                preselect = _candidateProcesses.Count - 1;
        }

        if (_processCombo.Items.Count > 0)
            _processCombo.SelectedIndex = preselect >= 0 ? preselect : 0;
    }

    private void AddIntStat(TableLayoutPanel panel, string label, Func<int> read, Action<int> write)
    {
        int row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) }, 0, row);

        var box = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(box, 1, row);

        var button = new Button { Text = "Definir", Dock = DockStyle.Fill };
        button.Click += (_, _) =>
        {
            if (_trainer is null || !_trainer.HasValidPlayer) return;
            if (int.TryParse(box.Text, out var value))
            {
                try { write(value); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Erro ao escrever"); }
            }
        };
        panel.Controls.Add(button, 2, row);

        _stats.Add((box, read));
    }

    private void ToggleCheat(CheckBox box, Action<bool> apply)
    {
        if (_trainer is null) return;
        try
        {
            apply(box.Checked);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Falha ao aplicar cheat");
            box.Checked = false;
        }
    }

    private void OnConnectClick(object? sender, EventArgs e)
    {
        _trainer?.Dispose();
        _trainer = null;
        SetCheatsEnabled(false);
        _refreshTimer.Stop();

        var index = _processCombo.SelectedIndex;
        if (index < 0 || index >= _candidateProcesses.Count)
        {
            _statusLabel.Text = "Selecione um processo na lista";
            _statusLabel.ForeColor = Color.Firebrick;
            return;
        }

        try
        {
            _trainer = Hl2Trainer.TryAttach(_candidateProcesses[index]);
            if (_trainer is null)
            {
                _statusLabel.Text = "Não foi possível abrir o processo (tente rodar como administrador)";
                _statusLabel.ForeColor = Color.Firebrick;
                return;
            }

            _trainer.ActivatePointers();
            _statusLabel.Text = $"Conectado: {_trainer.ProcessDescription}";
            _statusLabel.ForeColor = Color.DarkGreen;
            SetCheatsEnabled(true);
            _refreshTimer.Start();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Falha ao conectar (ver mensagem)";
            _statusLabel.ForeColor = Color.Firebrick;
            _trainer?.Dispose();
            _trainer = null;
            MessageBox.Show(this, ex.Message, "Erro ao conectar / instalar hooks");
        }
    }

    private void SetCheatsEnabled(bool enabled)
    {
        _infHealth.Enabled = enabled;
        _infSuitArmor.Enabled = enabled;
        _infAmmoPrimary.Enabled = enabled;
        _infAmmoSecondary.Enabled = enabled;
    }

    private void RefreshStats()
    {
        if (_trainer is null || !_trainer.HasValidPlayer)
            return;

        try { _trainer.MaintainCheats(); }
        catch { /* transient read/write failure during level transitions */ }

        foreach (var (box, read) in _stats)
        {
            if (box.Focused) continue; // don't fight the user while they're typing
            try { box.Text = read().ToString(); }
            catch { /* transient read failure (e.g. level transition) — ignore this tick */ }
        }
    }
}

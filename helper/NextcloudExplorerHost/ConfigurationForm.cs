using System.ComponentModel;
using System.Diagnostics;

namespace NextcloudExplorerOpen;

internal sealed class ConfigurationForm : Form
{
    private readonly TextBox _serverUrl = new();
    private readonly TextBox _username = new();
    private readonly TextBox _password = new();
    private readonly CheckBox _showPassword = new();
    private readonly Label _serviceStatus = new();
    private readonly Label _status = new();
    private readonly Button _saveButton = new();
    private readonly Button _repairButton = new();
    private AppConfiguration? _existingConfiguration;

    public ConfigurationForm()
    {
        Text = "Nextcloud Explorer Open - Einrichtung";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(620, 430);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        BuildLayout();
        LoadExistingConfiguration();
        UpdateServiceStatus();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 20, 24, 20),
            ColumnCount = 1,
            RowCount = 12
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Text = "Windows-Verbindung einrichten"
        };

        var description = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            ForeColor = SystemColors.GrayText,
            Text = "Das App-Passwort wird ausschliesslich im Windows-Anmeldeinformationsspeicher abgelegt."
        };

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(description, 0, 1);

        AddField(layout, 2, "Nextcloud-Adresse", _serverUrl, "https://cloud.example.com/");
        AddField(layout, 4, "Benutzername", _username, "USERNAME");

        var passwordLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 5),
            Text = "Nextcloud-App-Passwort"
        };
        _password.Dock = DockStyle.Top;
        _password.UseSystemPasswordChar = true;
        _password.Margin = new Padding(0, 0, 0, 0);
        _password.AccessibleName = "Nextcloud-App-Passwort";
        layout.Controls.Add(passwordLabel, 0, 6);
        layout.Controls.Add(_password, 0, 7);

        var passwordActions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 5, 0, 0),
            WrapContents = false
        };

        _showPassword.AutoSize = true;
        _showPassword.Text = "Anzeigen";
        _showPassword.CheckedChanged += (_, _) => _password.UseSystemPasswordChar = !_showPassword.Checked;

        var passwordLink = new LinkLabel
        {
            AutoSize = true,
            Margin = new Padding(20, 3, 0, 0),
            Text = "App-Passwort in Nextcloud erstellen"
        };
        passwordLink.LinkClicked += (_, _) => OpenNextcloudSecurityPage();

        passwordActions.Controls.Add(_showPassword);
        passwordActions.Controls.Add(passwordLink);
        layout.Controls.Add(passwordActions, 0, 8);

        var serviceRow = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 14, 0, 0),
            WrapContents = false
        };

        _serviceStatus.AutoSize = true;
        _serviceStatus.Margin = new Padding(0, 7, 12, 0);

        _repairButton.AutoSize = true;
        _repairButton.Text = "Einrichtung reparieren";
        _repairButton.Click += RepairButtonClick;

        serviceRow.Controls.Add(_serviceStatus);
        serviceRow.Controls.Add(_repairButton);
        layout.Controls.Add(serviceRow, 0, 9);

        _status.AutoSize = true;
        _status.MaximumSize = new Size(560, 0);
        _status.Margin = new Padding(0, 12, 0, 0);
        layout.Controls.Add(_status, 0, 10);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        _saveButton.AutoSize = true;
        _saveButton.Text = "Speichern und testen";
        _saveButton.Click += SaveButtonClick;

        var closeButton = new Button
        {
            AutoSize = true,
            Text = "Schliessen",
            DialogResult = DialogResult.Cancel
        };

        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(closeButton);
        layout.Controls.Add(buttons, 0, 11);

        AcceptButton = _saveButton;
        CancelButton = closeButton;
        Controls.Add(layout);
    }

    private static void AddField(
        TableLayoutPanel layout,
        int labelRow,
        string labelText,
        TextBox input,
        string placeholder)
    {
        var label = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 5),
            Text = labelText
        };

        input.Dock = DockStyle.Top;
        input.PlaceholderText = placeholder;
        input.Margin = new Padding(0);
        input.AccessibleName = labelText;

        layout.Controls.Add(label, 0, labelRow);
        layout.Controls.Add(input, 0, labelRow + 1);
    }

    private void LoadExistingConfiguration()
    {
        if (!ConfigurationStore.TryLoad(out _existingConfiguration, out _) || _existingConfiguration is null)
        {
            return;
        }

        _serverUrl.Text = _existingConfiguration.ServerBaseUrl;
        _username.Text = _existingConfiguration.Username;

        if (CredentialStore.Exists(_existingConfiguration.CredentialTarget))
        {
            _password.PlaceholderText = "Gespeichertes App-Passwort beibehalten";
        }
    }

    private async void SaveButtonClick(object? sender, EventArgs eventArgs)
    {
        SetBusy(true);
        SetStatus("Verbindung wird geprueft...", isError: false);

        string password = _password.Text;
        try
        {
            AppConfiguration configuration = AppConfiguration.Create(_serverUrl.Text, _username.Text);
            bool hasNewPassword = !string.IsNullOrWhiteSpace(password);
            bool canReuseStoredPassword = _existingConfiguration is not null &&
                string.Equals(
                    _existingConfiguration.CredentialTarget,
                    configuration.CredentialTarget,
                    StringComparison.Ordinal) &&
                CredentialStore.Exists(configuration.CredentialTarget);

            if (!hasNewPassword && !canReuseStoredPassword)
            {
                throw new AppException("credential_missing", "Bitte ein Nextcloud-App-Passwort eintragen.");
            }

            await EnsureWebClientReadyAsync();

            if (hasNewPassword)
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                await NextcloudCredentialVerifier.VerifyAsync(configuration, password, cancellation.Token);
                CredentialStore.Write(configuration.CredentialTarget, configuration.Username, password);
            }

            ConfigurationStore.Save(configuration);
            await Task.Run(() => WebDavConnection.VerifyRoot(configuration));

            if (_existingConfiguration is not null &&
                !string.Equals(
                    _existingConfiguration.CredentialTarget,
                    configuration.CredentialTarget,
                    StringComparison.Ordinal))
            {
                CredentialStore.Delete(_existingConfiguration.CredentialTarget);
            }

            _existingConfiguration = configuration;
            SetStatus("Einrichtung erfolgreich abgeschlossen.", isError: false);
            MessageBox.Show(
                this,
                "Die sichere Windows-Verbindung zu Nextcloud funktioniert.",
                "Nextcloud Explorer Open",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Der Nextcloud-Verbindungstest hat zu lange gedauert.", isError: true);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            SetStatus("Die Windows-Sicherheitsabfrage wurde abgebrochen.", isError: true);
        }
        catch (Exception ex) when (ex is AppException or Win32Exception or HttpRequestException or IOException)
        {
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            _password.Clear();
            password = string.Empty;
            _showPassword.Checked = false;
            SetBusy(false);
            UpdateServiceStatus();
        }
    }

    private async Task EnsureWebClientReadyAsync()
    {
        if (WebClientService.IsRunning() && WebClientService.IsAutomatic())
        {
            return;
        }

        DialogResult answer = MessageBox.Show(
            this,
            "Windows muss den WebClient-Dienst einmalig aktivieren. Danach startet er automatisch. Fortfahren?",
            "Windows-WebClient einrichten",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);

        if (answer != DialogResult.Yes)
        {
            throw new AppException("service_repair_cancelled", "Ohne den Windows-WebClient kann Explorer WebDAV nicht oeffnen.");
        }

        await Task.Run(WebClientService.Repair);
    }

    private async void RepairButtonClick(object? sender, EventArgs eventArgs)
    {
        SetBusy(true);
        try
        {
            await Task.Run(WebClientService.Repair);
            SetStatus("Der Windows-WebClient wurde repariert.", isError: false);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            SetStatus("Die Windows-Sicherheitsabfrage wurde abgebrochen.", isError: true);
        }
        catch (Exception ex) when (ex is AppException or Win32Exception)
        {
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            SetBusy(false);
            UpdateServiceStatus();
        }
    }

    private void OpenNextcloudSecurityPage()
    {
        try
        {
            string serverBase = NextcloudAddress.NormalizeServerBase(_serverUrl.Text);
            Uri securityPage = new(new Uri(serverBase), "index.php/settings/user/security");
            _ = Process.Start(new ProcessStartInfo
            {
                FileName = securityPage.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is AppException or Win32Exception)
        {
            SetStatus(ex.Message, isError: true);
        }
    }

    private void UpdateServiceStatus()
    {
        bool running = WebClientService.IsRunning();
        bool automatic = WebClientService.IsAutomatic();
        _serviceStatus.Text = running && automatic
            ? "Windows-WebClient: bereit"
            : "Windows-WebClient: Einrichtung erforderlich";
        _serviceStatus.ForeColor = running && automatic ? Color.DarkGreen : Color.DarkGoldenrod;
        _repairButton.Visible = !running || !automatic;
    }

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        _saveButton.Enabled = !busy;
        _repairButton.Enabled = !busy;
        _serverUrl.Enabled = !busy;
        _username.Enabled = !busy;
        _password.Enabled = !busy;
    }

    private void SetStatus(string message, bool isError)
    {
        _status.Text = message;
        _status.ForeColor = isError ? Color.Firebrick : Color.DarkGreen;
    }
}

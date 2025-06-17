using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using static song_box.Utils;

namespace song_box
{
    public partial class Form1 : Form
    {
        private static string AppName => ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute), false))?.Title ?? "Unknown Title";
        private static readonly string programVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        private static readonly string programFullName = $"oklookat/{AppName} {programVersion}";

        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private TextBox outputBox;
        private Button exitButton;
        private CheckBox autoStartCheckbox;
        private const int MaxLines = 1000;
        private readonly Queue<string> lineQueue = new Queue<string>();
        private ILogger logger;

        private Config.AppConfig appConfig;
        private SingBoxConfig singBoxConfig;
        private SingBox singBox;
        private AppUpdater appUpdater;



        public Form1()
        {
            // Base init.
            InitializeComponent();
            InitializeLayout();
        }


        private void InitializeLayout()
        {
            // Set main form properties
            Text = AppName;
            Size = new Size(800, 500);
            FormClosing += Form1_FormClosing;
            Resize += Form1_Resize;

            // Load and set the application icon
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("song_box.icon.ico"))
            {
                Icon = new Icon(stream);
            }

            // Initialize tray menu
            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Exit", OnExit);

            // Initialize tray icon
            trayIcon = new NotifyIcon
            {
                Text = AppName,
                Icon = SystemIcons.Application, // Temporary icon before loading real one
                ContextMenu = trayMenu,
                Visible = true
            };

            // Set actual tray icon from resources
            using (Stream stream = assembly.GetManifestResourceStream("song_box.icon.ico"))
            {
                trayIcon.Icon = new Icon(stream);
            }

            // Handle tray icon click
            trayIcon.Click += TrayIcon_Click;

            // Auto-start checkbox
            autoStartCheckbox = new CheckBox
            {
                Text = "Запуск с Windows",
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = true
            };
            autoStartCheckbox.CheckedChanged += AutoStartCheckbox_CheckedChanged;
            autoStartCheckbox.Checked = AutoRun.Exists(AppName);
            autoStartCheckbox.TextAlign = ContentAlignment.MiddleLeft;
            autoStartCheckbox.Dock = DockStyle.Fill;
            var checkboxContainer = new Panel
            {
                AutoSize = true,
                Width = 200,
                Height = autoStartCheckbox.Height,
            };
            checkboxContainer.Controls.Add(autoStartCheckbox);
            checkboxContainer.Padding = new Padding(0);
            checkboxContainer.Margin = new Padding(0);
            checkboxContainer.Anchor = AnchorStyles.None;

            // Buttons
            var updateButton = new Button
            {
                Text = "Обновить все",
                AutoSize = true
            };
            updateButton.Click += OnUpdateButton;

            exitButton = new Button
            {
                Text = "Выход",
                AutoSize = true
            };
            exitButton.Click += OnExit;

            // Output log box
            outputBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                BackColor = Color.Black,
                ForeColor = Color.LightGreen
            };

            // Layout setup
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Panel for buttons and checkbox
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Padding = new Padding(10)
            };
            buttonPanel.Controls.Add(updateButton);
            buttonPanel.Controls.Add(exitButton);
            buttonPanel.Controls.Add(checkboxContainer);

            // Add panels to main layout
            layout.Controls.Add(buttonPanel, 0, 0);
            layout.Controls.Add(outputBox, 0, 1);

            // Add layout to form
            Controls.Add(layout);
        }

        private void TrayIcon_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
        }

        private void AddLineToOutput(string line)
        {
            if (outputBox.InvokeRequired)
            {
                outputBox.Invoke(new Action(() => AddLineToOutput(line)));
                return;
            }

            lineQueue.Enqueue(line);
            if (lineQueue.Count > MaxLines)
                lineQueue.Dequeue();

            outputBox.Lines = lineQueue.ToArray();
            outputBox.SelectionStart = outputBox.Text.Length;
            outputBox.ScrollToCaret();
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
                Hide();
        }

        private void OnUpdateButton(object sender, EventArgs e)
        {
            appUpdater.CheckUpdateAndUpdate();
            singBoxConfig.UpdateConfig();
        }

        private void OnExit(object sender, EventArgs e)
        {
            singBox?.Dispose();

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            Close();
            Application.Exit();
            Environment.Exit(0);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            logger = new TextBoxLogger(outputBox);
            logger.Info($"==== {programFullName} ====");

            try
            {
                // Config.
                appConfig = Config.AppConfig.Read();

                // Updates.
                appUpdater = new AppUpdater(logger, exitButton.PerformClick, appConfig.AppUpdater);

                // sing-box config
                var appConfigSingBoxConfig = appConfig.SingBoxConfig;
                singBoxConfig = new SingBoxConfig(logger, appConfigSingBoxConfig);

                // sing-box
                var appConfigSingBox = appConfig.SingBox;
                singBox = new SingBox(logger, appConfigSingBox, SingBoxConfig.configPath);
                singBox.Start();
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }
        }

        public class TextBoxLogger : ILogger
        {
            private readonly TextBox _outputBox;
            private readonly int maxLines = 1000;
            private readonly Queue<string> _lineQueue = new Queue<string>();

            public TextBoxLogger(TextBox outputBox)
            {
                _outputBox = outputBox;
            }

            public void Debug(string message) => WriteLine("DEBUG", message);
            public void Info(string message) => WriteLine("INFO", message);
            public void Warn(string message) => WriteLine("WARN", message);
            public void Error(string message) => WriteLine("ERROR", message);

            private void WriteLine(string level, string message)
            {
                string line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";

                if (_outputBox.InvokeRequired)
                {
                    _outputBox.Invoke(new Action(() => AppendLineToBox(line)));
                }
                else
                {
                    AppendLineToBox(line);
                }
            }

            private void AppendLineToBox(string line)
            {
                _lineQueue.Enqueue(line);
                if (_lineQueue.Count > maxLines)
                    _lineQueue.Dequeue();

                _outputBox.Lines = _lineQueue.ToArray();
                _outputBox.SelectionStart = _outputBox.Text.Length;
                _outputBox.ScrollToCaret();
            }
        }


        private void AutoStartCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (autoStartCheckbox.Checked)
            {
                AutoRun.Add(AppName, Application.ExecutablePath);
            }
            else
            {
                AutoRun.Remove(AppName);
            }
        }
    }
}

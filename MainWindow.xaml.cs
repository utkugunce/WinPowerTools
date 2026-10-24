using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinPowerTools.Services;
using Forms = System.Windows.Forms;

using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = System.Windows.MessageBox;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using DataFormats = System.Windows.DataFormats;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace WinPowerTools
{
    public partial class MainWindow : Window
    {
        private const int HOTKEY_ID = 9001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_SPACE = 0x20;
        private const int WM_HOTKEY = 0x0312;

        private Forms.NotifyIcon? _notifyIcon;
        private readonly DispatcherTimer _hogTimer = new DispatcherTimer();
        private readonly NetworkDiskHogService _hogService = new NetworkDiskHogService();
        private List<JunkCategoryModel> _scannedJunk = new();

        public MainWindow()
        {
            InitializeComponent();
            SetupSystemTray();

            _hogTimer.Interval = TimeSpan.FromSeconds(1.5);
            _hogTimer.Tick += (s, e) => UpdateHogData();

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private static SolidColorBrush Brush(string hex)
        {
            return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Küresel Ctrl + Space Kısayolunu Kaydet
            var helper = new WindowInteropHelper(this);
            WindowPinService.RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_CONTROL, VK_SPACE);

            var source = HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(HwndHook);

            UpdateSleepStatus();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            WindowPinService.UnregisterHotKey(helper.Handle, HOTKEY_ID);
            _notifyIcon?.Dispose();
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                var (isPinned, title) = WindowPinService.ToggleForegroundWindowPin();
                string status = isPinned ? "📌 En Üste Sabitlendi" : "Serbest Bırakıldı";
                _notifyIcon?.ShowBalloonTip(1500, "WinPowerTools Pin", $"[{status}]: {title}", Forms.ToolTipIcon.Info);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void SetupSystemTray()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Text = "WinPowerTools Suite",
                Visible = true
            };

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                _notifyIcon.Icon = File.Exists(iconPath) ? new System.Drawing.Icon(iconPath) : System.Drawing.SystemIcons.Application;
            }
            catch
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add(new Forms.ToolStripMenuItem("WinPowerTools'u Aç", null, (s, e) => ShowWindow()));
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(new Forms.ToolStripMenuItem("Çıkış", null, (s, e) => WpfApplication.Current.Shutdown()));

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += (s, e) => ShowWindow();
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        // ================= NAVİGASYON SEKMELERİ =================
        private void HideAllPanels()
        {
            PanelUnlocker.Visibility = Visibility.Collapsed;
            PanelCleaner.Visibility = Visibility.Collapsed;
            PanelHog.Visibility = Visibility.Collapsed;
            PanelSleep.Visibility = Visibility.Collapsed;
            PanelPin.Visibility = Visibility.Collapsed;

            NavUnlocker.Foreground = Brush("#94A3B8");
            NavCleaner.Foreground = Brush("#94A3B8");
            NavHog.Foreground = Brush("#94A3B8");
            NavSleep.Foreground = Brush("#94A3B8");
            NavPin.Foreground = Brush("#94A3B8");

            _hogTimer.Stop();
        }

        private void NavUnlocker_Click(object sender, RoutedEventArgs e)
        {
            HideAllPanels();
            PanelUnlocker.Visibility = Visibility.Visible;
            NavUnlocker.Foreground = Brush("#38BDF8");
        }

        private void NavCleaner_Click(object sender, RoutedEventArgs e)
        {
            HideAllPanels();
            PanelCleaner.Visibility = Visibility.Visible;
            NavCleaner.Foreground = Brush("#38BDF8");
        }

        private void NavHog_Click(object sender, RoutedEventArgs e)
        {
            HideAllPanels();
            PanelHog.Visibility = Visibility.Visible;
            NavHog.Foreground = Brush("#38BDF8");
            UpdateHogData();
            _hogTimer.Start();
        }

        private void NavSleep_Click(object sender, RoutedEventArgs e)
        {
            HideAllPanels();
            PanelSleep.Visibility = Visibility.Visible;
            NavSleep.Foreground = Brush("#38BDF8");
            UpdateSleepStatus();
        }

        private void NavPin_Click(object sender, RoutedEventArgs e)
        {
            HideAllPanels();
            PanelPin.Visibility = Visibility.Visible;
            NavPin.Foreground = Brush("#38BDF8");
        }

        // ================= 1. DOSYA KİLİDİ ÇÖZÜCÜ =================
        private void FileDropArea_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void FileDropArea_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    InspectLockedFile(files[0]);
                }
            }
        }

        private void FileDropArea_Click(object sender, MouseButtonEventArgs e)
        {
            var ofd = new OpenFileDialog { Title = "Kilitli veya Silinmeyen Dosyayı Seçin" };
            if (ofd.ShowDialog() == true)
            {
                InspectLockedFile(ofd.FileName);
            }
        }

        private void InspectLockedFile(string path)
        {
            TxtSelectedFilePath.Text = path;
            var lockingProcs = FileUnlockerService.FindLockingProcesses(path);
            ListLockingProcs.ItemsSource = lockingProcs;

            if (lockingProcs.Count == 0)
            {
                TxtUnlockStatus.Text = "✅ Bu dosya hiçbir süreç tarafından kilitlenmemiş (Serbest).";
                TxtUnlockStatus.Foreground = Brush("#34D399");
            }
            else
            {
                TxtUnlockStatus.Text = $"⚠️ Dosya {lockingProcs.Count} adet aktif süreç tarafından kilitlenmiş!";
                TxtUnlockStatus.Foreground = Brush("#FBBF24");
            }
        }

        private void BtnKillProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is int pid)
            {
                bool success = FileUnlockerService.KillProcess(pid);
                if (success)
                {
                    WpfMessageBox.Show($"PID {pid} başarıyla sonlandırıldı! Dosya kilidi çözüldü.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (!string.IsNullOrEmpty(TxtSelectedFilePath.Text) && File.Exists(TxtSelectedFilePath.Text))
                    {
                        InspectLockedFile(TxtSelectedFilePath.Text);
                    }
                }
                else
                {
                    WpfMessageBox.Show($"PID {pid} sonlandırılamadı. Süreç zaten kapanmış veya yönetici izni gerekiyor olabilir.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // ================= 2. GİZLİ ÇÖP TEMİZLEYİCİ =================
        private async void BtnScanJunk_Click(object sender, RoutedEventArgs e)
        {
            TxtJunkStatus.Text = "Taranıyor, lütfen bekleyin...";
            var progress = new Progress<string>(msg => TxtJunkStatus.Text = msg);

            _scannedJunk = await JunkCleanerService.ScanJunkAsync(progress);
            ListJunkCategories.ItemsSource = null;
            ListJunkCategories.ItemsSource = _scannedJunk;

            long totalBytes = _scannedJunk.Sum(x => x.TotalBytes);
            TxtJunkStatus.Text = $"Tarama tamamlandı! Toplam {totalBytes / (1024.0 * 1024.0):F1} MB temizlenebilir veri bulundu.";
        }

        private async void BtnCleanSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _scannedJunk.Where(x => x.IsSelected).ToList();
            if (selected.Count == 0)
            {
                WpfMessageBox.Show("Lütfen temizlemek istediğiniz en az bir kategori seçin.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TxtJunkStatus.Text = "Temizleniyor...";
            long cleaned = await JunkCleanerService.CleanCategoriesAsync(selected);

            WpfMessageBox.Show($"Temizlik tamamlandı!\nToplam {cleaned / (1024.0 * 1024.0):F1} MB disk alanı kurtarıldı. 🎉", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            BtnScanJunk_Click(sender, e);
        }

        // ================= 3. TRAFİK VE DİSK BEKÇİSİ =================
        private void UpdateHogData()
        {
            try
            {
                var topHogs = _hogService.GetTopHogs();
                ListHogs.ItemsSource = topHogs;
            }
            catch { }
        }

        // ================= 4. PİL VE UYKU KONTROLÜ =================
        private void UpdateSleepStatus()
        {
            var status = SleepWatchdogService.GetStatus();
            TxtBatteryState.Text = status.BatteryStatusText;
            TxtBatteryPercent.Text = $"%{status.BatteryPercent}";
            TxtBatteryRemaining.Text = status.EstimatedRemainingTime;
            TxtBatteryState.Foreground = status.IsOnAcPower ? Brush("#34D399") : Brush("#FBBF24");
        }

        private void BtnForceSleep_Click(object sender, RoutedEventArgs e)
        {
            SleepWatchdogService.ForceSleep();
        }

        private void BtnForceHibernate_Click(object sender, RoutedEventArgs e)
        {
            SleepWatchdogService.ForceHibernate();
        }

        // ================= 5. PENCERE SABİTLEYİCİ =================
        private void BtnPinActiveWindow_Click(object sender, RoutedEventArgs e)
        {
            var (isPinned, title) = WindowPinService.ToggleForegroundWindowPin();
            TxtPinFeedback.Text = isPinned ? $"✅ Sabitlendi: {title}" : $"Serbest Bırakıldı: {title}";
        }

        // Pencere Kontrolleri
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Hide();
    }
}

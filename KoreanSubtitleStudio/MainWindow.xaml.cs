using KoreanSubtitleStudio.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;

namespace KoreanSubtitleStudio
{
    public partial class MainWindow : Window
    {
        private readonly List<string> _inputs = new List<string>();
        private readonly BackgroundWorker _worker = new BackgroundWorker();
        private string _recentFolder;

        private sealed class WorkRequest
        {
            public string[] Files;
            public int Shift;
            public bool Subfolder;
            public bool Overwrite;
        }

        private sealed class WorkReport
        {
            public int Success;
            public int Total;
            public readonly List<string> Failures = new List<string>();
        }

        public MainWindow()
        {
            InitializeComponent();
            _worker.WorkerReportsProgress = true;
            _worker.DoWork += ConvertFiles;
            _worker.ProgressChanged += ShowProgress;
            _worker.RunWorkerCompleted += FinishWork;
        }

        private void OnAddFiles(object sender, RoutedEventArgs e)
        {
            var picker = new Microsoft.Win32.OpenFileDialog
            {
                Title = "SRT 자막 선택", Filter = "SRT 자막 (*.srt)|*.srt", Multiselect = true
            };
            if (picker.ShowDialog() == true) AddInputs(picker.FileNames);
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            var dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (dropped != null) AddInputs(dropped.Where(IsSrt));
        }

        private static bool IsSrt(string path)
        {
            return File.Exists(path) && string.Equals(Path.GetExtension(path), ".srt", StringComparison.OrdinalIgnoreCase);
        }

        private void AddInputs(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                var absolute = Path.GetFullPath(file);
                if (!_inputs.Any(item => string.Equals(item, absolute, StringComparison.OrdinalIgnoreCase))) _inputs.Add(absolute);
            }
            RefreshList();
        }

        private void OnClearFiles(object sender, RoutedEventArgs e)
        {
            if (_worker.IsBusy) return;
            _inputs.Clear();
            WorkProgress.Value = 0;
            FolderButton.Visibility = Visibility.Collapsed;
            RefreshList();
        }

        private void RefreshList()
        {
            InputList.Items.Clear();
            foreach (var file in _inputs) InputList.Items.Add(Path.GetFileName(file));
            EmptyGuide.Visibility = _inputs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            CountLabel.Text = _inputs.Count + "개 파일";
            StateLabel.Text = _inputs.Count == 0 ? "변환할 파일을 추가해 주세요." : "변환 준비가 완료되었습니다.";
        }

        private void OnStart(object sender, RoutedEventArgs e)
        {
            if (_inputs.Count == 0)
            {
                MessageBox.Show("SRT 파일을 먼저 추가해 주세요.", "파일 없음", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            double seconds;
            if (!double.TryParse(ShiftInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) &&
                !double.TryParse(ShiftInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out seconds))
            {
                MessageBox.Show("시간 보정값을 숫자로 입력해 주세요. 예: -1.500", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                ShiftInput.Focus(); return;
            }
            if (Math.Abs(seconds) > 86400)
            {
                MessageBox.Show("시간 보정 범위는 ±24시간입니다.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning); return;
            }
            SetWorking(true);
            WorkProgress.Value = 0;
            FolderButton.Visibility = Visibility.Collapsed;
            _worker.RunWorkerAsync(new WorkRequest
            {
                Files = _inputs.ToArray(), Shift = (int)Math.Round(seconds * 1000),
                Subfolder = DestinationChoice.SelectedIndex == 1, Overwrite = OverwriteChoice.IsChecked == true
            });
        }

        private void ConvertFiles(object sender, DoWorkEventArgs e)
        {
            var request = (WorkRequest)e.Argument;
            var report = new WorkReport { Total = request.Files.Length };
            var converter = new SubtitleConversionService();
            for (var index = 0; index < request.Files.Length; index++)
            {
                var input = request.Files[index];
                try
                {
                    var parent = Path.GetDirectoryName(input);
                    var destination = request.Subfolder ? Path.Combine(parent, "VTT") : parent;
                    var output = Path.Combine(destination, Path.GetFileNameWithoutExtension(input) + ".vtt");
                    converter.Convert(input, output, request.Shift, request.Overwrite);
                    _recentFolder = destination;
                    report.Success++;
                }
                catch (Exception exception) { report.Failures.Add(Path.GetFileName(input) + " · " + exception.Message); }
                _worker.ReportProgress((index + 1) * 100 / request.Files.Length, Path.GetFileName(input));
            }
            e.Result = report;
        }

        private void ShowProgress(object sender, ProgressChangedEventArgs e)
        {
            WorkProgress.Value = e.ProgressPercentage;
            StateLabel.Text = "변환 중 · " + e.UserState + " · " + e.ProgressPercentage + "%";
        }

        private void FinishWork(object sender, RunWorkerCompletedEventArgs e)
        {
            SetWorking(false);
            if (e.Error != null)
            {
                StateLabel.Text = "예상하지 못한 오류가 발생했습니다.";
                MessageBox.Show(e.Error.Message, "변환 오류", MessageBoxButton.OK, MessageBoxImage.Error); return;
            }
            var report = (WorkReport)e.Result;
            StateLabel.Text = report.Failures.Count == 0
                ? "완료 · " + report.Success + "개 파일을 변환했습니다."
                : "완료 · 성공 " + report.Success + "개 / 실패 " + report.Failures.Count + "개";
            FolderButton.Visibility = report.Success > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (report.Failures.Count > 0)
                MessageBox.Show(string.Join("\n", report.Failures.ToArray()), "변환 결과", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void SetWorking(bool working)
        {
            StartButton.IsEnabled = !working; InputList.IsEnabled = !working;
            DestinationChoice.IsEnabled = !working; OverwriteChoice.IsEnabled = !working; ShiftInput.IsEnabled = !working;
        }

        private void OnOpenFolder(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_recentFolder) && Directory.Exists(_recentFolder)) Process.Start("explorer.exe", _recentFolder);
        }
    }
}

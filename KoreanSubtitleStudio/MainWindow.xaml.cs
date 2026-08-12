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
            public string CustomFolder;
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
            if (_inputs.Count > 0 && InputList.SelectedIndex < 0) InputList.SelectedIndex = 0;
        }

        private void OnInputSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (InputList.SelectedIndex >= 0) ShowPreview(false);
        }

        private void OnRefreshPreview(object sender, RoutedEventArgs e) { ShowPreview(true); }

        private void OnOpenVttInPreview(object sender, RoutedEventArgs e)
        {
            var picker = new Microsoft.Win32.OpenFileDialog
            {
                Title = "미리 볼 VTT 자막 선택",
                Filter = "WebVTT 자막 (*.vtt)|*.vtt"
            };
            if (picker.ShowDialog() != true) return;

            try
            {
                var bytes = File.ReadAllBytes(picker.FileName);
                string content;
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                    content = System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
                else
                {
                    try { content = new System.Text.UTF8Encoding(false, true).GetString(bytes); }
                    catch (System.Text.DecoderFallbackException) { content = System.Text.Encoding.GetEncoding(949).GetString(bytes); }
                }
                if (!content.TrimStart().StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("올바른 WebVTT 파일이 아닙니다.");

                PreviewTitle.Text = Path.GetFileName(picker.FileName) + " · 기존 VTT 파일";
                PreviewText.Text = content;
                PreviewText.ScrollToHome();
                WorkspaceTabs.SelectedIndex = 1;
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "VTT 열기 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShowPreview(bool switchToPreview)
        {
            var index = InputList.SelectedIndex;
            if (index < 0 || index >= _inputs.Count)
            {
                PreviewTitle.Text = "파일을 선택해 주세요";
                PreviewText.Text = "파일 목록에서 미리 볼 SRT를 선택해 주세요.";
                return;
            }

            int shift;
            if (!TryReadShift(out shift))
            {
                PreviewTitle.Text = Path.GetFileName(_inputs[index]);
                PreviewText.Text = "시간 보정값이 올바르지 않아 미리보기를 만들 수 없습니다.";
                if (switchToPreview) WorkspaceTabs.SelectedIndex = 1;
                return;
            }

            try
            {
                var preview = new SubtitleConversionService().CreatePreview(_inputs[index], shift);
                PreviewTitle.Text = Path.GetFileName(_inputs[index]) + " · " + preview.CueCount + "개 자막";
                PreviewText.Text = preview.Content;
                PreviewText.ScrollToHome();
                if (switchToPreview) WorkspaceTabs.SelectedIndex = 1;
            }
            catch (Exception exception)
            {
                PreviewTitle.Text = Path.GetFileName(_inputs[index]);
                PreviewText.Text = "미리보기 오류\r\n\r\n" + exception.Message;
                if (switchToPreview) WorkspaceTabs.SelectedIndex = 1;
            }
        }

        private bool TryReadShift(out int milliseconds)
        {
            double seconds;
            var valid = double.TryParse(ShiftInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) ||
                        double.TryParse(ShiftInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out seconds);
            if (!valid || Math.Abs(seconds) > 86400) { milliseconds = 0; return false; }
            milliseconds = (int)Math.Round(seconds * 1000);
            return true;
        }

        private void OnDestinationChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CustomFolderPanel != null)
                CustomFolderPanel.Visibility = DestinationChoice.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnBrowseFolder(object sender, RoutedEventArgs e)
        {
            var picker = new ExplorerFolderPicker
            {
                Title = "변환된 VTT 파일을 저장할 폴더 선택",
                InitialFolder = Directory.Exists(CustomFolderInput.Text) ? CustomFolderInput.Text : null
            };
            var selectedFolder = picker.ShowDialog(this);
            if (!string.IsNullOrWhiteSpace(selectedFolder)) CustomFolderInput.Text = selectedFolder;
        }

        private void OnStart(object sender, RoutedEventArgs e)
        {
            if (_inputs.Count == 0)
            {
                MessageBox.Show("SRT 파일을 먼저 추가해 주세요.", "파일 없음", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            int shift;
            if (!TryReadShift(out shift))
            {
                MessageBox.Show("시간 보정값을 숫자로 입력해 주세요. 예: -1.500", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                ShiftInput.Focus(); return;
            }
            if (DestinationChoice.SelectedIndex == 2 && string.IsNullOrWhiteSpace(CustomFolderInput.Text))
            {
                MessageBox.Show("VTT 파일을 저장할 폴더를 선택해 주세요.", "저장 폴더 없음", MessageBoxButton.OK, MessageBoxImage.Information);
                OnBrowseFolder(sender, e);
                if (string.IsNullOrWhiteSpace(CustomFolderInput.Text)) return;
            }
            SetWorking(true);
            WorkProgress.Value = 0;
            FolderButton.Visibility = Visibility.Collapsed;
            _worker.RunWorkerAsync(new WorkRequest
            {
                Files = _inputs.ToArray(), Shift = shift,
                Subfolder = DestinationChoice.SelectedIndex == 1, Overwrite = OverwriteChoice.IsChecked == true,
                CustomFolder = DestinationChoice.SelectedIndex == 2 ? CustomFolderInput.Text : null
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
                    var destination = !string.IsNullOrEmpty(request.CustomFolder)
                        ? request.CustomFolder
                        : request.Subfolder ? Path.Combine(parent, "VTT") : parent;
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
            CustomFolderInput.IsEnabled = !working;
        }

        private void OnOpenFolder(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_recentFolder) && Directory.Exists(_recentFolder)) Process.Start("explorer.exe", _recentFolder);
        }
    }
}

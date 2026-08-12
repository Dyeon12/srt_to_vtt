using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace KoreanSubtitleStudio.Services
{
    public sealed class ConversionResult
    {
        public string OutputPath { get; private set; }
        public int CueCount { get; private set; }
        public ConversionResult(string outputPath, int cueCount) { OutputPath = outputPath; CueCount = cueCount; }
    }

    public sealed class PreviewResult
    {
        public string Content { get; private set; }
        public int CueCount { get; private set; }
        public PreviewResult(string content, int cueCount) { Content = content; CueCount = cueCount; }
    }

    public sealed class SubtitleConversionService
    {
        private static readonly Regex TimelinePattern = new Regex(
            @"^\s*(\d{1,3}):(\d{2}):(\d{2})[,.](\d{1,3})\s*-->\s*(\d{1,3}):(\d{2}):(\d{2})[,.](\d{1,3})(.*)$",
            RegexOptions.Compiled);

        public ConversionResult Convert(string inputPath, string outputPath, int shiftMilliseconds, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
                throw new FileNotFoundException("SRT 파일을 찾을 수 없습니다.", inputPath);
            if (!string.Equals(Path.GetExtension(inputPath), ".srt", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("SRT 확장자 파일만 변환할 수 있습니다.");
            if (File.Exists(outputPath) && !overwrite)
                throw new IOException("동일한 VTT 파일이 이미 존재합니다.");

            var preview = CreatePreview(inputPath, shiftMilliseconds);
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, preview.Content, new UTF8Encoding(true));
            return new ConversionResult(outputPath, preview.CueCount);
        }

        public PreviewResult CreatePreview(string inputPath, int shiftMilliseconds)
        {
            if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
                throw new FileNotFoundException("SRT 파일을 찾을 수 없습니다.", inputPath);
            if (!string.Equals(Path.GetExtension(inputPath), ".srt", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("SRT 확장자 파일만 변환할 수 있습니다.");

            var source = DecodeSource(File.ReadAllBytes(inputPath));
            var normalized = source.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            var blocks = Regex.Split(normalized, @"\n[ \t]*\n");
            var document = new StringBuilder();
            document.Append("WEBVTT\r\n\r\n");
            var cueCount = 0;

            foreach (var block in blocks)
            {
                var lines = block.Split('\n');
                var timelineIndex = FindTimeline(lines);
                if (timelineIndex < 0) continue;

                var timeline = TimelinePattern.Match(lines[timelineIndex]);
                var start = ReadTimestamp(timeline, 1);
                var end = ReadTimestamp(timeline, 5);
                var shift = TimeSpan.FromMilliseconds(shiftMilliseconds);
                start = NotNegative(start.Add(shift));
                end = NotNegative(end.Add(shift));
                if (end < start) end = start;

                document.Append(WriteTimestamp(start)).Append(" --> ").Append(WriteTimestamp(end));
                document.Append(timeline.Groups[9].Value.TrimEnd()).Append("\r\n");
                for (var lineIndex = timelineIndex + 1; lineIndex < lines.Length; lineIndex++)
                    document.Append(lines[lineIndex].TrimEnd()).Append("\r\n");
                document.Append("\r\n");
                cueCount++;
            }

            if (cueCount == 0) throw new InvalidDataException("유효한 SRT 타임라인을 찾지 못했습니다.");
            return new PreviewResult(document.ToString(), cueCount);
        }

        private static int FindTimeline(IList<string> lines)
        {
            for (var index = 0; index < lines.Count; index++)
                if (TimelinePattern.IsMatch(lines[index])) return index;
            return -1;
        }

        private static TimeSpan ReadTimestamp(Match match, int firstGroup)
        {
            var hours = int.Parse(match.Groups[firstGroup].Value, CultureInfo.InvariantCulture);
            var minutes = int.Parse(match.Groups[firstGroup + 1].Value, CultureInfo.InvariantCulture);
            var seconds = int.Parse(match.Groups[firstGroup + 2].Value, CultureInfo.InvariantCulture);
            var fraction = match.Groups[firstGroup + 3].Value.PadRight(3, '0');
            var milliseconds = int.Parse(fraction.Substring(0, 3), CultureInfo.InvariantCulture);
            return TimeSpan.FromMilliseconds((((hours * 60L) + minutes) * 60L + seconds) * 1000L + milliseconds);
        }

        private static string WriteTimestamp(TimeSpan value)
        {
            var hours = (long)value.TotalHours;
            return hours.ToString("00", CultureInfo.InvariantCulture) + ":" +
                   value.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
                   value.Seconds.ToString("00", CultureInfo.InvariantCulture) + "." +
                   value.Milliseconds.ToString("000", CultureInfo.InvariantCulture);
        }

        private static TimeSpan NotNegative(TimeSpan value) { return value < TimeSpan.Zero ? TimeSpan.Zero : value; }

        private static string DecodeSource(byte[] bytes)
        {
            if (StartsWith(bytes, 0xEF, 0xBB, 0xBF)) return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            if (StartsWith(bytes, 0xFF, 0xFE)) return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (StartsWith(bytes, 0xFE, 0xFF)) return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            try { return new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException) { return Encoding.GetEncoding(949).GetString(bytes); }
        }

        private static bool StartsWith(byte[] bytes, params byte[] prefix)
        {
            if (bytes.Length < prefix.Length) return false;
            for (var index = 0; index < prefix.Length; index++) if (bytes[index] != prefix[index]) return false;
            return true;
        }
    }
}

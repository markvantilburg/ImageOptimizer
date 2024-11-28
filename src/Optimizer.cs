using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MadsKristensen.ImageOptimizer
{
    internal class Optimizer
    {
        private static readonly string[] _sizeSuffixes = new[] { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        private static readonly RatingPrompt _rating = new("MadsKristensen.ImageOptimizer64bit", Vsix.Name, General.Instance);

        private static OutputWindowPane OutputWindow { get; set; }

        public async Task OptimizeAsync(IEnumerable<string> imageFilePaths, CompressionType type, string solutionFullName = null)
        {
            var compressor = new Compressor();
            var imageCount = imageFilePaths.Count();
            var cacheRoot = string.IsNullOrEmpty(solutionFullName) ? imageFilePaths.ElementAt(0) : solutionFullName;
            var cache = new Cache(cacheRoot, type == CompressionType.Lossy);

            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, TaskScheduler = TaskScheduler.Default };
            List<CompressionResult> results = [];

            await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);
            await VS.StatusBar.ShowMessageAsync($"Optimizing selected images...");

            await Task.Run(() =>
            {
                _ = Parallel.For(0, imageCount, options, (i, state) =>
                {
                    try
                    {
                        var filePath = imageFilePaths.ElementAt(i);
                        var name = Path.GetFileName(filePath);

                        CompressionResult result = cache.IsFullyOptimized(filePath) ? CreateCacheResult(filePath) : compressor.CompressFile(filePath, type == CompressionType.Lossy);

                        if (result.Saving > 0 && result.ResultFileSize > 0 && File.Exists(result.ResultFileName))
                        {
                            File.Copy(result.ResultFileName, result.OriginalFileName, true);
                            File.Delete(result.ResultFileName);

                            var maxLength = imageFilePaths.Max(r => Path.GetFileName(result.OriginalFileName).Length);
                            var p = Math.Round(100 - (result.ResultFileSize / (double)result.OriginalFileSize * 100), 1, MidpointRounding.AwayFromZero);
                        }
                        else
                        {
                            cache.AddToCache(filePath);
                        }

                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        ex.LogAsync().FireAndForget();
                    }
                });
            });

            _rating.RegisterSuccessfulUsage();
            await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);
            DisplayEndResultsAsync(results).FireAndForget();
        }

        private async Task DisplayEndResultsAsync(IEnumerable<CompressionResult> list)
        {
            var savings = list.Where(r => r != null).Sum(r => r.Saving);
            var originals = list.Where(r => r != null).Sum(r => r.OriginalFileSize);
            var results = list.Where(r => r != null).Sum(r => r.ResultFileSize);

            OutputWindow ??= await VS.Windows.CreateOutputWindowPaneAsync(Vsix.Name);

            if (savings > 0)
            {
                IEnumerable<CompressionResult> filesOptimized = list.Where(r => r != null);
                var maxLength = filesOptimized.Max(r => Path.GetFileName(r.OriginalFileName).Length);
                StringBuilder sb = new();

                foreach (CompressionResult result in filesOptimized)
                {
                    var name = Path.GetFileName(result.OriginalFileName).PadRight(maxLength);
                    var p = Math.Round(100 - (result.ResultFileSize / (double)result.OriginalFileSize * 100), 1, MidpointRounding.AwayFromZero);

                    _ = sb.AppendLine(name + "\t  optimized by " + ToFileSize(result.Saving) + " / " + p + "%");
                }

                var successfulOptimizations = list.Count(x => x != null);
                var percent = Math.Round(100 - (results / (double)originals * 100), 1, MidpointRounding.AwayFromZero);
                var image = successfulOptimizations == 1 ? "image" : "images";
                var msg = successfulOptimizations + " " + image + " optimized. Total saving of " + ToFileSize(savings) + " / " + percent + "%";

                await VS.StatusBar.ShowMessageAsync(msg);
                await OutputWindow.WriteLineAsync(sb.ToString() + Environment.NewLine + msg + Environment.NewLine);
            }
            else
            {
                await VS.StatusBar.ShowMessageAsync("The images were already optimized");
                await OutputWindow.WriteLineAsync("The images were already optimized");
            }

            await OutputWindow.ActivateAsync();
        }

        private CompressionResult CreateCacheResult(string file)
        {
            return new CompressionResult(file, file, TimeSpan.Zero) { Processed = false };
        }

        // From https://stackoverflow.com/a/14488941
        public static string ToFileSize(long value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + ToFileSize(-value, decimalPlaces); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            var mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag)
            // [i.e. the number of bytes in the unit corresponding to mag]
            var adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return value < 1024
                ? string.Format("{0:n0} {1}", adjustedSize, _sizeSuffixes[mag])
                : string.Format("{0:n" + decimalPlaces + "} {1}", adjustedSize, _sizeSuffixes[mag]);
        }
    }
}

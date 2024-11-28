using System.Collections.Generic;
using System.IO;

namespace MadsKristensen.ImageOptimizer
{
    internal class Cache
    {
        public Dictionary<string, long> _cache;
        private FileInfo _cacheFile;
        private readonly bool _lossy;
        private static readonly object _syncRoot = new();

        public Cache(string rootFolder, bool lossy)
        {
            _lossy = lossy;
            _cache = ReadCacheFromDisk(rootFolder);
        }

        public bool IsFullyOptimized(string file)
        {
            var info = new FileInfo(file);

            return _cache.ContainsKey(file) && _cache[file] == info.Length;
        }

        public void AddToCache(string file)
        {
            if (string.IsNullOrEmpty(_cacheFile?.FullName))
            {
                return;
            }

            var info = new FileInfo(file);
            _cache[file] = info.Length;

            if (!_cacheFile.Directory.Exists)
                _cacheFile.Directory.Create();

            lock (_syncRoot)
            {
                using (var writer = new StreamWriter(_cacheFile.FullName, false))
                {
                    foreach (var key in _cache.Keys)
                    {
                        writer.WriteLine(key + "|" + _cache[key]);
                    }
                }
            }
        }

        private Dictionary<string, long> ReadCacheFromDisk(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return [];
            }

            _cacheFile = LoadCacheFileName(fileName);
            var dic = new Dictionary<string, long>();

            if (!_cacheFile.Exists)
            {
                return dic;
            }

            var lines = File.ReadAllLines(_cacheFile.FullName);

            foreach (var line in lines)
            {
                var args = line.Split('|');

                if (args.Length != 2)
                {
                    continue;
                }

                if (long.TryParse(args[1], out var length))
                {
                    dic.Add(args[0], length);
                }
            }

            return dic;
        }

        private FileInfo LoadCacheFileName(string fileName)
        {
            FileInfo file = new(fileName);
            DirectoryInfo directory = file.Directory;

            while (directory != null)
            {
                var vsDirPath = Path.Combine(directory.FullName, ".vs");

                if (Directory.Exists(vsDirPath))
                {
                    var cacheFileName = _lossy ? "cache-lossy.txt" : "cache-lossless.txt";
                    return new FileInfo(Path.Combine(vsDirPath, Vsix.Name, cacheFileName));
                }

                directory = directory.Parent;
            }

            return null;
        }
    }
}

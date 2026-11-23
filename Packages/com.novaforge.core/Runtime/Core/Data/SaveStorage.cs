using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Data
{
    /// <summary>
    /// Raw key/value text storage. Keys are validated identifiers (no path traversal).
    /// One storage instance is scoped to ONE game, which keeps saves of different games strictly separated.
    /// </summary>
    public interface ISaveStorage
    {
        bool Exists(string key);
        string Read(string key);
        /// <summary>Must be atomic: after a crash either the old or the new content is present, never a partial file.</summary>
        void Write(string key, string content);
        void Delete(string key);
        IEnumerable<string> Keys();
    }

    /// <summary>
    /// File-based storage. Writes go to "key.tmp", are flushed, then atomically swapped in.
    /// The previous version is kept as "key.bak" for recovery.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private const string Ext = ".json";
        private const string TmpExt = ".tmp";
        private const string BakExt = ".bak";
        private readonly string _root;

        /// <param name="rootDirectory">e.g. Path.Combine(Application.persistentDataPath, "novaforge", gameId)</param>
        public FileSaveStorage(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("Root directory required.", nameof(rootDirectory));
            _root = rootDirectory;
            Directory.CreateDirectory(_root);
        }

        public string RootDirectory => _root;

        public bool Exists(string key) => File.Exists(PathFor(key));

        public string Read(string key) => File.ReadAllText(PathFor(key), Encoding.UTF8);

        public void Write(string key, string content)
        {
            var path = PathFor(key);
            var tmp = path + TmpExt;
            var bak = path + BakExt;
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = new UTF8Encoding(false).GetBytes(content ?? string.Empty);
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }

            if (File.Exists(path))
                File.Replace(tmp, path, bak, ignoreMetadataErrors: true);
            else
                File.Move(tmp, path);
        }

        public void Delete(string key)
        {
            var path = PathFor(key);
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + BakExt)) File.Delete(path + BakExt);
            if (File.Exists(path + TmpExt)) File.Delete(path + TmpExt);
        }

        public IEnumerable<string> Keys()
        {
            foreach (var f in Directory.GetFiles(_root, "*" + Ext))
                yield return Path.GetFileNameWithoutExtension(f);
        }

        /// <summary>Backup written by the previous successful Write, if any.</summary>
        public bool TryReadBackup(string key, out string content)
        {
            var bak = PathFor(key) + BakExt;
            if (File.Exists(bak))
            {
                content = File.ReadAllText(bak, Encoding.UTF8);
                return true;
            }
            content = null;
            return false;
        }

        private string PathFor(string key) => Path.Combine(_root, Guard.Id(key, nameof(key)) + Ext);
    }

    /// <summary>In-memory storage for tests and for ephemeral sessions.</summary>
    public sealed class InMemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, string> _data = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _backups = new Dictionary<string, string>();

        public bool Exists(string key) => _data.ContainsKey(Guard.Id(key, nameof(key)));

        public string Read(string key)
        {
            if (!_data.TryGetValue(Guard.Id(key, nameof(key)), out var v)) throw new FileNotFoundException(key);
            return v;
        }

        public void Write(string key, string content)
        {
            Guard.Id(key, nameof(key));
            if (_data.TryGetValue(key, out var old)) _backups[key] = old;
            _data[key] = content ?? string.Empty;
        }

        public void Delete(string key)
        {
            _data.Remove(key);
            _backups.Remove(key);
        }

        public IEnumerable<string> Keys() => new List<string>(_data.Keys);

        public bool TryReadBackup(string key, out string content) => _backups.TryGetValue(key, out content);

        /// <summary>Test helper: simulates on-disk corruption.</summary>
        public void Corrupt(string key, string garbage) => _data[key] = garbage;
    }
}

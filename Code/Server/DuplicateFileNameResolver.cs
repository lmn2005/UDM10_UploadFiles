using System.IO;

namespace UDM10.Server
{
    // Không ghi đè file trùng tên: tự thêm _1, _2, _3...
    public class DuplicateFileNameResolver
    {
        private readonly string _uploadsFolder;
        private readonly object _syncRoot = new();
        private readonly HashSet<string> _reservedPaths =
            new(StringComparer.OrdinalIgnoreCase);

        public DuplicateFileNameResolver(string uploadsFolder)
        {
            _uploadsFolder = uploadsFolder;
        }

        public string GetAvailablePath(string fileName)
        {
            lock (_syncRoot)
            {
                string name = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                int suffix = 0;

                while (true)
                {
                    string candidateName = suffix == 0
                        ? fileName
                        : $"{name}_{suffix}{ext}";
                    string candidate = Path.Combine(_uploadsFolder, candidateName);

                    if (!File.Exists(candidate) &&
                        !File.Exists(candidate + ".part") &&
                        _reservedPaths.Add(candidate))
                    {
                        return candidate;
                    }

                    suffix++;
                }
            }
        }

        public void ReleasePath(string path)
        {
            lock (_syncRoot)
            {
                _reservedPaths.Remove(path);
            }
        }
    }
}

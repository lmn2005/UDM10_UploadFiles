using System.IO;

namespace UDM10.Server
{
    // Không ghi đè file trùng tên: tự thêm _1, _2, _3...
    public class DuplicateFileNameResolver
    {
        // NTFS giới hạn 255 ký tự cho một thành phần tên file. TemporaryFileManager
        // ghi file tạm bằng cách nối thêm ".part" (5 ký tự), nên nếu không chừa chỗ,
        // một fileName gần 255 ký tự sẽ khiến việc tạo file tạm thất bại.
        private const int MaxFileNameComponentLength = 255;
        private const int PartSuffixLength = 5; // ".part"
        private const int MaxSafeFileNameLength =
            MaxFileNameComponentLength - PartSuffixLength;

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
                    string suffixText = suffix == 0 ? string.Empty : $"_{suffix}";
                    string candidateName = BuildSafeFileName(name, suffixText, ext);
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

        // Giữ nguyên phần mở rộng và hậu tố trùng tên; chỉ cắt bớt phần tên gốc
        // khi cần để tổng độ dài không vượt giới hạn an toàn.
        private static string BuildSafeFileName(string name, string suffixText, string ext)
        {
            string candidateName = $"{name}{suffixText}{ext}";
            if (candidateName.Length <= MaxSafeFileNameLength)
            {
                return candidateName;
            }

            int allowedNameLength =
                MaxSafeFileNameLength - suffixText.Length - ext.Length;
            allowedNameLength = Math.Max(1, allowedNameLength);

            string truncatedName = name.Length > allowedNameLength
                ? name.Substring(0, allowedNameLength)
                : name;

            return $"{truncatedName}{suffixText}{ext}";
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

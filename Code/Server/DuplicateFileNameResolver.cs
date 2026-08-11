using System.IO;

namespace UDM10.Server
{
    // Không ghi đè file trùng tên: tự thêm _1, _2, _3...
    public class DuplicateFileNameResolver
    {
        private readonly string _uploadsFolder;

        public DuplicateFileNameResolver(string uploadsFolder)
        {
            _uploadsFolder = uploadsFolder;
        }

        public string GetAvailablePath(string fileName)
        {
            string path = Path.Combine(_uploadsFolder, fileName);
            if (!File.Exists(path))
                return path;

            string name = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            int i = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(_uploadsFolder, $"{name}_{i}{ext}");
                i++;
            } while (File.Exists(candidate));

            return candidate;
        }
    }
}

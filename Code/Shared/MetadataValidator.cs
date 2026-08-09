using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UDM10.Shared
{
    public static class MetadataValidator
    {
        public static bool IsValid(string? fileName, long fileSize, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                errorMessage = "The file name cannot be empty.";
                return false;
            }

            if (fileSize <= 0)
            {
                errorMessage = "The file size is not valid (must be greater than 0 bytes).";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
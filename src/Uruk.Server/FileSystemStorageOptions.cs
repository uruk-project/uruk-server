using System;
using System.IO;

namespace Uruk.Server
{
    public class FileSystemStorageOptions
    {
        public FileSystemStorageOptions()
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Directory = Path.Combine(root, ".uruk-server");
        }

        public string Directory { get; set; }
    }
}

using System;
using System.IO;

namespace JoSystem.Models.DTOs
{
    public class FileSystemItem
    {
        public FileSystemItem(FileInfo file)
        {
            Name = file.Name;
            FullName = file.FullName;
            Type = "文件";
            DisplaySize = (file.Length / 1024.0 / 1024.0).ToString("F2");
            LastWriteTime = file.LastWriteTime.ToString();
            IsDirectory = false;
        }

        public FileSystemItem(DirectoryInfo dir)
        {
            Name = dir.Name;
            FullName = dir.FullName;
            Type = "文件夹";
            DisplaySize = "<DIR>";
            LastWriteTime = dir.LastWriteTime.ToString();
            IsDirectory = true;
        }

        public FileSystemItem(string path, string name, bool isParent)
        {
            Name = name;
            FullName = path;
            Type = "文件夹";
            DisplaySize = "<DIR>";
            IsDirectory = true;
            IsVirtualParent = isParent;
        }

        public string Name { get; set; }
        public string FullName { get; set; }
        public string Type { get; set; }
        public string DisplaySize { get; set; }
        public string LastWriteTime { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsVirtualParent { get; set; }
    }
}

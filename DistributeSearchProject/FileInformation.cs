using System;

namespace DistributeSearchProject
{
    [Serializable]
    class FileInformation {
        public string name;
        public string fullName;
        public string extension;
        public string directory;
        public DateTime CreationTime;
        public DateTime LastAccessTime;
        public DateTime LastModifyTime;
        public string hostIp;

        public FileInformation(string name, string fullName, string extension, string directory, DateTime creationTime, DateTime lastAccessTime, DateTime lastModifyTime, string hostIp) {
            this.name = name;
            this.fullName = fullName;
            this.extension = extension;
            this.directory = directory;
            CreationTime = creationTime;
            LastAccessTime = lastAccessTime;
            LastModifyTime = lastModifyTime;
            this.hostIp = hostIp;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Search.TcpSend;

namespace DistributeSearchProject.Search
{
    class DistributeSearch
    {
        private List<FileInfo> filesToSearch = new List<FileInfo>();
        private Dictionary<string, FileInfo> resultFiles = new Dictionary<string, FileInfo>();
        private bool allFilesSearched = false;

        private readonly Object endLock = new object();
        private readonly Object listLock = new object();
        private readonly Object resultLock = new object();

//        private readonly int threadCount = Environment.ProcessorCount;
        //ToDo: Think about searching threads
        private readonly int threadCount = 2;

        public delegate void EventHandler(FileInformation fileInfo, string host);
        public event EventHandler ListUpdate;

        private Thread callerThread;
        private Thread folderThread;
        private Thread[] searchThreads;

        private void SetAllFilesSearched() {
            lock (endLock) {
                allFilesSearched = true;   
            }
        }

        private bool IsAllFilesSearched() {
            lock (endLock) {
                return allFilesSearched;
            }
        }

        private int GetListCount() {
            lock (listLock) {
                return filesToSearch.Count;
            }
        }

        private FileInfo GetFirstFileInfo() {
            FileInfo result;
            lock (listLock) {
                result = filesToSearch[0];
                filesToSearch.RemoveAt(0);
            }

            return result;
        }

        private void AddFileInfo(FileInfo fileInfo) {
            bool callUpdateEvent = false;
            lock (resultLock) {
                if (!resultFiles.ContainsKey(fileInfo.Name.ToLower())) {
                    resultFiles.Add(fileInfo.Name.ToLower(), fileInfo);
                    callUpdateEvent = true;
                } else {
                    if (resultFiles[fileInfo.Name.ToLower()].LastWriteTime.CompareTo(fileInfo.LastWriteTime) < 0) {
                        resultFiles[fileInfo.Name.ToLower()] = fileInfo;
                        callUpdateEvent = true;
                    }
                }
            }

            if (callUpdateEvent) {
                var file = new FileInformation(
                    fileInfo.Name,
                    fileInfo.FullName,
                    fileInfo.Extension,
                    fileInfo.DirectoryName,
                    fileInfo.CreationTime,
                    fileInfo.LastAccessTime,
                    fileInfo.LastWriteTime,
                    //TODO: Get local IP from constructor
                    Settings.LOCAL_IP.ToString()
                );

                ListUpdate(file, file.hostIp);
            }
        }

        private void AddToBaseList(List<FileInfo> list) {
            lock (listLock) {
                filesToSearch.AddRange(list);
            }
        }

        public void StopSearch() {
            SetAllFilesSearched();
            if (folderThread != null) {
                folderThread.Interrupt();
                folderThread.Abort();
            }
            resultFiles.Clear();

            foreach (var thread in searchThreads) {
                if (thread != null) {
                    thread.Interrupt();
                    thread.Abort();
                }
            }

            if (callerThread != null) {
                callerThread.Interrupt();
                callerThread.Abort();
            }
        }

        public List<FileInfo> FindFiles(String data, String directory, int fileBufferize) {
            try {
                callerThread = Thread.CurrentThread;
                FolderSearcher folderSearcher = new FolderSearcher(directory, SetAllFilesSearched, AddToBaseList);
                folderThread = new Thread(folderSearcher.FindFilesToSearch);
                folderThread.IsBackground = true;
                folderThread.Start();     
  
                searchThreads = new Thread[threadCount];
                for (int i = 0; i < threadCount; i++) {
                    FileSearcher fileSearcher = new FileSearcher(
                        data,
                        fileBufferize,
                        IsAllFilesSearched,
                        GetListCount,
                        AddFileInfo,
                        GetFirstFileInfo
                        );

                    searchThreads[i] = new Thread(fileSearcher.TestFiles);
                    searchThreads[i].IsBackground = true;
                    searchThreads[i].Start();    
                }

                folderThread.Join();
                for (int i = 0; i < threadCount; i++) {
                    searchThreads[i].Join();
                }

                return new List<FileInfo>(resultFiles.Values);
            }
            catch (ThreadInterruptedException e) {
                return null;
            }
        }

        private class FolderSearcher {
            private List<FileInfo> fileBuffer = new List<FileInfo>();
            private string baseDir;
            private int maxBufferCount = 10;

            public delegate void CallBack();
            public delegate void AddToBaseList(List<FileInfo> list);
            private CallBack callBack;
            private AddToBaseList addToBaseList;

            public FolderSearcher(string baseDir, CallBack callBack, AddToBaseList addToBaseList) {
                this.baseDir = baseDir;
                this.callBack = callBack;
                this.addToBaseList = addToBaseList;
            }

            public void FindFilesToSearch() {
                try {
                    FindFilesToSearch(baseDir);
                    callBack();
                }
                catch (ThreadInterruptedException e) {
                }
//                catch (ThreadAbortException) {
//                }
            }

            private void FindFilesToSearch(string currentDir) {
                DirectoryInfo directoryInfo = new DirectoryInfo(currentDir);

                try {
                    foreach (var file in directoryInfo.GetFiles()) {
                        fileBuffer.Add(file);
                        if (fileBuffer.Count >= maxBufferCount)
                            EmptyBuffer();
                    }

                    foreach (var dir in directoryInfo.GetDirectories()) {
                        FindFilesToSearch(dir.FullName);
                    }
                }
                catch (UnauthorizedAccessException e) {
                    //TODO: Add logging
                }
                catch (FileNotFoundException e) {
                }
                catch (DirectoryNotFoundException e) {
                }

                if(fileBuffer.Count > 0)
                    EmptyBuffer();
            }

            private void EmptyBuffer() {
                addToBaseList(fileBuffer);
                fileBuffer.Clear();
            }
        }

        private class FileSearcher {
            private string dataPattern;
            private int bufferSize;

            public delegate bool EndTest();
            public delegate int GetCount();
            public delegate void ReturnResult(FileInfo fileInfo);
            public delegate FileInfo GetFirstFileInfo();
            private EndTest endTest;
            private GetCount getCount;
            private ReturnResult returnResult;
            private GetFirstFileInfo getFirstFileInfo;

            public FileSearcher(string dataPattern, int bufferSize, EndTest endTest, GetCount getCount, ReturnResult returnResult, GetFirstFileInfo getFirstFileInfo) {
                this.dataPattern = dataPattern.ToLower();
                this.bufferSize = bufferSize;
                this.endTest = endTest;
                this.getCount = getCount;
                this.returnResult = returnResult;
                this.getFirstFileInfo = getFirstFileInfo;
            }

            public void TestFiles() {
                try {
                    while (!endTest() || getCount() > 0) {
                        if (getCount() == 0) {
                            Thread.Sleep(0);
                            continue;
                        }

                        FileInfo fileInfo = getFirstFileInfo();

                        string fullName = fileInfo.FullName;
                        fileInfo = TestFile(dataPattern, fileInfo.FullName, fileInfo.Length, bufferSize);
                        if (fileInfo != null)
                            returnResult(fileInfo);
                    }
                }
                catch (ThreadInterruptedException e) {
                }
            }

            private FileInfo TestFile(String data, String fileName, long fileLength, int bufferSize)
            {
                FileStream reader;
                try {
                    reader = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                catch (Exception ex) {
                    return null;
                }

                String line;
                int find = 0;
                byte[] buffer = new byte[bufferSize];
                long offset = 0;
                int bytesRead = 0;

                while (offset <= fileLength) {
                    reader.Seek(offset, SeekOrigin.Begin);
                    bytesRead = reader.Read(buffer, 0, bufferSize);

                    line = Encoding.Default.GetString(buffer).ToLower();
                    find += (line.Length - line.Replace(data, "").Length) / data.Length;
                    if (find >= 2)
                        break;

                    offset += bufferSize - data.Length + 1;
                }

                reader.Close();
                if (find >= 2) {
                    return new FileInfo(fileName);
                } else {
                    return null;
                }
            }
        }
    }
}

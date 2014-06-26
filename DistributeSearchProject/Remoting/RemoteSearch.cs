using System;

namespace DistributeSearchProject.Remoting
{
    class RemoteSearch: MarshalByRefObject {
        //This may need a lock
        public delegate void FindFilesDelegate(String data);
        public static FindFilesDelegate LocalFindFiles;

        public void FindFiles(String data) {
            LocalFindFiles(data);
        }
    }
}

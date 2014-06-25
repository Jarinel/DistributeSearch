using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DistributeSearchProject.Search;
using Search.TcpSend;

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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Search.TcpSend;

namespace DistributeSearchProject.Remoting
{
    class RemoteAddResult: MarshalByRefObject {
        public delegate void AddResultDelegate(FileInformation file, string host);
        public static AddResultDelegate LocalAddResult;

        public void AddResult(FileInformation file, string host) {
            LocalAddResult(file, host);
        }
    }
}

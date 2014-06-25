using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistributeSearchProject.Remoting
{
    class RemoteHostProvider: MarshalByRefObject {
        public delegate List<string> GetHostsDelegate();
        public static GetHostsDelegate GetHostsFunction;

        public delegate void SetActualHostsDelegate(List<string> list);
        public static SetActualHostsDelegate SetActualHostsFunction;

        public List<string> GetHosts() {
            return GetHostsFunction();
        }

        public void SetActualHosts(List<string> list) {
            SetActualHostsFunction(list);
        }
    }
}

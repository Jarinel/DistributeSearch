using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistributeSearchProject.Remoting
{
    class RemoteSearchResolve: MarshalByRefObject {
        public delegate long SearchResolveDelegate(long unique);
        public static SearchResolveDelegate SearchResolveFunction;

        public long SearchResolve(long unique) {
            return SearchResolveFunction(unique);
        }
    }
}

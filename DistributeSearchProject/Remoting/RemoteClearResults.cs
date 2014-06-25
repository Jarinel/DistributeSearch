using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistributeSearchProject.Remoting
{
    class RemoteClearResults: MarshalByRefObject {
        public delegate void ClearResultsDelegate();
        public static ClearResultsDelegate ClearResultsFunction;

        public void ClearResults() {
            ClearResultsFunction();
        }
    }
}

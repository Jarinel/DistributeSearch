using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistributeSearchProject.Remoting
{
    class RemoteStopFinding: MarshalByRefObject {
        public delegate void StopFindingDelegate();
        public static StopFindingDelegate StopFindingFunction;

        public void StopFinding() {
            StopFindingFunction();
        }
    }
}

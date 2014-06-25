using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Threading.Tasks;
using Search.TcpSend;

namespace DistributeSearchProject.Remoting
{
    class RemotingServer {
        private int port;

        public RemotingServer(int port) {
            this.port = port;
        }

        public IChannel GetChannel(int tcpPort, bool isSecure) {
            var serverProv = new BinaryServerFormatterSinkProvider();
            serverProv.TypeFilterLevel = TypeFilterLevel.Full;
            IDictionary propBag = new Hashtable();
            propBag["port"] = tcpPort;
            propBag["typeFilterLevel"] = TypeFilterLevel.Full;
            propBag["name"] = Guid.NewGuid().ToString();

            if (isSecure) {
                propBag["secure"] = isSecure;
                propBag["impersonate"] = false;
            }
            return new TcpChannel(propBag, null, serverProv);
        }

        public void Start() {
            var channel = (TcpChannel) GetChannel(port, false);
            ChannelServices.RegisterChannel(channel, false);
            RemotingConfiguration.RegisterWellKnownServiceType(typeof(RemoteSearch), "RemoteSearch", WellKnownObjectMode.SingleCall);
            RemotingConfiguration.RegisterWellKnownServiceType(typeof(RemoteAddResult), "RemoteAddResult", WellKnownObjectMode.SingleCall);
            RemotingConfiguration.RegisterWellKnownServiceType(typeof(RemoteClearResults), "RemoteClearResults", WellKnownObjectMode.SingleCall);
            RemotingConfiguration.RegisterWellKnownServiceType(typeof(RemoteStopFinding), "RemoteStopFinding", WellKnownObjectMode.SingleCall);

            RemotingConfiguration.RegisterWellKnownServiceType(typeof(TcpFileSender), "TcpFileSender", WellKnownObjectMode.SingleCall);
        }
    }
}

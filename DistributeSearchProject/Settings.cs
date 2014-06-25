using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DistributeSearchProject
{
    static class Settings {
        public static IPAddress BROADCAST_IP;
        public static IPAddress LOCAL_IP;

        public static int UDP_PORT;
        public static int REMOTING_SERVER_PORT;
        public static int DOWNLOAD_PORT;

        public static int UDP_BROADCAST_DELAY;
        public static int HOST_UPDATE_TIME;
        public static int HOST_DELETE_TIME;
        
        public static string DIRECTORY;
        public static string DOWNLOAD_DIRECTORY;

        public static int BUFFER_SIZE;
        public static int TCP_TRANSFER_BUFFER;

        static Settings() {
            BROADCAST_IP            = IPAddress.Parse(ConfigurationManager.AppSettings["broadcast_ip"]);
            LOCAL_IP                = IPAddress.Parse(ConfigurationManager.AppSettings["localhost_ip"]);

            UDP_PORT                = int.Parse(ConfigurationManager.AppSettings["udp_port"]);
            REMOTING_SERVER_PORT    = int.Parse(ConfigurationManager.AppSettings["remoting_server_port"]);
            DOWNLOAD_PORT           = int.Parse(ConfigurationManager.AppSettings["download_port"]);

            UDP_BROADCAST_DELAY     = int.Parse(ConfigurationManager.AppSettings["udp_broadcast_delay"]);
            HOST_UPDATE_TIME        = int.Parse(ConfigurationManager.AppSettings["host_update_time"]);
            HOST_DELETE_TIME        = int.Parse(ConfigurationManager.AppSettings["host_delete_time"]);

            DIRECTORY               = ConfigurationManager.AppSettings["directory"];
            DOWNLOAD_DIRECTORY      = ConfigurationManager.AppSettings["download_directory"];

            BUFFER_SIZE             = int.Parse(ConfigurationManager.AppSettings["buffer_size"]);
            TCP_TRANSFER_BUFFER     = int.Parse(ConfigurationManager.AppSettings["tcp_transfer_buffer"]);
        }

    }
}

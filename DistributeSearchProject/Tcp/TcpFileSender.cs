using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using DistributeSearchProject;

namespace Search.TcpSend
{
    class TcpFileSender: MarshalByRefObject {
        private Socket sender;

        public TcpFileSender() {
            sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public void SendFile(string host, int port, string fileName, int bufferSize) {
//            IPEndPoint ipEndPoint = new IPEndPoint(Dns.GetHostEntry(host).AddressList[0], port);
            IPEndPoint ipEndPoint = new IPEndPoint(Dns.Resolve(host).AddressList[0], port);

            try {
                sender.Connect(ipEndPoint);
                NetworkStream networkStream = new NetworkStream(sender);

                FileInfo fileInfo = new FileInfo(fileName);
                FileInformation information = new FileInformation(
                    fileInfo.Name,
                    fileInfo.FullName,
                    fileInfo.Extension,
                    fileInfo.DirectoryName,
                    fileInfo.CreationTime,
                    fileInfo.LastAccessTime,
                    fileInfo.LastWriteTime,
                    Settings.LOCAL_IP.ToString()
                );

                BinaryFormatter formatter = new BinaryFormatter();
                MemoryStream memoryStream = new MemoryStream();

                formatter.Serialize(memoryStream, information);
                memoryStream.Position = 0;
                byte[] infoBuffer = new byte[memoryStream.Length];
                memoryStream.Read(infoBuffer, 0, infoBuffer.Length);

                byte[] headerLength = BitConverter.GetBytes(infoBuffer.Length);
                networkStream.Write(headerLength, 0, headerLength.Length);
                networkStream.Write(infoBuffer, 0, infoBuffer.Length);

                FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                byte[] buffer = new byte[bufferSize];
                int bytesRead = 0;
                while ((bytesRead = fileStream.Read(buffer, 0, bufferSize)) != 0) {
                    networkStream.Write(buffer, 0, bytesRead);    
                }

                fileStream.Close();
                memoryStream.Close();
                networkStream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                sender.Close();
            }
        }
    }
}

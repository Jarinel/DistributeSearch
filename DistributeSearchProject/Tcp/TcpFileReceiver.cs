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
    class TcpFileReceiver {
        private Socket listener;
        private byte[] buffer;

        public TcpFileReceiver(string host, int port, int bufferSize) {
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
//            IPEndPoint ipEndPoint = new IPEndPoint(Dns.GetHostEntry(host).AddressList[0], port);
            IPEndPoint ipEndPoint = new IPEndPoint(Dns.Resolve(host).AddressList[0], port);
            listener.Bind(ipEndPoint);

            buffer = new byte[bufferSize];
        }

        public void ReceiveFile(string dest) {
            listener.Listen(1);

            try {
                Socket sender = listener.Accept();
                NetworkStream networkStream = new NetworkStream(sender);

                if (networkStream.CanRead) {
                    byte[] intBuffer = new byte[4];
                    networkStream.Read(intBuffer, 0, 4); //Read int that represents size of the fileinfo
                    int fileInfoSize = BitConverter.ToInt32(intBuffer, 0);

                    byte[] fileInfoBuffer = new byte[fileInfoSize];
                    networkStream.Read(fileInfoBuffer, 0, fileInfoSize);
                    MemoryStream memoryStream = new MemoryStream();
                    memoryStream.Write(fileInfoBuffer, 0, fileInfoSize);
                    memoryStream.Position = 0;

                    BinaryFormatter formatter = new BinaryFormatter();
                    FileInformation fileInformation = (FileInformation) formatter.Deserialize(memoryStream);

                    string filePath = dest + "\\" + fileInformation.name;
                    FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                    
                    int bytesRead = 0;
                    while ((bytesRead = networkStream.Read(buffer, 0, buffer.Length)) != 0) {
                        fileStream.Write(buffer, 0, bytesRead);
                    }

                    fileStream.Close();

                    File.SetCreationTime(filePath, fileInformation.CreationTime);
                    File.SetLastAccessTime(filePath, fileInformation.LastAccessTime);
                    File.SetLastWriteTime(filePath, fileInformation.LastModifyTime);

                    memoryStream.Close();
                    networkStream.Close();
                } else {
                    Console.WriteLine("Can't read from stream!");
                }
            }
            catch (Exception e) {
                Console.WriteLine(e);
            }
            finally {
                listener.Close();
            }
        }
    }
}

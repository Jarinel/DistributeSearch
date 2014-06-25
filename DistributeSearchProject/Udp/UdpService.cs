using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace DistributeSearchProject.Udp
{
    class UdpService {
        private IPEndPoint ipEndPoint;

        private readonly Object portLock = new object();
        private int _port;
        private readonly int broadcastDelay;

        public int Port {
            get {
                lock (portLock) {
                    return _port;
                }
            }
            set {
                lock (portLock) {
                    _port = value;
                } 
            }
        }

        private readonly Object newConnectionEventLock = new object();
        public delegate void NewConnectionEventHandler(IPAddress client);
        private event NewConnectionEventHandler _NewConnectionEvent;
        public event NewConnectionEventHandler NewConnectionEvent {
            add {
                lock (newConnectionEventLock) {
                    _NewConnectionEvent += value;
                }
            }
            remove {
                lock (newConnectionEventLock) {
                    _NewConnectionEvent -= value;
                }
            }
        }

        private UdpClient sender;
        private UdpClient receiver;

        private Thread senderThread;
        private Thread receiverThread;

        private readonly Object stopLock = new object();
        private bool _isStopped = false;
        public bool IsStopped {
            get {
                lock (stopLock) {
                    return _isStopped;   
                }
            }
            set {
                lock (stopLock) {
                    _isStopped = value;
                }
            }
        }

        public UdpService(IPAddress broadcastIp, int port, int broadcastDelay) {
            ipEndPoint = new IPEndPoint(broadcastIp, port);
            _port = port;
            this.broadcastDelay = broadcastDelay;

            
        }

        public void Start() {
            IsStopped = false;

            receiver = new UdpClient(Port);
            sender = new UdpClient();

            StartReceiver();
            StartSender();
        }

        public void Stop() {
            IsStopped = true;

            senderThread.Interrupt();
            receiverThread.Interrupt();

            sender.Close();
            receiver.Close();
        }

        private void StartSender() {
            senderThread = new Thread(SenderProc);
            senderThread.Start();
        }

        private void SenderProc() {
            while (!IsStopped) {
                try {
                    sender.Send(new byte[] {1}, 1, ipEndPoint);
                    Thread.Sleep(broadcastDelay);
                }
                catch (ThreadInterruptedException e) {
                }
            }
        }

        private void StartReceiver() {
            receiverThread = new Thread(ReceiverProc);
            receiverThread.Start();
        }

        private void ReceiverProc() {
            var remote = new IPEndPoint(IPAddress.Any, Port);
            while (!IsStopped) {
                try {
                    receiver.Receive(ref remote);
                    if(_NewConnectionEvent != null)
                        _NewConnectionEvent(remote.Address);
                }
                catch (ThreadInterruptedException e) {
                }
                catch (SocketException e) {
                }
            }
        }
    }
}

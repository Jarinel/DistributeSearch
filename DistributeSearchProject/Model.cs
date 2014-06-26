using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DistributeSearchProject.Remoting;
using DistributeSearchProject.Search;
using Search.TcpSend;

namespace DistributeSearchProject
{
    class Model {
        private ConcurrentDictionary<IPAddress, DateTime> hosts = new ConcurrentDictionary<IPAddress, DateTime>();
        private ConcurrentDictionary<string, FileInformation> searchResult = new ConcurrentDictionary<string, FileInformation>();

        private ConcurrentBag<string> actualHosts = new ConcurrentBag<string>(); 

        private DistributeSearch searcher;

        public delegate void UpdateHostsEventHandler(ICollection<IPAddress> hosts);
        public event UpdateHostsEventHandler UpdateHostsEvent;

        public delegate void UpdateSearchResultEventHandler(string result);
        public event UpdateSearchResultEventHandler UpdateSearchResultEvent;

//        public delegate void SearchFinishedEventHandler();
//        public event SearchFinishedEventHandler SearchFinishedEvent;

        public delegate void ModelClosingEventHandler();
        public event ModelClosingEventHandler ModelClosingEvent;
        
        private object closeLock = new object();
        private bool _Closed = false;
        public bool Closed {
            get {
                lock (closeLock) {
                    return _Closed;
                }
            }
            set {
                lock (closeLock) {
                    _Closed = value;
                }
            }
        }

        private object uniqueLock = new object();
        private long _Unique;

        public long Unique {
            get {
                lock (uniqueLock) {
                    return _Unique;
                }
            }
            set {
                lock (uniqueLock) {
                    _Unique = value;
                }
            }
        }

        public enum MainState
        {
            Idle,
            ReadyToSearch,
            Searching,
            SearchInitiator
        }

        private readonly object stateLock = new object();
        private MainState _state;

        public MainState State {
            get {
                lock (stateLock) {
                    return _state;
                }
            }
            set {
                lock (stateLock) {
                    _state = value;
                }
            }
        }

        private Thread hostRemoverThread;

        public Model() {
            hostRemoverThread = new Thread(RemoveHostsProc);
            hostRemoverThread.Start();

            State = MainState.Idle;
        }

        public void Close() {
            Closed = true;

            hostRemoverThread.Interrupt();
            if(ModelClosingEvent != null)
                ModelClosingEvent();
        }

        public long SearchResolve(long unique) {
            if (State == MainState.Idle || State == MainState.Searching) {
                State = MainState.ReadyToSearch;
            }

            if (State == MainState.SearchInitiator) {
                if (Unique - unique < 0) {
                    State = MainState.ReadyToSearch;
                }    
            }

            return Unique;
        }

        public void ClearSearchResults() {
            searchResult.Clear();
        }

        public List<string> GetActualHosts() {
            return new List<string>(actualHosts);
        } 

        public List<string> GetHosts() {
            return new List<string>(hosts.Keys.Select(x => x.ToString()));
        }

        public void SetActualHosts(List<string> list) {
            actualHosts = new ConcurrentBag<string>(list);
        }

        public void FindFiles(String data) {
            new Thread(() => {
                State = MainState.Searching;

                searcher = new DistributeSearch();
                searcher.ListUpdate += AddResult;

                State = MainState.Searching;
                //Stop search threads if close before search finished
                ModelClosingEvent += searcher.StopSearch;

                Log.WriteInfo("Начат поиск в папке " + Settings.DIRECTORY);
                searcher.FindFiles(data, Settings.DIRECTORY, Settings.BUFFER_SIZE);
                Log.WriteInfo("Поиск завершен");

                State = MainState.Idle;
                //Search finished before program close
                ModelClosingEvent -= searcher.StopSearch;

//                if (SearchFinishedEvent != null)
//                    SearchFinishedEvent();
            }).Start();
        }

        public void StopFinding() {
            var stopSearchThread = new Thread(() => {
                if (State == MainState.Idle)
                    return;

                try {
                    ModelClosingEvent -= searcher.StopSearch;
                }
                catch (ArgumentException e) {
                }

                if (searcher != null)
                    searcher.StopSearch();
            });
            stopSearchThread.Start();
            stopSearchThread.Join();
        }

        public void AddHost(IPAddress ipAddress) {
            if (hosts.ContainsKey(ipAddress))
                hosts[ipAddress] = DateTime.Now;
            else {
                hosts.TryAdd(ipAddress, DateTime.Now);

                Log.WriteInfo(ipAddress + " подключился");

                if (UpdateHostsEvent != null)
                    UpdateHostsEvent(hosts.Keys);
            }
        }

        public List<string> CollectActualHosts() {
            List<string> hosts = GetHosts();
            List<string> result = new List<string>();

            foreach (var host in hosts) {
                try {
                    var remoteHostProviderUrl = "tcp://" + host + ":" + Settings.REMOTING_SERVER_PORT + "/RemoteHostProvider";
                    var remoteHostProvider =
                        (RemoteHostProvider) Activator.GetObject(typeof (RemoteHostProvider), remoteHostProviderUrl);

                    var list = remoteHostProvider.GetHosts();
                    result = result.Union(list).ToList();
                }
                catch (SocketException e) {
                    Log.WriteWarning(e.Message);
                }
                catch (RemotingException e) {
                    Log.WriteWarning(e.Message);
                }
                catch (Exception e) {
                    Log.WriteException(e);
                }
            }

            return result;
        }

        public void SetActualHostsOnMachines(List<string> hosts) {
            foreach (var host in hosts) {
                try {
                    var remoteHostProviderUrl = "tcp://" + host + ":" + Settings.REMOTING_SERVER_PORT + "/RemoteHostProvider";
                    var remoteHostProvider =
                        (RemoteHostProvider)Activator.GetObject(typeof(RemoteHostProvider), remoteHostProviderUrl);
    
                    remoteHostProvider.SetActualHosts(hosts);
                }
                catch (SocketException e) {
                    Log.WriteWarning(e.Message);
                }
                catch (RemotingException e) {
                    Log.WriteWarning(e.Message);
                }
                catch (Exception e) {
                    Log.WriteException(e);
                }
            }
        }

        private void RemoveHostsProc() {
            while (!Closed) {
                try {
                    var now = DateTime.Now;

                    var toDelete = (from time in hosts where (now - time.Value).TotalMilliseconds > Settings.HOST_DELETE_TIME select time.Key).ToList();

                    foreach (var address in toDelete){
                        DateTime temp;
                        hosts.TryRemove(address, out temp);

                        Log.WriteInfo(address + " отключился");
                    }

                    if (UpdateHostsEvent != null && toDelete.Count > 0)
                        UpdateHostsEvent(hosts.Keys);
                    Thread.Sleep(Settings.HOST_UPDATE_TIME);
                }
                catch(ThreadInterruptedException e) {
                }
            }
        }

        private void SendResultToOthers(FileInformation file, string localHost) {
            var Hosts = actualHosts;
            foreach (var host in Hosts) {
                try {
                    if (host.Equals(localHost))
                        continue;

                    var url = "tcp://" + host + ":" + Settings.REMOTING_SERVER_PORT + "/RemoteAddResult";
                    var remote = (RemoteAddResult) Activator.GetObject(typeof (RemoteAddResult), url);
                    remote.AddResult(file, localHost);

                    Log.WriteInfo("Передаем файл " + file.name + " на хост " + host);
                }
                catch (SocketException e) {
                    //We just remove bad host from actualHosts
                    //From hosts it will be removed
//                    DateTime time;
//                    hosts.TryRemove(host, out time);
//                    new Thread(() => UpdateHostsEvent(hosts.Keys)).Start();
                    string data;
                    actualHosts.TryTake(out data);
                    Log.WriteInfo(data + " отключился от поиска");
                }
                //TODO: Concretize Remoting exception
                catch (RemotingException e) {
//                    DateTime time;
//                    hosts.TryRemove(host, out time);
//                    new Thread(() => UpdateHostsEvent(hosts.Keys)).Start();  
                    string data;
                    actualHosts.TryTake(out data);
                    Log.WriteInfo(data + " отключился от поиска");
                }
            }
        }

        public void AddResult(FileInformation file, string host) {
            SendResultToOthers(file, host);

            //Here lock may be needed
            if (searchResult.ContainsKey(file.name)) {
                if (searchResult[file.name].LastModifyTime.CompareTo(file.LastModifyTime) < 0) {
                    Log.WriteInfo("Файл " + file.name + " [" + searchResult[file.name].directory + "] с хоста " + searchResult[file.name].hostIp + " заменен версией с хоста " + file.hostIp + " из папки " + file.directory);

                    searchResult[file.name] = file;
                }
            } else {
                searchResult.TryAdd(file.name, file);
                if (UpdateSearchResultEvent != null)
                    UpdateSearchResultEvent(file.name);
                Log.WriteInfo("Файл " + file.name + " добавлен, хост [" + host + "]");
            }
        }

        public void AddResultByRemote(FileInformation file, string host) {
            if (searchResult.ContainsKey(file.name)) {
                if (searchResult[file.name].LastModifyTime.CompareTo(file.LastModifyTime) < 0) {
                    Log.WriteInfo("Файл " + file.name + " [" + searchResult[file.name].directory + "] с хоста " + searchResult[file.name].hostIp + " заменен версией с хоста " + file.hostIp + " из папки " + file.directory);

                    searchResult[file.name] = file;
                }
            } else {
                searchResult.TryAdd(file.name, file);
                if (UpdateSearchResultEvent != null)
                    UpdateSearchResultEvent(file.name);
                Log.WriteInfo("Файл " + file.name + " добавлен, хост [" + host + "]");
            }
        }

        public FileInformation GetFileInformation(string fileName) {
            return searchResult[fileName];
        }
    }
}

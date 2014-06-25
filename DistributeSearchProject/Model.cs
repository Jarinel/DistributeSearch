﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
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

        public enum MainState
        {
            IDLE,
            WAITING,
            SEARCHING
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

            State = MainState.IDLE;
        }

        public void Close() {
            Closed = true;

            hostRemoverThread.Interrupt();
            if(ModelClosingEvent != null)
                ModelClosingEvent();
        }

        public void ClearSearchResults() {
            searchResult.Clear();
        }

        public List<string> GetHosts() {
            return new List<string>(hosts.Keys.Select(x => x.ToString()));
        } 

        public void FindFiles(String data) {
            new Thread(() => {
                searcher = new DistributeSearch();
                searcher.ListUpdate += AddResult;

                State = MainState.SEARCHING;
                //Stop search threads if close before search finished
                ModelClosingEvent += searcher.StopSearch;

                searcher.FindFiles(data, Settings.DIRECTORY, Settings.BUFFER_SIZE);

                State = MainState.IDLE;
                //Search finished before program close
                ModelClosingEvent -= searcher.StopSearch;

//                if (SearchFinishedEvent != null)
//                    SearchFinishedEvent();
            }).Start();
        }

        public void StopFinding() {
            var stopSearchThread = new Thread(() => {
                if (State == MainState.IDLE)
                    return;

                ModelClosingEvent -= searcher.StopSearch;

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

                if (UpdateHostsEvent != null)
                    UpdateHostsEvent(hosts.Keys);
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
            var Hosts = hosts.Keys;
            foreach (var host in Hosts) {
                try {
                    if (host.ToString().Equals(localHost))
                        continue;

                    var url = "tcp://" + host + ":" + Settings.REMOTING_SERVER_PORT + "/RemoteAddResult";
                    var remote = (RemoteAddResult) Activator.GetObject(typeof (RemoteAddResult), url);
                    remote.AddResult(file, localHost);
                }
                catch (SocketException e) {
                    DateTime time;
                    hosts.TryRemove(host, out time);
                    new Thread(() => UpdateHostsEvent(hosts.Keys)).Start();
                }
                //TODO: Concretize Remoting exception
                catch (RemotingException e) {
                    DateTime time;
                    hosts.TryRemove(host, out time);
                    new Thread(() => UpdateHostsEvent(hosts.Keys)).Start();    
                }
            }
        }

        public void AddResult(FileInformation file, string host) {
            SendResultToOthers(file, host);

            //Here lock may be needed
            if (searchResult.ContainsKey(file.name)) {
                if (searchResult[file.name].LastModifyTime.CompareTo(file.LastModifyTime) < 0) {
                    searchResult[file.name] = file;
                }
            } else {
                searchResult.TryAdd(file.name, file);
                if (UpdateSearchResultEvent != null)
                    UpdateSearchResultEvent(file.name);
            }
        }

        public void AddResultByRemote(FileInformation file, string host) {
            if (searchResult.ContainsKey(file.name)) {
                if (searchResult[file.name].LastModifyTime.CompareTo(file.LastModifyTime) < 0) {
                    searchResult[file.name] = file;
                }
            } else {
                searchResult.TryAdd(file.name, file);
                if (UpdateSearchResultEvent != null)
                    UpdateSearchResultEvent(file.name);
            }
        }

        public FileInformation GetFileInformation(string fileName) {
            return searchResult[fileName];
        }
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DistributeSearchProject.Remoting;
using DistributeSearchProject.Udp;
using Search.TcpSend;

namespace DistributeSearchProject
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private UdpService udpService;
        private RemotingServer remotingServer;
        private Model model;

        private object HostListBoxLock = new object();

        public enum DownloadState {
            IDLE,
            DOWNLOADING,
            UPLOADING
        }

//        private MainState mainState;

        private readonly object downloadStateLock = new object();
        private DownloadState _downloadState;

        public DownloadState downloadState {
            get {
                lock (downloadStateLock) {
                    return _downloadState;
                }
            }
            set {
                lock (downloadStateLock) {
                    _downloadState = value;
                }
            }
        }

        private delegate void ResetDownloadStateDelegate();
        private void ResetDownloadState() {
            downloadState = DownloadState.IDLE;
        }

        public MainWindow()
        {
            InitializeComponent();

            Directory.CreateDirectory(Settings.DOWNLOAD_DIRECTORY);
            Closing += ClosingHandler;

//            mainState = MainState.Idle;
            downloadState = DownloadState.IDLE;

            model = new Model();
            model.UpdateHostsEvent += HostListBoxUpdateHandler;
            model.UpdateSearchResultEvent += ResultListBoxUpdateHandler;

            RemoteSearch.LocalFindFiles                 += model.FindFiles;
            RemoteStopFinding.StopFindingFunction       += model.StopFinding;
            RemoteAddResult.LocalAddResult              += model.AddResultByRemote;
            RemoteHostProvider.GetHostsFunction         += model.GetHosts;
            RemoteHostProvider.SetActualHostsFunction   += model.SetActualHosts;
            RemoteSearchResolve.SearchResolveFunction   += model.SearchResolve;

            RemoteClearResults.ClearResultsFunction     += ClearResults;

            model.Unique = long.Parse(Settings.LOCAL_IP.ToString().Replace(".", ""));

            udpService = new UdpService(Settings.BROADCAST_IP, Settings.UDP_PORT, Settings.UDP_BROADCAST_DELAY);
            udpService.NewConnectionEvent += model.AddHost;
            udpService.Start();

            remotingServer = new RemotingServer(Settings.REMOTING_SERVER_PORT);
            remotingServer.Start();

            FindButton.Click += ClickHandler;
            DownloadButton.Click += DownloadButtonClickHandler;
            ResultListBox.SelectionChanged += ShowFileInfo;
        }

        private void ClosingHandler(object o, CancelEventArgs e) {
            udpService.Stop();
            udpService = null;

            model.Close();
        }

        private delegate void HostListBoxUpdateHandlerDelegate(ICollection<IPAddress> hosts);
        private void HostListBoxUpdateHandler(ICollection<IPAddress> hosts) {
            if (HostListBox.Dispatcher.CheckAccess()) {
                HostListBox.Items.Clear();

                foreach (var host in hosts) {
                    HostListBox.Items.Add(host);
                }
            } else {
                HostListBox.Dispatcher.Invoke(
                    DispatcherPriority.Normal,
                    new HostListBoxUpdateHandlerDelegate(HostListBoxUpdateHandler),
                    hosts
                );
            }
        }

        private delegate void ResultListBoxUpdateHandlerDelegate(string result);
        private void ResultListBoxUpdateHandler(string result) {
            try {
                if (ResultListBox.Dispatcher.CheckAccess()) {
                    ResultListBox.Items.Add(result);
                } else {
                    ResultListBox.Dispatcher.Invoke(
                        DispatcherPriority.Normal,
                        new ResultListBoxUpdateHandlerDelegate(ResultListBoxUpdateHandler),
                        result
                    );
                }
            }
            catch (ThreadInterruptedException e) {
            }
        }

        private delegate void ClearResultsDelegate();
        private void ClearResults() {
            if (ResultListBox.Dispatcher.CheckAccess()) {
                ResultListBox.Items.Clear();
                model.ClearSearchResults();
            } else {
                ResultListBox.Dispatcher.Invoke(
                    DispatcherPriority.Normal,
                    new ClearResultsDelegate(ClearResults)
                );
            }
        }

        private void PrepareSearch(List<string> hosts) {
            model.State = Model.MainState.SearchInitiator;

            foreach (var host in hosts) {
                var url = "tcp://" + host + ":" + Settings.REMOTING_SERVER_PORT + "/RemoteSearchResolve";
                var resolver = (RemoteSearchResolve) Activator.GetObject(typeof (RemoteSearchResolve), url);

                var unique = resolver.SearchResolve(model.Unique);
                if (model.Unique - unique < 0) {
                    model.State = Model.MainState.ReadyToSearch;
                    break;
                }
            }
        }

        private void ClickHandler(object sender, RoutedEventArgs eventArgs) {
            ClearResults();
            model.StopFinding();

            model.SetActualHostsOnMachines(model.CollectActualHosts());
//            List<string> hosts = model.GetHosts();
            List<string> hosts = model.GetActualHosts();

            PrepareSearch(hosts);
            if (model.State == Model.MainState.ReadyToSearch)
                return;

            foreach (var host in hosts) {
                try {
                    if (!Settings.LOCAL_IP.ToString().Equals(host)) {
                        var stopSearchUrl = "tcp://" + host + ":" + Settings.REMOTING_SERVER_PORT + "/RemoteStopFinding";
                        RemoteStopFinding stopFinding = (RemoteStopFinding) Activator.GetObject(typeof (RemoteStopFinding), stopSearchUrl);
                        stopFinding.StopFinding();

                        var clearUrl = "tcp://" + host + ":" + Settings.REMOTING_SERVER_PORT + "/RemoteClearResults";
                        RemoteClearResults clearResults = (RemoteClearResults) Activator.GetObject(typeof (RemoteClearResults), clearUrl);
                        clearResults.ClearResults();
                    }

                    var url = "tcp://" + host + ":" + Settings.REMOTING_SERVER_PORT + "/RemoteSearch";
                    RemoteSearch remote = (RemoteSearch) Activator.GetObject(typeof(RemoteSearch), url);
                    var data = FindPatternTextBox.Text;
                    remote.FindFiles(data);
                }
                catch (SocketException e) {
                }
                //TODO: Concretize Remoting exception (here too)
                catch (RemotingException e) {
                }
            }
        }

        private void DownloadButtonClickHandler(object sender, RoutedEventArgs eventArgs) {
            if (ResultListBox.SelectedItem == null)
                return;

            if(downloadState != DownloadState.IDLE)
                return; //Or send some message to user

            var fileName = ResultListBox.SelectedItem.ToString();
            var file = model.GetFileInformation(fileName);
            
            if(file.hostIp.Equals(Settings.LOCAL_IP.ToString()))
                return; //Already have this file
            
            ResetDownloadStateDelegate callBack = ResetDownloadState;

            downloadState = DownloadState.DOWNLOADING;
            
            var listenerThread = new Thread(() => {
                TcpFileReceiver tcpReceiver = new TcpFileReceiver(Settings.LOCAL_IP.ToString(), Settings.DOWNLOAD_PORT, Settings.TCP_TRANSFER_BUFFER);
                tcpReceiver.ReceiveFile(Settings.DOWNLOAD_DIRECTORY);
                callBack();
            });
            listenerThread.IsBackground = true;
            listenerThread.Start();
            
            var senderThread = new Thread(() => {
                try {
                    var url = "tcp://" + file.hostIp + ":" + Settings.REMOTING_SERVER_PORT + "/TcpFileSender";
                    TcpFileSender tcpSender = (TcpFileSender) Activator.GetObject(typeof(TcpFileSender), url);
                    tcpSender.SendFile(Settings.LOCAL_IP.ToString(), Settings.DOWNLOAD_PORT, file.fullName, Settings.TCP_TRANSFER_BUFFER);
                }
                catch (SocketException e) {
                }
            });
            senderThread.IsBackground = true;
            senderThread.Start();
        }

        private void ShowFileInfo(object sender, SelectionChangedEventArgs eventArgs) {
            if(ResultListBox.SelectedItem == null)
                return;

            var fileName = ResultListBox.SelectedItem.ToString();
            var file = model.GetFileInformation(fileName);

            var sb = new StringBuilder();
            sb.Append("Подробная информация о файле: " + fileName + "\n");
            sb.Append("IP хоста с файлом: " + file.hostIp + "\n");
            sb.Append("Путь на хосте до файла: " + file.directory + "\n");
            sb.Append("Дата измения файла: " + file.LastModifyTime);

            FileInfoLabel.Content = sb.ToString();
        }
    }
}

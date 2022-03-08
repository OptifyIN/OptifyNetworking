using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Linq;
using System.Diagnostics;

namespace OptifyNetworking
{
    /// <summary>
    /// An instance of an OptifyNetworking server
    /// </summary>
    public class ONServer
    {
        //Private variables
        private TcpListener server = null;
        private Thread requestthread;
        private Thread clientpollthread;
        private Thread singledatathread;
        private bool serverstarted = false;

        //Public variables
        private int _port = 0;
        /// <summary>
        /// The port where the server listen
        /// </summary>
        public int Port { get { return _port; } set { if (!serverstarted) _port = value; else throw new Exception("You cannot change the server port while it's started. Error code : ON006"); } }

        private IPAddress _ip;
        /// <summary>
        /// The local IP adress
        /// </summary>
        public IPAddress IP { get { return _ip; } set { if (!serverstarted) _ip = value; else throw new Exception("You cannont change the server ip while it's started. Error code : ON007"); } }

        /// <summary>
        /// The number of clients connected to the server
        /// </summary>
        public int ClientConnectedCount { get { return _clientlist.Count; } }

        /// <summary>
        /// Max packet size, in byte. If you get over the limit, bytes will be ignored/carried over the next packet.
        /// Default : 32767 bytes
        /// </summary>
        public int PacketSize { get; set; } = short.MaxValue;

        /// <summary>
        /// The encoding used by the server. Customs encodings are supported. Default : UTF8
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.UTF8;

        private List<ClientInfo> _clientlist = new List<ClientInfo>();
        /// <summary>
        /// Return a list containing the ClientInfo of all the clients connected
        /// </summary>
        public List<ClientInfo> ClientList { get { return _clientlist; } }

        /// <summary>
        /// Will clear the null bytes at the end of the recived packets. Can increase delay, lag if the packet lenght (without null bytes) is long
        /// </summary>
        public bool ClearNullBytes { get; set; } = true;

        /// <summary>
        /// Time between each poll to the client. Note : If you need real time disconnect event, set it to 0, but set it to lower will probably (need test) increase lag and delay
        /// </summary>
        public int PollRate { get; set; } = 100;

        public bool SingleThread = false;

        //Events
        public delegate void DataReceivedEventHandler(object sender, Message e);
        public event DataReceivedEventHandler DataReceived;
        protected virtual void RaiseDataReceived(byte[] data,string Text,TcpClient ci)
        {
            DataReceived?.Invoke(this, new Message(data, Text, ci, Encoding));
        }

        public delegate void ClientConnectedEventHandler(object sender, ClientConnectedEventArgs e);
        public event ClientConnectedEventHandler ClientConnected;
        protected virtual void RaiseClientConnected(ClientInfo ci)
        {
            ClientConnected?.Invoke(this, new ClientConnectedEventArgs(ci));
        }

        public delegate void ClientDisconnectedEventHandler(object sender, ClientDisconnectedEventArgs e);
        public event ClientDisconnectedEventHandler ClientDisconnected;
        protected virtual void RaiseClientDisconnected(ClientInfo ci)
        {
            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(ci));
        }

        //Methods
        /// <summary>
        /// ONServer constructor
        /// </summary>
        /// <param name="IP">The local IP adress</param>
        /// <param name="Port">The port where the server listen</param>
        public ONServer(string IP,int Port)
        {
            _port = Port;
            _ip = IPAddress.Parse(IP);
        }
        /// <summary>
        /// ONServer constructor
        /// </summary>
        /// <param name="Port">The port where the server listen</param>
        public ONServer(int Port)
        {
            _port = Port;
            _ip = IPAddress.Any;
        }
        /// <summary>
        /// ONServer constructor
        /// </summary>
        public ONServer()
        {
            _ip = IPAddress.Any;
        }

        /// <summary>
        /// Start the server
        /// </summary>
        public void Start()
        {
            if (serverstarted) return;
            if(_port == 0)
            {
                Exception ex = new Exception("Can't start server without a port. Error code : ON001");
                throw ex;
            }
            if (_ip == null)
            {
                Exception ex = new Exception("Can't start server without an IP. Error code : ON008");
                throw ex;
            }
            server = new TcpListener(_ip, _port);
            server.Start();
            requestthread = new Thread(new ThreadStart(AcceptRequestThread));
            requestthread.Name = "ReqThread";
            requestthread.Start();
            clientpollthread = new Thread(new ThreadStart(ClientPollThread));
            clientpollthread.Name = "ClientPollThread";
            clientpollthread.Start();
            if (SingleThread)
            {
                singledatathread = new Thread(new ThreadStart(SingleDataThread));
                singledatathread.Start();
            }

            serverstarted = true;
        }

        public void Start(int Port)
        {
            if (serverstarted) return;
            _port = Port;
            if(_ip == null)
            {
                Exception ex = new Exception("Can't start server without an IP. Error code : ON008");
                throw ex;
            }
            server = new TcpListener(_ip, _port);
            server.Start();
            requestthread = new Thread(new ThreadStart(AcceptRequestThread));
            requestthread.Name = "ReqThread";
            requestthread.Start();
            clientpollthread = new Thread(new ThreadStart(ClientPollThread));
            clientpollthread.Name = "ClientPollThread";
            clientpollthread.Start();
            if (SingleThread)
            {
                singledatathread = new Thread(new ThreadStart(SingleDataThread));
                singledatathread.Start();
            }
                
            serverstarted = true;
        }

        public void Start(string IP ,int Port)
        {
            if (serverstarted) return;
            _port = Port;
            _ip = IPAddress.Parse(IP);
            server = new TcpListener(_ip, _port);
            server.Start();
            requestthread = new Thread(new ThreadStart(AcceptRequestThread));
            requestthread.Name = "ReqThread";
            requestthread.Start();
            clientpollthread = new Thread(new ThreadStart(ClientPollThread));
            clientpollthread.Name = "ClientPollThread";
            clientpollthread.Start();
            if (SingleThread)
            {
                singledatathread = new Thread(new ThreadStart(SingleDataThread));
                singledatathread.Start();
            }

            serverstarted = true;
        }

        public void Stop()
        {
            if (!serverstarted) return;
            server.Stop();
            requestthread.Abort();
            clientpollthread.Abort();
            if (SingleThread)
                singledatathread.Abort();
            foreach (ClientInfo ci in _clientlist)
            {
                if(!SingleThread)
                    ci.DataThread.Abort();
                try
                {
                    ci.NetworkStream.Close();
                }
                catch { }
                ci.Client.Close();
            }
            _clientlist.Clear();
            serverstarted = false;
        }

        public void Broadcast(string Text)
        {
            foreach(ClientInfo ci in _clientlist)
            {
                ci.SendClient(Text,Encoding);
            }
        }

        public void Broadcast(byte[] Data)
        {
            foreach (ClientInfo ci in _clientlist)
            {
                ci.SendClient(Data);
            }
        }

        private bool IsSocketConnected(Socket s)
        {
            //https://stackoverflow.com/questions/2661764/how-to-check-if-a-socket-is-connected-disconnected-in-c
            bool part1 = s.Poll(1000, SelectMode.SelectRead);
            bool part2 = (s.Available == 0);
            if ((part1 && part2) || !s.Connected)
                return false;
            else
                return true;
        }

        private void AcceptRequestThread()
        {
            while (true)
            {
                if (SingleThread)
                {
                    TcpClient client = server.AcceptTcpClient();
                    NetworkStream ns = client.GetStream();
                    ClientInfo i = new ClientInfo();
                    IPEndPoint ipe = client.Client.RemoteEndPoint as IPEndPoint;
                    i.Name = "Client" + ipe.Address;
                    i.IP = ipe.Address;
                    i.NetworkStream = ns;
                    i.Client = client;
                    _clientlist.Add(i);
                    RaiseClientConnected(i);
                }
                else
                {
                    try
                    {
                        TcpClient client = server.AcceptTcpClient();
                        NetworkStream ns = client.GetStream();
                        ClientInfo i = new ClientInfo();
                        IPEndPoint ipe = client.Client.RemoteEndPoint as IPEndPoint;
                        i.Name = "Client" + ipe.Address;
                        i.IP = ipe.Address;
                        i.NetworkStream = ns;
                        i.Client = client;

                        //ONServer s = this;
                        Thread datathread = new Thread(() => ClientDataThread(i));
                        datathread.Name = "DataThread" + i.Name;
                        i.DataThread = datathread;
                        datathread.Start();
                        _clientlist.Add(i);
                        RaiseClientConnected(i);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }

        private void ClientPollThread()
        {
            while (true)
            {
                try
                {
                    foreach (ClientInfo ci in _clientlist)
                    {
                        if (ci.Client == null) goto skip;
                        if (!IsSocketConnected(ci.Client.Client)) //Client not responding / disconnected
                        {
                            //Trigger event client disconnected
                            RaiseClientDisconnected(ci);
                            //Disconnect
                            _clientlist.Remove(ci);
                            ci.DataThread.Abort();
                            ci.NetworkStream.Close();
                            ci.NetworkStream.Flush();
                            ci.Client.Close();
                        }
                    skip:;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                if(PollRate != 0)
                    Thread.Sleep(PollRate);
            }
        }

        private void ClientDataThread(ClientInfo i)
        {
            while (true)
            {
                byte[] bytes = new byte[PacketSize]; //packet size / default 32Kio (32767o)
                if (i.NetworkStream.Read(bytes, 0, bytes.Length) != 0)
                {
                    if (ClearNullBytes)
                        bytes = bytes.TakeWhile(x => x != 0).ToArray();
                    RaiseDataReceived(bytes, Encoding.GetString(bytes), i.Client);
                    i.NetworkStream.Flush();
                }
            }
        }

        private void SingleDataThread()
        {
            while (true)
            {
                try
                {
                    foreach (ClientInfo c in _clientlist)
                    {
                        if (c == null)
                            continue;
                        if (!c.Recieving && c.NetworkStream.CanRead)
                        {
                            c.Buffer = new byte[PacketSize];
                            c.Recieving = true;
                            c.NetworkStream.BeginRead(c.Buffer, 0, c.Buffer.Length, new AsyncCallback(SingleRead), c);
                        }
                    }
                }
                catch { }
            }
        }

        private void SingleRead(IAsyncResult ar)
        {
            ClientInfo c = (ClientInfo)ar.AsyncState;
            int k = c.NetworkStream.EndRead(ar);
            byte[] bytes = c.Buffer;
            if(k != 0)
            {
                if (ClearNullBytes)
                    bytes = bytes.TakeWhile(x => x != 0).ToArray();
                RaiseDataReceived(bytes, Encoding.GetString(bytes), c.Client);
                c.NetworkStream.Flush();
                c.Recieving = true;
            }
        }

    }

    public class ONClient
    {
        //Privates variables
        private NetworkStream ns;
        private Thread datathread;
        private Thread serverpollthread;

        //Public variables
        private TcpClient _tcpclient;
        public TcpClient Client { get { return _tcpclient; } }

        private bool _clientconnected;
        public bool ClientConnected { get { return _clientconnected; } }

        private int _port = -1;
        public int Port { get { return _port; } set { if (!_clientconnected) _port = value; else throw new Exception("You cannot change the client port while connected. Error code : ON002"); } }

        private string _ip;
        public string IP { get { return _ip; } set { if (!_clientconnected) _ip = value; else throw new Exception("You cannont change the client ip while connected. Error code : ON003"); }  }

        public Encoding Encoding { get; set; } = Encoding.UTF8;

        public int PacketSize { get; set; } = short.MaxValue;

        /// <summary>
        /// Will clear the null bytes at the end of the recived packets. Can increase delay, lag if the packet lenght (without null bytes) is long
        /// </summary>
        public bool ClearNullBytes { get; set; } = true;

        /// <summary>
        /// Time between each poll to the client. Note : If you need real time disconnect event, set it to 0, but set it to lower will probably (need test) increase lag and delay
        /// </summary>
        public int PollRate { get; set; } = 100;

        //Events
        public delegate void DataReceivedEventHandler(object sender, Message e);
        public event DataReceivedEventHandler DataReceived;
        protected virtual void RaiseDataReceived(byte[] data, string Text)
        {
            DataReceived?.Invoke(this, new Message(data, Text, Client, Encoding));
        }

        public ONClient()
        {
            _tcpclient = new TcpClient();
        }

        public ONClient(string ip, int port, bool connect = false)
        {
            _ip = ip;
            _port = port;
            if (connect) Connect();
        }

        public ONClient(IPEndPoint ipe, bool connect = false)
        {
            _ip = ipe.Address.ToString();
            _port = ipe.Port;
            if (connect) Connect();
        }

        /// <summary>
        /// Require the ONClient instance to have an ip and a port already set
        /// </summary>
        public void Connect()
        {
            if (_clientconnected) return;
            if (_ip == null) throw new Exception("The hostname cannot be null. Error code : ON004");
            if (_port <= 0) throw new Exception("The port cannot be <= 0. Error code : ON005");
            _tcpclient = new TcpClient();
            _tcpclient.Connect(_ip,_port);
            ns = _tcpclient.GetStream();
            datathread = new Thread(new ThreadStart(DataThread));
            datathread.Start();
            serverpollthread = new Thread(new ThreadStart(ServerPollThread));
            serverpollthread.Start();
            _clientconnected = true;
        }

        public void Connect(string ip, int port)
        {
            if (_clientconnected) return;
            _ip = ip;
            _port = port;
            if (_ip == null) throw new Exception("The hostname cannot be null. Error code : ON004");
            if (_port <= 0) throw new Exception("The port cannot be <= 0. Error code : ON005");
            _tcpclient = new TcpClient();
            _tcpclient.Connect(_ip, _port);
            ns = _tcpclient.GetStream();
            datathread = new Thread(new ThreadStart(DataThread));
            datathread.Start();
            serverpollthread = new Thread(new ThreadStart(ServerPollThread));
            serverpollthread.Start();
            _clientconnected = true;
        }

        public void Disconnect()
        {
            if (!_clientconnected) return;
            ns.Close();
            _tcpclient.Close();
            datathread.Abort();
            serverpollthread.Abort();
            _clientconnected = false;
        }

        public void Write(string Text)
        {
            byte[] b = Encoding.GetBytes(Text);
            ns.Write(b, 0, b.Length);
        }

        public void Write(byte[] Data)
        {
            ns.Write(Data, 0, Data.Length);
        }

        public string WriteAndGetReply(string Text, int Timeout)
        {
            byte[] b = WriteAndGetReply(Encoding.GetBytes(Text), Timeout);
            return Encoding.GetString(b);
        }

        Stopwatch s = new Stopwatch();
        private byte[] wlgrbuffer = new byte[0];
        public byte[] WriteAndGetReply(byte[] Data, int Timeout)
        {
            wlgrbuffer = new byte[0];
            Write(Data);
            
            s.Start();
            while (wlgrbuffer.SequenceEqual(new byte[0]) && s.ElapsedMilliseconds <= Timeout)
            {

            }
            s.Stop();
            s.Reset();
            if (wlgrbuffer.SequenceEqual(new byte[0]))
            {
                return new byte[0];
            }
            else
            {
                return wlgrbuffer;
            }
        }

        private void DataThread()
        {
            while (true)
            {
                byte[] bytes = new byte[PacketSize]; //packet size / default 32Kio (32767o)
                if (ns.Read(bytes, 0, bytes.Length) != 0)
                {
                    if(ClearNullBytes)
                        bytes = bytes.TakeWhile(x => x != 0).ToArray();
                    if(s.IsRunning && s.ElapsedMilliseconds <= 1000)
                    {
                        wlgrbuffer = bytes;
                        goto suite;
                    }
                    RaiseDataReceived(bytes, Encoding.GetString(bytes));
                suite:;
                    ns.Flush();
                }
            }
        }

        private void ServerPollThread()
        {
            while (true)
            {
                try
                {
                    if (_tcpclient.Client == null) goto skip;
                    if (!IsSocketConnected(_tcpclient.Client)) //Client not responding / disconnected
                    {
                        //Disconnect
                        Disconnect();
                    }
                skip:;

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                if (PollRate != 0)
                    Thread.Sleep(PollRate);
            }
        }
        private bool IsSocketConnected(Socket s)
        {
            //https://stackoverflow.com/questions/2661764/how-to-check-if-a-socket-is-connected-disconnected-in-c
            bool part1 = s.Poll(1000, SelectMode.SelectRead);
            bool part2 = (s.Available == 0);
            if ((part1 && part2) || !s.Connected)
                return false;
            else
                return true;
        }
    }

    #region Events
    public class Message
    {
        public Message(byte[] data,string text,TcpClient tc,Encoding enc)
        {
            Data = data;
            Text = text;
            TcpClient = tc;
            ec = enc;
        }
        public byte[] Data { get; }
        public string Text { get; }
        public TcpClient TcpClient { get; }
        private Encoding ec;

        public void Reply(string Text)
        {
            if (string.IsNullOrEmpty(Text)) throw new Exception("Cannot reply a blank string. Error code : ON005");
            Reply(ec.GetBytes(Text));
        }

        public void Reply(byte[] Data)
        {
            TcpClient.GetStream().Write(Data, 0, Data.Length);
        }
    }

    public class ClientConnectedEventArgs
    {
        public ClientConnectedEventArgs(ClientInfo ci)
        {
            ClientInfo = ci;
        }
        public ClientInfo ClientInfo { get; }
    }

    public class ClientDisconnectedEventArgs
    {
        public ClientDisconnectedEventArgs(ClientInfo ci)
        {
            ClientInfo = ci;
        }
        public ClientInfo ClientInfo { get; }
    }
    #endregion

    public class ClientInfo
    {
        public string Name;
        public IPAddress IP;
        public NetworkStream NetworkStream;
        public Thread DataThread;
        public TcpClient Client;
        public byte[] Buffer;
        public bool Recieving;

        public void SendClient(string Text,Encoding Encoding)
        { 
            byte[] b = Encoding.GetBytes(Text);
            NetworkStream.Write(b, 0, b.Length);
        }

        public void SendClient(byte[] Data)
        {
            NetworkStream.Write(Data, 0, Data.Length);
        }
    }

    #region Idea
    /*
     * Do the summary for each func/var/class
     * Do for UDP & HTTP
     * Add custom compression to allow very small packet
     * Add a way for the server to send big packets in group of small packet to save user time of doing it
    */
    #endregion
}

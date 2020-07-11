using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Timers;
using System.Windows.Threading;

//TODO: Not implemenmted
// Receive infrared points from app
// Setting LEDs on app UDPmote
namespace UdpmoteLib
{
    public class Udpmote : IDisposable
    {
        public event Action<UdpmoteState> UdpmoteChanged;
        public event Action<Dictionary<IPAddress, UdpmoteInfo>> AvailaibleUdpmotesChanged;
        public event Action<Dictionary<IPAddress, UdpmoteInfo>> ConnectedUdpmotesChanged;
        public event Action<UdpmoteInfo> UdpmoteConnected;
        public event Action<UdpmoteInfo> UdpmoteDisconnected;

        const int portAnswerBC = 4432;
        const int portBC = 4433;
        const int portData = 4434;
        const double intervalKeepAliveInMS = 300.0;
        const double intervalCheckBroadcastingMotesInMS = 300.0;

        public class State
        {
            public byte[] buffer = new byte[bufSize];
        }

        public Udpmote()
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);

            _socketBC = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socketBC.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
        }

        public void Connect()
        {
            var my_ip = GetLocalIPAddress();

            _socket.Bind(new IPEndPoint(IPAddress.Parse(my_ip), portData));
            _socket.BeginReceiveFrom(state.buffer, 0, bufSize, SocketFlags.None, ref epFrom, ReceiveData, state);


            _socketBC.Bind(new IPEndPoint(IPAddress.Any, portBC));
            _socketBC.BeginReceiveFrom(state.buffer, 0, bufSize, SocketFlags.None, ref epFromBC, ReceiveBroadcast, state);

            timerCleanning = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalCheckBroadcastingMotesInMS) };
            timerCleanning.Tick += TimerCleanning_Tick;
            timerCleanning.Start();

            timerKeepAlive = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalKeepAliveInMS) };
            timerKeepAlive.Tick += TimerKeepAlive_Tick;
            timerKeepAlive.Start();
        }

        public void Disconnect()
        {
        }

        #region Private

        private readonly Socket _socket, _socketBC;
        private const int bufSize = 8 * 1024;
        private readonly State state = new State();
        private EndPoint epFrom = new IPEndPoint(IPAddress.Any, portData);
        private EndPoint epFromBC = new IPEndPoint(IPAddress.Any, portBC);
        private DispatcherTimer timerCleanning;
        private DispatcherTimer timerKeepAlive;

        private readonly Dictionary<IPAddress, UdpmoteInfo> availableMotes = new Dictionary<IPAddress, UdpmoteInfo>();
        private readonly Dictionary<IPAddress, UdpmoteInfo> connectedMotes = new Dictionary<IPAddress, UdpmoteInfo>();

        private void TimerKeepAlive_Tick(object sender, EventArgs e)
        {
            var toDelete = new List<IPAddress>();
            foreach (var address in connectedMotes.Keys)
            {
                var ts = DateTime.Now - connectedMotes[address].latestData;
                if (ts.TotalSeconds > 1.0)
                {
                    toDelete.Add(address);
                    Console.WriteLine("LOST UDPMOTE");
                    UdpmoteDisconnected?.Invoke(connectedMotes[address]);
                }
                else
                {
                    //Console.WriteLine("ALIVE " + address.ToString());
                    byte[] buffer = Encoding.UTF8.GetBytes("KEEPALIVE#" + System.Environment.MachineName + "#" + connectedMotes[address].num.ToString());
                    _socketBC.SendTo(buffer, new IPEndPoint(address, portAnswerBC));
                    Console.WriteLine("SENDED KEEPALIVE: " + buffer.Length.ToString());
                }
            }
            foreach (var address in toDelete)
            {
                connectedMotes.Remove(address);
                ConnectedUdpmotesChanged?.Invoke(connectedMotes);
            }
        }

        private void TimerCleanning_Tick(object sender, EventArgs e)
        {
            var toDelete = new List<IPAddress>();
            foreach (var address in availableMotes.Keys)
            {
                var ts = DateTime.Now - availableMotes[address].latestBC;
                if (ts.TotalSeconds > 1.0)
                    toDelete.Add(address);
            }
            foreach (var address in toDelete)
            {
                Console.WriteLine("REMOVED FROM BC LIST: " + address.ToString());
                availableMotes.Remove(address);
            }

            if (toDelete.Count > 0)
                AvailaibleUdpmotesChanged?.Invoke(availableMotes);
        }

        private void ReceiveData(IAsyncResult data)
        {
            if (_socket == null) return;
            try
            {
                State so = (State)data.AsyncState;

                int bytes = _socket.EndReceiveFrom(data, ref epFrom);
                if (bytes != 27)
                    throw new ArgumentOutOfRangeException();

                var remoteIP = ((IPEndPoint)epFrom).Address;

                if (availableMotes.ContainsKey(remoteIP))
                {
                    if (availableMotes[remoteIP].latestData == DateTime.MinValue)
                    {
                        connectedMotes[remoteIP] = availableMotes[remoteIP];
                        ConnectedUdpmotesChanged?.Invoke(connectedMotes);
                        availableMotes.Remove(remoteIP);

                        connectedMotes[remoteIP].latestData = DateTime.Now;
                        UdpmoteConnected?.Invoke(connectedMotes[remoteIP]);
                    }
                }

                if (connectedMotes.ContainsKey(remoteIP))
                {
                    connectedMotes[remoteIP].latestData = DateTime.Now;
                    UdpmoteChanged?.Invoke(new UdpmoteState(so.buffer));
                }
                else
                    Console.WriteLine("ERROR: Unknown UDPmote! " + remoteIP.ToString());

                _socket.BeginReceiveFrom(so.buffer, 0, bufSize, SocketFlags.None, ref epFrom, ReceiveData, so);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void ReceiveBroadcast(IAsyncResult data)
        {
            if (_socketBC == null) return;
            try
            {
                State so = (State)data.AsyncState;

                int bytes = _socketBC.EndReceiveFrom(data, ref epFromBC);
                Console.WriteLine("RECEIVED BC: " + bytes.ToString());
                string name = Encoding.UTF8.GetString(so.buffer.ToList().GetRange(0, bytes).ToArray());
                //Console.WriteLine(name);

                // Console.WriteLine(bytes);
                bool hasChanged = false;
                var remoteIP = ((IPEndPoint)epFromBC).Address;
                if (availableMotes.ContainsKey(remoteIP))
                {
                    if (availableMotes[remoteIP].name != name)
                    {
                        hasChanged = true;
                        availableMotes[remoteIP].name = name;
                    }
                    availableMotes[remoteIP].latestBC = DateTime.Now;
                    availableMotes[remoteIP].latestData = DateTime.MinValue;
                }
                else
                {
                    hasChanged = true;
                    availableMotes[remoteIP] = new UdpmoteInfo(remoteIP, name, FreeNumMote(availableMotes, connectedMotes), false, DateTime.Now, DateTime.MinValue);
                }

                if (hasChanged)
                    AvailaibleUdpmotesChanged?.Invoke(availableMotes);

                byte[] buffer = Encoding.UTF8.GetBytes("BC#" + System.Environment.MachineName + "#" + availableMotes[remoteIP].num.ToString());
                _socketBC.SendTo(buffer, new IPEndPoint(remoteIP, portAnswerBC));

                Console.WriteLine("SENDED ANSWER BC: " + buffer.Length.ToString());

                _socketBC.BeginReceiveFrom(so.buffer, 0, bufSize, SocketFlags.None, ref epFromBC, ReceiveBroadcast, so);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private int FreeNumMote(Dictionary<IPAddress, UdpmoteInfo> availableMotes, Dictionary<IPAddress, UdpmoteInfo> connectedMotes)
        {
            int i = -1;
            while (true)
            {
                i++;
                bool isOk = true;
                foreach (var v in availableMotes.Values)
                {
                    if (v.num == i)
                    {
                        isOk = false;
                        break;
                    }
                }
                if (!isOk) continue;

                foreach (var v in connectedMotes.Values)
                {
                    if (v.num == i)
                    {
                        isOk = false;
                        break;
                    }
                }
                if (!isOk) continue;

                return i;
            }
        }

        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // close up our handles
            if (_socket != null)
                _socket.Close();
            if (disposing)
                Disconnect();
        }
        #endregion
    }
}

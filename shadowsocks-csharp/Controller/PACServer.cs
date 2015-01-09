using Shadowsocks.Model;
using Shadowsocks.Properties;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Shadowsocks.Controller
{
    class PACServer
    {
        public const int PORT = 8093;
        private const string DEFAULT_PAC_FILE = "pac.txt";
        private static Configuration config;

        Socket _listener;
        FileSystemWatcher watcher;

        public event EventHandler PACFileChanged;

        public void Start(Configuration configuration)
        {
            try
            {
                config = configuration;
                // Create a TCP/IP socket.
                _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                IPEndPoint localEndPoint = null;
                if (configuration.shareOverLan)
                {
                    localEndPoint = new IPEndPoint(IPAddress.Any, PORT);
                }
                else
                {
                    localEndPoint = new IPEndPoint(IPAddress.Loopback, PORT);
                }

                // Bind the socket to the local endpoint and listen for incoming connections.
                _listener.Bind(localEndPoint);
                _listener.Listen(100);
                _listener.BeginAccept(
                    new AsyncCallback(AcceptCallback),
                    _listener);

                WatchPacFile();
            }
            catch (SocketException)
            {
                _listener.Close();
                throw;
            }
        }

        public void Stop()
        {
            if (_listener != null)
            {
                _listener.Close();
                _listener = null;
            }
        }

        public string GetPacFilePath()
        {
            if (String.IsNullOrEmpty(pacFile))
            {
                pacFile = Path.Combine(Configuration.GetConfigPath(), DEFAULT_PAC_FILE);
            }
            return pacFile;
        }

        public string TouchPACFile()
        {
            if (File.Exists(GetPacFilePath()))
            {
                return pacFile;
            }
            else
            {
                FileManager.UncompressFile(pacFile, Resources.proxy_pac_txt);
                return pacFile;
            }
        }

        // we don't even use it
        static byte[] requestBuf = new byte[2048];

        public void AcceptCallback(IAsyncResult ar)
        {
            Socket listener = (Socket)ar.AsyncState;
            try
            {
                Socket conn = listener.EndAccept(ar);

                object[] state = new object[] {
                    conn,
                    requestBuf
                };

                conn.BeginReceive(requestBuf, 0, requestBuf.Length, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                try
                {
                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);
                }
                catch (ObjectDisposedException)
                {
                    // do nothing
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                }
            }
        }

        private string GetPACContent()
        {
            if (File.Exists(GetPacFilePath()))
            {
                return File.ReadAllText(pacFile, Encoding.UTF8);
            }
            else
            {
                return Utils.UnGzip(Resources.proxy_pac_txt);
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            object[] state = (object[])ar.AsyncState;

            Socket conn = (Socket)state[0];
            byte[] requestBuf = (byte[])state[1];
            try
            {
                int bytesRead = conn.EndReceive(ar);

                string pac = GetPACContent();

                IPEndPoint localEndPoint = (IPEndPoint)conn.LocalEndPoint;

                string proxy = GetPACAddress(requestBuf, localEndPoint);

                pac = pac.Replace("__PROXY__", proxy);

                if (bytesRead > 0)
                {
                    string text = String.Format(@"HTTP/1.1 200 OK
Server: Shadowsocks
Content-Type: application/x-ns-proxy-autoconfig
Content-Length: {0}
Connection: Close

", System.Text.Encoding.UTF8.GetBytes(pac).Length) + pac;
                    byte[] response = System.Text.Encoding.UTF8.GetBytes(text);
                    conn.BeginSend(response, 0, response.Length, 0, new AsyncCallback(SendCallback), conn);
                    Util.Utils.ReleaseMemory();
                }
                else
                {
                    conn.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                conn.Close();
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            Socket conn = (Socket)ar.AsyncState;
            try
            {
                conn.Shutdown(SocketShutdown.Send);
            }
            catch
            { }
        }

        private void WatchPacFile()
        {
            if (watcher != null)
            {
                watcher.Dispose();
            }
            watcher = new FileSystemWatcher(Path.GetDirectoryName(GetPacFilePath()));
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.Filter = DEFAULT_PAC_FILE;
            watcher.Changed += Watcher_Changed;
            watcher.Created += Watcher_Changed;
            watcher.Deleted += Watcher_Changed;
            watcher.Renamed += Watcher_Changed;
            watcher.EnableRaisingEvents = true;
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (PACFileChanged != null)
            {
                PACFileChanged(this, new EventArgs());
            }
        }

        private string GetPACAddress(byte[] requestBuf, IPEndPoint localEndPoint)
        {
            string proxy = "PROXY " + localEndPoint.Address + ":" + PolipoRunner.PORT + ";";
            //try
            //{
            //    string requestString = Encoding.UTF8.GetString(requestBuf);
            //    if (requestString.IndexOf("AppleWebKit") >= 0)
            //    {
            //        string address = "" + localEndPoint.Address + ":" + config.GetCurrentServer().local_port;
            //        proxy = "SOCKS5 " + address + "; SOCKS " + address + ";";
            //    }
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine(e);
            //}
            return proxy;
        }
    }
}

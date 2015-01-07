﻿using System.IO;
using Shadowsocks.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net.Sockets;

namespace Shadowsocks.Controller
{
    public class ShadowsocksController
    {
        // controller:
        // handle user actions
        // manipulates UI
        // interacts with low level logic

        private Thread _ramThread;

        private Local local;
        private PACServer pacServer;
        private Configuration _config;
        private PolipoRunner polipoRunner;
        private bool stopped = false;

        private bool _systemProxyIsDirty = false;

        public class PathEventArgs : EventArgs
        {
            public string Path;
        }

        public event EventHandler ConfigChanged;
        public event EventHandler EnableStatusChanged;
        public event EventHandler EnableGlobalChanged;
        public event EventHandler ShareOverLANStatusChanged;
        
        // when user clicked Edit PAC, and PAC file has already created
        public event EventHandler<PathEventArgs> PACFileReadyToOpen;

        public event ErrorEventHandler Errored;

        public ShadowsocksController()
        {
            _config = Configuration.Load();
        }

        public void Start()
        {
            Reload();
        }

        protected void ReportError(Exception e)
        {
            if (Errored != null)
            {
                Errored(this, new ErrorEventArgs(e));
            }
        }

        public Server GetCurrentServer()
        {
            return _config.GetCurrentServer();
        }

        // always return copy
        public Configuration GetConfiguration()
        {
            return Configuration.Load();
        }

        public void SaveProxyConfigs(Configuration config)
        {
            _config.pacPort = config.pacPort;
            _config.httpPort = config.httpPort;
            SaveServers(config.configs);
        }

        public void SaveServers(List<Server> servers)
        {
            _config.configs = servers;
            SaveConfig(_config);
        }

        public void ToggleEnable(bool enabled)
        {
            _config.enabled = enabled;
            UpdateSystemProxy();
            SaveConfig(_config);
            if (EnableStatusChanged != null)
            {
                EnableStatusChanged(this, new EventArgs());
            }
        }

        public void ToggleGlobal(bool global)
        {
            _config.global = global;
            UpdateSystemProxy();
            SaveConfig(_config);
            if (EnableGlobalChanged != null)
            {
                EnableGlobalChanged(this, new EventArgs());
            }
        }

        public void ToggleShareOverLAN(bool enabled)
        {
            _config.shareOverLan = enabled;
            SaveConfig(_config);
            if (ShareOverLANStatusChanged != null)
            {
                ShareOverLANStatusChanged(this, new EventArgs());
            }
        }

        public void SelectServerIndex(int index)
        {
            _config.index = index;
            SaveConfig(_config);
        }

        public void Stop()
        {
            if (stopped)
            {
                return;
            }
            stopped = true;
            if (local != null)
            {
                local.Stop();
            }
            if (polipoRunner != null)
            {
                polipoRunner.Stop();
            }
            if (_config.enabled)
            {
                SystemProxy.Disable();
            }
        }

        public void TouchPACFile()
        {
            string pacFilename = pacServer.TouchPACFile();
            if (PACFileReadyToOpen != null)
            {
                PACFileReadyToOpen(this, new PathEventArgs() { Path = pacFilename });
            }
        }

        public string GetQRCodeForCurrentServer()
        {
            Server server = GetCurrentServer();
            string parts = server.method + ":" + server.password + "@" + server.server + ":" + server.server_port;
            string base64 = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(parts));
            return "ss://" + base64;
        }

        protected void Reload()
        {
            // some logic in configuration updated the config when saving, we need to read it again
            _config = Configuration.Load();

            if (polipoRunner == null)
            {
                polipoRunner = new PolipoRunner();
            }
            if (pacServer == null)
            {
                pacServer = new PACServer();
                pacServer.PACFileChanged += pacServer_PACFileChanged;
            }

            pacServer.Stop();

            if (local != null)
            {
                local.Stop();
            }

            // don't put polipoRunner.Start() before pacServer.Stop()
            // or bind will fail when switching bind address from 0.0.0.0 to 127.0.0.1
            // though UseShellExecute is set to true now
            // http://stackoverflow.com/questions/10235093/socket-doesnt-close-after-application-exits-if-a-launched-process-is-open
            polipoRunner.Stop();

            int bindingPort = _config.httpPort;
            try
            {
                // Start polipo (HTTP proxy) if and only if configured port is in range
                // To disable HTTP proxy, user could set port to 0
                if (bindingPort > 0 && bindingPort <= 65535)
                {
                    // No exception will be thrown here as forked polipo process just exits with delay
                    polipoRunner.Start(_config);
                }
                else
                {
                    if (_config.enabled)
                    {
                        throw new Exception(I18N.GetString("HTTP proxy port out of range"));
                    }
                }

                // Start sslocal SOCKS5 proxy
                bindingPort = _config.configs[_config.index].local_port;
                local = new Local(_config);
                local.Start();

                // Start PAC server if and only if proxy is enabled and HTTP proxy is also running
                bindingPort = _config.pacPort;
                if (_config.enabled && polipoRunner.isRunning())
                {
                    pacServer.Start(_config);
                }
            }
            catch (Exception e)
            {
                e = WrapSocketAccessDeniedException(e, bindingPort);
                Logging.LogUsefulException(e);
                ReportError(e);
            }

            if (ConfigChanged != null)
            {
                ConfigChanged(this, new EventArgs());
            }

            UpdateSystemProxy();
            Util.Util.ReleaseMemory();
        }

        private Exception WrapSocketAccessDeniedException(Exception ex, int portNumber)
        {
            // translate Microsoft language into human language
            // i.e. An attempt was made to access a socket in a way forbidden by its access permissions => Port already in use
            if (ex is SocketException)
            {
                SocketException se = (SocketException) ex;
                if (se.SocketErrorCode == SocketError.AccessDenied)
                {
                    ex = new Exception(String.Format(I18N.GetString("Port {0} already in use"), portNumber), ex);
                }
            }
            return ex;
        }

        protected void SaveConfig(Configuration newConfig)
        {
            Configuration.Save(newConfig);
            Reload();
        }


        private void UpdateSystemProxy()
        {
            if (_config.enabled)
            {
                SystemProxy.Enable(_config);
                _systemProxyIsDirty = true;
            }
            else
            {
                // only switch it off if we have switched it on
                if (_systemProxyIsDirty)
                {
                    SystemProxy.Disable();
                    _systemProxyIsDirty = false;
                }
            }
        }

        private void pacServer_PACFileChanged(object sender, EventArgs e)
        {
            UpdateSystemProxy();
        }

        private void StartReleasingMemory()
        {
            _ramThread = new Thread(new ThreadStart(ReleaseMemory));
            _ramThread.IsBackground = true;
            _ramThread.Start();
        }

        private void ReleaseMemory()
        {
            while (true)
            {
                Util.Util.ReleaseMemory();
                Thread.Sleep(30 * 1000);
            }
        }
    }
}

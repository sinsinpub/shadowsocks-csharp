﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using SimpleJson;
using Shadowsocks.Controller;
using System.Text.RegularExpressions;

namespace Shadowsocks.Model
{
    [Serializable]
    public class Server
    {
        public string server;
        public int server_port;
        public int local_port;
        public string password;
        public string method;
        public string remarks;

        public string FriendlyName()
        {
            if (string.IsNullOrEmpty(server))
            {
                return I18N.GetString("New server");
            }
            if (string.IsNullOrEmpty(remarks))
            {
                return server + ":" + server_port;
            }
            else
            {
                return remarks + " (" + server + ":" + server_port + ")";
            }
        }

        public Server()
        {
            this.server = "";
            this.server_port = 8388;
            this.local_port = 1080;
            this.method = "aes-256-cfb";
            this.password = "";
            this.remarks = "";
        }

        public Server(string ssURL) : this()
        {
            string[] r1 = Regex.Split(ssURL, "ss://", RegexOptions.IgnoreCase);
            string base64 = r1[1].ToString();
            byte[] bytes = null;
            for (var i = 0; i < 3; i++) {
                try
                {
                    bytes = System.Convert.FromBase64String(base64);
                }
                catch (FormatException)
                {
                    base64 += "=";
                }
            }
            if (bytes == null)
            {
                throw new FormatException();
            }
            string[] parts = Encoding.UTF8.GetString(bytes).Split(new char[2] { ':', '@' });
            this.method = parts[0].ToString();
            this.password = parts[1].ToString();
            this.server = parts[2].ToString();
            this.server_port = int.Parse(parts[3].ToString());
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using Shadowsocks.Properties;
using SimpleJson;
using Shadowsocks.Util;

namespace Shadowsocks.Controller
{
    public class GFWListUpdater
    {
        private const string GFWLIST_URL = "https://autoproxy-gfwlist.googlecode.com/svn/trunk/gfwlist.txt";

        public event EventHandler UpdateCompleted;

        public event ErrorEventHandler Error;

        private void http_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                List<string> lines = ParseResult(e.Result);

                string abpContent = Utils.UnGzip(Resources.abp_js);
                abpContent = abpContent.Replace("__RULES__", SimpleJson.SimpleJson.SerializeObject(lines));
                File.WriteAllText(PACServer.GetPacFilePath(), abpContent, Encoding.UTF8);
                if (UpdateCompleted != null)
                {
                    UpdateCompleted(this, new EventArgs());
                }
            }
            catch (Exception ex)
            {
                if (Error != null)
                {
                    Error(this, new ErrorEventArgs(ex));
                }
            }
        }

        public void UpdatePACFromGFWList()
        {
            WebClient http = new WebClient();
            http.Proxy = new WebProxy(IPAddress.Loopback.ToString(), 8123);
            http.DownloadStringCompleted += http_DownloadStringCompleted;
            http.DownloadStringAsync(new Uri(GFWLIST_URL));
        }

        public List<string> ParseResult(string response)
        {
            byte[] bytes = Convert.FromBase64String(response);
            string content = Encoding.ASCII.GetString(bytes);
            string[] lines = content.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> valid_lines = new List<string>(lines.Length);
            foreach (string line in lines)
            {
                if (line.StartsWith("!") || line.StartsWith("["))
                    continue;
                valid_lines.Add(line);
            }
            return valid_lines;
        }
    }
}
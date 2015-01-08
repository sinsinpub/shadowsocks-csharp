using Shadowsocks.Controller;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Shadowsocks.Model
{
    [Serializable]
    public class Configuration
    {
        public List<Server> configs;
        public int index;
        public bool global;
        public bool enabled;
        public bool shareOverLan;
        public int pacPort = PACServer.DEFAULT_PORT;
        public int httpPort = PolipoRunner.DEFAULT_PORT;
        public bool updateCheck = true;
        public bool isDefault;

        private const string CONFIG_FILE = "gui-config.json";
        private static string configPath;

        public Server GetCurrentServer()
        {
            if (index >= 0 && index < configs.Count)
            {
                return configs[index];
            }
            else
            {
                return GetDefaultServer();
            }
        }

        public static void CheckServer(Server server)
        {
            CheckPort(server.local_port);
            CheckPort(server.server_port);
            CheckPassword(server.password);
            CheckServerAddr(server.server);
        }

        public static Configuration Load()
        {
            try
            {
                if (String.IsNullOrEmpty(configPath))
                {
                    CheckConfigPathWritable();
                }
                string configContent = File.ReadAllText(Path.Combine(configPath, CONFIG_FILE));
                Configuration config = SimpleJson.SimpleJson.DeserializeObject<Configuration>(configContent, new JsonSerializerStrategy());
                config.isDefault = false;
                return config;
            }
            catch (Exception e)
            {
                if (!(e is FileNotFoundException))
                {
                    Console.WriteLine(e);
                }
                return new Configuration
                {
                    index = 0,
                    isDefault = true,
                    configs = new List<Server>()
                    {
                        GetDefaultServer()
                    }
                };
            }
        }

        public static void Save(Configuration config)
        {
            if (config.index >= config.configs.Count)
            {
                config.index = config.configs.Count - 1;
            }
            if (config.index < 0)
            {
                config.index = 0;
            }
            config.isDefault = false;
            try
            {
                CheckConfigPathWritable();
                using (StreamWriter sw = new StreamWriter(File.Open(Path.Combine(configPath, CONFIG_FILE), FileMode.Create)))
                {
                    string jsonString = SimpleJson.SimpleJson.SerializeObject(config);
                    sw.Write(jsonString);
                    sw.Flush();
                }
            }
            catch (IOException e)
            {
                Console.Error.WriteLine(e);
            }
        }

        public static Server GetDefaultServer()
        {
            return new Server()
            {
                server = "",
                server_port = 8388,
                local_port = 1080,
                method = "aes-256-cfb",
                password = "",
                remarks = ""
            };
        }

        public static string GetConfigPath()
        {
            return configPath;
        }

        public static void Assert(bool condition)
        {
            if (!condition)
            {
                throw new Exception(I18N.GetString("Assertion failure"));
            }
        }

        public static void CheckPort(int port)
        {
            if (port <= 0 || port > 65535)
            {
                throw new ArgumentException(I18N.GetString("Port out of range"));
            }
        }

        private static void CheckPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException(I18N.GetString("Password can not be blank"));
            }
        }

        private static void CheckServerAddr(string server)
        {
            if (string.IsNullOrEmpty(server))
            {
                throw new ArgumentException(I18N.GetString("Server IP can not be blank"));
            }
        }

        private static bool CheckConfigPathWritable()
        {
            if (String.IsNullOrEmpty(configPath))
            {
                configPath = Path.GetDirectoryName(Application.ExecutablePath);
            }
            // Get rid of version
            string userAppDataPath = Path.GetDirectoryName(Application.UserAppDataPath);
            try
            {
                System.Security.AccessControl.DirectorySecurity ds = Directory.GetAccessControl(configPath);
                // Still try to write something...
                string tmpFile = Path.Combine(configPath, Path.GetFileNameWithoutExtension(CONFIG_FILE) + ".tmp");
                using (StreamWriter sw = new StreamWriter(File.Open(tmpFile, FileMode.Create)))
                {
                    sw.Write(0x00);
                    sw.Flush();
                    sw.Close();
                }
                if (File.Exists(tmpFile))
                {
                    File.Delete(tmpFile);
                    return true;
                }
            }
            catch (UnauthorizedAccessException e)
            {
                if (String.Equals(configPath, userAppDataPath)) {
                    throw e;
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            configPath = userAppDataPath;
            return false;
        }

        private class JsonSerializerStrategy : SimpleJson.PocoJsonSerializerStrategy
        {
            // convert string to int
            public override object DeserializeObject(object value, Type type)
            {
                if (type == typeof(Int32) && value.GetType() == typeof(string))
                {
                    return Int32.Parse(value.ToString());
                }
                return base.DeserializeObject(value, type);
            }
        }
    }
}

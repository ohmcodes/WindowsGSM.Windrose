using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;
using System.Collections.Generic;

namespace WindowsGSM.Plugins
{
    public class Windrose : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Windrose", // WindowsGSM.XXXX
            author = "ohmcodes",
            description = "WindowsGSM plugin for supporting Windrose Dedicated Server",
            version = "1.0",
            url = "https://github.com/ohmcodes/WindowsGSM.Windrose", // Github repository link (Best practice)
            color = "#1E8449" // Color Hex
        };

        // - Standard Constructor and properties
        public Windrose(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "4129620"; /* taken via https://steamdb.info/app/4129620/info/ */

        // - Game server Fixed variables
        public override string StartPath => @"R5\Binaries\Win64\WindroseServer-Win64-Shipping.exe"; // Game server start path
        public string FullName = "Windose Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 0; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        // - Game server default values
        public string ServerName = "WGSM Windrose";
        public string Defaultmap = ""; // Original (MapName)
        public string Maxplayers = "10"; // WGSM reads this as string but originally it is number or int (MaxPlayers)
        public string Port = "27015"; // WGSM reads this as string but originally it is number or int
        public string QueryPort = "27016"; // WGSM reads this as string but originally it is number or int (SteamQueryPort)
        public string Additional = string.Empty;


        private Dictionary<string, string> configData = new Dictionary<string, string>();


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {

        }

        // -                            Start server function, return its Process to WindowsGSM
       public Task<Process> Start()
        {
            string exePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);

            if (!File.Exists(exePath))
            {
                Error = $"{Path.GetFileName(exePath)} not found ({exePath})";
                return Task.FromResult<Process>(null);
            }

            ServerName = GetConfigString("ServerName", ServerName);
            Maxplayers = GetConfigString("MaxPlayerCount", Maxplayers);

            string param = BuildLaunchArguments();

            var p = new Process
            {
                StartInfo =
                {
                    FileName = exePath,
                    Arguments = param,
                    WorkingDirectory = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID),
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                },
                EnableRaisingEvents = true
            };

            try
            {
                p.Start();

                try
                {
                    p.PriorityClass = ProcessPriorityClass.AboveNormal;
                }
                catch
                {
                }

                return Task.FromResult(p);
            }
            catch (Exception e)
            {
                Error = e.ToString();
                return Task.FromResult<Process>(null);
            }
        }

        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
            });
            await Task.Delay(2000);
        }

        // - Update server function
        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: loginAnonymous);
            Error = error;
            await Task.Run(() => { p.WaitForExit(); });
            return p;
        }

        public bool IsInstallValid()
        {
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }

        public bool IsImportValid(string path)
        {
            string exePath = Path.Combine(path, "PackageInfo.bin");
            Error = $"Invalid Path! Fail to find {Path.GetFileName(exePath)}";
            return File.Exists(exePath);
        }

        public string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }

        public async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }

        private string BuildLaunchArguments()
        {
            string args = "-log";

            if (!string.IsNullOrWhiteSpace(Port))
                args += $" -PORT={Port}";

            if (!string.IsNullOrWhiteSpace(QueryPort))
                args += $" -QUERYPORT={QueryPort}";

            if (!string.IsNullOrWhiteSpace(Additional))
            {
                string extra = Additional.Trim();

                if (extra.IndexOf("-log", StringComparison.OrdinalIgnoreCase) < 0)
                    args += $" {extra}";
            }

            return args.Trim();
        }

        private string GetConfigString(string key, string fallback)
        {
            try
            {
                string root = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID);

                string[] possiblePaths =
                {
                    Path.Combine(root, "ServerDescription.json"),
                    Path.Combine(root, "R5", "ServerDescription.json"),
                    Path.Combine(root, "R5", "Saved", "SaveProfiles", "Default", "ServerDescription.json")
                };

                foreach (string path in possiblePaths)
                {
                    if (!File.Exists(path))
                        continue;

                    string json = File.ReadAllText(path);

                    var stringMatch = Regex.Match(
                        json,
                        $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]*)\"",
                        RegexOptions.IgnoreCase
                    );

                    if (stringMatch.Success)
                        return stringMatch.Groups[1].Value;

                    var numberMatch = Regex.Match(
                        json,
                        $"\"{Regex.Escape(key)}\"\\s*:\\s*(\\d+)",
                        RegexOptions.IgnoreCase
                    );

                    if (numberMatch.Success)
                        return numberMatch.Groups[1].Value;

                    var boolMatch = Regex.Match(
                        json,
                        $"\"{Regex.Escape(key)}\"\\s*:\\s*(true|false)",
                        RegexOptions.IgnoreCase
                    );

                    if (boolMatch.Success)
                        return boolMatch.Groups[1].Value;
                }
            }
            catch
            {
            }

            return fallback;
        }

        public bool IsPasswordProtected()
        {
            return GetConfigString("IsPasswordProtected", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public string GetPassword()
        {
            return GetConfigString("Password", "");
        }

        public string GetInviteCode()
        {
            return GetConfigString("InviteCode", "");
        }
    }
}

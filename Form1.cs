﻿using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Include.VR.Viewer.Mod
{
    public partial class frmViewrModLoader : Form
    {
        List<SupportedGame> supportedGameList = JsonConvert.DeserializeObject<List<SupportedGame>>(File.ReadAllText("games.json"));
        string[] files = new string[] {
            @"assets\gfxplugin-viewr.dll",
            @"assets\viewr.asset",
            @"assets\ViewRMod.dll",
            @"assets\IPA.zip"
        };

        public frmViewrModLoader()
        {
            InitializeComponent();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void frmViewrModLoader_Load(object sender, EventArgs e)
        {
            string[] parts = Application.ProductVersion.Split('.');
                        
            this.Text += $" v.{parts[0].PadLeft(2,'0')}{parts[1].PadLeft(2, '0')}{(parts[2].PadLeft(2, '0'))}";
            RefreshData();
        }

        private void btnPatch_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < lstBox.Items.Count; i++)
            {
                foreach (string filename in files)
                {
                    if (!File.Exists(filename))
                    {
                        MessageBox.Show("Install files are missing! Please download ViewR Mod Installer again.");
                        return;
                    }
                }

                if (lstBox.GetItemChecked(i))
                {
                    // do game patching here
                    SupportedGame sg = lstBox.Items[i] as SupportedGame;
                    if (sg != null)
                    {
                        string directory = $"{sg.Library}\\{sg.GameDirectory}";
                        //MessageBox.Show($"patching in {directory}");

                        int result = -1;
                        try
                        {
                            if (!File.Exists($"{directory}\\IPA.exe") && !File.Exists($"{directory}\\Mono.Cecil.dll") && !Directory.Exists($"{directory}\\IPA"))
                            {
                                //only if the entire ipa is missing, do we try using our own
                                ZipFile.ExtractToDirectory(@"assets\IPA.zip", directory);
                            }

                            Process IPA = Process.Start($"{directory}\\IPA.exe", $"\"{directory}\\{sg.ExeName}\"");
                            IPA.WaitForExit();
                            result = IPA.ExitCode;
                            if (result != 0)
                            {
                                MessageBox.Show($"IPA failed to patch {sg.GameName}");
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Something went wrong while installing IPA{Environment.NewLine}{ex.ToString()}");
                            continue;
                        }
                        if (!Directory.Exists($"{directory}\\Plugins"))
                        {
                            // we failed to install ipa even tho ipa says it installed
                            MessageBox.Show($"IPA failed to patch {sg.GameName}");
                            continue;
                        }

                        //copy plugin
                        try
                        {
                            // make missing folders if they're missing!
                            string appName = Path.GetFileNameWithoutExtension(sg.ExeName);
                            if (!Directory.Exists($"{directory}\\{appName}_Data\\Plugins\\"))
                                Directory.CreateDirectory($"{directory}\\{appName}_Data\\Plugins\\");
                            if (!Directory.Exists($"{directory}\\{appName}_Data\\StreamingAssets\\"))
                                Directory.CreateDirectory($"{directory}\\{appName}_Data\\StreamingAssets\\");

                            // copy files
                            File.Copy(@"assets\gfxplugin-viewr.dll", $"{directory}\\{appName}_Data\\Plugins\\gfxplugin-viewr.dll", true);
                            File.Copy(@"assets\viewr.asset", $"{directory}\\{appName}_Data\\StreamingAssets\\viewr.asset", true);
                            File.Copy(@"assets\ViewRMod.dll", $"{directory}\\Plugins\\ViewRMod.dll", true);
                        }
                        catch
                        {
                            MessageBox.Show($"Could not copy files, please ensure that the game is not running or download installer again.");
                        }

                        if ((!File.Exists($"{directory}\\viewrcamera.cfg") && // there's no cameracfg
                            !File.Exists($"{directory}\\viewrplugin.cfg")) || // and there's no plugincfg
                            checkBox1.Checked)
                        {
                            string cfg = "default.zip";
                            if (sg.ConfigName != null && sg.ConfigName != "")
                            {
                                cfg = sg.ConfigName;
                            }

                            if (File.Exists($"assets\\configs\\{cfg}"))
                            {
                                if (File.Exists($"{directory}\\viewrcamera.cfg")) File.Delete($"{directory}\\viewrcamera.cfg");
                                if (File.Exists($"{directory}\\viewrplugin.cfg")) File.Delete($"{directory}\\viewrplugin.cfg");
                                ZipFile.ExtractToDirectory($"assets\\configs\\{cfg}", directory);
                            }
                        }

                    }
                }
            }
            MessageBox.Show("Finished running installer.");
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = false;
            ofd.Filter = "Unity Game exe| *.exe";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                // create a SupportedGame
                SupportedGame newGame = new SupportedGame();
                newGame.ExeName = Path.GetFileName(ofd.FileName);
                newGame.Library = Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName));
                newGame.GameDirectory = Path.GetFileName(Path.GetDirectoryName(ofd.FileName));
                newGame.GameName = Path.GetFileNameWithoutExtension(ofd.FileName);
                newGame.ConfigName = null;

                // if newly added game is in the supported game list
                foreach (SupportedGame game in supportedGameList)
                {
                    if (game.ExeName == newGame.ExeName)
                    {
                        newGame.ConfigName = game.ConfigName;
                        break;
                    }
                }

                // if we couldn't find it, we use default.zip
                if (newGame.ConfigName == null)
                    newGame.ConfigName = "default.zip";
                lstBox.Items.Add(newGame, true);
                supportedGameList.Add(newGame);
                SaveGameList();
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshData();
        }

        private void RefreshData()
        {
            // read the steam install directory from the registry
            string installPath = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", "");
            if (!Directory.Exists(installPath))
            {
                MessageBox.Show("Unable to locate steam folder, please add games manually.");
                return;
            }

            List<string> libraryLocations = new List<string>();
            libraryLocations.Add(installPath + @"\steamapps\common");
            if (File.Exists(installPath + @"\steamapps\libraryfolders.vdf"))
            {
                // only do the steamapps folder in the current path
                Regex r = new Regex(".*{(.+)}.*", RegexOptions.Singleline);
                MatchCollection mc = r.Matches(File.ReadAllText(installPath + @"\steamapps\libraryfolders.vdf"));
                string valveGarbageFileContents = mc[0].Groups[1].Captures[0].Value.Trim();
                string[] lines = valveGarbageFileContents.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string[] valveData = line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (valveData.Length < 2)
                        continue;
                    if (int.TryParse(valveData[0].Replace("\"", ""), out int devNull))
                    {
                        if (Directory.Exists(valveData[1].Replace("\"", "").Replace("\\\\", "\\")))
                        {
                            libraryLocations.Add(valveData[1].Replace("\"", "").Replace("\\\\", "\\") + @"\steamapps\common");
                        }
                    }
                }
            }

            lstBox.Items.Clear();

            foreach(SupportedGame sg in supportedGameList)
            {
                if(sg.SteamAppID != 0)
                {
                    foreach(string library in libraryLocations)
                    {
                        if (File.Exists(Path.GetDirectoryName(library) + $"\\appmanifest_{sg.SteamAppID}.acf"))
                        {
                            sg.Library = library;
                            lstBox.Items.Add(sg, true);
                            break;
                        }
                    }
                }
            }

            // add custom items
            List<SupportedGame> custom = supportedGameList.Where(x => x.SteamAppID == 0).ToList();
            foreach (SupportedGame sg in custom)
            {
                lstBox.Items.Add(sg, true);
            }
        }

        private void SaveGameList()
        {
            File.WriteAllText("games.json",
            JsonConvert.SerializeObject(
            supportedGameList.Select(
            x => new SupportedGame()
            {
                ExeName = x.ExeName,
                SteamAppID = x.SteamAppID,
                GameDirectory = x.GameDirectory,
                GameName = x.GameName,
                ConfigName = x.ConfigName == "default.zip" ?  null : x.ConfigName,
                Library = x.SteamAppID == 0 ? x.Library : null
            }).ToList(),
            new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            })
            );
        }

        private void lstBox_SelectedIndexChanged(object sender, EventArgs e) {

        }
    }

    public class SupportedGame
    {
        public string Library { get; set; }
        public int SteamAppID { get; set; }
        public string GameName { get; set; }
        public string GameDirectory { get; set; }
        public string ExeName { get; set; }
        public string ConfigName { get; set; }

        public override string ToString()
        {
            return this.GameName;
        }
    }

}

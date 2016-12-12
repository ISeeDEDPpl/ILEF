// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------

namespace ILEF.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    //using System.Threading;
    using System.Windows.Forms;
    //using ILEF.Lookup;
    //using InnerSpaceAPI;
    //using LavishScriptAPI;

    public static class Logging
    {
        static Logging()
        {
            Logging.PathToCurrentDirectory = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        public static string PathToCurrentDirectory;
        public static bool DebugMaintainConsoleLogs { get; set; }

        public static DateTime DateTimeForLogs;
        //list of colors
        public const string Green = "\ag";    //traveler mission control
        public const string Yellow = "\ay";
        public const string Blue = "\ab";     //DO NOT USE - blends into default lavish GUIs background.
        public const string Red = "\ar";      //error panic
        public const string Orange = "\ao";   //error can fix
        public const string Purple = "\ap";   //combat
        public const string Magenta = "\am";  //drones
        public const string Teal = "\at";     //log debug
        public const string White = "\aw";    //questor

        public const string Debug = Teal;     //log debug

        public static string EVELoginUserName;
        public static string EVELoginPassword;
        public static string MyCharacterName;

        public static string CharacterSettingsPath;


        private static string colorLogLine;
        private static string plainLogLine;
        public static bool ConsoleLogOpened = false;
        public static string ExtConsole { get; set; }
        //public static string ConsoleLog { get; set; }
        //public static string ConsoleLogRedacted { get; set; }
        public static string SessionDataCachePath { get; set; }
        public static string Logpath { get; set; }

        public static bool InnerspaceGeneratedConsoleLog { get; set; }
        public static bool UseInnerspace { get; set; }
        public static bool EnableVisualStyles { get; set; }
        public static bool DebugDisableAutoLogin { get; set; }
        //public static bool ConsoleLog { get; set; }
        public static string ConsoleLogPath { get; set; } //we should set this to a sane value (via get { blah } when we are pre-login....
        public static string ConsoleLogFile { get; set; } //we should set this to a sane value (via get { blah } when we are pre-login....
        public static bool SaveLogRedacted { get; set; } //we should set this to a sane value (via get { blah } when we are pre-login....
        //public static bool ConsoleLogRedacted { get; set; }
        public static string redactedPlainLogLine { get; set; }
        public static string redactedColorLogLine { get; set; }
        public static string ConsoleLogPathRedacted { get; set; }  //we should set this to a sane value (via get { blah } when we are pre-login....
        public static string ConsoleLogFileRedacted { get; set; }  //we should set this to a sane value (via get { blah } when we are pre-login....

        //
        // number of days of console logs to keep (anything older will be deleted on startup)
        //
        public static int ConsoleLogDaysOfLogsToKeep { get; set; }

        private static string _characterNameForLogs;
        public static string characterNameForLogs
        {
            get
            {
                if (String.IsNullOrEmpty(_characterNameForLogs))
                {
                    if (String.IsNullOrEmpty(Logging.MyCharacterName))
                    {
                        //if (String.IsNullOrEmpty(QMSettings.Instance.CharacterName))
                        //{
                            return "_PreLogin-UnknownCharacterName_";
                        //}

                        //return Logging.FilterPath(QMSettings.Instance.CharacterName);
                    }

                    return Logging.FilterPath(Logging.MyCharacterName);
                }

                return _characterNameForLogs;
            }
            set
            {
                _characterNameForLogs = value;
            }
        }

        //public  void Log(string line)
        //public static void Log(string module, string line, string color = Logging.White)
        public static void Log(string DescriptionOfWhere, string line, string color, bool verbose = false)
        {
            try
            {
                //
                // Log location and log names defined here
                //
                //Logging.SessionDataCachePath = Logging.PathToCurrentDirectory + "\\SessionDataCache\\" + characterNameForLogs + "\\";
                //Logging.Logpath = Logging.PathToCurrentDirectory + "\\log\\" + characterNameForLogs + "\\";

                //logpath_s = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\log\\";
                Logging.ConsoleLogPath = System.IO.Path.Combine(Logging.Logpath, "Console\\");
                Logging.ConsoleLogFile = System.IO.Path.Combine(Logging.ConsoleLogPath, string.Format("{0:MM-dd-yyyy}", DateTime.Today) + "-" + characterNameForLogs + "-" + "console" + ".log");
                Logging.ConsoleLogPathRedacted = System.IO.Path.Combine(Logging.Logpath, "Console\\");
                Logging.ConsoleLogFileRedacted = System.IO.Path.Combine(Logging.ConsoleLogPath, string.Format("{0:MM-dd-yyyy}", DateTime.Today) + "-" + "redacted" + "-" + "console" + ".log");

                DateTimeForLogs = DateTime.Now;

                if (verbose) //tons of info
                {
                    //
                    // verbose text logging - with line numbers, filenames and Methods listed ON EVERY LOGGING LINE - this is ALOT more detail
                    //
                    System.Diagnostics.StackFrame sf = new System.Diagnostics.StackFrame(1, true);
                    DescriptionOfWhere += "-[line" + sf.GetFileLineNumber().ToString() + "]in[" + System.IO.Path.GetFileName(sf.GetFileName()) + "][" + sf.GetMethod().Name + "]";
                }

                colorLogLine = line;
                Logging.redactedColorLogLine = String.Format("{0:HH:mm:ss} {1}", DateTimeForLogs, Logging.Orange + "[" + Logging.Yellow + DescriptionOfWhere + Logging.Orange + "] " + color + FilterSensitiveInfo(colorLogLine));  //In memory Console Log with sensitive info redacted

                plainLogLine = FilterColorsFromLogs(line);
                Logging.redactedPlainLogLine = String.Format("{0:HH:mm:ss} {1}", DateTimeForLogs, "[" + DescriptionOfWhere + "] " + FilterSensitiveInfo(plainLogLine) + "\r\n");  //In memory Console Log with sensitive info redacted
                Logging.ExtConsole = Logging.redactedPlainLogLine;

                //
                // Log To Screen
                //
                if (Logging.UseInnerspace) //Write logging entry to the Innerspace Console window
                {
                    //InnerSpace.Echo(Logging.redactedColorLogLine);
                }
                else // Write directly to the EVE Console window (if you want to see this you must be running EXEFile.exe without the /noconsole switch)
                {
                    Console.Write(Logging.redactedPlainLogLine);
                }

                //
                // Log To File
                //
                if (!Logging.ConsoleLogOpened)
                {
                    PrepareConsoleLog();
                }

                if (Logging.ConsoleLogOpened)
                {
                    WriteToConsoleLog();
                }
            }
            catch (Exception exception)
            {
                BasicLog(DescriptionOfWhere, exception.Message);
            }
        }

        private static bool PrepareConsoleLog()
        {
            //
            // begin logging to file
            //
            if (Logging.ConsoleLogPath != null && Logging.ConsoleLogFile != null)
            {
                if (Logging.InnerspaceGeneratedConsoleLog && Logging.UseInnerspace)
                {
                    //InnerSpace.Echo(string.Format("{0:HH:mm:ss} {1}", DateTimeForLogs, "log " + Logging.ConsoleLogFile + "-innerspace-generated.log"));
                    //LavishScript.ExecuteCommand("log " + Logging.ConsoleLogFile + "-innerspace-generated.log");
                }

                if (!string.IsNullOrEmpty(Logging.ConsoleLogFile))
                {
                    Directory.CreateDirectory(Logging.ConsoleLogPath);
                    if (Directory.Exists(Logging.ConsoleLogPath))
                    {
                        Logging.ConsoleLogOpened = true;
                        return true;
                    }

                    //if (Logging.UseInnerspace) InnerSpace.Echo(string.Format("{0:HH:mm:ss} {1}", DateTimeForLogs, "Logging: Unable to find (or create): " + Logging.ConsoleLogPath));
                }

                //
                // manually echo an error here?
                //
                return false;
            }

            //
            // manually echo an error here?
            //
            return false;
        }

        private static void WriteToConsoleLog()
        {
            //
            // log file ready: add next logging entry...
            //
            //
            // normal text logging
            //
            if (Logging.ConsoleLogFile != null) //normal
            {
                File.AppendAllText(Logging.ConsoleLogFile, Logging.redactedPlainLogLine); //Write In Memory Console log entry to File
            }

            //
            // redacted text logging - sensitive info removed so you can generally paste the contents of this log publicly w/o fear of easily exposing user identifiable info
            //
            if (Logging.ConsoleLogFileRedacted != null)
            {
                File.AppendAllText(Logging.ConsoleLogFileRedacted, Logging.redactedPlainLogLine); //Write In Memory Console log entry to File
            }

            return;
        }

        public static void BasicLog(string module, string logmessage)
        {
            Console.WriteLine("{0:HH:mm:ss} {1}", DateTime.UtcNow,"[" + module + "] " + logmessage);
            if (Logging.SaveLogRedacted && Logging.ConsoleLogFileRedacted != null)
            {
                if (Directory.Exists(Path.GetDirectoryName(Logging.ConsoleLogFileRedacted)))
                {
                    File.AppendAllText(Logging.ConsoleLogFileRedacted, string.Format("{0:HH:mm:ss} {1}", DateTime.UtcNow,"[" + module + "] " + logmessage));
                }
            }

            if (Logging.SaveLogRedacted && Logging.ConsoleLogFile != null)
            {
                if (Directory.Exists(Path.GetDirectoryName(Logging.ConsoleLogFile)))
                {
                    File.AppendAllText(Logging.ConsoleLogFile, string.Format("{0:HH:mm:ss} {1}", DateTime.UtcNow, "[" + module + "] " + logmessage));
                }
            }
        }

        //path = path.Replace(Environment.CommandLine, "");
        //path = path.Replace(Environment.GetCommandLineArgs(), "");

        public static void InvalidateCache()
        {
            Logging._characterNameForLogs = string.Empty;
            return;
        }

        public static string FilterSensitiveInfo(string line)
        {
            try
            {
                if (line == null)
                    return string.Empty;
                if (!string.IsNullOrEmpty(Logging.MyCharacterName))
                {
                    line = line.Replace(Logging.MyCharacterName, Logging.MyCharacterName.Substring(0, 2) + "_MyEVECharacterNameRedacted_");
                    line = line.Replace("/" + Logging.MyCharacterName, "/" + Logging.MyCharacterName.Substring(0, 2) + "_MyEVECharacterNameRedacted_");
                    line = line.Replace("\\" + Logging.MyCharacterName, "\\" + Logging.MyCharacterName.Substring(0, 2) + "_MyEVECharacterNameRedacted_");
                    line = line.Replace("[" + Logging.MyCharacterName + "]", "[" + Logging.MyCharacterName.Substring(0, 2) + "_MyEVECharacterNameRedacted_]");
                    line = line.Replace(Logging.MyCharacterName + ".xml", Logging.MyCharacterName.Substring(0, 2) + "_MyEVECharacterNameRedacted_.xml");
                }

                if (!string.IsNullOrEmpty(Logging.EVELoginUserName) && !string.IsNullOrEmpty(Logging.EVELoginUserName))
                {
                    line = line.Replace(Logging.EVELoginUserName, Logging.EVELoginUserName.Substring(0, 2) + "_MyEVELoginNameRedacted_");
                }

                if (!string.IsNullOrEmpty(Logging.EVELoginPassword) && !string.IsNullOrWhiteSpace(Logging.EVELoginPassword))
                {
                    line = line.Replace(Logging.EVELoginPassword, Logging.EVELoginPassword.Substring(0, 2) + "_MyEVELoginPasswordRedacted_");
                }

                if (!string.IsNullOrEmpty(Logging.CharacterSettingsPath))
                {
                    line = line.Replace(Logging.CharacterSettingsPath, Logging.CharacterSettingsPath.Substring(0, 2) + "_MySettingsFileNameRedacted_.xml");
                }

                //if (!string.IsNullOrEmpty(Cache.Instance.CurrentAgent))
                //{
                //    if (Logging.DebugLogging) InnerSpace.Echo("Logging.Log: FilterSensitiveInfo: CurrentAgent exists [" + Cache.Instance.CurrentAgent + "]");
                //    line = line.Replace(" " + Cache.Instance.CurrentAgent + " ", " _MyCurrentAgentRedacted_ ");
                //    line = line.Replace("[" + Cache.Instance.CurrentAgent + "]", "[_MyCurrentAgentRedacted_]");
                //}
                //if (Cache.Instance.AgentId != -1)
                //{
                //    if(Logging.DebugLogging) InnerSpace.Echo("Logging.Log: FilterSensitiveInfo: AgentId is not -1");
                //    line = line.Replace(" " + Cache.Instance.AgentId + " ", " _MyAgentIdRedacted_ ");
                //    line = line.Replace("[" + Cache.Instance.AgentId + "]", "[_MyAgentIdRedacted_]");
                //}
                if (!String.IsNullOrEmpty(Logging.EVELoginUserName))
                {
                    line = line.Replace(Logging.EVELoginUserName, Logging.EVELoginUserName.Substring(0, 2) + "_HiddenEVELoginName_");
                }
                if (!String.IsNullOrEmpty(Logging.EVELoginPassword))
                {
                    line = line.Replace(Logging.EVELoginPassword, "_HiddenPassword_");
                }
                if (!string.IsNullOrEmpty(Environment.UserName))
                {
                    line = line.Replace("\\" + Environment.UserName + "\\", "\\_MyWindowsLoginNameRedacted_\\");
                    line = line.Replace("/" + Environment.UserName + "/", "/_MyWindowsLoginNameRedacted_/");
                }
                if (!string.IsNullOrEmpty(Environment.UserDomainName))
                {
                    line = line.Replace(Environment.UserDomainName, "_MyWindowsDomainNameRedacted_");
                }

                return line;
            }
            catch (Exception exception)
            {
                BasicLog("FilterSensitiveInfo", exception.Message);
                return line;
            }
        }

         public static string ReplaceUnderscoresWithSpaces(string line)
        {
            try
            {
                if (line == null)
                    return string.Empty;
                if (!string.IsNullOrEmpty(line))
                {
                    line = line.Replace("_", " ");
                }

                return line;
            }
            catch (Exception exception)
            {
                BasicLog("ReplaceUnderscoresWithSpaces", exception.Message);
                return line;
            }
        }

        public static class RichTextBoxExtensions
        {
            public static void AppendText(RichTextBox box, string text, Color color)
            {
                box.SelectionStart = box.TextLength;
                box.SelectionLength = 0;

                box.SelectionColor = color;
                box.AppendText(text);
                box.SelectionColor = box.ForeColor;
            }
        }

        public static string FilterPath(string path)
        {
            try
            {
                if (path == null)
                {
                    return string.Empty;
                }

                path = path.Replace("\"", "");
                path = path.Replace("?", "");
                path = path.Replace("\\", "");
                path = path.Replace("/", "");
                path = path.Replace("'", "");
                path = path.Replace("*", "");
                path = path.Replace(":", "");
                path = path.Replace(">", "");
                path = path.Replace("<", "");
                path = path.Replace(".", "");
                path = path.Replace(",", "");
                path = path.Replace("'", "");
                while (path.IndexOf("  ", System.StringComparison.Ordinal) >= 0)
                    path = path.Replace("  ", " ");
                return path.Trim();
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.FilterPath", "Exception [" + exception + "]", Logging.Debug);
                return null;
            }
        }

        public static string FilterColorsFromLogs(string line)
        {
            try
            {
                if (line == null)
                    return string.Empty;

                line = line.Replace("\ag", "");
                line = line.Replace("\ay", "");
                line = line.Replace("\ab", "");
                line = line.Replace("\ar", "");
                line = line.Replace("\ao", "");
                line = line.Replace("\ap", "");
                line = line.Replace("\am", "");
                line = line.Replace("\at", "");
                line = line.Replace("\aw", "");
                while (line.IndexOf("  ", System.StringComparison.Ordinal) >= 0)
                    line = line.Replace("  ", " ");
                return line.Trim();
            }
            catch (Exception exception)
            {
                BasicLog("FilterSensitiveInfo", exception.Message);
                return null;
            }
        }

        public static void MaintainConsoleLogs()
        {
            const string searchpattern = ".log";

            //calculate the current date - the number of keep days (make sure you use the negative value if QMSettings.Instance.ConsoleLogDaysOfLogsToKeep as we want to keep that many days in the past, not that many days in the future)
            DateTime keepdate = DateTime.UtcNow.AddDays(-Logging.ConsoleLogDaysOfLogsToKeep);

            //this is where it gets the directory and looks at
            //the files in the directory to compare the last write time
            //against the keepdate variable.
            try
            {
                if (Logging.DebugMaintainConsoleLogs) Logging.Log("Logging.MaintainConsoleLogs", "ConsoleLogPath is [" + Logging.ConsoleLogPath + "]", Logging.White);
                DirectoryInfo fileListing = new DirectoryInfo(Logging.ConsoleLogPath);

                if (fileListing.Exists)
                {
                    if (Logging.DebugMaintainConsoleLogs) Logging.Log("Logging.MaintainConsoleLogs", "if (fileListing.Exists)", Logging.White);
                    foreach (FileInfo log in fileListing.GetFiles(searchpattern))
                    {
                        if (Logging.DebugMaintainConsoleLogs) Logging.Log("Logging.MaintainConsoleLogs", "foreach (FileInfo log in fileListing.GetFiles(searchpattern))", Logging.White);
                        if (log.LastWriteTime <= keepdate)
                        {
                            if (Logging.DebugMaintainConsoleLogs) Logging.Log("Logging.MaintainConsoleLogs", "if (log.LastWriteTime <= keepdate)", Logging.White);
                            try
                            {
                                Logging.Log("Logging", "Removing old console log named [" + log.Name + "] Dated [" + log.LastWriteTime + "]", Logging.White);
                                log.Delete();
                            }
                            catch (Exception ex)
                            {
                                Logging.Log("Logging", "Unable to delete log [" + ex.Message + "]", Logging.White);
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                BasicLog("FilterSensitiveInfo", exception.Message);
            }
        }

        public static IEnumerable<string> SplitArguments(string commandLine)
        {
            try
            {
                char[] parmChars = commandLine.ToCharArray();
                bool inSingleQuote = false;
                bool inDoubleQuote = false;
                for (int index = 0; index < parmChars.Length; index++)
                {
                    if (parmChars[index] == '"' && !inSingleQuote)
                    {
                        inDoubleQuote = !inDoubleQuote;
                        parmChars[index] = '\n';
                    }
                    if (parmChars[index] == '\'' && !inDoubleQuote)
                    {
                        inSingleQuote = !inSingleQuote;
                        parmChars[index] = '\n';
                    }
                    if (!inSingleQuote && !inDoubleQuote && parmChars[index] == ' ')
                        parmChars[index] = '\n';
                }
                return (new string(parmChars)).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            }
            catch (Exception exception)
            {
                BasicLog("SplitArguments", exception.Message);
                return null;
            }
        }

        public static void ShowConsoleWindow()
        {
            Logging.Log("Adapt", "Showing Console Window", Logging.White);
            IntPtr handle = GetConsoleWindow();
            if (handle == IntPtr.Zero)
            {
                AllocConsole();
            }
            else
            {
                ShowWindow(handle, SW_SHOW);
            }
        }

        public static void HideConsoleWindow()
        {
            Logging.Log("Adapt", "Hiding Console Window", Logging.White);
            IntPtr handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        //
        // Debug Variables
        //
        public static bool DebugActivateGate { get; set; }
        public static bool DebugActivateWeapons { get; set; }
        public static bool DebugActivateBastion { get; set; }
        public static bool DebugAdaptEVE { get; set; }
        public static bool DebugAdaptEVEDLL { get; set; }
        public static bool DebugAddDronePriorityTarget { get; set; }
        public static bool DebugAddPrimaryWeaponPriorityTarget { get; set; }
        public static bool DebugAgentInteractionReplyToAgent { get; set; }
        public static bool DebugAllMissionsOnBlackList { get; set; }
        public static bool DebugAllMissionsOnGreyList { get; set; }
        public static bool DebugAmmo { get; set; }
        public static bool DebugAppDomains { get; set; }
        public static bool DebugArm { get; set; }
        public static bool DebugAttachVSDebugger { get; set; }
        public static bool DebugAutoStart { get; set; }
        public static bool DebugBeforeLogin { get; set; }
        public static bool DebugBlackList { get; set; }
        public static bool DebugCargoHold { get; set; }
        public static bool DebugChat { get; set; }
        public static bool DebugCleanup { get; set; }
        public static bool DebugClearPocket { get; set; }
        public static bool DebugCombat { get; set; }
        public static bool DebugCombatMissionBehavior { get; set; }
        public static bool DebugCourierMissions { get; set; }
        public static bool DebugDecline { get; set; }
        public static bool DebugDefense { get; set; }
        public static bool DebugDisableCleanup { get; set; }
        public static bool DebugDisableCombatMissionsBehavior { get; set; }
        public static bool DebugDisableCombatMissionCtrl { get; set; }
        public static bool DebugDisableCombat { get; set; }
        public static bool DebugDisableDrones { get; set; }
        public static bool DebugDisablePanic { get; set; }
        public static bool DebugDisableSalvage { get; set; }
        public static bool DebugDisableTargetCombatants { get; set; }
        public static bool DebugDisableGetBestTarget { get; set; }
        public static bool DebugDisableGetBestDroneTarget { get; set; }
        public static bool DebugDisableNavigateIntoRange { get; set; }
        public static bool DebugDoneAction { get; set; }
        public static bool DebugDoNotCloseTelcomWindows { get; set; }
        public static bool DebugDrones { get; set; }
        public static bool DebugDroneHealth { get; set; }
        public static bool DebugEachWeaponsVolleyCache { get; set; }
        public static bool DebugEntityCache { get; set; }
        public static bool DebugExecuteMission { get; set; }
        public static bool DebugExceptions { get; set; }
        public static bool DebugFittingMgr { get; set; }
        public static bool DebugFleetSupportSlave { get; set; }
        public static bool DebugFleetSupportMaster { get; set; }
        public static bool DebugGetBestTarget { get; set; }
        public static bool DebugGetBestDroneTarget { get; set; }
        public static bool DebugGotobase { get; set; }
        public static bool DebugGreyList { get; set; }
        public static bool DebugHangars { get; set; }
        public static bool DebugIdle { get; set; }
        public static bool DebugInSpace { get; set; }
        public static bool DebugInStation { get; set; }
        public static bool DebugInWarp { get; set; }
        public static bool DebugIsReadyToShoot { get; set; }
        public static bool DebugItemHangar { get; set; }
        public static bool DebugKillTargets { get; set; }
        public static bool DebugKillAction { get; set; }
        public static bool DebugLoadScripts { get; set; }
        public static bool DebugLogging { get; set; }
        public static bool DebugLootWrecks { get; set; }
        public static bool DebugLootValue { get; set; }
        public static bool DebugNavigateOnGrid { get; set; }
        public static bool DebugMiningBehavior { get; set; }
        public static bool DebugMissionFittings { get; set; }
        public static bool DebugMoveTo { get; set; }
        public static bool DebugOnframe { get; set; }
        public static bool DebugOverLoadWeapons { get; set; }
        public static bool DebugPanic { get; set; }
        public static bool DebugPerformance { get; set; }
        public static bool DebugPotentialCombatTargets { get; set; }
        public static bool DebugPreferredPrimaryWeaponTarget { get; set; }
        public static bool DebugPreLogin { get; set; }
        public static bool DebugQuestorLoader { get; set; }
        public static bool DebugQuestorManager { get; set; }
        public static bool DebugQuestorEVEOnFrame { get; set; }
        public static bool DebugReloadAll { get; set; }
        public static bool DebugReloadorChangeAmmo { get; set; }
        public static bool DebugRemoteRepair { get; set; }
        public static bool DebugSalvage { get; set; }
        public static bool DebugScheduler { get; set; }
        public static bool DebugSettings { get; set; }
        public static bool DebugShipTargetValues { get; set; }
        public static bool DebugSkillTraining { get; set; }
        public static bool DebugSpeedMod { get; set; }
        public static bool DebugStatistics { get; set; }
        public static bool DebugStorylineMissions { get; set; }
        public static bool DebugTargetCombatants { get; set; }
        public static bool DebugTargetWrecks { get; set; }
        public static bool DebugTractorBeams { get; set; }
        public static bool DebugTraveler { get; set; }
        public static bool DebugUI { get; set; }
        public static bool DebugUndockBookmarks { get; set; }
        public static bool DebugUnloadLoot { get; set; }
        public static bool DebugValuedump { get; set; }
        public static bool DebugWalletBalance { get; set; }
        public static bool DebugWeShouldBeInSpaceORInStationAndOutOfSessionChange { get; set; }
        public static bool DebugWatchForActiveWars { get; set; }
    }
}
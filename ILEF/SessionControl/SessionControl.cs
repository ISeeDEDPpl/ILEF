#pragma warning disable 1591

namespace ILEF.SessionControl
{
    using System;
    using System.Linq;
    using System.Diagnostics;
    using global::ILoveEVE.Framework;
    using global::ILEF.Core;
    using global::ILEF.Logging;
    using global::ILEF.KanedaToolkit;

    /// <summary>
    /// Userprofile for an eve account including play sessions, can be serialized
    /// </summary>
    [Serializable]
    public class Profile
    {
        public string Username;
        public string Password;
        public long CharacterID;
    }

    /// <summary>
    /// Global settings for SessionControl class
    /// </summary>
    public class LoginGlobalSettings : Settings
    {
        public LoginGlobalSettings() : base("Login") { }
        /// <summary>
        /// Available userprofiles, keyed by the character name
        /// </summary>
        public new SerializableDictionary<string, Profile> Profiles = new SerializableDictionary<string, Profile>();
    }

    /// <summary>
    /// Session state persistence
    /// </summary>
    public class LoginGlobalState : Settings {
        public LoginGlobalState() : base("Login.State") { }
        public SerializableDictionary<long, DateTime> SessionStart = new SerializableDictionary<long, DateTime>();
        public SerializableDictionary<long, bool> Reconnect = new SerializableDictionary<long, bool>();
    }

/// <summary>
/// Profile-based settings for SessionControl class
/// </summary>
public class LoginLocalSettings : Settings
    {
        public string Mode = "Duration";
        public int LoginDelta = 0;
        public int LogoutHours = 24;
        public int LogoutDelta = 20;
        public int Downtime = 30;
        public int DowntimeDelta = 10;
        public DateTime PeriodStart = DateTime.Now;
        public DateTime PeriodEnd = DateTime.Now.AddHours(2);
    }

    /// <summary>
    /// Sessioncontrol provides interface for logging in and out of Eve and awareness of downtime
    /// </summary>
    public class SessionControl : State
    {
        #region Instantiation

        static SessionControl _Instance;
        /// <summary>
        /// Singletoner
        /// </summary>
        public static SessionControl Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new SessionControl();
                }
                return _Instance;
            }
        }

        public DirectEve DirectEve { get; set; }

        private SessionControl()
        {
            LoginDelta = random.Next(Config.LoginDelta);
            QueueState(LoginScreen);
            QueueState(CharScreen);
            QueueState(Monitor);
        }

        #endregion

        #region Variables

        /// <summary>
        /// Global config containing all login information
        /// </summary>
        public LoginGlobalSettings GlobalConfig = new LoginGlobalSettings();
        /// <summary>
        /// Global state
        /// </summary>
        public LoginGlobalState GlobalState = new LoginGlobalState();
        /// <summary>
        /// Config for this class
        /// </summary>
        public LoginLocalSettings Config = new LoginLocalSettings();
        /// <summary>
        /// Log for this class
        /// </summary>
        public Logger Log = new Logger("LoginControl");
        /// <summary>
        /// The character name to work with
        /// </summary>
        public string characterName { get; set; }

        DateTime Instanced = DateTime.Now;
        Random random = new Random();
        int DowntimeDelta = 0;
        int LoginDelta = 0;
        int LogoutDelta = 0;

        private Profile _curProfile;

        #endregion

        #region Events

        /// <summary>
        /// Fired when LoginControl thinks it is time to get ready to log out, call PerformLogout afterwards to finish it
        /// </summary>
        public event Action LogOut;

        #endregion

        #region Actions

        /// <summary>
        /// Sets up _curProfile with data from GlobalConfig
        /// </summary>
        public void UpdateCurrentProfile()
        {
            if (characterName == null) characterName = Cache.Instance.Name;
            if (characterName != null && GlobalConfig.Profiles.ContainsKey(characterName)) _curProfile = GlobalConfig.Profiles[characterName];
        }

        /// <summary>
        /// Perform a logout (closes the client)
        /// </summary>
        public void PerformLogout()
        {
            QueueState(Logout);
        }

        /// <summary>
        /// Opens up the configuration dialog, this is a MODAL dialog and will block the thread!
        /// </summary>
        public void Configure()
        {
            UI.SessionControl Configuration = new UI.SessionControl();
            Configuration.ShowDialog();
        }

        public void NewDowntimeDelta()
        {
            DowntimeDelta = random.Next(Config.DowntimeDelta);
        }
        public void NewLoginDelta()
        {
            LoginDelta = random.Next(Config.LoginDelta);
        }
        #endregion

        #region States

        #region Utility

        #endregion

        #region LoggingIn

        bool LoginScreen(object[] Params)
        {
            UpdateCurrentProfile();
            if (DirectEve.Session.IsInSpace || DirectEve.Session.IsInStation || DirectEve.Login.AtCharacterSelection) return true;

            if (DirectEve.Login.AtLogin)
            {
                if (DirectEve.Login.IsLoading) return false;
                if (DirectEve.Login.ServerStatus != "Status: OK") return false;

                if (Cache.Instance.DirectEve.Windows.Count != 0)
                {
                    foreach (DirectWindow window in Cache.Instance.DirectEve.Windows)
                    {
                        if (string.IsNullOrEmpty(window.Html))
                            continue;
                        Logging.Log("Startup", "WindowTitles:" + window.Name + "::" + window.Html, Logging.White);

                        //
                        // Close these windows and continue
                        //
                        if (window.Name == "telecom" && !Logging.DebugDoNotCloseTelcomWindows)
                        {
                            Logging.Log("Startup", "Closing telecom message...", Logging.Yellow);
                            Logging.Log("Startup", "Content of telecom window (HTML): [" + (window.Html).Replace("\n", "").Replace("\r", "") + "]", Logging.Yellow);
                            window.Close();
                            continue;
                        }

                        // Modal windows must be closed
                        // But lets only close known modal windows
                        if (window.IsModal)
                        {
                            bool close = false;
                            bool restart = false;
                            bool needHumanIntervention = false;
                            bool sayYes = false;
                            bool sayOk = false;
                            bool quit = false;

                            //bool update = false;

                            if (!string.IsNullOrEmpty(window.Html))
                            {
                                //errors that are repeatable and unavoidable even after a restart of eve/questor
                                needHumanIntervention = window.Html.Contains("reason: Account subscription expired");

                                //update |= window.Html.Contains("The update has been downloaded");

                                // Server going down
                                //Logging.Log("[Startup] (1) close is: " + close);
                                close |= window.Html.ToLower().Contains("please make sure your characters are out of harms way");
                                close |= window.Html.ToLower().Contains("accepting connections");
                                close |= window.Html.ToLower().Contains("could not connect");
                                close |= window.Html.ToLower().Contains("the connection to the server was closed");
                                close |= window.Html.ToLower().Contains("server was closed");
                                close |= window.Html.ToLower().Contains("make sure your characters are out of harm");
                                close |= window.Html.ToLower().Contains("connection to server lost");
                                close |= window.Html.ToLower().Contains("the socket was closed");
                                close |= window.Html.ToLower().Contains("the specified proxy or server node");
                                close |= window.Html.ToLower().Contains("starting up");
                                close |= window.Html.ToLower().Contains("unable to connect to the selected server");
                                close |= window.Html.ToLower().Contains("could not connect to the specified address");
                                close |= window.Html.ToLower().Contains("connection timeout");
                                close |= window.Html.ToLower().Contains("the cluster is not currently accepting connections");
                                close |= window.Html.ToLower().Contains("your character is located within");
                                close |= window.Html.ToLower().Contains("the transport has not yet been connected");
                                close |= window.Html.ToLower().Contains("the user's connection has been usurped");
                                close |= window.Html.ToLower().Contains("the EVE cluster has reached its maximum user limit");
                                close |= window.Html.ToLower().Contains("the connection to the server was closed");
                                close |= window.Html.ToLower().Contains("client is already connecting to the server");

                                //close |= window.Html.Contains("A client update is available and will now be installed");
                                //
                                // eventually it would be nice to hit ok on this one and let it update
                                //
                                close |= window.Html.ToLower().Contains("client update is available and will now be installed");
                                close |= window.Html.ToLower().Contains("change your trial account to a paying account");

                                //
                                // these windows require a restart of eve all together
                                //
                                restart |= window.Html.ToLower().Contains("the connection was closed");
                                restart |= window.Html.ToLower().Contains("connection to server lost."); //INFORMATION
                                restart |= window.Html.ToLower().Contains("local cache is corrupt");
                                sayOk |= window.Html.ToLower().Contains("local session information is corrupt");
                                restart |= window.Html.ToLower().Contains("The client's local session"); // information is corrupt");
                                restart |= window.Html.ToLower().Contains("restart the client prior to logging in");

                                //
                                // these windows require a quit of eve all together
                                //
                                quit |= window.Html.ToLower().Contains("the socket was closed");

                                //
                                // Modal Dialogs the need "yes" pressed
                                //
                                //sayYes |= window.Html.Contains("There is a new build available. Would you like to download it now");
                                //sayOk |= window.Html.Contains("The update has been downloaded. The client will now close and the update process begin");
                                sayOk |= window.Html.Contains("The transport has not yet been connected, or authentication was not successful");

                                //Logging.Log("[Startup] (2) close is: " + close);
                                //Logging.Log("[Startup] (1) window.Html is: " + window.Html);
                            }

                            //if (update)
                            //{
                            //    int secRestart = (400 * 3) + Cache.Instance.RandomNumber(3, 18) * 100 + Cache.Instance.RandomNumber(1, 9) * 10;
                            //    LavishScript.ExecuteCommand("uplink exec Echo [${Time}] timedcommand " + secRestart + " OSExecute taskkill /IM launcher.exe");
                            //}

                            if (sayYes)
                            {
                                Logging.Log("Startup", "Found a window that needs 'yes' chosen...", Logging.White);
                                Logging.Log("Startup", "Content of modal window (HTML): [" + (window.Html).Replace("\n", "").Replace("\r", "") + "]", Logging.White);
                                window.AnswerModal("Yes");
                                continue;
                            }

                            if (sayOk)
                            {
                                Logging.Log("Startup", "Found a window that needs 'ok' chosen...", Logging.White);
                                Logging.Log("Startup", "Content of modal window (HTML): [" + (window.Html).Replace("\n", "").Replace("\r", "") + "]", Logging.White);
                                window.AnswerModal("OK");
                                if (window.Html.Contains("The update has been downloaded. The client will now close and the update process begin"))
                                {
                                    //
                                    // schedule the closing of launcher.exe via a timedcommand (10 min?) in the uplink...
                                    //
                                }
                                continue;
                            }

                            if (quit)
                            {
                                Logging.Log("Startup", "Restarting eve...", Logging.Red);
                                Logging.Log("Startup", "Content of modal window (HTML): [" + (window.Html).Replace("\n", "").Replace("\r", "") + "]", Logging.Red);
                                window.AnswerModal("quit");

                                //_directEve.ExecuteCommand(DirectCmd.CmdQuitGame);
                            }

                            if (restart)
                            {
                                Logging.Log("Startup", "Restarting eve...", Logging.Red);
                                Logging.Log("Startup", "Content of modal window (HTML): [" + (window.Html).Replace("\n", "").Replace("\r", "") + "]", Logging.Red);
                                window.AnswerModal("restart");
                                continue;
                            }

                            if (close)
                            {
                                Logging.Log("Startup", "Closing modal window...", Logging.Yellow);
                                Logging.Log("Startup", "Content of modal window (HTML): [" + (window.Html).Replace("\n", "").Replace("\r", "") + "]", Logging.Yellow);
                                window.Close();
                                continue;
                            }

                            if (needHumanIntervention)
                            {
                                Logging.Log("Startup", "ERROR! - Human Intervention is required in this case: halting all login attempts - ERROR!", Logging.Red);
                                Logging.Log("Startup", "window.Name is: " + window.Name, Logging.Red);
                                Logging.Log("Startup", "window.Html is: " + window.Html, Logging.Red);
                                Logging.Log("Startup", "window.Caption is: " + window.Caption, Logging.Red);
                                Logging.Log("Startup", "window.Type is: " + window.Type, Logging.Red);
                                Logging.Log("Startup", "window.ID is: " + window.Id, Logging.Red);
                                Logging.Log("Startup", "window.IsDialog is: " + window.IsDialog, Logging.Red);
                                Logging.Log("Startup", "window.IsKillable is: " + window.IsKillable, Logging.Red);
                                Logging.Log("Startup", "window.Viewmode is: " + window.ViewMode, Logging.Red);
                                Logging.Log("Startup", "ERROR! - Human Intervention is required in this case: halting all login attempts - ERROR!", Logging.Red);
                                //_humanInterventionRequired = true;
                                continue;
                            }
                        }

                        if (string.IsNullOrEmpty(window.Html))
                            continue;

                        if (window.Name == "telecom")
                            continue;
                        Logging.Log("Startup", "We have an unexpected window, auto login halted.", Logging.Red);
                        Logging.Log("Startup", "window.Name is: " + window.Name, Logging.Red);
                        Logging.Log("Startup", "window.Html is: " + window.Html, Logging.Red);
                        Logging.Log("Startup", "window.Caption is: " + window.Caption, Logging.Red);
                        Logging.Log("Startup", "window.Type is: " + window.Type, Logging.Red);
                        Logging.Log("Startup", "window.ID is: " + window.Id, Logging.Red);
                        Logging.Log("Startup", "window.IsDialog is: " + window.IsDialog, Logging.Red);
                        Logging.Log("Startup", "window.IsKillable is: " + window.IsKillable, Logging.Red);
                        Logging.Log("Startup", "window.Viewmode is: " + window.ViewMode, Logging.Red);
                        Logging.Log("Startup", "We have got an unexpected window, auto login halted.", Logging.Red);
                        continue;
                    }
                }

                if (DirectEve.Login.IsConnecting) return false;

                if (_curProfile != null)
                {
                    if (Config.Mode == "Duration" && DateTime.Now <= Instanced.AddMinutes(LoginDelta)) return false;
                    if (Config.Mode == "Period" && DateTime.Now.TimeOfDay < Config.PeriodStart.TimeOfDay) return false;
                    if (Config.Mode == "Period" && DateTime.Now.TimeOfDay > Config.PeriodEnd.TimeOfDay)
                    {
                        Log.Log("|oRun period already complete, closing");
                        Clear();
                        QueueState(Logout);
                    }
                    Log.Log("|oLogging into account");
                    Log.Log(" |g{0}", _curProfile.Username);
                    DirectEve.Login.Login(_curProfile.Username, _curProfile.Password);
                    InsertState(LoginScreen);
                    WaitFor(5, () => DirectEve.Login.IsConnecting);
                    return true;
                }
            }
            return false;
        }

        bool CharScreen(object[] Params)
        {
            UpdateCurrentProfile();
            if (DirectEve.Session.IsInSpace || DirectEve.Session.IsInStation)
            {
                if (_curProfile != null)
                {
                    if (GlobalState.Reconnect == null || !GlobalState.Reconnect.ContainsKey(_curProfile.CharacterID) || !GlobalState.Reconnect[_curProfile.CharacterID])
                    {
                        GlobalState.SessionStart.AddOrUpdate(_curProfile.CharacterID, DateTime.Now);
                        GlobalState.Reconnect.AddOrUpdate(_curProfile.CharacterID, false);
                        GlobalState.Save();
                    }
                    else
                    {
                        GlobalState.Reconnect.AddOrUpdate(_curProfile.CharacterID, false);
                        GlobalState.Save();
                    }
                }
                DowntimeDelta = random.Next(Config.DowntimeDelta);
                LogoutDelta = random.Next(Config.LogoutDelta);
                return true;
            }
            if (DirectEve.Login.IsLoading) return false;

            if (DirectEve.Login.AtCharacterSelection && DirectEve.Login.IsCharacterSelectionReady)
            {
                if (_curProfile != null)
                {
                    DirectLoginSlot character = DirectEve.Login.CharacterSlots.FirstOrDefault(a => a.CharId == _curProfile.CharacterID);
                    if (character != null)
                    {
                        Log.Log("|oActivating character");
                        Log.Log(" |g{0}", characterName);
                        character.Activate();
                        InsertState(CharScreen);
                        WaitFor(5, () => DirectEve.Login.AtCharacterSelection);
                        return true;
                    }
                }
                Log.Log("|rUnable to find character, check configuration");
                Clear();
                return true;
            }

            return false;
        }

        #endregion

        bool Monitor(object[] Params)
        {
            UpdateCurrentProfile();

            if (Config.Mode == "Period")
            {
                if (DateTime.Now.TimeOfDay > Config.PeriodEnd.TimeOfDay)
                {
                    if (LogOut != null)
                    {
                        LogOut();
                    }
                    return true;
                }
            }

            if (Config.Mode == "Duration")
            {
                if (_curProfile != null)
                {
                    try
                    {
                        if (DateTime.Now > GlobalState.SessionStart[_curProfile.CharacterID].AddHours(Config.LogoutHours).AddMinutes(LogoutDelta))
                            //|| DirectEve.Session.Now.AddMinutes(Config.Downtime + DowntimeDelta) > DirectEve.Session.NextDowntime)
                        {
                            if (LogOut != null)
                            {
                                LogOut();
                            }
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #region LoggingOut

        bool Logout(object[] Params)
        {
            //LavishScript.ExecuteCommand("Exit");
            Process.GetCurrentProcess().Kill();
            Environment.Exit(0);
            return true;
        }

        #endregion

        #endregion
    }
}

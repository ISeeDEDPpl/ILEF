
namespace ILEF.Activities
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Timers;
    using System.Xml.Linq;
    using ILoveEVE.Framework;
    using DirectEve;
    using global::ILEF.BackgroundTasks;
    using global::ILEF.Caching;
    using global::ILEF.Logging;
    using global::ILEF.Lookup;
    using global::ILEF.States;
    public static class LoginToEVE
    {
        public static bool loggedInAndreadyToStartQuestorUI;
        public static bool useLoginOnFrameEvent;
        public static List<CharSchedule> CharSchedules { get; private set; }
        public static DateTime QuestorProgramLaunched = DateTime.UtcNow;
        private static bool _questorScheduleSaysWeShouldLoginNow;
        public static DateTime QuestorSchedulerReadyToLogin = DateTime.UtcNow;
        public static DateTime EVEAccountLoginStarted = DateTime.UtcNow;
        public static DateTime NextSlotActivate = DateTime.UtcNow;
        public static bool _loginOnly;
        public static bool _showHelp;

        private static bool __chantlingScheduler;

        public static bool _chantlingScheduler
        {
            get
            {
                return __chantlingScheduler;
            }
            set
            {
                __chantlingScheduler = value;
                if (__chantlingScheduler == false && string.IsNullOrEmpty(Logging.MyCharacterName))
                {
                    Logging.Log("Startup", "We were told to use the scheduler but we are Missing the CharacterName to login with...", Logging.Debug);
                }
            }
        }

        private static bool __loginNowIgnoreScheduler;

        public static bool _loginNowIgnoreScheduler
        {
            get
            {
                return __loginNowIgnoreScheduler;
            }
            set
            {
                __loginNowIgnoreScheduler = value;
                _chantlingScheduler = false;
            }
        }

        public static bool _standaloneInstance
        {
            get
            {
                return !Logging.UseInnerspace;
            }
            set
            {
                //Logging.Log("Startup", "Setting: UseInnerspace = [" + !value + "]", Logging.White);
                Logging.UseInnerspace = !value;
            }

        }

        public static bool _loadAdaptEVE;

        private static double _minutesToStart;
        private static bool? _readyToLoginEVEAccount;

        public static bool ReadyToLoginToEVEAccount
        {
            get
            {
                try
                {
                    return _readyToLoginEVEAccount ?? false;
                }
                catch (Exception ex)
                {
                    Logging.Log("ReadyToLoginToEVE", "Exception [" + ex + "]", Logging.Debug);
                    return false;
                }
            }

            set
            {
                _readyToLoginEVEAccount = value;
                if (value) //if true
                {
                    QuestorSchedulerReadyToLogin = DateTime.UtcNow;
                }
            }
        }
        private static bool _humanInterventionRequired;
        public static bool MissingEasyHookWarningGiven;
        private static readonly System.Timers.Timer Timer = new System.Timers.Timer();
        private const int RandStartDelay = 30; //Random startup delay in minutes
        private static readonly Random R = new Random();
        private static int ServerStatusCheck = 0;
        private static DateTime _nextPulse;
        private static DateTime _lastServerStatusCheckWasNotOK = DateTime.MinValue;
        public static DateTime StartTime = DateTime.MaxValue;
        public static DateTime StopTime = DateTime.MinValue;
        public static DateTime DoneLoggingInToEVETimeStamp = DateTime.MaxValue;
        public static List<string> _QuestorParamaters;
        public static string PreLoginSettingsINI;

        public static bool OptionallyLoadPreLoginSettingsFromINI(IList<string> args)
        {
            if (!string.IsNullOrEmpty(Logging.EVELoginUserName) &&
                !string.IsNullOrEmpty(Logging.EVELoginPassword) &&
                !string.IsNullOrEmpty(Logging.MyCharacterName))
            {
                Logging.Log("Startup", "EVELoginUserName, EVELoginPassword and MyCharacterName all specified at the command line, not loading settings from ini.", Logging.White);
                return false;
            }

            //
            // (optionally) Load EVELoginUserName, EVELoginPassword, MyCharacterName (and other settings) from an ini
            //
            if (args.Count() == 1 && args[0].ToLower().EndsWith(".ini"))
            {
                PreLoginSettingsINI = System.IO.Path.Combine(Logging.PathToCurrentDirectory, args[0]);
            }
            else if (args.Count() == 2 && args[1].ToLower().EndsWith(".ini"))
            {
                PreLoginSettingsINI = System.IO.Path.Combine(Logging.PathToCurrentDirectory, args[0] + " " + args[1]);
            }
            if (!string.IsNullOrEmpty(PreLoginSettingsINI) && File.Exists(PreLoginSettingsINI))
            {
                Logging.Log("Startup", "Found [" + PreLoginSettingsINI + "] loading Questor PreLogin Settings", Logging.White);
                if (!PreLoginSettings(PreLoginSettingsINI))
                {
                    Logging.Log("Startup.PreLoginSettings", "Failed to load PreLogin settings from [" + PreLoginSettingsINI + "]", Logging.Debug);
                    return false;
                }

                Logging.Log("Startup", "Successfully loaded PreLogin Settings", Logging.White);
                return true;
            }

            return false;
        }

        public static bool LoadDirectEVEInstance()
        {
            #region Load DirectEVE

            //
            // Load DirectEVE
            //

            try
            {
                bool EasyHookExists = File.Exists(System.IO.Path.Combine(Logging.PathToCurrentDirectory, "EasyHook.dll"));
                if (!EasyHookExists && !LoginToEVE.MissingEasyHookWarningGiven)
                {
                    Logging.Log("Startup", "EasyHook DLL's are missing. Please copy them into the same directory as your questor.exe", Logging.Orange);
                    Logging.Log("Startup", "halting!", Logging.Orange);
                    LoginToEVE.MissingEasyHookWarningGiven = true;
                    return false;
                }

                int TryLoadingDirectVE = 0;
                while (QMCache.Instance.DirectEve == null && TryLoadingDirectVE < 30)
                {
                    if (!Logging.UseInnerspace)
                    {
                        try
                        {
                            Logging.Log("Startup", "Starting Instance of DirectEVE using StandaloneFramework - broken!", Logging.Debug);
                            //QMCache.Instance.DirectEve = new DirectEve(new StandaloneFramework());
                            TryLoadingDirectVE++;
                            Logging.Log("Startup", "DirectEVE should now be active: see above for any messages from DirectEVE", Logging.Debug);
                            return true;
                        }
                        catch (Exception exception)
                        {
                            Logging.Log("Startup", "exception [" + exception + "]", Logging.Orange);
                            continue;
                        }
                    }

                    try
                    {
                        Logging.Log("Startup", "Starting Instance of DirectEVE using Innerspace", Logging.Debug);
                        QMCache.Instance.DirectEve = new DirectEve();
                        TryLoadingDirectVE++;
                        Logging.Log("Startup", "DirectEVE should now be active: see above for any messages from DirectEVE", Logging.Debug);
                        return true;
                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Startup", "exception [" + exception + "]", Logging.Orange);
                        continue;
                    }
                }
            }
            catch (Exception exception)
            {
                Logging.Log("Startup", "exception [" + exception + "]", Logging.Orange);
                return false;
            }

            if (QMCache.Instance.DirectEve == null)
            {
                try
                {
                    Logging.Log("Startup", "Error on Loading DirectEve, maybe server is down", Logging.Orange);
                    QMCache.Instance.CloseQuestorCMDLogoff = false;
                    QMCache.Instance.CloseQuestorCMDExitGame = true;
                    QMCache.Instance.CloseQuestorEndProcess = true;
                    Cleanup.ReasonToStopQuestor = "Error on Loading DirectEve, maybe server is down";
                    Cleanup.SignalToQuitQuestorAndEVEAndRestartInAMoment = true;
                    Cleanup.CloseQuestor(Cleanup.ReasonToStopQuestor, true);
                    return false;
                }
                catch (Exception exception)
                {
                    Logging.BasicLog("Startup", "Exception while logging exception, oh joy [" + exception + "]");
                    return false;
                }
            }

            return true;
            #endregion Load DirectEVE
        }

        public static void WaitToLoginUntilSchedulerSaysWeShould()
        {
            string path = Logging.PathToCurrentDirectory;
            Logging.MyCharacterName = Logging.MyCharacterName.Replace("\"", ""); // strip quotation marks if any are present


            CharSchedules = new List<CharSchedule>();
            if (path != null)
            {
                //
                // we should add a check for a missing schedules.xml here and log to the user if it is missing
                //
                XDocument values = XDocument.Load(Path.Combine(path, "Schedules.xml"));
                if (values.Root != null)
                {
                    foreach (XElement value in values.Root.Elements("char"))
                    {
                        CharSchedules.Add(new CharSchedule(value));
                    }
                }
            }

            //
            // chantling scheduler
            //
            CharSchedule schedule = CharSchedules.FirstOrDefault(v => v.ScheduleCharacterName == Logging.MyCharacterName);
            if (schedule == null)
            {
                Logging.Log("Startup", "Error - character [" + Logging.MyCharacterName + "] not found in Schedules.xml!", Logging.Red);
                return;
            }

            if (schedule.LoginUserName == null || schedule.LoginPassWord == null)
            {
                Logging.Log("Startup", "Error - Login details not specified in Schedules.xml!", Logging.Red);
                return;
            }

            Logging.EVELoginUserName = schedule.LoginUserName;
            Logging.EVELoginPassword = schedule.LoginPassWord;
            Logging.Log("Startup", "User: " + schedule.LoginUserName + " Name: " + schedule.ScheduleCharacterName, Logging.White);

            if (schedule.StartTimeSpecified)
            {
                if (schedule.Start1 > schedule.Stop1) schedule.Stop1 = schedule.Stop1.AddDays(1);
                if (DateTime.Now.AddHours(2) > schedule.Start1 && DateTime.Now < schedule.Stop1)
                {
                    StartTime = schedule.Start1;
                    StopTime = schedule.Stop1;
                    Time.Instance.StopTimeSpecified = true;
                    Logging.Log("Startup", "Schedule1: Start1: " + schedule.Start1 + " Stop1: " + schedule.Stop1, Logging.White);
                }
            }

            if (schedule.StartTime2Specified)
            {
                if (schedule.Start2 > schedule.Stop2) schedule.Stop2 = schedule.Stop2.AddDays(1);
                if (DateTime.Now.AddHours(2) > schedule.Start2 && DateTime.Now < schedule.Stop2)
                {
                    StartTime = schedule.Start2;
                    StopTime = schedule.Stop2;
                    Time.Instance.StopTimeSpecified = true;
                    Logging.Log("Startup", "Schedule2: Start2: " + schedule.Start2 + " Stop2: " + schedule.Stop2, Logging.White);
                }
            }

            if (schedule.StartTime3Specified)
            {
                if (schedule.Start3 > schedule.Stop3) schedule.Stop3 = schedule.Stop3.AddDays(1);
                if (DateTime.Now.AddHours(2) > schedule.Start3 && DateTime.Now < schedule.Stop3)
                {
                    StartTime = schedule.Start3;
                    StopTime = schedule.Stop3;
                    Time.Instance.StopTimeSpecified = true;
                    Logging.Log("Startup", "Schedule3: Start3: " + schedule.Start3 + " Stop3: " + schedule.Stop3, Logging.White);
                }
            }

            //
            // if we have not found a workable schedule yet assume schedule 1 is correct. what we want.
            //
            if (schedule.StartTimeSpecified && StartTime == DateTime.MaxValue)
            {
                StartTime = schedule.Start1;
                StopTime = schedule.Stop1;
                Logging.Log("Startup", "Forcing Schedule 1 because none of the schedules started within 2 hours", Logging.White);
                Logging.Log("Startup", "Schedule 1: Start1: " + schedule.Start1 + " Stop1: " + schedule.Stop1, Logging.White);
            }

            if (schedule.StartTimeSpecified || schedule.StartTime2Specified || schedule.StartTime3Specified)
            {
                StartTime = StartTime.AddSeconds(R.Next(0, (RandStartDelay * 60)));
            }

            if ((DateTime.Now > StartTime))
            {
                if ((DateTime.Now.Subtract(StartTime).TotalMinutes < 1200)) //if we're less than x hours past start time, start now
                {
                    StartTime = DateTime.Now;
                    _questorScheduleSaysWeShouldLoginNow = true;
                }
                else
                {
                    StartTime = StartTime.AddDays(1); //otherwise, start tomorrow at start time
                }
            }
            else if ((StartTime.Subtract(DateTime.Now).TotalMinutes > 1200)) //if we're more than x hours shy of start time, start now
            {
                StartTime = DateTime.Now;
                _questorScheduleSaysWeShouldLoginNow = true;
            }

            if (StopTime < StartTime)
            {
                StopTime = StopTime.AddDays(1);
            }

            //if (schedule.RunTime > 0) //if runtime is specified, overrides stop time
            //    StopTime = StartTime.AddMinutes(schedule.RunTime); //minutes of runtime

            //if (schedule.RunTime < 18 && schedule.RunTime > 0)     //if runtime is 10 or less, assume they meant hours
            //    StopTime = StartTime.AddHours(schedule.RunTime);   //hours of runtime

            if (_loginNowIgnoreScheduler)
            {
                _questorScheduleSaysWeShouldLoginNow = true;
            }
            else
            {
                Logging.Log("Startup", " Start Time: " + StartTime + " - Stop Time: " + StopTime, Logging.White);
            }

            if (!_questorScheduleSaysWeShouldLoginNow)
            {
                _minutesToStart = StartTime.Subtract(DateTime.Now).TotalMinutes;
                Logging.Log("Startup", "Starting at " + StartTime + ". " + String.Format("{0:0.##}", _minutesToStart) + " minutes to go.", Logging.Yellow);
                Timer.Elapsed += new ElapsedEventHandler(TimerEventProcessor);
                if (_minutesToStart > 0)
                {
                    Timer.Interval = (int)(_minutesToStart * 60000);
                }
                else
                {
                    Timer.Interval = 1000;
                }

                Timer.Enabled = true;
                Timer.Start();
            }
            else
            {
                ReadyToLoginToEVEAccount = true;
                Logging.Log("Startup", "Already passed start time.  Starting in 15 seconds.", Logging.White);
                System.Threading.Thread.Sleep(15000);
            }

            //
            // chantling scheduler (above)
            //
        }

        public static void LoginOnFrame(object sender, EventArgs e)
        {
            // New frame, invalidate old cache
            //if (Logging.DebugOnframe) Logging.Log("LoginOnFrame", "EveryFrame: Stating Cache.InvalidateCache", Logging.White);
            QMCache.Instance.InvalidateCache();

            //if (Logging.DebugOnframe) Logging.Log("LoginOnFrame", "EveryFrame: Done with InvalidateCache", Logging.White);
            Time.Instance.LastFrame = DateTime.UtcNow;
            Time.Instance.LastSessionIsReady = DateTime.UtcNow;
            //update this regardless before we login there is no session

            if (Cleanup.SignalToQuitQuestorAndEVEAndRestartInAMoment)
            {
                if (_States.CurrentQuestorState != QuestorState.CloseQuestor)
                {
                    _States.CurrentQuestorState = QuestorState.CloseQuestor;
                    Cleanup.BeginClosingQuestor();
                }
            }

            if (DateTime.UtcNow < _lastServerStatusCheckWasNotOK.AddSeconds(RandomNumber(10, 20)))
            {
                //Logging.Log("LoginOnFrame", "lastServerStatusCheckWasNotOK = [" + _lastServerStatusCheckWasNotOK.ToShortTimeString() + "] waiting 10 to 20 seconds.", Logging.White);
                return;
            }

            _lastServerStatusCheckWasNotOK = DateTime.UtcNow.AddDays(-1); //reset this so we never hit this twice in a row w/o another server status check not being OK.

            if (DateTime.UtcNow < _nextPulse)
            {
                if (Logging.DebugOnframe) Logging.Log("LoginOnFrame", "if (DateTime.UtcNow < _nextPulse)", Logging.White);
                return;
            }

            if (Logging.DebugOnframe) Logging.Log("LoginOnFrame", "Pulse...", Logging.White);


            _nextPulse = DateTime.UtcNow.AddMilliseconds(Time.Instance.QuestorBeforeLoginPulseDelay_milliseconds);

            if (DateTime.UtcNow < QuestorProgramLaunched.AddSeconds(7))
            {
                //
                // do not login for the first 7 seconds, wait...
                //
                return;
            }

            if (!ReadyToLoginToEVEAccount && QMCache.Instance.DirectEve.Login.AtLogin)
            {
                Logging.Log("Startup", "if (!ReadyToLoginToEVEAccount)", Logging.White);
                return;
            }

            if (_chantlingScheduler && !string.IsNullOrEmpty(Logging.MyCharacterName) &&
                !_questorScheduleSaysWeShouldLoginNow)
            {
                Logging.Log("Startup", "if (_chantlingScheduler && !string.IsNullOrEmpty(Logging._character) && !_questorScheduleSaysWeShouldLoginNow)", Logging.White);
                return;
            }

            if (_humanInterventionRequired)
            {
                Logging.Log("Startup", "OnFrame: _humanInterventionRequired is true (this will spam every second or so)", Logging.Orange);
                return;
            }

            // If the session is ready, then we are done :)
            if (QMCache.Instance.DirectEve.Session.IsReady)
            {
                if (DateTime.UtcNow > Time.Instance.LoginStarted_DateTime.AddSeconds(50))
                {
                    Logging.Log("Startup", "We have successfully logged in", Logging.White);
                    Time.Instance.LastSessionIsReady = DateTime.UtcNow;
                    useLoginOnFrameEvent = false;
                }

                return;
            }

            if (Logging.DebugOnframe) Logging.Log("LoginOnFrame", "before: if (QMCache.Instance.DirectEve.Windows.Count != 0)", Logging.White);

            // We should not get any windows
            if (QMCache.Instance.DirectEve.Windows.Count != 0)
            {
                foreach (DirectWindow window in QMCache.Instance.DirectEve.Windows)
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
                            _humanInterventionRequired = true;
                            return;
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
                    return;
                }
                return;
            }

            if (QMCache.Instance.DirectEve.Login.AtLogin && QMCache.Instance.DirectEve.Login.ServerStatus != "Status: OK")
            {
                if (ServerStatusCheck <= 20) // at 10 sec a piece this would be 200+ seconds
                {
                    Logging.Log("Startup", "Server status[" + QMCache.Instance.DirectEve.Login.ServerStatus + "] != [OK] try later", Logging.Orange);
                    ServerStatusCheck++;
                    //retry the server status check twice (with 1 sec delay between each) before kicking in a larger delay
                    if (ServerStatusCheck > 2)
                    {
                        _lastServerStatusCheckWasNotOK = DateTime.UtcNow;
                    }

                    return;
                }

                ServerStatusCheck = 0;
                Cleanup.ReasonToStopQuestor = "Server Status Check shows server still not ready after more than 3 min. Restarting Questor. ServerStatusCheck is [" + ServerStatusCheck + "]";
                Logging.Log("Startup", Cleanup.ReasonToStopQuestor, Logging.Red);
                Time.EnteredCloseQuestor_DateTime = DateTime.UtcNow;
                Cleanup.CloseQuestor(Cleanup.ReasonToStopQuestor, true);
                return;
            }

            if (QMCache.Instance.DirectEve.Login.AtLogin && !QMCache.Instance.DirectEve.Login.IsLoading && !QMCache.Instance.DirectEve.Login.IsConnecting)
            {
                if (DateTime.UtcNow.Subtract(QuestorSchedulerReadyToLogin).TotalMilliseconds > RandomNumber(Time.Instance.EVEAccountLoginDelayMinimum_seconds * 1000, Time.Instance.EVEAccountLoginDelayMaximum_seconds * 1000))
                {
                    Logging.Log("Startup", "Login account [" + Logging.EVELoginUserName + "]", Logging.White);
                    QMCache.Instance.DirectEve.Login.Login(Logging.EVELoginUserName, Logging.EVELoginPassword);
                    EVEAccountLoginStarted = DateTime.UtcNow;
                    Logging.Log("Startup", "Waiting for Character Selection Screen", Logging.White);
                    return;
                }
            }

            if (QMCache.Instance.DirectEve.Login.AtCharacterSelection && QMCache.Instance.DirectEve.Login.IsCharacterSelectionReady && !QMCache.Instance.DirectEve.Login.IsConnecting && !QMCache.Instance.DirectEve.Login.IsLoading)
            {
                if (DateTime.UtcNow.Subtract(EVEAccountLoginStarted).TotalMilliseconds > RandomNumber(Time.Instance.CharacterSelectionDelayMinimum_seconds * 1000, Time.Instance.CharacterSelectionDelayMaximum_seconds * 1000) && DateTime.UtcNow > NextSlotActivate)
                {
                    foreach (DirectLoginSlot slot in QMCache.Instance.DirectEve.Login.CharacterSlots)
                    {
                        if (slot.CharId.ToString(CultureInfo.InvariantCulture) != Logging.MyCharacterName && System.String.Compare(slot.CharName, Logging.MyCharacterName, System.StringComparison.OrdinalIgnoreCase) != 0)
                        {
                            continue;
                        }

                        Logging.Log("Startup", "Activating character [" + slot.CharName + "]", Logging.White);
                        NextSlotActivate = DateTime.UtcNow.AddSeconds(5);
                        slot.Activate();
                        //EVECharacterSelected = DateTime.UtcNow;
                        return;
                    }

                    Logging.Log("Startup", "Character id/name [" + Logging.MyCharacterName + "] not found, retrying in 10 seconds", Logging.White);
                }
            }
        }

        private static void TimerEventProcessor(Object myObject, EventArgs myEventArgs)
        {
            Timer.Stop();
            Logging.Log("Startup", "Timer elapsed.  Starting now.", Logging.White);
            ReadyToLoginToEVEAccount = true;
            _questorScheduleSaysWeShouldLoginNow = true;
        }

        public static int RandomNumber(int min, int max)
        {
            Random random = new Random();
            return random.Next(min, max);
        }

        public static IEnumerable<string> SplitArguments(string commandLine)
        {
            var parmChars = commandLine.ToCharArray();
            var inSingleQuote = false;
            var inDoubleQuote = false;
            for (var index = 0; index < parmChars.Length; index++)
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

        public static bool PreLoginSettings(string iniFile)
        {
            if (string.IsNullOrEmpty(iniFile))
            {
                Logging.Log("PreLoginSettings", "iniFile was not passed to PreLoginSettings", Logging.Debug);
                return false;
            }

            try
            {
                if (!File.Exists(iniFile))
                {
                    Logging.Log("PreLoginSettings", "Could not find inifile named [" + iniFile + "]", Logging.Debug);
                }
                else
                {
                    Logging.Log("PreLoginSettings", "found a inifile named [" + Path.GetFileName(iniFile).Substring(0, 4) + "_MyINIFileRedacted_" + "]", Logging.Debug);
                }

                int index = 0;
                foreach (string line in File.ReadAllLines(iniFile))
                {
                    index++;
                    if (line.StartsWith(";"))
                    {
                        //Logging.Log("PreLoginSettings.Comment", line, Logging.Debug);
                        continue;
                    }

                    if (line.StartsWith("["))
                    {
                        //Logging.Log("PreLoginSettings.Section", line, Logging.Debug);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        //Logging.Log("PreLoginSettings.RawIniData-StartsWithWhitespaceSpace", line, Logging.Debug);
                        continue;
                    }

                    if (string.IsNullOrEmpty(line))
                    {
                        //Logging.Log("PreLoginSettings.RawIniData-IsNullOrEmpty", line, Logging.Debug);
                        continue;
                    }

                    //Logging.Log("PreLoginSettings.RawIniData", line, Logging.Debug);

                    string[] sLine = line.Split(new string[] { "=" }, StringSplitOptions.RemoveEmptyEntries);
                    //Logging.Log("PreLoginSettings", "Processing line: [" + index + "] of [" + Path.GetFileName(iniFile).Substring(0, 4) + "_MyINIFileRedacted_] Found Var: [" + sLine[0] + "] Found Value: [" + sLine[1] + "]", Logging.Debug);
                    //if (sLine.Count() != 2 && !sLine[0].Equals(ProxyUsername) && !sLine[0].Equals(ProxyPassword) )
                    if (sLine.Count() != 2)
                    {
                        Logging.Log("PreLoginSettings", "IniFile not right format at line: [" + index + "]", Logging.Debug);
                    }

                    switch (sLine[0].ToLower())
                    {
                        case "gameloginusername":
                            Logging.EVELoginUserName = sLine[1];
                            Logging.Log("PreLoginSettings", "EVELoginUserName [" + Logging.EVELoginUserName + "]", Logging.Debug);
                            break;

                        case "gameloginpassword":
                            Logging.EVELoginPassword = sLine[1];
                            Logging.Log("PreLoginSettings", "EVELoginPassword [" + Logging.EVELoginPassword + "]", Logging.Debug);
                            break;

                        case "eveloginusername":
                            Logging.EVELoginUserName = sLine[1];
                            Logging.Log("PreLoginSettings", "EVELoginUserName [" + Logging.EVELoginUserName + "]", Logging.Debug);
                            break;

                        case "eveloginpassword":
                            Logging.EVELoginPassword = sLine[1];
                            Logging.Log("PreLoginSettings", "EVELoginPassword [" + Logging.EVELoginPassword + "]", Logging.Debug);
                            break;

                        case "characternametologin":
                            try
                            {
                                Logging.MyCharacterName = sLine[1];
                                //Logging.MyCharacterName = Logging.MyCharacterName.Replace("_", " ");
                                Logging.Log("PreLoginSettings", "MyCharacterName [" + Logging.MyCharacterName + "]", Logging.Debug);
                            }
                            catch (Exception ex)
                            {
                                Logging.Log("PreLoginSettings.characternametologin", "Exception [" + ex + "]", Logging.Debug);
                            }

                            break;

                        case "questorloginonly":
                            _loginOnly = bool.Parse(sLine[1]);
                            Logging.Log("PreLoginSettings", "_loginOnly [" + _loginOnly + "]", Logging.Debug);
                            break;

                        case "questorusescheduler":
                            _chantlingScheduler = bool.Parse(sLine[1]);
                            Logging.Log("PreLoginSettings", "_chantlingScheduler [" + _chantlingScheduler + "]", Logging.Debug);
                            break;

                        case "standaloneinstance":
                            _standaloneInstance = bool.Parse(sLine[1]);
                            Logging.Log("PreLoginSettings", "_standaloneInstance [" + _standaloneInstance + "]", Logging.Debug);
                            break;

                        case "enablevisualstyles":
                            Logging.EnableVisualStyles = bool.Parse(sLine[1]);
                            Logging.Log("PreLoginSettings", "EnableVisualStyles [" + Logging.EnableVisualStyles + "]", Logging.Debug);
                            break;

                        case "debugbeforelogin":
                            Logging.DebugBeforeLogin = bool.Parse(sLine[1]);
                            Logging.Log("PreLoginSettings", "DebugBeforeLogin [" + Logging.DebugBeforeLogin + "]", Logging.Debug);
                            break;

                        case "debugdisableautologin":
                            Logging.DebugDisableAutoLogin = bool.Parse(sLine[1]);
                            Logging.Log("PreLoginSettings", "DebugDisableAutoLogin [" + Logging.DebugDisableAutoLogin + "]", Logging.Debug);
                            break;

                        case "debugonframe":
                            Logging.DebugOnframe = bool.Parse(sLine[1]);
                            Logging.Log("PreLoginSettings", "DebugOnframe: [" + Logging.DebugOnframe + "]", Logging.Debug);
                            break;
                    }
                }

                if (Logging.EVELoginUserName == null)
                {
                    Logging.Log("PreLoginSettings", "Missing: EVELoginUserName in [" + Path.GetFileName(iniFile).Substring(0, 4) + "_MyINIFileRedacted_" + "]: questor cant possibly AutoLogin without the EVE Login UserName", Logging.Debug);
                }

                if (Logging.EVELoginPassword == null)
                {
                    Logging.Log("PreLoginSettings", "Missing: EVELoginPassword in [" + Path.GetFileName(iniFile).Substring(0, 4) + "_MyINIFileRedacted_" + "]: questor cant possibly AutoLogin without the EVE Login Password!", Logging.Debug);
                }

                if (Logging.MyCharacterName == null)
                {
                    Logging.Log("PreLoginSettings", "Missing: CharacterNameToLogin in [" + Path.GetFileName(iniFile).Substring(0, 4) + "_MyINIFileRedacted_" + "]: questor cant possibly AutoLogin without the EVE CharacterName to choose", Logging.Debug);
                }

                Logging.Log("PreLoginSettings", "Done reading ini", Logging.Debug);
                return true;
            }
            catch (Exception exception)
            {
                Logging.Log("Startup.PreLoginSettings", "Exception [" + exception + "]", Logging.Debug);
                return false;
            }
        }
    }
}

﻿#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Linq;
using EveComFramework.Core;
using EveCom;
using IrcDotNet;
using EveComFramework.KanedaToolkit;
using LavishScriptAPI;

namespace EveComFramework.Comms
{
    #region Settings

    /// <summary>
    /// Settings for the Comms class
    /// </summary>
    public class CommsSettings : Settings
    {
        public bool UseIRC = false;
        public string Server = "irc1.lavishsoft.com";
        public int Port = 6667;
        public string SendTo1;
        public string SendTo2;
        public string SendTo3;
        public bool Local = true;
        public bool NPC = false;
        public bool AllChat = false;
        public bool Wallet = true;
        public bool ChatInvite = true;
        public bool Grid = false;
        public bool LocalTraffic = false;
    }

    #endregion

    public class Comms : State
    {
        #region Instantiation

        static Comms _Instance;
        /// <summary>
        /// Singletoner
        /// </summary>
        public static Comms Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new Comms();
                }
                return _Instance;
            }
        }

        private Comms()
        {
            DefaultFrequency = 200;
            QueueState(Init);
            QueueState(ConnectIRC);
            QueueState(Blank, 5000);
            QueueState(PostInit);
            QueueState(Control);
            NonFleetPlayers.AddNonFleetPlayers();
        }

        #endregion

        #region Variables

        /// <summary>
        /// Config for this module
        /// </summary>
        public CommsSettings Config = new CommsSettings();
        public Logger Console = new Logger("Comms");
        string LastLocal = "";
        Dictionary<string, string> chatMessages = new Dictionary<string, string>();
        double LastWallet;
        bool ChatInviteSeen = false;

        public Queue<string> ChatQueue = new Queue<string>();
        public Queue<string> LocalQueue = new Queue<string>();

        public IrcClient IRC = new IrcClient();

        Targets.Targets NonFleetPlayers = new Targets.Targets();
        List<Entity> NonFleetMemberOnGrid = new List<Entity>();
        List<Pilot> PilotCache = new List<Pilot>();
        int SolarSystem = -1;

        public ChatChannel LocalChat
        {
            get
            {
                return ChatChannel.All.FirstOrDefault(a => a.ID.Contains(Session.SolarSystemID.ToString()));
            }
        }

        public ChatChannel FleetChat
        {
            get
            {
                return ChatChannel.All.FirstOrDefault(a => a.ID.Contains("fleetid"));
            }
        }

        public ChatChannel ByName(String name)
        {
            if(name == "Local") return LocalChat;
            if(name == "Fleet") return FleetChat;
            return ChatChannel.All.FirstOrDefault(a => a.Name == name && a.CanSpeak);
        }

        #endregion

        #region Actions


        #endregion

        #region Events

        public event Action ToggleStop;
        public event Action ToggleLogoff;
        public event Action Start;
        public event Action Panic;
        public event Action ClearPanic;
        public event Action Skip;

        void PMReceived(object sender, IrcMessageEventArgs e)
        {
            if (e.Source.Name == Config.SendTo1 || e.Source.Name == Config.SendTo2 || e.Source.Name == Config.SendTo3)
            {
                if (e.Text.ToLower().StartsWith("?") || e.Text.ToLower().StartsWith("help"))
                {
                    ChatQueue.Enqueue("---------------Currently supported commands---------------");
                    ChatQueue.Enqueue("? or help - Display this menu!");
                    ChatQueue.Enqueue("Togglestop - Toggles on/off a stop at next opportunity");
                    ChatQueue.Enqueue("Togglelogoff - Toggles logoff on stop");
                    ChatQueue.Enqueue("Start - Starts the bot (ignored if bot is running)");
                    ChatQueue.Enqueue("Skip - Forces the bot to skip the current anomaly");
                    ChatQueue.Enqueue("Local <message> - Relays <message> to local (space must follow Local and don't put the <>!)");
                    ChatQueue.Enqueue("Listlocal or Locallist - Lists pilots currently in local chat");
                    ChatQueue.Enqueue("panic - Trigger panicked flee");
                    ChatQueue.Enqueue("clearpanic - Clear panic");
                    ChatQueue.Enqueue("exit - Exit game client immediately");
                    ChatQueue.Enqueue("iscommand <command> - Executes <command> InnerSpace command (space must follow Local and don't put the <>!)");
                    ChatQueue.Enqueue("setdestination <solarsystemid/stationid> - Set ingame destination");
                    ChatQueue.Enqueue("autopilot <enabled/disabled> - Control ECF autopilot");
                    ChatQueue.Enqueue("All commands are not case sensitive!");
                }
                if (e.Text.ToLower().StartsWith("togglestop") && ToggleStop != null)
                {
                    ToggleStop();
                }
                if (e.Text.ToLower().StartsWith("start") && Start != null)
                {
                    Start();
                }
                if (e.Text.ToLower().StartsWith("togglelogoff") && ToggleLogoff != null)
                {
                    ToggleLogoff();
                }
                if (e.Text.ToLower().StartsWith("panic") && Panic != null)
                {
                    Panic();
                }
                if (e.Text.ToLower().StartsWith("clearpanic") && ClearPanic != null)
                {
                    ClearPanic();
                }
                if (e.Text.ToLower().StartsWith("skip") && Skip != null)
                {
                    Skip();
                }
                if (e.Text.ToLower().StartsWith("local "))
                {
                    LocalQueue.Enqueue(e.Text.Remove(0, 6));
                }
                if (e.Text.ToLower().StartsWith("iscommand "))
                {
                    LavishScriptAPI.LavishScript.ExecuteCommand(e.Text.Remove(0, 10));
                }
                if (e.Text.ToLower().StartsWith("exit"))
                {
                    LavishScriptAPI.LavishScript.ExecuteCommand("exit");
                }
                if (e.Text.ToLower().StartsWith("setdestination"))
                {
                    string destionationString = e.Text.Remove(0, 15);
                    try
                    {
                        Route.SetDestination(long.Parse(destionationString));
                    }
                    catch
                    {
                        // ignored
                    }
                }

                if (e.Text.ToLower().StartsWith("autopilot enabled"))
                {
                    Move.Move.Instance.ToggleAutopilot(true);
                }

                if (e.Text.ToLower().StartsWith("autopilot disabled"))
                {
                    Move.Move.Instance.ToggleAutopilot(false);
                }

                if (e.Text.ToLower().StartsWith("listlocal") || e.Text.ToLower().StartsWith("locallist"))
                {
                    List<Pilot> tempLocalPilots = new List<Pilot>();
                    EVEFrameUtil.Do(() =>
                    {
                        try
                        {
                            if (Local.Pilots.Count < 20)
                            {
                                ChatQueue.Enqueue("Local has [" + Local.Pilots.Count + "] pilots currently");
                                tempLocalPilots = Local.Pilots;
                            }
                            else
                            {
                                ChatQueue.Enqueue("Local has [" + Local.Pilots.Count + "] pilots currently - too many to list details individually");
                            }
                        }
                        catch (Exception) { }
                    });

                    EVEFrameUtil.Do(() =>
                    {
                        if (tempLocalPilots != null && tempLocalPilots.Any() && tempLocalPilots.Count < 20)
                        {
                            foreach (Pilot tempLocalPilot in tempLocalPilots)
                            {
                                //ChatQueue.Enqueue("Name: [" + tempLocalPilot.Name + "]");
                                if (string.IsNullOrWhiteSpace(tempLocalPilot.CorpName))
                                {
                                    ChatQueue.Enqueue("Name: [" + tempLocalPilot.Name + "] CorpName not yet cached");
                                    try
                                    {
                                        Pilot tempPilot = Local.Pilots.FirstOrDefault(i => i.Name == tempLocalPilot.Name);
                                        if (tempPilot != null)
                                        {
                                            ChatQueue.Enqueue("Show info for [" + tempPilot.Name + "]'s corp");
                                            tempPilot.ShowCorpInfo();
                                        }
                                    }
                                    catch (Exception)
                                    {
                                    }
                                }
                            }
                        }
                    });

                    int intLocalPilot = 0;
                    EVEFrameUtil.Do(() =>
                    {
                        try
                        {
                            if (Local.Pilots.Count < 20)
                            {
                                ChatQueue.Enqueue("---Local List (corp members not listed) [" + Local.Pilots.Count + "] total [" + Local.Pilots.Count(i => i.CorpID != Me.CorpID) + "] w/o corp ---");
                                foreach (Pilot p in Local.Pilots.Where(i => i.CorpID != Me.CorpID))
                                {
                                    try
                                    {
                                        intLocalPilot++;
                                        ChatQueue.Enqueue("[" + intLocalPilot + "][" + p.Name + "] Hostile[ " + p.Hostile() + " ] Corp[ " + p.CorpName + " ][" + p.AllianceName + "]" + " - [ http://evewho.com/pilot/" + p.Name.Replace(" ", "%20") + " ]");
                                    }
                                    catch (Exception){}
                                }
                            }
                            else
                            {
                                ChatQueue.Enqueue("Local has [" + Local.Pilots.Count + "] pilots currently - too many to list details individually");
                            }
                        }
                        catch (Exception){}
                    });

                    EVEFrameUtil.Do(() =>
                    {
                        try
                        {
                            foreach (Window w in Window.All.Where(i => i.Name == "infowindow" && i.Ready && (i.Caption.Equals("Corporation: Information") || i.Caption.Equals("Alliance: Information"))))
                            {
                                w.Close();
                            }
                        }
                        catch (Exception) { }
                    });
                    ChatQueue.Enqueue("----------------End List----------------");
                }
                if (e.Text.ToLower().StartsWith("listentities"))
                {
                    ChatQueue.Enqueue("---------------Entity List---------------");
                    EVEFrameUtil.Do(() =>
                    {
                        foreach (Entity entity in Entity.All.Where(i => i.Distance < 200000 && i.GroupID != Group.Wreck && i.CategoryID != Category.Drone && i.CategoryID != Category.Charge && i.CategoryID != Category.Asteroid))
                        {
                            ChatQueue.Enqueue("Name [" + entity.Name + "] Distance [" + Math.Round(entity.Distance/1000,0) + "k] GroupID [" + (int)entity.GroupID + "][" + entity.GroupID + "] TypeID [" + entity.TypeID + "]");
                        }
                    });
                    ChatQueue.Enqueue("----------------End List----------------");
                }
                if (e.Text.ToLower().StartsWith("listwindows") || e.Text.ToLower().StartsWith("windowlist"))
                {
                    ChatQueue.Enqueue("---------------Window List---------------");
                    EVEFrameUtil.Do(() =>
                    {
                        try
                        {
                            foreach (Window w in EveCom.Window.All)
                            {
                                ChatQueue.Enqueue("Name: [" + w.Name + "] Type [" + w.Type + "] Caption [" + w.Caption + "] WindowCaption[" + w.WindowCaption + "]");
                            }
                        }
                        catch (Exception) { }
                    });
                    ChatQueue.Enqueue("----------------End List----------------");
                }
            }
        }

        void Error(object sender, IrcErrorEventArgs e)
        {
            EVEFrame.Log("Error: " + e.Error.Message);
        }


        #endregion

        #region States

        bool Init(object[] Params)
        {
            if (!Session.Safe || (!Session.InSpace && !Session.InStation)) return false;

            try
            {
                if (LocalChat.Messages.Any()) LastLocal = LocalChat.Messages.Last().Text;
            }
            catch
            {
                LastLocal = "";
            }
            LastWallet = Wallet.ISK;

            IRC.Error += Error;

            return true;
        }

        bool ConnectIRC(object[] Params)
        {
            if (!Session.Safe || (!Session.InSpace && !Session.InStation)) return false;

            if (Config.UseIRC && !IRC.IsConnected)
            {
                try
                {
                    IrcUserRegistrationInfo reginfo = new IrcUserRegistrationInfo();
                    reginfo.NickName = Me.Name.Replace(" ", string.Empty).Replace("'", string.Empty);
                    reginfo.RealName = Me.Name.Replace(" ", string.Empty).Replace("'", string.Empty);
                    reginfo.UserName = Me.Name.Replace(" ", string.Empty).Replace("'", string.Empty);
                    IRC.FloodPreventer = new IrcStandardFloodPreventer(4, 2000);
                    IRC.Connect(new Uri("irc://" + Config.Server), reginfo);
                }
                catch(Exception ex)
                {
                    Console.Log("Exception [" + ex + "]");
                    return false;
                }
            }

            return true;
        }
        bool Blank(object[] Params)
        {
            return true;
        }

        bool PostInit(object[] Params)
        {
            if (!Config.UseIRC) return true;

            if (!IRC.IsConnected)
            {
                DislodgeCurState(ConnectIRC);
                InsertState(Blank, 10000);
                QueueState(Control);
                return false;
            }
            IRC.LocalUser.MessageReceived += PMReceived;
            if (!string.IsNullOrWhiteSpace(Config.SendTo1))
            {
                IRC.LocalUser.SendMessage(Config.SendTo1, "Connected - type ? or help for instructions");
            }
            if (!string.IsNullOrWhiteSpace(Config.SendTo2))
            {
                IRC.LocalUser.SendMessage(Config.SendTo2, "Connected - type ? or help for instructions");
            }
            if (!string.IsNullOrWhiteSpace(Config.SendTo3))
            {
                IRC.LocalUser.SendMessage(Config.SendTo3, "Connected - type ? or help for instructions");
            }

            return true;
        }

        bool Control(object[] Params)
        {
            if (!IRC.IsConnected)
            {
                DislodgeCurState(ConnectIRC);
                InsertState(Blank, 10000);
                return false;
            }

            if (!Session.Safe || (!Session.InSpace && !Session.InStation)) return false;

            if (Session.SolarSystemID != SolarSystem)
            {
                PilotCache = Local.Pilots;
                SolarSystem = Session.SolarSystemID;
            }

            List<Pilot> newPilots = Local.Pilots.Where(a => !PilotCache.Contains(a)).ToList();
            if (newPilots.Any())
            {
                try
                {
                    foreach (Window w in Window.All.Where(i => i.Name.Contains("infowindow") && i.Ready && (i.Caption.Equals("Corporation: Information") || i.Caption.Equals("Alliance: Information"))))
                    {
                        w.Close();
                        return false;
                    }
                }
                catch (Exception) { }

                if (Config.LocalTraffic && Local.Pilots.Count < 100)
                {
                    try
                    {
                        foreach (Pilot pilot in newPilots)
                        {
                            if (pilot.CorpID != 0 && string.IsNullOrWhiteSpace(pilot.CorpName))
                            {
                                pilot.ShowCorpInfo();
                                return false;
                            }

                            if (pilot.AllianceID != 0 && string.IsNullOrWhiteSpace(pilot.AllianceName))
                            {
                                pilot.ShowAllianceInfo();
                                return false;
                            }

                            ChatQueue.Enqueue("<Local> New Pilot: [" + pilot.StandingsStatus() + "][ " + pilot.Name + " ][" + pilot.CorpName + "][" + pilot.AllianceName + "][" + pilot.StandingsStatus() + "] - [ http://evewho.com/pilot/" + pilot.Name.Replace(" ", "%20") + " ]");
                        }
                    }
                    catch (Exception){}
                }
            }

            PilotCache = Local.Pilots;

            try
            {
                if (Config.Local)
                {
                    if (Config.AllChat)
                    {
                        foreach (ChatChannel channel in ChatChannel.All)
                        {
                            try
                            {
                                if (channel.Messages.Count > 0)
                                {
                                    if (!chatMessages.ContainsKey(channel.ID))
                                    {
                                        chatMessages.Add(channel.ID, string.Empty);
                                    }
                                    if (chatMessages.FirstOrDefault(a => a.Key == channel.ID).Value != channel.Messages.Last().Text)
                                    {
                                        chatMessages.AddOrUpdate(channel.ID, channel.Messages.Last().Text);
                                        if (channel.Messages.Last().SenderID > 1 || Config.NPC)
                                        {
                                            ChatQueue.Enqueue("[Chat] <" + channel.Name + "> " + channel.Messages.Last().SenderName + ": " + channel.Messages.Last().Text);
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                EVEFrame.Log(e);
                            }
                        }
                    }
                    else
                    {
                        if (LocalChat.Messages.Any())
                        {
                            if (LocalChat.Messages.Last().Text != LastLocal)
                            {
                                LastLocal = LocalChat.Messages.Last().Text;
                                if (LocalChat.Messages.Last().SenderID > 1 || Config.NPC)
                                {
                                    ChatQueue.Enqueue("[Chat] <Local> " + LocalChat.Messages.Last().SenderName + ": " + LastLocal);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                EVEFrame.Log(e);
            }

            if (Config.ChatInvite)
            {
                Window ChatInvite = Window.All.FirstOrDefault(a => a.Name.Contains("ChatInvitation"));
                if (!ChatInviteSeen && ChatInvite != null)
                {
                    ChatQueue.Enqueue("<Comms> !!!!!!!!!!!!!!!!!!!!New Chat Invitation received!!!!!!!!!!!!!!!!!!!!");
                    ChatInviteSeen = true;
                }
                if (ChatInviteSeen && ChatInvite == null)
                {
                    ChatInviteSeen = false;
                }
            }

            if (Config.Wallet && LastWallet != Wallet.ISK)
            {
                double difference = Wallet.ISK - LastWallet;
                LastWallet = Wallet.ISK;
                ChatQueue.Enqueue("<Wallet> " + toISK(LastWallet) + " Delta: " + toISK(difference));
            }

            if (Session.InSpace && Config.Grid)
            {
                Entity AddNonFleet = NonFleetPlayers.TargetList.FirstOrDefault(a => !NonFleetMemberOnGrid.Contains(a));
                if (AddNonFleet != null)
                {
                    ChatQueue.Enqueue("<Security> Non fleet member on grid: " + AddNonFleet.Name + " - http://evewho.com/pilot/" + AddNonFleet.Name.Replace(" ", "%20") + " (" + AddNonFleet.Type + ")");
                    NonFleetMemberOnGrid.Add(AddNonFleet);
                }

                NonFleetMemberOnGrid = NonFleetPlayers.TargetList.Where(a => NonFleetMemberOnGrid.Contains(a)).ToList();
            }

            if (Config.UseIRC)
            {
                if (ChatQueue != null && ChatQueue.Any())
                {
                    try
                    {
                        List<string> listOfIrcUsersToSendTo = new List<string>();
                        if (!string.IsNullOrWhiteSpace(Config.SendTo1))
                        {
                            listOfIrcUsersToSendTo.Add(Config.SendTo1);
                        }

                        if (!string.IsNullOrWhiteSpace(Config.SendTo2))
                        {
                            listOfIrcUsersToSendTo.Add(Config.SendTo2);
                        }

                        if (!string.IsNullOrWhiteSpace(Config.SendTo3))
                        {
                            listOfIrcUsersToSendTo.Add(Config.SendTo3);
                        }

                        IEnumerable<string> ienumerableOfIrcUsersToSendTo = listOfIrcUsersToSendTo;
                        IRC.LocalUser.SendMessage(ienumerableOfIrcUsersToSendTo, ChatQueue.Dequeue());
                    }
                    catch (Exception ex)
                    {
                        Console.Log("Exception [" + ex + "]");
                    }
                }
            }

            if (LocalQueue.Count > 0)
            {
                LocalChat.Send(LocalQueue.Dequeue());
            }
            return false;
        }

        #endregion


        string toISK(double val)
        {
            if (val > 1000000000) return string.Format("{0:0.00}b isk", val / 1000000000);
            if (val > 1000000) return string.Format("{0:0.00}m isk", val / 1000000);
            if (val > 1000) return string.Format("{0:0.00}k isk", val / 1000);
            return string.Format("{0:0.00} isk", val);
        }

        public static bool MatchMessageAnom(String message, String anom)
        {
            if (message.StartsWith("^") || message.StartsWith("*"))
            {
                message = message.Substring(1);
            }
            if (message.EndsWith("^") || message.EndsWith("*"))
            {
                message = message.Substring(0, message.Length-1);
            }
            String anomShort = anom.Substring(0, 3);
            String anomDigit = anom.Substring(4, 3);
            if (message.ToUpper() == anom) return true;
            if (message.ToUpper() == anomShort) return true;
            if (message.ToUpper() == anomDigit) return true;
            if (message.ToUpper().StartsWith(anomShort + "<BR>")) return true;
            if (message.ToUpper().Contains("<BR>" + anomShort + "<BR>")) return true;
            if (message.ToUpper().EndsWith("<BR>" + anomShort)) return true;
            if (message.ToUpper().Split(' ').All(a => a.Length == 3) && message.ToUpper().Split(' ').Contains(anomShort)) return true;
            if (message.EndsWith(" " + anomDigit)) return true;
            return false;
        }
    }

}

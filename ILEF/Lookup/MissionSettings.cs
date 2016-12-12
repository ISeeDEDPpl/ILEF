// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------

namespace ILEF.Lookup
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using System.Xml.XPath;
    using global::ILoveEVE.Framework;
    using global::ILEF.Actions;
    using global::ILEF.Caching;
    using global::ILEF.Combat;
    using global::ILEF.Logging;
    using global::ILEF.States;

    public static class MissionSettings
    {
        static MissionSettings()
        {
            ChangeMissionShipFittings = false;
            DefaultFittingName = null;
            FactionBlacklist = new List<string>();
            ListOfAgents = new List<AgentsList>();
            ListofFactionFittings = new List<FactionFitting>();
            ListOfMissionFittings = new List<MissionFitting>();
            _listOfMissionFittings = new List<MissionFitting>();
            AmmoTypesToLoad = new Dictionary<Ammo, DateTime>();
            MissionBlacklist = new List<string>();
            MissionGreylist = new List<string>();
            MissionItems = new List<string>();
            MissionUseDrones = null;
            UseMissionShip = false;
            DamageTypesForThisMission = new Dictionary<DamageType, DateTime>();
            DamageTypesInMissionXML = new List<DamageType>();
        }

        private static List<FactionFitting> _listofFactionFittings;
        //
        // Fitting Settings - if enabled
        //
        public static List<FactionFitting> ListofFactionFittings
        {
            get
            {
                try
                {
                    if (QMSettings.Instance.UseFittingManager) //no need to look for or load these settings if FittingManager is disabled
                    {
                        if (_listofFactionFittings != null && _listofFactionFittings.Any())
                        {
                            return _listofFactionFittings;
                        }

                        //
                        // if _listofFactionFittings is empty make sure it is NOT null!
                        //
                        _listofFactionFittings = new List<FactionFitting>();

                        XElement factionFittings = QMSettings.Instance.CharacterSettingsXml.Element("factionFittings") ??
                                                   QMSettings.Instance.CharacterSettingsXml.Element("factionfittings") ??
                                                   QMSettings.Instance.CommonSettingsXml.Element("factionFittings") ??
                                                   QMSettings.Instance.CommonSettingsXml.Element("factionfittings");

                        if (factionFittings != null)
                        {
                            string factionFittingXmlElementName = "";
                            if (factionFittings.Elements("factionFitting").Any())
                            {
                                factionFittingXmlElementName = "factionFitting";
                            }
                            else
                            {
                                factionFittingXmlElementName = "factionfitting";
                            }

                            int i = 0;
                            foreach (XElement factionfitting in factionFittings.Elements(factionFittingXmlElementName))
                            {
                                i++;
                                MissionSettings._listofFactionFittings.Add(new FactionFitting(factionfitting));
                                if (Logging.DebugFittingMgr) Logging.Log("Settings.LoadMFactionFitting", "[" + i + "] Faction Fitting [" + factionfitting + "]", Logging.Teal);
                            }

                            return _listofFactionFittings;
                        }

                        QMSettings.Instance.UseFittingManager = false;
                        if (Logging.DebugFittingMgr) Logging.Log("Settings", "No faction fittings specified.  Fitting manager will not be used.", Logging.Orange);
                        return new List<FactionFitting>();
                    }

                    return new List<FactionFitting>();
                }
                catch (Exception exception)
                {
                    Logging.Log("Settings", "Error Loading Faction Fittings Settings [" + exception + "]", Logging.Teal);
                    return new List<FactionFitting>();
                }
            }

            private set
            {
                _listofFactionFittings = value;
            }
        }

        public static List<AgentsList> ListOfAgents { get; set; }

        private static List<MissionFitting> _listOfMissionFittings;
        public static List<MissionFitting> ListOfMissionFittings
        {
            get
            {
                //
                // Load List of Mission Fittings available for Fitting based on the name of the mission
                //
                try
                {
                    XElement xmlElementMissionFittingsSection = QMSettings.Instance.CharacterSettingsXml.Element("missionfittings") ?? QMSettings.Instance.CommonSettingsXml.Element("missionfittings");
                    if (QMSettings.Instance.UseFittingManager) //no need to look for or load these settings if FittingManager is disabled
                    {
                        if (xmlElementMissionFittingsSection != null)
                        {
                            if (Logging.DebugFittingMgr) Logging.Log("Settings", "Loading Mission Fittings", Logging.White);
                            int i = 0;
                            foreach (XElement missionfitting in xmlElementMissionFittingsSection.Elements("missionfitting"))
                            {
                                i++;
                                MissionSettings._listOfMissionFittings.Add(new MissionFitting(missionfitting));
                                if (Logging.DebugFittingMgr) Logging.Log("Settings.LoadMissionFittings", "[" + i + "] Mission Fitting [" + missionfitting + "]", Logging.Teal);
                            }

                            if (Logging.DebugFittingMgr) Logging.Log("Settings", "        Mission Fittings now has [" + MissionSettings._listOfMissionFittings.Count + "] entries", Logging.White);
                            return _listOfMissionFittings;
                        }

                        return new List<MissionFitting>();
                    }

                    return new List<MissionFitting>();
                }
                catch (Exception exception)
                {
                    Logging.Log("Settings", "Error Loading Mission Fittings Settings [" + exception + "]", Logging.Teal);
                    return new List<MissionFitting>();
                }
            }

            private set
            {
                _listOfMissionFittings = value;
            }
        }

        private static string _defaultFittingName;

        private static FactionFitting _defaultFitting;
        public static string DefaultFittingName
        {
            get
            {
                if (ListofFactionFittings != null && ListofFactionFittings.Any())
                {
                    _defaultFitting = ListofFactionFittings.Find(m => m.FactionName.ToLower() == "default");
                    _defaultFittingName = _defaultFitting.FittingName;
                    return _defaultFittingName;
                }

                Logging.Log("MissionSettings", "DefaultFittingName - no fitting found for the faction named [ default ], assuming a fitting name of [ default ] exists", Logging.Debug);
                return "default";
            }
            set
            {
                _defaultFittingName = value;
            }
        }

        public static DirectAgentMission Mission { get; set; }
        public static DirectAgentMission FirstAgentMission { get; set; }
        public static IEnumerable<DirectAgentMission> myAgentMissionList { get; set; }
        public static bool MissionXMLIsAvailable { get; set; }
        public static string MissionXmlPath { get; set; }
        public static string MissionName { get; set; }
        public static float MinAgentBlackListStandings { get; set; }
        public static float MinAgentGreyListStandings { get; set; }
        public static string MissionsPath { get; set; }
        public static bool RequireMissionXML { get; set; }
        public static bool AllowNonStorylineCourierMissionsInLowSec { get; set; }
        public static bool WaitDecline { get; set; }
        public static int NumberOfTriesToDeleteBookmarks = 3;
        public static int MaterialsForWarOreID { get; set; }
        public static int MaterialsForWarOreQty { get; set; }
        public static int StopSessionAfterMissionNumber = int.MaxValue;
        public static int GreyListedMissionsDeclined = 0;
        public static string LastGreylistMissionDeclined = string.Empty;
        public static int BlackListedMissionsDeclined = 0;
        public static string LastBlacklistMissionDeclined = string.Empty;

        //
        // Pocket Specific Settings (we should make these ALL settable via the mission XML inside of pockets
        //

        public static int? PocketDroneTypeID { get; set; }
        public static bool? PocketKillSentries { get; set; }
        public static bool? PocketUseDrones { get; set; }
        public static double? PocketOrbitDistance = null;
        public static double? PocketOptimalRange = null;
        public static int? PocketActivateRepairModulesAtThisPerc { get; set; }

        //
        // Mission Specific Settings (we should make these ALL settable via the mission XML outside of pockets (just inside the mission tag)
        //
        public static int? MissionDroneTypeID { get; set; }
        public static bool? MissionDronesKillHighValueTargets = null;
        public static bool? MissionKillSentries { get; set; }
        public static bool? MissionUseDrones { get; set; }
        public static double? MissionOrbitDistance = null;
        public static double? MissionOptimalRange = null;
        public static int? MissionActivateRepairModulesAtThisPerc { get; set; }
        public static int MissionWeaponGroupId { get; set; }
        public static string BringMissionItem { get; set; }
        public static int BringMissionItemQuantity { get; set; }
        public static string BringOptionalMissionItem { get; set; }
        public static int BringOptionalMissionItemQuantity { get; set; }
        public static double MissionWarpAtDistanceRange { get; set; } //in km

        //
        // Faction Specific Settings (we should make these ALL settable via some mechanic that I have not come up with yet
        //
        public static int? FactionDroneTypeID { get; set; }
        public static bool? FactionDronesKillHighValueTargets = null;
        public static double? FactionOrbitDistance = null;
        public static double? FactionOptimalRange = null;
        public static int? FactionActivateRepairModulesAtThisPerc { get; set; }


        //
        // Mission Blacklist / Greylist Settings
        //
        public static List<string> MissionBlacklist { get; private set; }
        public static List<string> MissionGreylist { get; private set; }
        public static List<string> FactionBlacklist { get; private set; }

        public static void LoadMissionBlackList(XElement CharacterSettingsXml, XElement CommonSettingsXml)
        {
            try
            {
                //if (QMSettings.Instance.CharacterMode.ToLower() == "Combat Missions".ToLower())
                //{
                //
                // Mission Blacklist
                //
                MissionBlacklist.Clear();
                XElement xmlElementBlackListSection = CharacterSettingsXml.Element("blacklist") ?? CommonSettingsXml.Element("blacklist");
                if (xmlElementBlackListSection != null)
                {
                    Logging.Log("Settings", "Loading Mission Blacklist", Logging.White);
                    int i = 1;
                    foreach (XElement BlacklistedMission in xmlElementBlackListSection.Elements("mission"))
                    {
                        MissionBlacklist.Add(Logging.FilterPath((string)BlacklistedMission));
                        if (Logging.DebugBlackList) Logging.Log("Settings.LoadBlackList", "[" + i + "] Blacklisted mission Name [" + Logging.FilterPath((string)BlacklistedMission) + "]", Logging.Teal);
                        i++;
                    }
                    Logging.Log("Settings", "        Mission Blacklist now has [" + MissionBlacklist.Count + "] entries", Logging.White);
                }
                //}

            }
            catch (Exception ex)
            {
                Logging.Log("Settings.LoadMissionBlackList", "Exception: [" + ex + "]", Logging.Debug);
            }
        }

        public static void LoadMissionGreyList(XElement CharacterSettingsXml, XElement CommonSettingsXml)
        {
            try
            {
                //if (QMSettings.Instance.CharacterMode.ToLower() == "Combat Missions".ToLower())
                //{
                //
                // Mission Greylist
                //
                MissionGreylist.Clear();
                XElement xmlElementGreyListSection = CharacterSettingsXml.Element("greylist") ?? CommonSettingsXml.Element("greylist");

                if (xmlElementGreyListSection != null)
                {
                    Logging.Log("Settings", "Loading Mission GreyList", Logging.White);
                    int i = 1;
                    foreach (XElement GreylistedMission in xmlElementGreyListSection.Elements("mission"))
                    {
                        MissionGreylist.Add(Logging.FilterPath((string)GreylistedMission));
                        if (Logging.DebugGreyList) Logging.Log("Settings.LoadGreyList", "[" + i + "] GreyListed mission Name [" + Logging.FilterPath((string)GreylistedMission) + "]", Logging.Teal);
                        i++;
                    }
                    Logging.Log("Settings", "        Mission GreyList now has [" + MissionGreylist.Count + "] entries", Logging.White);
                }
                //}
            }
            catch (Exception ex)
            {
                Logging.Log("Settings.LoadMissionGreyList", "Exception: [" + ex + "]", Logging.Debug);
            }
        }

        public static void LoadFactionBlacklist(XElement CharacterSettingsXml, XElement CommonSettingsXml)
        {
            try
            {
                //
                // Faction Blacklist
                //
                FactionBlacklist.Clear();
                XElement factionblacklist = CharacterSettingsXml.Element("factionblacklist") ?? CommonSettingsXml.Element("factionblacklist");
                if (factionblacklist != null)
                {
                    Logging.Log("Settings", "Loading Faction Blacklist", Logging.White);
                    foreach (XElement faction in factionblacklist.Elements("faction"))
                    {
                        Logging.Log("Settings", "        Missions from the faction [" + (string)faction + "] will be declined", Logging.White);
                        FactionBlacklist.Add((string)faction);
                    }

                    Logging.Log("Settings", "        Faction Blacklist now has [" + FactionBlacklist.Count + "] entries", Logging.White);
                }
            }
            catch (Exception ex)
            {
                Logging.Log("Settings.LoadMissionGreyList", "Exception: [" + ex + "]", Logging.Debug);
            }
        }

        //public XDocument InvTypes;
        public static XDocument UnloadLootTheseItemsAreLootItems;
        //public static XDocument InvIgnore;

        /// <summary>
        ///   Returns the mission objectives from
        /// </summary>
        public static List<string> MissionItems { get; private set; }

        private static string _fittingToLoad; //name of the final fitting we want to use

        public static string FittingToLoad
        {
            get
            {
                if (MissionFittingNameForThisMissionName == null)
                {
                    if (FactionFittingNameForThisMissionsFaction == null)
                    {
                        //
                        // if both mission and faction fittings are null we need to try to locate and use the default fitting
                        //
                        _fittingToLoad = MissionSettings.DefaultFittingName.ToLower();
                    }

                    _fittingToLoad = FactionFittingNameForThisMissionsFaction;
                    return _fittingToLoad;
                }

                _fittingToLoad = FactionFittingNameForThisMissionsFaction;
                return _fittingToLoad;
            }

            set
            {
                _fittingToLoad = value;
            }
        }


        public static string MissionSpecificShip { get; set; } //stores name of mission specific ship
        public static string FactionSpecificShip { get; set; } //stores name of mission specific ship
        public static string CurrentFit { get; set; }

        private static string _factionFittingNameForThisMissionsFaction;

        private static FactionFitting FactionFittingForThisMissionsFaction { get; set; }
        public static string FactionFittingNameForThisMissionsFaction
        {
            get
            {
                if (_factionFittingNameForThisMissionsFaction == null)
                {
                    if (MissionSettings.ListofFactionFittings.Any(i => i.FactionName.ToLower() == FactionName.ToLower()))
                    {
                        if (MissionSettings.ListofFactionFittings.FirstOrDefault(m => m.FactionName.ToLower() == FactionName.ToLower()) != null)
                        {
                            FactionFittingForThisMissionsFaction = MissionSettings.ListofFactionFittings.FirstOrDefault(m => m.FactionName.ToLower() == FactionName.ToLower());
                            if (FactionFittingForThisMissionsFaction != null)
                            {
                                _factionFittingNameForThisMissionsFaction = FactionFittingForThisMissionsFaction.FittingName;
                                if (FactionFittingForThisMissionsFaction.DroneTypeID != null && FactionFittingForThisMissionsFaction.DroneTypeID != 0)
                                {
                                    Drones.FactionDroneTypeID = (int)FactionFittingForThisMissionsFaction.DroneTypeID;
                                }

                                Logging.Log("AgentInteraction", "Faction fitting: " + FactionFittingForThisMissionsFaction.FactionName + "Using DroneTypeID [" + Drones.DroneTypeID + "]", Logging.Yellow);
                                return _factionFittingNameForThisMissionsFaction;
                            }

                            return null;
                        }

                        return null;
                    }

                    //
                    // Assume the faction named Default has a fit assigned (we couldnt find the actual faction assigned to a fit (we tried above))
                    //
                    if (MissionSettings.ListofFactionFittings.Any(i => i.FactionName.ToLower() == "Default".ToLower()))
                    {
                        if (MissionSettings.ListofFactionFittings.FirstOrDefault(m => m.FactionName.ToLower() == "Default".ToLower()) != null)
                        {
                            FactionFittingForThisMissionsFaction = MissionSettings.ListofFactionFittings.FirstOrDefault(m => m.FactionName.ToLower() == "Default".ToLower());
                            if (FactionFittingForThisMissionsFaction != null)
                            {
                                _factionFittingNameForThisMissionsFaction = FactionFittingForThisMissionsFaction.FittingName;
                                if (FactionFittingForThisMissionsFaction.DroneTypeID != null && FactionFittingForThisMissionsFaction.DroneTypeID != 0)
                                {
                                    Drones.FactionDroneTypeID = (int)FactionFittingForThisMissionsFaction.DroneTypeID;
                                }

                                Logging.Log("AgentInteraction", "Faction fitting: " + FactionFittingForThisMissionsFaction.FactionName + "Using DroneTypeID [" + Drones.DroneTypeID + "]", Logging.Yellow);
                                return _factionFittingNameForThisMissionsFaction;
                            }

                            return null;
                        }
                    }

                    return null;
                }

                return _factionFittingNameForThisMissionsFaction;
            }

            set { _factionFittingNameForThisMissionsFaction = value; }
        }

        private static string _missionFittingNameForThisMissionName;

        private static MissionFitting _missionFittingForThisMissionName;
        public static string MissionFittingNameForThisMissionName
        {
            get
            {
                if (_missionFittingForThisMissionName == null)
                {
                    if (MissionSettings.ListOfMissionFittings != null)
                    {
                        if (MissionSettings.ListOfMissionFittings.Any())
                        {
                            if (MissionSettings.ListOfMissionFittings.Any(i => i.MissionName != null && MissionSettings.Mission != null && i.MissionName.ToLower() == Mission.Name))
                            {
                                IEnumerable<MissionFitting> tempListOfMissionFittings = MissionSettings.ListOfMissionFittings.Where(i => i.MissionName.ToLower() == Mission.Name);
                                if (tempListOfMissionFittings != null && tempListOfMissionFittings.Any())
                                {
                                    foreach (MissionFitting fittingMatchingFaction in tempListOfMissionFittings)
                                    {
                                        if (fittingMatchingFaction.FactionName != null)
                                        {
                                            if (fittingMatchingFaction.FactionName == MissionSettings.FactionName)
                                            {
                                                _missionFittingForThisMissionName = fittingMatchingFaction;
                                                _missionFittingNameForThisMissionName = fittingMatchingFaction.FittingName;
                                                if (fittingMatchingFaction.DroneTypeID != null)
                                                {
                                                    MissionSettings.MissionDroneTypeID = fittingMatchingFaction.DroneTypeID;
                                                }

                                                //_fitting.Ship - this should allow for mission specific ships... if we want to allow for that
                                                return _missionFittingNameForThisMissionName;
                                            }

                                            continue;
                                        }

                                        continue;
                                    }

                                    MissionFitting fitting = tempListOfMissionFittings.FirstOrDefault();
                                    if (fitting != null)
                                    {
                                        _missionFittingForThisMissionName = fitting;
                                        _missionFittingNameForThisMissionName = fitting.FittingName;
                                        if (fitting.DroneTypeID != null)
                                        {
                                            MissionSettings.MissionDroneTypeID = fitting.DroneTypeID;
                                        }

                                        //_fitting.Ship - this should allow for mission specific ships... if we want to allow for that
                                        return _missionFittingNameForThisMissionName;
                                    }

                                    return null;
                                }

                                Logging.Log("MissionSettings", "MissionFittingNameForThisMissionName: if (tempListOfMissionFittings != null && tempListOfMissionFittings.Any())", Logging.Debug);
                                return null;
                            }

                            Logging.Log("MissionSettings", "MissionFittingNameForThisMissionName: if (!MissionSettings.ListOfMissionFittings.Any(i => i.MissionName != null && Mission != null && i.MissionName.ToLower() == Mission.Name))", Logging.Debug);
                            return null;
                        }

                        Logging.Log("MissionSettings", "MissionFittingNameForThisMissionName: if (!MissionSettings.ListOfMissionFittings.Any())", Logging.Debug);
                        return null;
                    }

                    Logging.Log("MissionSettings", "MissionFittingNameForThisMissionName: if (MissionSettings.ListOfMissionFittings == null )", Logging.Debug);
                    return null;
                }

                return _factionFittingNameForThisMissionsFaction;
            }

            set { _factionFittingNameForThisMissionsFaction = value; }
        }

        public static string FactionName { get; set; }
        public static bool UseMissionShip { get; set; } // flags whether we're using a mission specific ship
        public static bool ChangeMissionShipFittings { get; set; } // used for situations in which missionShip's specified, but no faction or mission fittings are; prevents default
        public static  Dictionary<Ammo, DateTime> AmmoTypesToLoad { get; set; }
        //public static List<Ammo> FactionAmmoTypesToLoad { get; set; }

        public static int MissionsThisSession = 0;

        public static bool ThisMissionIsNotWorthSalvaging()
        {
            if (MissionSettings.MissionName != null)
            {
                if (MissionSettings.MissionName.ToLower().Contains("Attack of the Drones".ToLower()))
                {
                    Logging.Log("MissionSettings", "Do not salvage a drones mission as they are crap now", Logging.Purple);
                    return true;
                }

                if (MissionSettings.MissionName.ToLower().Contains("Infiltrated Outposts".ToLower()))
                {
                    Logging.Log("MissionSettings", "Do not salvage a drones mission as they are crap now", Logging.Purple);
                    return true;
                }

                if (MissionSettings.MissionName.ToLower().Contains("Rogue Drone Harassment".ToLower()))
                {
                    Logging.Log("MissionSettings", "Do not salvage a drones mission as they are crap now", Logging.Purple);
                    return true;
                }

                return false;
            }

            return false;
        }

        /// <summary>
        ///   Returns the first mission bookmark that starts with a certain string
        /// </summary>
        /// <returns></returns>
        public static DirectAgentMissionBookmark GetMissionBookmark(long agentId, string startsWith)
        {
            try
            {
                // Get the missions
                DirectAgentMission missionForBookmarkInfo = QMCache.Instance.GetAgentMission(agentId, true);
                if (missionForBookmarkInfo == null)
                {
                    Logging.Log("Cache.DirectAgentMissionBookmark", "missionForBookmarkInfo [null] <---bad  parameters passed to us:  agentid [" + agentId + "] startswith [" + startsWith + "]", Logging.White);
                    return null;
                }

                // Did we accept this mission?
                if (missionForBookmarkInfo.State != (int)MissionState.Accepted)
                {
                    Logging.Log("GetMissionBookmark", "missionForBookmarkInfo.State: [" + missionForBookmarkInfo.State.ToString() + "]", Logging.Debug);
                }

                if (missionForBookmarkInfo.AgentId != agentId)
                {
                    Logging.Log("GetMissionBookmark", "missionForBookmarkInfo.AgentId: [" + missionForBookmarkInfo.AgentId.ToString() + "]", Logging.Debug);
                    Logging.Log("GetMissionBookmark", "agentId: [" + agentId + "]", Logging.Debug);
                    return null;
                }

                if (missionForBookmarkInfo.Bookmarks.Any(b => b.Title.ToLower().StartsWith(startsWith.ToLower())))
                {
                    Logging.Log("GetMissionBookmark", "MissionBookmark Found", Logging.White);
                    return missionForBookmarkInfo.Bookmarks.FirstOrDefault(b => b.Title.ToLower().StartsWith(startsWith.ToLower()));
                }

                if (QMCache.Instance.AllBookmarks.Any(b => b.Title.ToLower().StartsWith(startsWith.ToLower())))
                {
                    Logging.Log("GetMissionBookmark", "MissionBookmark From your Agent Not Found, but we did find a bookmark for a mission", Logging.Debug);
                    return (DirectAgentMissionBookmark)QMCache.Instance.AllBookmarks.FirstOrDefault(b => b.Title.ToLower().StartsWith(startsWith.ToLower()));
                }

                Logging.Log("GetMissionBookmark", "MissionBookmark From your Agent Not Found: and as a fall back we could not find any bookmark starting with [" + startsWith + "] either... ", Logging.Debug);
                return null;
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.DirectAgentMissionBookmark", "Exception [" + exception + "]", Logging.Debug);
                return null;
            }
        }

        public static void ClearPocketSpecificSettings()
        {
            MissionSettings.PocketActivateRepairModulesAtThisPerc = null;
            MissionSettings.PocketKillSentries = null;
            MissionSettings.PocketOptimalRange = null;
            MissionSettings.PocketOrbitDistance = null;
            MissionSettings.PocketUseDrones = null;
            MissionSettings.PocketDamageType = null;
            //MissionSettings.ManualDamageType = null;
        }

        public static void ClearMissionSpecificSettings()
        {
            //
            // Clear Mission Specific Settings
            //
            MissionSettings.MissionDronesKillHighValueTargets = null;
            MissionSettings.MissionWeaponGroupId = 0;
            MissionSettings.MissionWarpAtDistanceRange = 0;
            MissionSettings.MissionXMLIsAvailable = true;
            MissionSettings.MissionDroneTypeID = null;
            MissionSettings.MissionKillSentries = null;
            MissionSettings.MissionUseDrones = null;
            MissionSettings.MissionOrbitDistance = null;
            MissionSettings.MissionOptimalRange = null;
            MissionSettings.MissionDamageType = null;
            MissionSettings._factionFittingNameForThisMissionsFaction = null;
            MissionSettings.FactionFittingForThisMissionsFaction = null;
            MissionSettings._fittingToLoad = null;
            MissionSettings._listOfMissionFittings.Clear();
        }

        public static void ClearFactionSpecificSettings()
        {
            MissionSettings.FactionActivateRepairModulesAtThisPerc = null;
            MissionSettings.FactionDroneTypeID = null;
            MissionSettings.FactionDronesKillHighValueTargets = null;
            MissionSettings.FactionOptimalRange = null;
            MissionSettings.FactionOrbitDistance = null;
            MissionSettings.FactionDamageType = null;
            //MissionSettings.ManualDamageType = null;
            MissionSettings._listofFactionFittings.Clear();
        }

        public static IDictionary<TKey, TValue> AddOrUpdate_<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary.ContainsKey(key))
            {
                dictionary[key] = value;
            }
            else
            {
                dictionary.Add(key, value);
            }

            return dictionary;
        }

        public static void LoadMissionXmlData()
        {
            Logging.Log("AgentInteraction", "Loading mission xml [" + MissionName + "] from [" + MissionSettings.MissionXmlPath + "]", Logging.Yellow);

            ClearMissionSpecificSettings();

            //FactionDamageType,
            //MissionDamageType,
            //PocketDamageType,
            //ManualDamageType

            //
            // this loads the settings global to the mission, NOT individual pockets
            //
            XDocument missionXml = null;
            try
            {
                missionXml = XDocument.Load(MissionXmlPath);

                //load mission specific ammo and WeaponGroupID if specified in the mission xml
                if (missionXml.Root != null)
                {
                    XElement ammoTypes = missionXml.Root.Element("ammoTypes");
                    if (ammoTypes != null && ammoTypes.Elements("ammoType").Any())
                    {
                        Logging.Log("LoadMissionXMLData", "Clearing existing list of Ammo To load: using ammoTypes from [" + MissionSettings.MissionXmlPath + "]", Logging.White);
                        AmmoTypesToLoad = new Dictionary<Ammo, DateTime>();
                        foreach (XElement ammo in ammoTypes.Elements("ammoType"))
                        {
                            Logging.Log("LoadSpecificAmmo", "Adding [" + new Ammo(ammo).Name + "] to the list of ammo to load: from: ammoTypes", Logging.White);
                            AmmoTypesToLoad.AddOrUpdate_(new Ammo(ammo), DateTime.UtcNow);
                            MissionSettings.MissionDamageType = (DamageType)Enum.Parse(typeof(DamageType), (string)ammo, true);
                            MissionSettings.loadedAmmo = true;
                        }

                        //Cache.Instance.DamageType
                    }

                    ammoTypes = missionXml.Root.Element("missionammo");
                    if (ammoTypes != null && ammoTypes.Elements("ammoType").Any())
                    {
                        Logging.Log("LoadMissionXMLData", "Clearing existing list of Ammo To load: using missionammo from [" + MissionSettings.MissionXmlPath + "]", Logging.White);
                        AmmoTypesToLoad = new Dictionary<Ammo, DateTime>();
                        foreach (XElement ammo in ammoTypes.Elements("ammo"))
                        {
                            Logging.Log("LoadSpecificAmmo", "Adding [" + new Ammo(ammo).Name + "] to the list of ammo to load: from: missionammo", Logging.White);
                            AmmoTypesToLoad.AddOrUpdate_(new Ammo(ammo), DateTime.UtcNow);
                            MissionSettings.MissionDamageType = (DamageType) Enum.Parse(typeof (DamageType), (string)ammo, true);
                            MissionSettings.loadedAmmo = true;
                        }

                        //Cache.Instance.DamageType
                    }

                    MissionWeaponGroupId = (int?)missionXml.Root.Element("weaponGroupId") ?? 0;
                    MissionUseDrones = (bool?) missionXml.Root.Element("useDrones");
                    MissionKillSentries = (bool?)missionXml.Root.Element("killSentries");
                    MissionWarpAtDistanceRange = (int?)missionXml.Root.Element("missionWarpAtDistanceRange") ?? 0; //distance in km
                    MissionSettings.MissionDroneTypeID = (int?)missionXml.Root.Element("DroneTypeId") ?? null;

                    DamageTypesInMissionXML = new List<DamageType>();
                    DamageTypesForThisMission = new Dictionary<DamageType, DateTime>();

                    //missionXml.XPathSelectElements("//damagetype").Select(e => (DamageType)Enum.Parse(typeof(DamageType), (string)e, true)).ToList();

                    //DamageTypesForThisMission = new List<DamageType>();
                    DamageTypesInMissionXML = missionXml.XPathSelectElements("//damagetype").Select(e => (DamageType)Enum.Parse(typeof(DamageType), (string)e, true)).ToList();
                    foreach (DamageType damageTypeElement in DamageTypesInMissionXML)
                    {
                        DamageTypesForThisMission.AddOrUpdate_(damageTypeElement, DateTime.UtcNow);
                        DamageType damageTypeElementCopy = damageTypeElement;
                        foreach (Ammo _ammoType in Combat.Ammo.Where(i => i.DamageType == damageTypeElementCopy))
                        {
                            Logging.Log("AgentInteraction", "Mission XML for [" + MissionName + "] specified to load [" + damageTypeElementCopy + "] Damagetype. Adding [" + _ammoType.Name + "][" + _ammoType.TypeId +"] to the list of ammoToLoad", Logging.White);
                            AmmoTypesToLoad.AddOrUpdate_((_ammoType), DateTime.UtcNow);
                            MissionSettings.MissionDamageType = _ammoType.DamageType;
                            loadedAmmo = true;
                        }
                    }

                    if (DamageTypesForThisMission.Any() && !AmmoTypesToLoad.Any())
                    {
                        int _MissionDamageTypeCount_ = DamageTypesInMissionXML.Count();

                        Logging.Log("AgentInteraction", "Mission XML specified there are [" + _MissionDamageTypeCount_ + "] Damagetype(s) for [" + MissionName + "] listed below: ", Logging.White);

                        _MissionDamageTypeCount_ = 0;
                        foreach (KeyValuePair<DamageType, DateTime> _missionDamageType in DamageTypesForThisMission)
                        {
                            _MissionDamageTypeCount_++;
                            //MissionDamageType = DamageTypesForThisMission.FirstOrDefault();
                            Logging.Log("AgentInteraction", "[" + _MissionDamageTypeCount_ + "] DamageType [" + _missionDamageType + "]", Logging.White);
                        }

                        LoadCorrectFactionOrMissionAmmo();
                        loadedAmmo = true;
                        return;
                    }

                    return;
                }

                return;
            }
            catch (Exception ex)
            {
                Logging.Log("AgentInteraction", "Error in mission (not pocket) specific XML tags [" + MissionName + "], " + ex.Message, Logging.Orange);
            }
            finally
            {
                missionXml = null;
                System.GC.Collect();
            }

            return;
        }

        public static bool loadedAmmo = false;

        public static void LoadCorrectFactionOrMissionAmmo()
        {
            try
            {
                if (QMCache.Instance.Weapons.Any(i => i.TypeId == (int)TypeID.CivilianGatlingAutocannon
                                                 || i.TypeId == (int)TypeID.CivilianGatlingPulseLaser
                                                 || i.TypeId == (int)TypeID.CivilianGatlingRailgun
                                                 || i.TypeId == (int)TypeID.CivilianLightElectronBlaster))
                {
                    Logging.Log("LoadSpecificAmmo", "No ammo needed for civilian guns: no ammo added to MissionAmmo to load", Logging.White);
                    return;
                }

                if (!MissionSettings.loadedAmmo)
                {
                    Logging.Log("LoadSpecificAmmo", "Clearing existing list of Ammo To load", Logging.White);
                    MissionSettings.AmmoTypesToLoad = new Dictionary<Ammo, DateTime>();
                }

                if (MissionDamageType != null)
                {
                    if (Combat.Ammo.Any(a => a.DamageType == MissionSettings.MissionDamageType))
                    {
                        foreach (KeyValuePair<DamageType, DateTime> missionDamageType in DamageTypesForThisMission)
                        {
                            Logging.Log("LoadSpecificAmmo.mission", "DamageType [" + missionDamageType + "] is one of the damagetypes we should load", Logging.White);
                            KeyValuePair<DamageType, DateTime> damageTypeToSearchFor = missionDamageType;
                            foreach (Ammo specificAmmoType in Combat.Ammo.Where(a => a.DamageType == damageTypeToSearchFor.Key).Select(a => a.Clone()))
                            {
                                Logging.Log("LoadSpecificAmmo.mission", "Adding [" + specificAmmoType + "] to the list of AmmoTypes to load. It is defined as [" + missionDamageType + "]", Logging.White);
                                MissionSettings.AmmoTypesToLoad.AddOrUpdate_(specificAmmoType, DateTime.UtcNow);
                                MissionSettings.loadedAmmo = true;
                            }
                        }
                    }
                }

                if (FactionDamageType != null && !MissionSettings.AmmoTypesToLoad.Any())
                {
                    if (Combat.Ammo.Any(a => a.DamageType == MissionSettings.FactionDamageType))
                    {
                        Logging.Log("LoadSpecificAmmo.faction", "DamageType [" + FactionDamageType + "] is one of the damagetypes we should load", Logging.White);
                        foreach (Ammo specificAmmoType in Combat.Ammo.Where(a => a.DamageType == FactionDamageType).Select(a => a.Clone()))
                        {
                            Logging.Log("LoadSpecificAmmo.faction", "Adding [" + specificAmmoType + "] to the list of AmmoTypes to load. It is defined as [" + FactionDamageType + "]", Logging.White);
                            MissionSettings.AmmoTypesToLoad.AddOrUpdate_(specificAmmoType, DateTime.UtcNow);
                            MissionSettings.loadedAmmo = true;
                        }
                    }
                }

                Logging.Log("LoadSpecificAmmo", "Done building the AmmoToLoad List. AmmoToLoad list follows:", Logging.White);
                int intAmmoToLoad = 0;
                foreach (KeyValuePair<Ammo, DateTime> ammoTypeToLoad in MissionSettings.AmmoTypesToLoad)
                {
                    intAmmoToLoad++;
                    Logging.Log("LoadSpecificAmmo", "AmmoTypesToLoad [" + intAmmoToLoad + "] Name: [" + ammoTypeToLoad.Key.Name + "] DamageType: [" + ammoTypeToLoad.Key.DamageType + "] Range: [" + ammoTypeToLoad.Key.Range + "] Quantity: [" + ammoTypeToLoad.Key.Quantity + "]" , Logging.White);
                }

                return;
            }
            catch (Exception exception)
            {
                Logging.Log("LoadSpecificAmmo", "Exception [" + exception + "]", Logging.Debug);
                return;
            }
        }

        /*
        public static void GetDungeonId(string html)
        {
            HtmlAgilityPack.HtmlDocument missionHtml = new HtmlAgilityPack.HtmlDocument();
            missionHtml.LoadHtml(html);
            try
            {
                foreach (HtmlAgilityPack.HtmlNode nd in missionHtml.DocumentNode.SelectNodes("//a[@href]"))
                {
                    if (nd.Attributes["href"].Value.Contains("dungeonID="))
                    {
                        Cache.Instance.DungeonId = nd.Attributes["href"].Value;
                        Logging.Log("GetDungeonId", "DungeonID is: " + Cache.Instance.DungeonId, Logging.White);
                    }
                    else
                    {
                        Cache.Instance.DungeonId = "n/a";
                    }
                }
            }
            catch (Exception exception)
            {
                Logging.Log("GetDungeonId", "if (nd.Attributes[href].Value.Contains(dungeonID=)) - Exception: [" + exception + "]", Logging.White);
            }
        }
        */

        public static void GetFactionName(string html)
        {
            Statistics.SaveMissionHTMLDetails(html, MissionName);
            // We are going to check damage types
            Regex logoRegex = new Regex("img src=\"factionlogo:(?<factionlogo>\\d+)");

            Match logoMatch = logoRegex.Match(html);
            if (logoMatch.Success)
            {
                string logo = logoMatch.Groups["factionlogo"].Value;

                // Load faction xml
                string factionsXML = Path.Combine(QMSettings.Instance.Path, "Factions.xml");
                try
                {
                    XDocument xml = XDocument.Load(factionsXML);
                    if (xml.Root != null)
                    {
                        XElement faction = xml.Root.Elements("faction").FirstOrDefault(f => (string)f.Attribute("logo") == logo);
                        if (faction != null)
                        {
                            FactionName = (string)faction.Attribute("name");
                            return;
                        }
                    }
                    else
                    {
                        Logging.Log("CombatMissionSettings", "ERROR! unable to read [" + factionsXML + "]  no root element named <faction> ERROR!", Logging.Red);
                    }
                }
                catch (Exception ex)
                {
                    Logging.Log("CombatMissionSettings", "ERROR! unable to find [" + factionsXML + "] ERROR! [" + ex.Message + "]", Logging.Red);
                }
            }

            bool roguedrones = false;
            bool mercenaries = false;
            bool eom = false;
            bool seven = false;
            if (!string.IsNullOrEmpty(html))
            {
                roguedrones |= html.Contains("Destroy the Rogue Drones");
                roguedrones |= html.Contains("Rogue Drone Harassment Objectives");
                roguedrones |= html.Contains("Air Show! Objectives");
                roguedrones |= html.Contains("Alluring Emanations Objectives");
                roguedrones |= html.Contains("Anomaly Objectives");
                roguedrones |= html.Contains("Attack of the Drones Objectives");
                roguedrones |= html.Contains("Drone Detritus Objectives");
                roguedrones |= html.Contains("Drone Infestation Objectives");
                roguedrones |= html.Contains("Evolution Objectives");
                roguedrones |= html.Contains("Infected Ruins Objectives");
                roguedrones |= html.Contains("Infiltrated Outposts Objectives");
                roguedrones |= html.Contains("Mannar Mining Colony");
                roguedrones |= html.Contains("Missing Convoy Objectives");
                roguedrones |= html.Contains("Onslaught Objectives");
                roguedrones |= html.Contains("Patient Zero Objectives");
                roguedrones |= html.Contains("Persistent Pests Objectives");
                roguedrones |= html.Contains("Portal to War Objectives");
                roguedrones |= html.Contains("Rogue Eradication Objectives");
                roguedrones |= html.Contains("Rogue Hunt Objectives");
                roguedrones |= html.Contains("Rogue Spy Objectives");
                roguedrones |= html.Contains("Roving Rogue Drones Objectives");
                roguedrones |= html.Contains("Soothe The Salvage Beast");
                roguedrones |= html.Contains("Wildcat Strike Objectives");
                eom |= html.Contains("Gone Berserk Objectives");
                seven |= html.Contains("The Damsel In Distress Objectives");
            }

            if (roguedrones)
            {
                MissionSettings.FactionName = "rogue drones";
                return;
            }
            if (eom)
            {
                MissionSettings.FactionName = "eom";
                return;
            }
            if (mercenaries)
            {
                MissionSettings.FactionName = "mercenaries";
                return;
            }
            if (seven)
            {
                MissionSettings.FactionName = "the seven";
                return;
            }

            Logging.Log("AgentInteraction", "Unable to find the faction for [" + MissionName + "] when searching through the html (listed below)", Logging.Orange);

            Logging.Log("AgentInteraction", html, Logging.White);
            return;
        }

        /// <summary>
        ///   Best damage type for the mission
        /// </summary>
        public static DamageType? CurrentDamageType
        {
            get
            {
                //if (ManualDamageType == null)
                //{
                    if (PocketDamageType == null)
                    {
                        if (MissionDamageType == null)
                        {
                            if (FactionDamageType == null)
                            {
                                if (Logging.DebugCombat) Logging.Log("CurrentDamageType", "Note: ManualDamageType, PocketDamageType, MissionDamageType and FactionDamageType were all NULL, defaulting to 1st Ammo listed in AmmoToLoad", Logging.Debug);
                                if (AmmoTypesToLoad != null && AmmoTypesToLoad.Any())
                                {
                                    DamageType currentDamageType = AmmoTypesToLoad.FirstOrDefault().Key.DamageType;
                                    return currentDamageType;
                                }

                                return null;
                            }

                            return (DamageType) FactionDamageType;
                        }

                        return (DamageType) MissionDamageType;
                    }

                    return (DamageType)PocketDamageType;
                //}
                //return (DamageType) ManualDamageType;
            }
        }

        //
        // FactionDamageType, MissionDamageType, PocketDamageType, ManualDamageType
        //

        public static DamageType DefaultDamageType { get; set; }
        public static DamageType? FactionDamageType { get; set; }
        public static DamageType? MissionDamageType { get; set; }
        public static DamageType? PocketDamageType { get; set; }
        //public static DamageType? ManualDamageType { get; set; }
        public static Dictionary<DamageType, DateTime> DamageTypesForThisMission  { get; set; }
        public static IEnumerable<DamageType> DamageTypesInMissionXML { get; set; }

        public static DamageType GetFactionDamageType(string html)
        {
            DamageType damageTypeToUse;
            // We are going to check damage types
            Regex logoRegex = new Regex("img src=\"factionlogo:(?<factionlogo>\\d+)");

            Match logoMatch = logoRegex.Match(html);
            if (logoMatch.Success)
            {
                string logo = logoMatch.Groups["factionlogo"].Value;

                // Load faction xml
                XDocument xml = XDocument.Load(Path.Combine(QMSettings.Instance.Path, "Factions.xml"));
                if (xml.Root != null)
                {
                    XElement faction = xml.Root.Elements("faction").FirstOrDefault(f => (string)f.Attribute("logo") == logo);
                    if (faction != null)
                    {
                        FactionName = (string)faction.Attribute("name");
                        Logging.Log("GetMissionDamageType", "[" + MissionName + "] Faction [" + FactionName + "]", Logging.Yellow);
                        if (faction.Attribute("damagetype") != null)
                        {
                            damageTypeToUse = ((DamageType) Enum.Parse(typeof (DamageType), (string) faction.Attribute("damagetype")));
                            Logging.Log("GetMissionDamageType", "Faction DamageType defined as [" + damageTypeToUse + "]", Logging.Yellow);
                            return (DamageType)damageTypeToUse;
                        }

                        Logging.Log("GetMissionDamageType", "DamageType not found for Faction [" + FactionName + "], Defaulting to DamageType  [" + MissionSettings.DefaultDamageType + "]", Logging.Yellow);
                        return MissionSettings.DefaultDamageType;
                    }

                    Logging.Log("GetMissionDamageType", "Faction not found in factions.xml, Defaulting to DamageType  [" + MissionSettings.DefaultDamageType + "]", Logging.Yellow);
                    return MissionSettings.DefaultDamageType;
                }

                Logging.Log("GetMissionDamageType", "Factions.xml is missing, Defaulting to DamageType  [" + MissionSettings.DefaultDamageType + "]", Logging.Yellow);
                return MissionSettings.DefaultDamageType;
            }

            Logging.Log("GetMissionDamageType", "Faction logo not matched, Defaulting to DamageType  [" + MissionSettings.DefaultDamageType + "]", Logging.Yellow);
            return MissionSettings.DefaultDamageType;
        }

        public static void UpdateMissionName(long AgentID = 0)
        {
            if (AgentID != 0)
            {
                MissionSettings.Mission = QMCache.Instance.GetAgentMission(AgentID, true);
                if (MissionSettings.Mission != null && AgentInteraction.Agent != null)
                {
                    // Update loyalty points again (the first time might return -1)
                    Statistics.LoyaltyPoints = AgentInteraction.Agent.LoyaltyPoints;
                    MissionSettings.MissionName = MissionSettings.Mission.Name;
                    //if (Logging.UseInnerspace)
                    //{
                    //    LavishScript.ExecuteCommand("WindowText EVE - " + QMSettings.Instance.CharacterName + " - " + MissionSettings.MissionName);
                    //}
                }
            }
            else
            {
                //if (Logging.UseInnerspace)
                //{
                //    LavishScript.ExecuteCommand("WindowText EVE - " + QMSettings.Instance.CharacterName);
                //}
            }
        }

        public static void SetmissionXmlPath(string missionName)
        {
            try
            {
                if (!string.IsNullOrEmpty(FactionName))
                {
                    MissionXmlPath = System.IO.Path.Combine(MissionsPath, Logging.FilterPath(missionName) + "-" + FactionName + ".xml");
                    if (!File.Exists(MissionXmlPath))
                    {
                        //
                        // This will always fail for courier missions, can we detect those and suppress these log messages?
                        //
                        Logging.Log("Cache.SetmissionXmlPath", "[" + MissionXmlPath + "] not found.", Logging.White);
                        MissionXmlPath = System.IO.Path.Combine(MissionsPath, Logging.FilterPath(missionName) + ".xml");
                        if (!File.Exists(MissionXmlPath))
                        {
                            Logging.Log("Cache.SetmissionXmlPath", "[" + MissionXmlPath + "] not found", Logging.White);
                        }

                        if (File.Exists(MissionXmlPath))
                        {
                            Logging.Log("Cache.SetmissionXmlPath", "[" + MissionXmlPath + "] found!", Logging.Green);
                        }
                    }
                }
                else
                {
                    MissionXmlPath = System.IO.Path.Combine(MissionsPath, Logging.FilterPath(missionName) + ".xml");
                }
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.SetmissionXmlPath", "Exception [" + exception + "]", Logging.Debug);
            }
        }

    }
}
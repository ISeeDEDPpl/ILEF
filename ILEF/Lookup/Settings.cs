
namespace ILEF.Lookup
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Xml;
    using System.Xml.Linq;
    using ILEF.Actions;
    using ILEF.BackgroundTasks;
    using ILEF.Combat;
    using ILEF.Caching;
    using ILEF.Logging;
    using ILEF.States;

    public class QMSettings
    {
        /// <summary>
        /// Singleton implementation
        /// </summary>
        static QMSettings _instance;

        public static QMSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new QMSettings();
                }

                return _instance;
            }
        }

        public string CharacterName;
        private DateTime _lastModifiedDateOfMySettingsFile;
        private DateTime _lastModifiedDateOfMyCommonSettingsFile;
        private int SettingsLoadedICount = 0;

        public static int SettingsInstances = 0;

        public QMSettings()
        {
            try
            {
                Interlocked.Increment(ref SettingsInstances);
            }
            catch (Exception exception)
            {
                Logging.Log("QMSettings Initialization", "Exception: [" + exception + "]", Logging.Red);
                return;
            }
        }

        ~QMSettings()
        {
            Interlocked.Decrement(ref SettingsInstances);
        }

        public bool CharacterXMLExists = true;
        public bool CommonXMLExists = false;
        public bool SchedulesXMLExists = true;
        public bool EVEMemoryManager = false;
        public long MemoryManagerTrimThreshold = 524288000;
        public bool FactionXMLExists = true;
        public bool QuestorStatisticsExists = true;
        public bool QuestorSettingsExists = true;
        public bool QuestorManagerExists = true;

        public string TargetSelectionMethod { get; set; }
        public bool DetailedCurrentTargetHealthLogging { get; set; }
        public bool DefendWhileTraveling { get; set; }

        //public bool setEveClientDestinationWhenTraveling { get; set; }
        public string EveServerName { get; set; }
        public int EnforcedDelayBetweenModuleClicks { get; set; }
        public bool AvoidShootingTargetsWithMissilesIfweKNowTheyAreAboutToBeHitWithAPreviousVolley { get; set; }
        public string CharacterToAcceptInvitesFrom { get; set; }

        //
        // Misc Settings
        //
        public string CharacterMode { get; set; }
        public bool AutoStart { get; set; }
        public bool Disable3D { get; set; }
        public int MinimumDelay { get; set; }
        public int RandomDelay { get; set; }

        //
        // Console Log Settings
        //
        public int MaxLineConsole { get; set; }

        //
        // Enable / Disable Major Features that do not have categories of their own below
        //
        public bool EnableStorylines { get; set; }
        public string StoryLineBaseBookmark { get; set; }
        public bool DeclineStorylinesInsteadofBlacklistingfortheSession { get; set; }
        public bool UseLocalWatch { get; set; }
        public bool UseFittingManager { get; set; }

        public bool WatchForActiveWars { get; set; }

        public bool FleetSupportSlave { get; set; }

        public bool FleetSupportMaster { get; set; }

        public string FleetName { get; set; }
        public List<string> CharacterNamesForMasterToInviteToFleet = new List<string>();

        public int NumberOfModulesToActivateInCycle = 4;

        //
        // Local Watch settings - if enabled
        //
        public int LocalBadStandingPilotsToTolerate { get; set; }
        public double LocalBadStandingLevelToConsiderBad { get; set; }
        public bool FinishWhenNotSafe { get; set; }

        //
        // Invasion Settings
        //
        public int BattleshipInvasionLimit { get; set; }
        public int BattlecruiserInvasionLimit { get; set; }
        public int CruiserInvasionLimit { get; set; }
        public int FrigateInvasionLimit { get; set; }
        public int InvasionMinimumDelay { get; set; }
        public int InvasionRandomDelay { get; set; }

        //
        // Ship Names
        //
        public string SalvageShipName { get; set; }
        public string TransportShipName { get; set; }
        public string TravelShipName { get; set; }
        public string MiningShipName { get; set; }

        //
        //Use HomeBookmark
        //
        public bool UseHomebookmark { get; set; }

        //
        // Storage location for loot, ammo, and bookmarks
        //
        public string HomeBookmarkName { get; set; }
        public string LootHangarTabName { get; set; }
        public string AmmoHangarTabName { get; set; }
        public string BookmarkHangar { get; set; }
        public string LootContainerName { get; set; }

        public string HighTierLootContainer { get; set; }

        //
        // Travel and Undock Settings
        //
        public string BookmarkPrefix { get; set; }
        public string SafeSpotBookmarkPrefix { get; set; }
        public string BookmarkFolder { get; set; }

        public string TravelToBookmarkPrefix { get; set; }

        public string UndockBookmarkPrefix { get; set; }

        //
        // EVE Process Memory Ceiling and EVE wallet balance Change settings
        //
        public int WalletBalanceChangeLogOffDelay { get; set; }

        public string WalletBalanceChangeLogOffDelayLogoffOrExit { get; set; }

        public Int64 EVEProcessMemoryCeiling { get; set; }
        public bool CloseQuestorCMDUplinkInnerspaceProfile { get; set; }
        public bool CloseQuestorCMDUplinkIsboxerCharacterSet { get; set; }
        public bool CloseQuestorAllowRestart { get; set; }
        public bool CloseQuestorArbitraryOSCmd { get; set; }
        public string CloseQuestorOSCmdContents { get; set; }
        public bool LoginQuestorArbitraryOSCmd { get; set; }
        public string LoginQuestorOSCmdContents { get; set; }
        public bool LoginQuestorLavishScriptCmd { get; set; }
        public string LoginQuestorLavishScriptContents { get; set; }
        public bool MinimizeEveAfterStartingUp { get; set; }

        public string LavishIsBoxerCharacterSet { get; set; }
        public string LavishInnerspaceProfile { get; set; }
        public string LavishGame { get; set; }

        //
        // Script Settings - TypeIDs for the scripts you would like to use in these modules
        //
        public int TrackingDisruptorScript { get; private set; }
        public int TrackingComputerScript { get; private set; }
        public int TrackingLinkScript { get; private set; }
        public int SensorBoosterScript { get; private set; }
        public int SensorDampenerScript { get; private set; }
        public int AncillaryShieldBoosterScript { get; private set; } //they are not scripts, but they work the same, but are consumable for our purposes that does not matter
        public int CapacitorInjectorScript { get; private set; }      //they are not scripts, but they work the same, but are consumable for our purposes that does not matter
        public int NumberOfCapBoostersToLoad { get; private set; }
        //
        // OverLoad Settings (this WILL burn out modules, likely very quickly!
        // If you enable the overloading of a slot it is HIGHLY recommended you actually have something overloadable in that slot =/
        //
        public bool OverloadWeapons { get; set; }

        //
        // Questor GUI location settings
        //
        public int? WindowXPosition { get; set; }
        public int? WindowYPosition { get; set; }
        public int? EVEWindowXPosition { get; set; }
        public int? EVEWindowYPosition { get; set; }
        public int? EVEWindowXSize { get; set; }
        public int? EVEWindowYSize { get; set; }

        //
        // Email SMTP settings
        //
        public bool EmailSupport { get; private set; }
        public string EmailAddress { get; private set; }
        public string EmailPassword { get; private set; }
        public string EmailSMTPServer { get; private set; }
        public int EmailSMTPPort { get; private set; }
        public string EmailAddressToSendAlerts { get; set; }
        public bool? EmailEnableSSL { get; private set; }

        //
        // Skill Training Settings
        //
        public bool ThisToonShouldBeTrainingSkills { get; set; } //as opposed to another toon on the same account

        public string UserDefinedLavishScriptScript1 { get; set; }
        public string UserDefinedLavishScriptScript1Description { get; set; }
        public string UserDefinedLavishScriptScript2 { get; set; }
        public string UserDefinedLavishScriptScript2Description { get; set; }
        public string UserDefinedLavishScriptScript3 { get; set; }
        public string UserDefinedLavishScriptScript3Description { get; set; }
        public string UserDefinedLavishScriptScript4 { get; set; }
        public string UserDefinedLavishScriptScript4Description { get; set; }

        public string LoadQuestorDebugInnerspaceCommandAlias { get; set; }
        public string LoadQuestorDebugInnerspaceCommand { get; set; }
        public string UnLoadQuestorDebugInnerspaceCommandAlias { get; set; }
        public string UnLoadQuestorDebugInnerspaceCommand { get; set; }

        //
        // path information - used to load the XML and used in other modules
        //
        public string Path = Logging.PathToCurrentDirectory;

        public string CommonSettingsPath { get; private set; }
        public string CommonSettingsFileName { get; private set; }

        public event EventHandler<EventArgs> SettingsLoaded;

        public bool DefaultSettingsLoaded;
        public XElement CommonSettingsXml { get; set; }
        public XElement CharacterSettingsXml { get; set; }
        public void ReadSettingsFromXML()
        {
            try
            {
                QMSettings.Instance.CommonSettingsFileName = (string)CharacterSettingsXml.Element("commonSettingsFileName") ?? "common.xml";
                QMSettings.Instance.CommonSettingsPath = System.IO.Path.Combine(QMSettings.Instance.Path, QMSettings.Instance.CommonSettingsFileName);

                if (File.Exists(QMSettings.Instance.CommonSettingsPath))
                {
                    QMSettings.Instance.CommonXMLExists = true;
                    CommonSettingsXml = XDocument.Load(QMSettings.Instance.CommonSettingsPath).Root;
                    if (CommonSettingsXml == null)
                    {
                        Logging.Log("Settings", "found [" + QMSettings.Instance.CommonSettingsPath + "] but was unable to load it: FATAL ERROR - use the provided settings.xml to create that file.", Logging.Red);
                    }
                }
                else
                {
                    QMSettings.Instance.CommonXMLExists = false;
                    //
                    // if the common XML does not exist, load the characters XML into the CommonSettingsXml just so we can simplify the XML element loading stuff.
                    //
                    CommonSettingsXml = XDocument.Load(Logging.CharacterSettingsPath).Root;
                }

                if (CommonSettingsXml == null) return; // this should never happen as we load the characters xml here if the common xml is missing. adding this does quiet some warnings though

                if (QMSettings.Instance.CommonXMLExists) Logging.Log("Settings", "Loading Settings from [" + QMSettings.Instance.CommonSettingsPath + "] and", Logging.Green);
                Logging.Log("Settings", "Loading Settings from [" + Logging.CharacterSettingsPath + "]", Logging.Green);
                //
                // these are listed by feature and should likely be re-ordered to reflect that
                //

                //
                // Debug Settings
                //
                Logging.DebugActivateGate = (bool?)CharacterSettingsXml.Element("debugActivateGate") ?? (bool?)CommonSettingsXml.Element("debugActivateGate") ?? false;
                Logging.DebugActivateBastion = (bool?)CharacterSettingsXml.Element("debugActivateBastion") ?? (bool?)CommonSettingsXml.Element("debugActivateBastion") ?? false;
                Logging.DebugActivateWeapons = (bool?)CharacterSettingsXml.Element("debugActivateWeapons") ?? (bool?)CommonSettingsXml.Element("debugActivateWeapons") ?? false;
                Logging.DebugAddDronePriorityTarget = (bool?)CharacterSettingsXml.Element("debugAddDronePriorityTarget") ?? (bool?)CommonSettingsXml.Element("debugAddDronePriorityTarget") ?? false;
                Logging.DebugAddPrimaryWeaponPriorityTarget = (bool?)CharacterSettingsXml.Element("debugAddPrimaryWeaponPriorityTarget") ?? (bool?)CommonSettingsXml.Element("debugAddPrimaryWeaponPriorityTarget") ?? false;
                Logging.DebugAgentInteractionReplyToAgent = (bool?)CharacterSettingsXml.Element("debugAgentInteractionReplyToAgent") ?? (bool?)CommonSettingsXml.Element("debugAgentInteractionReplyToAgent") ?? false;
                Logging.DebugAllMissionsOnBlackList = (bool?)CharacterSettingsXml.Element("debugAllMissionsOnBlackList") ?? (bool?)CommonSettingsXml.Element("debugAllMissionsOnBlackList") ?? false;
                Logging.DebugAllMissionsOnGreyList = (bool?)CharacterSettingsXml.Element("debugAllMissionsOnGreyList") ?? (bool?)CommonSettingsXml.Element("debugAllMissionsOnGreyList") ?? false;
                Logging.DebugAmmo = (bool?)CharacterSettingsXml.Element("debugAmmo") ?? (bool?)CommonSettingsXml.Element("debugAmmo") ?? false;
                Logging.DebugArm = (bool?)CharacterSettingsXml.Element("debugArm") ?? (bool?)CommonSettingsXml.Element("debugArm") ?? false;
                Logging.DebugAttachVSDebugger = (bool?)CharacterSettingsXml.Element("debugAttachVSDebugger") ?? (bool?)CommonSettingsXml.Element("debugAttachVSDebugger") ?? false;
                Logging.DebugAutoStart = (bool?)CharacterSettingsXml.Element("debugAutoStart") ?? (bool?)CommonSettingsXml.Element("debugAutoStart") ?? false;
                Logging.DebugBlackList = (bool?)CharacterSettingsXml.Element("debugBlackList") ?? (bool?)CommonSettingsXml.Element("debugBlackList") ?? false;
                Logging.DebugCargoHold = (bool?)CharacterSettingsXml.Element("debugCargoHold") ?? (bool?)CommonSettingsXml.Element("debugCargoHold") ?? false;
                Logging.DebugChat = (bool?)CharacterSettingsXml.Element("debugChat") ?? (bool?)CommonSettingsXml.Element("debugChat") ?? false;
                Logging.DebugCleanup = (bool?)CharacterSettingsXml.Element("debugCleanup") ?? (bool?)CommonSettingsXml.Element("debugCleanup") ?? false;
                Logging.DebugClearPocket = (bool?)CharacterSettingsXml.Element("debugClearPocket") ?? (bool?)CommonSettingsXml.Element("debugClearPocket") ?? false;
                Logging.DebugCombat = (bool?)CharacterSettingsXml.Element("debugCombat") ?? (bool?)CommonSettingsXml.Element("debugCombat") ?? false;
                Logging.DebugCombatMissionBehavior = (bool?)CharacterSettingsXml.Element("debugCombatMissionBehavior") ?? (bool?)CommonSettingsXml.Element("debugCombatMissionBehavior") ?? false;
                Logging.DebugCourierMissions = (bool?)CharacterSettingsXml.Element("debugCourierMissions") ?? (bool?)CommonSettingsXml.Element("debugCourierMissions") ?? false;
                Logging.DebugDecline = (bool?)CharacterSettingsXml.Element("debugDecline") ?? (bool?)CommonSettingsXml.Element("debugDecline") ?? false;
                Logging.DebugDefense = (bool?)CharacterSettingsXml.Element("debugDefense") ?? (bool?)CommonSettingsXml.Element("debugDefense") ?? false;
                Logging.DebugDisableCleanup = (bool?)CharacterSettingsXml.Element("debugDisableCleanup") ?? (bool?)CommonSettingsXml.Element("debugDisableCleanup") ?? false;
                Logging.DebugDisableCombatMissionsBehavior = (bool?)CharacterSettingsXml.Element("debugDisableCombatMissionsBehavior") ?? (bool?)CommonSettingsXml.Element("debugDisableCombatMissionsBehavior") ?? false;
                Logging.DebugDisableCombatMissionCtrl = (bool?)CharacterSettingsXml.Element("debugDisableCombatMissionCtrl") ?? (bool?)CommonSettingsXml.Element("debugDisableCombatMissionCtrl") ?? false;
                Logging.DebugDisableCombat = (bool?)CharacterSettingsXml.Element("debugDisableCombat") ?? (bool?)CommonSettingsXml.Element("debugDisableCombat") ?? false;
                Logging.DebugDisableDrones = (bool?)CharacterSettingsXml.Element("debugDisableDrones") ?? (bool?)CommonSettingsXml.Element("debugDisableDrones") ?? false;
                Logging.DebugDisablePanic = (bool?)CharacterSettingsXml.Element("debugDisablePanic") ?? (bool?)CommonSettingsXml.Element("debugDisablePanic") ?? false;
                Logging.DebugDisableGetBestTarget = (bool?)CharacterSettingsXml.Element("debugDisableGetBestTarget") ?? (bool?)CommonSettingsXml.Element("debugDisableGetBestTarget") ?? false;
                Logging.DebugDisableGetBestDroneTarget = (bool?)CharacterSettingsXml.Element("debugDisableGetBestDroneTarget") ?? (bool?)CommonSettingsXml.Element("debugDisableGetBestTarget") ?? false;
                Logging.DebugDisableSalvage = (bool?)CharacterSettingsXml.Element("debugDisableSalvage") ?? (bool?)CommonSettingsXml.Element("debugDisableSalvage") ?? false;
                Logging.DebugDisableGetBestTarget = (bool?)CharacterSettingsXml.Element("debugDisableGetBestTarget") ?? (bool?)CommonSettingsXml.Element("debugDisableGetBestTarget") ?? false;
                Logging.DebugDisableTargetCombatants = (bool?)CharacterSettingsXml.Element("debugDisableTargetCombatants") ?? (bool?)CommonSettingsXml.Element("debugDisableTargetCombatants") ?? false;
                Logging.DebugDisableNavigateIntoRange = (bool?)CharacterSettingsXml.Element("debugDisableNavigateIntoRange") ?? (bool?)CommonSettingsXml.Element("debugDisableNavigateIntoRange") ?? false;
                Logging.DebugDoneAction = (bool?)CharacterSettingsXml.Element("debugDoneAction") ?? (bool?)CommonSettingsXml.Element("debugDoneAction") ?? false;
                Logging.DebugDoNotCloseTelcomWindows = (bool?)CharacterSettingsXml.Element("debugDoNotCloseTelcomWindows") ?? (bool?)CommonSettingsXml.Element("debugDoNotCloseTelcomWindows") ?? false;
                Logging.DebugDrones = (bool?)CharacterSettingsXml.Element("debugDrones") ?? (bool?)CommonSettingsXml.Element("debugDrones") ?? false;
                Logging.DebugDroneHealth = (bool?)CharacterSettingsXml.Element("debugDroneHealth") ?? (bool?)CommonSettingsXml.Element("debugDroneHealth") ?? false;
                Logging.DebugEachWeaponsVolleyCache = (bool?)CharacterSettingsXml.Element("debugEachWeaponsVolleyCache") ?? (bool?)CommonSettingsXml.Element("debugEachWeaponsVolleyCache") ?? false;
                Logging.DebugEntityCache = (bool?)CharacterSettingsXml.Element("debugEntityCache") ?? (bool?)CommonSettingsXml.Element("debugEntityCache") ?? false;
                Logging.DebugExecuteMission = (bool?)CharacterSettingsXml.Element("debugExecutMission") ?? (bool?)CommonSettingsXml.Element("debugExecutMission") ?? false;
                Logging.DebugExceptions = (bool?)CharacterSettingsXml.Element("debugExceptions") ?? (bool?)CommonSettingsXml.Element("debugExceptions") ?? false;
                Logging.DebugFittingMgr = (bool?)CharacterSettingsXml.Element("debugFittingMgr") ?? (bool?)CommonSettingsXml.Element("debugFittingMgr") ?? false;
                Logging.DebugFleetSupportSlave = (bool?)CharacterSettingsXml.Element("debugFleetSupportSlave") ?? (bool?)CommonSettingsXml.Element("debugFleetSupportSlave") ?? false;
                Logging.DebugFleetSupportMaster = (bool?)CharacterSettingsXml.Element("debugFleetSupportMaster") ?? (bool?)CommonSettingsXml.Element("debugFleetSupportMaster") ?? false;
                Logging.DebugGetBestTarget = (bool?)CharacterSettingsXml.Element("debugGetBestTarget") ?? (bool?)CommonSettingsXml.Element("debugGetBestTarget") ?? false;
                Logging.DebugGetBestDroneTarget = (bool?)CharacterSettingsXml.Element("debugGetBestDroneTarget") ?? (bool?)CommonSettingsXml.Element("debugGetBestDroneTarget") ?? false;
                Logging.DebugGotobase = (bool?)CharacterSettingsXml.Element("debugGotobase") ?? (bool?)CommonSettingsXml.Element("debugGotobase") ?? false;
                Logging.DebugGreyList = (bool?)CharacterSettingsXml.Element("debugGreyList") ?? (bool?)CommonSettingsXml.Element("debugGreyList") ?? false;
                Logging.DebugHangars = (bool?)CharacterSettingsXml.Element("debugHangars") ?? (bool?)CommonSettingsXml.Element("debugHangars") ?? false;
                Logging.DebugIdle = (bool?)CharacterSettingsXml.Element("debugIdle") ?? (bool?)CommonSettingsXml.Element("debugIdle") ?? false;
                Logging.DebugInSpace = (bool?)CharacterSettingsXml.Element("debugInSpace") ?? (bool?)CommonSettingsXml.Element("debugInSpace") ?? false;
                Logging.DebugInStation = (bool?)CharacterSettingsXml.Element("debugInStation") ?? (bool?)CommonSettingsXml.Element("debugInStation") ?? false;
                Logging.DebugInWarp = (bool?)CharacterSettingsXml.Element("debugInWarp") ?? (bool?)CommonSettingsXml.Element("debugInWarp") ?? false;
                Logging.DebugIsReadyToShoot = (bool?)CharacterSettingsXml.Element("debugIsReadyToShoot") ?? (bool?)CommonSettingsXml.Element("debugIsReadyToShoot") ?? false;
                Logging.DebugItemHangar = (bool?)CharacterSettingsXml.Element("debugItemHangar") ?? (bool?)CommonSettingsXml.Element("debugItemHangar") ?? false;
                Logging.DebugKillTargets = (bool?)CharacterSettingsXml.Element("debugKillTargets") ?? (bool?)CommonSettingsXml.Element("debugKillTargets") ?? false;
                Logging.DebugKillAction = (bool?)CharacterSettingsXml.Element("debugKillAction") ?? (bool?)CommonSettingsXml.Element("debugKillAction") ?? false;
                Logging.DebugLoadScripts = (bool?)CharacterSettingsXml.Element("debugLoadScripts") ?? (bool?)CommonSettingsXml.Element("debugLoadScripts") ?? false;
                Logging.DebugLogging = (bool?)CharacterSettingsXml.Element("debugLogging") ?? (bool?)CommonSettingsXml.Element("debugLogging") ?? false;
                Logging.DebugLootWrecks = (bool?)CharacterSettingsXml.Element("debugLootWrecks") ?? (bool?)CommonSettingsXml.Element("debugLootWrecks") ?? false;
                Logging.DebugLootValue = (bool?)CharacterSettingsXml.Element("debugLootValue") ?? (bool?)CommonSettingsXml.Element("debugLootValue") ?? false;
                Logging.DebugMaintainConsoleLogs = (bool?)CharacterSettingsXml.Element("debugMaintainConsoleLogs") ?? (bool?)CommonSettingsXml.Element("debugMaintainConsoleLogs") ?? false;
                Logging.DebugMiningBehavior = (bool?)CharacterSettingsXml.Element("debugMiningBehavior") ?? (bool?)CommonSettingsXml.Element("debugMiningBehavior") ?? false;
                Logging.DebugMissionFittings = (bool?)CharacterSettingsXml.Element("debugMissionFittings") ?? (bool?)CommonSettingsXml.Element("debugMissionFittings") ?? false;
                Logging.DebugMoveTo = (bool?)CharacterSettingsXml.Element("debugMoveTo") ?? (bool?)CommonSettingsXml.Element("debugMoveTo") ?? false;
                Logging.DebugNavigateOnGrid = (bool?)CharacterSettingsXml.Element("debugNavigateOnGrid") ?? (bool?)CommonSettingsXml.Element("debugNavigateOnGrid") ?? false;
                Logging.DebugOnframe = (bool?)CharacterSettingsXml.Element("debugOnframe") ?? (bool?)CommonSettingsXml.Element("debugOnframe") ?? false;
                Logging.DebugOverLoadWeapons = (bool?)CharacterSettingsXml.Element("debugOverLoadWeapons") ?? (bool?)CommonSettingsXml.Element("debugOverLoadWeapons") ?? false;
                Logging.DebugPanic = (bool?)CharacterSettingsXml.Element("debugPanic") ?? (bool?)CommonSettingsXml.Element("debugPanic") ?? false;
                Logging.DebugPerformance = (bool?)CharacterSettingsXml.Element("debugPerformance") ?? (bool?)CommonSettingsXml.Element("debugPerformance") ?? false;                                     //enables more console logging having to do with the sub-states within each state
                Logging.DebugPotentialCombatTargets = (bool?)CharacterSettingsXml.Element("debugPotentialCombatTargets") ?? (bool?)CommonSettingsXml.Element("debugPotentialCombatTargets") ?? false;
                Logging.DebugPreferredPrimaryWeaponTarget = (bool?)CharacterSettingsXml.Element("debugPreferredPrimaryWeaponTarget") ?? (bool?)CommonSettingsXml.Element("debugPreferredPrimaryWeaponTarget") ?? false;
                Logging.DebugPreLogin = (bool?)CharacterSettingsXml.Element("debugPreferredPrimaryWeaponTarget") ?? (bool?)CommonSettingsXml.Element("debugPreferredPrimaryWeaponTarget") ?? false;
                Logging.DebugQuestorManager = (bool?)CharacterSettingsXml.Element("debugQuestorManager") ?? (bool?)CommonSettingsXml.Element("debugQuestorManager") ?? false;
                Logging.DebugQuestorEVEOnFrame = (bool?)CharacterSettingsXml.Element("debugQuestorEVEOnFrame") ?? (bool?)CommonSettingsXml.Element("debugQuestorEVEOnFrame") ?? false;
                Logging.DebugReloadAll = (bool?)CharacterSettingsXml.Element("debugReloadAll") ?? (bool?)CommonSettingsXml.Element("debugReloadAll") ?? false;
                Logging.DebugReloadorChangeAmmo = (bool?)CharacterSettingsXml.Element("debugReloadOrChangeAmmo") ?? (bool?)CommonSettingsXml.Element("debugReloadOrChangeAmmo") ?? false;
                Logging.DebugRemoteRepair = (bool?)CharacterSettingsXml.Element("debugRemoteRepair") ?? (bool?)CommonSettingsXml.Element("debugRemoteRepair") ?? false;
                Logging.DebugSalvage = (bool?)CharacterSettingsXml.Element("debugSalvage") ?? (bool?)CommonSettingsXml.Element("debugSalvage") ?? false;
                Logging.DebugScheduler = (bool?)CharacterSettingsXml.Element("debugScheduler") ?? (bool?)CommonSettingsXml.Element("debugScheduler") ?? false;
                Logging.DebugSettings = (bool?)CharacterSettingsXml.Element("debugSettings") ?? (bool?)CommonSettingsXml.Element("debugSettings") ?? false;
                Logging.DebugShipTargetValues = (bool?)CharacterSettingsXml.Element("debugShipTargetValues") ?? (bool?)CommonSettingsXml.Element("debugShipTargetValues") ?? false;
                Logging.DebugSkillTraining = (bool?)CharacterSettingsXml.Element("debugSkillTraining") ?? (bool?)CommonSettingsXml.Element("debugSkillTraining") ?? false;
                Logging.DebugSpeedMod = (bool?)CharacterSettingsXml.Element("debugSpeedMod") ?? (bool?)CommonSettingsXml.Element("debugSpeedMod") ?? false;
                Logging.DebugStatistics = (bool?)CharacterSettingsXml.Element("debugStatistics") ?? (bool?)CommonSettingsXml.Element("debugStatistics") ?? false;
                Logging.DebugStorylineMissions = (bool?)CharacterSettingsXml.Element("debugStorylineMissions") ?? (bool?)CommonSettingsXml.Element("debugStorylineMissions") ?? false;
                Logging.DebugTargetCombatants = (bool?)CharacterSettingsXml.Element("debugTargetCombatants") ?? (bool?)CommonSettingsXml.Element("debugTargetCombatants") ?? false;
                Logging.DebugTargetWrecks = (bool?)CharacterSettingsXml.Element("debugTargetWrecks") ?? (bool?)CommonSettingsXml.Element("debugTargetWrecks") ?? false;
                Logging.DebugTraveler = (bool?)CharacterSettingsXml.Element("debugTraveler") ?? (bool?)CommonSettingsXml.Element("debugTraveler") ?? false;
                Logging.DebugTractorBeams = (bool?)CharacterSettingsXml.Element("debugTractorBeams") ?? (bool?)CommonSettingsXml.Element("debugTractorBeams") ?? false;
                Logging.DebugUI = (bool?)CharacterSettingsXml.Element("debugUI") ?? (bool?)CommonSettingsXml.Element("debugUI") ?? false;
                Logging.DebugUndockBookmarks = (bool?)CharacterSettingsXml.Element("debugUndockBookmarks") ?? (bool?)CommonSettingsXml.Element("debugUndockBookmarks") ?? false;
                Logging.DebugUnloadLoot = (bool?)CharacterSettingsXml.Element("debugUnloadLoot") ?? (bool?)CommonSettingsXml.Element("debugUnloadLoot") ?? false;
                Logging.DebugValuedump = (bool?)CharacterSettingsXml.Element("debugValuedump") ?? (bool?)CommonSettingsXml.Element("debugValuedump") ?? false;
                Logging.DebugWalletBalance = (bool?)CharacterSettingsXml.Element("debugWalletBalance") ?? (bool?)CommonSettingsXml.Element("debugWalletBalance") ?? false;
                Logging.DebugWeShouldBeInSpaceORInStationAndOutOfSessionChange = (bool?)CharacterSettingsXml.Element("debugWeShouldBeInSpaceORInStationAndOutOfSessionChange") ?? (bool?)CommonSettingsXml.Element("debugWeShouldBeInSpaceORInStationAndOutOfSessionChange") ?? false;
                Logging.DebugWatchForActiveWars = (bool?)CharacterSettingsXml.Element("debugWatchForActiveWars") ?? (bool?)CommonSettingsXml.Element("debugWatchForActiveWars") ?? false;
                DetailedCurrentTargetHealthLogging = (bool?)CharacterSettingsXml.Element("detailedCurrentTargetHealthLogging") ?? (bool?)CommonSettingsXml.Element("detailedCurrentTargetHealthLogging") ?? true;
                DefendWhileTraveling = (bool?)CharacterSettingsXml.Element("defendWhileTraveling") ?? (bool?)CommonSettingsXml.Element("defendWhileTraveling") ?? true;
                //Logging.UseInnerspace = (bool?)CharacterSettingsXml.Element("useInnerspace") ?? (bool?)CommonSettingsXml.Element("useInnerspace") ?? true;
                //setEveClientDestinationWhenTraveling = (bool?)CharacterSettingsXml.Element("setEveClientDestinationWhenTraveling") ?? (bool?)CommonSettingsXml.Element("setEveClientDestinationWhenTraveling") ?? false;
                TargetSelectionMethod = (string)CharacterSettingsXml.Element("targetSelectionMethod") ?? (string)CommonSettingsXml.Element("targetSelectionMethod") ?? "isdp"; //other choice is "old"
                CharacterToAcceptInvitesFrom = (string)CharacterSettingsXml.Element("characterToAcceptInvitesFrom") ?? (string)CommonSettingsXml.Element("characterToAcceptInvitesFrom") ?? QMSettings.Instance.CharacterName;
                MemoryManagerTrimThreshold = (long?)CharacterSettingsXml.Element("memoryManagerTrimThreshold") ?? (long?)CommonSettingsXml.Element("memoryManagerTrimThreshold") ?? 524288000;
                EveServerName = (string)CharacterSettingsXml.Element("eveServerName") ?? (string)CommonSettingsXml.Element("eveServerName") ?? "Tranquility";
                EnforcedDelayBetweenModuleClicks = (int?)CharacterSettingsXml.Element("enforcedDelayBetweenModuleClicks") ?? (int?)CommonSettingsXml.Element("enforcedDelayBetweenModuleClicks") ?? 3000;
                AvoidShootingTargetsWithMissilesIfweKNowTheyAreAboutToBeHitWithAPreviousVolley = (bool?)CharacterSettingsXml.Element("avoidShootingTargetsWithMissilesIfweKNowTheyAreAboutToBeHitWithAPreviousVolley") ?? (bool?)CommonSettingsXml.Element("AvoidShootingTargetsWithMissilesIfweKNowTheyAreAboutToBeHitWithAPreviousVolley") ?? false;
                //
                // Misc Settings
                //
                CharacterMode = (string)CharacterSettingsXml.Element("characterMode") ?? (string)CommonSettingsXml.Element("characterMode") ?? "Combat Missions".ToLower();

                //other option is "salvage"

                //if (!Cache.Instance.DirectEve.Login.AtLogin || DateTime.UtcNow > Time.Instance.QuestorStarted_DateTime.AddMinutes(1))
                //{
                    Combat.Ammo = new List<Ammo>();
                    if (QMSettings.Instance.CharacterMode.ToLower() == "dps".ToLower())
                    {
                        QMSettings.Instance.CharacterMode = "Combat Missions".ToLower();
                    }

                    AutoStart = (bool?)CharacterSettingsXml.Element("autoStart") ?? (bool?)CommonSettingsXml.Element("autoStart") ?? false; // auto Start enabled or disabled by default?
                //}

                MaxLineConsole = (int?)CharacterSettingsXml.Element("maxLineConsole") ?? (int?)CommonSettingsXml.Element("maxLineConsole") ?? 1000;
                // maximum console log lines to show in the GUI
                Disable3D = (bool?)CharacterSettingsXml.Element("disable3D") ?? (bool?)CommonSettingsXml.Element("disable3D") ?? false; // Disable3d graphics while in space
                RandomDelay = (int?)CharacterSettingsXml.Element("randomDelay") ?? (int?)CommonSettingsXml.Element("randomDelay") ?? 0;
                MinimumDelay = (int?)CharacterSettingsXml.Element("minimumDelay") ?? (int?)CommonSettingsXml.Element("minimumDelay") ?? 0;

                //if (!Cache.Instance.DirectEve.Login.AtLogin || DateTime.UtcNow > Time.Instance.QuestorStarted_DateTime.AddMinutes(1))
                //{
                    //
                    // Enable / Disable Major Features that do not have categories of their own below
                    //
                    try
                    {
                        UseFittingManager = (bool?)CharacterSettingsXml.Element("UseFittingManager") ?? (bool?)CommonSettingsXml.Element("UseFittingManager") ?? true;
                        EnableStorylines = (bool?)CharacterSettingsXml.Element("enableStorylines") ?? (bool?)CommonSettingsXml.Element("enableStorylines") ?? false;
                        StoryLineBaseBookmark = (string)CharacterSettingsXml.Element("storyLineBaseBookmark") ?? (string)CommonSettingsXml.Element("storyLineBaseBookmark") ?? string.Empty;
                        DeclineStorylinesInsteadofBlacklistingfortheSession = (bool?)CharacterSettingsXml.Element("declineStorylinesInsteadofBlacklistingfortheSession") ?? (bool?)CommonSettingsXml.Element("declineStorylinesInsteadofBlacklistingfortheSession") ?? false;
                        UseLocalWatch = (bool?)CharacterSettingsXml.Element("UseLocalWatch") ?? (bool?)CommonSettingsXml.Element("UseLocalWatch") ?? true;
                        WatchForActiveWars = (bool?)CharacterSettingsXml.Element("watchForActiveWars") ?? (bool?)CommonSettingsXml.Element("watchForActiveWars") ?? true;

                        FleetSupportSlave = (bool?)CharacterSettingsXml.Element("fleetSupportSlave") ?? (bool?)CommonSettingsXml.Element("fleetSupportSlave") ?? true;
                        FleetSupportMaster = (bool?)CharacterSettingsXml.Element("fleetSupportMaster") ?? (bool?)CommonSettingsXml.Element("fleetSupportMaster") ?? true;
                        FleetName = (string)CharacterSettingsXml.Element("fleetName") ?? (string)CommonSettingsXml.Element("fleetName") ?? "Fleet1";
                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Major Feature Settings: Exception [" + exception + "]", Logging.Teal);
                    }


                    //
                    //CharacterNamesForMasterToInviteToFleet
                    //
                    QMSettings.Instance.CharacterNamesForMasterToInviteToFleet.Clear();
                    XElement xmlCharacterNamesForMasterToInviteToFleet = CharacterSettingsXml.Element("characterNamesForMasterToInviteToFleet") ?? CharacterSettingsXml.Element("characterNamesForMasterToInviteToFleet");
                    if (xmlCharacterNamesForMasterToInviteToFleet != null)
                    {
                        Logging.Log("Settings", "Loading CharacterNames For Master To Invite To Fleet", Logging.White);
                        int i = 1;
                        foreach (XElement CharacterToInvite in xmlCharacterNamesForMasterToInviteToFleet.Elements("character"))
                        {
                            QMSettings.Instance.CharacterNamesForMasterToInviteToFleet.Add((string)CharacterToInvite);
                            if (Logging.DebugFleetSupportMaster) Logging.Log("Settings.LoadFleetList", "[" + i + "] CharacterName [" + (string)CharacterToInvite + "]", Logging.Teal);
                            i++;
                        }
                        if (QMSettings.Instance.FleetSupportMaster) Logging.Log("Settings", "        CharacterNamesForMasterToInviteToFleet now has [" + CharacterNamesForMasterToInviteToFleet.Count + "] entries", Logging.White);
                    }

                    //
                    // Agent Standings and Mission Settings
                    //
                    try
                    {
                        //if (QMSettings.Instance.CharacterMode.ToLower() == "Combat Missions".ToLower())
                        //{
                        MissionSettings.MinAgentBlackListStandings = (float?)CharacterSettingsXml.Element("minAgentBlackListStandings") ?? (float?)CommonSettingsXml.Element("minAgentBlackListStandings") ?? (float)6.0;
                        MissionSettings.MinAgentGreyListStandings = (float?)CharacterSettingsXml.Element("minAgentGreyListStandings") ?? (float?)CommonSettingsXml.Element("minAgentGreyListStandings") ?? (float)5.0;
                        MissionSettings.WaitDecline = (bool?)CharacterSettingsXml.Element("waitDecline") ?? (bool?)CommonSettingsXml.Element("waitDecline") ?? false;

                        string relativeMissionsPath = (string)CharacterSettingsXml.Element("missionsPath") ?? (string)CommonSettingsXml.Element("missionsPath");
                        MissionSettings.MissionsPath = System.IO.Path.Combine(QMSettings.Instance.Path, relativeMissionsPath);
                        Logging.Log("Settings", "MissionsPath is: [" + MissionSettings.MissionsPath + "]", Logging.White);

                        MissionSettings.RequireMissionXML = (bool?)CharacterSettingsXml.Element("requireMissionXML") ?? (bool?)CommonSettingsXml.Element("requireMissionXML") ?? false;
                        MissionSettings.AllowNonStorylineCourierMissionsInLowSec = (bool?)CharacterSettingsXml.Element("LowSecMissions") ?? (bool?)CommonSettingsXml.Element("LowSecMissions") ?? false;
                        MissionSettings.MaterialsForWarOreID = (int?)CharacterSettingsXml.Element("MaterialsForWarOreID") ?? (int?)CommonSettingsXml.Element("MaterialsForWarOreID") ?? 20;
                        MissionSettings.MaterialsForWarOreQty = (int?)CharacterSettingsXml.Element("MaterialsForWarOreQty") ?? (int?)CommonSettingsXml.Element("MaterialsForWarOreQty") ?? 8000;
                        Combat.KillSentries = (bool?)CharacterSettingsXml.Element("killSentries") ?? (bool?)CommonSettingsXml.Element("killSentries") ?? false;
                        //}
                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Agent Standings and Mission Settings: Exception [" + exception + "]", Logging.Teal);
                    }


                    //
                    // Local Watch Settings - if enabled
                    //
                    try
                    {
                        LocalBadStandingPilotsToTolerate = (int?)CharacterSettingsXml.Element("LocalBadStandingPilotsToTolerate") ?? (int?)CommonSettingsXml.Element("LocalBadStandingPilotsToTolerate") ?? 1;
                        LocalBadStandingLevelToConsiderBad = (double?)CharacterSettingsXml.Element("LocalBadStandingLevelToConsiderBad") ?? (double?)CommonSettingsXml.Element("LocalBadStandingLevelToConsiderBad") ?? -0.1;
                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Local watch Settings: Exception [" + exception + "]", Logging.Teal);
                    }

                    //
                    // Invasion Settings
                    //
                    try
                    {
                        BattleshipInvasionLimit = (int?)CharacterSettingsXml.Element("battleshipInvasionLimit") ?? (int?)CommonSettingsXml.Element("battleshipInvasionLimit") ?? 0;

                        // if this number of BattleShips lands on grid while in a mission we will enter panic
                        BattlecruiserInvasionLimit = (int?)CharacterSettingsXml.Element("battlecruiserInvasionLimit") ?? (int?)CommonSettingsXml.Element("battlecruiserInvasionLimit") ?? 0;

                        // if this number of BattleCruisers lands on grid while in a mission we will enter panic
                        CruiserInvasionLimit = (int?)CharacterSettingsXml.Element("cruiserInvasionLimit") ?? (int?)CommonSettingsXml.Element("cruiserInvasionLimit") ?? 0;

                        // if this number of Cruisers lands on grid while in a mission we will enter panic
                        FrigateInvasionLimit = (int?)CharacterSettingsXml.Element("frigateInvasionLimit") ?? (int?)CommonSettingsXml.Element("frigateInvasionLimit") ?? 0;

                        // if this number of Frigates lands on grid while in a mission we will enter panic
                        InvasionRandomDelay = (int?)CharacterSettingsXml.Element("invasionRandomDelay") ?? (int?)CommonSettingsXml.Element("invasionRandomDelay") ?? 0; // random relay to stay docked
                        InvasionMinimumDelay = (int?)CharacterSettingsXml.Element("invasionMinimumDelay") ?? (int?)CommonSettingsXml.Element("invasionMinimumDelay") ?? 0;

                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Invasion Settings: Exception [" + exception + "]", Logging.Teal);
                    }

                    // minimum delay to stay docked

                    //
                    // Value - Used in calculations
                    //
                    Statistics.IskPerLP = (double?)CharacterSettingsXml.Element("IskPerLP") ?? (double?)CommonSettingsXml.Element("IskPerLP") ?? 600; //used in value calculations

                    //
                    // Undock settings
                    //
                    UndockBookmarkPrefix = (string)CharacterSettingsXml.Element("undockprefix") ?? (string)CommonSettingsXml.Element("undockprefix") ?? (string)CharacterSettingsXml.Element("bookmarkWarpOut") ?? (string)CommonSettingsXml.Element("bookmarkWarpOut") ?? "";

                    //
                    // Ship Names
                    //
                    try
                    {
                        Combat.CombatShipName = (string)CharacterSettingsXml.Element("combatShipName") ?? (string)CommonSettingsXml.Element("combatShipName") ?? "My frigate of doom";
                        SalvageShipName = (string)CharacterSettingsXml.Element("salvageShipName") ?? (string)CommonSettingsXml.Element("salvageShipName") ?? "My Destroyer of salvage";
                        TransportShipName = (string)CharacterSettingsXml.Element("transportShipName") ?? (string)CommonSettingsXml.Element("transportShipName") ?? "My Hauler of transportation";
                        TravelShipName = (string)CharacterSettingsXml.Element("travelShipName") ?? (string)CommonSettingsXml.Element("travelShipName") ?? "My Shuttle of traveling";
                        MiningShipName = (string)CharacterSettingsXml.Element("miningShipName") ?? (string)CommonSettingsXml.Element("miningShipName") ?? "My Exhumer of Destruction";
                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Ship Name Settings [" + exception + "]", Logging.Teal);
                    }

                    try
                    {
                        //
                        // Storage Location for Loot, Ammo, Bookmarks
                        //
                        UseHomebookmark = (bool?)CharacterSettingsXml.Element("UseHomebookmark") ?? (bool?)CommonSettingsXml.Element("UseHomebookmark") ?? false;
                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading UseHomebookmark [" + exception + "]", Logging.Teal);
                    }

                    //
                    // Storage Location for Loot, Ammo, Bookmarks
                    //
                    try
                    {

                        HomeBookmarkName = (string)CharacterSettingsXml.Element("homeBookmarkName") ?? (string)CommonSettingsXml.Element("homeBookmarkName") ?? "myHomeBookmark";
                        LootHangarTabName = (string)CharacterSettingsXml.Element("lootHangar") ?? (string)CommonSettingsXml.Element("lootHangar");
                        if (string.IsNullOrEmpty(QMSettings.Instance.LootHangarTabName))
                        {
                            Logging.Log("Settings", "LootHangar [" + "ItemsHangar" + "]", Logging.White);
                        }
                        else
                        {
                            Logging.Log("Settings", "LootHangar [" + QMSettings.Instance.LootHangarTabName + "]", Logging.White);
                        }
                        AmmoHangarTabName = (string)CharacterSettingsXml.Element("ammoHangar") ?? (string)CommonSettingsXml.Element("ammoHangar");
                        if (string.IsNullOrEmpty(QMSettings.Instance.AmmoHangarTabName))
                        {
                            Logging.Log("Settings", "AmmoHangar [" + "ItemHangar" + "]", Logging.White);
                        }
                        else
                        {
                            Logging.Log("Settings", "AmmoHangar [" + QMSettings.Instance.AmmoHangarTabName + "]", Logging.White);
                        }
                        BookmarkHangar = (string)CharacterSettingsXml.Element("bookmarkHangar") ?? (string)CommonSettingsXml.Element("bookmarkHangar");
                        LootContainerName = (string)CharacterSettingsXml.Element("lootContainer") ?? (string)CommonSettingsXml.Element("lootContainer");
                        if (LootContainerName != null)
                        {
                            LootContainerName = LootContainerName.ToLower();
                        }
                        HighTierLootContainer = (string)CharacterSettingsXml.Element("highValueLootContainer") ?? (string)CommonSettingsXml.Element("highValueLootContainer");
                        if (HighTierLootContainer != null)
                        {
                            HighTierLootContainer = HighTierLootContainer.ToLower();
                        }
                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Hangar Settings [" + exception + "]", Logging.Teal);
                    }

                    //
                    // Loot and Salvage Settings
                    //
                    try
                    {

                        Salvage.LootEverything = (bool?)CharacterSettingsXml.Element("lootEverything") ?? (bool?)CommonSettingsXml.Element("lootEverything") ?? true;
                        Salvage.UseGatesInSalvage = (bool?)CharacterSettingsXml.Element("useGatesInSalvage") ?? (bool?)CommonSettingsXml.Element("useGatesInSalvage") ?? false;

                        // if our mission does not DeSpawn (likely someone in the mission looting our stuff?) use the gates when salvaging to get to our bookmarks
                        Salvage.CreateSalvageBookmarks = (bool?)CharacterSettingsXml.Element("createSalvageBookmarks") ?? (bool?)CommonSettingsXml.Element("createSalvageBookmarks") ?? false;
                        Salvage.CreateSalvageBookmarksIn = (string)CharacterSettingsXml.Element("createSalvageBookmarksIn") ?? (string)CommonSettingsXml.Element("createSalvageBookmarksIn") ?? "Player";

                        //Player or Corp
                        //other setting is "Corp"
                        BookmarkPrefix = (string)CharacterSettingsXml.Element("bookmarkPrefix") ?? (string)CommonSettingsXml.Element("bookmarkPrefix") ?? "Salvage:";
                        SafeSpotBookmarkPrefix = (string)CharacterSettingsXml.Element("safeSpotBookmarkPrefix") ?? (string)CommonSettingsXml.Element("safeSpotBookmarkPrefix") ?? "safespot";
                        BookmarkFolder = (string)CharacterSettingsXml.Element("bookmarkFolder") ?? (string)CommonSettingsXml.Element("bookmarkFolder") ?? "Salvage:";
                        TravelToBookmarkPrefix = (string)CharacterSettingsXml.Element("travelToBookmarkPrefix") ?? (string)CommonSettingsXml.Element("travelToBookmarkPrefix") ?? "MeetHere:";
                        Salvage.MinimumWreckCount = (int?)CharacterSettingsXml.Element("minimumWreckCount") ?? (int?)CommonSettingsXml.Element("minimumWreckCount") ?? 1;
                        Salvage.AfterMissionSalvaging = (bool?)CharacterSettingsXml.Element("afterMissionSalvaging") ?? (bool?)CommonSettingsXml.Element("afterMissionSalvaging") ?? false;
                        Salvage.FirstSalvageBookmarksInSystem = (bool?)CharacterSettingsXml.Element("FirstSalvageBookmarksInSystem") ?? (bool?)CommonSettingsXml.Element("FirstSalvageBookmarksInSystem") ?? false;
                        Salvage.SalvageMultipleMissionsinOnePass = (bool?)CharacterSettingsXml.Element("salvageMultpleMissionsinOnePass") ?? (bool?)CommonSettingsXml.Element("salvageMultpleMissionsinOnePass") ?? false;
                        Salvage.UnloadLootAtStation = (bool?)CharacterSettingsXml.Element("unloadLootAtStation") ?? (bool?)CommonSettingsXml.Element("unloadLootAtStation") ?? false;
                        Salvage.ReserveCargoCapacity = (int?)CharacterSettingsXml.Element("reserveCargoCapacity") ?? (int?)CommonSettingsXml.Element("reserveCargoCapacity") ?? 0;
                        Salvage.MaximumWreckTargets = (int?)CharacterSettingsXml.Element("maximumWreckTargets") ?? (int?)CommonSettingsXml.Element("maximumWreckTargets") ?? 0;
                        Salvage.WreckBlackListSmallWrecks = (bool?)CharacterSettingsXml.Element("WreckBlackListSmallWrecks") ?? (bool?)CommonSettingsXml.Element("WreckBlackListSmallWrecks") ?? false;
                        Salvage.WreckBlackListMediumWrecks = (bool?)CharacterSettingsXml.Element("WreckBlackListMediumWrecks") ?? (bool?)CommonSettingsXml.Element("WreckBlackListMediumWrecks") ?? false;
                        Salvage.AgeofBookmarksForSalvageBehavior = (int?)CharacterSettingsXml.Element("ageofBookmarksForSalvageBehavior") ?? (int?)CommonSettingsXml.Element("ageofBookmarksForSalvageBehavior") ?? 45;
                        Salvage.AgeofSalvageBookmarksToExpire = (int?)CharacterSettingsXml.Element("ageofSalvageBookmarksToExpire") ?? (int?)CommonSettingsXml.Element("ageofSalvageBookmarksToExpire") ?? 120;
                        Salvage.LootOnlyWhatYouCanWithoutSlowingDownMissionCompletion = (bool?)CharacterSettingsXml.Element("lootOnlyWhatYouCanWithoutSlowingDownMissionCompletion") ?? (bool?)CommonSettingsXml.Element("lootOnlyWhatYouCanWithoutSlowingDownMissionCompletion") ?? false;
                        Salvage.TractorBeamMinimumCapacitor = (int?)CharacterSettingsXml.Element("tractorBeamMinimumCapacitor") ?? (int?)CommonSettingsXml.Element("tractorBeamMinimumCapacitor") ?? 0;
                        Salvage.SalvagerMinimumCapacitor = (int?)CharacterSettingsXml.Element("salvagerMinimumCapacitor") ?? (int?)CommonSettingsXml.Element("salvagerMinimumCapacitor") ?? 0;
                        Salvage.DoNotDoANYSalvagingOutsideMissionActions = (bool?)CharacterSettingsXml.Element("doNotDoANYSalvagingOutsideMissionActions") ?? (bool?)CommonSettingsXml.Element("doNotDoANYSalvagingOutsideMissionActions") ?? false;
                        Salvage.LootItemRequiresTarget = (bool?)CharacterSettingsXml.Element("lootItemRequiresTarget") ?? (bool?)CommonSettingsXml.Element("lootItemRequiresTarget") ?? false;
                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Loot and Salvage Settings [" + exception + "]", Logging.Teal);
                    }

                    //
                    // Weapon and targeting Settings
                    //
                    try
                    {
                        MissionSettings.DefaultDamageType = (DamageType)Enum.Parse(typeof(DamageType), (string)CharacterSettingsXml.Element("defaultDamageType") ?? (string)CommonSettingsXml.Element("defaultDamageType") ?? "EM", true);
                        Combat.WeaponGroupId = (int?)CharacterSettingsXml.Element("weaponGroupId") ?? (int?)CommonSettingsXml.Element("weaponGroupId") ?? 0;
                        Combat.DontShootFrigatesWithSiegeorAutoCannons = (bool?)CharacterSettingsXml.Element("DontShootFrigatesWithSiegeorAutoCannons") ?? (bool?)CommonSettingsXml.Element("DontShootFrigatesWithSiegeorAutoCannons") ?? false;
                        Combat.maxHighValueTargets = (int?)CharacterSettingsXml.Element("maximumHighValueTargets") ?? (int?)CommonSettingsXml.Element("maximumHighValueTargets") ?? 2;
                        Combat.maxLowValueTargets = (int?)CharacterSettingsXml.Element("maximumLowValueTargets") ?? (int?)CommonSettingsXml.Element("maximumLowValueTargets") ?? 2;
                        Combat.DoNotSwitchTargetsIfTargetHasMoreThanThisArmorDamagePercentage = (int?)CharacterSettingsXml.Element("doNotSwitchTargetsIfTargetHasMoreThanThisArmorDamagePercentage") ?? (int?)CommonSettingsXml.Element("doNotSwitchTargetsIfTargetHasMoreThanThisArmorDamagePercentage") ?? 60;
                        Combat.DistanceNPCFrigatesShouldBeIgnoredByPrimaryWeapons = (int?)CharacterSettingsXml.Element("distanceNPCFrigatesShouldBeIgnoredByPrimaryWeapons") ?? (int?)CommonSettingsXml.Element("distanceNPCFrigatesShouldBeIgnoredByPrimaryWeapons") ?? 7000; //also requires SpeedFrigatesShouldBeIgnoredByMainWeapons
                        Combat.SpeedNPCFrigatesShouldBeIgnoredByPrimaryWeapons = (int?)CharacterSettingsXml.Element("speedNPCFrigatesShouldBeIgnoredByPrimaryWeapons") ?? (int?)CommonSettingsXml.Element("speedNPCFrigatesShouldBeIgnoredByPrimaryWeapons") ?? 300; //also requires DistanceFrigatesShouldBeIgnoredByMainWeapons
                        Arm.ArmLoadCapBoosters = (bool?)CharacterSettingsXml.Element("armLoadCapBoosters") ?? (bool?)CommonSettingsXml.Element("armLoadCapBoosters") ?? false;
                        Combat.SelectAmmoToUseBasedOnShipSize = (bool?)CharacterSettingsXml.Element("selectAmmoToUseBasedOnShipSize") ?? (bool?)CommonSettingsXml.Element("selectAmmoToUseBasedOnShipSize") ?? false;

                        Combat.MinimumTargetValueToConsiderTargetAHighValueTarget = (int?)CharacterSettingsXml.Element("minimumTargetValueToConsiderTargetAHighValueTarget") ?? (int?)CommonSettingsXml.Element("minimumTargetValueToConsiderTargetAHighValueTarget") ?? 2;
                        Combat.MaximumTargetValueToConsiderTargetALowValueTarget = (int?)CharacterSettingsXml.Element("maximumTargetValueToConsiderTargetALowValueTarget") ?? (int?)CommonSettingsXml.Element("maximumTargetValueToConsiderTargetALowValueTarget") ?? 1;

                        Combat.AddDampenersToPrimaryWeaponsPriorityTargetList = (bool?)CharacterSettingsXml.Element("addDampenersToPrimaryWeaponsPriorityTargetList") ?? (bool?)CommonSettingsXml.Element("addDampenersToPrimaryWeaponsPriorityTargetList") ?? true;
                        Combat.AddECMsToPrimaryWeaponsPriorityTargetList = (bool?)CharacterSettingsXml.Element("addECMsToPrimaryWeaponsPriorityTargetList") ?? (bool?)CommonSettingsXml.Element("addECMsToPrimaryWeaponsPriorityTargetList") ?? true;
                        Combat.AddNeutralizersToPrimaryWeaponsPriorityTargetList = (bool?)CharacterSettingsXml.Element("addNeutralizersToPrimaryWeaponsPriorityTargetList") ?? (bool?)CommonSettingsXml.Element("addNeutralizersToPrimaryWeaponsPriorityTargetList") ?? true;
                        Combat.AddTargetPaintersToPrimaryWeaponsPriorityTargetList = (bool?)CharacterSettingsXml.Element("addTargetPaintersToPrimaryWeaponsPriorityTargetList") ?? (bool?)CommonSettingsXml.Element("addTargetPaintersToPrimaryWeaponsPriorityTargetList") ?? true;
                        Combat.AddTrackingDisruptorsToPrimaryWeaponsPriorityTargetList = (bool?)CharacterSettingsXml.Element("addTrackingDisruptorsToPrimaryWeaponsPriorityTargetList") ?? (bool?)CommonSettingsXml.Element("addTrackingDisruptorsToPrimaryWeaponsPriorityTargetList") ?? true;
                        Combat.AddWarpScramblersToPrimaryWeaponsPriorityTargetList = (bool?)CharacterSettingsXml.Element("addWarpScramblersToPrimaryWeaponsPriorityTargetList") ?? (bool?)CommonSettingsXml.Element("addWarpScramblersToPrimaryWeaponsPriorityTargetList") ?? true;
                        Combat.AddWebifiersToPrimaryWeaponsPriorityTargetList = (bool?)CharacterSettingsXml.Element("addWebifiersToPrimaryWeaponsPriorityTargetList") ?? (bool?)CommonSettingsXml.Element("addWebifiersToPrimaryWeaponsPriorityTargetList") ?? true;

                        Drones.AddDampenersToDronePriorityTargetList = (bool?)CharacterSettingsXml.Element("addDampenersToDronePriorityTargetList") ?? (bool?)CommonSettingsXml.Element("addDampenersToDronePriorityTargetList") ?? true;
                        Drones.AddECMsToDroneTargetList = (bool?)CharacterSettingsXml.Element("addECMsToDroneTargetList") ?? (bool?)CommonSettingsXml.Element("addECMsToDroneTargetList") ?? true;
                        Drones.AddNeutralizersToDronePriorityTargetList = (bool?)CharacterSettingsXml.Element("addNeutralizersToDronePriorityTargetList") ?? (bool?)CommonSettingsXml.Element("addNeutralizersToDronePriorityTargetList") ?? true;
                        Drones.AddTargetPaintersToDronePriorityTargetList = (bool?)CharacterSettingsXml.Element("addTargetPaintersToDronePriorityTargetList") ?? (bool?)CommonSettingsXml.Element("addTargetPaintersToDronePriorityTargetList") ?? true;
                        Drones.AddTrackingDisruptorsToDronePriorityTargetList = (bool?)CharacterSettingsXml.Element("addTrackingDisruptorsToDronePriorityTargetList") ?? (bool?)CommonSettingsXml.Element("addTrackingDisruptorsToDronePriorityTargetList") ?? true;
                        Drones.AddWarpScramblersToDronePriorityTargetList = (bool?)CharacterSettingsXml.Element("addWarpScramblersToDronePriorityTargetList") ?? (bool?)CommonSettingsXml.Element("addWarpScramblersToDronePriorityTargetList") ?? true;
                        Drones.AddWebifiersToDronePriorityTargetList = (bool?)CharacterSettingsXml.Element("addWebifiersToDronePriorityTargetList") ?? (bool?)CommonSettingsXml.Element("addWebifiersToDronePriorityTargetList") ?? true;

                        Combat.ListPriorityTargetsEveryXSeconds = (double?)CharacterSettingsXml.Element("listPriorityTargetsEveryXSeconds") ?? (double?)CommonSettingsXml.Element("listPriorityTargetsEveryXSeconds") ?? 900;

                        Combat.InsideThisRangeIsHardToTrack = (double?)CharacterSettingsXml.Element("insideThisRangeIsHardToTrack") ?? (double?)CommonSettingsXml.Element("insideThisRangeIsHardToTrack") ?? 15000;

                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Weapon and targeting Settings [" + exception + "]", Logging.Teal);
                    }

                    //
                    // Script and Booster Settings - TypeIDs for the scripts you would like to use in these modules
                    //
                    try
                    {
                        // 29003 Focused Warp Disruption Script   // hictor and InfiniPoint
                        //
                        // 29007 Tracking Speed Disruption Script // tracking disruptor
                        // 29005 Optimal Range Disruption Script  // tracking disruptor
                        // 29011 Scan Resolution Script           // sensor booster
                        // 29009 Targeting Range Script           // sensor booster
                        // 29015 Targeting Range Dampening Script // sensor dampener
                        // 29013 Scan Resolution Dampening Script // sensor dampener
                        // 29001 Tracking Speed Script            // tracking enhancer and tracking computer
                        // 28999 Optimal Range Script             // tracking enhancer and tracking computer

                        // 3554  Cap Booster 100
                        // 11283 Cap Booster 150
                        // 11285 Cap Booster 200
                        // 263   Cap Booster 25
                        // 11287 Cap Booster 400
                        // 264   Cap Booster 50
                        // 3552  Cap Booster 75
                        // 11289 Cap Booster 800
                        // 31982 Navy Cap Booster 100
                        // 31990 Navy Cap Booster 150
                        // 31998 Navy Cap Booster 200
                        // 32006 Navy Cap Booster 400
                        // 32014 Navy Cap Booster 800

                        TrackingDisruptorScript = (int?)CharacterSettingsXml.Element("trackingDisruptorScript") ?? (int?)CommonSettingsXml.Element("trackingDisruptorScript") ?? (int)TypeID.TrackingSpeedDisruptionScript;
                        TrackingComputerScript = (int?)CharacterSettingsXml.Element("trackingComputerScript") ?? (int?)CommonSettingsXml.Element("trackingComputerScript") ?? (int)TypeID.TrackingSpeedScript;
                        TrackingLinkScript = (int?)CharacterSettingsXml.Element("trackingLinkScript") ?? (int?)CommonSettingsXml.Element("trackingLinkScript") ?? (int)TypeID.TrackingSpeedScript;
                        SensorBoosterScript = (int?)CharacterSettingsXml.Element("sensorBoosterScript") ?? (int?)CommonSettingsXml.Element("sensorBoosterScript") ?? (int)TypeID.TargetingRangeScript;
                        SensorDampenerScript = (int?)CharacterSettingsXml.Element("sensorDampenerScript") ?? (int?)CommonSettingsXml.Element("sensorDampenerScript") ?? (int)TypeID.TargetingRangeDampeningScript;
                        AncillaryShieldBoosterScript = (int?)CharacterSettingsXml.Element("ancillaryShieldBoosterScript") ?? (int?)CommonSettingsXml.Element("ancillaryShieldBoosterScript") ?? (int)TypeID.AncillaryShieldBoosterScript;
                        CapacitorInjectorScript = (int?)CharacterSettingsXml.Element("capacitorInjectorScript") ?? (int?)CommonSettingsXml.Element("capacitorInjectorScript") ?? (int)TypeID.CapacitorInjectorScript;
                        NumberOfCapBoostersToLoad = (int?)CharacterSettingsXml.Element("capacitorInjectorToLoad") ?? (int?)CommonSettingsXml.Element("capacitorInjectorToLoad") ?? (int?)CharacterSettingsXml.Element("capBoosterToLoad") ?? (int?)CommonSettingsXml.Element("capBoosterToLoad") ?? 15;

                        //
                        // OverLoad Settings (this WILL burn out modules, likely very quickly!
                        // If you enable the overloading of a slot it is HIGHLY recommended you actually have something overloadable in that slot =/
                        //
                        OverloadWeapons = (bool?)CharacterSettingsXml.Element("overloadWeapons") ?? (bool?)CommonSettingsXml.Element("overloadWeapons") ?? false;

                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Script and Booster Settings [" + exception + "]", Logging.Teal);
                    }

                    //
                    // Speed and Movement Settings
                    //
                    try
                    {
                        NavigateOnGrid.AvoidBumpingThingsBool = (bool?)CharacterSettingsXml.Element("avoidBumpingThings") ?? (bool?)CommonSettingsXml.Element("avoidBumpingThings") ?? true;
                        NavigateOnGrid.SpeedTank = (bool?)CharacterSettingsXml.Element("speedTank") ?? (bool?)CommonSettingsXml.Element("speedTank") ?? false;
                        NavigateOnGrid.OrbitDistance = (int?)CharacterSettingsXml.Element("orbitDistance") ?? (int?)CommonSettingsXml.Element("orbitDistance") ?? 0;
                        NavigateOnGrid.OrbitStructure = (bool?)CharacterSettingsXml.Element("orbitStructure") ?? (bool?)CommonSettingsXml.Element("orbitStructure") ?? false;
                        NavigateOnGrid.OptimalRange = (int?)CharacterSettingsXml.Element("optimalRange") ?? (int?)CommonSettingsXml.Element("optimalRange") ?? 0;
                        Combat.NosDistance = (int?)CharacterSettingsXml.Element("NosDistance") ?? (int?)CommonSettingsXml.Element("NosDistance") ?? 38000;
                        Combat.RemoteRepairDistance = (int?)CharacterSettingsXml.Element("remoteRepairDistance") ?? (int?)CommonSettingsXml.Element("remoteRepairDistance") ?? 2000;
                        Defense.MinimumPropulsionModuleDistance = (int?)CharacterSettingsXml.Element("minimumPropulsionModuleDistance") ?? (int?)CommonSettingsXml.Element("minimumPropulsionModuleDistance") ?? 5000;
                        Defense.MinimumPropulsionModuleCapacitor = (int?)CharacterSettingsXml.Element("minimumPropulsionModuleCapacitor") ?? (int?)CommonSettingsXml.Element("minimumPropulsionModuleCapacitor") ?? 0;

                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Speed and Movement Settings [" + exception + "]", Logging.Teal);
                    }

                    //
                    // Tanking Settings
                    //
                    try
                    {
                        Defense.ActivateRepairModulesAtThisPerc = (int?)CharacterSettingsXml.Element("activateRepairModules") ?? (int?)CommonSettingsXml.Element("activateRepairModules") ?? 65;
                        Defense.DeactivateRepairModulesAtThisPerc = (int?)CharacterSettingsXml.Element("deactivateRepairModules") ?? (int?)CommonSettingsXml.Element("deactivateRepairModules") ?? 95;
                        Defense.InjectCapPerc = (int?)CharacterSettingsXml.Element("injectcapperc") ?? (int?)CommonSettingsXml.Element("injectcapperc") ?? 60;
                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Tanking Settings [" + exception + "]", Logging.Teal);
                    }

                    //
                    // Panic Settings
                    //
                    try
                    {
                        Panic.MinimumShieldPct = (int?)CharacterSettingsXml.Element("minimumShieldPct") ?? (int?)CommonSettingsXml.Element("minimumShieldPct") ?? 100;
                        Panic.MinimumArmorPct = (int?)CharacterSettingsXml.Element("minimumArmorPct") ?? (int?)CommonSettingsXml.Element("minimumArmorPct") ?? 100;
                        Panic.MinimumCapacitorPct = (int?)CharacterSettingsXml.Element("minimumCapacitorPct") ?? (int?)CommonSettingsXml.Element("minimumCapacitorPct") ?? 50;
                        Panic.SafeShieldPct = (int?)CharacterSettingsXml.Element("safeShieldPct") ?? (int?)CommonSettingsXml.Element("safeShieldPct") ?? 90;
                        Panic.SafeArmorPct = (int?)CharacterSettingsXml.Element("safeArmorPct") ?? (int?)CommonSettingsXml.Element("safeArmorPct") ?? 90;
                        Panic.SafeCapacitorPct = (int?)CharacterSettingsXml.Element("safeCapacitorPct") ?? (int?)CommonSettingsXml.Element("safeCapacitorPct") ?? 80;
                        Panic.UseStationRepair = (bool?)CharacterSettingsXml.Element("useStationRepair") ?? (bool?)CommonSettingsXml.Element("useStationRepair") ?? true;
                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Panic Settings [" + exception + "]", Logging.Teal);
                    }

                    //
                    // Drone Settings
                    //
                    try
                    {
                        Drones.UseDrones = (bool?)CharacterSettingsXml.Element("useDrones") ?? (bool?)CommonSettingsXml.Element("useDrones") ?? true;
                        Drones.DroneTypeID = (int?)CharacterSettingsXml.Element("droneTypeId") ?? (int?)CommonSettingsXml.Element("droneTypeId") ?? 0;
                        Drones.DroneControlRange = (int?)CharacterSettingsXml.Element("droneControlRange") ?? (int?)CommonSettingsXml.Element("droneControlRange") ?? 0;
                        Drones.DronesDontNeedTargetsBecauseWehaveThemSetOnAggressive = (bool?)CharacterSettingsXml.Element("dronesDontNeedTargetsBecauseWehaveThemSetOnAggressive") ?? (bool?)CommonSettingsXml.Element("dronesDontNeedTargetsBecauseWehaveThemSetOnAggressive") ?? true;
                        Drones.DroneMinimumShieldPct = (int?)CharacterSettingsXml.Element("droneMinimumShieldPct") ?? (int?)CommonSettingsXml.Element("droneMinimumShieldPct") ?? 50;
                        Drones.DroneMinimumArmorPct = (int?)CharacterSettingsXml.Element("droneMinimumArmorPct") ?? (int?)CommonSettingsXml.Element("droneMinimumArmorPct") ?? 50;
                        Drones.DroneMinimumCapacitorPct = (int?)CharacterSettingsXml.Element("droneMinimumCapacitorPct") ?? (int?)CommonSettingsXml.Element("droneMinimumCapacitorPct") ?? 0;
                        Drones.DroneRecallShieldPct = (int?)CharacterSettingsXml.Element("droneRecallShieldPct") ?? (int?)CommonSettingsXml.Element("droneRecallShieldPct") ?? 0;
                        Drones.DroneRecallArmorPct = (int?)CharacterSettingsXml.Element("droneRecallArmorPct") ?? (int?)CommonSettingsXml.Element("droneRecallArmorPct") ?? 0;
                        Drones.DroneRecallCapacitorPct = (int?)CharacterSettingsXml.Element("droneRecallCapacitorPct") ?? (int?)CommonSettingsXml.Element("droneRecallCapacitorPct") ?? 0;
                        Drones.LongRangeDroneRecallShieldPct = (int?)CharacterSettingsXml.Element("longRangeDroneRecallShieldPct") ?? (int?)CommonSettingsXml.Element("longRangeDroneRecallShieldPct") ?? 0;
                        Drones.LongRangeDroneRecallArmorPct = (int?)CharacterSettingsXml.Element("longRangeDroneRecallArmorPct") ?? (int?)CommonSettingsXml.Element("longRangeDroneRecallArmorPct") ?? 0;
                        Drones.LongRangeDroneRecallCapacitorPct = (int?)CharacterSettingsXml.Element("longRangeDroneRecallCapacitorPct") ?? (int?)CommonSettingsXml.Element("longRangeDroneRecallCapacitorPct") ?? 0;
                        Drones.DronesKillHighValueTargets = (bool?)CharacterSettingsXml.Element("dronesKillHighValueTargets") ?? (bool?)CommonSettingsXml.Element("dronesKillHighValueTargets") ?? false;
                        Drones.BelowThisHealthLevelRemoveFromDroneBay = (int?)CharacterSettingsXml.Element("belowThisHealthLevelRemoveFromDroneBay") ?? (int?)CommonSettingsXml.Element("belowThisHealthLevelRemoveFromDroneBay") ?? 150;
                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Drone Settings [" + exception + "]", Logging.Teal);
                    }

                    //
                    // Ammo settings
                    //
                    try
                    {
                        Combat.Ammo.Clear();
                        XElement ammoTypes = CharacterSettingsXml.Element("ammoTypes") ?? CommonSettingsXml.Element("ammoTypes");

                        if (ammoTypes != null)
                        {
                            foreach (XElement ammo in ammoTypes.Elements("ammoType"))
                            {
                                Combat.Ammo.Add(new Ammo(ammo));
                            }
                        }

                        Combat.MinimumAmmoCharges = (int?)CharacterSettingsXml.Element("minimumAmmoCharges") ?? (int?)CommonSettingsXml.Element("minimumAmmoCharges") ?? 2;
                        if (Combat.MinimumAmmoCharges < 2) Combat.MinimumAmmoCharges = 2; //do not allow MinimumAmmoCharges to be set lower than 1. We always want to reload before the weapon is empty!

                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Ammo Settings [" + exception + "]", Logging.Teal);
                    }

                    //
                    // List of Agents we should use
                    //
                    try
                    {
                        //if (QMSettings.Instance.CharacterMode.ToLower() == "Combat Missions".ToLower())
                        //{
                        MissionSettings.ListOfAgents.Clear();
                        XElement agentList = CharacterSettingsXml.Element("agentsList") ?? CommonSettingsXml.Element("agentsList");

                        if (agentList != null)
                        {
                            if (agentList.HasElements)
                            {
                                foreach (XElement agent in agentList.Elements("agentList"))
                                {
                                    MissionSettings.ListOfAgents.Add(new AgentsList(agent));
                                }
                            }
                            else
                            {
                                Logging.Log("Settings", "agentList exists in your characters config but no agents were listed.", Logging.Red);
                            }
                        }
                        else
                        {
                            Logging.Log("Settings", "Error! No Agents List specified.", Logging.Red);
                        }

                        //}
                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Agent Settings [" + exception + "]", Logging.Teal);
                    }

                    //
                    // Loading Mission Blacklists/GreyLists
                    //
                    try
                    {
                        MissionSettings.LoadMissionBlackList(CharacterSettingsXml, CommonSettingsXml);
                        MissionSettings.LoadMissionGreyList(CharacterSettingsXml, CommonSettingsXml);
                        MissionSettings.LoadFactionBlacklist(CharacterSettingsXml, CommonSettingsXml);
                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading Mission Blacklists/GreyLists [" + exception + "]", Logging.Teal);
                    }

                    //
                    // agent standing requirements
                    //
                    try
                    {
                        AgentInteraction.StandingsNeededToAccessLevel1Agent = (float?)CharacterSettingsXml.Element("standingsNeededToAccessLevel1Agent") ?? (float?)CommonSettingsXml.Element("standingsNeededToAccessLevel1Agent") ?? -11;
                        AgentInteraction.StandingsNeededToAccessLevel2Agent = (float?)CharacterSettingsXml.Element("standingsNeededToAccessLevel2Agent") ?? (float?)CommonSettingsXml.Element("standingsNeededToAccessLevel2Agent") ?? 1;
                        AgentInteraction.StandingsNeededToAccessLevel3Agent = (float?)CharacterSettingsXml.Element("standingsNeededToAccessLevel3Agent") ?? (float?)CommonSettingsXml.Element("standingsNeededToAccessLevel3Agent") ?? 3;
                        AgentInteraction.StandingsNeededToAccessLevel4Agent = (float?)CharacterSettingsXml.Element("standingsNeededToAccessLevel4Agent") ?? (float?)CommonSettingsXml.Element("standingsNeededToAccessLevel4Agent") ?? 5;
                        AgentInteraction.StandingsNeededToAccessLevel5Agent = (float?)CharacterSettingsXml.Element("standingsNeededToAccessLevel5Agent") ?? (float?)CommonSettingsXml.Element("standingsNeededToAccessLevel5Agent") ?? 7;

                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Settings", "Error Loading AgentStandings requirements [" + exception + "]", Logging.Teal);
                    }

                    //
                    // Skill Training Settings
                    //
                    ThisToonShouldBeTrainingSkills = (bool?)CharacterSettingsXml.Element("thisToonShouldBeTrainingSkills") ?? (bool?)CommonSettingsXml.Element("thisToonShouldBeTrainingSkills") ?? true;
                //}

                //
                // Location of the Questor GUI on startup (default is off the screen)
                //
                //X Questor GUI window position (needs to be changed, default is off screen)
                WindowXPosition = (int?)CharacterSettingsXml.Element("windowXPosition") ?? (int?)CommonSettingsXml.Element("windowXPosition") ?? 1;

                //Y Questor GUI window position (needs to be changed, default is off screen)
                WindowYPosition = (int?)CharacterSettingsXml.Element("windowYPosition") ?? (int?)CommonSettingsXml.Element("windowYPosition") ?? 1;

                //
                // Location of the EVE Window on startup (default is to leave the window alone)
                //
                try
                {
                    //EVE Client window position
                    EVEWindowXPosition = (int?)CharacterSettingsXml.Element("eveWindowXPosition") ?? (int?)CommonSettingsXml.Element("eveWindowXPosition") ?? 0;

                    //EVE Client window position
                    EVEWindowYPosition = (int?)CharacterSettingsXml.Element("eveWindowYPosition") ?? (int?)CommonSettingsXml.Element("eveWindowYPosition") ?? 0;

                    //
                    // Size of the EVE Window on startup (default is to leave the window alone)
                    // This CAN and WILL distort the proportions of the EVE client if you configure it to do so.
                    // ISBOXER arguably does this with more elegance...
                    //
                    //EVE Client window position
                    EVEWindowXSize = (int?)CharacterSettingsXml.Element("eveWindowXSize") ?? (int?)CommonSettingsXml.Element("eveWindowXSize") ?? 0;

                    //EVE Client window position
                    EVEWindowYSize = (int?)CharacterSettingsXml.Element("eveWindowYSize") ?? (int?)CommonSettingsXml.Element("eveWindowYSize") ?? 0;
                }
                catch
                {
                    Logging.Log("Settings", "Invalid Format for eveWindow Settings - skipping", Logging.Teal);
                }

                //
                // at what memory usage do we need to restart this session?
                //
                EVEProcessMemoryCeiling = (int?)CharacterSettingsXml.Element("EVEProcessMemoryCeiling") ?? (int?)CommonSettingsXml.Element("EVEProcessMemoryCeiling") ?? 2048;

                CloseQuestorCMDUplinkInnerspaceProfile = (bool?)CharacterSettingsXml.Element("CloseQuestorCMDUplinkInnerspaceProfile") ?? (bool?)CommonSettingsXml.Element("CloseQuestorCMDUplinkInnerspaceProfile") ?? true;
                CloseQuestorCMDUplinkIsboxerCharacterSet = (bool?)CharacterSettingsXml.Element("CloseQuestorCMDUplinkIsboxerCharacterSet") ?? (bool?)CommonSettingsXml.Element("CloseQuestorCMDUplinkIsboxerCharacterSet") ?? false;
                CloseQuestorAllowRestart = (bool?)CharacterSettingsXml.Element("CloseQuestorAllowRestart") ?? (bool?)CommonSettingsXml.Element("CloseQuestorAllowRestart") ?? true;
                CloseQuestorArbitraryOSCmd = (bool?)CharacterSettingsXml.Element("CloseQuestorArbitraryOSCmd") ?? (bool?)CommonSettingsXml.Element("CloseQuestorArbitraryOSCmd") ?? false;

                //true or false
                CloseQuestorOSCmdContents = (string)CharacterSettingsXml.Element("CloseQuestorOSCmdContents") ?? (string)CommonSettingsXml.Element("CloseQuestorOSCmdContents") ?? "cmd /k (date /t && time /t && echo. && echo. && echo Questor is configured to use the feature: CloseQuestorArbitraryOSCmd && echo But No actual command was specified in your characters settings xml! && pause)";

                LoginQuestorArbitraryOSCmd = (bool?)CharacterSettingsXml.Element("LoginQuestorArbitraryOSCmd") ?? (bool?)CommonSettingsXml.Element("LoginQuestorArbitraryOSCmd") ?? false;

                //true or false
                LoginQuestorOSCmdContents = (string)CharacterSettingsXml.Element("LoginQuestorOSCmdContents") ?? (string)CommonSettingsXml.Element("LoginQuestorOSCmdContents") ?? "cmd /k (date /t && time /t && echo. && echo. && echo Questor is configured to use the feature: LoginQuestorArbitraryOSCmd && echo But No actual command was specified in your characters settings xml! && pause)";
                LoginQuestorLavishScriptCmd = (bool?)CharacterSettingsXml.Element("LoginQuestorLavishScriptCmd") ?? (bool?)CommonSettingsXml.Element("LoginQuestorLavishScriptCmd") ?? false;

                //true or false
                LoginQuestorLavishScriptContents = (string)CharacterSettingsXml.Element("LoginQuestorLavishScriptContents") ?? (string)CommonSettingsXml.Element("LoginQuestorLavishScriptContents") ?? "echo Questor is configured to use the feature: LoginQuestorLavishScriptCmd && echo But No actual command was specified in your characters settings xml! && pause)";

                MinimizeEveAfterStartingUp = (bool?)CharacterSettingsXml.Element("MinimizeEveAfterStartingUp") ?? (bool?)CommonSettingsXml.Element("MinimizeEveAfterStartingUp") ?? false;

                //the above setting can be set to any script or commands available on the system. make sure you test it from a command prompt while in your .net programs directory

                WalletBalanceChangeLogOffDelay = (int?)CharacterSettingsXml.Element("walletbalancechangelogoffdelay") ?? (int?)CommonSettingsXml.Element("walletbalancechangelogoffdelay") ?? 30;
                WalletBalanceChangeLogOffDelayLogoffOrExit = (string)CharacterSettingsXml.Element("walletbalancechangelogoffdelayLogofforExit") ?? (string)CommonSettingsXml.Element("walletbalancechangelogoffdelayLogofforExit") ?? "exit";

                //
                // Enable / Disable the different types of logging that are available
                //
                Logging.InnerspaceGeneratedConsoleLog = (bool?)CharacterSettingsXml.Element("innerspaceGeneratedConsoleLog") ?? (bool?)CommonSettingsXml.Element("innerspaceGeneratedConsoleLog") ?? false; // save the innerspace generated console log to file
                //Logging.SaveConsoleLog = (bool?)CharacterSettingsXml.Element("saveLog") ?? (bool?)CommonSettingsXml.Element("saveLog") ?? true; // save the console log to file
                Logging.SaveLogRedacted = (bool?)CharacterSettingsXml.Element("saveLogRedacted") ?? (bool?)CommonSettingsXml.Element("saveLogRedacted") ?? true; // save the console log redacted to file
                Statistics.SessionsLog = (bool?)CharacterSettingsXml.Element("SessionsLog") ?? (bool?)CommonSettingsXml.Element("SessionsLog") ?? true;
                Statistics.DroneStatsLog = (bool?)CharacterSettingsXml.Element("DroneStatsLog") ?? (bool?)CommonSettingsXml.Element("DroneStatsLog") ?? true;
                Statistics.WreckLootStatistics = (bool?)CharacterSettingsXml.Element("WreckLootStatistics") ?? (bool?)CommonSettingsXml.Element("WreckLootStatistics") ?? true;
                Statistics.MissionStats1Log = (bool?)CharacterSettingsXml.Element("MissionStats1Log") ?? (bool?)CommonSettingsXml.Element("MissionStats1Log") ?? true;
                Statistics.MissionStats2Log = (bool?)CharacterSettingsXml.Element("MissionStats2Log") ?? (bool?)CommonSettingsXml.Element("MissionStats2Log") ?? true;
                Statistics.MissionStats3Log = (bool?)CharacterSettingsXml.Element("MissionStats3Log") ?? (bool?)CommonSettingsXml.Element("MissionStats3Log") ?? true;
                Statistics.MissionDungeonIdLog = (bool?)CharacterSettingsXml.Element("MissionDungeonIdLog") ?? (bool?)CommonSettingsXml.Element("MissionDungeonIdLog") ?? true;
                Statistics.PocketStatistics = (bool?)CharacterSettingsXml.Element("PocketStatistics") ?? (bool?)CommonSettingsXml.Element("PocketStatistics") ?? true;
                Statistics.PocketStatsUseIndividualFilesPerPocket = (bool?)CharacterSettingsXml.Element("PocketStatsUseIndividualFilesPerPocket") ?? (bool?)CommonSettingsXml.Element("PocketStatsUseIndividualFilesPerPocket") ?? true;
                Statistics.PocketObjectStatisticsLog = (bool?)CharacterSettingsXml.Element("PocketObjectStatisticsLog") ?? (bool?)CommonSettingsXml.Element("PocketObjectStatisticsLog") ?? true;
                Statistics.VolleyStatsLog = (bool?)CharacterSettingsXml.Element("VolleyStatsLog") ?? (bool?)CommonSettingsXml.Element("VolleyStatsLog") ?? true;
                Statistics.WindowStatsLog = (bool?)CharacterSettingsXml.Element("WindowStatsLog") ?? (bool?)CommonSettingsXml.Element("WindowStatsLog") ?? true;

                //
                // Email Settings
                //
                EmailSupport = (bool?)CharacterSettingsXml.Element("emailSupport") ?? (bool?)CommonSettingsXml.Element("emailSupport") ?? false;
                EmailAddress = (string)CharacterSettingsXml.Element("emailAddress") ?? (string)CommonSettingsXml.Element("emailAddress") ?? "";
                EmailPassword = (string)CharacterSettingsXml.Element("emailPassword") ?? (string)CommonSettingsXml.Element("emailPassword") ?? "";
                EmailSMTPServer = (string)CharacterSettingsXml.Element("emailSMTPServer") ?? (string)CommonSettingsXml.Element("emailSMTPServer") ?? "";
                EmailSMTPPort = (int?)CharacterSettingsXml.Element("emailSMTPPort") ?? (int?)CommonSettingsXml.Element("emailSMTPPort") ?? 25;
                EmailAddressToSendAlerts = (string)CharacterSettingsXml.Element("emailAddressToSendAlerts") ?? (string)CommonSettingsXml.Element("emailAddressToSendAlerts") ?? "";
                EmailEnableSSL = (bool?)CharacterSettingsXml.Element("emailEnableSSL") ?? (bool?)CommonSettingsXml.Element("emailEnableSSL") ?? false;

                //
                // User Defined LavishScript Scripts that tie to buttons in the UI
                //
                UserDefinedLavishScriptScript1 = (string)CharacterSettingsXml.Element("userDefinedLavishScriptScript1") ?? (string)CommonSettingsXml.Element("userDefinedLavishScriptScript1") ?? "";
                UserDefinedLavishScriptScript1Description = (string)CharacterSettingsXml.Element("userDefinedLavishScriptScript1Description") ?? (string)CommonSettingsXml.Element("userDefinedLavishScriptScript1Description") ?? "";
                UserDefinedLavishScriptScript2 = (string)CharacterSettingsXml.Element("userDefinedLavishScriptScript2") ?? (string)CommonSettingsXml.Element("userDefinedLavishScriptScript2") ?? "";
                UserDefinedLavishScriptScript2Description = (string)CharacterSettingsXml.Element("userDefinedLavishScriptScript2Description") ?? (string)CommonSettingsXml.Element("userDefinedLavishScriptScript2Description") ?? "";
                UserDefinedLavishScriptScript3 = (string)CharacterSettingsXml.Element("userDefinedLavishScriptScript3") ?? (string)CommonSettingsXml.Element("userDefinedLavishScriptScript3") ?? "";
                UserDefinedLavishScriptScript3Description = (string)CharacterSettingsXml.Element("userDefinedLavishScriptScript3Description") ?? (string)CommonSettingsXml.Element("userDefinedLavishScriptScript3Description") ?? "";
                UserDefinedLavishScriptScript4 = (string)CharacterSettingsXml.Element("userDefinedLavishScriptScript4") ?? (string)CommonSettingsXml.Element("userDefinedLavishScriptScript4") ?? "";
                UserDefinedLavishScriptScript4Description = (string)CharacterSettingsXml.Element("userDefinedLavishScriptScript4Description") ?? (string)CommonSettingsXml.Element("userDefinedLavishScriptScript4Description") ?? "";

                LoadQuestorDebugInnerspaceCommandAlias = (string)CharacterSettingsXml.Element("loadQuestorDebugInnerspaceCommandAlias") ?? (string)CommonSettingsXml.Element("loadQuestorDebugInnerspaceCommandAlias") ?? "1";
                LoadQuestorDebugInnerspaceCommand = (string)CharacterSettingsXml.Element("loadQuestorDebugInnerspaceCommand") ?? (string)CommonSettingsXml.Element("loadQuestorDebugInnerspaceCommand") ?? "dotnet q1 questor.exe";
                UnLoadQuestorDebugInnerspaceCommandAlias = (string)CharacterSettingsXml.Element("unLoadQuestorDebugInnerspaceCommandAlias") ?? (string)CommonSettingsXml.Element("unLoadQuestorDebugInnerspaceCommandAlias") ?? "2";
                UnLoadQuestorDebugInnerspaceCommand = (string)CharacterSettingsXml.Element("unLoadQuestorDebugInnerspaceCommand") ?? (string)CommonSettingsXml.Element("unLoadQuestorDebugInnerspaceCommand") ?? "dotnet -unload q1";

                //
                // number of days of console logs to keep (anything older will be deleted on startup)
                //
                Logging.ConsoleLogDaysOfLogsToKeep = (int?)CharacterSettingsXml.Element("consoleLogDaysOfLogsToKeep") ?? (int?)CommonSettingsXml.Element("consoleLogDaysOfLogsToKeep") ?? 14;
                //Logging.tryToLogToFile = (bool?)CharacterSettingsXml.Element("tryToLogToFile") ?? (bool?)CommonSettingsXml.Element("tryToLogToFile") ?? true;

                QMSettings.Instance.EVEMemoryManager = File.Exists(System.IO.Path.Combine(QMSettings.Instance.Path, "MemManager.exe")); //https://github.com/VendanAndrews/EveMemManager
                QMSettings.Instance.FactionXMLExists = File.Exists(System.IO.Path.Combine(QMSettings.Instance.Path, "faction.XML"));
                QMSettings.Instance.SchedulesXMLExists = File.Exists(System.IO.Path.Combine(QMSettings.Instance.Path, "schedules.XML"));
                QMSettings.Instance.QuestorManagerExists = File.Exists(System.IO.Path.Combine(QMSettings.Instance.Path, "QuestorManager.exe"));
                QMSettings.Instance.QuestorSettingsExists = File.Exists(System.IO.Path.Combine(QMSettings.Instance.Path, "QuestorSettings.exe"));
                QMSettings.Instance.QuestorStatisticsExists = File.Exists(System.IO.Path.Combine(QMSettings.Instance.Path, "QuestorStatistics.exe"));
            }
            catch(Exception exception)
            {
                Logging.Log("Settings", "ReadSettingsFromXML: Exception [" + exception + "]", Logging.Teal);
            }
        }

        public void LoadSettings(bool forcereload = false)
        {
            if (DateTime.UtcNow < Time.Instance.NextLoadSettings)
            {
                return;
            }

            Time.Instance.NextLoadSettings = DateTime.UtcNow.AddSeconds(15);

            try
            {
                if (Logging.MyCharacterName != null)
                {
                    QMSettings.Instance.CharacterName = Logging.MyCharacterName;
                    //Logging.Log("Settings", "CharacterName was pulled from the Scheduler: [" + QMSettings.Instance.CharacterName + "]", Logging.White);
                }
                else
                {
                    QMSettings.Instance.CharacterName = QMCache.Instance.DirectEve.Me.Name;
                    //Logging.Log("Settings", "CharacterName was pulled from your live EVE session: [" + QMSettings.Instance.CharacterName + "]", Logging.White);
                }
            }
            catch (Exception ex)
            {
                Logging.Log("Settings", "Exception trying to find CharacterName [" + ex + "]", Logging.White);
                QMSettings.Instance.CharacterName = "AtLoginScreenNoCharactersLoggedInYet";
            }

            Logging.CharacterSettingsPath = System.IO.Path.Combine(QMSettings.Instance.Path, Logging.FilterPath(QMSettings.Instance.CharacterName) + ".xml");
            //QMSettings.Instance.CommonSettingsPath = System.IO.Path.Combine(QMSettings.Instance.Path, QMSettings.Instance.CommonSettingsFileName);

            if (Logging.CharacterSettingsPath == System.IO.Path.Combine(QMSettings.Instance.Path, ".xml"))
            {
                if (DateTime.UtcNow > Time.Instance.LastSessionChange.AddSeconds(30))
                {
                    Cleanup.ReasonToStopQuestor = "CharacterName not defined! - Are we still logged in? Did we lose connection to eve? Questor should be restarting here.";
                    Logging.Log("Settings", "CharacterName not defined! - Are we still logged in? Did we lose connection to eve? Questor should be restarting here.", Logging.White);
                    QMSettings.Instance.CharacterName = "NoCharactersLoggedInAnymore";
                    Time.EnteredCloseQuestor_DateTime = DateTime.UtcNow;
                    Cleanup.SignalToQuitQuestorAndEVEAndRestartInAMoment = true;
                    _States.CurrentQuestorState = QuestorState.CloseQuestor;
                    Cleanup.CloseQuestor(Cleanup.ReasonToStopQuestor);
                    return;
                }

                Logging.Log("Settings", "CharacterName not defined! - Are we logged in yet? Did we lose connection to eve?", Logging.White);
                QMSettings.Instance.CharacterName = "AtLoginScreenNoCharactersLoggedInYet";
                //Cleanup.SignalToQuitQuestorAndEVEAndRestartInAMoment = true;
            }

            try
            {
                bool reloadSettings = true;
                if (File.Exists(Logging.CharacterSettingsPath))
                {
                    reloadSettings = _lastModifiedDateOfMySettingsFile != File.GetLastWriteTime(Logging.CharacterSettingsPath);
                    if (!reloadSettings)
                    {
                        if (File.Exists(QMSettings.Instance.CommonSettingsPath)) reloadSettings = _lastModifiedDateOfMyCommonSettingsFile != File.GetLastWriteTime(CommonSettingsPath);
                    }
                    if (!reloadSettings && forcereload) reloadSettings = true;

                    if (!reloadSettings)
                        return;
                }
            }
            catch (Exception ex)
            {
                Logging.Log("Settings", "Exception [" + ex + "]", Logging.White);
            }

            if (!File.Exists(Logging.CharacterSettingsPath) && !QMSettings.Instance.DefaultSettingsLoaded) //if the settings file does not exist initialize these values. Should we not halt when missing the settings XML?
            {
                QMSettings.Instance.CharacterXMLExists = false;
                DefaultSettingsLoaded = true;
                //LavishScript.ExecuteCommand("log " + Cache.Instance.DirectEve.Me.Name + ".log");
                //LavishScript.ExecuteCommand("uplink echo Settings: unable to find [" + QMSettings.Instance.SettingsPath + "] loading default (bad! bad! bad!) settings: you should fix this! NOW.");
                Logging.Log("Settings", "WARNING! unable to find [" + Logging.CharacterSettingsPath + "] loading default generic, and likely incorrect, settings: WARNING!", Logging.Orange);
                Logging.DebugActivateGate = false;
                Logging.DebugActivateWeapons = false;
                Logging.DebugAddDronePriorityTarget = false;
                Logging.DebugAddPrimaryWeaponPriorityTarget = false;
                Logging.DebugAgentInteractionReplyToAgent = false;
                Logging.DebugAllMissionsOnBlackList = false;
                Logging.DebugAllMissionsOnGreyList = false;
                Logging.DebugArm = false;
                Logging.DebugAttachVSDebugger = false;
                Logging.DebugAutoStart = false;
                Logging.DebugBlackList = false;
                Logging.DebugCargoHold = false;
                Logging.DebugChat = false;
                Logging.DebugCleanup = false;
                Logging.DebugClearPocket = false;
                Logging.DebugCourierMissions = false;
                Logging.DebugDecline = false;
                Logging.DebugDefense = false;
                Logging.DebugDisableCleanup = false;
                Logging.DebugDisableCombatMissionsBehavior = false;
                Logging.DebugDisableCombatMissionCtrl = false;
                Logging.DebugDisableCombat = false;
                Logging.DebugDisableDrones = false;
                Logging.DebugDisablePanic = false;
                Logging.DebugDisableSalvage = false;
                Logging.DebugDisableGetBestTarget = false;
                Logging.DebugDisableTargetCombatants = false;
                Logging.DebugDisableNavigateIntoRange = false;
                Logging.DebugDrones = false;
                Logging.DebugDroneHealth = false;
                Logging.DebugExceptions = false;
                Logging.DebugFittingMgr = false;
                Logging.DebugFleetSupportSlave = false;
                Logging.DebugFleetSupportMaster = false;
                Logging.DebugGetBestTarget = false;
                Logging.DebugGetBestDroneTarget = false;
                Logging.DebugGotobase = false;
                Logging.DebugGreyList = false;
                Logging.DebugHangars = false;
                Logging.DebugIdle = false;
                Logging.DebugInWarp = false;
                Logging.DebugItemHangar = false;
                Logging.DebugKillTargets = false;
                Logging.DebugKillAction = false;
                Logging.DebugLoadScripts = false;
                Logging.DebugLogging = false;
                Logging.DebugLootWrecks = false;
                Logging.DebugLootValue = false;
                Logging.DebugMaintainConsoleLogs = false;
                Logging.DebugMiningBehavior = false;
                Logging.DebugMissionFittings = false;
                Logging.DebugMoveTo = false;
                Logging.DebugNavigateOnGrid = false;
                Logging.DebugOnframe = false;
                Logging.DebugOverLoadWeapons = false;
                Logging.DebugPerformance = false;
                Logging.DebugPotentialCombatTargets = false;
                Logging.DebugQuestorManager = false;
                Logging.DebugReloadAll = false;
                Logging.DebugReloadorChangeAmmo = false;
                Logging.DebugRemoteRepair = false;
                Logging.DebugSalvage = false;
                Logging.DebugScheduler = false;
                Logging.DebugSettings = false;
                Logging.DebugShipTargetValues = false;
                Logging.DebugSkillTraining = true;
                Logging.DebugStatistics = false;
                Logging.DebugStorylineMissions = false;
                Logging.DebugTargetCombatants = false;
                Logging.DebugTargetWrecks = false;
                Logging.DebugTractorBeams = false;
                Logging.DebugTraveler = false;
                Logging.DebugUI = false;
                Logging.DebugUnloadLoot = false;
                Logging.DebugValuedump = false;
                Logging.DebugWalletBalance = false;
                Logging.DebugWatchForActiveWars = true;
                DetailedCurrentTargetHealthLogging = false;
                DefendWhileTraveling = true;
                //Logging.UseInnerspace = true;
                // setEveClientDestinationWhenTraveling = false;

                CharacterToAcceptInvitesFrom = QMSettings.Instance.CharacterName;
                //
                // Misc Settings
                //
                CharacterMode = "none";
                AutoStart = false; // auto Start enabled or disabled by default
                // maximum console log lines to show in the GUI
                Disable3D = false; // Disable3d graphics while in space
                RandomDelay = 15;
                MinimumDelay = 20;
                //
                // Enable / Disable Major Features that do not have categories of their own below
                //
                UseFittingManager = false;
                EnableStorylines = false;
                DeclineStorylinesInsteadofBlacklistingfortheSession = false;
                UseLocalWatch = false;
                WatchForActiveWars = true;

                FleetSupportSlave = false;
                FleetSupportMaster = false;
                FleetName = "Fleet1";
                CharacterNamesForMasterToInviteToFleet.Clear();

                // Console Log Settings
                //
                //Logging.SaveConsoleLog = true; // save the console log to file
                MaxLineConsole = 1000;
                //
                // Agent Standings and Mission Settings
                //
                MissionSettings.MinAgentBlackListStandings = 1;
                MissionSettings.MinAgentGreyListStandings = (float)-1.7;
                MissionSettings.WaitDecline = false;
                const string relativeMissionsPath = "Missions";
                MissionSettings.MissionsPath = System.IO.Path.Combine(QMSettings.Instance.Path, relativeMissionsPath);
                //Logging.Log("Settings","Default MissionXMLPath is: [" + MissionsPath + "]",Logging.White);
                MissionSettings.RequireMissionXML = false;
                MissionSettings.AllowNonStorylineCourierMissionsInLowSec = false;
                MissionSettings.MaterialsForWarOreID = 20;
                MissionSettings.MaterialsForWarOreQty = 8000;
                Combat.KillSentries = false;
                //
                // Local Watch Settings - if enabled
                //
                LocalBadStandingPilotsToTolerate = 1;
                LocalBadStandingLevelToConsiderBad = -0.1;
                //
                // Invasion Settings
                //
                BattleshipInvasionLimit = 2;
                // if this number of BattleShips lands on grid while in a mission we will enter panic
                BattlecruiserInvasionLimit = 2;
                // if this number of BattleCruisers lands on grid while in a mission we will enter panic
                CruiserInvasionLimit = 2;
                // if this number of cruisers lands on grid while in a mission we will enter panic
                FrigateInvasionLimit = 2;
                // if this number of frigates lands on grid while in a mission we will enter panic
                InvasionRandomDelay = 30; // random relay to stay docked
                InvasionMinimumDelay = 30; // minimum delay to stay docked

                //
                // Questor GUI Window Position
                //
                WindowXPosition = 400;
                WindowYPosition = 600;
                //
                // Salvage and loot settings
                //
                Salvage.ReserveCargoCapacity = 0;
                Salvage.MaximumWreckTargets = 0;

                //
                // at what memory usage do we need to restart this session?
                //
                EVEProcessMemoryCeiling = 2048;

                CloseQuestorCMDUplinkInnerspaceProfile = true;
                CloseQuestorCMDUplinkIsboxerCharacterSet = false;
                CloseQuestorAllowRestart = true;

                CloseQuestorArbitraryOSCmd = false; //true or false
                CloseQuestorOSCmdContents = string.Empty;
                //the above setting can be set to any script or commands available on the system. make sure you test it from a command prompt while in your .net programs directory

                LoginQuestorArbitraryOSCmd = false;
                LoginQuestorOSCmdContents = String.Empty;
                LoginQuestorLavishScriptCmd = false;
                LoginQuestorLavishScriptContents = string.Empty;
                MinimizeEveAfterStartingUp = false;

                WalletBalanceChangeLogOffDelay = 30;
                WalletBalanceChangeLogOffDelayLogoffOrExit = "exit";

                //
                // Value - Used in calculations
                //
                Statistics.IskPerLP = 600; //used in value calculations

                //
                // Undock settings
                //
                UndockBookmarkPrefix = "Insta";

                //
                // Location of the Questor GUI on startup (default is off the screen)
                //
                WindowXPosition = 0;

                //windows position (needs to be changed, default is off screen)
                WindowYPosition = 0;

                //windows position (needs to be changed, default is off screen)
                EVEWindowXPosition = 0;
                EVEWindowYPosition = 0;
                EVEWindowXSize = 0;
                EVEWindowYSize = 0;

                //
                // Ship Names
                //
                Combat.CombatShipName = "Raven";
                SalvageShipName = "Noctis";
                TransportShipName = "Transport";
                TravelShipName = "Travel";
                MiningShipName = "Hulk";

                //
                // Usage of HomeBookmark @ dedicated salvager
                UseHomebookmark = false;
                //
                // Storage Location for Loot, Ammo, Bookmarks
                //
                HomeBookmarkName = "myHomeBookmark";
                LootHangarTabName = String.Empty;
                AmmoHangarTabName = String.Empty;
                BookmarkHangar = String.Empty;
                LootContainerName = String.Empty;

                //
                // Loot and Salvage Settings
                //
                Salvage.LootEverything = true;
                Salvage.UseGatesInSalvage = false;
                // if our mission does not DeSpawn (likely someone in the mission looting our stuff?) use the gates when salvaging to get to our bookmarks
                Salvage.CreateSalvageBookmarks = false;
                Salvage.CreateSalvageBookmarksIn = "Player"; //Player or Corp
                //other setting is "Corp"
                BookmarkPrefix = "Salvage:";
                SafeSpotBookmarkPrefix = "safespot";
                BookmarkFolder = "Salvage";
                TravelToBookmarkPrefix = "MeetHere:";
                Salvage.MinimumWreckCount = 1;
                Salvage.AfterMissionSalvaging = false;
                Salvage.FirstSalvageBookmarksInSystem = false;
                Salvage.SalvageMultipleMissionsinOnePass = false;
                Salvage.UnloadLootAtStation = false;
                Salvage.ReserveCargoCapacity = 100;
                Salvage.MaximumWreckTargets = 0;
                Salvage.WreckBlackListSmallWrecks = false;
                Salvage.WreckBlackListMediumWrecks = false;
                Salvage.AgeofBookmarksForSalvageBehavior = 60;
                Salvage.AgeofSalvageBookmarksToExpire = 120;
                Salvage.DeleteBookmarksWithNPC = false;
                Salvage.LootOnlyWhatYouCanWithoutSlowingDownMissionCompletion = false;
                Salvage.TractorBeamMinimumCapacitor = 0;
                Salvage.SalvagerMinimumCapacitor = 0;
                Salvage.DoNotDoANYSalvagingOutsideMissionActions = false;
                Salvage.LootItemRequiresTarget = false;

                //
                // Enable / Disable the different types of logging that are available
                //
                Statistics.SessionsLog = false;
                Statistics.DroneStatsLog = false;
                Statistics.WreckLootStatistics = false;
                Statistics.MissionStats1Log = false;
                Statistics.MissionStats2Log = false;
                Statistics.MissionStats3Log = false;
                Statistics.PocketStatistics = false;
                Statistics.PocketStatsUseIndividualFilesPerPocket = false;
                Statistics.PocketObjectStatisticsLog = false;

                //
                // Weapon and targeting Settings
                //
                Combat.WeaponGroupId = 506; //cruise
                Combat.DontShootFrigatesWithSiegeorAutoCannons = false;
                Combat.maxHighValueTargets = 2;
                Combat.maxLowValueTargets = 2;
                Combat.DoNotSwitchTargetsIfTargetHasMoreThanThisArmorDamagePercentage = 60;
                Combat.DistanceNPCFrigatesShouldBeIgnoredByPrimaryWeapons = 7000; //also requires SpeedFrigatesShouldBeIgnoredByMainWeapons
                Combat.SpeedNPCFrigatesShouldBeIgnoredByPrimaryWeapons = 300; //also requires DistanceFrigatesShouldBeIgnoredByMainWeapons


                // (IsNPCBattleship) return 4;
                // (IsNPCBattlecruiser) return 3;
                // (IsNPCCruiser) return 2;
                // (IsNPCFrigate) return 0;
                Combat.MinimumTargetValueToConsiderTargetAHighValueTarget = 2;
                Combat.MaximumTargetValueToConsiderTargetALowValueTarget = 1;

                Combat.AddDampenersToPrimaryWeaponsPriorityTargetList = true;
                Combat.AddNeutralizersToPrimaryWeaponsPriorityTargetList = true;
                Combat.AddWarpScramblersToPrimaryWeaponsPriorityTargetList = true;
                Combat.AddWebifiersToPrimaryWeaponsPriorityTargetList = true;
                Combat.AddTargetPaintersToPrimaryWeaponsPriorityTargetList = true;
                Combat.AddECMsToPrimaryWeaponsPriorityTargetList = true;
                Combat.AddTrackingDisruptorsToPrimaryWeaponsPriorityTargetList = true;

                Drones.AddDampenersToDronePriorityTargetList = true;
                Drones.AddNeutralizersToDronePriorityTargetList = true;
                Drones.AddWarpScramblersToDronePriorityTargetList = true;
                Drones.AddWebifiersToDronePriorityTargetList = true;
                Drones.AddTargetPaintersToDronePriorityTargetList = true;
                Drones.AddECMsToDroneTargetList = true;
                Drones.AddTrackingDisruptorsToDronePriorityTargetList = true;

                Combat.InsideThisRangeIsHardToTrack = 15000;
                //
                // Script Settings - TypeIDs for the scripts you would like to use in these modules
                //
                // 29003 Focused Warp Disruption Script   // Hictor and InfiniPoint
                //
                // 29007 Tracking Speed Disruption Script // tracking disruptor
                // 29005 Optimal Range Disruption Script  // tracking disruptor
                // 29011 Scan Resolution Script           // sensor booster
                // 29009 Targeting Range Script           // sensor booster
                // 29015 Targeting Range Dampening Script // sensor dampener
                // 29013 Scan Resolution Dampening Script // sensor dampener
                // 29001 Tracking Speed Script            // tracking enhancer and tracking computer
                // 28999 Optimal Range Script             // tracking enhancer and tracking computer

                // 3554  Cap Booster 100
                // 11283 Cap Booster 150
                // 11285 Cap Booster 200
                // 263   Cap Booster 25
                // 11287 Cap Booster 400
                // 264   Cap Booster 50
                // 3552  Cap Booster 75
                // 11289 Cap Booster 800
                // 31982 Navy Cap Booster 100
                // 31990 Navy Cap Booster 150
                // 31998 Navy Cap Booster 200
                // 32006 Navy Cap Booster 400
                // 32014 Navy Cap Booster 800

                TrackingDisruptorScript = 29007;
                TrackingComputerScript = 29001;
                TrackingLinkScript = 29001;
                SensorBoosterScript = 29009;
                SensorDampenerScript = 29015;
                AncillaryShieldBoosterScript = 11289;
                CapacitorInjectorScript = 11289;
                NumberOfCapBoostersToLoad = 15;

                //
                // OverLoad Settings (this WILL burn out modules, likely very quickly!
                // If you enable the overloading of a slot it is HIGHLY recommended you actually have something overloadable in that slot =/
                //
                OverloadWeapons = false;

                //
                // Speed and Movement Settings
                //
                NavigateOnGrid.AvoidBumpingThingsBool = true;
                NavigateOnGrid.SpeedTank = false;
                NavigateOnGrid.OrbitDistance = 0;
                NavigateOnGrid.OrbitStructure = false;
                NavigateOnGrid.OptimalRange = 0;
                Combat.NosDistance = 38000;
                Combat.RemoteRepairDistance = 2000;
                Defense.MinimumPropulsionModuleDistance = 5000;
                Defense.MinimumPropulsionModuleCapacitor = 0;

                //
                // Tanking Settings
                //
                Defense.ActivateRepairModulesAtThisPerc = 65;
                Defense.DeactivateRepairModulesAtThisPerc = 95;
                Defense.InjectCapPerc = 60;

                //
                // Panic Settings
                //
                Panic.MinimumShieldPct = 50;
                Panic.MinimumArmorPct = 50;
                Panic.MinimumCapacitorPct = 50;
                Panic.SafeShieldPct = 0;
                Panic.SafeArmorPct = 0;
                Panic.SafeCapacitorPct = 0;
                Panic.UseStationRepair = true;

                //
                // Drone Settings
                //
                Drones.UseDrones = true;
                Drones.DroneTypeID = 2488;
                Drones.DroneControlRange = 25000;
                Drones.DroneMinimumShieldPct = 50;
                Drones.DroneMinimumArmorPct = 50;
                Drones.DroneMinimumCapacitorPct = 0;
                Drones.DroneRecallShieldPct = 0;
                Drones.DroneRecallArmorPct = 0;
                Drones.DroneRecallCapacitorPct = 0;
                Drones.LongRangeDroneRecallShieldPct = 0;
                Drones.LongRangeDroneRecallArmorPct = 0;
                Drones.LongRangeDroneRecallCapacitorPct = 0;
                Drones.DronesKillHighValueTargets = false;
                Drones.BelowThisHealthLevelRemoveFromDroneBay = 150;

                //
                // number of days of console logs to keep (anything older will be deleted on startup)
                //
                Logging.ConsoleLogDaysOfLogsToKeep = 14;

                Combat.maxHighValueTargets = 0;
                Combat.maxLowValueTargets = 0;

                //
                // Email Settings
                //
                EmailSupport = false;
                EmailAddress = "";
                EmailPassword = "";
                EmailSMTPServer = "";
                EmailSMTPPort = 25;
                EmailAddressToSendAlerts = "";
                EmailEnableSSL = false;

                //
                // Skill Training Settings
                //
                ThisToonShouldBeTrainingSkills = true;

                UserDefinedLavishScriptScript1 = "";
                UserDefinedLavishScriptScript1Description = "";
                UserDefinedLavishScriptScript2 = "";
                UserDefinedLavishScriptScript2Description = "";
                UserDefinedLavishScriptScript3 = "";
                UserDefinedLavishScriptScript3Description = "";
                UserDefinedLavishScriptScript4 = "";
                UserDefinedLavishScriptScript4Description = "";

                AgentInteraction.StandingsNeededToAccessLevel1Agent = -11;
                AgentInteraction.StandingsNeededToAccessLevel2Agent = 1;
                AgentInteraction.StandingsNeededToAccessLevel3Agent = 3;
                AgentInteraction.StandingsNeededToAccessLevel4Agent = 5;
                AgentInteraction.StandingsNeededToAccessLevel5Agent = 7;
                //
                // Clear various lists
                //
                Combat.Ammo.Clear();
                //ItemsBlackList.Clear();
                Salvage.WreckBlackList.Clear();
                MissionSettings.ListofFactionFittings.Clear();
                MissionSettings.ListOfAgents.Clear();
                MissionSettings.ListOfMissionFittings.Clear();

                //
                // Clear the Blacklist
                //
                MissionSettings.MissionBlacklist.Clear();
                MissionSettings.MissionGreylist.Clear();
                MissionSettings.FactionBlacklist.Clear();

                MissionSettings.MissionName = null;
            }
            else //if the settings file exists - load the characters settings XML
            {
                QMSettings.Instance.CharacterXMLExists = true;
                ;
                using (XmlTextReader reader = new XmlTextReader(Logging.CharacterSettingsPath))
                {
                    reader.EntityHandling = EntityHandling.ExpandEntities;
                    CharacterSettingsXml = XDocument.Load(reader).Root;
                }

                if (CharacterSettingsXml == null)
                {
                    Logging.Log("Settings", "unable to find [" + Logging.CharacterSettingsPath + "] FATAL ERROR - use the provided settings.xml to create that file.", Logging.Red);
                }
                else
                {
                    if (File.Exists(Logging.CharacterSettingsPath)) _lastModifiedDateOfMySettingsFile = File.GetLastWriteTime(Logging.CharacterSettingsPath);
                    if (File.Exists(QMSettings.Instance.CommonSettingsPath)) _lastModifiedDateOfMyCommonSettingsFile = File.GetLastWriteTime(CommonSettingsPath);
                    ReadSettingsFromXML();
                }
            }

            Statistics.SessionsLogPath = Logging.Logpath;
            Statistics.SessionsLogFile = System.IO.Path.Combine(Statistics.SessionsLogPath, Logging.characterNameForLogs + ".Sessions.log");
            Statistics.DroneStatsLogPath = Logging.Logpath;
            Statistics.DroneStatslogFile = System.IO.Path.Combine(Statistics.DroneStatsLogPath, Logging.characterNameForLogs + ".DroneStats.log");
            Statistics.VolleyStatsLogPath = System.IO.Path.Combine(Logging.Logpath, "VolleyStats\\");
            Statistics.VolleyStatslogFile = System.IO.Path.Combine(Statistics.VolleyStatsLogPath, Logging.characterNameForLogs + ".VolleyStats-DayOfYear[" + DateTime.UtcNow.DayOfYear + "].log");
            Statistics.WindowStatsLogPath = System.IO.Path.Combine(Logging.Logpath, "WindowStats\\");
            Statistics.WindowStatslogFile = System.IO.Path.Combine(Statistics.WindowStatsLogPath, Logging.characterNameForLogs + ".WindowStats-DayOfYear[" + DateTime.UtcNow.DayOfYear + "].log");
            Statistics.WreckLootStatisticsPath = Logging.Logpath;
            Statistics.WreckLootStatisticsFile = System.IO.Path.Combine(Statistics.WreckLootStatisticsPath, Logging.characterNameForLogs + ".WreckLootStatisticsDump.log");
            Statistics.MissionStats1LogPath = System.IO.Path.Combine(Logging.Logpath, "MissionStats\\");
            Statistics.MissionStats1LogFile = System.IO.Path.Combine(Statistics.MissionStats1LogPath, Logging.characterNameForLogs + ".Statistics.log");
            Statistics.MissionStats2LogPath = System.IO.Path.Combine(Logging.Logpath, "MissionStats\\");
            Statistics.MissionStats2LogFile = System.IO.Path.Combine(Statistics.MissionStats2LogPath, Logging.characterNameForLogs + ".DatedStatistics.log");
            Statistics.MissionStats3LogPath = System.IO.Path.Combine(Logging.Logpath, "MissionStats\\");
            Statistics.MissionStats3LogFile = System.IO.Path.Combine(Statistics.MissionStats3LogPath, Logging.characterNameForLogs + ".CustomDatedStatistics.csv");
            Statistics.MissionDungeonIdLogPath = System.IO.Path.Combine(Logging.Logpath, "MissionStats\\");
            Statistics.MissionDungeonIdLogFile = System.IO.Path.Combine(Statistics.MissionDungeonIdLogPath, Logging.characterNameForLogs + "Mission-DungeonId-list.csv");
            Statistics.PocketStatisticsPath = System.IO.Path.Combine(Logging.Logpath, "PocketStats\\");
            Statistics.PocketStatisticsFile = System.IO.Path.Combine(Statistics.PocketStatisticsPath, Logging.characterNameForLogs + "pocketstats-combined.csv");
            Statistics.PocketObjectStatisticsPath = System.IO.Path.Combine(Logging.Logpath, "PocketObjectStats\\");
            Statistics.PocketObjectStatisticsFile = System.IO.Path.Combine(Statistics.PocketObjectStatisticsPath, Logging.characterNameForLogs + "PocketObjectStats-combined.csv");
            Statistics.MissionDetailsHtmlPath = System.IO.Path.Combine(Logging.Logpath, "MissionDetailsHTML\\");

            try
            {
                Directory.CreateDirectory(Logging.Logpath);
                Directory.CreateDirectory(Logging.SessionDataCachePath);
                Directory.CreateDirectory(Logging.ConsoleLogPath);
                Directory.CreateDirectory(Statistics.SessionsLogPath);
                Directory.CreateDirectory(Statistics.DroneStatsLogPath);
                Directory.CreateDirectory(Statistics.WreckLootStatisticsPath);
                Directory.CreateDirectory(Statistics.MissionStats1LogPath);
                Directory.CreateDirectory(Statistics.MissionStats2LogPath);
                Directory.CreateDirectory(Statistics.MissionStats3LogPath);
                Directory.CreateDirectory(Statistics.MissionDungeonIdLogPath);
                Directory.CreateDirectory(Statistics.PocketStatisticsPath);
                Directory.CreateDirectory(Statistics.PocketObjectStatisticsPath);
                Directory.CreateDirectory(Statistics.VolleyStatsLogPath);
                Directory.CreateDirectory(Statistics.WindowStatsLogPath);
            }
            catch (Exception exception)
            {
                Logging.Log("Settings", "Problem creating directories for logs [" + exception + "]", Logging.Debug);
            }
            //create all the logging directories even if they are not configured to be used - we can adjust this later if it really bugs people to have some potentially empty directories.

            if (!QMSettings.Instance.DefaultSettingsLoaded)
            {
                if (SettingsLoaded != null)
                {
                    SettingsLoadedICount++;
                    if (QMSettings.Instance.CommonXMLExists) Logging.Log("Settings", "[" + SettingsLoadedICount + "] Done Loading Settings from [" + QMSettings.Instance.CommonSettingsPath + "] and", Logging.Green);
                    Logging.Log("Settings", "[" + SettingsLoadedICount + "] Done Loading Settings from [" + Logging.CharacterSettingsPath + "]", Logging.Green);

                    SettingsLoaded(this, new EventArgs());
                }
            }
        }

        public int RandomNumber(int min, int max)
        {
            Random random = new Random();
            return random.Next(min, max);
        }
    }
}

// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace ILEF.Lookup
{
    public class Time
    {
        private static readonly Time _instance = new Time();
        public static Time Instance
        {
            get { return _instance; }
        }
        public int LootingDelay_milliseconds = 800;                         // Delay between loot attempts
        public int WarpScrambledNoDelay_seconds = 10;                       // Time after you are no longer warp scrambled to consider it IMPORTANT That you warp soon
        public int RemoveBookmarkDelay_seconds = 5;                         // Delay between each removal of a bookmark
        public int QuestorPulseInSpace_milliseconds = 800;                 // Used to delay the next pulse, units: milliseconds. Default is 600
        public int QuestorPulseInStation_milliseconds = 800;                // Used to delay the next pulse, units: milliseconds. Default is 400
        public int DefenceDelay_milliseconds = 1500;                        // Delay between defence actions
        public int AfterburnerDelay_milliseconds = 3500;                    //
        public int RepModuleDelay_milliseconds = 2500;                      //
        public int ApproachDelay_seconds = 15;                              //
        public int TargetDelay_milliseconds = 1200;                         //
        public int TargetsAreFullDelay_seconds = 5;                         // Delay used when we have determined that all our targeting slots are full
        public int DelayBetweenSalvagingSessions_minutes = 10;              //
        public int OrbitDelay_seconds = 15;                                 // This is the delay between orbit commands, units: seconds. Default is 15
        public int DockingDelay_seconds = 17;                               // This is the delay between docking attempts, units: seconds. Default is 15
        public int WarptoDelay_seconds = 10;                                // This is the delay between warpto commands, units: seconds. Default is 10
        public int WeaponDelay_milliseconds = 220;                          //
        public int NosDelay_milliseconds = 220;                             //
        public int WebDelay_milliseconds = 220;                             //
        public int RemoteRepairerDelay_milliseconds = 220;                  //
        public int WarpDisruptorDelay_milliseconds = 220;                   //
        public int PainterDelay_milliseconds = 800;                         // This is the delay between target painter activations and should stagger the painters somewhat (purposely)
        public int ValidateSettings_seconds = 15;                           // This is the delay between character settings validation attempts. The settings will be reloaded at this interval if they have changed. Default is 15
        public int SetupLogPathDelay_seconds = 10;                          // Why is this delay here? this can likely be removed with some testing... Default is 10
        public int SessionRunningTimeUpdate_seconds = 15;                   // This is used to update the session running time counter every x seconds: default is 15 seconds
        public int WalletCheck_minutes = 1;                                 // Used to delay the next wallet balance check, units: minutes. Default is 1
        public int DelayedGotoBase_seconds = 15;                            // Delay before going back to base, usually after a disconnect / reconnect. units: seconds. Default is 15
        public int WaitforBadGuytoGoAway_minutes = 25;                      // Stay docked for this amount of time before checking local again, units: minutes. Default is 5
        public int CloseQuestorDelayBeforeExit_seconds = 20;                // Delay before closing eve, units: seconds. Default is 20
        public int QuestorBeforeLoginPulseDelay_milliseconds = 5000;        // Pulse Delay for Program.cs: Used to control the speed at which the program will retry logging in and retry checking the schedule
        public int SwitchShipsDelay_seconds = 10;                           // Switch Ships Delay before retrying, units: seconds. Default is 10
        public int SwitchShipsCheck_seconds = 5;                            // Switch Ships Check to see if ship is correct, units: seconds. Default is 7
        public int FittingWindowLoadFittingDelay_seconds = 5;               // We can ask the fitting to be loaded using the fitting window, but we cant know it is done, thus this delay, units: seconds. Default is 10
        public int WaitforItemstoMove_seconds = 1;                          // Arm state: wait for items to move, units: seconds. Default is 5
        public int CheckLocalDelay_seconds = 5;                             // Local Check for bad standings pilots, delay between checks, units: seconds. Default is 5
        public int ReloadWeaponDelayBeforeUsable_seconds = 12;              // Delay after reloading before that module is usable again (non-energy weapons), units: seconds. Default is 12
        public int BookmarkPocketRetryDelay_seconds = 20;                   // When checking to see if a bookmark needs to be made in a pocket for after mission salvaging this is the delay between retries, units: seconds. Default is 20
        public int NoGateFoundRetryDelay_seconds = 30;                      // no gate found on grid when executing the activate action, wait this long to see if it appears (lag), units: seconds. Default is 30
        public int AlignDelay_minutes = 2;                                  // Delay between the last align command and the next, units: minutes. Default is 2
        public int DelayBetweenJetcans_seconds = 185;                       // Once you have made a JetCan you cannot make another for 3 minutes, units: seconds. Default is 185 (to account for lag)
        public int SalvageStackItemsDelayBeforeResuming_seconds = 2;        // When stacking items in cargohold delay before proceeding, units: seconds. Default is 5
        public int SalvageStackItems_seconds = 150;                         // When salvaging stack items in your cargo every x seconds, units: seconds. Default is 180
        public int SalvageDelayBetweenActions_milliseconds = 500;           //
        public int MaxSalvageMinutesPerPocket = 10;                         // Max Salvage TIme per pocket before moving on to the next pocket. Default is 10 min
        public int TravelerExitStationAmIInSpaceYet_seconds = 17;           // Traveler - Exit Station before you are in space delay, units: seconds. Default is 7
        public int TravelerNoStargatesFoundRetryDelay_seconds = 15;         // Traveler could not find any StarGates, retry when this time has elapsed, units: seconds. Default is 15
        public int TravelerJumpedGateNextCommandDelay_seconds = 15;         // Traveler jumped a gate - delay before assuming we have loaded grid, units: seconds. Default is 15
        public int TravelerInWarpedNextCommandDelay_seconds = 15;           // Traveler is in warp - delay before processing another command, units: seconds. Default is 15
        public int WrecksDisappearAfter_minutes = 110;                      // used to determine how long a wreck will be in space: usually to delay salvaging until a later time, units: minutes. Default is 120 minutes (2 hours)
        public int AverageTimeToCompleteAMission_minutes = 40;              // average time for all missions, all races, all ShipTypes (guestimated)... it is used to determine when to do things like salvage. units: minutes. Default is 30
        public int AverageTimetoSalvageMultipleMissions_minutes = 40;       // average time it will take to salvage the multiple mission chain we plan on salvaging all in one go.
        public int CheckForWindows_seconds = 15;                            // Check for and deal with modal windows every x seconds, units: seconds. Default is 15
        public int ScheduleCheck_seconds = 120;                             // How often when in IDLE, we should check to see if we need to logoff / restart, this can be set to a low number, default is 120 seconds (2 minutes)
        public int ValueDumpPulse_milliseconds = 200;                       // Used to delay the next ValueDump pulse, units: milliseconds. Default is 500
        public int NoFramesRestart_seconds = 45;
        public int NoFramesReallyRestart_seconds = 90;
        public int NoSessionIsReadyRestart_seconds = 60;
        public int NoSessionIsReadyReallyRestart_seconds = 120;
        public int Marketlookupdelay_seconds = 1;
        public int Marketsellorderdelay_seconds = 2;
        public int Marketbuyorderdelay_seconds = 2;
        public int QuestorScheduleNotUsed_Hours = 10;
        public int SkillTrainerPulse_milliseconds = 800;
        public int EVEAccountLoginDelayMinimum_seconds = 10;
        public int EVEAccountLoginDelayMaximum_seconds = 16;
        public int CharacterSelectionDelayMinimum_seconds = 10;
        public int CharacterSelectionDelayMaximum_seconds = 16;
        public int ReLogDelayMinimum_seconds = 35;                          //DO NOT set this lower than 20 or so seconds!
        public int ReLogDelayMaximum_seconds = 60;
        public int RecallDronesDelayBetweenRetries = 15;                    //Time between recall commands for drones when attempting to pull drones
        public int EnforcedDelayBetweenArbitraryAmmoChanges = 60;           //do not allow changing ammo before this # of seconds, default is 60.

        //
        //
        //
        public DateTime LastAccelerationGateDetected = DateTime.UtcNow;
        public DateTime LastCloaked = DateTime.MinValue;
        public DateTime LastFrame = DateTime.UtcNow;
        public DateTime LastInStation = DateTime.MinValue;
        public DateTime LastInSpace = DateTime.MinValue;
        public DateTime LastInWarp = DateTime.UtcNow;
        public DateTime LastJettison { get; set; }
        public DateTime LastKnownGoodConnectedTime { get; set; }
        public DateTime LastLocalWatchAction = DateTime.UtcNow;
        public DateTime LastLoggingAction = DateTime.MinValue;
        public DateTime LastPreferredDroneTargetDateTime = DateTime.UtcNow;
        public DateTime LastPreferredPrimaryWeaponTargetDateTime = DateTime.UtcNow;
        public DateTime LastScheduleCheck = DateTime.UtcNow;
        public DateTime LastSessionChange = DateTime.UtcNow; 
        public DateTime LastStackAmmoHangar = DateTime.UtcNow;
        public DateTime LastStackLootHangar = DateTime.UtcNow;
        public DateTime LastStackItemHangar = DateTime.UtcNow;
        public DateTime LastOpenHangar = DateTime.UtcNow;
        public DateTime LastStackShipsHangar = DateTime.UtcNow;
        public DateTime LastStackCargohold = DateTime.UtcNow;
        public DateTime LastStackLootContainer = DateTime.UtcNow;
        public DateTime LastUpdateOfSessionRunningTime;
        public DateTime LastWalletCheck = DateTime.UtcNow;
        public DateTime LastSessionIsReady = DateTime.UtcNow;
        public DateTime LastLogMessage = DateTime.UtcNow;

        public DateTime ManualStopTime = DateTime.Now.AddHours(10);
        public DateTime ManualRestartTime = DateTime.Now.AddHours(10);
        public DateTime MissionBookmarkTimeout = DateTime.Now.AddHours(10);

        public DateTime NextCheckCorpisAtWar = DateTime.UtcNow; 
        public DateTime NextInSpaceorInStation;
        public DateTime NextTimeCheckAction = DateTime.UtcNow;
        public DateTime NextQMJobCheckAction = DateTime.UtcNow;
        public DateTime NextSalvageTrip = DateTime.UtcNow;
        public static DateTime NextClearPocketCache = DateTime.UtcNow;
        public DateTime NextLoadSettings { get; set; }
        public DateTime NextWindowAction { get; set; }
        public DateTime NextGetAgentMissionAction { get; set; }
        public DateTime NextOpenContainerInSpaceAction { get; set; }
        public DateTime NextOpenLootContainerAction { get; set; }
        public DateTime NextOpenCorpBookmarkHangarAction { get; set; }
        public DateTime NextDroneBayAction { get; set; }
        public DateTime NextOpenHangarAction { get; set; }
        public DateTime NextOpenCargoAction { get; set; }
        public DateTime NextOpenCurrentShipsCargoWindowAction { get; set; }
        public DateTime NextMakeActiveTargetAction { get; set; }
        public DateTime NextArmAction { get; set; }
        public DateTime NextSalvageAction { get; set; }
        public DateTime NextBookmarkAction { get; set; }
        public DateTime NextTractorBeamAction { get; set; }
        public DateTime NextLootAction { get; set; }
        public DateTime NextAfterburnerAction { get; set; }
        public DateTime NextRepModuleAction { get; set; }
        public DateTime NextActivateModules { get; set; }
        public DateTime NextRemoveBookmarkAction { get; set; }
        public DateTime NextApproachAction { get; set; }
        public DateTime NextOrbit { get; set; }
        public DateTime NextTravelerAction { get; set; }
        public DateTime NextTargetAction { get; set; }
        public DateTime NextActivateAction { get; set; }
        public DateTime NextBookmarkPocketAttempt { get; set; }
        public DateTime NextAlign { get; set; }
        public DateTime NextUndockAction { get; set; }
        public DateTime NextDockAction { get; set; }
        public DateTime NextJumpAction { get; set; }
        public DateTime NextWarpAction { get; set; }
        public DateTime NextDroneRecall { get; set; }
        public DateTime NextStartupAction { get; set; }
        public DateTime NextRepairItemsAction { get; set; }
        public DateTime NextRepairDronesAction { get; set; }
        public DateTime NextEVEMemoryManagerAction { get; set; }
        public DateTime NextGetBestCombatTarget { get; set; }
        public DateTime NextGetBestDroneTarget { get; set; }
        public DateTime NextSkillTrainerProcessState;
        public DateTime NextSkillTrainerAction = DateTime.MinValue;
        public DateTime NextBastionAction { get; set; }
        public DateTime NextBastionModeDeactivate { get; set; }
        public DateTime NextLPStoreAction { get; set; }
        public DateTime NextModuleDisableAutoReload { get; set; }

        public DateTime QuestorStarted_DateTime = DateTime.UtcNow;
        public DateTime LoginStarted_DateTime = DateTime.UtcNow;
        public static DateTime EnteredCloseQuestor_DateTime { get; set; }

        public DateTime StartedBoosting { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime StopTime = DateTime.Now.AddHours(10);
        public DateTime WehaveMoved = DateTime.UtcNow;
        
        public bool StopTimeSpecified = true;
        public int MaxRuntime = int.MaxValue;

        /// <summary>
        ///   Modules last reload time
        /// </summary>
        public Dictionary<long, DateTime> LastReloadedTimeStamp = new Dictionary<long, DateTime>();
        public Dictionary<long, DateTime> LastReloadAttemptTimeStamp = new Dictionary<long, DateTime>();
        

        /// <summary>
        ///   Modules last changed ammo time
        /// </summary>
        public Dictionary<long, DateTime> LastChangedAmmoTimeStamp = new Dictionary<long, DateTime>();

        /// <summary>
        ///   Modules last activated time
        /// </summary>
        public Dictionary<long, DateTime> LastActivatedTimeStamp = new Dictionary<long, DateTime>();

        /// <summary>
        ///   Modules last Click time (this is used for activating AND deactivating!)
        /// </summary>
        public Dictionary<long, DateTime> LastClickedTimeStamp = new Dictionary<long, DateTime>();

        /// <summary>
        ///   Reload time per module
        /// </summary>
        public Dictionary<long, long> ReloadTimePerModule = new Dictionary<long, long>();

        
    }
}
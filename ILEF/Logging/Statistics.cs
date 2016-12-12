
namespace ILEF.Logging
{
    using System;
    using System.Linq;
    using ILoveEVE.Framework;
    using System.IO;
    using System.Globalization;
    using System.Collections.Generic;
    using ILEF.Actions;
    using ILEF.Activities;
    using ILEF.BackgroundTasks;
    using ILEF.Caching;
    using ILEF.Combat;
    using ILEF.Lookup;
    using ILEF.States;

    public class Statistics
    {
        public StatisticsState State { get; set; }

        //private DateTime _lastStatisticsAction;
        public DateTime MissionLoggingStartedTimestamp { get; set; }

        public static DateTime StartedMission = DateTime.UtcNow;
        public static DateTime FinishedMission = DateTime.UtcNow;
        public static DateTime StartedSalvaging = DateTime.UtcNow;
        public static DateTime FinishedSalvaging = DateTime.UtcNow;
        public static DateTime StartedPocket = DateTime.UtcNow;
        public static int LootValue { get; set; }
        public static int LoyaltyPoints { get; set; }
        public static int LostDrones { get; set; }
        public static int DroneRecalls { get; set; }
        public static int AmmoConsumption { get; set; }
        public static int AmmoValue { get; set; }
        public static int MissionsThisSession { get; set; }
        public static int MissionCompletionErrors { get; set; }
        public static int OutOfDronesCount { get; set; }

        public static int AgentLPRetrievalAttempts { get; set; }

        public static bool MissionLoggingCompleted; //false
        public static bool DroneLoggingCompleted; //false
        //private bool PocketLoggingCompleted = false;
        //private bool SessionLoggingCompleted = false;

        public bool MissionLoggingStarted = true;

        public static DateTime DateTimeForLogs;

        /// <summary>
        ///   Singleton implementation
        /// </summary>
        private static readonly Statistics _instance = new Statistics();

        public static DateTime LastMissionCompletionError;

        public static bool SessionsLog { get; set; }
        public static string SessionsLogPath { get; set; }
        public static string SessionsLogFile { get; set; }
        public static bool DroneStatsLog { get; set; }
        public static string DroneStatsLogPath { get; set; }
        public static string DroneStatslogFile { get; set; }
        public static bool VolleyStatsLog { get; set; }
        public static string VolleyStatsLogPath { get; set; }
        public static string VolleyStatslogFile { get; set; }
        public static bool WindowStatsLog { get; set; }
        public static string WindowStatsLogPath { get; set; }
        public static string WindowStatslogFile { get; set; }
        public static bool WreckLootStatistics { get; set; }
        public static string WreckLootStatisticsPath { get; set; }
        public static string WreckLootStatisticsFile { get; set; }
        public static bool MissionStats1Log { get; set; }
        public static string MissionStats1LogPath { get; set; }
        public static string MissionStats1LogFile { get; set; }
        public static bool MissionStats2Log { get; set; }
        public static string MissionStats2LogPath { get; set; }
        public static string MissionStats2LogFile { get; set; }
        public static bool MissionStats3Log { get; set; }
        public static string MissionStats3LogPath { get; set; }
        public static string MissionStats3LogFile { get; set; }
        public static bool MissionDungeonIdLog { get; set; }
        public static string MissionDungeonIdLogPath { get; set; }
        public static string MissionDungeonIdLogFile { get; set; }
        public static bool PocketStatistics { get; set; }
        public static string PocketStatisticsPath { get; set; }
        public static string PocketStatisticsFile { get; set; }
        public static bool PocketObjectStatisticsBool { get; set; }
        public static string PocketObjectStatisticsPath { get; set; }
        public static string PocketObjectStatisticsFile { get; set; }
        public static string MissionDetailsHtmlPath { get; set; }
        public static bool PocketStatsUseIndividualFilesPerPocket = true;
        public static bool PocketObjectStatisticsLog { get; set; }
        public static int RepairCycleTimeThisPocket { get; set; }
        public static int PanicAttemptsThisPocket { get; set; }
        public static double LowestShieldPercentageThisMission { get; set; }
        public static double LowestArmorPercentageThisMission { get; set; }
        public static double LowestCapacitorPercentageThisMission { get; set; }
        public static double LowestShieldPercentageThisPocket { get; set; }
        public static double LowestArmorPercentageThisPocket { get; set; }
        public static double LowestCapacitorPercentageThisPocket { get; set; }
        public static int PanicAttemptsThisMission { get; set; }
        public static int RepairCycleTimeThisMission { get; set; }
        public static double SessionIskGenerated { get; set; }
        public static double SessionLootGenerated { get; set; }
        public static double SessionLPGenerated { get; set; }
        public static int SessionRunningTime { get; set; }
        public static double SessionIskPerHrGenerated { get; set; }
        public static double SessionLootPerHrGenerated { get; set; }
        public static double SessionLPPerHrGenerated { get; set; }
        public static double SessionTotalPerHrGenerated { get; set; }
        public static int TimeSpentReloading_seconds = 0;
        public static int TimeSpentInMission_seconds = 0;
        public static int TimeSpentInMissionInRange = 0;
        public static int TimeSpentInMissionOutOfRange = 0;
        public static int WrecksThisPocket;
        public static int WrecksThisMission;
        public static double IskPerLP { get; set; }

        Statistics()
        {
            Statistics.PanicAttemptsThisPocket = 0;
            Statistics.LowestShieldPercentageThisPocket = 100;
            Statistics.LowestArmorPercentageThisPocket = 100;
            Statistics.LowestCapacitorPercentageThisPocket = 100;
            Statistics.PanicAttemptsThisMission = 0;
            Statistics.LowestShieldPercentageThisMission = 100;
            Statistics.LowestArmorPercentageThisMission = 100;
            Statistics.LowestCapacitorPercentageThisMission = 100;
        }

        public double TimeInCurrentMission()
        {
            double missiontimeMinutes = Math.Round(DateTime.UtcNow.Subtract(Statistics.StartedMission).TotalMinutes, 0);
            return missiontimeMinutes;
        }

        public static bool WreckStatistics(IEnumerable<ItemCache> items, EntityCache containerEntity)
        {
            //if (QMSettings.Instance.DateTimeForLogs = EveTime)
            //{
            //    DateTimeForLogs = DateTime.UtcNow;
            //}
            //else //assume LocalTime
            //{
            DateTimeForLogs = DateTime.Now;
            //}

            if (Statistics.WreckLootStatistics)
            {
                if (containerEntity != null)
                {
                    // Log all items found in the wreck
                    File.AppendAllText(WreckLootStatisticsFile, "TIME: " + string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTimeForLogs) + "\n");
                    File.AppendAllText(WreckLootStatisticsFile, "NAME: " + containerEntity.Name + "\n");
                    File.AppendAllText(WreckLootStatisticsFile, "ITEMS:" + "\n");
                    foreach (ItemCache item in items.OrderBy(i => i.TypeId))
                    {
                        File.AppendAllText(WreckLootStatisticsFile, "TypeID: " + item.TypeId.ToString(CultureInfo.InvariantCulture) + "\n");
                        File.AppendAllText(WreckLootStatisticsFile, "Name: " + item.Name + "\n");
                        File.AppendAllText(WreckLootStatisticsFile, "Quantity: " + item.Quantity.ToString(CultureInfo.InvariantCulture) + "\n");
                        File.AppendAllText(WreckLootStatisticsFile, "=\n");
                    }
                    File.AppendAllText(WreckLootStatisticsFile, ";" + "\n");
                }
            }
            return true;
        }

        public static bool LogWindowActionToWindowLog(string Windowname, string Description)
        {
            try
            {
                string textToLogToFile;
                if (!File.Exists(WindowStatslogFile))
                {
                    //
                    // build header
                    //
                    textToLogToFile = "WindowName;Description;Time;Seconds Since LastInSpace;Seconds Since LastInStation;Seconds Since We Started;\r\n";
                    File.AppendAllText(WindowStatslogFile, textToLogToFile);
                }

                //
                // header should already be built.
                //
                textToLogToFile = Windowname + ";" + Description +  ";" + DateTime.UtcNow.ToShortTimeString() + ";" + Time.Instance.LastInSpace.Subtract(DateTime.UtcNow).TotalSeconds + ";" + Time.Instance.LastInStation.Subtract(DateTime.UtcNow).TotalSeconds + ";" + Time.Instance.QuestorStarted_DateTime.Subtract(DateTime.UtcNow).TotalSeconds + ";";
                textToLogToFile += "\r\n";

                //Logging.Log("Statistics", ";PocketObjectStatistics;" + objectline, Logging.White);
                File.AppendAllText(WindowStatslogFile, textToLogToFile);
                return true;
            }
            catch (Exception ex)
            {
                Logging.Log("Statistics", "Exception while logging to file [" + ex.Message + "]", Logging.White);
                return false;
            }
        }

        public static bool PocketObjectStatistics(List<EntityCache> things, bool force = false)
        {
            if (PocketObjectStatisticsLog || force)
            {
                string currentPocketName = Logging.FilterPath("Random-Grid");
                try
                {
                    if (!String.IsNullOrEmpty(MissionSettings.MissionName))
                    {
                        currentPocketName = Logging.FilterPath(MissionSettings.MissionName);
                    }
                }
                catch (Exception ex)
                {
                    Logging.Log("Statistics", "PocketObjectStatistics: is QMCache.Instance.MissionName null?: exception was [" + ex.Message + "]", Logging.White);
                }

                PocketObjectStatisticsFile = Path.Combine(
                        PocketObjectStatisticsPath,
                        Logging.FilterPath(QMCache.Instance.DirectEve.Me.Name) + " - " + currentPocketName + " - " +
                        CombatMissionCtrl.PocketNumber + " - ObjectStatistics.csv");

                Logging.Log("Statistics.ObjectStatistics", "Logging info on the [" + things.Count + "] objects in this pocket to [" + PocketObjectStatisticsFile + "]", Logging.White);

                if (File.Exists(PocketObjectStatisticsFile))
                {
                    File.Delete(PocketObjectStatisticsFile);
                }

                //
                // build header
                //
                string objectline = "Name;Distance;TypeId;GroupId;CategoryId;IsNPC;IsPlayer;TargetValue;Velocity;ID;\r\n";
                //Logging.Log("Statistics",";PocketObjectStatistics;" + objectline,Logging.White);
                File.AppendAllText(PocketObjectStatisticsFile, objectline);

                //
                // iterate through entities
                //
                foreach (EntityCache thing in things.OrderBy(i => i.Distance))
                {
                    objectline = thing.Name + ";";
                    objectline += Math.Round(thing.Distance / 1000, 0) + ";";
                    objectline += thing.TypeId + ";";
                    objectline += thing.GroupId + ";";
                    objectline += thing.CategoryId + ";";
                    objectline += thing.IsNpc + ";";
                    objectline += thing.IsPlayer + ";";
                    objectline += thing.TargetValue + ";";
                    objectline += Math.Round(thing.Velocity, 0) + ";";
                    objectline += thing.Id + ";\r\n";

                    //
                    // can we somehow get the X,Y,Z coord? If we could we could use this info to build some kind of grid layout...
                    // or at least know the distances between all the NPCs... thus be able to infer which NPCs were in which 'groups'
                    //

                    //Logging.Log("Statistics", ";PocketObjectStatistics;" + objectline, Logging.White);
                    File.AppendAllText(PocketObjectStatisticsFile, objectline);
                }
            }
            return true;
        }

        public static bool LogEntities(List<EntityCache> things, bool force = false)
        {
            // iterate through entities
            //
            Logging.Log("Entities", "--------------------------- Start (listed below)-----------------------------", Logging.Yellow);
            things = things.ToList();
            if (things.Any())
            {
                int icount = 0;
                foreach (EntityCache thing in things.OrderBy(i => i.Distance))
                {
                    icount++;
                    Logging.Log(icount.ToString(), thing.Name + "[" + Math.Round(thing.Distance / 1000, 0) + "k] GroupID[" + thing.GroupId + "] ID[" + thing.MaskedId + "] isSentry[" + thing.IsSentry + "] IsHVT[" + thing.IsHighValueTarget + "] IsLVT[" + thing.IsLowValueTarget + "] IsIgnored[" + thing.IsIgnored + "]", Logging.Debug);
                }
            }
            Logging.Log("Entities", "--------------------------- Done  (listed above)-----------------------------", Logging.Yellow);

            return true;
        }

        public static bool ListItems(IEnumerable<ItemCache> ItemsToList)
        {
            Logging.Log("Items", "--------------------------- Start (listed below)-----------------------------", Logging.Yellow);
            ItemsToList = ItemsToList.ToList();
            if (ItemsToList.Any())
            {

                int icount = 0;
                foreach (ItemCache item in ItemsToList.OrderBy(i => i.TypeId).ThenBy(i => i.GroupId))
                {
                    icount++;
                    Logging.Log(icount.ToString(), "[" + item.Name + "] GroupID [" + item.GroupId + "], IsContraband [" + item.IsContraband + "]", Logging.Debug);
                }
            }
            Logging.Log("Items", "--------------------------- Done  (listed above)-----------------------------", Logging.Yellow);

            return true;
        }

        public static bool ModuleInfo(IEnumerable<ModuleCache> _modules)
        {
            Logging.Log("ModuleInfo", "--------------------------- Start (listed below)-----------------------------", Logging.Yellow);
            _modules = _modules.ToList();
            if (_modules != null && _modules.Any())
            {

                int icount = 0;
                foreach (ModuleCache _module in _modules.OrderBy(i => i.TypeId).ThenBy(i => i.GroupId))
                {
                    icount++;
                    Logging.Log(icount.ToString(), "TypeID [" + _module.TypeId + "] GroupID [" + _module.GroupId + "] isOnline [" + _module.IsOnline + "] isActivatable [" + _module.IsActivatable + "] IsActive [" + _module.IsActive + "] OptimalRange [" + _module.OptimalRange + "] Falloff [" + _module.FallOff + "] Duration [" + _module.Duration + "] IsActive [" + _module.IsActive + "]", Logging.Debug);
                }
            }
            Logging.Log("ModuleInfo", "--------------------------- Done  (listed above)-----------------------------", Logging.Yellow);
            //Logging.Log("WeaponInfo", "--------------------------- Start (listed below)-----------------------------", Logging.Yellow);
            //
            //if (QMCache.Instance.Weapons != null && QMCache.Instance.Weapons.Any())
            //{
            //
            //    int iCount = 0;
            //    foreach (ModuleCache weapon in QMCache.Instance.Weapons.OrderBy(i => i.TypeId).ThenBy(i => i.GroupId))
            //    {
            //        iCount++;
            //        Logging.Log(icount.ToString(), "TypeID [" + weapon.TypeId + "] GroupID [" + weapon.GroupId + "] isOnline [" + weapon.IsOnline + "] isActivatable [" + weapon.IsActivatable + "] IsActive [" + weapon.IsActive + "] OptimalRange [" + weapon.OptimalRange + "] Falloff [" + weapon.FallOff + "] Duration [" + weapon.Duration + "] LastReload [" + Math.Round(DateTime.UtcNow.Subtract(Combat.LastWeaponReload[weapon.ItemId]).TotalSeconds, 0) + "]", Logging.Debug);
            //    }
            //}
            //Logging.Log("WeaponInfo", "--------------------------- Done  (listed above)-----------------------------", Logging.Yellow);


            return true;
        }

        public static bool ListClassInstanceInfo()
        {
            Logging.Log("debug", "--------------------------- Start (listed below)-----------------------------", Logging.Yellow);
            if (QMCache.Instance.EntitiesOnGrid.Any())
            {
                //Logging.Log("debug", "Entities: [" + QMCache.Instance.Entities.Count() + "] EntityCache  Class Instances: [" + EntityCache.EntityCacheInstances + "]", Logging.Debug);
                Logging.Log("debug", "InvType Class Instances: [" + InvType.InvTypeInstances + "]", Logging.Debug);
                Logging.Log("debug", "Cache Class Instances: [" + QMCache.CacheInstances + "]", Logging.Debug);
                Logging.Log("debug", "Settings Class Instances: [" + QMSettings.SettingsInstances + "]", Logging.Debug);
            }
            Logging.Log("debug", "--------------------------- Done  (listed above) -----------------------------", Logging.Yellow);


            return true;
        }

        public static bool ListIgnoredTargets()
        {
            Logging.Log("IgnoreTargets", "--------------------------- Start (listed below)-----------------------------", Logging.Yellow);
            Logging.Log("IgnoreTargets", "Note: Ignore Targets are based on Text Matching. If you ignore: Angel Warlord you ignore all of them on the field!", Logging.Debug);
            if (CombatMissionCtrl.IgnoreTargets.Any())
            {

                int icount = 0;
                foreach (string ignoreTarget in CombatMissionCtrl.IgnoreTargets)
                {
                    icount++;
                    Logging.Log(icount.ToString(), "[" + ignoreTarget + "] of a total of [" + CombatMissionCtrl.IgnoreTargets.Count() + "]", Logging.Debug);
                }
            }
            Logging.Log("IgnoreTargets", "--------------------------- Done  (listed above) -----------------------------", Logging.Yellow);
            return true;
        }

        public static bool ListDronePriorityTargets(IEnumerable<EntityCache> primaryDroneTargets)
        {
            Logging.Log("DPT", "--------------------------- Start (listed below)-----------------------------", Logging.Yellow);
            if (Drones.PreferredDroneTarget != null)
            {
                Logging.Log("DPT", "[" + 0 + "] PreferredDroneTarget [" + Drones.PreferredDroneTarget.Name + "][" + Math.Round(Drones.PreferredDroneTarget.Distance / 1000, 0) + "k] IsInOptimalRange [" + Drones.PreferredDroneTarget.IsInOptimalRange + "] IsTarget [" + Drones.PreferredDroneTarget.IsTarget + "]", Logging.Debug);
            }

            primaryDroneTargets = primaryDroneTargets.ToList();
            if (primaryDroneTargets.Any())
            {
                int icount = 0;
                foreach (EntityCache dronePriorityTarget in primaryDroneTargets.OrderBy(i => i.DronePriorityLevel).ThenBy(i => i.Name))
                {
                    icount++;
                    Logging.Log(icount.ToString(), "[" + dronePriorityTarget.Name + "][" + Math.Round(dronePriorityTarget.Distance / 1000, 0) + "k] IsInOptimalRange [" + dronePriorityTarget.IsInOptimalRange + "] IsTarget [" + dronePriorityTarget.IsTarget + "] DronePriorityLevel [" + dronePriorityTarget.DronePriorityLevel + "]", Logging.Debug);
                }
            }
            Logging.Log("DPT", "--------------------------- Done  (listed above) -----------------------------", Logging.Yellow);
            return true;
        }

        public static bool ListTargetedandTargeting(IEnumerable<EntityCache> targetedandTargeting)
        {
            Logging.Log("List", "--------------------------- Start (listed below)-----------------------------", Logging.Yellow);
            targetedandTargeting = targetedandTargeting.ToList();
            if (targetedandTargeting.Any())
            {
                int icount = 0;
                foreach (EntityCache targetedandTargetingEntity in targetedandTargeting.OrderBy(i => i.Distance).ThenBy(i => i.Name))
                {
                    icount++;
                    Logging.Log(icount.ToString(), "[" + targetedandTargetingEntity.Name + "][" + Math.Round(targetedandTargetingEntity.Distance / 1000, 0) + "k] IsIgnored [" + targetedandTargetingEntity.IsIgnored + "] IsInOptimalRange [" + targetedandTargetingEntity.IsInOptimalRange + "] isTarget [" + targetedandTargetingEntity.IsTarget + "] isTargeting [" + targetedandTargetingEntity.IsTargeting + "] IsPrimaryWeaponPriorityTarget [" + targetedandTargetingEntity.IsPrimaryWeaponPriorityTarget + "] IsDronePriorityTarget [" + targetedandTargetingEntity.IsDronePriorityTarget + "]", Logging.Debug);
                }
            }
            Logging.Log("List", "--------------------------- Done  (listed above)-----------------------------", Logging.Yellow);
            return true;
        }

        public static bool EntityStatistics(IEnumerable<EntityCache> things)
        {
            string objectline = "Name;Distance;TypeId;GroupId;CategoryId;IsNPC;IsNPCByGroupID;IsPlayer;TargetValue;Velocity;HaveLootRights;IsContainer;ID;\r\n";
            Logging.Log("Statistics", ";EntityStatistics;" + objectline, Logging.White);

            things = things.ToList();

            if (!things.Any()) //if their are no entries, return
            {
                Logging.Log("Statistics", "EntityStatistics: No entries to log", Logging.White);
                return true;
            }

            foreach (EntityCache thing in things.OrderBy(i => i.Distance))
            {
                objectline = thing.Name + ";";
                objectline += Math.Round(thing.Distance / 1000, 0) + ";";
                objectline += thing.TypeId + ";";
                objectline += thing.GroupId + ";";
                objectline += thing.CategoryId + ";";
                objectline += thing.IsNpc + ";";
                objectline += thing.IsNpcByGroupID + ";";
                objectline += thing.IsPlayer + ";";
                objectline += thing.TargetValue + ";";
                objectline += Math.Round(thing.Velocity, 0) + ";";
                objectline += thing.HaveLootRights + ";";
                objectline += thing.IsContainer + ";";
                objectline += thing.Id + ";\r\n";

                //
                // can we somehow get the X,Y,Z coord? If we could we could use this info to build some kinda mission simulator...
                // or at least know the distances between all the NPCs... thus be able to infer which NPCs were in which 'groups'
                //

                Logging.Log("Statistics", ";EntityStatistics;" + objectline, Logging.White);
            }
            return true;
        }

        /**
        public static bool IndividualVolleyDataStatistics(List<EachWeaponsVolleyCache> ListOfVolleyDataToCrunch )
        {
            try
            {
                if (VolleyStatsLog)
                {
                    if (!Directory.Exists(VolleyStatsLogPath))
                    {
                        Directory.CreateDirectory(VolleyStatsLogPath);
                    }

                    VolleyStatslogFile = System.IO.Path.Combine(VolleyStatsLogPath, Logging.characterNameForLogs + ".VolleyStats[" + DateTime.UtcNow.DayOfYear + "].log");

                    if (!File.Exists(VolleyStatslogFile))
                    {
                        File.AppendAllText(VolleyStatslogFile, "VolleyNumber;TimeOfTheVolley;targetName;targetDistance;moduleName;moduleAmmoTypeName;myShipName;pocketNumber;missionName;systemName;moduleCurrentCharges;moduleFalloff;moduleItemID;moduleOptimal;moduleTargetID;moduleTypeID;myShipShieldPercentage;myShipArmorPercentage;myShipHullPercentage;myShipCapacitorPercentage;myShipVelocity;myShipXCoordinate;myShipYCoordinate;myShipZCoordinate;targetTransversalVelocity;targetAngularVelocity;targetXCoordinate;targetYCoordinate;targetZCoordinate\r\n");
                    }

                    foreach (EachWeaponsVolleyCache individualVolleyDataEntry in ListOfVolleyDataToCrunch)
                    {
                        string objectline = individualVolleyDataEntry.thisWasVolleyNumber + ";";
                        objectline += individualVolleyDataEntry.ThisVolleyCacheCreated.ToShortTimeString() + ";";
                        objectline += individualVolleyDataEntry.targetName + ";";
                        objectline += Math.Round(individualVolleyDataEntry.targetDistance / 1000, 2) + ";";
                        objectline += individualVolleyDataEntry.targetVelocity + ";";
                        objectline += individualVolleyDataEntry.moduleName + ";";
                        objectline += individualVolleyDataEntry.moduleAmmoTypeName + ";";
                        objectline += individualVolleyDataEntry.myShipName + ";";
                        objectline += individualVolleyDataEntry.pocketNumber + ";";
                        objectline += individualVolleyDataEntry.missionName + ";";
                        objectline += individualVolleyDataEntry.systemName + ";";
                        objectline += individualVolleyDataEntry.moduleCurrentCharges + ";";
                        objectline += individualVolleyDataEntry.moduleFalloff + ";";
                        objectline += individualVolleyDataEntry.moduleItemID + ";";
                        objectline += individualVolleyDataEntry.moduleOptimal + ";";
                        objectline += individualVolleyDataEntry.moduleTargetID + ";";
                        objectline += individualVolleyDataEntry.moduleTypeID + ";";
                        objectline += individualVolleyDataEntry.myShipShieldPercentage + ";";
                        objectline += individualVolleyDataEntry.myShipArmorPercentage + ";";
                        objectline += individualVolleyDataEntry.myShipHullPercentage + ";";
                        objectline += individualVolleyDataEntry.myShipCapacitorPercentage + ";";
                        objectline += individualVolleyDataEntry.myShipVelocity + ";";
                        objectline += individualVolleyDataEntry.myShipXCoordinate + ";";
                        objectline += individualVolleyDataEntry.myShipYCoordinate + ";";
                        objectline += individualVolleyDataEntry.myShipZCoordinate + ";";
                        objectline += individualVolleyDataEntry.targetTransversalVelocity + ";";
                        objectline += individualVolleyDataEntry.targetAngularVelocity + ";";
                        objectline += individualVolleyDataEntry.targetXCoordinate + ";";
                        objectline += individualVolleyDataEntry.targetYCoordinate + ";";
                        objectline += individualVolleyDataEntry.targetZCoordinate + ";";
                        objectline += "\r\n";

                        File.AppendAllText(VolleyStatslogFile, objectline);
                    }
                }
            }
            catch (Exception exception)
            {
                Logging.Log("Statistics", "IndividualVolleyDataStatistics - Exception: [" + exception + "]", Logging.Red);
                return false;
            }

            return true;
        }
        **/
        public static bool AmmoConsumptionStatistics()
        {
            // Ammo Consumption statistics
            // Is cargo open?
            if (QMCache.Instance.CurrentShipsCargo == null)
            {
                Logging.Log("AmmoConsumptionStatistics", "if (QMCache.Instance.CurrentShipsCargo == null)", Logging.Teal);
                return false;
            }

            IEnumerable<Ammo> correctAmmo1 = Combat.Ammo.Where(a => a.DamageType == MissionSettings.CurrentDamageType);
            IEnumerable<DirectItem> ammoCargo = QMCache.Instance.CurrentShipsCargo.Items.Where(i => correctAmmo1.Any(a => a.TypeId == i.TypeId));
            try
            {
                foreach (DirectItem item in ammoCargo)
                {
                    Ammo ammo1 = Combat.Ammo.FirstOrDefault(a => a.TypeId == item.TypeId);
                    DirectInvType ammoType;
                    QMCache.Instance.DirectEve.InvTypes.TryGetValue(item.TypeId, out ammoType);
                    if (ammo1 != null) Statistics.AmmoConsumption = (ammo1.Quantity - item.Quantity);
                    if (ammoType != null)
                    {
                        //Statistics.AmmoValue = ((int?)ammoType.AveragePrice ?? 0) * Statistics.AmmoConsumption;
                    }
                }
            }
            catch (Exception exception)
            {
                Logging.Log("Statistics.AmmoConsumptionStatistics","Exception: " + exception,Logging.Debug);
            }

            return true;
        }

        public static bool WriteDroneStatsLog()
        {
            //if (QMSettings.Instance.DateTimeForLogs = EveTime)
            //{
            //    DateTimeForLogs = DateTime.UtcNow;
            //}
            //else //assume LocalTime
            //{
            DateTimeForLogs = DateTime.Now;

            //}

            if (DroneStatsLog && !Statistics.DroneLoggingCompleted)
            {
                // Lost drone statistics
                if (Drones.UseDrones &&
                     QMCache.Instance.ActiveShip.GroupId != (int)Group.Capsule &&
                     QMCache.Instance.ActiveShip.GroupId != (int)Group.Shuttle &&
                     QMCache.Instance.ActiveShip.GroupId != (int)Group.Frigate &&
                     QMCache.Instance.ActiveShip.GroupId != (int)Group.Industrial &&
                     QMCache.Instance.ActiveShip.GroupId != (int)Group.TransportShip &&
                     QMCache.Instance.ActiveShip.GroupId != (int)Group.Freighter)
                {
                    if (!File.Exists(DroneStatslogFile))
                    {
                        File.AppendAllText(DroneStatslogFile, "Date;Mission;Number of lost drones;# of Recalls\r\n");
                    }

                    string droneline = DateTimeForLogs.ToShortDateString() + ";";
                    droneline += DateTimeForLogs.ToShortTimeString() + ";";
                    droneline += MissionSettings.MissionName + ";";
                    droneline += Statistics.LostDrones + ";";
                    droneline += +Statistics.DroneRecalls + ";\r\n";
                    File.AppendAllText(DroneStatslogFile, droneline);
                    Statistics.DroneLoggingCompleted = true;
                }
                else
                {
                    Logging.Log("DroneStats", "We do not use drones in this type of ship, skipping drone stats", Logging.White);
                    Statistics.DroneLoggingCompleted = true;
                }
            }

            // Lost drone statistics stuff ends here
            return true;
        }

        public static void WriteSessionLogStarting()
        {
            //if (QMSettings.Instance.DateTimeForLogs = EVETIme)
            //{
            //    DateTimeForLogs = DateTime.UtcNow;
            //}
            //else //assume LocalTime
            //{
            DateTimeForLogs = DateTime.Now;

            //}

            if (SessionsLog)
            {
                if (QMCache.Instance.MyWalletBalance != 0 || QMCache.Instance.MyWalletBalance != -2147483648) // this hopefully resolves having negative max int in the session logs occasionally
                {
                    //
                    // prepare the Questor Session Log - keeps track of starts, restarts and exits, and hopefully the reasons
                    //
                    // Get the path
                    if (!Directory.Exists(SessionsLogPath))
                    {
                        Directory.CreateDirectory(SessionsLogPath);
                    }

                    // Write the header
                    if (!File.Exists(SessionsLogFile))
                    {
                        File.AppendAllText(SessionsLogFile, "Date;RunningTime;SessionState;LastMission;WalletBalance;MemoryUsage;Reason;IskGenerated;LootGenerated;LPGenerated;Isk/Hr;Loot/Hr;LP/HR;Total/HR;\r\n");
                    }

                    // Build the line
                    string line = DateTimeForLogs + ";";                           //Date
                    line += "0" + ";";                                          //RunningTime
                    line += Cleanup.SessionState + ";";                  //SessionState
                    line += "" + ";";                                           //LastMission
                    line += QMCache.Instance.MyWalletBalance + ";";               //WalletBalance
                    line += QMCache.Instance.TotalMegaBytesOfMemoryUsed + ";";    //MemoryUsage
                    line += "Starting" + ";";                                   //Reason
                    line += ";";                                                //IskGenerated
                    line += ";";                                                //LootGenerated
                    line += ";";                                                //LPGenerated
                    line += ";";                                                //Isk/Hr
                    line += ";";                                                //Loot/Hr
                    line += ";";                                                //LP/HR
                    line += ";\r\n";                                            //Total/HR

                    // The mission is finished
                    File.AppendAllText(SessionsLogFile, line);

                    Cleanup.SessionState = "";
                    Logging.Log("Statistics: WriteSessionLogStarting", "Writing session data to [ " + SessionsLogFile + " ]", Logging.White);
                }
            }
        }

        public static bool WriteSessionLogClosing()
        {
            //if (QMSettings.Instance.DateTimeForLogs = EVETIme)
            //{
            //    DateTimeForLogs = DateTime.UtcNow;
            //}
            //else //assume LocalTime
            //{
            DateTimeForLogs = DateTime.Now;

            //}

            if (SessionsLog) // if false we do not write a session log, doubles as a flag so we don't write the session log more than once
            {
                //
                // prepare the Questor Session Log - keeps track of starts, restarts and exits, and hopefully the reasons
                //

                // Get the path

                if (!Directory.Exists(SessionsLogPath))
                {
                    Directory.CreateDirectory(SessionsLogPath);
                }

                SessionIskPerHrGenerated = ((int)SessionIskGenerated / (DateTime.UtcNow.Subtract(Time.Instance.QuestorStarted_DateTime).TotalMinutes / 60));
                SessionLootPerHrGenerated = ((int)SessionLootGenerated / (DateTime.UtcNow.Subtract(Time.Instance.QuestorStarted_DateTime).TotalMinutes / 60));
                SessionLPPerHrGenerated = (((int)SessionLPGenerated * (int)Statistics.IskPerLP) / (DateTime.UtcNow.Subtract(Time.Instance.QuestorStarted_DateTime).TotalMinutes / 60));
                SessionTotalPerHrGenerated = ((int)SessionIskPerHrGenerated + (int)SessionLootPerHrGenerated + (int)SessionLPPerHrGenerated);
                Logging.Log("QuestorState.CloseQuestor", "Writing Session Data [1]", Logging.White);

                // Write the header
                if (!File.Exists(SessionsLogFile))
                {
                    File.AppendAllText(SessionsLogFile, "Date;RunningTime;SessionState;LastMission;WalletBalance;MemoryUsage;Reason;IskGenerated;LootGenerated;LPGenerated;Isk/Hr;Loot/Hr;LP/HR;Total/HR;\r\n");
                }

                // Build the line
                string line = DateTimeForLogs + ";";                               // Date
                line += SessionRunningTime + ";";                // RunningTime
                line += Cleanup.SessionState + ";";                      // SessionState
                line += MissionSettings.MissionName + ";";                       // LastMission
                line += QMCache.Instance.MyWalletBalance + ";";                   // WalletBalance
                line += ((int)QMCache.Instance.TotalMegaBytesOfMemoryUsed + ";"); // MemoryUsage
                line += Cleanup.ReasonToStopQuestor + ";";               // Reason to Stop Questor
                line += SessionIskGenerated + ";";               // Isk Generated This Session
                line += SessionLootGenerated + ";";              // Loot Generated This Session
                line += SessionLPGenerated + ";";                // LP Generated This Session
                line += SessionIskPerHrGenerated + ";";          // Isk Generated per hour this session
                line += SessionLootPerHrGenerated + ";";         // Loot Generated per hour This Session
                line += SessionLPPerHrGenerated + ";";           // LP Generated per hour This Session
                line += SessionTotalPerHrGenerated + ";\r\n";    // Total Per Hour This Session

                // The mission is finished
                Logging.Log("Statistics: WriteSessionLogClosing", line, Logging.White);
                File.AppendAllText(SessionsLogFile, line);

                Logging.Log("Statistics: WriteSessionLogClosing", "Writing to session log [ " + SessionsLogFile, Logging.White);
                Logging.Log("Statistics: WriteSessionLogClosing", "Questor is stopping because: " + Cleanup.ReasonToStopQuestor, Logging.White);
                SessionsLog = false; //so we don't write the session log more than once per session
            }
            return true;
        }

        public static void WritePocketStatistics()
        {
            //if (QMSettings.Instance.DateTimeForLogs = EVETIme)
            //{
            //    DateTimeForLogs = DateTime.UtcNow;
            //}
            //else //assume LocalTime
            //{
            DateTimeForLogs = DateTime.Now;

            //}

            // We are not supposed to create bookmarks
            //if (!QMSettings.Instance.LogBounties)
            //    return;

            //agentID needs to change if its a storyline mission - so its assigned in storyline.cs to the various modules directly.
            string currentPocketName = Logging.FilterPath(MissionSettings.MissionName);
            if (PocketStatistics)
            {
                if (PocketStatsUseIndividualFilesPerPocket)
                {
                    PocketStatisticsFile = Path.Combine(PocketStatisticsPath, Logging.FilterPath(QMCache.Instance.DirectEve.Me.Name) + " - " + currentPocketName + " - " + CombatMissionCtrl.PocketNumber + " - PocketStatistics.csv");
                }
                if (!Directory.Exists(PocketStatisticsPath))
                    Directory.CreateDirectory(PocketStatisticsPath);

                //
                // this is writing down stats from the PREVIOUS pocket (if any?!)
                //

                // Write the header
                if (!File.Exists(PocketStatisticsFile))
                    File.AppendAllText(PocketStatisticsFile, "Date and Time;Mission Name ;Pocket;Time to complete;Isk;panics;LowestShields;LowestArmor;LowestCapacitor;RepairCycles;Wrecks\r\n");

                // Build the line
                string pocketstatsLine = DateTimeForLogs + ";";                                                            //Date
                pocketstatsLine += currentPocketName + ";";                                                                //Mission Name
                pocketstatsLine += "pocket" + (CombatMissionCtrl.PocketNumber) + ";";                                         //Pocket number
                pocketstatsLine += ((int)DateTime.UtcNow.Subtract(Statistics.StartedMission).TotalMinutes) + ";"; //Time to Complete
                pocketstatsLine += QMCache.Instance.MyWalletBalance - QMCache.Instance.WealthatStartofPocket + ";";            //Isk
                pocketstatsLine += Statistics.PanicAttemptsThisPocket + ";";                                           //Panics
                pocketstatsLine += ((int)Statistics.LowestShieldPercentageThisPocket) + ";";                           //LowestShields
                pocketstatsLine += ((int)Statistics.LowestArmorPercentageThisPocket) + ";";                            //LowestArmor
                pocketstatsLine += ((int)Statistics.LowestCapacitorPercentageThisPocket) + ";";                        //LowestCapacitor
                pocketstatsLine += Statistics.RepairCycleTimeThisPocket + ";";                                         //repairCycles
                pocketstatsLine += Statistics.WrecksThisPocket + ";";                                                  //wrecksThisPocket
                pocketstatsLine += "\r\n";

                // The old pocket is finished
                Logging.Log("Statistics: WritePocketStatistics", "Writing pocket statistics to [ " + PocketStatisticsFile + " ] and clearing stats for next pocket", Logging.White);
                File.AppendAllText(PocketStatisticsFile, pocketstatsLine);
            }

            // Update statistic values for next pocket stats
            QMCache.Instance.WealthatStartofPocket = QMCache.Instance.MyWalletBalance;
            Statistics.StartedPocket = DateTime.UtcNow;
            Statistics.PanicAttemptsThisPocket = 0;
            Statistics.LowestShieldPercentageThisPocket = 101;
            Statistics.LowestArmorPercentageThisPocket = 101;
            Statistics.LowestCapacitorPercentageThisPocket = 101;
            Statistics.RepairCycleTimeThisPocket = 0;
            Statistics.WrecksThisMission += Statistics.WrecksThisPocket;
            Statistics.WrecksThisPocket = 0;
            QMCache.Instance.OrbitEntityNamed = null;
        }

        public static void SaveMissionHTMLDetails(string MissionDetailsHtml, string missionName)
        {
            DateTimeForLogs = DateTime.Now;

            string missionDetailsHtmlFile = Path.Combine(MissionDetailsHtmlPath, missionName + " - " + "mission-description-html.txt");

            if (!Directory.Exists(MissionDetailsHtmlPath))
            {
                Directory.CreateDirectory(MissionDetailsHtmlPath);
            }

            // Write the file
            if (!File.Exists(missionDetailsHtmlFile))
            {
                Logging.Log("Statistics: SaveMissionHTMLDetails", "Writing mission details HTML [ " + missionDetailsHtmlFile + " ]", Logging.White);
                File.AppendAllText(missionDetailsHtmlFile, MissionDetailsHtml);
            }
        }

        public static void WriteMissionStatistics(long statisticsForThisAgent)
        {
            //if (QMSettings.Instance.DateTimeForLogs = EveTime)
            //{
            //    DateTimeForLogs = DateTime.UtcNow;
            //}
            //else //assume LocalTime
            //{
            DateTimeForLogs = DateTime.Now;

            //}

            if (QMCache.Instance.InSpace)
            {
                Logging.Log("Statistics", "We have started questor in space, assume we do not need to write any statistics at the moment.", Logging.Teal);
                Statistics.MissionLoggingCompleted = true; //if the mission was completed more than 10 min ago assume the logging has been done already.
                return;
            }

            MissionSettings.Mission = QMCache.Instance.GetAgentMission(statisticsForThisAgent, true);
            if (Logging.DebugStatistics) // we only need to see the following wall of comments if debugging mission statistics
            {
                Logging.Log("Statistics", "...Checking to see if we should create a mission log now...", Logging.White);
                Logging.Log("Statistics", " ", Logging.White);
                Logging.Log("Statistics", " ", Logging.White);
                Logging.Log("Statistics", "The Rules for After Mission Logging are as Follows...", Logging.White);
                Logging.Log("Statistics", "1)  we must have loyalty points with the current agent (disabled at the moment)", Logging.White); //which we already verified if we got this far
                Logging.Log("Statistics", "2) QMCache.Instance.MissionName must not be empty - we must have had a mission already this session", Logging.White);
                Logging.Log("Statistics", "AND", Logging.White);
                Logging.Log("Statistics", "3a QMCache.Instance.mission == null - their must not be a current mission OR", Logging.White);
                Logging.Log("Statistics", "3b QMCache.Instance.mission.State != (int)MissionState.Accepted) - the mission state is not 'Accepted'", Logging.White);
                Logging.Log("Statistics", " ", Logging.White);
                Logging.Log("Statistics", " ", Logging.White);
                Logging.Log("Statistics", "If those are all met then we get to create a log for the previous mission.", Logging.White);

                if (!string.IsNullOrEmpty(MissionSettings.MissionName)) //condition 1
                {
                    Logging.Log("Statistics", "1 We must have a mission because MissionName is filled in", Logging.White);
                    Logging.Log("Statistics", "1 Mission is: " + MissionSettings.MissionName, Logging.White);

                    if (MissionSettings.Mission != null) //condition 2
                    {
                        Logging.Log("Statistics", "2 QMCache.Instance.mission is: " + MissionSettings.Mission, Logging.White);
                        Logging.Log("Statistics", "2 QMCache.Instance.mission.Name is: " + MissionSettings.Mission.Name, Logging.White);
                        Logging.Log("Statistics", "2 QMCache.Instance.mission.State is: " + MissionSettings.Mission.State, Logging.White);

                        if (MissionSettings.Mission.State != (int)MissionState.Accepted) //condition 3
                        {
                            Logging.Log("Statistics", "MissionState is NOT Accepted: which is correct if we want to do logging", Logging.White);
                        }
                        else
                        {
                            Logging.Log("Statistics", "MissionState is Accepted: which means the mission is not yet complete", Logging.White);
                            Statistics.MissionLoggingCompleted = true; //if it is not true - this means we should not be trying to log mission stats atm
                        }
                    }
                    else
                    {
                        Logging.Log("Statistics", "mission is NULL - which means we have no current mission", Logging.White);
                        Statistics.MissionLoggingCompleted = true; //if it is not true - this means we should not be trying to log mission stats atm
                    }
                }
                else
                {
                    Logging.Log("Statistics", "1 We must NOT have had a mission yet because MissionName is not filled in", Logging.White);
                    Statistics.MissionLoggingCompleted = true; //if it is not true - this means we should not be trying to log mission stats atm
                }
            }

            if (AgentLPRetrievalAttempts > 5)
            {
                Logging.Log("Statistics", "WriteMissionStatistics: We do not have loyalty points with the current agent yet, still -1, attempt # [" + AgentLPRetrievalAttempts + "] giving up", Logging.White);
                AgentLPRetrievalAttempts = 0;
                Statistics.MissionLoggingCompleted = true; //if it is not true - this means we should not be trying to log mission stats atm
                return;
            }

            // Seeing as we completed a mission, we will have loyalty points for this agent
            if (AgentInteraction.Agent.LoyaltyPoints == -1)
            {
                AgentLPRetrievalAttempts++;
                Logging.Log("Statistics", "WriteMissionStatistics: We do not have loyalty points with the current agent yet, still -1, attempt # [" + AgentLPRetrievalAttempts + "] retrying...", Logging.White);
                return;
            }

            AgentLPRetrievalAttempts = 0;

            MissionsThisSession++;
            if (Logging.DebugStatistics) Logging.Log("Statistics", "We jumped through all the hoops: now do the mission logging", Logging.White);
            SessionIskGenerated = (SessionIskGenerated + (QMCache.Instance.MyWalletBalance - QMCache.Instance.Wealth));
            SessionLootGenerated = (SessionLootGenerated + Statistics.LootValue);
            SessionLPGenerated = (SessionLPGenerated + (AgentInteraction.Agent.LoyaltyPoints - Statistics.LoyaltyPoints));
            Logging.Log("Statistics", "Printing All Statistics Related Variables to the console log:", Logging.White);
            Logging.Log("Statistics", "Mission Name: [" + MissionSettings.MissionName + "]", Logging.White);
            Logging.Log("Statistics", "Faction: [" + MissionSettings.FactionName + "]", Logging.White);
            Logging.Log("Statistics", "System: [" + QMCache.Instance.MissionSolarSystem + "]", Logging.White);
            Logging.Log("Statistics", "Total Missions completed this session: [" + MissionsThisSession + "]", Logging.White);
            Logging.Log("Statistics", "StartedMission: [ " + Statistics.StartedMission + "]", Logging.White);
            Logging.Log("Statistics", "FinishedMission: [ " + Statistics.FinishedMission + "]", Logging.White);
            Logging.Log("Statistics", "StartedSalvaging: [ " + Statistics.StartedSalvaging + "]", Logging.White);
            Logging.Log("Statistics", "FinishedSalvaging: [ " + Statistics.FinishedSalvaging + "]", Logging.White);
            Logging.Log("Statistics", "Wealth before mission: [ " + QMCache.Instance.Wealth + "]", Logging.White);
            Logging.Log("Statistics", "Wealth after mission: [ " + QMCache.Instance.MyWalletBalance + "]", Logging.White);
            Logging.Log("Statistics", "Value of Loot from the mission: [" + Statistics.LootValue + "]", Logging.White);
            Logging.Log("Statistics", "Total LP after mission:  [" + AgentInteraction.Agent.LoyaltyPoints + "]", Logging.White);
            Logging.Log("Statistics", "Total LP before mission: [" + Statistics.LoyaltyPoints + "]", Logging.White);
            Logging.Log("Statistics", "LostDrones: [" + Statistics.LostDrones + "]", Logging.White);
            Logging.Log("Statistics", "DroneRecalls: [" + Statistics.DroneRecalls + "]", Logging.White);
            Logging.Log("Statistics", "AmmoConsumption: [" + Statistics.AmmoConsumption + "]", Logging.White);
            Logging.Log("Statistics", "AmmoValue: [" + Statistics.AmmoConsumption + "]", Logging.White);
            Logging.Log("Statistics", "Panic Attempts: [" + Statistics.PanicAttemptsThisMission + "]", Logging.White);
            Logging.Log("Statistics", "Lowest Shield %: [" + Math.Round(Statistics.LowestShieldPercentageThisMission, 0) + "]", Logging.White);
            Logging.Log("Statistics", "Lowest Armor %: [" + Math.Round(Statistics.LowestArmorPercentageThisMission, 0) + "]", Logging.White);
            Logging.Log("Statistics", "Lowest Capacitor %: [" + Math.Round(Statistics.LowestCapacitorPercentageThisMission, 0) + "]", Logging.White);
            Logging.Log("Statistics", "Repair Cycle Time: [" + Statistics.RepairCycleTimeThisMission + "]", Logging.White);
            Logging.Log("Statistics", "MissionXMLIsAvailable: [" + MissionSettings.MissionXMLIsAvailable + "]", Logging.White);
            Logging.Log("Statistics", "MissionCompletionerrors: [" + Statistics.MissionCompletionErrors + "]", Logging.White);
            Logging.Log("Statistics", "the stats below may not yet be correct and need some TLC", Logging.White);
            int weaponNumber = 0;
            foreach (ModuleCache weapon in QMCache.Instance.Weapons)
            {
                weaponNumber++;
                if (Time.Instance.ReloadTimePerModule != null && Time.Instance.ReloadTimePerModule.ContainsKey(weapon.ItemId))
                {
                    Logging.Log("Statistics", "Time Spent Reloading: [" + weaponNumber + "][" + Time.Instance.ReloadTimePerModule[weapon.ItemId] + "]", Logging.White);
                }
            }
            Logging.Log("Statistics", "Time Spent IN Mission: [" + TimeSpentInMission_seconds + "sec]", Logging.White);
            Logging.Log("Statistics", "Time Spent In Range: [" + TimeSpentInMissionInRange + "]", Logging.White);
            Logging.Log("Statistics", "Time Spent Out of Range: [" + TimeSpentInMissionOutOfRange + "]", Logging.White);

            if (MissionStats1Log)
            {
                if (!Directory.Exists(MissionStats1LogPath))
                {
                    Directory.CreateDirectory(MissionStats1LogPath);
                }

                // Write the header
                if (!File.Exists(MissionStats1LogFile))
                {
                    File.AppendAllText(MissionStats1LogFile, "Date;Mission;TimeMission;TimeSalvage;TotalTime;Isk;Loot;LP;\r\n");
                }

                // Build the line
                string line = DateTimeForLogs + ";";                                                                                        // Date
                line += MissionSettings.MissionName + ";";                                                                                   // Mission
                line += ((int)Statistics.FinishedMission.Subtract(Statistics.StartedMission).TotalMinutes) + ";";         // TimeMission
                line += ((int)Statistics.FinishedSalvaging.Subtract(Statistics.StartedSalvaging).TotalMinutes) + ";";     // Time Doing After Mission Salvaging
                line += ((int)DateTime.UtcNow.Subtract(Statistics.StartedMission).TotalMinutes) + ";";                             // Total Time doing Mission
                line += ((int)(QMCache.Instance.MyWalletBalance - QMCache.Instance.Wealth)) + ";";                                              // Isk (balance difference from start and finish of mission: is not accurate as the wallet ticks from bounty kills are every x minutes)
                line += Statistics.LootValue + ";";                                                                                // Loot
                line += (AgentInteraction.Agent.LoyaltyPoints - Statistics.LoyaltyPoints) + ";\r\n";                                 // LP

                // The mission is finished
                File.AppendAllText(MissionStats1LogFile, line);
                Logging.Log("Statistics", "writing mission log1 to  [ " + MissionStats1LogFile + " ]", Logging.White);

                //Logging.Log("Date;Mission;TimeMission;TimeSalvage;TotalTime;Isk;Loot;LP;");
                //Logging.Log(line);
            }
            if (MissionStats2Log)
            {
                if (!Directory.Exists(MissionStats2LogPath))
                {
                    Directory.CreateDirectory(MissionStats2LogPath);
                }

                // Write the header
                if (!File.Exists(MissionStats2LogFile))
                {
                    File.AppendAllText(MissionStats2LogFile, "Date;Mission;Time;Isk;Loot;LP;LostDrones;AmmoConsumption;AmmoValue\r\n");
                }

                // Build the line
                string line2 = string.Format("{0:MM/dd/yyyy HH:mm:ss}", DateTimeForLogs) + ";";                                      // Date
                line2 += MissionSettings.MissionName + ";";                                                                           // Mission
                line2 += ((int)Statistics.FinishedMission.Subtract(Statistics.StartedMission).TotalMinutes) + ";"; // TimeMission
                line2 += ((int)(QMCache.Instance.MyWalletBalance - QMCache.Instance.Wealth)) + ";";                                      // Isk
                line2 += Statistics.LootValue + ";";                                                                        // Loot
                line2 += (AgentInteraction.Agent.LoyaltyPoints - Statistics.LoyaltyPoints) + ";";                             // LP
                line2 += Statistics.LostDrones + ";";                                                                       // Lost Drones
                line2 += Statistics.AmmoConsumption + ";";                                                                  // Ammo Consumption
                line2 += Statistics.AmmoValue + ";\r\n";                                                                    // Ammo Value

                // The mission is finished
                Logging.Log("Statistics", "writing mission log2 to [ " + MissionStats2LogFile + " ]", Logging.White);
                File.AppendAllText(MissionStats2LogFile, line2);

                //Logging.Log("Date;Mission;Time;Isk;Loot;LP;LostDrones;AmmoConsumption;AmmoValue;");
                //Logging.Log(line2);
            }
            if (MissionStats3Log)
            {
                if (!Directory.Exists(MissionStats3LogPath))
                {
                    Directory.CreateDirectory(MissionStats3LogPath);
                }

                // Write the header
                if (!File.Exists(MissionStats3LogFile))
                {
                    File.AppendAllText(MissionStats3LogFile, "Date;Mission;Time;Isk;Loot;LP;DroneRecalls;LostDrones;AmmoConsumption;AmmoValue;Panics;LowestShield;LowestArmor;LowestCap;RepairCycles;AfterMissionsalvageTime;TotalMissionTime;MissionXMLAvailable;Faction;SolarSystem;DungeonID;OutOfDronesCount;\r\n");
                }

                // Build the line
                string line3 = DateTimeForLogs + ";";                                                                                        // Date
                line3 += MissionSettings.MissionName + ";";                                                                                   // Mission
                line3 += ((int)Statistics.FinishedMission.Subtract(Statistics.StartedMission).TotalMinutes) + ";";         // TimeMission
                line3 += ((long)(QMCache.Instance.MyWalletBalance - QMCache.Instance.Wealth)) + ";";                                             // Isk
                line3 += ((long)Statistics.LootValue) + ";";                                                                        // Loot
                line3 += ((long)AgentInteraction.Agent.LoyaltyPoints - Statistics.LoyaltyPoints) + ";";                               // LP
                line3 += Statistics.DroneRecalls + ";";                                                                             // Lost Drones
                line3 += "LostDrones:" + Statistics.LostDrones + ";";                                                               // Lost Drones
                line3 += Statistics.AmmoConsumption + ";";                                                                          // Ammo Consumption
                line3 += Statistics.AmmoValue + ";";                                                                                // Ammo Value
                line3 += "Panics:" + Statistics.PanicAttemptsThisMission + ";";                                                          // Panics
                line3 += ((int)Statistics.LowestShieldPercentageThisMission) + ";";                                                      // Lowest Shield %
                line3 += ((int)Statistics.LowestArmorPercentageThisMission) + ";";                                                       // Lowest Armor %
                line3 += ((int)Statistics.LowestCapacitorPercentageThisMission) + ";";                                                   // Lowest Capacitor %
                line3 += Statistics.RepairCycleTimeThisMission + ";";                                                                    // repair Cycle Time
                line3 += ((int)Statistics.FinishedSalvaging.Subtract(Statistics.StartedSalvaging).TotalMinutes) + ";";     // After Mission Salvaging Time
                line3 += ((int)Statistics.FinishedSalvaging.Subtract(Statistics.StartedSalvaging).TotalMinutes) + ((int)Statistics.FinishedMission.Subtract(Statistics.StartedMission).TotalMinutes) + ";"; // Total Time, Mission + After Mission Salvaging (if any)
                line3 += MissionSettings.MissionXMLIsAvailable.ToString(CultureInfo.InvariantCulture) + ";";
                line3 += MissionSettings.FactionName + ";";                                                                                   // FactionName that the mission is against
                line3 += QMCache.Instance.MissionSolarSystem + ";";                                                                            // SolarSystem the mission was located in
                line3 += QMCache.Instance.DungeonId + ";";                                                                                     // DungeonID - the unique identifier for this mission
                line3 += Statistics.OutOfDronesCount + ";";                                                                         // OutOfDronesCount - number of times we totally ran out of drones and had to go re-arm
                line3 += "\r\n";

                // The mission is finished
                Logging.Log("Statistics", "writing mission log3 to  [ " + MissionStats3LogFile + " ]", Logging.White);
                File.AppendAllText(MissionStats3LogFile, line3);

                //Logging.Log("Date;Mission;Time;Isk;Loot;LP;LostDrones;AmmoConsumption;AmmoValue;Panics;LowestShield;LowestArmor;LowestCap;RepairCycles;AfterMissionsalvageTime;TotalMissionTime;");
                //Logging.Log(line3);
            }
            if (MissionDungeonIdLog)
            {
                if (!Directory.Exists(MissionDungeonIdLogPath))
                {
                    Directory.CreateDirectory(MissionDungeonIdLogPath);
                }

                // Write the header
                if (!File.Exists(MissionDungeonIdLogFile))
                {
                    File.AppendAllText(MissionDungeonIdLogFile, "Mission;Faction;DungeonID;\r\n");
                }

                // Build the line
                string line4 = DateTimeForLogs + ";";              // Date
                line4 += MissionSettings.MissionName + ";";      // Mission
                line4 += MissionSettings.FactionName + ";";      // FactionName that the mission is against
                line4 += QMCache.Instance.DungeonId + ";";        // DungeonID - the unique identifier for this mission (parsed from the mission HTML)
                line4 += "\r\n";

                // The mission is finished
                Logging.Log("Statistics", "writing mission dungeonID log to  [ " + MissionDungeonIdLogFile + " ]", Logging.White);
                File.AppendAllText(MissionDungeonIdLogFile, line4);

                //Logging.Log("Date;Mission;Time;Isk;Loot;LP;LostDrones;AmmoConsumption;AmmoValue;Panics;LowestShield;LowestArmor;LowestCap;RepairCycles;AfterMissionsalvageTime;TotalMissionTime;");
                //Logging.Log(line3);
            }

            // Disable next log line
            Statistics.MissionLoggingCompleted = true;
            Statistics.LootValue = 0;
            Statistics.LoyaltyPoints = AgentInteraction.Agent.LoyaltyPoints;
            Statistics.StartedMission = DateTime.UtcNow;
            Statistics.FinishedMission = DateTime.UtcNow; //this may need to be reset to DateTime.MinValue, but that was causing other issues...
            MissionSettings.MissionName = string.Empty;
            Statistics.DroneRecalls = 0;
            Statistics.LostDrones = 0;
            Statistics.AmmoConsumption = 0;
            Statistics.AmmoValue = 0;
            Statistics.DroneLoggingCompleted = false;
            Statistics.MissionCompletionErrors = 0;
            Statistics.OutOfDronesCount = 0;
            foreach (ModuleCache weapon in QMCache.Instance.Weapons)
            {
                if (Time.Instance.ReloadTimePerModule != null && Time.Instance.ReloadTimePerModule.ContainsKey(weapon.ItemId))
                {
                    Time.Instance.ReloadTimePerModule[weapon.ItemId] = 0;
                }
            }

            Statistics.PanicAttemptsThisMission = 0;
            Statistics.LowestShieldPercentageThisMission = 101;
            Statistics.LowestArmorPercentageThisMission = 101;
            Statistics.LowestCapacitorPercentageThisMission = 101;
            Statistics.RepairCycleTimeThisMission = 0;
            Statistics.TimeSpentReloading_seconds = 0;             // this will need to be added to whenever we reload or switch ammo
            Statistics.TimeSpentInMission_seconds = 0;             // from landing on grid (loading mission actions) to going to base (changing to gotobase state)
            Statistics.TimeSpentInMissionInRange = 0;              // time spent totally out of range, no targets
            Statistics.TimeSpentInMissionOutOfRange = 0;           // time spent in range - with targets to kill (or no targets?!)
            QMCache.Instance.MissionSolarSystem = null;
            QMCache.Instance.DungeonId = "n/a";
            QMCache.Instance.OrbitEntityNamed = null;
        }

        public static void ProcessState()
        {
            switch (_States.CurrentStatisticsState)
            {
                case StatisticsState.Idle:
                    break;

                case StatisticsState.LogAllEntities:
                    if (!QMCache.Instance.InWarp)
                    {
                        _States.CurrentStatisticsState = StatisticsState.Idle;
                        Logging.Log("Statistics", "StatisticsState.LogAllEntities", Logging.Debug);
                        Statistics.LogEntities(QMCache.Instance.EntitiesOnGrid.ToList());
                    }
                    _States.CurrentStatisticsState = StatisticsState.Idle;
                    break;

                case StatisticsState.ListPotentialCombatTargets:
                    if (!QMCache.Instance.InWarp)
                    {
                        _States.CurrentStatisticsState = StatisticsState.Idle;
                        Logging.Log("Statistics", "StatisticsState.LogAllEntities", Logging.Debug);
                        Statistics.LogEntities(Combat.PotentialCombatTargets.Where(i => i.IsOnGridWithMe).ToList());
                    }
                    _States.CurrentStatisticsState = StatisticsState.Idle;
                    break;

                case StatisticsState.ListHighValueTargets:
                    if (!QMCache.Instance.InWarp)
                    {
                        _States.CurrentStatisticsState = StatisticsState.Idle;
                        Logging.Log("Statistics", "StatisticsState.LogAllEntities", Logging.Debug);
                        Statistics.LogEntities(Combat.PotentialCombatTargets.Where(i => i.IsHighValueTarget).ToList());
                    }
                    _States.CurrentStatisticsState = StatisticsState.Idle;
                    break;

                case StatisticsState.ListLowValueTargets:
                    if (!QMCache.Instance.InWarp)
                    {
                        _States.CurrentStatisticsState = StatisticsState.Idle;
                        Logging.Log("Statistics", "StatisticsState.LogAllEntities", Logging.Debug);
                        Statistics.LogEntities(Combat.PotentialCombatTargets.Where(i => i.IsLowValueTarget).ToList());
                    }
                    _States.CurrentStatisticsState = StatisticsState.Idle;
                    break;

                case StatisticsState.SessionLog:
                    _States.CurrentStatisticsState = StatisticsState.Idle;
                    break;

                case StatisticsState.ModuleInfo:
                    if (!QMCache.Instance.InWarp)
                    {
                        if (QMCache.Instance.InSpace || QMCache.Instance.InStation)
                        {
                            _States.CurrentStatisticsState = StatisticsState.Idle;
                            Logging.Log("Statistics", "StatisticsState.ModuleInfo", Logging.Debug);
                            Statistics.ModuleInfo(QMCache.Instance.Modules);
                        }
                    }
                    break;

                    //ListClassInstanceInfo

                case StatisticsState.ListClassInstanceInfo:
                    if (!QMCache.Instance.InWarp)
                    {
                        if (QMCache.Instance.InSpace)
                        {
                            _States.CurrentStatisticsState = StatisticsState.Idle;
                            Logging.Log("Statistics", "StatisticsState.ListClassInstanceInfo", Logging.Debug);
                            Statistics.ListClassInstanceInfo();
                        }
                    }
                    break;

                case StatisticsState.ListIgnoredTargets:
                    if (!QMCache.Instance.InWarp)
                    {
                        if (QMCache.Instance.InSpace)
                        {
                            _States.CurrentStatisticsState = StatisticsState.Idle;
                            Logging.Log("Statistics", "StatisticsState.ListIgnoredTargets", Logging.Debug);
                            Statistics.ListIgnoredTargets();
                        }
                    }
                    break;

                case StatisticsState.ListDronePriorityTargets:
                    if (!QMCache.Instance.InWarp)
                    {
                        if (QMCache.Instance.InSpace)
                        {
                            _States.CurrentStatisticsState = StatisticsState.Idle;
                            Logging.Log("Statistics", "StatisticsState.ListDronePriorityTargets", Logging.Debug);
                            Statistics.ListDronePriorityTargets(Drones.DronePriorityEntities);
                        }
                    }
                    break;

                case StatisticsState.ListTargetedandTargeting:
                    if (!QMCache.Instance.InWarp)
                    {
                        if (QMCache.Instance.InSpace)
                        {
                            _States.CurrentStatisticsState = StatisticsState.Idle;
                            Logging.Log("Statistics", "StatisticsState.ListTargetedandTargeting", Logging.Debug);
                            Statistics.ListTargetedandTargeting(QMCache.Instance.TotalTargetsandTargeting);
                        }
                    }
                    break;

                case StatisticsState.PocketObjectStatistics:
                    if (!QMCache.Instance.InWarp)
                    {
                        if (QMCache.Instance.EntitiesOnGrid.Any())
                        {
                            _States.CurrentStatisticsState = StatisticsState.Idle;
                            Logging.Log("Statistics", "StatisticsState.PocketObjectStatistics", Logging.Debug);
                            Statistics.PocketObjectStatistics(QMCache.Instance.EntitiesOnGrid.ToList(), true);
                        }
                    }
                    break;

                case StatisticsState.ListItemHangarItems:
                    if (QMCache.Instance.InStation && DateTime.UtcNow > Time.Instance.LastInSpace.AddSeconds(20))
                    {
                        _States.CurrentStatisticsState = StatisticsState.Idle;
                        Logging.Log("Statistics", "StatisticsState.ListItemHangarItems", Logging.Debug);
                        List<ItemCache> ItemsToList;
                        if (QMCache.Instance.ItemHangar != null && QMCache.Instance.ItemHangar.Items.Any())
                        {
                            ItemsToList = QMCache.Instance.ItemHangar.Items.Select(i => new ItemCache(i)).ToList();
                        }
                        else
                        {
                            ItemsToList = new List<ItemCache>();
                        }

                        Statistics.ListItems(ItemsToList);
                    }
                    break;

                case StatisticsState.ListLootHangarItems:
                    if (QMCache.Instance.InStation && DateTime.UtcNow > Time.Instance.LastInSpace.AddSeconds(20))
                    {
                        _States.CurrentStatisticsState = StatisticsState.Idle;
                        Logging.Log("Statistics", "StatisticsState.ListLootHangarItems", Logging.Debug);
                        List<ItemCache> ItemsToList;
                        if (QMCache.Instance.LootHangar != null && QMCache.Instance.LootHangar.Items.Any())
                        {
                            ItemsToList = QMCache.Instance.LootHangar.Items.Select(i => new ItemCache(i)).ToList();
                        }
                        else
                        {
                            ItemsToList = new List<ItemCache>();
                        }

                        Statistics.ListItems(ItemsToList);
                    }
                    break;

                case StatisticsState.ListLootContainerItems:
                    if (QMCache.Instance.InStation && DateTime.UtcNow > Time.Instance.LastInSpace.AddSeconds(20))
                    {
                        _States.CurrentStatisticsState = StatisticsState.Idle;
                        Logging.Log("Statistics", "StatisticsState.ListLootContainerItems", Logging.Debug);
                        List<ItemCache> ItemsToList;
                        if (QMCache.Instance.LootContainer != null && QMCache.Instance.LootContainer.Items.Any())
                        {
                            ItemsToList = QMCache.Instance.LootContainer.Items.Select(i => new ItemCache(i)).ToList();
                        }
                        else
                        {
                            ItemsToList = new List<ItemCache>();
                        }

                        Statistics.ListItems(ItemsToList);
                    }
                    break;


                case StatisticsState.Done:

                    //_lastStatisticsAction = DateTime.UtcNow;
                    _States.CurrentStatisticsState = StatisticsState.Idle;
                    break;

                default:

                    // Next state
                    _States.CurrentStatisticsState = StatisticsState.Idle;
                    break;
            }
        }
    }
}
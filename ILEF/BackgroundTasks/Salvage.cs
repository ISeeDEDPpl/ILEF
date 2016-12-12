// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------

namespace ILEF.BackgroundTasks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ILoveEVE.Framework;
    using global::ILEF.Caching;
    using global::ILEF.Combat;
    using global::ILEF.Logging;
    using global::ILEF.Lookup;
    using global::ILEF.States;

    public static class Salvage
    {
        static Salvage()
        {
            WreckBlackList = new List<int>();
            OpenedContainers = new Dictionary<long, DateTime>();
            //Interlocked.Increment(ref SalvageInstances);
        }

        private static DateTime _lastSalvageProcessState;
        public static Dictionary<long, DateTime> OpenedContainers;
        //public static int SalvageInstances;
        internal static List<ModuleCache> salvagers;
        private static List<EntityCache> wrecks;

        public static List<int> WreckBlackList { get; set; }
        public static bool WreckBlackListSmallWrecks { get; set; }
        public static bool WreckBlackListMediumWrecks { get; set; }
        public static bool OpenWrecks;
        public static bool MissionLoot;
        public static bool SalvageAll;
        public static bool CurrentlyShouldBeSalvaging;

        public static int? DedicatedSalvagerMaximumWreckTargets;
        public static int? _maximumWreckTargets { get; set; }

        public static int MaximumWreckTargets
        {
            get
            {
                if (DedicatedSalvagerMaximumWreckTargets != null)
                {
                    return (int)DedicatedSalvagerMaximumWreckTargets;
                }

                if (_maximumWreckTargets != null)
                {
                    return (int)_maximumWreckTargets;
                }

                return 1;
            }
            set
            {
                _maximumWreckTargets = value;
            }
        }

        public static bool? DedicatedSalvagerLootEverything { get; set; }
        public static bool? _lootEverything { get; set; }
        public static bool LootEverything
        {
            get
            {
                if (DedicatedSalvagerLootEverything != null)
                {
                    return (bool)DedicatedSalvagerLootEverything;
                }

                if (_lootEverything != null)
                {
                    return (bool)_lootEverything;
                }

                return false;
            }
            set
            {
                _lootEverything = value;
            }
        }
        public static int? DedicatedSalvagerReserveCargoCapacity { get; set; }
        public static int? _reserveCargoCapacity { get; set; }
        public static int ReserveCargoCapacity
        {
            get
            {
                if (DedicatedSalvagerReserveCargoCapacity != null)
                {
                    return (int)DedicatedSalvagerReserveCargoCapacity;
                }

                if (_reserveCargoCapacity != null)
                {
                    return (int)_reserveCargoCapacity;
                }

                return 1;
            }
            set
            {
                _reserveCargoCapacity = value;
            }
        }
        private static int ModuleNumber { get; set; }
        public static bool LootOnlyWhatYouCanWithoutSlowingDownMissionCompletion { get; set; }
        public static int TractorBeamMinimumCapacitor { get; set; }
        public static int SalvagerMinimumCapacitor { get; set; }
        public static bool DoNotDoANYSalvagingOutsideMissionActions { get; set; }
        public static bool LootItemRequiresTarget { get; set; }
        public static int MinimumWreckCount { get; set; }
        public static bool AfterMissionSalvaging { get; set; }
        public static bool UnloadLootAtStation { get; set; }
        public static bool UseGatesInSalvage { get; set; }
        public static int AgeofBookmarksForSalvageBehavior { get; set; } //in minutes
        public static int AgeofSalvageBookmarksToExpire { get; set; } //in minutes
        public static bool DeleteBookmarksWithNPC { get; set; }
        public static bool CreateSalvageBookmarks { get; set; }
        public static string CreateSalvageBookmarksIn { get; set; }
        public static bool SalvageMultipleMissionsinOnePass { get; set; }
        public static bool FirstSalvageBookmarksInSystem { get; set; }

        public static void MoveIntoRangeOfWrecks() // DO NOT USE THIS ANYWHERE EXCEPT A PURPOSEFUL SALVAGE BEHAVIOR! - if you use this while in combat it will make you go poof quickly.
        {
            //we cant move in bastion mode, do not try
            List<ModuleCache> bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
            if (bastionModules.Any(i => i.IsActive)) return;

            if (!QMCache.Instance.UnlootedContainers.Any())
            {
                Logging.Log("Salvage.NavigateIntorangeOfWrecks", "There are 0 UnlootedContainers left on the field: no UnlootedContainers to approach.", Logging.White);
                return;
            }

            EntityCache closestWreck = QMCache.Instance.UnlootedContainers.OrderBy(o => o.Distance).FirstOrDefault();
            if (closestWreck != null && (Math.Round(closestWreck.Distance, 0) > (int)Distances.SafeScoopRange))
            {
                if (QMCache.Instance.Approaching == null || QMCache.Instance.Approaching.Id != closestWreck.Id || QMCache.Instance.MyShipEntity.Velocity < 50)
                {
                    if (closestWreck.Distance > (int)Distances.WarptoDistance)
                    {
                        if (closestWreck.WarpTo())
                        {
                            Logging.Log("Salvage.NavigateIntorangeOfWrecks", "Warping to [" + Logging.Yellow + closestWreck.Name + Logging.White + "] which is [" + Logging.Yellow + Math.Round(closestWreck.Distance / 1000, 0) + Logging.White + "k away]", Logging.White);
                            return;
                        }

                        return;
                    }

                    if (closestWreck.Approach())
                    {
                        Logging.Log("Salvage.NavigateIntorangeOfWrecks", "Approaching [" + Logging.Yellow + closestWreck.Name + Logging.White + "] which is [" + Logging.Yellow + Math.Round(closestWreck.Distance / 1000, 0) + Logging.White + "k away]", Logging.White);
                        return;
                    }

                    return;
                }

                return;
            }

            if (closestWreck != null && (closestWreck.Distance <= (int)Distances.SafeScoopRange && QMCache.Instance.Approaching != null))
            {
                if (Time.Instance.NextApproachAction < DateTime.UtcNow)
                {
                    if (QMCache.Instance.MyShipEntity.Velocity != 0 && DateTime.UtcNow > Time.Instance.NextApproachAction)
                    {
                        NavigateOnGrid.StopMyShip();
                        Logging.Log("Salvage.NavigateIntorangeOfWrecks", "Stop ship, ClosestWreck [" + Logging.Yellow + Math.Round(closestWreck.Distance, 0) + Logging.White + "m] is within scooprange [" + (int)Distances.SafeScoopRange + "m] and we were approaching", Logging.White);
                        return;
                    }
                }
            }

            return;
        }

        private static void ActivateTractorBeams()
        {
            if (Time.Instance.NextTractorBeamAction > DateTime.UtcNow)
            {
                if (Logging.DebugTractorBeams) Logging.Log("Salvage.ActivateTractorBeams", "Debug: QMCache.Instance.NextTractorBeamAction is still in the future, waiting", Logging.Teal);
                return;
            }

            IEnumerable<ModuleCache> tractorBeams = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.TractorBeam).ToList();
            if (!tractorBeams.Any())
                return;

            if (QMCache.Instance.InMission && QMCache.Instance.InSpace && QMCache.Instance.ActiveShip.CapacitorPercentage < Salvage.TractorBeamMinimumCapacitor)
            {
                if (Logging.DebugTractorBeams) Logging.Log("ActivateTractorBeams", "Capacitor [" + Math.Round(QMCache.Instance.ActiveShip.CapacitorPercentage, 0) + "%] below [" + Salvage.TractorBeamMinimumCapacitor + "%] TractorBeamMinimumCapacitor", Logging.Red);
                return;
            }

            double tractorBeamRange = tractorBeams.Min(t => t.OptimalRange);
            wrecks = null;
            wrecks = QMCache.Instance.Targets.Where(t => (t.GroupId == (int)Group.Wreck || t.GroupId == (int)Group.CargoContainer) && t.Distance < tractorBeamRange).ToList();

            int tractorsProcessedThisTick = 0;
            ModuleNumber = 0;

            //
            // Deactivate tractorbeams
            //
            foreach (ModuleCache tractorBeam in tractorBeams)
            {
                ModuleNumber++;
                if (tractorBeam.IsActive)
                {
                    //tractorBeams.Remove(tractorBeam);
                    if (Logging.DebugTractorBeams) Logging.Log("ActivateTractorBeams.Deactivating", "[" + ModuleNumber + "] Tractorbeam is: IsActive [" + tractorBeam.IsActive + "]. Continue", Logging.Debug);
                    continue;
                }

                if (tractorBeam.InLimboState)
                {
                    //tractorBeams.Remove(tractorBeam);
                    if (Logging.DebugTractorBeams) Logging.Log("ActivateTractorBeams.Deactivating", "[" + ModuleNumber + "] Tractorbeam is: InLimboState [" + tractorBeam.InLimboState + "] IsDeactivating [" + tractorBeam.IsDeactivating + "] IsActivatable [" + tractorBeam.IsActivatable + "] IsOnline [" + tractorBeam.IsOnline + "] IsGoingOnline [" + tractorBeam.IsGoingOnline + "]. Continue", Logging.Debug);
                    continue;
                }

                //if ( !tractorBeam.IsActive && !tractorBeam.IsDeactivating)
                //    continue;

                EntityCache wreck = wrecks.FirstOrDefault(w => w.Id == tractorBeam.TargetId);

                //for  QMCache.Instance.UnlootedContainers.Contains()
                bool currentWreckUnlooted = false;

                if (Logging.DebugTractorBeams) Logging.Log("Salvage.ActivateTractorBeams.Deactivating", "MyShip.Velocity [" + Math.Round(QMCache.Instance.MyShipEntity.Velocity, 0) + "]", Logging.Teal);
                if (QMCache.Instance.MyShipEntity.Velocity > 300)
                {
                    if (Logging.DebugTractorBeams) Logging.Log("Salvage.ActivateTractorBeams.Deactivating", "if (QMCache.Instance.MyShip.Velocity > 300)", Logging.Teal);
                    if (QMCache.Instance.UnlootedContainers.Any(unlootedcontainer => tractorBeam.TargetId == unlootedcontainer.Id))
                    {
                        currentWreckUnlooted = true;
                        if (Logging.DebugTractorBeams) Logging.Log("Salvage.ActivateTractorBeams.Deactivating", "if (tractorBeam.TargetId == unlootedcontainer.Id) break;", Logging.Teal);
                    }
                }

                // If the wreck no longer exists, or its within loot range then disable the tractor beam
                // If the wreck no longer exist, beam should be deactivated automatically. Without our interaction.
                if (tractorBeam.IsActive)
                {
                    if (wreck == null || (wreck.Distance <= (int) Distances.SafeScoopRange && !currentWreckUnlooted && QMCache.Instance.MyShipEntity.Velocity < 300))
                    {
                        if (Logging.DebugTractorBeams)
                        {
                            if (wreck != null)
                            {
                                Logging.Log("Salvage.ActivateTractorBeams.Deactivating", "[" + ModuleNumber + "] Tractorbeam: IsActive [" + tractorBeam.IsActive + "] and the wreck [" + wreck.Name ?? "null" + "] is in SafeScoopRange [" + Math.Round(wreck.Distance / 1000, 0) + "]", Logging.Teal);
                            }
                            else
                            {
                                Logging.Log("Salvage.ActivateTractorBeams.Deactivating", "[" + ModuleNumber + "] Tractorbeam: IsActive [" + tractorBeam.IsActive + "] on what? wreck was null!", Logging.Teal);
                            }
                        }

                        //tractorBeams.Remove(tractorBeam);
                        if (tractorBeam.Click())
                        {
                            tractorsProcessedThisTick++;
                            Time.Instance.NextTractorBeamAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.SalvageDelayBetweenActions_milliseconds);
                            if (tractorsProcessedThisTick < QMSettings.Instance.NumberOfModulesToActivateInCycle)
                            {
                                if (Logging.DebugTractorBeams) Logging.Log("Salvage.ActivateTractorBeams.Deactivating", "[" + ModuleNumber + "] Tractorbeam: Process Next Tractorbeam", Logging.Teal);
                                continue;
                            }

                            if (Logging.DebugTractorBeams) Logging.Log("Salvage.ActivateTractorBeams.Deactivating", "[" + ModuleNumber + "] Tractorbeam: We have processed [" + QMSettings.Instance.NumberOfModulesToActivateInCycle + "] tractors this tick, return", Logging.Teal);
                            return;
                        }

                        continue;
                    }

                    continue;
                }

                wrecks.RemoveAll(w => w.Id == tractorBeam.TargetId);
            }


            //
            // Activate tractorbeams
            //
            int WreckNumber = 0;
            foreach (EntityCache wreck in wrecks.OrderByDescending(i => i.IsLootTarget))
            {
                WreckNumber++;
                // This velocity check solves some bugs where velocity showed up as 150000000m/s
                if ((int)wreck.Velocity != 0) //if the wreck is already moving assume we should not tractor it.
                {
                    if (Logging.DebugTractorBeams) Logging.Log("Salvage.ActivateTractorBeams.Activating", "[" + WreckNumber + "] Wreck [" + wreck.Name + "][" + wreck.MaskedId + "] is already moving: do not tractor a wreck that is moving", Logging.Debug);
                    continue;
                }

                // Is this wreck within range?
                if (wreck.Distance < (int)Distances.SafeScoopRange)
                {
                    continue;
                }

                if (!tractorBeams.Any()) return;

                foreach (ModuleCache tractorBeam in tractorBeams)
                {
                    ModuleNumber++;
                    if (tractorBeam.IsActive)
                    {
                        if (Logging.DebugTractorBeams) Logging.Log("Salvage.ActivateTractorBeams.Activating", "[" + WreckNumber + "][::" + ModuleNumber + "] _ Tractorbeam is: IsActive [" + tractorBeam.IsActive + "]. Continue", Logging.Debug);
                        continue;
                    }

                    if (tractorBeam.InLimboState)
                    {
                        if (Logging.DebugTractorBeams) Logging.Log("Salvage.ActivateTractorBeams.Activating", "[" + WreckNumber + "][::" + ModuleNumber + "] __ Tractorbeam is: InLimboState [" + tractorBeam.InLimboState + "] IsDeactivating [" + tractorBeam.IsDeactivating + "] IsActivatable [" + tractorBeam.IsActivatable + "] IsOnline [" + tractorBeam.IsOnline + "] IsGoingOnline [" + tractorBeam.IsGoingOnline + "] TargetId [" + tractorBeam.TargetId + "]. Continue", Logging.Debug);
                        continue;
                    }

                    //if (tractorBeam.TargetId != -1)
                    //{
                    //    if (Logging.DebugTractorBeams) Logging.Log("Salvage.ActivateTractorBeams.Activating", "[" + WreckNumber + "][::" + ModuleNumber + "] ___ Tractorbeam is: InLimboState [" + tractorBeam.InLimboState + "] IsDeactivating [" + tractorBeam.IsDeactivating + "] IsActivatable [" + tractorBeam.IsActivatable + "] IsOnline [" + tractorBeam.IsOnline + "] IsGoingOnline [" + tractorBeam.IsGoingOnline + "] TargetId [" + tractorBeam.TargetId + "]. Continue", Logging.Debug);
                    //    continue;
                    //}

                    //
                    // this tractor has already been activated at least once
                    //
                    if (Time.Instance.LastActivatedTimeStamp != null && Time.Instance.LastActivatedTimeStamp.ContainsKey(tractorBeam.ItemId))
                    {
                        if (Time.Instance.LastActivatedTimeStamp[tractorBeam.ItemId].AddSeconds(5) > DateTime.UtcNow)
                        {
                            continue;
                        }
                    }

                    if (tractorBeams.Any(i => i.TargetId == wreck.Id))
                    {
                        continue;
                    }

                    //tractorBeams.Remove(tractorBeam);
                    if (tractorBeam.Activate(wreck))
                    {
                        tractorsProcessedThisTick++;
                        Logging.Log("Salvage", "[" + WreckNumber + "][::" + ModuleNumber + "] Activating tractorbeam [" + ModuleNumber + "] on [" + wreck.Name + "][" + Math.Round(wreck.Distance / 1000, 0) + "k][" + wreck.MaskedId + "] IsWreckEmpty [" + wreck.IsWreckEmpty + "]", Logging.White);
                        Time.Instance.NextTractorBeamAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.SalvageDelayBetweenActions_milliseconds);
                        break; //we do not need any more tractors on this wreck
                    }

                    continue;
                }

                if (tractorsProcessedThisTick > QMSettings.Instance.NumberOfModulesToActivateInCycle)
                {
                    //
                    // if we have processed 'enough' wrecks this tick, return
                    //
                    return;
                }

                //
                // move on to the next wreck
                //
                continue;
            }

            return;
        }
        public static void ActivateSalvagers(IEnumerable<EntityCache> SalvageThese = null)
        {
            if (Time.Instance.NextSalvageAction > DateTime.UtcNow)
            {
                if (Logging.DebugSalvage) Logging.Log("Salvage.ActivateSalvagers", "Debug: QMCache.Instance.NextSalvageAction is still in the future, waiting", Logging.Teal);
                return;
            }

            salvagers = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Salvager).ToList();

            if (salvagers.Count == 0)
            {
                if (Logging.DebugSalvage) Logging.Log("Salvage.ActivateSalvagers", "Debug: if (salvagers.Count == 0)", Logging.Teal);
                return;
            }

            if (QMCache.Instance.InMission && QMCache.Instance.InSpace && QMCache.Instance.ActiveShip.CapacitorPercentage < Salvage.TractorBeamMinimumCapacitor)
            {
                if (Logging.DebugSalvage) Logging.Log("ActivateSalvagers", "Capacitor [" + Math.Round(QMCache.Instance.ActiveShip.CapacitorPercentage, 0) + "%] below [" + Salvage.SalvagerMinimumCapacitor + "%] SalvagerMinimumCapacitor", Logging.Red);
                return;
            }

            double salvagerRange = salvagers.Min(s => s.OptimalRange);
            if (SalvageThese == null)
            {
                wrecks = QMCache.Instance.Targets.Where(t => t.GroupId == (int)Group.Wreck && t.Distance < salvagerRange && WreckBlackList.All(a => a != t.TypeId)).ToList();
            }
            else
            {
                wrecks = SalvageThese.Where(i => i.Distance < salvagers.Min(s => s.OptimalRange)).ToList();
            }

            if (Salvage.SalvageAll)
            {
                wrecks = QMCache.Instance.Targets.Where(t => t.GroupId == (int)Group.Wreck && t.Distance < salvagerRange).ToList();
            }

            if (wrecks.Count == 0)
            {
                if (Logging.DebugSalvage) Logging.Log("Salvage.ActivateSalvagers", "Debug: if (wrecks.Count == 0)", Logging.Teal);
                return;
            }

            //
            // Activate
            //
            int salvagersProcessedThisTick = 0;
            int WreckNumber = 0;
            foreach (EntityCache wreck in wrecks.OrderByDescending(i => i.IsLootTarget))
            {
                WreckNumber++;
                foreach (ModuleCache salvager in salvagers)
                {
                    ModuleNumber++;
                    if (salvager.IsActive)
                    {
                        if (Logging.DebugSalvage) Logging.Log("Salvage.ActivateSalvagers.Activating", "[" + WreckNumber + "][::" + ModuleNumber + "] _ Salvager is: IsActive [" + salvager.IsActive + "]. Continue", Logging.Debug);
                        continue;
                    }

                    if (salvager.InLimboState)
                    {
                        if (Logging.DebugSalvage) Logging.Log("Salvage.ActivateSalvagers.Activating", "[" + WreckNumber + "][::" + ModuleNumber + "] __ Salvager is: InLimboState [" + salvager.InLimboState + "] IsDeactivating [" + salvager.IsDeactivating + "] IsActivatable [" + salvager.IsActivatable + "] IsOnline [" + salvager.IsOnline + "] IsGoingOnline [" + salvager.IsGoingOnline + "] TargetId [" + salvager.TargetId + "]. Continue", Logging.Debug);
                        continue;
                    }

                    //
                    // this tractor has already been activated at least once
                    //
                    if (Time.Instance.LastActivatedTimeStamp != null && Time.Instance.LastActivatedTimeStamp.ContainsKey(salvager.ItemId))
                    {
                        if (Time.Instance.LastActivatedTimeStamp[salvager.ItemId].AddSeconds(5) > DateTime.UtcNow)
                        {
                            continue;
                        }
                    }

                    //
                    // if we have more wrecks on the field then we have salvagers that have not yet been activated
                    //
                    if (wrecks.Count() >= salvagers.Count(i => !i.IsActive))
                    {
                        if (Logging.DebugSalvage) Logging.Log("Salvage.ActivateSalvagers", "We have [" + wrecks.Count() + "] wrecks  and [" + salvagers.Count(i => !i.IsActive) + "] available salvagers of [" + salvagers.Count() + "] total", Logging.Teal);
                        //
                        // skip activating any more salvagers on this wreck that already has at least 1 salvager on it.
                        //
                        if (salvagers.Any(i => i.IsActive && i.LastTargetId == wreck.Id))
                        {
                            if (Logging.DebugSalvage) Logging.Log("Salvage.ActivateSalvagers", "Not assigning another salvager to wreck [" + wreck.Name + "][" + wreck.MaskedId + "]at[" + Math.Round(wreck.Distance / 1000, 0) + "k] as it already has at least 1 salvager active", Logging.Teal);
                            //
                            // Break out of the Foreach salvager in salvagers and continue to the next wreck
                            //
                            break;
                        }
                    }

                    Logging.Log("Salvage", "Activating salvager [" + ModuleNumber + "] on [" + wreck.Name + "][ID: " + wreck.MaskedId + "] we have [" + wrecks.Count() + "] wrecks targeted in salvager range", Logging.White);
                    if (salvager.Activate(wreck))
                    {
                        salvagersProcessedThisTick++;
                        Time.Instance.NextSalvageAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.SalvageDelayBetweenActions_milliseconds);
                        if (salvagersProcessedThisTick < QMSettings.Instance.NumberOfModulesToActivateInCycle)
                        {
                            if (Logging.DebugSalvage) Logging.Log("Salvage.ActivateSalvagers", "Debug: if (salvagersProcessedThisTick < QMSettings.Instance.NumberOfModulesToActivateInCycle)", Logging.Teal);
                            continue;
                        }

                        //
                        // return, no more processing this tick
                        //
                        return;
                    }

                    //
                    // move on to the next salvager
                    //
                    continue;
                }

                //
                // move on to the next wreck
                //
                continue;
            }
        }

        /// <summary>
        ///   Target wrecks within range
        /// </summary>
        public static void TargetWrecks(IEnumerable<EntityCache> TargetTheseEntities = null)
        {
            // We are jammed, we do not need to log (Combat does this already)
            if (QMCache.Instance.MaxLockedTargets == 0 || QMCache.Instance.Targets.Any() && QMCache.Instance.Targets.Count() >= QMCache.Instance.MaxLockedTargets)
            {
                if (Logging.DebugTargetWrecks) Logging.Log("Salvage.TargetWrecks", "Debug: if (QMCache.Instance.MaxLockedTargets == 0) || QMCache.Instance.Targets.Any() && Cache.Instance.Targets.Count() >= Cache.Instance.MaxLockedTargets", Logging.Teal);
                return;
            }

            List<ModuleCache> tractorBeams = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.TractorBeam).ToList();

            List<EntityCache> targets = new List<EntityCache>();
            targets.AddRange(QMCache.Instance.Targets);
            targets.AddRange(QMCache.Instance.Targeting);

            bool hasSalvagers = QMCache.Instance.Modules.Any(m => m.GroupId == (int)Group.Salvager);
            List<EntityCache> wreckTargets = targets.Where(t => (t.GroupId == (int)Group.Wreck || t.GroupId == (int)Group.CargoContainer) && t.CategoryId == (int)CategoryID.Celestial).ToList();

            //
            // UnTarget Wrecks/Containers, etc as they get in range
            //
            foreach (EntityCache wreck in wreckTargets.OrderByDescending(i => i.IsLootTarget))
            {
                if (!hasSalvagers)
                {
                    if (wreck.IsWreckEmpty) //this only returns true if it is a wreck, not for cargo containers, spawn containers, etc.
                    {
                        Logging.Log("Salvage", "Wreck: [" + wreck.Name + "][" + Math.Round(wreck.Distance / 1000, 0) + "k][ID: " + wreck.MaskedId + "] wreck is empty, unlocking container.", Logging.White);
                        QMCache.Instance.LootedContainers.Add(wreck.Id);
                        wreck.UnlockTarget("Salvage");
                        continue;
                    }
                }

                if (!SalvageAll)
                {
                    if (WreckBlackList.Any(a => a == wreck.TypeId))
                    {
                        Logging.Log("Salvage", "Cargo Container [" + wreck.Name + "][" + Math.Round(wreck.Distance / 1000, 0) + "k][ID: " + wreck.MaskedId + "] wreck is on our blacklist, unlocking container.", Logging.White);
                        QMCache.Instance.LootedContainers.Add(wreck.Id);
                        wreck.UnlockTarget("Salvage");
                        continue;
                    }
                }

                if (hasSalvagers && wreck.GroupId != (int)Group.CargoContainer)
                {
                    if (Logging.DebugTargetWrecks) Logging.Log("Salvage.TargetWrecks", "Debug: if (hasSalvagers && wreck.GroupId != (int)Group.CargoContainer))", Logging.Teal);
                    continue;
                }

                // Unlock if within loot range
                if (wreck.Distance < (int)Distances.SafeScoopRange)
                {
                    Logging.Log("Salvage", "Cargo Container [" + wreck.Name + "][" + Math.Round(wreck.Distance / 1000, 0) + "k][ID: " + wreck.MaskedId + "] within loot range, unlocking container.", Logging.White);
                    wreck.UnlockTarget("Salvage");
                    continue;
                }
            }

            if (MissionLoot)
            {
                if (wreckTargets.Count >= QMCache.Instance.MaxLockedTargets)
                {
                    if (Logging.DebugTargetWrecks) Logging.Log("Salvage.TargetWrecks", "Debug: if (wreckTargets.Count >= QMCache.Instance.MaxLockedTargets)", Logging.Teal);
                    return;
                }
            }
            else if ((wreckTargets.Count >= MaximumWreckTargets && Combat.TargetedBy.Any()) || QMCache.Instance.Targets.Count() >= QMCache.Instance.MaxLockedTargets)
            {
                if (Logging.DebugTargetWrecks) Logging.Log("Salvage.TargetWrecks", "Debug: else if (wreckTargets.Count >= MaximumWreckTargets)", Logging.Teal);
                return;
            }

            double tractorBeamRange = 0;
            if (tractorBeams.Count > 0)
            {
                tractorBeamRange = tractorBeams.Min(t => t.OptimalRange);
            }

            if (!OpenWrecks)
            {
                if (Logging.DebugTargetWrecks) Logging.Log("Salvage.TargetWrecks", "Debug: OpenWrecks is false, we do not need to target any wrecks.", Logging.Teal);
                return;
            }

            //
            // TargetWrecks/Container, etc If needed
            //
            int wrecksProcessedThisTick = 0;
            IEnumerable<EntityCache> AttemptToTargetThese = null;
            if (TargetTheseEntities != null)
            {
                AttemptToTargetThese = TargetTheseEntities;
            }
            else
            {
                AttemptToTargetThese = QMCache.Instance.UnlootedContainers;
            }

            foreach (EntityCache wreck in AttemptToTargetThese.OrderByDescending(i => i.IsLootTarget))
            {
                // Its already a target, ignore it
                if (wreck.IsTarget || wreck.IsTargeting)
                {
                    if (Logging.DebugTargetWrecks) Logging.Log("Salvage.TargetWrecks", "Debug: if (wreck.IsTarget || wreck.IsTargeting)", Logging.Teal);
                    continue;
                }

                if (wreck.Distance > tractorBeamRange)
                {
                    if (Logging.DebugTargetWrecks) Logging.Log("Salvage.TargetWrecks", "Debug: if (wreck.Distance > tractorBeamRange)", Logging.Teal);
                    continue;
                }

                if (!wreck.HaveLootRights || TargetTheseEntities != null)
                {
                    if (Logging.DebugTargetWrecks) Logging.Log("Salvage.TargetWrecks", "Debug: if (!wreck.HaveLootRights)", Logging.Teal);
                    continue;
                }

                // No need to tractor a non-wreck within loot range
                if (wreck.GroupId != (int)Group.Wreck && wreck.Distance < (int)Distances.SafeScoopRange)
                {
                    if (Logging.DebugTargetWrecks) Logging.Log("Salvage.TargetWrecks", "Debug: if (wreck.GroupId != (int)Group.Wreck && wreck.Distance < (int)Distance.SafeScoopRange)", Logging.Teal);
                    continue;
                }

                if (!SalvageAll)
                {
                    //
                    // do not tractor blacklisted wrecks
                    //
                    if (WreckBlackList.Any(a => a == wreck.TypeId) && !QMCache.Instance.ListofContainersToLoot.Contains(wreck.Id))
                    {
                        QMCache.Instance.LootedContainers.Add(wreck.Id);
                        if (Logging.DebugTargetWrecks) Logging.Log("Salvage.TargetWrecks", "Debug: if (QMSettings.Instance.WreckBlackList.Any(a => a == wreck.TypeId)", Logging.Teal);
                        continue;
                    }
                }

                if (wreck.GroupId != (int)Group.Wreck && wreck.GroupId != (int)Group.CargoContainer)
                {
                    if (Logging.DebugTargetWrecks) Logging.Log("Salvage.TargetWrecks", "Debug: if (wreck.GroupId != (int)Group.Wreck && wreck.GroupId != (int)Group.CargoContainer)", Logging.Teal);
                    continue;
                }

                if (!hasSalvagers)
                {
                    // Ignore already looted wreck
                    if (QMCache.Instance.LootedContainers.Contains(wreck.Id))
                    {
                        if (Logging.DebugTargetWrecks) Logging.Log("Salvage.TargetWrecks", "Debug: Ignoring Already Looted Entity ID [" + wreck.Id + "]", Logging.Teal);
                        continue;
                    }

                    // Ignore empty wrecks
                    if (wreck.IsWreckEmpty) //this only returns true if it is a wreck, not for cargo containers, spawn containers, etc.
                    {
                        QMCache.Instance.LootedContainers.Add(wreck.Id);
                        if (Logging.DebugTargetWrecks) Logging.Log("Salvage.TargetWrecks", "Debug: Ignoring Empty Entity ID [" + wreck.Id + "]", Logging.Teal);
                        continue;
                    }

                    // Ignore wrecks already in loot range
                    if (wreck.Distance < (int)Distances.SafeScoopRange)
                    {
                        if (Logging.DebugTargetWrecks) Logging.Log("Salvage.TargetWrecks", "Debug: Ignoring Entity that is already in loot range ID [" + wreck.Id + "]", Logging.Teal);
                        continue;
                    }
                }

                if (wreck.LockTarget("Salvage"))
                {
                    Logging.Log("Salvage", "Locking [" + wreck.Name + "][" + Math.Round(wreck.Distance / 1000, 0) + "k][ID: " + wreck.MaskedId + "][" + Math.Round(wreck.Distance / 1000, 0) + "k away]", Logging.White);
                    wreckTargets.Add(wreck);
                    wrecksProcessedThisTick++;
                    if (Logging.DebugSalvage) Logging.Log("Salvage", "wrecksProcessedThisTick [" + wrecksProcessedThisTick + "]", Logging.Teal);

                    if (MissionLoot)
                    {
                        if (wreckTargets.Count >= QMCache.Instance.MaxLockedTargets)
                        {
                            if (Logging.DebugTargetWrecks) Logging.Log("Salvage", " wreckTargets.Count [" + wreckTargets.Count + "] >= QMCache.Instance.MaxLockedTargets) [" + QMCache.Instance.MaxLockedTargets + "]", Logging.Teal);
                            return;
                        }
                    }
                    else
                    {
                        if (wreckTargets.Count >= MaximumWreckTargets)
                        {
                            if (Logging.DebugTargetWrecks) Logging.Log("Salvage", " wreckTargets.Count [" + wreckTargets.Count + "] >= MaximumWreckTargets [" + MaximumWreckTargets + "]", Logging.Teal);
                            return;
                        }
                    }

                    if (wrecksProcessedThisTick < QMSettings.Instance.NumberOfModulesToActivateInCycle)
                    {
                        if (Logging.DebugTargetWrecks) Logging.Log("Salvage", "if (wrecksProcessedThisTick [" + wrecksProcessedThisTick + "] < QMSettings.Instance.NumberOfModulesToActivateInCycle [" + QMSettings.Instance.NumberOfModulesToActivateInCycle + "])", Logging.Teal);
                        continue;
                    }
                }

                return;
            }
        }

        /// <summary>
        ///   Loot any wrecks & cargo containers close by
        /// </summary>
        private static void LootWrecks()
        {
            try
            {
                if (Time.Instance.NextLootAction > DateTime.UtcNow)
                {
                    if (Logging.DebugLootWrecks) Logging.Log("Salvage", "Debug: QMCache.Instance.NextLootAction is still in the future, waiting", Logging.Teal);
                    return;
                }

                //
                // when full return to base and unloadloot
                //
                if (UnloadLootAtStation && QMCache.Instance.CurrentShipsCargo != null && QMCache.Instance.CurrentShipsCargo.Capacity > 150 && (QMCache.Instance.CurrentShipsCargo.Capacity - QMCache.Instance.CurrentShipsCargo.UsedCapacity) < 50)
                {
                    if (_States.CurrentCombatMissionBehaviorState == CombatMissionsBehaviorState.ExecuteMission)
                    {
                        if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "(mission) We are full, heading back to base to dump loot ", Logging.Teal);
                        _States.CurrentCombatHelperBehaviorState = States.CombatHelperBehaviorState.GotoBase;
                        return;
                    }

                    if (_States.CurrentDedicatedBookmarkSalvagerBehaviorState == States.DedicatedBookmarkSalvagerBehaviorState.Salvage)
                    {
                        if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "(salvage) We are full, heading back to base to dump loot ", Logging.Teal);
                        _States.CurrentDedicatedBookmarkSalvagerBehaviorState = DedicatedBookmarkSalvagerBehaviorState.GotoBase;
                        Time.Instance.NextSalvageTrip = DateTime.UtcNow;
                        return;
                    }

                    Logging.Log("Salvage.LootWrecks", "We are full: we are using a behavior that does not have a supported place to auto dump loot: error!", Logging.Orange);
                    return;
                }

                // Open a container in range
                int containersProcessedThisTick = 0;

                if (Logging.DebugLootWrecks)
                {
                    int containersInRangeCount = 0;
                    if (QMCache.Instance.Containers.Any(i => i.Distance < (double)Distances.ScoopRange))
                    {
                        containersInRangeCount = QMCache.Instance.Containers.Count(i => i.Distance < (double)Distances.ScoopRange);
                    }

                    List<EntityCache> containersOutOfRange = QMCache.Instance.Containers.Where(e => e.Distance >= (int)Distances.SafeScoopRange).ToList();
                    int containersOutOfRangeCount = 0;
                    if (containersOutOfRange.Any())
                    {
                        containersOutOfRangeCount = containersOutOfRange.Count();
                    }

                    Logging.Log("Salvage", "Debug: containersInRange count [" + containersInRangeCount + "]", Logging.Teal);
                    Logging.Log("Salvage", "Debug: containersOutOfRange count [" + containersOutOfRangeCount + "]", Logging.Teal);
                }

                if (QMCache.Instance.CurrentShipsCargo == null)
                {
                    if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "if (QMCache.Instance.CurrentShipsCargo == null)", Logging.Teal);
                    return;
                }


                List<ItemCache> shipsCargo = new List<ItemCache>();
                double freeCargoCapacity = 0;
                if (QMCache.Instance.CurrentShipsCargo.Items.Any())
                {
                    shipsCargo = QMCache.Instance.CurrentShipsCargo.Items.Select(i => new ItemCache(i)).ToList();
                    freeCargoCapacity = QMCache.Instance.CurrentShipsCargo.Capacity - QMCache.Instance.CurrentShipsCargo.UsedCapacity;
                }
                else
                {
                    freeCargoCapacity = QMCache.Instance.CurrentShipsCargo.Capacity;
                    if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "if (!QMCache.Instance.CurrentShipsCargo.Items.Any()) - really? 0 items in cargo?", Logging.Teal);
                }

                foreach (EntityCache containerEntity in QMCache.Instance.Containers.Where(e => e.Distance <= (int)Distances.SafeScoopRange).OrderByDescending(i => i.IsLootTarget))
                {
                    containersProcessedThisTick++;

                    // Empty wreck, ignore
                    if (containerEntity.IsWreckEmpty) //this only returns true if it is a wreck, not for cargo containers, spawn containers, etc.
                    {
                        QMCache.Instance.LootedContainers.Add(containerEntity.Id);
                        if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "Ignoring Empty Wreck", Logging.Teal);
                        continue;
                    }

                    // We looted this container
                    if (QMCache.Instance.LootedContainers.Contains(containerEntity.Id))
                    {
                        if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "We have already looted [" + containerEntity.Id + "]", Logging.White);
                        continue;
                    }

                    // Ignore open request within 10 seconds
                    if (OpenedContainers.ContainsKey(containerEntity.Id) && DateTime.UtcNow.Subtract(OpenedContainers[containerEntity.Id]).TotalSeconds < 10)
                    {
                        if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "We attempted to open [" + containerEntity.Id + "] less than 10 sec ago, ignoring", Logging.White);
                        continue;
                    }

                    // Don't even try to open a wreck if you are speed tanking and you are not processing a loot action
                    if (NavigateOnGrid.SpeedTank && !QMCache.Instance.MyShipEntity.IsBattleship && OpenWrecks == false)
                    {
                        if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "SpeedTank is true and OpenWrecks is false [" + containerEntity.Id + "]", Logging.White);
                        continue;
                    }

                    // Don't even try to open a wreck if you are specified LootEverything as false and you are not processing a loot action
                    //      this is currently commented out as it would keep Golems and other non-speed tanked ships from looting the field as they cleared
                    //      missions, but NOT stick around after killing things to clear it ALL. Looteverything==false does NOT mean loot nothing
                    //if (QMSettings.Instance.LootEverything == false && QMCache.Instance.OpenWrecks == false)
                    //    continue;

                    // Open the container
                    QMCache.Instance.ContainerInSpace = QMCache.Instance.DirectEve.GetContainer(containerEntity.Id);
                    if (QMCache.Instance.ContainerInSpace == null)
                    {
                        if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "if (QMCache.Instance.ContainerInSpace == null)", Logging.White);
                        continue;
                    }

                    if (QMCache.Instance.ContainerInSpace.Window == null)
                    {
                        if (containerEntity.OpenCargo())
                        {
                            if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "if (containerEntity.OpenCargo())", Logging.White);
                            Time.Instance.NextLootAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.LootingDelay_milliseconds);
                        }

                        if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "if (QMCache.Instance.ContainerInSpace.Window == null)", Logging.White);

                        return;
                    }

                    if (!QMCache.Instance.ContainerInSpace.Window.IsReady)
                    {
                        if (Logging.DebugLootWrecks) Logging.Log("Salvage", "LootWrecks: QMCache.Instance.ContainerInSpace.Window is not ready", Logging.White);
                        return;
                    }

                    if (QMCache.Instance.ContainerInSpace.Window.IsReady)
                    {
                        Logging.Log("Salvage", "Opened container [" + containerEntity.Name + "][" + Math.Round(containerEntity.Distance / 1000, 0) + "k][ID: " + containerEntity.MaskedId + "]", Logging.White);
                        if (Logging.DebugLootWrecks) Logging.Log("Salvage", "LootWrecks: QMCache.Instance.ContainerInSpace.Window is ready", Logging.White);
                        OpenedContainers[containerEntity.Id] = DateTime.UtcNow;
                        Time.Instance.NextLootAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.LootingDelay_milliseconds);

                        // List its items
                        IEnumerable<ItemCache> items = QMCache.Instance.ContainerInSpace.Items.Select(i => new ItemCache(i)).ToList();
                        if (Logging.DebugLootWrecks && items.Any()) Logging.Log("Salvage.LootWrecks", "Found [" + items.Count() + "] items in [" + containerEntity.Name + "][" + Math.Round(containerEntity.Distance / 1000, 0) + "k][" + containerEntity.MaskedId + "]", Logging.Teal);

                        // Build a list of items to loot
                        List<ItemCache> lootItems = new List<ItemCache>();

                        // log wreck contents to file
                        if (!Statistics.WreckStatistics(items, containerEntity)) break;

                        if (items.Any())
                        {
                            // Walk through the list of items ordered by highest value item first
                            foreach (ItemCache item in items.OrderByDescending(i => i.IsContraband).ThenByDescending(i => i.IskPerM3))
                            {
                                if (freeCargoCapacity < 1000) //this should allow BSs to not pickup large low value items but haulers and noctis' to scoop everything
                                {
                                    // We never want to pick up a cap booster
                                    if (item.GroupID == (int)Group.CapacitorGroupCharge)
                                    {
                                        continue;
                                    }
                                }

                                // We pick up loot depending on isk per m3
                                bool _isMissionItem = MissionSettings.MissionItems.Contains((item.Name ?? string.Empty).ToLower());
                                if (MissionSettings.MissionItems.Any())
                                {
                                    if (QMSettings.Instance.FleetSupportSlave && !QMSettings.Instance.FleetSupportMaster)
                                    {
                                        if (QMCache.Instance.ListofMissionCompletionItemsToLoot.Contains(item.Name) || item.IsMissionItem)
                                        {
                                            if (Logging.DebugFleetSupportSlave) Logging.Log("Salvage.LootWrecks", "[" + item.Name + "] is an item specified in a lootitem action. Do not loot it here. Let the Master loot this.", Logging.Teal);
                                            QMCache.Instance.LootedContainers.Add(containerEntity.Id);
                                            continue;
                                        }
                                    }
                                }


                                // Never pick up contraband (unless its the mission item)
                                if (item.IsContraband) //is the mission item EVER contraband?!
                                {
                                    if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "[" + item.Name + "] is not the mission item and is considered Contraband: ignore it", Logging.Teal);
                                    QMCache.Instance.LootedContainers.Add(containerEntity.Id);
                                    continue;
                                }

                                if (!Salvage.LootOnlyWhatYouCanWithoutSlowingDownMissionCompletion)
                                {
                                    // Do we want to loot other items?
                                    if (!_isMissionItem && !LootEverything)
                                    {
                                        continue;
                                    }
                                }

                                try
                                {
                                    // We are at our max, either make room or skip the item
                                    if ((freeCargoCapacity - item.TotalVolume) <= (item.IsMissionItem ? 0 : ReserveCargoCapacity))
                                    {
                                        Logging.Log("Salvage.LootWrecks", "We Need More m3: FreeCargoCapacity [" + freeCargoCapacity + "] - [" + item.Name + "][" + item.TotalVolume + "total][" + item.Volume + "each]", Logging.Debug);

                                        // Make a list of items which are worth less
                                        List<ItemCache> worthLess = null;
                                        if (_isMissionItem)
                                        {
                                            worthLess = shipsCargo;
                                        }
                                        else if (item.IskPerM3.HasValue)
                                        {
                                            worthLess = shipsCargo.Where(sc => sc.IskPerM3.HasValue && sc.IskPerM3 < item.IskPerM3).ToList();
                                        }
                                        else
                                        {
                                            worthLess = shipsCargo.Where(sc => !sc.IsMissionItem && sc.IskPerM3.HasValue).ToList();
                                        }

                                        if (_States.CurrentQuestorState == QuestorState.CombatMissionsBehavior)
                                        {
                                            // Remove mission item from this list
                                            worthLess.RemoveAll(wl => MissionSettings.MissionItems.Contains((wl.Name ?? string.Empty).ToLower()));
                                            if (!string.IsNullOrEmpty(MissionSettings.BringMissionItem))
                                            {
                                                worthLess.RemoveAll(wl => (wl.Name ?? string.Empty).ToLower() == MissionSettings.BringMissionItem.ToLower());
                                            }

                                            // Consider dropping ammo if it concerns the mission item!
                                            if (!_isMissionItem)
                                            {
                                                worthLess.RemoveAll(wl => Combat.Ammo.Any(a => a.TypeId == wl.TypeId));
                                            }
                                        }

                                        // Nothing is worth less then the current item
                                        if (!worthLess.Any())
                                        {
                                            if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "[" + item.Name + "] ::: if (!worthLess.Any()) continue ", Logging.Teal);
                                            continue;
                                        }

                                        // Not enough space even if we dumped the crap
                                        if ((freeCargoCapacity + worthLess.Sum(wl => wl.TotalVolume)) < item.TotalVolume)
                                        {
                                            if (item.IsMissionItem)
                                            {
                                                Logging.Log("Salvage", "Not enough space for [" + item.Name + "] Need [" + item.TotalVolume + "] maximum available [" + (freeCargoCapacity + worthLess.Sum(wl => wl.TotalVolume)) + "]", Logging.White);
                                                //
                                                // partially loot the mission item if possible.
                                                //

                                            }
                                            continue;
                                        }

                                        // Start clearing out items that are worth less
                                        List<DirectItem> moveTheseItems = new List<DirectItem>();
                                        foreach (ItemCache wl in worthLess.OrderBy(wl => wl.IskPerM3.HasValue ? wl.IskPerM3.Value : double.MaxValue).ThenByDescending(wl => wl.TotalVolume))
                                        {
                                            // Mark this item as moved
                                            moveTheseItems.Add(wl.DirectItem);

                                            // Subtract (now) free volume
                                            freeCargoCapacity += wl.TotalVolume;

                                            // We freed up enough space?
                                            if ((freeCargoCapacity - item.TotalVolume) >= ReserveCargoCapacity)
                                            {
                                                break;
                                            }
                                        }

                                        if (moveTheseItems.Count > 0)
                                        {
											Logging.Log("Salvage","We are full, not enough room for the mission item. Heading back to base to dump loot.",Logging.Debug);
                                            //GotoBase and dump loot in the hopes that we can grab what we need on the next run
                                            if (_States.CurrentQuestorState == QuestorState.CombatMissionsBehavior) _States.CurrentCombatMissionBehaviorState = CombatMissionsBehaviorState.GotoBase;
                                            if (_States.CurrentQuestorState == QuestorState.CombatHelperBehavior) _States.CurrentCombatHelperBehaviorState = CombatHelperBehaviorState.GotoBase;
                                            if (_States.CurrentQuestorState == QuestorState.DedicatedBookmarkSalvagerBehavior) _States.CurrentDedicatedBookmarkSalvagerBehaviorState = DedicatedBookmarkSalvagerBehaviorState.GotoBase;
											/**
                                            // jettison loot
                                            if (DateTime.UtcNow.Subtract(Time.Instance.LastJettison).TotalSeconds < Time.Instance.DelayBetweenJetcans_seconds)
                                            {
                                                return;
                                            }

                                            Logging.Log("Salvage", "Jettisoning [" + moveTheseItems.Count + "] items to make room for the more valuable loot", Logging.White);

                                            // Note: This could (in theory) fuck up with the bot jettison an item and
                                            // then picking it up again :/ (granted it should never happen unless
                                            // mission item volume > reserved volume
                                            Cache.Instance.CurrentShipsCargo.Jettison(moveTheseItems.Select(i => i.ItemId));
                                            Time.Instance.NextLootAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.LootingDelay_milliseconds);
                                            Time.Instance.LastJettison = DateTime.UtcNow;
											**/
                                            return;
                                        }

                                        return;
                                    }

                                    // Update free space
                                    freeCargoCapacity -= item.TotalVolume;
                                    lootItems.Add(item);
                                    //if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "We just added 1 more item to lootItems for a total of [" + lootItems.Count() + "] items we will loot from [" + containerID + "]", Logging.Teal);
                                }
                                catch (Exception exception)
                                {
                                    Logging.Log("Salvage.LootWrecks", "We Need More m3: Exception [" + exception + "]", Logging.Debug);
                                }
                            }
                        }

                        // Mark container as looted
                        QMCache.Instance.LootedContainers.Add(containerEntity.Id);

                        // Loot actual items
                        if (lootItems.Count != 0)
                        {
                            Logging.Log("Salvage.LootWrecks", "Looting container [" + containerEntity.Name + "][" + Math.Round(containerEntity.Distance / 1000, 0) + "k][ID: " + containerEntity.MaskedId + "], [" + lootItems.Count + "] valuable items", Logging.White);
                            if (Logging.DebugLootWrecks)
                            {
                                int icount = 0;
                                if (lootItems != null && lootItems.Any())
                                {
                                    foreach (var lootItem in lootItems)
                                    {
                                        icount++;
                                        Logging.Log("Salvage.LootWrecks", "[" + icount + "]LootItems Contains: [" + lootItem.Name + "] Quantity[" + lootItem.Quantity + "k] isContraband [" + lootItem.IsContraband + "] groupID [" + lootItem.GroupID + "] typeID [" + lootItem.TypeId + "] isCommonMissionItem [" + lootItem.IsCommonMissionItem + "]", Logging.White);
                                        if (lootItem.GroupID == (int)Group.Drugs ||
                                            lootItem.GroupID == (int)Group.ToxicWaste ||
                                            lootItem.TypeId == (int)TypeID.Small_Arms ||
                                            lootItem.TypeId == (int)TypeID.Ectoplasm)
                                        {
                                            lootItems.Remove(lootItem);
                                            Logging.Log("Salvage.LootWrecks", "[" + icount + "] Removed this from LootItems before looting [" + lootItem.Name + "] Quantity[" + lootItem.Quantity + "k] isContraband [" + lootItem.IsContraband + "] groupID [" + lootItem.GroupID + "] typeID [" + lootItem.TypeId + "] isCommonMissionItem [" + lootItem.IsCommonMissionItem + "]", Logging.White);
                                        }
                                    }
                                }
                            }

                            QMCache.Instance.CurrentShipsCargo.Add(lootItems.Select(i => i.DirectItem));
                        }
                        else
                        {
                            Logging.Log("Salvage.LootWrecks", "Container [" + containerEntity.Name + "][" + Math.Round(containerEntity.Distance / 1000, 0) + "k][ID: " + containerEntity.MaskedId + "] contained no valuable items", Logging.White);
                        }

                        return;
                    }

                    if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks","Reached End of LootWrecks Routine w/o finding a wreck to loot",Logging.Debug);
                    //add cont proceed this tick
                    //if (containersProcessedThisTick < QMSettings.Instance.NumberOfModulesToActivateInCycle)
                    //{
                    //    if (Logging.DebugLootWrecks) Logging.Log("Salvage.LootWrecks", "if (containersProcessedThisTick < QMSettings.Instance.NumberOfModulesToActivateInCycle)", Logging.White);
                    //    continue;
                    //}
                    return;
                }
            }
            catch (Exception exception)
            {
                Logging.Log("Salvage.LootWrecks", "Exception [" + exception + "]", Logging.Debug);
            }
        }

        private static void BlacklistWrecks()
        {
            //
            // if enabled the following would keep you from looting or salvaging small wrecks
            //
            //list of small wreck
            if (WreckBlackListSmallWrecks)
            {
                WreckBlackList.Add(26557);
                WreckBlackList.Add(26561);
                WreckBlackList.Add(26564);
                WreckBlackList.Add(26567);
                WreckBlackList.Add(26570);
                WreckBlackList.Add(26573);
                WreckBlackList.Add(26576);
                WreckBlackList.Add(26579);
                WreckBlackList.Add(26582);
                WreckBlackList.Add(26585);
                WreckBlackList.Add(26588);
                WreckBlackList.Add(26591);
                WreckBlackList.Add(26594);
                WreckBlackList.Add(26935);
            }

            //
            // if enabled the following would keep you from looting or salvaging medium wrecks
            //
            //list of medium wreck
            if (WreckBlackListMediumWrecks)
            {
                WreckBlackList.Add(26558);
                WreckBlackList.Add(26562);
                WreckBlackList.Add(26568);
                WreckBlackList.Add(26574);
                WreckBlackList.Add(26580);
                WreckBlackList.Add(26586);
                WreckBlackList.Add(26592);
                WreckBlackList.Add(26934);
            }
        }

        public static void ProcessState()
        {
            if (DateTime.UtcNow < _lastSalvageProcessState.AddMilliseconds(500) || Logging.DebugDisableSalvage) //if it has not been 100ms since the last time we ran this ProcessState return. We can't do anything that close together anyway
                return;

            _lastSalvageProcessState = DateTime.UtcNow;

            // Nothing to salvage in stations
            if (QMCache.Instance.InStation)
            {
                _States.CurrentSalvageState = SalvageState.Idle;
                return;
            }

            if (!QMCache.Instance.InSpace)
            {
                _States.CurrentSalvageState = SalvageState.Idle;
                return;
            }

            // What? No ship entity?
            if (QMCache.Instance.ActiveShip.Entity == null)
            {
                _States.CurrentSalvageState = SalvageState.Idle;
                return;
            }

            // When in warp there's nothing we can do, so ignore everything
            if (QMCache.Instance.InSpace && QMCache.Instance.InWarp)
            {
                _States.CurrentSalvageState = SalvageState.Idle;
                return;
            }

            // There is no salving when cloaked -
            // why not? seems like we might be able to ninja-salvage with a covert-ops hauler with some additional coding (someday?)
            if (QMCache.Instance.ActiveShip.Entity.IsCloaked)
            {
                _States.CurrentSalvageState = SalvageState.Idle;
                return;
            }

            if (Salvage.DoNotDoANYSalvagingOutsideMissionActions && !CurrentlyShouldBeSalvaging)
            {
                if (Logging.DebugSalvage) Logging.Log("Salvage", "DoNotDoANYSalvagingOutsideMissionActions [" + Salvage.DoNotDoANYSalvagingOutsideMissionActions + "] CurrentlyShouldBeSalvaging [" + CurrentlyShouldBeSalvaging + "] return;", Logging.Debug);
                return;
            }

            switch (_States.CurrentSalvageState)
            {
                case SalvageState.TargetWrecks:
                    if (Logging.DebugSalvage) Logging.Log("Salvage", "SalvageState.TargetWrecks:", Logging.Debug);
                    TargetWrecks();

                    // Next state
                    _States.CurrentSalvageState = SalvageState.LootWrecks;
                    break;

                case SalvageState.LootWrecks:
                    if (Logging.DebugSalvage) Logging.Log("Salvage", "SalvageState.LootWrecks:", Logging.Debug);
                    LootWrecks();

                    _States.CurrentSalvageState = SalvageState.SalvageWrecks;
                    break;

                case SalvageState.SalvageWrecks:
                    if (Logging.DebugSalvage) Logging.Log("Salvage", "SalvageState.SalvageWrecks:", Logging.Debug);
                    ActivateTractorBeams();
                    ActivateSalvagers();

                    // Default action
                    _States.CurrentSalvageState = SalvageState.TargetWrecks;
                    if (QMCache.Instance.CurrentShipsCargo.IsValid && QMCache.Instance.CurrentShipsCargo.Items.Any() && Time.Instance.LastStackCargohold.AddMinutes(5) < DateTime.UtcNow)
                    {
                        // Check if there are actually duplicates
                        bool duplicates = QMCache.Instance.CurrentShipsCargo.Items.Where(i => i.Quantity > 0).GroupBy(i => i.TypeId).Any(t => t.Count() > 1);
                        if (duplicates)
                        {
                            _States.CurrentSalvageState = SalvageState.StackItems;
                        }
                    }
                    break;

                case SalvageState.StackItems:
                    if (!QMCache.Instance.StackCargoHold("Salvage")) return;
                    Logging.Log("Salvage", "Done stacking", Logging.White);
                    _States.CurrentSalvageState = SalvageState.TargetWrecks;
                    break;

                case SalvageState.Idle:
                    if (QMCache.Instance.InSpace &&
                        (QMCache.Instance.ActiveShip.Entity != null &&
                        !QMCache.Instance.ActiveShip.Entity.IsCloaked &&
                        (QMCache.Instance.ActiveShip.GivenName.ToLower() != Combat.CombatShipName.ToLower() ||
                        QMCache.Instance.ActiveShip.GivenName.ToLower() != QMSettings.Instance.SalvageShipName.ToLower()) &&
                        !QMCache.Instance.InWarp))
                    {
                        if (Logging.DebugSalvage) Logging.Log("Salvage", "SalvageState.Idle:", Logging.Debug);
                        _States.CurrentSalvageState = SalvageState.TargetWrecks;
                        return;
                    }
                    break;

                default:

                    // Unknown state, goto first state
                    _States.CurrentSalvageState = SalvageState.TargetWrecks;
                    break;
            }
        }

        public static void InvalidateCache()
        {
            try
            {
                //
                // this list of variables is cleared every pulse.
                //
                salvagers = null;
                wrecks = null;
            }
            catch (Exception exception)
            {
                Logging.Log("Salvage.InvalidateCache", "Exception [" + exception + "]", Logging.Debug);
            }
        }
    }
}
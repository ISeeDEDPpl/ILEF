
namespace ILEF.Combat
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using ILoveEVE.Framework;
    using global::ILEF.Actions;
    using global::ILEF.Activities;
    using global::ILEF.Caching;
    using global::ILEF.Logging;
    using global::ILEF.Lookup;
    using global::ILEF.States;

    /// <summary>
    ///   The drones class will manage any and all drone related combat
    /// </summary>
    /// <remarks>
    ///   Drones will always work their way from lowest value target to highest value target and will only attack entities (not structures)
    /// </remarks>
    public static class Drones
    {
        //public static int DronesInstances;

        static Drones()
        {
            //Interlocked.Increment(ref DronesInstances);
        }

        //~Drones()
        //{
        //    Interlocked.Decrement(ref DronesInstances);
        //}

        private static int _lastDroneCount;
        //private static DateTime _lastEngageCommand;
        private static DateTime _lastRecallCommand;

        private static int _recallCount;
        private static DateTime _lastLaunch;
        private static DateTime _lastRecall;

        private static DateTime _launchTimeout;
        private static int _launchTries;
        private static double _activeDronesShieldTotalOnLastPulse;
        private static double _activeDronesArmorTotalOnLastPulse;
        private static double _activeDronesStructureTotalOnLastPulse;
        private static double _activeDronesShieldPercentageOnLastPulse;
        private static double _activeDronesArmorPercentageOnLastPulse;
        private static double _activeDronesStructurePercentageOnLastPulse;
        public static bool WarpScrambled; //false
        private static DateTime _nextDroneAction = DateTime.UtcNow;
        private static DateTime _nextWarpScrambledWarning = DateTime.MinValue;
        public static long? LastTargetIDDronesEngaged { get; set; }

        public static int GetShipsDroneBayAttempts { get; set; }
        public static bool AddWarpScramblersToDronePriorityTargetList { get; set; }
        public static bool AddWebifiersToDronePriorityTargetList { get; set; }
        public static bool AddDampenersToDronePriorityTargetList { get; set; }
        public static bool AddNeutralizersToDronePriorityTargetList { get; set; }
        public static bool AddTargetPaintersToDronePriorityTargetList { get; set; }
        public static bool AddECMsToDroneTargetList { get; set; }
        public static bool AddTrackingDisruptorsToDronePriorityTargetList { get; set; }
        private static IEnumerable<EntityCache> _activeDrones; //cleared every frame in Cache.InvalidateCache()
        public static IEnumerable<EntityCache> ActiveDrones
        {
            get
            {
                if (_activeDrones == null)
                {
                    if (QMCache.Instance.DirectEve.ActiveDrones.Any())
                    {
                        _activeDrones = QMCache.Instance.DirectEve.ActiveDrones.Select(d => new EntityCache(d)).ToList();
                        return _activeDrones;
                    }

                    return new List<EntityCache>();
                }

                return _activeDrones;
            }
        }

        private static int _droneTypeID; //only cleared by reloading settings

        public static int DroneTypeID
        {
            get
            {
                if (MissionSettings.PocketDroneTypeID != null)
                {
                    return (int)MissionSettings.PocketDroneTypeID;
                }

                if (MissionSettings.MissionDroneTypeID != null)
                {
                    return (int) MissionSettings.MissionDroneTypeID;
                }

                if (MissionSettings.FactionDroneTypeID != null)
                {
                    return (int)MissionSettings.FactionDroneTypeID;
                }

                return _droneTypeID;
            }
            set
            {
                _droneTypeID = value;
            }
        }

        public static int FactionDroneTypeID { get; set; }

        private static bool _useDrones;  //only cleared by reloading settings

        public static bool UseDrones
        {
            get
            {
                if (MissionSettings.PocketUseDrones != null)
                {
                    if (Logging.DebugDrones) Logging.Log("Drones.useDrones","We are using PocketDrones setting [" + MissionSettings.PocketUseDrones + "]",Logging.Debug);
                    return (bool)MissionSettings.PocketUseDrones;
                }

                if (MissionSettings.MissionUseDrones != null)
                {
                    if (Logging.DebugDrones) Logging.Log("Drones.useDrones", "We are using MissionDrones setting [" + MissionSettings.PocketUseDrones + "]", Logging.Debug);
                    return (bool) MissionSettings.MissionUseDrones;
                }

                return _useDrones;
            }
            set
            {
                _useDrones = value;
            }
        }

        public static int DroneControlRange { get; set; }
        public static bool DronesDontNeedTargetsBecauseWehaveThemSetOnAggressive { get; set; }
        public static int DroneMinimumShieldPct { get; set; }
        public static int DroneMinimumArmorPct { get; set; }
        public static int DroneMinimumCapacitorPct { get; set; }
        public static int DroneRecallShieldPct { get; set; }
        public static int DroneRecallArmorPct { get; set; }
        public static int DroneRecallCapacitorPct { get; set; }
        public static int BelowThisHealthLevelRemoveFromDroneBay { get; set; }
        public static int LongRangeDroneRecallShieldPct { get; set; }
        public static int LongRangeDroneRecallArmorPct { get; set; }
        public static int LongRangeDroneRecallCapacitorPct { get; set; }
        private static bool _dronesKillHighValueTargets;
        public static bool DronesKillHighValueTargets
        {
            get
            {
                if (MissionSettings.MissionDronesKillHighValueTargets != null)
                {
                    return (bool)MissionSettings.MissionDronesKillHighValueTargets;
                }

                return _dronesKillHighValueTargets;
            }
            set
            {
                _dronesKillHighValueTargets = value;
            }
        }

        /// <summary>
        ///   Used for Drones to know that it should retract drones
        /// </summary>
        public static bool IsMissionPocketDone { get; set; }

        private static double? _maxDroneRange;

        public static double MaxDroneRange
        {
            get
            {
                if (_maxDroneRange == null)
                {
                    _maxDroneRange = Math.Min(DroneControlRange, Combat.MaxTargetRange);
                    return (double) _maxDroneRange;
                }

                return (double) _maxDroneRange;
            }
        }
        /// <summary>
        ///   Drone target chosen by GetBest Target
        /// </summary>
        public static long? PreferredDroneTargetID;
        private static EntityCache _preferredDroneTarget; //cleared every frame in Cache.InvalidateCache()
        public static EntityCache PreferredDroneTarget
        {
            get
            {
                if (_preferredDroneTarget == null)
                {
                    if (PreferredDroneTargetID != null)
                    {
                        if (QMCache.Instance.EntitiesOnGrid.Any(i => i.Id == PreferredDroneTargetID))
                        {
                            _preferredDroneTarget = QMCache.Instance.EntitiesOnGrid.FirstOrDefault(i => i.Id == PreferredDroneTargetID);
                            return _preferredDroneTarget;
                        }

                        return null;
                    }

                    return null;
                }

                return _preferredDroneTarget;
            }
            set
            {
                if (value == null)
                {
                    if (_preferredDroneTarget != null)
                    {
                        _preferredDroneTarget = null;
                        PreferredDroneTargetID = null;
                        Logging.Log("PreferredPrimaryWeaponTarget.Set", "[ null ]", Logging.Debug);
                        return;
                    }
                }
                else
                {
                    if (_preferredDroneTarget != null && _preferredDroneTarget.Id != value.Id)
                    {
                        _preferredDroneTarget = value;
                        PreferredDroneTargetID = value.Id;
                        if (Logging.DebugGetBestTarget) Logging.Log("PreferredPrimaryWeaponTarget.Set", value + " [" + value.MaskedId + "]", Logging.Debug);
                        return;
                    }
                }
            }
        }

        private static List<PriorityTarget> _dronePriorityTargets;

        public static List<PriorityTarget> DronePriorityTargets
        {
            get
            {
                try
                {
                    //
                    // remove targets that no longer exist
                    //
                    if (_dronePriorityTargets != null && _dronePriorityTargets.Any())
                    {
                        foreach (PriorityTarget dronePriorityTarget in _dronePriorityTargets)
                        {
                            if (QMCache.Instance.EntitiesOnGrid.All(i => i.Id != dronePriorityTarget.EntityID))
                            {
                                _dronePriorityTargets.Remove(dronePriorityTarget);
                                break;
                            }
                        }

                        return _dronePriorityTargets;
                    }

                    //
                    // initialize a fresh list - to be filled in during panic (updated every tick)
                    //
                    _dronePriorityTargets = new List<PriorityTarget>();
                    return _dronePriorityTargets;
                }
                catch (NullReferenceException)
                {
                    return null;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.DronePriorityEntities", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
        }

        private static IEnumerable<EntityCache> _dronePriorityEntities;

        public static IEnumerable<EntityCache> DronePriorityEntities
        {
            get
            {
                try
                {
                    //
                    // every frame re-populate the DronePriorityEntities from the list of IDs we have tucked away in DronePriorityTargets
                    // this occurs because in Invalidatecache() we are, necessarily,  clearing this every frame!
                    //
                    if (_dronePriorityEntities == null)
                    {
                        if (DronePriorityTargets != null && DronePriorityTargets.Any())
                        {
                            _dronePriorityEntities = DronePriorityTargets.OrderByDescending(pt => pt.DronePriority).ThenBy(pt => pt.Entity.Distance).Select(pt => pt.Entity);
                            return _dronePriorityEntities;
                        }

                        _dronePriorityEntities = new List<EntityCache>();
                        return _dronePriorityEntities;
                    }

                    //
                    // if we have already populated the list this frame return the list we already generated
                    //
                    return _dronePriorityEntities;
                }
                catch (NullReferenceException)
                {
                    return null;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.DronePriorityEntities", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
        }

        /// <summary>
        ///   Remove targets from priority list
        /// </summary>
        /// <param name = "targets"></param>
        public static bool RemoveDronePriorityTargets(List<EntityCache> targets)
        {
            try
            {
                targets = targets.ToList();

                if (targets.Any() && _dronePriorityTargets != null && _dronePriorityTargets.Any() && _dronePriorityTargets.Any(pt => targets.Any(t => t.Id == pt.EntityID)))
                {
                    _dronePriorityTargets.RemoveAll(pt => targets.Any(t => t.Id == pt.EntityID));
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logging.Log("RemoveDronePriorityTargets", "Exception [" + ex + "]", Logging.Debug);
                return false;
            }
        }

        public static void AddDronePriorityTargetsByName(string stringEntitiesToAdd)
        {
            try
            {
                IEnumerable<EntityCache> entitiesToAdd = QMCache.Instance.EntitiesByPartialName(stringEntitiesToAdd).ToList();
                if (entitiesToAdd.Any())
                {
                    foreach (EntityCache entityToAdd in entitiesToAdd)
                    {
                        Logging.Log("RemovingPWPT", "adding [" + entityToAdd.Name + "][" + Math.Round(entityToAdd.Distance / 1000, 0) + "k][" + entityToAdd.MaskedId + "] to the PWPT List", Logging.Debug);
                        AddDronePriorityTarget(entityToAdd, DronePriority.PriorityKillTarget, "AddDPTByName");
                        continue;
                    }

                    return;
                }

                Logging.Log("Adding DPT", "[" + stringEntitiesToAdd + "] was not found on grid", Logging.Debug);
                return;
            }
            catch (Exception ex)
            {
                Logging.Log("AddDronePriorityTargetsByName", "Exception [" + ex + "]", Logging.Debug);
                return;
            }
        }

        public static void RemovedDronePriorityTargetsByName(string stringEntitiesToRemove)
        {
            try
            {
                List<EntityCache> entitiesToRemove = QMCache.Instance.EntitiesByName(stringEntitiesToRemove, QMCache.Instance.EntitiesOnGrid).ToList();
                if (entitiesToRemove.Any())
                {
                    Logging.Log("RemovingDPT", "removing [" + stringEntitiesToRemove + "] from the DPT List", Logging.Debug);
                    RemoveDronePriorityTargets(entitiesToRemove);
                    return;
                }

                Logging.Log("RemovingDPT", "[" + stringEntitiesToRemove + "] was not found on grid", Logging.Debug);
                return;
            }
            catch (Exception ex)
            {
                Logging.Log("RemovedDronePriorityTargetsByName", "Exception [" + ex + "]", Logging.Debug);
            }
        }

        public static void AddDronePriorityTargets(IEnumerable<EntityCache> ewarEntities, DronePriority priority, string module, bool AddEwarTypeToPriorityTargetList = true)
        {
            try
            {
                ewarEntities = ewarEntities.ToList();
                if (ewarEntities.Any())
                {
                    foreach (EntityCache ewarEntity in ewarEntities)
                    {
                        AddDronePriorityTarget(ewarEntity, priority, module, AddEwarTypeToPriorityTargetList);
                        continue;
                    }

                    return;
                }

                return;
            }
            catch (Exception ex)
            {
                Logging.Log("AddDronePriorityTargets", "Exception [" + ex + "]", Logging.Debug);
            }
        }

        public static void AddDronePriorityTarget(EntityCache ewarEntity, DronePriority priority, string module, bool AddEwarTypeToPriorityTargetList = true)
        {
            try
            {
                if (AddEwarTypeToPriorityTargetList && Drones.UseDrones)
                {
                    if ((ewarEntity.IsIgnored) || DronePriorityTargets.Any(p => p.EntityID == ewarEntity.Id))
                    {
                        if (Logging.DebugAddDronePriorityTarget) Logging.Log("AddDronePriorityTargets", "if ((target.IsIgnored) || DronePriorityTargets.Any(p => p.Id == target.Id))", Logging.Debug);
                        return;
                    }

                    if (DronePriorityTargets.All(i => i.EntityID != ewarEntity.Id))
                    {
                        int DronePriorityTargetCount = 0;
                        if (DronePriorityTargets.Any())
                        {
                            DronePriorityTargetCount = DronePriorityTargets.Count();
                        }
                        Logging.Log(module, "Adding [" + ewarEntity.Name + "] Speed [" + Math.Round(ewarEntity.Velocity, 2) + " m/s] Distance [" + Math.Round(ewarEntity.Distance / 1000, 2) + "] [ID: " + ewarEntity.MaskedId + "] as a drone priority target [" + priority.ToString() + "] we have [" + DronePriorityTargetCount + "] other DronePriorityTargets", Logging.Teal);
                        _dronePriorityTargets.Add(new PriorityTarget { Name = ewarEntity.Name, EntityID = ewarEntity.Id, DronePriority = priority });
                    }

                    return;
                }

                if (Logging.DebugAddDronePriorityTarget) Logging.Log(module, "UseDrones is [" + Drones.UseDrones.ToString() + "] AddWarpScramblersToDronePriorityTargetList is [" + Drones.AddWarpScramblersToDronePriorityTargetList + "] [" + ewarEntity.Name + "] was not added as a Drone PriorityTarget (why did we even try?)", Logging.Teal);
                return;
            }
            catch (Exception ex)
            {
                Logging.Log("AddDronePriorityTarget", "Exception [" + ex + "]", Logging.Debug);
            }
        }

        public static EntityCache FindDronePriorityTarget(EntityCache currentTarget, DronePriority priorityType, bool AddECMTypeToDronePriorityTargetList, double Distance, bool FindAUnTargetedEntity = true)
        {
            if (AddECMTypeToDronePriorityTargetList)
            {
                //if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget", "Checking for Neutralizing priority targets (currentTarget first)", Logging.Teal);
                // Choose any Neutralizing primary weapon priority targets
                try
                {
                    EntityCache target = null;
                    try
                    {
                        if (DronePriorityEntities.Any(pt => pt.DronePriorityLevel == priorityType))
                        {
                            target = DronePriorityEntities.Where(pt => ((FindAUnTargetedEntity || pt.IsReadyToShoot) && currentTarget != null && pt.Id == currentTarget.Id && (pt.Distance < Distance) && pt.IsActiveDroneEwarType == priorityType)
                                                                                                || ((FindAUnTargetedEntity || pt.IsReadyToShoot) && pt.Distance < Distance && pt.IsActiveDroneEwarType == priorityType))
                                                                                                       .OrderByDescending(pt => pt.IsNPCFrigate)
                                                                                                       .ThenByDescending(pt => pt.IsLastTargetDronesWereShooting)
                                                                                                       .ThenByDescending(pt => pt.IsInDroneRange)
                                                                                                       .ThenBy(pt => pt.IsEntityIShouldKeepShootingWithDrones)
                                                                                                       .ThenBy(pt => (pt.ShieldPct + pt.ArmorPct + pt.StructurePct))
                                                                                                       .ThenBy(pt => pt.Nearest5kDistance)
                                                                                                       .FirstOrDefault();
                        }
                    }
                    catch (NullReferenceException) { }  // Not sure why this happens, but seems to be no problem

                    if (target != null)
                    {
                        //if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget", "NeutralizingPrimaryWeaponPriorityTarget [" + NeutralizingPriorityTarget.Name + "][" + Math.Round(NeutralizingPriorityTarget.Distance / 1000, 2) + "k][" + QMCache.Instance.MaskedID(NeutralizingPriorityTarget.Id) + "] GroupID [" + NeutralizingPriorityTarget.GroupId + "]", Logging.Debug);

                        if (!FindAUnTargetedEntity)
                        {
                            Drones.PreferredDroneTarget = target;
                            Time.Instance.LastPreferredDroneTargetDateTime = DateTime.UtcNow;
                            return target;
                        }

                        return target;
                    }

                    return null;
                }
                catch (NullReferenceException) { }

                return null;
            }

            return null;
        }

        public static bool GetBestDroneTarget(double distance, bool highValueFirst, string callingroutine, List<EntityCache> _potentialTargets = null)
        {
            if (Logging.DebugDisableGetBestDroneTarget || !Drones.UseDrones)
            {
                if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "!QMCache.Instance.UseDrones - drones are disabled currently", Logging.Teal);
                return true;
            }

            if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "Attempting to get Best Drone Target", Logging.Teal);

            if (DateTime.UtcNow < Time.Instance.NextGetBestDroneTarget)
            {
                if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "Cant GetBest yet....Too Soon!", Logging.Teal);
                return false;
            }

            Time.Instance.NextGetBestDroneTarget = DateTime.UtcNow.AddMilliseconds(2000);

            //if (!QMCache.Instance.Targets.Any()) //&& _potentialTargets == null )
            //{
            //    if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " Debug: DebugGetBestDroneTarget:", "We have no locked targets and [" + QMCache.Instance.Targeting.Count() + "] targets being locked atm", Logging.Teal);
            //    return false;
            //}

            EntityCache currentDroneTarget = null;

            if (QMCache.Instance.EntitiesOnGrid.Any(i => i.IsLastTargetDronesWereShooting))
            {
                currentDroneTarget = QMCache.Instance.EntitiesOnGrid.FirstOrDefault(i => i.IsLastTargetDronesWereShooting);

            }

            if (DateTime.UtcNow < Time.Instance.LastPreferredDroneTargetDateTime.AddSeconds(6) && (Drones.PreferredDroneTarget != null && QMCache.Instance.EntitiesOnGrid.Any(t => t.Id == Drones.PreferredDroneTarget.Id)))
            {
                if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "We have a PreferredDroneTarget [" + Drones.PreferredDroneTarget.Name + "] that was chosen less than 6 sec ago, and is still alive.", Logging.Teal);
                return true;
            }

            //We need to make sure that our current Preferred is still valid, if not we need to clear it out
            //This happens when we have killed the last thing within our range (or the last thing in the pocket)
            //and there is nothing to replace it with.
            //if (QMCache.Instance.PreferredDroneTarget != null
            //    && QMCache.Instance.Entities.All(t => t.Id != Instance.PreferredDroneTargetID))
            //{
            //    if (Logging.DebugGetBestDroneTarget) Logging.Log("GetBestDroneTarget", "PreferredDroneTarget is not valid, clearing it", Logging.White);
            //    QMCache.Instance.PreferredDroneTarget = null;
            //}

            //
            // if currentTarget set to something (not null) and it is actually an entity...
            //
            if (currentDroneTarget != null && currentDroneTarget.IsReadyToShoot && currentDroneTarget.IsLowValueTarget)
            {
                if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget: currentDroneTarget", "We have a currentTarget [" + currentDroneTarget.Name + "][" + currentDroneTarget.MaskedId + "][" + Math.Round(currentDroneTarget.Distance / 1000, 2) + "k], testing conditions", Logging.Teal);

                #region Is our current target any other drone priority target?
                //
                // Is our current target any other drone priority target? AND if our target is just a PriorityKillTarget assume ALL E-war is more important.
                //
                if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget: currentTarget", "Checking Priority", Logging.Teal);
                if (DronePriorityEntities.Any(pt => pt.IsReadyToShoot
                                                        && pt.Nearest5kDistance < MaxDroneRange
                                                        && pt.Id == currentDroneTarget.Id
                                                        && !currentDroneTarget.IsHigherPriorityPresent))
                {
                    if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "CurrentTarget [" + currentDroneTarget.Name + "][" + Math.Round(currentDroneTarget.Distance / 1000, 2) + "k][" + currentDroneTarget.MaskedId + "] GroupID [" + currentDroneTarget.GroupId + "]", Logging.Debug);
                    Drones.PreferredDroneTarget = currentDroneTarget;
                    Time.Instance.LastPreferredDroneTargetDateTime = DateTime.UtcNow;
                    return true;
                }
                #endregion Is our current target any other drone priority target?

                #region Is our current target already in armor? keep shooting the same target if so...
                //
                // Is our current target already low health? keep shooting the same target if so...
                //
                if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget: currentDroneTarget", "Checking Low Health", Logging.Teal);
                if (currentDroneTarget.IsEntityIShouldKeepShootingWithDrones)
                {
                    if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "currentDroneTarget [" + currentDroneTarget.Name + "][" + Math.Round(currentDroneTarget.Distance / 1000, 2) + "k][" + currentDroneTarget.MaskedId + " GroupID [" + currentDroneTarget.GroupId + "]] has less than 80% shields, keep killing this target", Logging.Debug);
                    Drones.PreferredDroneTarget = currentDroneTarget;
                    Time.Instance.LastPreferredDroneTargetDateTime = DateTime.UtcNow;
                    return true;
                }

                #endregion Is our current target already in armor? keep shooting the same target if so...

                #region If none of the above matches, does our current target meet the conditions of being hittable and in range
                if (!currentDroneTarget.IsHigherPriorityPresent)
                {
                    if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget: currentDroneTarget", "Does the currentTarget exist? Can it be hit?", Logging.Teal);
                    if (currentDroneTarget.IsReadyToShoot && currentDroneTarget.Nearest5kDistance < MaxDroneRange)
                    {
                        if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "if  the currentDroneTarget exists and the target is the right size then continue shooting it;", Logging.Debug);
                        if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "currentDroneTarget is [" + currentDroneTarget.Name + "][" + Math.Round(currentDroneTarget.Distance / 1000, 2) + "k][" + currentDroneTarget.MaskedId + "] GroupID [" + currentDroneTarget.GroupId + "]", Logging.Debug);

                        Drones.PreferredDroneTarget = currentDroneTarget;
                        Time.Instance.LastPreferredDroneTargetDateTime = DateTime.UtcNow;
                        return true;
                    }
                }
                #endregion
            }

            //
            // process the list of PrimaryWeaponPriorityTargets in this order... Eventually the order itself should be user selectable
            // this allow us to kill the most 'important' things doing e-war first instead of just handling them by range
            //

            // Do we have ANY warp scrambling entities targeted starting with currentTarget
            // this needs QMSettings.Instance.AddWarpScramblersToPrimaryWeaponsPriorityTargetList true, otherwise they will just get handled in any order below...
            if (FindDronePriorityTarget(currentDroneTarget, DronePriority.WarpScrambler, Drones.AddWarpScramblersToDronePriorityTargetList, distance) != null)
                return true;

            // Do we have ANY ECM entities targeted starting with currentTarget
            // this needs QMSettings.Instance.AddECMsToPrimaryWeaponsPriorityTargetList true, otherwise they will just get handled in any order below...
            if (FindDronePriorityTarget(currentDroneTarget, DronePriority.Webbing, Drones.AddECMsToDroneTargetList, distance) != null)
                return true;

            // Do we have ANY tracking disrupting entities targeted starting with currentTarget
            // this needs QMSettings.Instance.AddTrackingDisruptorsToPrimaryWeaponsPriorityTargetList true, otherwise they will just get handled in any order below...
            if (FindDronePriorityTarget(currentDroneTarget, DronePriority.PriorityKillTarget, Drones.AddTrackingDisruptorsToDronePriorityTargetList, distance) != null)
                return true;

            // Do we have ANY Neutralizing entities targeted starting with currentTarget
            // this needs QMSettings.Instance.AddNeutralizersToPrimaryWeaponsPriorityTargetList true, otherwise they will just get handled in any order below...
            if (FindDronePriorityTarget(currentDroneTarget, DronePriority.PriorityKillTarget, Drones.AddNeutralizersToDronePriorityTargetList, distance) != null)
                return true;

            // Do we have ANY Target Painting entities targeted starting with currentTarget
            // this needs QMSettings.Instance.AddTargetPaintersToPrimaryWeaponsPriorityTargetList true, otherwise they will just get handled in any order below...
            if (FindDronePriorityTarget(currentDroneTarget, DronePriority.PriorityKillTarget, Drones.AddTargetPaintersToDronePriorityTargetList, distance) != null)
                return true;

            // Do we have ANY Sensor Dampening entities targeted starting with currentTarget
            // this needs QMSettings.Instance.AddDampenersToPrimaryWeaponsPriorityTargetList true, otherwise they will just get handled in any order below...
            if (FindDronePriorityTarget(currentDroneTarget, DronePriority.PriorityKillTarget, Drones.AddDampenersToDronePriorityTargetList, distance) != null)
                return true;

            // Do we have ANY Webbing entities targeted starting with currentTarget
            // this needs QMSettings.Instance.AddWebifiersToPrimaryWeaponsPriorityTargetList true, otherwise they will just get handled in any order below...
            if (FindDronePriorityTarget(currentDroneTarget, DronePriority.PriorityKillTarget, Drones.AddWebifiersToDronePriorityTargetList, distance) != null)
                return true;

            #region Get the closest drone priority target
            //
            // Get the closest primary weapon priority target
            //
            if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "Checking Closest DronePriorityTarget", Logging.Teal);
            EntityCache dronePriorityTarget = null;
            try
            {
                dronePriorityTarget = DronePriorityEntities.Where(p => p.Nearest5kDistance < MaxDroneRange
                                                                            && !p.IsIgnored
                                                                            && p.IsReadyToShoot)
                                                                           .OrderBy(pt => pt.DronePriorityLevel)
                                                                           .ThenByDescending(pt => pt.IsEwarTarget)
                                                                           .ThenByDescending(pt => pt.IsTargetedBy)
                                                                           .ThenBy(pt => pt.Nearest5kDistance)
                                                                           .FirstOrDefault();
            }
            catch (NullReferenceException) { }  // Not sure why this happens, but seems to be no problem

            if (dronePriorityTarget != null)
            {
                if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "dronePriorityTarget is [" + dronePriorityTarget.Name + "][" + Math.Round(dronePriorityTarget.Distance / 1000, 2) + "k][" + dronePriorityTarget.MaskedId + "] GroupID [" + dronePriorityTarget.GroupId + "]", Logging.Debug);
                Drones.PreferredDroneTarget = dronePriorityTarget;
                Time.Instance.LastPreferredDroneTargetDateTime = DateTime.UtcNow;
                return true;
            }

            #endregion Get the closest drone priority target

            #region did our calling routine (CombatMissionCtrl?) pass us targets to shoot?
            //
            // This is where CombatMissionCtrl would pass targets to GetBestDroneTarget
            //
            if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "Checking Calling Target", Logging.Teal);
            if (_potentialTargets != null && _potentialTargets.Any())
            {
                EntityCache callingDroneTarget = null;
                try
                {
                    callingDroneTarget = _potentialTargets.OrderBy(t => t.Nearest5kDistance).FirstOrDefault();
                }
                catch (NullReferenceException) { }

                if (callingDroneTarget != null && callingDroneTarget.IsReadyToShoot)
                {
                    if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "if (callingDroneTarget != null && !callingDroneTarget.IsIgnored)", Logging.Debug);
                    if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "callingDroneTarget is [" + callingDroneTarget.Name + "][" + Math.Round(callingDroneTarget.Distance / 1000, 2) + "k][" + callingDroneTarget.MaskedId + "] GroupID [" + callingDroneTarget.GroupId + "]", Logging.Debug);
                    AddDronePriorityTarget(callingDroneTarget, DronePriority.PriorityKillTarget, " GetBestDroneTarget: callingDroneTarget");
                    Drones.PreferredDroneTarget = callingDroneTarget;
                    Time.Instance.LastPreferredDroneTargetDateTime = DateTime.UtcNow;
                    return true;
                }

                //return false; //do not return here, continue to process targets, we did not find one yet
            }
            #endregion

            #region Get the closest Low Value Target

            if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "Checking Closest Low Value", Logging.Teal);
            EntityCache lowValueTarget = null;

            if (Combat.PotentialCombatTargets.Any())
            {
                if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "get closest: if (potentialCombatTargets.Any())", Logging.Teal);

                lowValueTarget = Combat.PotentialCombatTargets.Where(t => t.IsLowValueTarget && t.IsReadyToShoot)
                    .OrderBy(t => t.IsEwarTarget)
                    .ThenByDescending(t => t.IsNPCFrigate)
                    .ThenByDescending(t => t.IsTargetedBy)
                    .ThenBy(QMCache.Instance.OrderByLowestHealth())
                    .ThenBy(t => t.Nearest5kDistance)
                    .FirstOrDefault();
            }
            #endregion

            #region Get the closest high value target
            //
            // Get the closest low value target //excluding things going too fast for guns to hit (if you have guns fitted)
            //
            if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "Checking closest Low Value", Logging.Teal);
            EntityCache highValueTarget = null;
            if (Combat.PotentialCombatTargets.Any())
            {
                highValueTarget = Combat.PotentialCombatTargets.Where(t => t.IsHighValueTarget && t.IsReadyToShoot)
                    .OrderByDescending(t => !t.IsNPCFrigate)
                    .ThenByDescending(t => t.IsTargetedBy)
                    .ThenBy(QMCache.Instance.OrderByLowestHealth())
                    .ThenBy(t => t.Nearest5kDistance)
                    .FirstOrDefault();
            }
            #endregion

            #region prefer to grab a lowvaluetarget, if none avail use a high value target
            if (lowValueTarget != null || highValueTarget != null)
            {
                if (Logging.DebugGetBestDroneTarget) Logging.Log(callingroutine + " GetBestDroneTarget:", "Checking use High Value", Logging.Teal);
                if (Logging.DebugGetBestDroneTarget)
                {
                    if (highValueTarget != null)
                    {
                        Logging.Log(callingroutine + " GetBestDroneTarget:", "highValueTarget is [" + highValueTarget.Name + "][" + Math.Round(highValueTarget.Distance / 1000, 2) + "k][" + highValueTarget.MaskedId + "] GroupID [" + highValueTarget.GroupId + "]", Logging.Debug);
                    }
                    else
                    {
                        Logging.Log(callingroutine + " GetBestDroneTarget:", "highValueTarget is [ null ]", Logging.Debug);
                    }
                }
                Drones.PreferredDroneTarget = lowValueTarget ?? highValueTarget ?? null;
                Time.Instance.LastPreferredDroneTargetDateTime = DateTime.UtcNow;
                return true;
            }
            #endregion

            if (Logging.DebugGetBestDroneTarget) Logging.Log("GetBestDroneTarget: none", "Could not determine a suitable Drone target", Logging.Debug);
            #region If we did not find anything at all (wtf!?!?)
            if (Logging.DebugGetBestDroneTarget)
            {
                if (QMCache.Instance.Targets.Any())
                {
                    Logging.Log("GetBestDroneTarget (Drones): none", ".", Logging.Debug);
                    Logging.Log("GetBestDroneTarget (Drones): none", "*** ALL LOCKED/LOCKING TARGETS LISTED BELOW", Logging.Debug);
                    int LockedTargetNumber = 0;
                    foreach (EntityCache __target in QMCache.Instance.Targets)
                    {
                        LockedTargetNumber++;
                        Logging.Log("GetBestDroneTarget (Drones): none", "*** Target: [" + LockedTargetNumber + "][" + __target.Name + "][" + Math.Round(__target.Distance / 1000, 2) + "k][" + __target.MaskedId + "][isTarget: " + __target.IsTarget + "][isTargeting: " + __target.IsTargeting + "] GroupID [" + __target.GroupId + "]", Logging.Debug);
                    }
                    Logging.Log("GetBestDroneTarget (Drones): none", "*** ALL LOCKED/LOCKING TARGETS LISTED ABOVE", Logging.Debug);
                    Logging.Log("GetBestDroneTarget (Drones): none", ".", Logging.Debug);
                }

                if (Combat.PotentialCombatTargets.Any(t => !t.IsTarget && !t.IsTargeting))
                {
                    if (CombatMissionCtrl.IgnoreTargets.Any())
                    {
                        int IgnoreCount = CombatMissionCtrl.IgnoreTargets.Count;
                        Logging.Log("GetBestDroneTarget (Drones): none", "Ignore List has [" + IgnoreCount + "] Entities in it.", Logging.Debug);
                    }

                    Logging.Log("GetBestDroneTarget (Drones): none", "***** ALL [" + Combat.PotentialCombatTargets.Count() + "] potentialCombatTargets LISTED BELOW (not yet targeted or targeting)", Logging.Debug);
                    int potentialCombatTargetNumber = 0;
                    foreach (EntityCache potentialCombatTarget in Combat.PotentialCombatTargets)
                    {
                        potentialCombatTargetNumber++;
                        Logging.Log("GetBestDroneTarget (Drones): none", "***** Unlocked [" + potentialCombatTargetNumber + "]: [" + potentialCombatTarget.Name + "][" + Math.Round(potentialCombatTarget.Distance / 1000, 2) + "k][" + potentialCombatTarget.MaskedId + "][isTarget: " + potentialCombatTarget.IsTarget + "] GroupID [" + potentialCombatTarget.GroupId + "]", Logging.Debug);
                    }
                    Logging.Log("GetBestDroneTarget (Drones): none", "***** ALL [" + Combat.PotentialCombatTargets.Count() + "] potentialCombatTargets LISTED ABOVE (not yet targeted or targeting)", Logging.Debug);
                    Logging.Log("GetBestDroneTarget (Drones): none", ".", Logging.Debug);
                }
            }
            #endregion

            Time.Instance.NextGetBestDroneTarget = DateTime.UtcNow;
            return false;
        }

        private static double GetActiveDroneShieldTotal()
        {
            if (!Drones.ActiveDrones.Any())
                return 0;

            return Drones.ActiveDrones.Sum(d => d.ShieldHitPoints);
        }

        private static double GetActiveDroneArmorTotal()
        {
            if (!Drones.ActiveDrones.Any())
                return 0;

            if (Drones.ActiveDrones.Any(i => i.ArmorPct * 100 < 100))
            {
                Arm.NeedRepair = true;
            }

            return Drones.ActiveDrones.Sum(d => d.ArmorHitPoints);
        }

        private static double GetActiveDroneStructureTotal()
        {
            if (!Drones.ActiveDrones.Any())
                return 0;

            if (Drones.ActiveDrones.Any(i => i.StructurePct * 100 < 100))
            {
                Arm.NeedRepair = true;
            }

            return Drones.ActiveDrones.Sum(d => d.StructureHitPoints);
        }

        private static double GetActiveDroneShieldPercentage()
        {
            if (!Drones.ActiveDrones.Any())
                return 0;

            return Drones.ActiveDrones.Sum(d => d.ShieldPct * 100);
        }

        private static double GetActiveDroneArmorPercentage()
        {
            if (!Drones.ActiveDrones.Any())
                return 0;

            return Drones.ActiveDrones.Sum(d => d.ArmorPct * 100);
        }

        private static double GetActiveDroneStructurePercentage()
        {
            if (!Drones.ActiveDrones.Any())
                return 0;

            return Drones.ActiveDrones.Sum(d => d.StructurePct * 100);
        }

        /// <summary>
        ///   Engage the target
        /// </summary>
        private static void EngageTarget()
        {
            try
            {
                if (Logging.DebugDrones) Logging.Log("Drones.EngageTarget", "Entering EngageTarget()", Logging.Debug);

                // Find the first active weapon's target
                //TargetingCache.CurrentDronesTarget = QMCache.Instance.EntityById(_lastTarget);

                if (Logging.DebugDrones) Logging.Log("Drones.EngageTarget", "MaxDroneRange [" + MaxDroneRange + "] lowValueTargetTargeted [" + Combat.lowValueTargetsTargeted.Count() + "] LVTT InDroneRange [" + Combat.lowValueTargetsTargeted.Count(i => i.Distance < Drones.MaxDroneRange) + "] highValueTargetTargeted [" + Combat.highValueTargetsTargeted.Count() + "] HVTT InDroneRange [" + Combat.highValueTargetsTargeted.Count(i => i.Distance < Drones.MaxDroneRange) + "]", Logging.Debug);
                // Return best possible low value target

                if (PreferredDroneTarget == null || !PreferredDroneTarget.IsFrigate)
                {
                    GetBestDroneTarget(MaxDroneRange, !Drones.DronesKillHighValueTargets, "Drones");
                }

                EntityCache DroneToShoot = PreferredDroneTarget;

                if (DroneToShoot == null)
                {
                    if (Logging.DebugDrones) Logging.Log("Drones.EngageTarget", "PreferredDroneTarget is null, picking a target using a simple rule set...", Logging.Debug);
                    if (QMCache.Instance.Targets.Any(i => !i.IsContainer && !i.IsBadIdea && i.Distance < MaxDroneRange))
                    {
                        DroneToShoot = QMCache.Instance.Targets.Where(i => !i.IsContainer && !i.IsBadIdea && i.Distance < MaxDroneRange).OrderByDescending(i => i.IsWarpScramblingMe).ThenByDescending(i => i.IsFrigate).ThenBy(i => i.Distance).FirstOrDefault();
                        if (DroneToShoot == null)
                        {
                            Logging.Log("EngageTarget", "DroneToShoot is Null, this is bad.", Logging.Debug);
                        }
                    }
                }

                if (DroneToShoot != null)
                {
                    if (DroneToShoot.IsReadyToShoot && DroneToShoot.Distance < MaxDroneRange)
                    {
                        if (Logging.DebugDrones) Logging.Log("Drones.EngageTarget", "if (DroneToShoot != null && DroneToShoot.IsReadyToShoot && DroneToShoot.Distance < QMCache.Instance.MaxDroneRange)", Logging.Debug);

                        // Nothing to engage yet, probably re-targeting
                        if (!DroneToShoot.IsTarget)
                        {
                            if (Logging.DebugDrones) Logging.Log("Drones.EngageTarget", "if (!DroneToShoot.IsTarget)", Logging.Debug);
                            return;
                        }

                        if (DroneToShoot.IsBadIdea) //&& !DroneToShoot.IsAttacking)
                        {
                            if (Logging.DebugDrones) Logging.Log("Drones.EngageTarget", "if (DroneToShoot.IsBadIdea && !DroneToShoot.IsAttacking) return;", Logging.Debug);
                            return;
                        }

                        // Is our current target still the same and are all the drones shooting the PreferredDroneTarget?
                        if (LastTargetIDDronesEngaged != null)
                        {
                            if (LastTargetIDDronesEngaged == DroneToShoot.Id && Drones.ActiveDrones.All(i => i.FollowId == PreferredDroneTargetID && (i. Mode == 1 || i.Mode == 6 || i.Mode == 10)))
                            {
                                if (Logging.DebugDrones) Logging.Log("Drones.EngageTarget", "if (LastDroneTargetID [" + LastTargetIDDronesEngaged + "] == DroneToShoot.Id [" + DroneToShoot.Id + "] && QMCache.Instance.ActiveDrones.Any(i => i.FollowId != QMCache.Instance.PreferredDroneTargetID) [" + Drones.ActiveDrones.Any(i => i.FollowId != PreferredDroneTargetID) + "])", Logging.Debug);
                                return;
                            }
                        }

                        //
                        // If we got this far we need to tell the drones to do something
                        // Is the last target our current active target?
                        //
                        if (DroneToShoot.IsActiveTarget)
                        {
                            // Engage target
                            Logging.Log("Drones", "Engaging [ " + Drones.ActiveDrones.Count() + " ] drones on [" + DroneToShoot.Name + "][ID: " + DroneToShoot.MaskedId + "]" + Math.Round(DroneToShoot.Distance / 1000, 0) + "k away]", Logging.Magenta);
                            QMCache.Instance.DirectEve.ExecuteCommand(DirectCmd.CmdDronesEngage);
                            //_lastEngageCommand = DateTime.UtcNow;
                            // Save target id (so we do not constantly switch)
                            LastTargetIDDronesEngaged = DroneToShoot.Id;
                        }
                        else // Make the target active
                        {
                            if (DateTime.UtcNow > Time.Instance.NextMakeActiveTargetAction)
                            {
                                DroneToShoot.MakeActiveTarget();
                                Logging.Log("Drones", "[" + DroneToShoot.Name + "][ID: " + DroneToShoot.MaskedId + "]IsActiveTarget[" + DroneToShoot.IsActiveTarget + "][" + Math.Round(DroneToShoot.Distance / 1000, 0) + "k away] has been made the ActiveTarget (needed for drones)", Logging.Magenta);
                                Time.Instance.NextMakeActiveTargetAction = DateTime.UtcNow.AddSeconds(5 + QMCache.Instance.RandomNumber(0, 3));
                            }
                        }
                    }

                    if (Logging.DebugDrones) Logging.Log("Drones.EngageTarget", "if (DroneToShoot != null && DroneToShoot.IsReadyToShoot && DroneToShoot.Distance < QMCache.Instance.MaxDroneRange)", Logging.Debug);
                    return;
                }

                if (Logging.DebugDrones) Logging.Log("Drones.EngageTarget", "if (QMCache.Instance.PreferredDroneTargetID != null)", Logging.Debug);
                return;
            }
            catch (Exception ex)
            {
                Logging.Log("Drones.EngageTarget", "Exception [" + ex + "]", Logging.Debug);
            }
        }

        private static DirectContainer _droneBay;
        public static DirectContainer DroneBay
        {
            get
            {
                try
                {
                    if (!QMCache.Instance.InSpace && QMCache.Instance.InStation)
                    {
                        if (_droneBay == null)
                        {
                            _droneBay = QMCache.Instance.DirectEve.GetShipsDroneBay();
                        }

                        if (QMCache.Instance.Windows.All(i => !i.Type.Contains("Drone"))) // look for windows via the window (via caption of form type) ffs, not what is attached to this DirectCotnainer
                        {
                            if (DateTime.UtcNow > Time.Instance.LastOpenHangar.AddSeconds(10))
                            {
                                Statistics.LogWindowActionToWindowLog("Dronebay", "Opening Dronebay");
                                QMCache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenDroneBayOfActiveShip);
                                Time.Instance.LastOpenHangar = DateTime.UtcNow;
                            }
                        }

                        return _droneBay;
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    Logging.Log("Dronebay", "Exception [" + ex + "]", Logging.Debug);
                    return null;
                }
            }

            set { _droneBay = value; }
        }

        public static bool CloseDroneBayWindow(string module)
        {
            if (DateTime.UtcNow < Time.Instance.NextDroneBayAction)
            {
                //Logging.Log(module + ": Closing Drone Bay: waiting [" + Math.Round(QMCache.Instance.NextOpenDroneBayAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + "sec]",Logging.White);
                return false;
            }

            try
            {
                if ((!QMCache.Instance.InSpace && !QMCache.Instance.InStation))
                {
                    Logging.Log(module, "Closing Drone Bay: We are not in station or space?!", Logging.Orange);
                    return false;
                }

                if (QMCache.Instance.InStation || QMCache.Instance.InSpace)
                {
                    DroneBay = QMCache.Instance.DirectEve.GetShipsDroneBay();
                }
                else
                {
                    return false;
                }

                // Is the drone bay open? if so, close it
                if (DroneBay.Window != null)
                {
                    Time.Instance.NextDroneBayAction = DateTime.UtcNow.AddSeconds(2 + QMCache.Instance.RandomNumber(1, 3));
                    Logging.Log(module, "Closing Drone Bay: waiting [" + Math.Round(Time.Instance.NextDroneBayAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + "sec]", Logging.White);
                    DroneBay.Window.Close();
                    Statistics.LogWindowActionToWindowLog("Dronebay", "Closing DroneBay");
                    return true;
                }

                return true;
            }
            catch (Exception exception)
            {
                Logging.Log("CloseDroneBay", "Unable to complete CloseDroneBay [" + exception + "]", Logging.Teal);
                return false;
            }
        }

        private static bool ShouldWeLaunchDrones()
        {
            // Always launch if we're scrambled
            if (!Combat.PotentialCombatTargets.Any(pt => pt.IsWarpScramblingMe))
            {
                if (!Drones.UseDrones)
                {
                    if (Logging.DebugDrones) Logging.Log("Drones.ShouldWeLaunchDrones", "UseDrones is [" + UseDrones + "] Not Launching Drones", Logging.Debug);
                    return false;
                }

                // Are we done with this mission pocket?
                if (Drones.IsMissionPocketDone)
                {
                    if (Logging.DebugDrones) Logging.Log("Drones.ShouldWeLaunchDrones", "IsMissionPocketDone [" + Drones.IsMissionPocketDone + "] Not Launching Drones", Logging.Debug);
                    return false;
                }

                // If above my ships shield minimum for launching drones
                if (QMCache.Instance.ActiveShip.ShieldPercentage <= DroneMinimumShieldPct)
                {
                    if (Logging.DebugDrones) Logging.Log("Drones.ShouldWeLaunchDrones", "My Ships ShieldPercentage [" + QMCache.Instance.ActiveShip.ShieldPercentage + "] is below [" + DroneMinimumShieldPct + "] Not Launching Drones", Logging.Debug);
                    return false;
                }

                // If above my ships armor minimum for launching drones
                if (QMCache.Instance.ActiveShip.ArmorPercentage <= DroneMinimumArmorPct)
                {
                    if (Logging.DebugDrones) Logging.Log("Drones.ShouldWeLaunchDrones", "My Ships ArmorPercentage [" + QMCache.Instance.ActiveShip.ArmorPercentage + "] is below [" + DroneMinimumArmorPct + "] Not Launching Drones", Logging.Debug);
                    return false;
                }

                // If above my ships capacitor minimum for launching drones
                if (QMCache.Instance.ActiveShip.CapacitorPercentage <= DroneMinimumCapacitorPct)
                {
                    if (Logging.DebugDrones) Logging.Log("Drones.ShouldWeLaunchDrones", "My Ships CapacitorPercentage [" + QMCache.Instance.ActiveShip.CapacitorPercentage + "] is below [" + DroneMinimumCapacitorPct + "] Not Launching Drones", Logging.Debug);
                    return false;
                }

                // yes if there are targets to kill
                if (!Combat.Aggressed.Any(e => (!e.IsSentry || (e.IsSentry && Combat.KillSentries) || (e.IsSentry && e.IsEwarTarget)) && e.Distance < MaxDroneRange) && !QMCache.Instance.Targets.Any(e => e.IsLargeCollidable))
                {
                    if (Logging.DebugDrones) Logging.Log("Drones.ShouldWeLaunchDrones", "We have nothing Aggressed; MaxDroneRange [" + MaxDroneRange + "] DroneControlrange [" + DroneControlRange + "] TargetingRange [" + Combat.MaxTargetRange + "]", Logging.Debug);
                    return false;
                }

                if (_States.CurrentQuestorState != QuestorState.CombatMissionsBehavior)
                {
                    if (!QMCache.Instance.EntitiesOnGrid.Any(e => ((!e.IsSentry && !e.IsBadIdea && e.CategoryId == (int)CategoryID.Entity && e.IsNpc && !e.IsContainer && !e.IsLargeCollidable) || e.IsAttacking) && e.Distance < MaxDroneRange))
                    {
                        if (Logging.DebugDrones) Logging.Log("Drones.ShouldWeLaunchDrones", "QuestorState is [" + _States.CurrentQuestorState.ToString() + "] We have nothing to shoot;", Logging.Debug);
                        return false;
                    }
                }

                // If drones get aggro'd within 30 seconds, then wait (5 * _recallCount + 5) seconds since the last recall
                if (_lastLaunch < _lastRecall && _lastRecall.Subtract(_lastLaunch).TotalSeconds < 30)
                {
                    if (_lastRecall.AddSeconds(5 * _recallCount + 5) < DateTime.UtcNow)
                    {
                        // Increase recall count and allow the launch
                        _recallCount++;

                        // Never let _recallCount go above 5
                        if (_recallCount > 5)
                        {
                            _recallCount = 5;
                        }

                        return true;
                    }

                    // Do not launch the drones until the delay has passed
                    if (Logging.DebugDrones) Logging.Log("Drones.ShouldWeLaunchDrones", "We are still in _lastRecall delay.", Logging.Debug);
                    return false;
                }

                // Drones have been out for more then 30s
                _recallCount = 0;
                return true;
            }

            return true;
        }
        private static bool ShouldWeRecallDrones()
        {
            try
            {
                // Default to long range recall
                int lowShieldWarning = LongRangeDroneRecallShieldPct;
                int lowArmorWarning = LongRangeDroneRecallArmorPct;
                int lowCapWarning = LongRangeDroneRecallCapacitorPct;

                if (Drones.ActiveDrones.Average(d => d.Distance) < (MaxDroneRange / 2d))
                {
                    lowShieldWarning = DroneRecallShieldPct;
                    lowArmorWarning = DroneRecallArmorPct;
                    lowCapWarning = DroneRecallCapacitorPct;
                }

                if (!Drones.UseDrones)
                {
                    Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones: UseDrones is [" + UseDrones + "]", Logging.Magenta);
                    return true;
                }

                // Are we done (for now) ?
                int TargetedByInDroneRangeCount = Combat.TargetedBy.Count(e => (!e.IsSentry || (e.IsSentry && Combat.KillSentries) || (e.IsSentry && e.IsEwarTarget)) && e.IsInDroneRange);
                if (TargetedByInDroneRangeCount == 0 && !WarpScrambled) //&& !QMSettings.Instance.FleetSupportSlave)
                {
                    int TargtedByCount = 0;
                    if (Combat.TargetedBy != null && Combat.TargetedBy.Any())
                    {
                        TargtedByCount = Combat.TargetedBy.Count();
                        EntityCache __closestTargetedBy = Combat.TargetedBy.OrderBy(i => i.Distance).FirstOrDefault(e => !e.IsSentry || (e.IsSentry && Combat.KillSentries) || (e.IsSentry && e.IsEwarTarget));
                        if (__closestTargetedBy != null)
                        {
                            Logging.Log("Drones", "The closest target that is targeting ME is at [" + __closestTargetedBy.Distance + "]k", Logging.Debug);
                        }
                    }

                    Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones: There are [" + Combat.PotentialCombatTargets.Count(e => e.IsInDroneRange && (!e.IsSentry || (e.IsSentry && Combat.KillSentries) || (e.IsSentry && e.IsEwarTarget))) + "] PotentialCombatTargets not targeting us within My MaxDroneRange: [" + Math.Round(MaxDroneRange / 1000, 0) + "k] Targeting Range Is [" + Math.Round(Combat.MaxTargetRange / 1000, 0) + "k] We have [" + TargtedByCount + "] total things targeting us and [" + Combat.PotentialCombatTargets.Count(e => !e.IsSentry || (e.IsSentry && Combat.KillSentries) || (e.IsSentry && e.IsEwarTarget)) + "] total PotentialCombatTargets", Logging.Magenta);

                    if (Logging.DebugDrones)
                    {
                        foreach (EntityCache PCTInDroneRange in Combat.PotentialCombatTargets.Where(i => i.IsInDroneRange && i.IsTargetedBy))
                        {
                            Logging.Log("Drones", "Recalling Drones Details:  PCTInDroneRange [" + PCTInDroneRange.Name + "][" + PCTInDroneRange.MaskedId + "] at [" + Math.Round(PCTInDroneRange.Distance /1000, 2) + "] not targeting us yet", Logging.Magenta);
                        }
                    }

                    return true;
                }

                if (Drones.IsMissionPocketDone && !WarpScrambled)
                {
                    Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones: We are done with this pocket.", Logging.Magenta);
                    return true;
                }

                if (_activeDronesShieldTotalOnLastPulse > GetActiveDroneShieldTotal() + 5)
                {
                    Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones: shields! [Old: " + _activeDronesShieldTotalOnLastPulse.ToString("N2") + "][New: " + GetActiveDroneShieldTotal().ToString("N2") + "]", Logging.Magenta);
                    return true;
                }

                if (_activeDronesArmorTotalOnLastPulse > GetActiveDroneArmorTotal() + 5)
                {
                    Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones: armor! [Old:" + _activeDronesArmorTotalOnLastPulse.ToString("N2") + "][New: " + GetActiveDroneArmorTotal().ToString("N2") + "]", Logging.Magenta);
                    return true;
                }

                if (_activeDronesStructureTotalOnLastPulse > GetActiveDroneStructureTotal() + 5)
                {
                    Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones: structure! [Old:" + _activeDronesStructureTotalOnLastPulse.ToString("N2") + "][New: " + GetActiveDroneStructureTotal().ToString("N2") + "]", Logging.Magenta);
                    return true;
                }

                if (_activeDronesShieldPercentageOnLastPulse > GetActiveDroneShieldPercentage() + 1)
                {
                    Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones: shields! [Old: " + _activeDronesShieldPercentageOnLastPulse.ToString("N2") + "][New: " + GetActiveDroneShieldPercentage().ToString("N2") + "]", Logging.Magenta);
                    return true;
                }

                if (_activeDronesArmorPercentageOnLastPulse > GetActiveDroneArmorPercentage() + 1)
                {
                    Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones: armor! [Old:" + _activeDronesArmorPercentageOnLastPulse.ToString("N2") + "][New: " + GetActiveDroneArmorPercentage().ToString("N2") + "]", Logging.Magenta);
                    return true;
                }

                if (_activeDronesStructurePercentageOnLastPulse > GetActiveDroneStructurePercentage() + 1)
                {
                    Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones: structure! [Old:" + _activeDronesStructurePercentageOnLastPulse.ToString("N2") + "][New: " + GetActiveDroneStructurePercentage().ToString("N2") + "]", Logging.Magenta);
                    return true;
                }

                if (Drones.ActiveDrones.Count() < _lastDroneCount)
                {
                    // Did we lose a drone? (this should be covered by total's as well though)
                    Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones: We lost a drone! [Old:" + _lastDroneCount + "][New: " + Drones.ActiveDrones.Count() + "]", Logging.Orange);
                    return true;
                }

                if ((Combat.PotentialCombatTargets.Any() && !Combat.PotentialCombatTargets.Any(i => i.IsTargeting || i.IsTarget)) && !DronesDontNeedTargetsBecauseWehaveThemSetOnAggressive)
                {
                    Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones due to [" + QMCache.Instance.Targets.Count() + "] targets being locked. Locking [" + QMCache.Instance.Targeting.Count() + "] targets atm", Logging.Orange);
                    return true;
                }

                if (QMCache.Instance.ActiveShip.ShieldPercentage < lowShieldWarning && !WarpScrambled)
                {
                    Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones due to shield [" + Math.Round(QMCache.Instance.ActiveShip.ShieldPercentage, 0) + "%] below [" + lowShieldWarning + "%] minimum", Logging.Orange);
                    return true;
                }

                if (QMCache.Instance.ActiveShip.ArmorPercentage < lowArmorWarning && !WarpScrambled)
                {
                    Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones due to armor [" + Math.Round(QMCache.Instance.ActiveShip.ArmorPercentage, 0) + "%] below [" + lowArmorWarning + "%] minimum", Logging.Orange);
                    return true;
                }

                if (QMCache.Instance.ActiveShip.CapacitorPercentage < lowCapWarning && !WarpScrambled)
                {
                    Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones due to capacitor [" + Math.Round(QMCache.Instance.ActiveShip.CapacitorPercentage, 0) + "%] below [" + lowCapWarning + "%] minimum", Logging.Orange);
                    return true;
                }

                if (_States.CurrentQuestorState == QuestorState.CombatMissionsBehavior && !WarpScrambled)
                {
                    if (_States.CurrentCombatMissionBehaviorState == CombatMissionsBehaviorState.GotoBase && !WarpScrambled)
                    {
                        Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones due to gotobase state", Logging.Orange);
                        return true;
                    }

                    if (_States.CurrentCombatMissionBehaviorState == CombatMissionsBehaviorState.GotoMission && !WarpScrambled)
                    {
                        Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones due to gotomission state", Logging.Orange);
                        return true;
                    }

                    if (_States.CurrentCombatMissionBehaviorState == CombatMissionsBehaviorState.Panic && !WarpScrambled)
                    {
                        Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones due to panic state", Logging.Orange);
                        return true;
                    }
                }
                else if (_States.CurrentQuestorState == QuestorState.CombatHelperBehavior && !WarpScrambled)
                {
                    if (_States.CurrentCombatHelperBehaviorState == CombatHelperBehaviorState.Panic && !WarpScrambled)
                    {
                        Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones due to panic state", Logging.Orange);
                        return true;
                    }

                    if (_States.CurrentCombatHelperBehaviorState == CombatHelperBehaviorState.GotoBase && !WarpScrambled)
                    {
                        Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones due to panic state", Logging.Orange);
                        return true;
                    }
                }
                else if (_States.CurrentQuestorState == QuestorState.DedicatedBookmarkSalvagerBehavior && !WarpScrambled)
                {
                    if (_States.CurrentDedicatedBookmarkSalvagerBehaviorState == DedicatedBookmarkSalvagerBehaviorState.GotoBase && !WarpScrambled)
                    {
                        Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones due to gotobase state", Logging.Orange);
                        return true;
                    }

                    if (_States.CurrentDedicatedBookmarkSalvagerBehaviorState == DedicatedBookmarkSalvagerBehaviorState.Panic && !WarpScrambled)
                    {
                        Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones due to panic state", Logging.Orange);
                        return true;
                    }

                    if (_States.CurrentDedicatedBookmarkSalvagerBehaviorState == DedicatedBookmarkSalvagerBehaviorState.GotoNearestStation && !WarpScrambled)
                    {
                        Logging.Log("Drones", "Recalling [ " + Drones.ActiveDrones.Count() + " ] drones due to GotoNearestStation state", Logging.Orange);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logging.Log("ShouldWeRecallDrones", "Exception [" + ex + "]", Logging.Debug);
                return false;
            }
        }

        private static bool OnEveryDroneProcessState()
        {
            if (_nextDroneAction > DateTime.UtcNow || Logging.DebugDisableDrones) return false;

            if (Logging.DebugDrones) Logging.Log("Drones.ProcessState", "Entering Drones.ProcessState", Logging.Debug);
            _nextDroneAction = DateTime.UtcNow.AddMilliseconds(800);

            if (QMCache.Instance.InStation ||                 // There is really no combat in stations (yet)
                !QMCache.Instance.InSpace ||                  // if we are not in space yet, wait...
                QMCache.Instance.MyShipEntity == null ||      // What? No ship entity?
                QMCache.Instance.ActiveShip.Entity.IsCloaked  // There is no combat when cloaked
                )
            {
                if (Logging.DebugDrones) Logging.Log("Drones.ProcessState", "InStation [" + QMCache.Instance.InStation + "] InSpace [" + QMCache.Instance.InSpace + "] IsCloaked [" + QMCache.Instance.ActiveShip.Entity.IsCloaked + "] - doing nothing", Logging.Debug);
                _States.CurrentDroneState = DroneState.Idle;
                return false;
            }

            if (!Drones.UseDrones && Drones.ActiveDrones.Any())
            {
                if (Logging.DebugDrones) Logging.Log("Drones.ProcessState", "UseDrones [" + Drones.UseDrones + "]", Logging.Debug);
                if (!RecallingDronesState()) return false;
                return false;
            }

            if (Drones.ActiveDrones == null)
            {
                if (Logging.DebugDrones) Logging.Log("Drones.ProcessState", "ActiveDrones == null", Logging.Debug);
                _States.CurrentDroneState = DroneState.Idle;
                return false;
            }

            if (QMCache.Instance.MyShipEntity.IsShipWithNoDroneBay)
            {
                if (Logging.DebugDrones) Logging.Log("Drones.ProcessState", "IsShipWithNoDronesBay - Setting useDrones to false.", Logging.Debug);
                Drones.UseDrones = false;
                _States.CurrentDroneState = DroneState.Idle;
                return false;
            }

            if (!Drones.ActiveDrones.Any() && QMCache.Instance.InWarp)
            {
                if (Logging.DebugDrones) Logging.Log("Drones.ProcessState", "No Active Drones in space and we are InWarp - doing nothing", Logging.Debug);
                RemoveDronePriorityTargets(Drones.DronePriorityEntities.ToList());
                _States.CurrentDroneState = DroneState.Idle;
                return false;
            }

            if (!QMCache.Instance.EntitiesOnGrid.Any())
            {
                if (Logging.DebugDrones) Logging.Log("Drones.ProcessState", "Nothing to shoot on grid - doing nothing", Logging.Debug);
                RemoveDronePriorityTargets(Drones.DronePriorityEntities.ToList());
                _States.CurrentDroneState = DroneState.Idle;
                return false;
            }

            return true;
        }
        private static bool IdleDroneState()
        {
            //
            // below is the reasons we will start the combat state(s) - if the below is not met do nothing
            //
            if (QMCache.Instance.InSpace &&
                QMCache.Instance.ActiveShip.Entity != null &&
                !QMCache.Instance.ActiveShip.Entity.IsCloaked &&
                QMCache.Instance.ActiveShip.GivenName.ToLower() == Combat.CombatShipName &&
                Drones.UseDrones &&
                !QMCache.Instance.InWarp)
            {
                _States.CurrentDroneState = DroneState.WaitingForTargets;
                return true;
            }

            return false;
        }

        private static bool WaitingForTargetsDroneState()
        {
            // Are we in the right state ?
            if (Drones.ActiveDrones.Any())
            {
                // Apparently not, we have drones out, go into fight mode
                _States.CurrentDroneState = DroneState.Fighting;
                return true;
            }

            if (QMCache.Instance.Targets.Any() || (Combat.PotentialCombatTargets.Any() && DronesDontNeedTargetsBecauseWehaveThemSetOnAggressive))
            {
                // Should we launch drones?
                if (!ShouldWeLaunchDrones()) return false;

                // Reset launch tries
                _launchTries = 0;
                _lastLaunch = DateTime.UtcNow;
                _States.CurrentDroneState = DroneState.Launch;
                return true;
            }

            return true;
        }

        private static bool LaunchDronesState()
        {
            if (Logging.DebugDrones) Logging.Log("Drones.Launch", "LaunchAllDrones", Logging.Debug);
            // Launch all drones
            _launchTimeout = DateTime.UtcNow;
            QMCache.Instance.ActiveShip.LaunchAllDrones();
            _States.CurrentDroneState = DroneState.Launching;
            return true;
        }

        private static bool LaunchingDronesState()
        {
            if (Logging.DebugDrones) Logging.Log("Drones.Launching", "Entering Launching State...", Logging.Debug);
            // We haven't launched anything yet, keep waiting
            if (!Drones.ActiveDrones.Any())
            {
                if (Logging.DebugDrones) Logging.Log("Drones.Launching", "No Drones in space yet. waiting", Logging.Debug);
                if (DateTime.UtcNow.Subtract(_launchTimeout).TotalSeconds > 10)
                {
                    // Relaunch if tries < 5
                    if (_launchTries < 5)
                    {
                        _launchTries++;
                        _States.CurrentDroneState = DroneState.Launch;
                        return true;
                    }

                    _States.CurrentDroneState = DroneState.OutOfDrones;
                }

                return true;
            }

            // Are we done launching?
            if (_lastDroneCount == Drones.ActiveDrones.Count())
            {
                Logging.Log("Drones", "[" + Drones.ActiveDrones.Count() + "] Drones Launched", Logging.Magenta);
                _States.CurrentDroneState = DroneState.Fighting;
                return true;
            }

            return true;
        }

        private static bool OutOfDronesDronesState()
        {
            if (Drones.UseDrones && _States.CurrentCombatMissionBehaviorState == CombatMissionsBehaviorState.ExecuteMission) //&& QMSettings.Instance.CharacterMode == "CombatMissions")
            {
                if (Statistics.OutOfDronesCount >= 3)
                {
                    Logging.Log("Drones", "We are Out of Drones! AGAIN - Headed back to base to stay!", Logging.Red);
                    _States.CurrentCombatMissionBehaviorState = CombatMissionsBehaviorState.GotoBase;
                    Statistics.MissionCompletionErrors = 10; //this effectively will stop questor in station so we do not try to do this mission again, this needs human intervention if we have lots this many drones
                    Statistics.OutOfDronesCount++;
                }

                Logging.Log("Drones", "We are Out of Drones! - Headed back to base to Re-Arm", Logging.Red);
                _States.CurrentCombatMissionBehaviorState = CombatMissionsBehaviorState.GotoBase;
                Statistics.OutOfDronesCount++;
                return true;
            }

            return true;
        }

        private static bool FightingDronesState()
        {
            if (Logging.DebugDrones) Logging.Log("Drones.Fighting", "Should we recall our drones? This is a possible list of reasons why we should", Logging.Debug);

            if (!Drones.ActiveDrones.Any())
            {
                Logging.Log("Drones", "Apparently we have lost all our drones", Logging.Orange);
                _States.CurrentDroneState = DroneState.Idle;
                return false;
            }

            if (Combat.PotentialCombatTargets.Any(pt => pt.IsWarpScramblingMe))
            {
                EntityCache WarpScrambledBy = QMCache.Instance.Targets.OrderBy(d => d.Distance).ThenByDescending(i => i.IsWarpScramblingMe).FirstOrDefault();
                if (WarpScrambledBy != null && DateTime.UtcNow > _nextWarpScrambledWarning)
                {
                    _nextWarpScrambledWarning = DateTime.UtcNow.AddSeconds(20);
                    Logging.Log("Drones", "We are scrambled by: [" + Logging.White + WarpScrambledBy.Name + Logging.Orange + "][" + Logging.White + Math.Round(WarpScrambledBy.Distance, 0) + Logging.Orange + "][" + Logging.White + WarpScrambledBy.Id + Logging.Orange + "]", Logging.Orange);
                    WarpScrambled = true;
                }
            }
            else
            {
                //Logging.Log("Drones: We are not warp scrambled at the moment...");
                WarpScrambled = false;
            }

            if (ShouldWeRecallDrones())
            {
                Statistics.DroneRecalls++;
                _States.CurrentDroneState = DroneState.Recalling;
                return true;
            }

            if (Logging.DebugDrones) Logging.Log("Drones.Fighting", "EngageTarget(); - before", Logging.Debug);

            EngageTarget();

            if (Logging.DebugDrones) Logging.Log("Drones.Fighting", "EngageTarget(); - after", Logging.Debug);
            // We lost a drone and did not recall, assume panicking and launch (if any) additional drones
            if (Drones.ActiveDrones.Count() < _lastDroneCount)
            {
                _States.CurrentDroneState = DroneState.Launch;
            }

            return true;
        }

        private static bool RecallingDronesState()
        {
            // Give recall command every x seconds (default is 15)
            if (DateTime.UtcNow.Subtract(_lastRecallCommand).TotalSeconds > Time.Instance.RecallDronesDelayBetweenRetries + QMCache.Instance.RandomNumber(0, 2))
            {
                QMCache.Instance.DirectEve.ExecuteCommand(DirectCmd.CmdDronesReturnToBay);
                _lastRecallCommand = DateTime.UtcNow;
                return true;
            }

            // Are we done?
            if (!Drones.ActiveDrones.Any())
            {
                _lastRecall = DateTime.UtcNow;
                _nextDroneAction = DateTime.UtcNow.AddSeconds(3);
                if (!Drones.UseDrones)
                {
                    _States.CurrentDroneState = DroneState.Idle;
                    return false;
                }

                _States.CurrentDroneState = DroneState.WaitingForTargets;
                return true;
            }

            return true;
        }

        public static void ProcessState()
        {
            try
            {
                if (!OnEveryDroneProcessState()) return;

                switch (_States.CurrentDroneState)
                {
                    case DroneState.WaitingForTargets:
                        if (!WaitingForTargetsDroneState()) return;
                        break;

                    case DroneState.Launch:
                        if (!LaunchDronesState()) return;
                        break;

                    case DroneState.Launching:
                        if (!LaunchingDronesState()) return;
                        break;

                    case DroneState.OutOfDrones:
                        if (!OutOfDronesDronesState()) return;
                        break;

                    case DroneState.Fighting:
                        if (!FightingDronesState()) return;
                        break;

                    case DroneState.Recalling:
                        if (!RecallingDronesState()) return;
                        break;

                    case DroneState.Idle:
                        if (!IdleDroneState()) return;
                        break;
                }

                // Update health values
                _activeDronesShieldTotalOnLastPulse = GetActiveDroneShieldTotal();
                _activeDronesArmorTotalOnLastPulse = GetActiveDroneArmorTotal();
                _activeDronesStructureTotalOnLastPulse = GetActiveDroneStructureTotal();
                _activeDronesShieldPercentageOnLastPulse = GetActiveDroneShieldPercentage();
                _activeDronesArmorPercentageOnLastPulse = GetActiveDroneArmorPercentage();
                _activeDronesStructurePercentageOnLastPulse = GetActiveDroneStructurePercentage();
                _lastDroneCount = Drones.ActiveDrones.Count();
            }
            catch (Exception ex)
            {
                Logging.Log("Drones.ProcessState","Exception [" + ex + "]",Logging.Debug);
                return;
            }
        }

        /// <summary>
        ///   Invalidate the cached items every pulse (called from cache.invalidatecache, which itself is called every frame in questor.cs)
        /// </summary>
        public static void InvalidateCache()
        {
            try
            {
                //
                // this list of variables is cleared every pulse.
                //
                _activeDrones = null;
                _droneBay = null;
                _dronePriorityEntities = null;
                _maxDroneRange = null;
                _preferredDroneTarget = null;

                if (_dronePriorityTargets != null && _dronePriorityTargets.Any())
                {
                    _dronePriorityTargets.ForEach(pt => pt.ClearCache());
                }
            }
            catch (Exception exception)
            {
                Logging.Log("Drones.InvalidateCache", "Exception [" + exception + "]", Logging.Debug);
            }
        }
    }
}
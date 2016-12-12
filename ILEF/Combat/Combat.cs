// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------

namespace ILEF.Combat
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ILoveEVE.Framework;
    using ILEF.Activities;
    using ILEF.BackgroundTasks;
    using ILEF.Caching;
    using ILEF.Logging;
    using ILEF.Lookup;
    using ILEF.States;

    /// <summary>
    ///   The combat class will target and kill any NPC that is targeting the questor.
    ///   It will also kill any NPC that is targeted but not aggressive  toward the questor.
    /// </summary>
    public static class Combat
    {
        static Combat()
        {
            //Interlocked.Increment(ref CombatInstances);
            Ammo = new List<Ammo>();
        }

        //~Combat()
        //{
        //    Interlocked.Decrement(ref CombatInstances);
        //}

        private static string _combatShipName;
        public static string CombatShipName
        {
            get
            {
                if (MissionSettings.MissionSpecificShip != null)
                {
                    return MissionSettings.MissionSpecificShip;
                }

                if (MissionSettings.FactionSpecificShip != null)
                {
                    return MissionSettings.FactionSpecificShip;
                }

                return _combatShipName;
            }
            set
            {
                _combatShipName = value;
            }
        }
        private static bool _isJammed;
        private static int _weaponNumber;
        private static int MaxCharges { get; set; }
        private static DateTime _lastCombatProcessState;
        private static DateTime _lastReloadAll;
        //private static int _reloadAllIteration;
        public static IEnumerable<EntityCache> highValueTargetsTargeted;
        public static IEnumerable<EntityCache> lowValueTargetsTargeted;
        public static int? maxHighValueTargets;
        public static int? maxLowValueTargets;

        private static int maxTotalTargets
        {
            get
            {
                try
                {
                    return maxHighValueTargets + maxLowValueTargets ?? 2;
                }
                catch (Exception ex)
                {
                    Logging.Log("Combat.maxTotalTargets", "Exception [" + ex + "]", Logging.Debug);
                    return 2;
                }
            }
        }
        //public static int CombatInstances = 0;
        private static int icount = 0;
        public static int NosDistance { get; set; }
        public static int RemoteRepairDistance { get; set; }
        public static List<EntityCache> TargetingMe { get; set; }
        public static List<EntityCache> NotYetTargetingMe { get; set; }
        private static bool _killSentries;
        public static bool KillSentries
        {
            get
            {
                if (MissionSettings.MissionKillSentries != null)
                    return (bool)MissionSettings.MissionKillSentries;
                return _killSentries;
            }
            set
            {
                _killSentries = value;
            }
        }
        public static bool DontShootFrigatesWithSiegeorAutoCannons { get; set; }
        public static int WeaponGroupId { get; set; }
        //public static int MaximumHighValueTargets { get; set; }
        //public static int MaximumLowValueTargets { get; set; }
        public static int MinimumAmmoCharges { get; set; }
        public static List<Ammo> Ammo { get; set; }
        public static List<MiningCrystals> MiningCrystals { get; private set; }
        public static int MinimumTargetValueToConsiderTargetAHighValueTarget { get; set; }
        public static int MaximumTargetValueToConsiderTargetALowValueTarget { get; set; }
        public static bool SelectAmmoToUseBasedOnShipSize { get; set; }
        public static int DoNotSwitchTargetsIfTargetHasMoreThanThisArmorDamagePercentage { get; set; }
        public static double DistanceNPCFrigatesShouldBeIgnoredByPrimaryWeapons { get; set; } //also requires SpeedFrigatesShouldBeIgnoredByMainWeapons
        public static double SpeedNPCFrigatesShouldBeIgnoredByPrimaryWeapons { get; set; } //also requires DistanceFrigatesShouldBeIgnoredByMainWeapons
        public static bool AddWarpScramblersToPrimaryWeaponsPriorityTargetList { get; set; }
        public static bool AddWebifiersToPrimaryWeaponsPriorityTargetList { get; set; }
        public static bool AddDampenersToPrimaryWeaponsPriorityTargetList { get; set; }
        public static bool AddNeutralizersToPrimaryWeaponsPriorityTargetList { get; set; }
        public static bool AddTargetPaintersToPrimaryWeaponsPriorityTargetList { get; set; }
        public static bool AddECMsToPrimaryWeaponsPriorityTargetList { get; set; }
        public static bool AddTrackingDisruptorsToPrimaryWeaponsPriorityTargetList { get; set; }
        public static double ListPriorityTargetsEveryXSeconds { get; set; }
        public static double InsideThisRangeIsHardToTrack { get; set; }

        /// <summary>
        ///   Targeted by cache //cleared in InvalidateCache
        /// </summary>
        private static  List<EntityCache> _targetedBy;
        public static IEnumerable<EntityCache> TargetedBy
        {
            get { return _targetedBy ?? (_targetedBy = PotentialCombatTargets.Where(e => e.IsTargetedBy).ToList()); }
        }

        /// <summary>
        ///   Aggressed cache //cleared in InvalidateCache
        /// </summary>
        private static List<EntityCache> _aggressed;
        public static IEnumerable<EntityCache> Aggressed
        {
            get { return _aggressed ?? (_aggressed = PotentialCombatTargets.Where(e => e.IsAttacking).ToList()); }
        }

        //
        // entities that have been locked (or are being locked now)
        // entities that are IN range
        // entities that eventually we want to shoot (and now that they are locked that will happen shortly)
        //
        public static IEnumerable<EntityCache> combatTargets
        {
            get
            {
                if (_combatTargets == null)
                {
                    //List<EntityCache>
                    if (QMCache.Instance.InSpace)
                    {
                        if (_combatTargets == null)
                        {
                            List<EntityCache> targets = new List<EntityCache>();
                            targets.AddRange(QMCache.Instance.Targets);
                            targets.AddRange(QMCache.Instance.Targeting);

                            _combatTargets = targets.Where(e => e.CategoryId == (int)CategoryID.Entity && e.Distance < (double)Distances.OnGridWithMe
                                                                && !e.IsIgnored
                                                                && (!e.IsSentry || (e.IsSentry && Combat.KillSentries) || (e.IsSentry && e.IsEwarTarget))
                                                                && (e.IsNpc || e.IsNpcByGroupID)
                                                                && e.Distance < Combat.MaxRange
                                                                && !e.IsContainer
                                                                && !e.IsFactionWarfareNPC
                                                                && !e.IsEntityIShouldLeaveAlone
                                                                && !e.IsBadIdea
                                                                && !e.IsCelestial
                                                                && !e.IsAsteroid)
                                                                .ToList();

                            return _combatTargets;
                        }

                        return _combatTargets;
                    }

                    return QMCache.Instance.Targets.ToList();
                }

                return _combatTargets;
            }
        }

        //
        // entities that have potentially not been locked yet
        // entities that may not be in range yet
        // entities that eventually we want to shoot
        //
        public static IEnumerable<EntityCache> PotentialCombatTargets
        {
            get
            {
                if (_potentialCombatTargets == null)
                {
                    //List<EntityCache>
                    if (QMCache.Instance.InSpace)
                    {
                        _potentialCombatTargets = QMCache.Instance.EntitiesOnGrid.Where(e => e.CategoryId == (int)CategoryID.Entity
                                                            && !e.IsIgnored
                                                            && (!e.IsSentry || (e.IsSentry && Combat.KillSentries) || (e.IsSentry && e.IsEwarTarget))
                                                            && (e.IsNpcByGroupID || e.IsAttacking) //|| e.isPreferredPrimaryWeaponTarget || e.IsPrimaryWeaponKillPriority || e.IsDronePriorityTarget || e.isPreferredDroneTarget) //|| e.IsNpc)
                            //&& !e.IsTarget
                                                            && !e.IsContainer
                                                            && !e.IsFactionWarfareNPC
                                                            && !e.IsEntityIShouldLeaveAlone
                                                            && !e.IsBadIdea // || e.IsBadIdea && e.IsAttacking)
                                                            && (!e.IsPlayer || e.IsPlayer && e.IsAttacking)
                                                            && !e.IsMiscJunk
                                                            && (!e.IsLargeCollidable || e.IsPrimaryWeaponPriorityTarget)
                                                            )
                                                            .ToList();

                        if (_potentialCombatTargets == null || !_potentialCombatTargets.Any())
                        {
                            _potentialCombatTargets = new List<EntityCache>();
                        }

                        return _potentialCombatTargets;
                    }

                    return new List<EntityCache>();
                }

                return _potentialCombatTargets;
            }
        }


        private static double? _maxrange;

        public static double MaxRange
        {
            get
            {
                if (_maxrange == null)
                {
                    _maxrange = Math.Min(QMCache.Instance.WeaponRange, MaxTargetRange);
                    return _maxrange ?? 0;
                }

                return _maxrange ?? 0;
            }
        }

        private static double? _maxTargetRange;

        public static double MaxTargetRange
        {
            get
            {
                if (_maxTargetRange == null)
                {
                    _maxTargetRange = QMCache.Instance.ActiveShip.MaxTargetRange;
                    return _maxTargetRange ?? 0;
                }

                return _maxTargetRange ?? 0;
            }
        }

        public static double LowValueTargetsHaveToBeWithinDistance
        {
            get
            {
                if (Drones.UseDrones && Drones.MaxDroneRange != 0)
                {
                    return Drones.MaxDroneRange;
                }

                //
                // if we are not using drones return min range (Weapons or targeting range whatever is lower)
                //
                return MaxRange;

            }
        }

        public static long? PreferredPrimaryWeaponTargetID;
        private static EntityCache _preferredPrimaryWeaponTarget;
        public static EntityCache PreferredPrimaryWeaponTarget
        {
            get
            {
                if (_preferredPrimaryWeaponTarget == null)
                {
                    if (PreferredPrimaryWeaponTargetID != null)
                    {
                        _preferredPrimaryWeaponTarget = QMCache.Instance.EntitiesOnGrid.FirstOrDefault(e => e.Id == PreferredPrimaryWeaponTargetID);

                        return _preferredPrimaryWeaponTarget ?? null;
                    }

                    return null;
                }

                return _preferredPrimaryWeaponTarget;
            }
            set
            {
                if (value == null)
                {
                    if (_preferredPrimaryWeaponTarget != null)
                    {
                        _preferredPrimaryWeaponTarget = null;
                        PreferredPrimaryWeaponTargetID = null;
                        if (Logging.DebugPreferredPrimaryWeaponTarget) Logging.Log("PreferredPrimaryWeaponTarget.Set", "[ null ]", Logging.Debug);
                        return;
                    }
                }
                else if ((_preferredPrimaryWeaponTarget != null && _preferredPrimaryWeaponTarget.Id != value.Id) || _preferredPrimaryWeaponTarget == null)
                {
                    _preferredPrimaryWeaponTarget = value;
                    PreferredPrimaryWeaponTargetID = value.Id;
                    if (Logging.DebugPreferredPrimaryWeaponTarget) Logging.Log("PreferredPrimaryWeaponTarget.Set", value.Name + " [" + value.MaskedId + "][" + Math.Round(value.Distance / 1000, 0) + "k] isTarget [" + value.IsTarget + "]", Logging.Debug);
                    return;
                }

                //if (Logging.DebugPreferredPrimaryWeaponTarget) Logging.Log("PreferredPrimaryWeaponTarget", "QMCache.Instance._preferredPrimaryWeaponTarget [" + QMCache.Instance._preferredPrimaryWeaponTarget.Name + "] is already set (no need to change)", Logging.Debug);
                return;
            }
        }

        private static List<PriorityTarget> _primaryWeaponPriorityTargetsPerFrameCaching;

        private static List<PriorityTarget> _primaryWeaponPriorityTargets;

        public static List<PriorityTarget> PrimaryWeaponPriorityTargets
        {
            get
            {
                try
                {
                    if (_primaryWeaponPriorityTargetsPerFrameCaching == null)
                    {
                        //
                        // remove targets that no longer exist
                        //
                        if (_primaryWeaponPriorityTargets != null && _primaryWeaponPriorityTargets.Any())
                        {
                            foreach (PriorityTarget _primaryWeaponPriorityTarget in _primaryWeaponPriorityTargets)
                            {
                                if (QMCache.Instance.EntitiesOnGrid.All(e => e.Id != _primaryWeaponPriorityTarget.EntityID))
                                {
                                    Logging.Log("PrimaryWeaponPriorityTargets", "Remove Target that is no longer in the Entities list [" + _primaryWeaponPriorityTarget.Name + "]ID[" + _primaryWeaponPriorityTarget.MaskedID + "] PriorityLevel [" + _primaryWeaponPriorityTarget.PrimaryWeaponPriority + "]", Logging.Debug);
                                    _primaryWeaponPriorityTargets.Remove(_primaryWeaponPriorityTarget);
                                    break;
                                }
                            }

                            _primaryWeaponPriorityTargetsPerFrameCaching = _primaryWeaponPriorityTargets;
                            return _primaryWeaponPriorityTargets;
                        }

                        //
                        // initialize a fresh list - to be filled in during panic (updated every tick)
                        //
                        _primaryWeaponPriorityTargets = new List<PriorityTarget>();
                        _primaryWeaponPriorityTargetsPerFrameCaching = _primaryWeaponPriorityTargets;
                        return _primaryWeaponPriorityTargets;
                    }

                    return _primaryWeaponPriorityTargetsPerFrameCaching;
                }
                catch (NullReferenceException)
                {
                    return null;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.PrimaryWeaponPriorityEntities", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
            set
            {
                _primaryWeaponPriorityTargets = value;
            }
        }

        private static IEnumerable<EntityCache> _primaryWeaponPriorityEntities;

        public static IEnumerable<EntityCache> PrimaryWeaponPriorityEntities
        {
            get
            {
                try
                {
                    //
                    // every frame re-populate the PrimaryWeaponPriorityEntities from the list of IDs we have tucked away in PrimaryWeaponPriorityEntities
                    // this occurs because in Invalidatecache() we are, necessarily,  clearing this every frame!
                    //
                    if (_primaryWeaponPriorityEntities == null)
                    {
                        if (_primaryWeaponPriorityTargets != null && _primaryWeaponPriorityTargets.Any())
                        {
                            _primaryWeaponPriorityEntities = PrimaryWeaponPriorityTargets.OrderByDescending(pt => pt.PrimaryWeaponPriority).ThenBy(pt => pt.Entity.Distance).Select(pt => pt.Entity).ToList();
                            return _primaryWeaponPriorityEntities;
                        }

                        if (Logging.DebugAddPrimaryWeaponPriorityTarget) Logging.Log("PrimaryWeaponPriorityEntities", "if (_primaryWeaponPriorityTargets.Any()) none available yet", Logging.Debug);
                        _primaryWeaponPriorityEntities = new List<EntityCache>();
                        return _primaryWeaponPriorityEntities;
                    }

                    //
                    // if we have already populated the list this frame return the list we already generated
                    //
                    return _primaryWeaponPriorityEntities;
                }
                catch (NullReferenceException)
                {
                    return null;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.PrimaryWeaponPriorityEntities", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
        }

        /// <summary>
        ///   Remove targets from priority list
        /// </summary>
        /// <param name = "targets"></param>
        public static bool RemovePrimaryWeaponPriorityTargets(List<EntityCache> targets)
        {
            try
            {
                targets = targets.ToList();

                if (targets.Any() && _primaryWeaponPriorityTargets != null && _primaryWeaponPriorityTargets.Any() && _primaryWeaponPriorityTargets.Any(pt => targets.Any(t => t.Id == pt.EntityID)))
                {
                    _primaryWeaponPriorityTargets.RemoveAll(pt => targets.Any(t => t.Id == pt.EntityID));
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logging.Log("RemovePrimaryWeaponPriorityTargets", "Exception [" + ex + "]", Logging.Debug);
            }

            return false;
        }

        public static void AddPrimaryWeaponPriorityTarget(EntityCache ewarEntity, PrimaryWeaponPriority priority, string module, bool AddEwarTypeToPriorityTargetList = true)
        {
            try
            {
                if ((ewarEntity.IsIgnored) || PrimaryWeaponPriorityTargets.Any(p => p.EntityID == ewarEntity.Id))
                {
                    if (Logging.DebugAddPrimaryWeaponPriorityTarget) Logging.Log("AddPrimaryWeaponPriorityTargets", "if ((target.IsIgnored) || PrimaryWeaponPriorityTargets.Any(p => p.Id == target.Id)) continue", Logging.Debug);
                    return;
                }

                if (AddEwarTypeToPriorityTargetList)
                {
                    //
                    // Primary Weapons
                    //
                    if (DoWeCurrentlyHaveTurretsMounted() && (ewarEntity.IsNPCFrigate || ewarEntity.IsFrigate)) //we use turrets, and this PrimaryWeaponPriorityTarget is a frigate
                    {
                        if (!ewarEntity.IsTooCloseTooFastTooSmallToHit)
                        {
                            if (PrimaryWeaponPriorityTargets.All(e => e.EntityID != ewarEntity.Id))
                            {
                                Logging.Log(module, "Adding [" + ewarEntity.Name + "] Speed [" + Math.Round(ewarEntity.Velocity, 2) + "m/s] Distance [" + Math.Round(ewarEntity.Distance / 1000, 2) + "k] [ID: " + ewarEntity.MaskedId + "] as a PrimaryWeaponPriorityTarget [" + priority.ToString() + "]", Logging.White);
                                _primaryWeaponPriorityTargets.Add(new PriorityTarget { Name = ewarEntity.Name, EntityID = ewarEntity.Id, PrimaryWeaponPriority = priority });
                                if (Logging.DebugKillAction)
                                {
                                    Logging.Log("Statistics", "Entering StatisticsState.ListPrimaryWeaponPriorityTargets", Logging.Debug);
                                    _States.CurrentStatisticsState = StatisticsState.ListPrimaryWeaponPriorityTargets;
                                }
                            }
                        }

                        return;
                    }

                    if (PrimaryWeaponPriorityTargets.All(e => e.EntityID != ewarEntity.Id))
                    {
                        Logging.Log(module, "Adding [" + ewarEntity.Name + "] Speed [" + Math.Round(ewarEntity.Velocity, 2) + "m/s] Distance [" + Math.Round(ewarEntity.Distance / 1000, 2) + "] [ID: " + ewarEntity.MaskedId + "] as a PrimaryWeaponPriorityTarget [" + priority.ToString() + "]", Logging.White);
                        _primaryWeaponPriorityTargets.Add(new PriorityTarget { Name = ewarEntity.Name, EntityID = ewarEntity.Id, PrimaryWeaponPriority = priority });
                        if (Logging.DebugKillAction)
                        {
                            Logging.Log("Statistics", "Entering StatisticsState.ListPrimaryWeaponPriorityTargets", Logging.Debug);
                            _States.CurrentStatisticsState = StatisticsState.ListPrimaryWeaponPriorityTargets;
                        }
                    }

                    return;
                }

                return;
            }
            catch (Exception ex)
            {
                Logging.Log("AddPrimaryWeaponPriorityTarget", "Exception [" + ex + "]", Logging.Debug);
            }

            return;
        }

        public static void AddPrimaryWeaponPriorityTargets(IEnumerable<EntityCache> ewarEntities, PrimaryWeaponPriority priority, string module, bool AddEwarTypeToPriorityTargetList = true)
        {
            try
            {
                ewarEntities = ewarEntities.ToList();
                if (ewarEntities.Any())
                {
                    foreach (EntityCache ewarEntity in ewarEntities)
                    {
                        AddPrimaryWeaponPriorityTarget(ewarEntity, priority, module, AddEwarTypeToPriorityTargetList);
                    }
                }

                return;
            }
            catch (Exception ex)
            {
                Logging.Log("AddPrimaryWeaponPriorityTargets", "Exception [" + ex + "]", Logging.Debug);
            }

            return;
        }

        public static void AddPrimaryWeaponPriorityTargetsByName(string stringEntitiesToAdd)
        {
            try
            {
                if (QMCache.Instance.EntitiesOnGrid.Any(r => r.Name == stringEntitiesToAdd))
                {
                    IEnumerable<EntityCache> entitiesToAdd = QMCache.Instance.EntitiesOnGrid.Where(t => t.Name == stringEntitiesToAdd).ToList();
                    if (entitiesToAdd.Any())
                    {

                        foreach (EntityCache entityToAdd in entitiesToAdd)
                        {
                            AddPrimaryWeaponPriorityTarget(entityToAdd, PrimaryWeaponPriority.PriorityKillTarget, "AddPWPTByName");
                            continue;
                        }

                        return;
                    }

                    Logging.Log("Adding PWPT", "[" + stringEntitiesToAdd + "] was not found.", Logging.Debug);
                    return;
                }

                int EntitiesOnGridCount = 0;
                if (QMCache.Instance.EntitiesOnGrid.Any())
                {
                    EntitiesOnGridCount = QMCache.Instance.EntitiesOnGrid.Count();
                }

                int EntitiesCount = 0;
                if (QMCache.Instance.EntitiesOnGrid.Any())
                {
                    EntitiesCount = QMCache.Instance.EntitiesOnGrid.Count();
                }

                Logging.Log("Adding PWPT", "[" + stringEntitiesToAdd + "] was not found. [" + EntitiesOnGridCount + "] entities on grid [" + EntitiesCount + "] entities", Logging.Debug);
                return;
            }
            catch (Exception ex)
            {
                Logging.Log("AddPrimaryWeaponPriorityTargetsByName", "Exception [" + ex + "]", Logging.Debug);
            }

            return;
        }

        public static void RemovePrimaryWeaponPriorityTargetsByName(string stringEntitiesToRemove)
        {
            try
            {
                IEnumerable<EntityCache> entitiesToRemove = QMCache.Instance.EntitiesByName(stringEntitiesToRemove, QMCache.Instance.EntitiesOnGrid).ToList();
                if (entitiesToRemove.Any())
                {
                    Logging.Log("RemovingPWPT", "removing [" + stringEntitiesToRemove + "] from the PWPT List", Logging.Debug);
                    RemovePrimaryWeaponPriorityTargets(entitiesToRemove.ToList());
                    return;
                }

                Logging.Log("RemovingPWPT", "[" + stringEntitiesToRemove + "] was not found on grid", Logging.Debug);
                return;
            }
            catch (Exception ex)
            {
                Logging.Log("RemovePrimaryWeaponPriorityTargetsByName", "Exception [" + ex + "]", Logging.Debug);
            }
        }

        public static void AddWarpScramblerByName(string stringEntitiesToAdd, int numberToIgnore = 0, bool notTheClosest = false)
        {
            try
            {
                IEnumerable<EntityCache> entitiesToAdd = QMCache.Instance.EntitiesByName(stringEntitiesToAdd, QMCache.Instance.EntitiesOnGrid).OrderBy(k => k.Distance).ToList();
                if (notTheClosest)
                {
                    entitiesToAdd = entitiesToAdd.OrderByDescending(m => m.Distance);
                }

                if (entitiesToAdd.Any())
                {
                    foreach (EntityCache entityToAdd in entitiesToAdd)
                    {
                        if (numberToIgnore > 0)
                        {
                            numberToIgnore--;
                            continue;
                        }

                        Logging.Log("AddWarpScramblerByName", "adding [" + entityToAdd.Name + "][" + Math.Round(entityToAdd.Distance / 1000, 0) + "k][" + entityToAdd.MaskedId + "] to the WarpScrambler List", Logging.Debug);
                        if (!QMCache.Instance.ListOfWarpScramblingEntities.Contains(entityToAdd.Id))
                        {
                            QMCache.Instance.ListOfWarpScramblingEntities.Add(entityToAdd.Id);
                        }
                        continue;
                    }

                    return;
                }

                Logging.Log("AddWarpScramblerByName", "[" + stringEntitiesToAdd + "] was not found on grid", Logging.Debug);
                return;
            }
            catch (Exception ex)
            {
                Logging.Log("AddWarpScramblerByName", "Exception [" + ex + "]", Logging.Debug);
            }
        }

        public static void AddWebifierByName(string stringEntitiesToAdd, int numberToIgnore = 0, bool notTheClosest = false)
        {
            try
            {
                IEnumerable<EntityCache> entitiesToAdd = QMCache.Instance.EntitiesByName(stringEntitiesToAdd, QMCache.Instance.EntitiesOnGrid).OrderBy(j => j.Distance).ToList();
                if (notTheClosest)
                {
                    entitiesToAdd = entitiesToAdd.OrderByDescending(e => e.Distance);
                }

                if (entitiesToAdd.Any())
                {
                    foreach (EntityCache entityToAdd in entitiesToAdd)
                    {
                        if (numberToIgnore > 0)
                        {
                            numberToIgnore--;
                            continue;
                        }
                        Logging.Log("AddWebifierByName", "adding [" + entityToAdd.Name + "][" + Math.Round(entityToAdd.Distance / 1000, 0) + "k][" + entityToAdd.MaskedId + "] to the Webifier List", Logging.Debug);
                        if (!QMCache.Instance.ListofWebbingEntities.Contains(entityToAdd.Id))
                        {
                            QMCache.Instance.ListofWebbingEntities.Add(entityToAdd.Id);
                        }
                        continue;
                    }

                    return;
                }

                Logging.Log("AddWebifierByName", "[" + stringEntitiesToAdd + "] was not found on grid", Logging.Debug);
                return;
            }
            catch (Exception ex)
            {
                Logging.Log("AddWebifierByName", "Exception [" + ex + "]", Logging.Debug);
            }
        }
        /// <summary>
        ///   _CombatTarget Entities cache - list of things we have targeted to kill //cleared in InvalidateCache
        /// </summary>
        private static List<EntityCache> _combatTargets;

        /// <summary>
        ///   _PotentialCombatTarget Entities cache - list of things we can kill //cleared in InvalidateCache
        /// </summary>
        private static List<EntityCache> _potentialCombatTargets;

        public static bool? _doWeCurrentlyHaveTurretsMounted;
        public static bool DoWeCurrentlyHaveTurretsMounted()
        {
            try
            {
                if (_doWeCurrentlyHaveTurretsMounted == null)
                {
                    //int ModuleNumber = 0;
                    foreach (ModuleCache m in QMCache.Instance.Modules)
                    {
                        if (m.GroupId == (int)Group.ProjectileWeapon
                         || m.GroupId == (int)Group.EnergyWeapon
                         || m.GroupId == (int)Group.HybridWeapon
                            //|| m.GroupId == (int)Group.CruiseMissileLaunchers
                            //|| m.GroupId == (int)Group.RocketLaunchers
                            //|| m.GroupId == (int)Group.StandardMissileLaunchers
                            //|| m.GroupId == (int)Group.TorpedoLaunchers
                            //|| m.GroupId == (int)Group.AssaultMissilelaunchers
                            //|| m.GroupId == (int)Group.HeavyMissilelaunchers
                            //|| m.GroupId == (int)Group.DefenderMissilelaunchers
                           )
                        {
                            _doWeCurrentlyHaveTurretsMounted = true;
                            return _doWeCurrentlyHaveTurretsMounted ?? true;
                        }

                        continue;
                    }

                    _doWeCurrentlyHaveTurretsMounted = false;
                    return _doWeCurrentlyHaveTurretsMounted ?? false;
                }

                return _doWeCurrentlyHaveTurretsMounted ?? false;
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.DoWeCurrentlyHaveTurretsMounted", "Exception [" + exception + "]", Logging.Debug);
            }

            return false;
        }

        public static EntityCache CurrentWeaponTarget()
        {
            // Find the first active weapon's target
            EntityCache _currentWeaponTarget = null;
            double OptimalOfWeapon = 0;
            double FallOffOfWeapon = 0;

            try
            {
                // Find the target associated with the weapon
                ModuleCache weapon = QMCache.Instance.Weapons.FirstOrDefault(m => m.IsOnline
                                                                                    && !m.IsReloadingAmmo
                                                                                    && !m.IsChangingAmmo
                                                                                    && m.IsActive);
                if (weapon != null)
                {
                    _currentWeaponTarget = QMCache.Instance.EntityById(weapon.TargetId);

                    //
                    // in a perfect world we'd always use the same guns / missiles across the board, for those that do not this will at least come up with sane numbers
                    //
                    if (OptimalOfWeapon <= 1)
                    {
                        OptimalOfWeapon = Math.Min(OptimalOfWeapon, weapon.OptimalRange);
                    }

                    if (FallOffOfWeapon <= 1)
                    {
                        FallOffOfWeapon = Math.Min(FallOffOfWeapon, weapon.FallOff);
                    }

                    if (_currentWeaponTarget != null && _currentWeaponTarget.IsReadyToShoot)
                    {
                        return _currentWeaponTarget;
                    }

                    return null;
                }

                return null;
            }
            catch (Exception exception)
            {
                Logging.Log("GetCurrentWeaponTarget", "exception [" + exception + "]", Logging.Debug);
            }

            return null;
        }

        public static EntityCache FindPrimaryWeaponPriorityTarget(EntityCache currentTarget, PrimaryWeaponPriority priorityType, bool AddECMTypeToPrimaryWeaponPriorityTargetList, double Distance, bool FindAUnTargetedEntity = true)
        {
            if (AddECMTypeToPrimaryWeaponPriorityTargetList)
            {
                //if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget", "Checking for Neutralizing priority targets (currentTarget first)", Logging.Teal);
                // Choose any Neutralizing primary weapon priority targets
                try
                {
                    EntityCache target = null;
                    try
                    {
                        if (Combat.PrimaryWeaponPriorityEntities.Any(pt => pt.PrimaryWeaponPriorityLevel == priorityType))
                        {
                            target = Combat.PrimaryWeaponPriorityEntities.Where(pt => ((FindAUnTargetedEntity || pt.IsReadyToShoot) && currentTarget != null && pt.Id == currentTarget.Id && pt.Distance < Distance && pt.IsActivePrimaryWeaponEwarType == priorityType && !pt.IsTooCloseTooFastTooSmallToHit)
                                                                                            || ((FindAUnTargetedEntity || pt.IsReadyToShoot) && pt.Distance < Distance && pt.PrimaryWeaponPriorityLevel == priorityType && !pt.IsTooCloseTooFastTooSmallToHit))
                                                                                    .OrderByDescending(pt => pt.IsReadyToShoot)
                                                                                    .ThenByDescending(pt => pt.IsCurrentTarget)
                                                                                    .ThenByDescending(pt => !pt.IsNPCFrigate)
                                                                                    .ThenByDescending(pt => pt.IsInOptimalRange)
                                                                                    .ThenBy(pt => (pt.ShieldPct + pt.ArmorPct + pt.StructurePct))
                                                                                    .ThenBy(pt => pt.Distance)
                                                                                    .FirstOrDefault();
                        }
                    }
                    catch (NullReferenceException) { }  // Not sure why this happens, but seems to be no problem

                    if (target != null)
                    {
                        if (!FindAUnTargetedEntity)
                        {
                            //if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget", "NeutralizingPrimaryWeaponPriorityTarget [" + NeutralizingPriorityTarget.Name + "][" + Math.Round(NeutralizingPriorityTarget.Distance / 1000, 2) + "k][" + QMCache.Instance.MaskedID(NeutralizingPriorityTarget.Id) + "] GroupID [" + NeutralizingPriorityTarget.GroupId + "]", Logging.Debug);
                            Logging.Log("FindPrimaryWeaponPriorityTarget", "if (!FindAUnTargetedEntity) Combat.PreferredPrimaryWeaponTargetID = [ " + target.Name + "][" + target.MaskedId + "]", Logging.White);
                            PreferredPrimaryWeaponTarget = target;
                            Time.Instance.LastPreferredPrimaryWeaponTargetDateTime = DateTime.UtcNow;
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


        public static EntityCache LastTargetPrimaryWeaponsWereShooting = null;

        public static EntityCache FindCurrentTarget()
        {
            try
            {
                EntityCache currentTarget = null;
                //
                // we cant do this because the target may need to be targeted again! ECM == bad
                //
                //if (Time.Instance.LastTargetWeWereShooting != null && QMCache.Instance.Entities.Any(i => i.Id == Time.Instance.LastTargetWeWereShooting.Id))
                //{
                //    currentTarget = Time.Instance.LastTargetWeWereShooting;
                //}

                if (currentTarget == null)
                {
                    if (CurrentWeaponTarget() != null
                    && CurrentWeaponTarget().IsReadyToShoot
                    && !CurrentWeaponTarget().IsIgnored)
                    {
                        LastTargetPrimaryWeaponsWereShooting = CurrentWeaponTarget();
                        currentTarget = LastTargetPrimaryWeaponsWereShooting;
                    }

                    if (DateTime.UtcNow < Time.Instance.LastPreferredPrimaryWeaponTargetDateTime.AddSeconds(6) && (PreferredPrimaryWeaponTarget != null && QMCache.Instance.EntitiesOnGrid.Any(t => t.Id == PreferredPrimaryWeaponTargetID)))
                    {
                        if (Logging.DebugGetBestTarget) Logging.Log("FindCurrentTarget", "We have a PreferredPrimaryWeaponTarget [" + PreferredPrimaryWeaponTarget.Name + "][" + Math.Round(PreferredPrimaryWeaponTarget.Distance / 1000, 0) + "k] that was chosen less than 6 sec ago, and is still alive.", Logging.Teal);
                    }
                }

                return currentTarget;
            }
            catch (Exception ex)
            {
                Logging.Log("FindCurrentTarget", "Exception [" + ex + "]", Logging.Debug);
                return null;
            }
        }

        public static bool CheckForPrimaryWeaponPriorityTargetsInOrder(EntityCache currentTarget, double distance)
        {
            try
            {
                // Do we have ANY warp scrambling entities targeted starting with currentTarget
                // this needs QMSettings.Instance.AddWarpScramblersToPrimaryWeaponsPriorityTargetList true, otherwise they will just get handled in any order below...
                if (FindPrimaryWeaponPriorityTarget(currentTarget, PrimaryWeaponPriority.WarpScrambler, Combat.AddWarpScramblersToPrimaryWeaponsPriorityTargetList, distance) != null)
                    return true;

                // Do we have ANY ECM entities targeted starting with currentTarget
                // this needs QMSettings.Instance.AddECMsToPrimaryWeaponsPriorityTargetList true, otherwise they will just get handled in any order below...
                if (FindPrimaryWeaponPriorityTarget(currentTarget, PrimaryWeaponPriority.Jamming, Combat.AddECMsToPrimaryWeaponsPriorityTargetList, distance) != null)
                    return true;

                // Do we have ANY tracking disrupting entities targeted starting with currentTarget
                // this needs QMSettings.Instance.AddTrackingDisruptorsToPrimaryWeaponsPriorityTargetList true, otherwise they will just get handled in any order below...
                if (FindPrimaryWeaponPriorityTarget(currentTarget, PrimaryWeaponPriority.TrackingDisrupting, Combat.AddTrackingDisruptorsToPrimaryWeaponsPriorityTargetList, distance) != null)
                    return true;

                // Do we have ANY Neutralizing entities targeted starting with currentTarget
                // this needs QMSettings.Instance.AddNeutralizersToPrimaryWeaponsPriorityTargetList true, otherwise they will just get handled in any order below...
                if (FindPrimaryWeaponPriorityTarget(currentTarget, PrimaryWeaponPriority.Neutralizing, Combat.AddNeutralizersToPrimaryWeaponsPriorityTargetList, distance) != null)
                    return true;

                // Do we have ANY Target Painting entities targeted starting with currentTarget
                // this needs QMSettings.Instance.AddTargetPaintersToPrimaryWeaponsPriorityTargetList true, otherwise they will just get handled in any order below...
                if (FindPrimaryWeaponPriorityTarget(currentTarget, PrimaryWeaponPriority.TargetPainting, Combat.AddTargetPaintersToPrimaryWeaponsPriorityTargetList, distance) != null)
                    return true;

                // Do we have ANY Sensor Dampening entities targeted starting with currentTarget
                // this needs QMSettings.Instance.AddDampenersToPrimaryWeaponsPriorityTargetList true, otherwise they will just get handled in any order below...
                if (FindPrimaryWeaponPriorityTarget(currentTarget, PrimaryWeaponPriority.Dampening, Combat.AddDampenersToPrimaryWeaponsPriorityTargetList, distance) != null)
                    return true;

                // Do we have ANY Webbing entities targeted starting with currentTarget
                // this needs QMSettings.Instance.AddWebifiersToPrimaryWeaponsPriorityTargetList true, otherwise they will just get handled in any order below...
                if (FindPrimaryWeaponPriorityTarget(currentTarget, PrimaryWeaponPriority.Webbing, Combat.AddWebifiersToPrimaryWeaponsPriorityTargetList, distance) != null)
                    return true;

                return false;
            }
            catch (Exception ex)
            {
                Logging.Log("CheckForPrimaryWeaponPriorityTargetsInOrder", "Exception [" + ex + "]", Logging.Debug);
                return false;
            }
        }

        /// <summary>
        ///   Return the best possible target (based on current target, distance and low value first)
        /// </summary>
        /// <param name="_potentialTargets"></param>
        /// <param name="distance"></param>
        /// <param name="lowValueFirst"></param>
        /// <param name="callingroutine"> </param>
        /// <returns></returns>
        public static bool GetBestPrimaryWeaponTarget(double distance, bool lowValueFirst, string callingroutine, List<EntityCache> _potentialTargets = null)
        {
            if (Logging.DebugDisableGetBestTarget)
            {
                return true;
            }

            if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "Attempting to get Best Target", Logging.Teal);

            if (DateTime.UtcNow < Time.Instance.NextGetBestCombatTarget)
            {
                if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "No need to run GetBestTarget again so soon. We only want to run once per tick", Logging.Teal);
                return false;
            }

            Time.Instance.NextGetBestCombatTarget = DateTime.UtcNow.AddMilliseconds(800);

            //if (!QMCache.Instance.Targets.Any()) //&& _potentialTargets == null )
            //{
            //    if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "We have no locked targets and [" + QMCache.Instance.Targeting.Count() + "] targets being locked atm", Logging.Teal);
            //    return false;
            //}

            EntityCache currentTarget = FindCurrentTarget();

            //We need to make sure that our current Preferred is still valid, if not we need to clear it out
            //This happens when we have killed the last thing within our range (or the last thing in the pocket)
            //and there is nothing to replace it with.
            //if (Combat.PreferredPrimaryWeaponTarget != null
            //    && QMCache.Instance.Entities.All(t => t.Id != Instance.PreferredPrimaryWeaponTargetID))
            //{
            //    if (Logging.DebugGetBestTarget) Logging.Log("GetBestTarget", "PreferredPrimaryWeaponTarget is not valid, clearing it", Logging.White);
            //    Combat.PreferredPrimaryWeaponTarget = null;
            //}

            //
            // process the list of PrimaryWeaponPriorityTargets in this order... Eventually the order itself should be user selectable
            // this allow us to kill the most 'important' things doing e-war first instead of just handling them by range
            //

            //
            // if currentTarget set to something (not null) and it is actually an entity...
            //
            if (currentTarget != null && currentTarget.IsReadyToShoot)
            {
                if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons): currentTarget", "We have a target, testing conditions", Logging.Teal);

                #region Is our current target any other primary weapon priority target?
                //
                // Is our current target any other primary weapon priority target? AND if our target is just a PriorityKillTarget assume ALL E-war is more important.
                //
                if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons): currentTarget", "Checking Priority", Logging.Teal);
                if (PrimaryWeaponPriorityEntities.Any(pt => pt.IsReadyToShoot
                                                        && pt.Distance < Combat.MaxRange
                                                        && pt.IsCurrentTarget
                                                        && !currentTarget.IsHigherPriorityPresent))
                {
                    if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "CurrentTarget [" + currentTarget.Name + "][" + Math.Round(currentTarget.Distance / 1000, 2) + "k][" + currentTarget.MaskedId + "] GroupID [" + currentTarget.GroupId + "]", Logging.Debug);
                    PreferredPrimaryWeaponTarget = currentTarget;
                    Time.Instance.LastPreferredPrimaryWeaponTargetDateTime = DateTime.UtcNow;
                    return true;
                }
                #endregion Is our current target any other primary weapon priority target?

                /*
                #region Current Target Health Logging
                //
                // Current Target Health Logging
                //
                bool currentTargetHealthLogNow = true;
                if (QMSettings.Instance.DetailedCurrentTargetHealthLogging)
                {
                    if ((int)currentTarget.Id != (int)TargetingCache.CurrentTargetID)
                    {
                        if ((int)currentTarget.ArmorPct == 0 && (int)currentTarget.ShieldPct == 0 && (int)currentTarget.StructurePct == 0)
                        {
                            //assume that any NPC with no shields, armor or hull is dead or does not yet have valid data associated with it
                        }
                        else
                        {
                            //
                            // assign shields and armor to targetingcache variables - compare them to each other
                            // to see if we need to send another log message to the console, if the values have not changed no need to log it.
                            //
                            if ((int)currentTarget.ShieldPct >= TargetingCache.CurrentTargetShieldPct ||
                                (int)currentTarget.ArmorPct >= TargetingCache.CurrentTargetArmorPct ||
                                (int)currentTarget.StructurePct >= TargetingCache.CurrentTargetStructurePct)
                            {
                                currentTargetHealthLogNow = false;
                            }

                            //
                            // now that we are done comparing - assign new values for this tick
                            //
                            TargetingCache.CurrentTargetShieldPct = (int)currentTarget.ShieldPct;
                            TargetingCache.CurrentTargetArmorPct = (int)currentTarget.ArmorPct;
                            TargetingCache.CurrentTargetStructurePct = (int)currentTarget.StructurePct;
                            if (currentTargetHealthLogNow)
                            {
                                Logging.Log(callingroutine, ".GetBestTarget (Weapons): CurrentTarget is [" + currentTarget.Name +                              //name
                                            "][" + (Math.Round(currentTarget.Distance / 1000, 0)).ToString(CultureInfo.InvariantCulture) +           //distance
                                            "k][Shield%:[" + Math.Round(currentTarget.ShieldPct * 100, 0).ToString(CultureInfo.InvariantCulture) +   //shields
                                            "][Armor%:[" + Math.Round(currentTarget.ArmorPct * 100, 0).ToString(CultureInfo.InvariantCulture) + "]" //armor
                                            , Logging.White);
                            }
                        }
                    }
                }

                #endregion Current Target Health Logging
                */

                #region Is our current target already in armor? keep shooting the same target if so...
                //
                // Is our current target already in armor? keep shooting the same target if so...
                //
                if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons): currentTarget", "Checking Low Health", Logging.Teal);
                if (currentTarget.IsEntityIShouldKeepShooting)
                {
                    if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "CurrentTarget [" + currentTarget.Name + "][" + Math.Round(currentTarget.Distance / 1000, 2) + "k][" + currentTarget.MaskedId + " GroupID [" + currentTarget.GroupId + "]] has less than 60% armor, keep killing this target", Logging.Debug);
                    PreferredPrimaryWeaponTarget = currentTarget;
                    Time.Instance.LastPreferredPrimaryWeaponTargetDateTime = DateTime.UtcNow;
                    return true;
                }

                #endregion Is our current target already in armor? keep shooting the same target if so...

                #region If none of the above matches, does our current target meet the conditions of being hittable and in range
                if (!currentTarget.IsHigherPriorityPresent)
                {
                    if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons): currentTarget", "Does the currentTarget exist? can it be hit?", Logging.Teal);
                    if (currentTarget.IsReadyToShoot
                        && (!currentTarget.IsNPCFrigate || (!Drones.UseDrones && !currentTarget.IsTooCloseTooFastTooSmallToHit))
                        && currentTarget.Distance < Combat.MaxRange)
                    {
                        if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "if  the currentTarget exists and the target is the right size then continue shooting it;", Logging.Debug);
                        if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "currentTarget is [" + currentTarget.Name + "][" + Math.Round(currentTarget.Distance / 1000, 2) + "k][" + currentTarget.MaskedId + "] GroupID [" + currentTarget.GroupId + "]", Logging.Debug);

                        PreferredPrimaryWeaponTarget = currentTarget;
                        Time.Instance.LastPreferredPrimaryWeaponTargetDateTime = DateTime.UtcNow;
                        return true;
                    }
                }
                #endregion
            }

            if (CheckForPrimaryWeaponPriorityTargetsInOrder(currentTarget, distance)) return true;

            #region Get the closest primary weapon priority target
            //
            // Get the closest primary weapon priority target
            //
            if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "Checking Closest PrimaryWeaponPriorityTarget", Logging.Teal);
            EntityCache primaryWeaponPriorityTarget = null;
            try
            {
                if (Combat.PrimaryWeaponPriorityEntities != null && Combat.PrimaryWeaponPriorityEntities.Any())
                {
                    primaryWeaponPriorityTarget = Combat.PrimaryWeaponPriorityEntities.Where(p => p.Distance < Combat.MaxRange
                                                                                && !p.IsIgnored
                                                                                && p.IsReadyToShoot
                                                                                && ((!p.IsNPCFrigate && !p.IsFrigate) || (!Drones.UseDrones && !p.IsTooCloseTooFastTooSmallToHit)))
                                                                               .OrderByDescending(pt => pt.IsTargetedBy)
                                                                               .ThenByDescending(pt => pt.IsCurrentTarget)
                                                                               .ThenByDescending(pt => pt.IsInOptimalRange)
                                                                               .ThenByDescending(pt => pt.IsEwarTarget)
                                                                               .ThenBy(pt => pt.PrimaryWeaponPriorityLevel)
                                                                               .ThenByDescending(pt => pt.TargetValue)
                                                                               .ThenBy(pt => pt.Nearest5kDistance)
                                                                               .FirstOrDefault();
                }
            }
            catch (NullReferenceException) { }  // Not sure why this happens, but seems to be no problem

            if (primaryWeaponPriorityTarget != null)
            {
                if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "primaryWeaponPriorityTarget is [" + primaryWeaponPriorityTarget.Name + "][" + Math.Round(primaryWeaponPriorityTarget.Distance / 1000, 2) + "k][" + primaryWeaponPriorityTarget.MaskedId + "] GroupID [" + primaryWeaponPriorityTarget.GroupId + "]", Logging.Debug);
                PreferredPrimaryWeaponTarget = primaryWeaponPriorityTarget;
                Time.Instance.LastPreferredPrimaryWeaponTargetDateTime = DateTime.UtcNow;
                return true;
            }

            #endregion Get the closest primary weapon priority target

            #region did our calling routine (CombatMissionCtrl?) pass us targets to shoot?
            //
            // This is where CombatMissionCtrl would pass targets to GetBestTarget
            //
            if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "Checking Calling Target", Logging.Teal);
            if (_potentialTargets != null && _potentialTargets.Any())
            {
                EntityCache callingTarget = null;
                try
                {
                    callingTarget = _potentialTargets.OrderBy(t => t.Distance).FirstOrDefault();
                }
                catch (NullReferenceException) { }

                if (callingTarget != null && (callingTarget.IsReadyToShoot || callingTarget.IsLargeCollidable)
                    //((!callingTarget.IsNPCFrigate && !callingTarget.IsFrigate)
                    //|| (!QMCache.Instance.UseDrones && !callingTarget.IsTooCloseTooFastTooSmallToHit))
                   )
                {
                    if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "if (callingTarget != null && !callingTarget.IsIgnored)", Logging.Debug);
                    if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "callingTarget is [" + callingTarget.Name + "][" + Math.Round(callingTarget.Distance / 1000, 2) + "k][" + callingTarget.MaskedId + "] GroupID [" + callingTarget.GroupId + "]", Logging.Debug);
                    AddPrimaryWeaponPriorityTarget(callingTarget, PrimaryWeaponPriority.PriorityKillTarget, "GetBestTarget: callingTarget");
                    PreferredPrimaryWeaponTarget = callingTarget;
                    Time.Instance.LastPreferredPrimaryWeaponTargetDateTime = DateTime.UtcNow;
                    return true;
                }

                //return false; //do not return here, continue to process targets, we did not find one yet
            }
            #endregion

            #region Get the closest High Value Target

            if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "Checking Closest High Value", Logging.Teal);
            EntityCache highValueTarget = null;

            if (PotentialCombatTargets.Any())
            {
                if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "get closest: if (potentialCombatTargets.Any())", Logging.Teal);

                highValueTarget = PotentialCombatTargets.Where(t => t.IsHighValueTarget && t.IsReadyToShoot)
                    .OrderByDescending(t => !t.IsNPCFrigate)
                    .ThenByDescending(t => t.IsTargetedBy)
                    .ThenByDescending(t => !t.IsTooCloseTooFastTooSmallToHit)
                    .ThenByDescending(t => t.IsInOptimalRange)
                    .ThenByDescending(pt => pt.TargetValue) //highest value first
                    .ThenByDescending(t => !t.IsCruiser)
                    .ThenBy(QMCache.Instance.OrderByLowestHealth())
                    .ThenBy(t => t.Nearest5kDistance)
                    .FirstOrDefault();
            }
            #endregion

            #region Get the closest low value target that is not moving too fast for us to hit
            //
            // Get the closest low value target //excluding things going too fast for guns to hit (if you have guns fitted)
            //
            if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "Checking closest Low Value", Logging.Teal);
            EntityCache lowValueTarget = null;
            if (PotentialCombatTargets.Any())
            {
                lowValueTarget = PotentialCombatTargets.Where(t => t.IsLowValueTarget && t.IsReadyToShoot)
                    .OrderByDescending(t => t.IsNPCFrigate)
                    .ThenByDescending(t => t.IsTargetedBy)
                    .ThenByDescending(t => t.IsTooCloseTooFastTooSmallToHit) //this will return false (not to close to fast to small), then true due to .net sort order of bools
                    .ThenBy(pt => pt.TargetValue) //lowest value first
                    .ThenBy(QMCache.Instance.OrderByLowestHealth())
                    .ThenBy(t => t.Nearest5kDistance)
                    .FirstOrDefault();
            }
            #endregion

            #region If lowValueFirst && lowValue aggrod or no high value aggrod
            if ((lowValueFirst && lowValueTarget != null)
                    && (lowValueTarget.IsTargetedBy
                    || (highValueTarget == null
                        || (highValueTarget != null
                        && !highValueTarget.IsTargetedBy))))
            {
                if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "Checking Low Value First", Logging.Teal);
                if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "lowValueTarget is [" + lowValueTarget.Name + "][" + Math.Round(lowValueTarget.Distance / 1000, 2) + "k][" + lowValueTarget.MaskedId + "] GroupID [" + lowValueTarget.GroupId + "]", Logging.Debug);
                Combat.PreferredPrimaryWeaponTarget = lowValueTarget;
                Time.Instance.LastPreferredPrimaryWeaponTargetDateTime = DateTime.UtcNow;
                return true;
            }
            #endregion

            #region High Value - aggrod, or no low value aggrod
            // high value if aggrod
            // if no high value aggrod, low value thats aggrod
            // if no high aggro, and no low aggro, shoot high value thats present
            if (highValueTarget != null)
            {
                if (highValueTarget.IsTargetedBy
                    || Drones.UseDrones
                    || (lowValueTarget == null
                        || (lowValueTarget != null
                        && !lowValueTarget.IsTargetedBy)))
                {
                    if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "Checking Use High Value", Logging.Teal);
                    if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "highValueTarget is [" + highValueTarget.Name + "][" + Math.Round(highValueTarget.Distance / 1000, 2) + "k][" + highValueTarget.MaskedId + "] GroupID [" + highValueTarget.GroupId + "]", Logging.Debug);
                    Combat.PreferredPrimaryWeaponTarget = highValueTarget;
                    Time.Instance.LastPreferredPrimaryWeaponTargetDateTime = DateTime.UtcNow;
                    return true;
                }
            }
            #endregion

            #region If we do not have a high value target but we do have a low value target
            if (lowValueTarget != null)
            {
                if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "Checking use Low Value", Logging.Teal);
                if (Logging.DebugGetBestTarget) Logging.Log(callingroutine + " Debug: GetBestTarget (Weapons):", "lowValueTarget is [" + lowValueTarget.Name + "][" + Math.Round(lowValueTarget.Distance / 1000, 2) + "k][" + lowValueTarget.MaskedId + "] GroupID [" + lowValueTarget.GroupId + "]", Logging.Debug);
                Combat.PreferredPrimaryWeaponTarget = lowValueTarget;
                Time.Instance.LastPreferredPrimaryWeaponTargetDateTime = DateTime.UtcNow;
                return true;
            }
            #endregion

            if (Logging.DebugGetBestTarget) Logging.Log("GetBestTarget: none", "Could not determine a suitable target", Logging.Debug);
            #region If we did not find anything at all (wtf!?!?)
            if (Logging.DebugGetBestTarget)
            {
                if (QMCache.Instance.Targets.Any())
                {
                    Logging.Log("GetBestTarget (Weapons): none", ".", Logging.Debug);
                    Logging.Log("GetBestTarget (Weapons): none", "*** ALL LOCKED/LOCKING TARGETS LISTED BELOW", Logging.Debug);
                    int LockedTargetNumber = 0;
                    foreach (EntityCache __target in QMCache.Instance.Targets)
                    {
                        LockedTargetNumber++;
                        Logging.Log("GetBestTarget (Weapons): none", "*** Target: [" + LockedTargetNumber + "][" + __target.Name + "][" + Math.Round(__target.Distance / 1000, 2) + "k][" + __target.MaskedId + "][isTarget: " + __target.IsTarget + "][isTargeting: " + __target.IsTargeting + "] GroupID [" + __target.GroupId + "]", Logging.Debug);
                    }
                    Logging.Log("GetBestTarget (Weapons): none", "*** ALL LOCKED/LOCKING TARGETS LISTED ABOVE", Logging.Debug);
                    Logging.Log("GetBestTarget (Weapons): none", ".", Logging.Debug);
                }

                if (Combat.PotentialCombatTargets.Any(t => !t.IsTarget && !t.IsTargeting))
                {
                    if (CombatMissionCtrl.IgnoreTargets.Any())
                    {
                        int IgnoreCount = CombatMissionCtrl.IgnoreTargets.Count;
                        Logging.Log("GetBestTarget (Weapons): none", "Ignore List has [" + IgnoreCount + "] Entities in it.", Logging.Debug);
                    }

                    Logging.Log("GetBestTarget (Weapons): none", "***** ALL [" + Combat.PotentialCombatTargets.Count() + "] potentialCombatTargets LISTED BELOW (not yet targeted or targeting)", Logging.Debug);
                    int potentialCombatTargetNumber = 0;
                    foreach (EntityCache potentialCombatTarget in Combat.PotentialCombatTargets)
                    {
                        potentialCombatTargetNumber++;
                        Logging.Log("GetBestTarget (Weapons): none", "***** Unlocked [" + potentialCombatTargetNumber + "]: [" + potentialCombatTarget.Name + "][" + Math.Round(potentialCombatTarget.Distance / 1000, 2) + "k][" + potentialCombatTarget.MaskedId + "][isTarget: " + potentialCombatTarget.IsTarget + "] GroupID [" + potentialCombatTarget.GroupId + "]", Logging.Debug);
                    }
                    Logging.Log("GetBestTarget (Weapons): none", "***** ALL [" + Combat.PotentialCombatTargets.Count() + "] potentialCombatTargets LISTED ABOVE (not yet targeted or targeting)", Logging.Debug);
                    Logging.Log("GetBestTarget (Weapons): none", ".", Logging.Debug);
                }
            }
            #endregion

            Time.Instance.NextGetBestCombatTarget = DateTime.UtcNow;
            return false;
        }


        // Reload correct (tm) ammo for the NPC
        // (enough/correct) ammo is loaded, false if wrong/not enough ammo is loaded
        private static bool ReloadNormalAmmo(ModuleCache weapon, EntityCache entity, int weaponNumber, bool force = false)
        {
            if (QMCache.Instance.Weapons.Any(i => i.TypeId == (int)TypeID.CivilianGatlingAutocannon
                                                 || i.TypeId == (int)TypeID.CivilianGatlingPulseLaser
                                                 || i.TypeId == (int)TypeID.CivilianGatlingRailgun
                                                 || i.TypeId == (int)TypeID.CivilianLightElectronBlaster))
            {
                //Logging.Log("ReloadAll", "Civilian guns do not use ammo.", Logging.Debug);
                return true;
            }

            if (entity == null)
            {
                entity = QMCache.Instance.MyShipEntity;
            }

            List<Ammo> correctAmmoToUse = null;
            List<Ammo> correctAmmoInCargo = null;

            //
            // NOTE: This new setting is NOT ready for use!
            // NOTE: when we are finished molesting ReloadNormalAmmo we should do the same to the routine used for lasers...
            //
            // ammo selection based on target size
            //
            if (Combat.SelectAmmoToUseBasedOnShipSize)
            {
                if (Time.Instance.LastChangedAmmoTimeStamp != null && Time.Instance.LastChangedAmmoTimeStamp.ContainsKey(weapon.ItemId))
                {
                    //
                    // Do not allow the changing of ammo if we already changed ammo in the last 60 seconds AND we do not need to reload yet
                    //
                    if (DateTime.UtcNow < Time.Instance.LastChangedAmmoTimeStamp[weapon.ItemId].AddSeconds(Time.Instance.EnforcedDelayBetweenArbitraryAmmoChanges))
                    {
                        if ((long)weapon.CurrentCharges != 0 && !force)
                        {
                            if (Logging.DebugReloadAll) Logging.Log("ReloadNormalAmmmo", "[" + weapon.TypeName + "] last reloaded [" + DateTime.UtcNow.Subtract(Time.Instance.LastChangedAmmoTimeStamp[weapon.ItemId]).TotalSeconds + "sec ago] [ " + weapon.CurrentCharges + " ] charges in [" + QMCache.Instance.Weapons.Count() + "] total weapons, minimum of [" + Combat.MinimumAmmoCharges + "] charges, MaxCharges is [" + weapon.MaxCharges + "]", Logging.Orange);
                            return true;
                        }
                    }
                }

                if (Combat.Ammo.Any(a => a.DamageType == MissionSettings.CurrentDamageType))
                {
                    // Get ammo based on damage type
                    correctAmmoToUse = Combat.Ammo.Where(a => a.DamageType == MissionSettings.CurrentDamageType).ToList();

                    //
                    // get Ammo Based on entity we are shooting's size class, default to normal ammo if we cant determine its size
                    //
                    if (entity.IsBattleship)
                    {
                        // this needs one more layer somewhere to determine the right damage type for battleships, etc (EM, kinetic, etc) and it needs to do it for
                        // default, faction and mission specific layers
                        //
                        //correctAmmoToUse = QMSettings.Instance.Ammo.Where(a => a.DamageType == DamageType.BattleShip_EM).ToList();
                    }
                    else if (entity.IsBattlecruiser)
                    {
                        //correctAmmoToUse = QMSettings.Instance.Ammo.Where(a => a.DamageType == DamageType.BattleShip_EM).ToList();
                    }
                    else if (entity.IsCruiser)
                    {
                        //correctAmmoToUse = QMSettings.Instance.Ammo.Where(a => a.DamageType == DamageType.BattleShip_EM).ToList();
                    }
                    else if (entity.IsFrigate)
                    {
                        //correctAmmoToUse = QMSettings.Instance.Ammo.Where(a => a.DamageType == DamageType.Frigate_EM).ToList();
                    }
                    else if (entity.IsLargeCollidable)
                    {
                        //correctAmmoToUse = QMSettings.Instance.Ammo.Where(a => a.DamageType == DamageType.LargeCollidable_EM).ToList();
                    }
                    else if (entity.IsPlayer)
                    {
                        //correctAmmoToUse = QMSettings.Instance.Ammo.Where(a => a.DamageType == DamageType.PVP_EM).ToList();
                    }

                    // Check if we still have that ammo in our cargo
                    correctAmmoInCargo = correctAmmoToUse.Where(a => QMCache.Instance.CurrentShipsCargo != null && QMCache.Instance.CurrentShipsCargo.Items.Any(i => i.TypeId == a.TypeId && i.Quantity >= Combat.MinimumAmmoCharges)).ToList();

                    //check if mission specific ammo is defined
                    if (MissionSettings.AmmoTypesToLoad.Count() != 0 && QMCache.Instance.CurrentShipsCargo != null)
                    {
                        //correctAmmoInCargo = QMCache.Instance.CurrentShipsCargo.Items.Where(i => i.TypeName == MissionSettings.AmmoTypesToLoad.FirstOrDefault().ToString());
                        //correctAmmoInCargo = MissionSettings.AmmoTypesToLoad.Where(MissionSettings.CurrentDamageType).ToList();
                    }

                    // Check if we still have that ammo in our cargo
                    correctAmmoInCargo = correctAmmoInCargo.Where(a => QMCache.Instance.CurrentShipsCargo != null && QMCache.Instance.CurrentShipsCargo.Items.Any(i => i.TypeId == a.TypeId && i.Quantity >= Combat.MinimumAmmoCharges)).ToList();
                    if (MissionSettings.AmmoTypesToLoad.Count() != 0)
                    {
                        //correctAmmoInCargo = MissionSettings.AmmoTypesToLoad;
                    }

                    // We are out of ammo! :(
                    if (!correctAmmoInCargo.Any())
                    {
                        Logging.Log("Combat", "ReloadNormalAmmo: not enough [" + MissionSettings.CurrentDamageType + "] ammo in cargohold: MinimumCharges: [" + Combat.MinimumAmmoCharges + "]", Logging.Orange);
                        _States.CurrentCombatState = CombatState.OutOfAmmo;
                        return false;
                    }


                }
                else
                {
                    Logging.Log("Combat", "ReloadNormalAmmo: ammo is not defined properly in the ammo section of this characters settings xml", Logging.Orange);
                    _States.CurrentCombatState = CombatState.OutOfAmmo;
                    return false;
                }

            }
            else
            {
                //
                // normal ammo selection - ignore target attributes and uses the right ammo for the pocket
                //
                if (Combat.Ammo.Any(a => a.DamageType == MissionSettings.CurrentDamageType))
                {
                    // Get ammo based on damage type
                    correctAmmoToUse = Combat.Ammo.Where(a => a.DamageType == MissionSettings.CurrentDamageType).ToList();

                    // Check if we still have that ammo in our cargo
                    correctAmmoInCargo = correctAmmoToUse.Where(a => QMCache.Instance.CurrentShipsCargo != null && QMCache.Instance.CurrentShipsCargo.Items.Any(i => i.TypeId == a.TypeId && i.Quantity >= Combat.MinimumAmmoCharges)).ToList();

                    //check if mission specific ammo is defined
                    if (MissionSettings.AmmoTypesToLoad.Count() != 0)
                    {
                        //correctAmmoInCargo = MissionSettings.AmmoTypesToLoad.Where(a => a.DamageType == MissionSettings.CurrentDamageType).ToList();
                    }

                    // Check if we still have that ammo in our cargo
                    correctAmmoInCargo = correctAmmoInCargo.Where(a => QMCache.Instance.CurrentShipsCargo != null && QMCache.Instance.CurrentShipsCargo.Items.Any(i => i.TypeId == a.TypeId && i.Quantity >= Combat.MinimumAmmoCharges)).ToList();
                    if (MissionSettings.AmmoTypesToLoad.Count() != 0)
                    {
                       //correctAmmoInCargo = MissionSettings.AmmoTypesToLoad;
                    }

                    // We are out of ammo! :(
                    if (!correctAmmoInCargo.Any())
                    {
                        Logging.Log("Combat", "ReloadNormalAmmo:: not enough [" + MissionSettings.CurrentDamageType + "] ammo in cargohold: MinimumCharges: [" + Combat.MinimumAmmoCharges + "] Note: CurrentDamageType [" + MissionSettings.CurrentDamageType + "]", Logging.Orange);
                        if (MissionSettings.FactionDamageType != null) Logging.Log("Combat", "FactionDamageType [" + MissionSettings.FactionDamageType + "] PocketDamageType overrides MissionDamageType overrides FactionDamageType", Logging.Orange);
                        if (MissionSettings.MissionDamageType != null) Logging.Log("Combat", "MissionDamageType [" + MissionSettings.MissionDamageType + "] PocketDamageType overrides MissionDamageType overrides FactionDamageType", Logging.Orange);
                        if (MissionSettings.PocketDamageType != null) Logging.Log("Combat", "PocketDamageType [" + MissionSettings.PocketDamageType + "] PocketDamageType overrides MissionDamageType overrides FactionDamageType", Logging.Orange);
                        _States.CurrentCombatState = CombatState.OutOfAmmo;
                        return false;
                    }
                }
                else
                {
                    Logging.Log("Combat", "ReloadNormalAmmo: ammo is not defined properly in the ammo section of this characters settings xml", Logging.Orange);
                    _States.CurrentCombatState = CombatState.OutOfAmmo;
                    return false;
                }
            }


            /******
            if (weapon.Charge != null)
            {
                IEnumerable<Ammo> areWeMissingAmmo = correctAmmo.Where(a => a.TypeId == weapon.Charge.TypeId);
                if (!areWeMissingAmmo.Any())
                {
                    if (DateTime.UtcNow.Subtract(Time.Instance.LastLoggingAction).TotalSeconds > 4)
                    {
                        Logging.Log("Combat", "ReloadNormalAmmo: We have ammo loaded that does not have a full reload available, checking cargo for other ammo", Logging.Orange);
                        Time.Instance.LastLoggingAction = DateTime.UtcNow;
                        try
                        {
                            if (QMSettings.Instance.Ammo.Any())
                            {
                                DirectItem availableAmmo = cargo.Items.OrderByDescending(i => i.Quantity).Where(a => QMSettings.Instance.Ammo.Any(i => i.TypeId == a.TypeId)).ToList().FirstOrDefault();
                                if (availableAmmo != null)
                                {
                                    QMCache.Instance.DamageType = QMSettings.Instance.Ammo.ToList().OrderByDescending(i => i.Quantity).Where(a => a.TypeId == availableAmmo.TypeId).ToList().FirstOrDefault().DamageType;
                                    Logging.Log("Combat", "ReloadNormalAmmo: found [" + availableAmmo.Quantity + "] units of  [" + availableAmmo.TypeName + "] changed DamageType to [" + QMCache.Instance.DamageType.ToString() + "]", Logging.Orange);
                                    return false;
                                }

                                Logging.Log("Combat", "ReloadNormalAmmo: unable to find any alternate ammo in your cargo", Logging.teal);
                                _States.CurrentCombatState = CombatState.OutOfAmmo;
                                return false;
                            }
                        }
                        catch (Exception)
                        {
                            Logging.Log("Combat", "ReloadNormalAmmo: unable to find any alternate ammo in your cargo", Logging.teal);
                            _States.CurrentCombatState = CombatState.OutOfAmmo;
                        }
                        return false;
                    }
                }
            }
            *****/

            // Get the best possible ammo
            Ammo ammo = correctAmmoInCargo.FirstOrDefault();
            try
            {
                if (ammo != null && entity != null)
                {
                    ammo = correctAmmoInCargo.Where(a => a.Range > entity.Distance).OrderBy(a => a.Range).FirstOrDefault();
                }
            }
            catch (Exception exception)
            {
                Logging.Log("Combat", "ReloadNormalAmmo: [" + weaponNumber + "] Unable to find the correct ammo: waiting [" + exception + "]", Logging.Teal);
                return false;
            }

            // We do not have any ammo left that can hit targets at that range!
            if (ammo == null)
            {
                if (Logging.DebugReloadAll) Logging.Log("ReloadNormalAmmmo", "[" + weaponNumber + "] We do not have any ammo left that can hit targets at that range!", Logging.Orange);
                return false;
            }

            // Do we have ANY ammo loaded? CurrentCharges would be 0 if we have no ammo at all.
            if ((long)weapon.CurrentCharges != 0 && weapon.Charge.TypeId == ammo.TypeId)
            {
                //Force a reload even through we have some ammo loaded already?
                if (!force)
                {
                    if (Logging.DebugReloadAll) Logging.Log("ReloadNormalAmmmo", "[" + weaponNumber + "] MaxRange [ " + weapon.MaxRange + " ] if we have 0 charges MaxRange will be 0", Logging.Orange);
                    Time.Instance.LastReloadAttemptTimeStamp[weapon.ItemId] = DateTime.UtcNow;
                    return true;
                }

                //we must have ammo, no need to reload at the moment\
                if (Logging.DebugReloadAll) Logging.Log("ReloadNormalAmmmo", "[" + weaponNumber + "] MaxRange [ " + weapon.MaxRange + " ] if we have 0 charges MaxRange will be 0", Logging.Orange);
                Time.Instance.LastReloadAttemptTimeStamp[weapon.ItemId] = DateTime.UtcNow;
                return true;
            }

            DirectItem charge = null;
            if (QMCache.Instance.CurrentShipsCargo != null)
            {
                if (QMCache.Instance.CurrentShipsCargo.Items.Any())
                {
                    charge = QMCache.Instance.CurrentShipsCargo.Items.FirstOrDefault(e => e.TypeId == ammo.TypeId && e.Quantity >= Combat.MinimumAmmoCharges);
                    // This should have shown up as "out of ammo"
                    if (charge == null)
                    {
                        if (Logging.DebugReloadAll) Logging.Log("ReloadNormalAmmmo", "We have no ammo in cargo?! This should have shown up as out of ammo", Logging.Orange);
                        return false;
                    }
                }
                else
                {
                    if (Logging.DebugReloadAll) Logging.Log("ReloadNormalAmmmo", "We have no items in cargo at all?! This should have shown up as out of ammo", Logging.Orange);
                    return false;
                }
            }
            else
            {
                if (Logging.DebugReloadAll) Logging.Log("ReloadNormalAmmmo", "CurrentShipsCargo is null?!", Logging.Orange);
                return false;
            }

            // If we are reloading, wait Time.ReloadWeaponDelayBeforeUsable_seconds (see time.cs)
            if (weapon.IsReloadingAmmo)
            {
                if (Logging.DebugReloadAll) Logging.Log("ReloadNormalAmmmo", "We are already reloading, wait - weapon.IsReloadingAmmo [" + weapon.IsReloadingAmmo + "]", Logging.Orange);
                return true;
            }

            // If we are changing ammo, wait Time.ReloadWeaponDelayBeforeUsable_seconds (see time.cs)
            if (weapon.IsChangingAmmo)
            {
                if (Logging.DebugReloadAll) Logging.Log("ReloadNormalAmmmo", "We are already changing ammo, wait - weapon.IsReloadingAmmo [" + weapon.IsReloadingAmmo + "]", Logging.Orange);
                return true;
            }

            //if (weapon.AutoReload && QMSettings.Instance.disableAutoreload)
            //{
            //    if (Logging.DebugReloadAll) Logging.Log("debug ReloadAll:", "weapon.AutoReload [" + weapon.AutoReload + "] setting it to false", Logging.Orange);
            //    weapon.SetAutoReload(false);
            //    return false;
            //}

            try
            {
                // Reload or change ammo
                if (weapon.Charge != null && weapon.Charge.TypeId == charge.TypeId && !weapon.IsChangingAmmo)
                {
                    if (weapon.ReloadAmmo(charge, weaponNumber, (double) ammo.Range))
                    {
                        return true;
                    }

                    Logging.Log("Combat.ReloadNormalAmmo", "ReloadAmmo failed.", Logging.Debug);
                    return false;
                }

                if (entity != null && weapon.ChangeAmmo(charge, weaponNumber, (double) ammo.Range, entity.Name, entity.Distance))
                {
                    return true;
                }

                Logging.Log("Combat.ReloadNormalAmmo", "ChangeAmmo failed.", Logging.Debug);
                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("Combat.ReloadNormalAmmo", "Exception [" + exception + "]", Logging.Debug);
            }

            // Return true as we are reloading ammo, assume it is the correct ammo...
            return true;
        }

        private static bool ReloadEnergyWeaponAmmo(ModuleCache weapon, EntityCache entity, int weaponNumber)
        {
            if (QMCache.Instance.Weapons.Any(i => i.TypeId == (int)TypeID.CivilianGatlingAutocannon
                                                 || i.TypeId == (int)TypeID.CivilianGatlingPulseLaser
                                                 || i.TypeId == (int)TypeID.CivilianGatlingRailgun
                                                 || i.TypeId == (int)TypeID.CivilianLightElectronBlaster))
            {
                //Logging.Log("ReloadAll", "Civilian guns do not use ammo.", Logging.Debug);
                return true;
            }

            // Get ammo based on damage type
            IEnumerable<Ammo> correctAmmo = Combat.Ammo.Where(a => a.DamageType == MissionSettings.CurrentDamageType).ToList();

            // Check if we still have that ammo in our cargo
            IEnumerable<Ammo> correctAmmoInCargo = correctAmmo.Where(a => QMCache.Instance.CurrentShipsCargo != null && QMCache.Instance.CurrentShipsCargo.Items.Any(e => e.TypeId == a.TypeId)).ToList();

            //check if mission specific ammo is defined
            if (MissionSettings.AmmoTypesToLoad.Count() != 0)
            {
                //correctAmmoInCargo = MissionSettings.AmmoTypesToLoad.Where(a => a.DamageType == MissionSettings.CurrentDamageType).ToList();
            }

            // Check if we still have that ammo in our cargo
            correctAmmoInCargo = correctAmmoInCargo.Where(a => QMCache.Instance.CurrentShipsCargo != null && QMCache.Instance.CurrentShipsCargo.Items.Any(e => e.TypeId == a.TypeId && e.Quantity >= Combat.MinimumAmmoCharges)).ToList();
            if (MissionSettings.AmmoTypesToLoad.Count() != 0)
            {
                //correctAmmoInCargo = MissionSettings.AmmoTypesToLoad;
            }

            // We are out of ammo! :(
            if (!correctAmmoInCargo.Any())
            {
                Logging.Log("Combat", "ReloadEnergyWeapon: not enough [" + MissionSettings.CurrentDamageType + "] ammo in cargohold: MinimumCharges: [" + Combat.MinimumAmmoCharges + "]", Logging.Orange);
                _States.CurrentCombatState = CombatState.OutOfAmmo;
                return false;
            }

            if (weapon.Charge != null)
            {
                IEnumerable<Ammo> areWeMissingAmmo = correctAmmoInCargo.Where(a => a.TypeId == weapon.Charge.TypeId);
                if (!areWeMissingAmmo.Any())
                {
                    Logging.Log("Combat", "ReloadEnergyWeaponAmmo: We have ammo loaded that does not have a full reload available in the cargo.", Logging.Orange);
                }
            }

            // Get the best possible ammo - energy weapons change ammo near instantly
            Ammo ammo = correctAmmoInCargo.Where(a => a.Range > (entity.Distance)).OrderBy(a => a.Range).FirstOrDefault(); //default

            // We do not have any ammo left that can hit targets at that range!
            if (ammo == null)
            {
                if (Logging.DebugReloadorChangeAmmo) Logging.Log("Combat", "ReloadEnergyWeaponAmmo: best possible ammo: [ ammo == null]", Logging.White);
                return false;
            }

            if (Logging.DebugReloadorChangeAmmo) Logging.Log("Combat", "ReloadEnergyWeaponAmmo: best possible ammo: [" + ammo.TypeId + "][" + ammo.DamageType + "]", Logging.White);
            if (Logging.DebugReloadorChangeAmmo) Logging.Log("Combat", "ReloadEnergyWeaponAmmo: best possible ammo: [" + entity.Name + "][" + Math.Round(entity.Distance / 1000, 0) + "]", Logging.White);

            DirectItem charge = QMCache.Instance.CurrentShipsCargo.Items.OrderBy(e => e.Quantity).FirstOrDefault(e => e.TypeId == ammo.TypeId);

            // We do not have any ammo left that can hit targets at that range!
            if (charge == null)
            {
                if (Logging.DebugReloadorChangeAmmo) Logging.Log("Combat", "ReloadEnergyWeaponAmmo: We do not have any ammo left that can hit targets at that range!", Logging.Orange);
                return false;
            }

            if (Logging.DebugReloadorChangeAmmo) Logging.Log("Combat", "ReloadEnergyWeaponAmmo: charge: [" + charge.TypeName + "][" + charge.TypeId + "]", Logging.White);

            // We have enough ammo loaded
            if (weapon.Charge != null && weapon.Charge.TypeId == ammo.TypeId)
            {
                if (Logging.DebugReloadorChangeAmmo) Logging.Log("Combat", "ReloadEnergyWeaponAmmo: We have Enough Ammo of that type Loaded Already", Logging.White);
                return true;
            }

            // We are reloading, wait
            if (weapon.IsReloadingAmmo)
                return true;

            // We are reloading, wait
            if (weapon.IsChangingAmmo)
                return true;

            // Reload or change ammo
            if (weapon.Charge != null && weapon.Charge.TypeId == charge.TypeId)
            {
                //
                // reload
                //
                if (weapon.ReloadAmmo(charge, weaponNumber, (double) ammo.Range))
                {
                    return true;
                }

                return false;
            }

            //
            // change ammo
            //
            if (weapon.ChangeAmmo(charge, weaponNumber, (double) ammo.Range, entity.Name, entity.Distance))
            {
                return true;
            }

            return false;



        }

        // Reload correct (tm) ammo for the NPC

        private static bool ReloadAmmo(ModuleCache weapon, EntityCache entity, int weaponNumber, bool force = false)
        {
            // We need the cargo bay open for both reload actions
            //if (!QMCache.Instance.OpenCargoHold("Combat: ReloadAmmo")) return false;
            if (QMCache.Instance.Weapons.Any(i => i.TypeId == (int)TypeID.CivilianGatlingAutocannon
                                             || i.TypeId == (int)TypeID.CivilianGatlingPulseLaser
                                             || i.TypeId == (int)TypeID.CivilianGatlingRailgun
                                             || i.TypeId == (int)TypeID.CivilianLightElectronBlaster))
            {
                Logging.Log("ReloadAll", "Civilian guns do not use ammo.", Logging.Debug);
                return true;
            }

            return weapon.IsEnergyWeapon ? ReloadEnergyWeaponAmmo(weapon, entity, weaponNumber) : ReloadNormalAmmo(weapon, entity, weaponNumber, force);
        }

        public static bool ReloadAll(EntityCache entity, bool force = false)
        {
            const int reloadAllDelay = 400;
            if (DateTime.UtcNow.Subtract(_lastReloadAll).TotalMilliseconds < reloadAllDelay)
            {
                return false;
            }

            _lastReloadAll = DateTime.UtcNow;

            if (QMCache.Instance.MyShipEntity.Name == QMSettings.Instance.TransportShipName)
            {
                if (Logging.DebugReloadAll) Logging.Log("ReloadAll", "You are in your TransportShip named [" + QMSettings.Instance.TransportShipName + "], no need to reload ammo!", Logging.Debug);
                return true;
            }

            if (QMCache.Instance.MyShipEntity.Name == QMSettings.Instance.TravelShipName)
            {
                if (Logging.DebugReloadAll) Logging.Log("ReloadAll", "You are in your TravelShipName named [" + QMSettings.Instance.TravelShipName + "], no need to reload ammo!", Logging.Debug);
                return true;
            }

            if (QMCache.Instance.MyShipEntity.GroupId == (int)Group.Shuttle)
            {
                if (Logging.DebugReloadAll) Logging.Log("ReloadAll", "You are in a Shuttle, no need to reload ammo!", Logging.Debug);
                return true;
            }

            if (QMCache.Instance.Weapons.Any(i => i.TypeId == (int)TypeID.CivilianGatlingAutocannon
                                                || i.TypeId == (int)TypeID.CivilianGatlingPulseLaser
                                                || i.TypeId == (int)TypeID.CivilianGatlingRailgun
                                                || i.TypeId == (int)TypeID.CivilianLightElectronBlaster))
            {
                if (Logging.DebugReloadAll) Logging.Log("ReloadAll", "Civilian guns do not use ammo.", Logging.Debug);
                return true;
            }

            _weaponNumber = 0;
            if (Logging.DebugReloadAll) Logging.Log("debug ReloadAll:", "Weapons (or stacks of weapons?): [" + QMCache.Instance.Weapons.Count() + "]", Logging.Orange);

            if (QMCache.Instance.Weapons.Any())
            {
                foreach (ModuleCache weapon in QMCache.Instance.Weapons)
                {
                    _weaponNumber++;
                    if (Time.Instance.LastReloadedTimeStamp != null && Time.Instance.LastReloadedTimeStamp.ContainsKey(weapon.ItemId))
                    {
                        if (DateTime.UtcNow < Time.Instance.LastReloadedTimeStamp[weapon.ItemId].AddSeconds(Time.Instance.ReloadWeaponDelayBeforeUsable_seconds))
                        {
                            if (Logging.DebugReloadAll) Logging.Log("debug ReloadAll", "Weapon [" + _weaponNumber + "] was just reloaded [" + Math.Round(DateTime.UtcNow.Subtract(Time.Instance.LastReloadedTimeStamp[weapon.ItemId]).TotalSeconds, 0) + "] seconds ago , moving on to next weapon", Logging.White);
                            continue;
                        }
                    }

                    if (Time.Instance.LastReloadAttemptTimeStamp != null && Time.Instance.LastReloadAttemptTimeStamp.ContainsKey(weapon.ItemId))
                    {
                        if (DateTime.UtcNow < Time.Instance.LastReloadAttemptTimeStamp[weapon.ItemId].AddSeconds(QMCache.Instance.RandomNumber(5, 10)))
                        {
                            if (Logging.DebugReloadAll) Logging.Log("debug ReloadAll", "Weapon [" + _weaponNumber + "] was just attempted to be reloaded [" + Math.Round(DateTime.UtcNow.Subtract(Time.Instance.LastReloadAttemptTimeStamp[weapon.ItemId]).TotalSeconds, 0) + "] seconds ago , moving on to next weapon", Logging.White);
                            continue;
                        }
                    }

                    if (weapon.CurrentCharges == weapon.MaxCharges)
                    {
                        if (Logging.DebugReloadAll) Logging.Log("debug ReloadAll", "Weapon [" + _weaponNumber + "] has [" + weapon.CurrentCharges + "] charges. MaxCharges is [" + weapon.MaxCharges + "]: checking next weapon", Logging.White);
                        continue;
                    }

                    // Reloading energy weapons prematurely just results in unnecessary error messages, so let's not do that
                    if (weapon.IsEnergyWeapon)
                    {
                        if (Logging.DebugReloadAll) Logging.Log("debug ReloadAll:", "if (weapon.IsEnergyWeapon) continue (energy weapons do not really need to reload)", Logging.Orange);
                        continue;
                    }

                    if (weapon.IsReloadingAmmo)
                    {
                        if (Logging.DebugReloadAll) Logging.Log("debug ReloadAll", "[" + weapon.TypeName + "][" + _weaponNumber + "] is still reloading, moving on to next weapon", Logging.White);
                        continue;
                    }

                    if (weapon.IsDeactivating)
                    {
                        if (Logging.DebugReloadAll) Logging.Log("debug ReloadAll", "[" + weapon.TypeName + "][" + _weaponNumber + "] is still Deactivating, moving on to next weapon", Logging.White);
                        continue;
                    }

                    if (weapon.IsChangingAmmo)
                    {
                        if (Logging.DebugReloadAll) Logging.Log("debug ReloadAll", "[" + weapon.TypeName + "][" + _weaponNumber + "] is still Changing Ammo, moving on to next weapon", Logging.White);
                        continue;
                    }

                    if (weapon.IsActive)
                    {
                        if (Logging.DebugReloadAll) Logging.Log("debug ReloadAll", "[" + weapon.TypeName + "][" + _weaponNumber + "] is Active, moving on to next weapon", Logging.White);
                        continue;
                    }

                    if (QMCache.Instance.CurrentShipsCargo != null && QMCache.Instance.CurrentShipsCargo.Items.Any())
                    {
                        if (!ReloadAmmo(weapon, entity, _weaponNumber, force)) continue; //by returning false here we make sure we only reload one gun (or stack) per iteration (basically per second)
                    }

                    return false;
                }

                if (Logging.DebugReloadAll) Logging.Log("debug ReloadAll", "completely reloaded all weapons", Logging.White);
                //_reloadAllIteration = 0;
                return true;
            }

            //_reloadAllIteration = 0;
            return true;
        }

        /// <summary> Returns true if it can activate the weapon on the target
        /// </summary>
        /// <remarks>
        ///   The idea behind this function is that a target that explodes is not being fired on within 5 seconds
        /// </remarks>
        /// <param name = "module"></param>
        /// <param name = "entity"></param>
        /// <param name = "isWeapon"></param>
        /// <returns></returns>
        private static bool CanActivate(ModuleCache module, EntityCache entity, bool isWeapon)
        {
            if (!module.IsOnline)
            {
                return false;
            }

            if (module.IsActive || !module.IsActivatable)
            {
                return false;
            }

            if (isWeapon && !entity.IsTarget)
            {
                Logging.Log("Combat.CanActivate", "We attempted to shoot [" + entity.Name + "][" + Math.Round(entity.Distance/1000, 2) + "] which is currently not locked!", Logging.Debug);
                return false;
            }

            if (isWeapon && entity.Distance > Combat.MaxRange)
            {
                Logging.Log("Combat.CanActivate", "We attempted to shoot [" + entity.Name + "][" + Math.Round(entity.Distance / 1000, 2) + "] which is out of weapons range!", Logging.Debug);
                return false;
            }

            if (module.IsReloadingAmmo)
                return false;

            if (module.IsChangingAmmo)
                return false;

            if (module.IsDeactivating)
                return false;

            // We have changed target, allow activation
            if (entity.Id != module.LastTargetId)
                return true;

            // We have reloaded, allow activation
            if (isWeapon && module.CurrentCharges == MaxCharges)
                return true;

            // if the module is not already active, we have a target, it is in range, we are not reloading then ffs shoot it...
            return true;
        }

        /// <summary> Activate weapons
        /// </summary>
        private static void ActivateWeapons(EntityCache target)
        {
            // When in warp there's nothing we can do, so ignore everything
            if (QMCache.Instance.InSpace && QMCache.Instance.InWarp)
            {
                if (Combat.PrimaryWeaponPriorityEntities != null && Combat.PrimaryWeaponPriorityEntities.Any())
                {
                    RemovePrimaryWeaponPriorityTargets(Combat.PrimaryWeaponPriorityEntities.ToList());
                }

                if (Drones.UseDrones && Drones.DronePriorityEntities != null && Drones.DronePriorityEntities.Any())
                {
                    Drones.RemoveDronePriorityTargets(Drones.DronePriorityEntities.ToList());
                }

                if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: we are in warp! doing nothing", Logging.Teal);
                return;
            }

            if (!QMCache.Instance.Weapons.Any())
            {
                if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: you have no weapons?", Logging.Teal);
                return;
            }

            //
            // Do we really want a non-mission action moving the ship around at all!! (other than speed tanking)?
            // If you are not in a mission by all means let combat actions move you around as needed
            /*
            if (!QMCache.Instance.InMission)
            {
                if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: we are NOT in a mission: NavigateInToRange", Logging.Teal);
                NavigateOnGrid.NavigateIntoRange(target, "Combat");
            }
            if (QMSettings.Instance.SpeedTank)
            {
                if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: We are Speed Tanking: NavigateInToRange", Logging.Teal);
                NavigateOnGrid.NavigateIntoRange(target, "Combat");
            }
            */
            if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: after navigate into range...", Logging.Teal);

            // Get the weapons

            // TODO: Add check to see if there is better ammo to use! :)
            // Get distance of the target and compare that with the ammo currently loaded

            //Deactivate weapons that needs to be deactivated for this list of reasons...
            _weaponNumber = 0;
            if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: Do we need to deactivate any weapons?", Logging.Teal);

            if (QMCache.Instance.Weapons.Any())
            {
                foreach (ModuleCache weapon in QMCache.Instance.Weapons)
                {
                    _weaponNumber++;
                    if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: for each weapon [" + _weaponNumber + "] in weapons", Logging.Teal);

                    if (Time.Instance.LastActivatedTimeStamp != null && Time.Instance.LastActivatedTimeStamp.ContainsKey(weapon.ItemId))
                    {
                        if (Time.Instance.LastActivatedTimeStamp[weapon.ItemId].AddMilliseconds(Time.Instance.WeaponDelay_milliseconds) > DateTime.UtcNow)
                        {
                            continue;
                        }
                    }

                    if (!weapon.IsActive)
                    {
                        if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: [" + weapon.TypeName + "][" + _weaponNumber + "] is not active: no need to do anything", Logging.Teal);
                        continue;
                    }

                    if (weapon.IsReloadingAmmo)
                    {
                        if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: [" + weapon.TypeName + "][" + _weaponNumber + "] is reloading ammo: waiting", Logging.Teal);
                        continue;
                    }

                    if (weapon.IsDeactivating)
                    {
                        if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: [" + weapon.TypeName + "][" + _weaponNumber + "] is deactivating: waiting", Logging.Teal);
                        continue;
                    }

                    if (weapon.IsChangingAmmo)
                    {
                        if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: [" + weapon.TypeName + "][" + _weaponNumber + "] is changing ammo: waiting", Logging.Teal);
                        continue;
                    }

                    // No ammo loaded
                    if (weapon.Charge == null)
                    {
                        if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: no ammo loaded? [" + weapon.TypeName + "][" + _weaponNumber + "] reload will happen elsewhere", Logging.Teal);
                        continue;
                    }

                    Ammo ammo = Combat.Ammo.FirstOrDefault(a => a.TypeId == weapon.Charge.TypeId);

                    //use mission specific ammo
                    if (MissionSettings.AmmoTypesToLoad.Count() != 0)
                    {
                        if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: MissionAmmocount is not 0", Logging.Teal);
                        //var x = 0;
                        //ammo = MissionSettings.AmmoTypesToLoad.TryGetValue((Ammo)weapon.Charge.TypeName, DateTime.Now);
                    }

                    // How can this happen? Someone manually loaded ammo
                    if (ammo == null)
                    {
                        if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: ammo == null [" + weapon.TypeName + "][" + _weaponNumber + "] someone manually loaded ammo?", Logging.Teal);
                        continue;
                    }

                    if (weapon.CurrentCharges >= 2)
                    {
                        // If we have already activated warp, deactivate the weapons
                        if (!QMCache.Instance.ActiveShip.Entity.IsWarping)
                        {
                            // Target is in range
                            if (target.Distance <= ammo.Range)
                            {
                                if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: target is in range: do nothing, wait until it is dead", Logging.Teal);
                                continue;
                            }
                        }
                    }

                    // Target is out of range, stop firing
                    if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: deactivate: target is out of range, stop firing", Logging.Teal);
                    if (weapon.Click()) return;
                    return;
                }

                // Hack for max charges returning incorrect value
                if (!QMCache.Instance.Weapons.Any(w => w.IsEnergyWeapon))
                {
                    MaxCharges = Math.Max(MaxCharges, QMCache.Instance.Weapons.Max(l => l.MaxCharges));
                    MaxCharges = Math.Max(MaxCharges, QMCache.Instance.Weapons.Max(l => l.CurrentCharges));
                }

                int weaponsActivatedThisTick = 0;
                int weaponsToActivateThisTick = QMCache.Instance.RandomNumber(1, 4);

                // Activate the weapons (it not yet activated)))
                _weaponNumber = 0;
                if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: activate: Do we need to activate any weapons?", Logging.Teal);
                foreach (ModuleCache weapon in QMCache.Instance.Weapons)
                {
                    _weaponNumber++;

                    if (Time.Instance.LastActivatedTimeStamp != null && Time.Instance.LastActivatedTimeStamp.ContainsKey(weapon.ItemId))
                    {
                        if (Time.Instance.LastActivatedTimeStamp[weapon.ItemId].AddMilliseconds(Time.Instance.WeaponDelay_milliseconds) > DateTime.UtcNow)
                        {
                            continue;
                        }
                    }

                    // Are we reloading, deactivating or changing ammo?
                    if (weapon.IsReloadingAmmo)
                    {
                        if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: Activate: [" + weapon.TypeName + "][" + _weaponNumber + "] is reloading, waiting.", Logging.Teal);
                        continue;
                    }

                    if (weapon.IsDeactivating)
                    {
                        if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: Activate: [" + weapon.TypeName + "][" + _weaponNumber + "] is deactivating, waiting.", Logging.Teal);
                        continue;
                    }

                    if (weapon.IsChangingAmmo)
                    {
                        if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: Activate: [" + weapon.TypeName + "][" + _weaponNumber + "] is changing ammo, waiting.", Logging.Teal);
                        continue;
                    }

                    if (!target.IsTarget)
                    {
                        if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: Activate: [" + weapon.TypeName + "][" + _weaponNumber + "] is [" + target.Name + "] is not locked, waiting.", Logging.Teal);
                        continue;
                    }

                    // Are we on the right target?
                    if (weapon.IsActive)
                    {
                        if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: Activate: [" + weapon.TypeName + "][" + _weaponNumber + "] is active already", Logging.Teal);
                        if (weapon.TargetId != target.Id && target.IsTarget)
                        {
                            if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: Activate: [" + weapon.TypeName + "][" + _weaponNumber + "] is shooting at the wrong target: deactivating", Logging.Teal);
                            if (weapon.Click()) return;

                            return;
                        }
                        continue;
                    }

                    // No, check ammo type and if that is correct, activate weapon
                    if (ReloadAmmo(weapon, target, _weaponNumber) && CanActivate(weapon, target, true))
                    {
                        if (weaponsActivatedThisTick > weaponsToActivateThisTick)
                        {
                            if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: if we have already activated x number of weapons return, which will wait until the next ProcessState", Logging.Teal);
                            return;
                        }

                        if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: Activate: [" + weapon.TypeName + "][" + _weaponNumber + "] has the correct ammo: activate", Logging.Teal);
                        if (weapon.Activate(target))
                        {
                            weaponsActivatedThisTick++; //increment the number of weapons we have activated this ProcessState so that we might optionally activate more than one module per tick
                            Logging.Log("Combat", "Activating weapon  [" + _weaponNumber + "] on [" + target.Name + "][" + target.MaskedId + "][" + Math.Round(target.Distance / 1000, 0) + "k away]", Logging.Teal);
                            continue;
                        }

                        continue;
                    }

                    if (Logging.DebugActivateWeapons) Logging.Log("Combat", "ActivateWeapons: ReloadReady [" + ReloadAmmo(weapon, target, _weaponNumber) + "] CanActivateReady [" + CanActivate(weapon, target, true) + "]", Logging.Teal);
                }
            }
            else
            {
                Logging.Log("Combat", "ActivateWeapons: you have no weapons with groupID: [ " + Combat.WeaponGroupId + " ]", Logging.Debug);
                icount = 0;
                foreach (ModuleCache __module in QMCache.Instance.Modules.Where(e => e.IsOnline && e.IsActivatable))
                {
                    icount++;
                    Logging.Log("Fitted Modules", "[" + icount + "] Module TypeID [ " + __module.TypeId + " ] ModuleGroupID [ " + __module.GroupId + " ] EveCentral Link [ http://eve-central.com/home/quicklook.html?typeid=" + __module.TypeId + " ]", Logging.Debug);
                }
            }
        }

        /// <summary> Activate target painters
        /// </summary>
        private static void ActivateTargetPainters(EntityCache target)
        {
            if (target.IsEwarImmune)
            {
                if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "Ignoring TargetPainter Activation on [" + target.Name + "]IsEwarImmune[" + target.IsEwarImmune + "]", Logging.Debug);
                return;
            }

            List<ModuleCache> targetPainters = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.TargetPainter).ToList();

            // Find the first active weapon
            // Assist this weapon
            _weaponNumber = 0;
            foreach (ModuleCache painter in targetPainters)
            {
                if (Time.Instance.LastActivatedTimeStamp != null && Time.Instance.LastActivatedTimeStamp.ContainsKey(painter.ItemId))
                {
                    if (Time.Instance.LastActivatedTimeStamp[painter.ItemId].AddMilliseconds(Time.Instance.PainterDelay_milliseconds) > DateTime.UtcNow)
                    {
                        continue;
                    }
                }

                _weaponNumber++;

                // Are we on the right target?
                if (painter.IsActive)
                {
                    if (painter.TargetId != target.Id)
                    {
                        if (painter.Click()) return;

                        return;
                    }

                    continue;
                }

                // Are we deactivating?
                if (painter.IsDeactivating)
                    continue;

                if (CanActivate(painter, target, false))
                {
                    if (painter.Activate(target))
                    {
                        Logging.Log("Combat", "Activating [" + painter.TypeName + "][" + _weaponNumber + "] on [" + target.Name + "][" + target.MaskedId + "][" + Math.Round(target.Distance / 1000, 0) + "k away]", Logging.Teal);
                        return;
                    }

                    continue;
                }
            }
        }

        /// <summary> Activate target painters
        /// </summary>
        private static void ActivateSensorDampeners(EntityCache target)
        {
            if (target.IsEwarImmune)
            {
                if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "Ignoring SensorDamps Activation on [" + target.Name + "]IsEwarImmune[" + target.IsEwarImmune + "]", Logging.Debug);
                return;
            }

            List<ModuleCache> sensorDampeners = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.SensorDampener).ToList();

            // Find the first active weapon
            // Assist this weapon
            _weaponNumber = 0;
            foreach (ModuleCache sensorDampener in sensorDampeners)
            {
                if (Time.Instance.LastActivatedTimeStamp != null && Time.Instance.LastActivatedTimeStamp.ContainsKey(sensorDampener.ItemId))
                {
                    if (Time.Instance.LastActivatedTimeStamp[sensorDampener.ItemId].AddMilliseconds(Time.Instance.PainterDelay_milliseconds) > DateTime.UtcNow)
                    {
                        continue;
                    }
                }

                _weaponNumber++;

                // Are we on the right target?
                if (sensorDampener.IsActive)
                {
                    if (sensorDampener.TargetId != target.Id)
                    {
                        if (sensorDampener.Click()) return;
                        return;
                    }

                    continue;
                }

                // Are we deactivating?
                if (sensorDampener.IsDeactivating)
                    continue;

                if (CanActivate(sensorDampener, target, false))
                {
                    if (sensorDampener.Activate(target))
                    {
                        Logging.Log("Combat", "Activating [" + sensorDampener.TypeName + "][" + _weaponNumber + "] on [" + target.Name + "][" + target.MaskedId + "][" + Math.Round(target.Distance / 1000, 0) + "k away]", Logging.Teal);
                        return;
                    }

                    continue;
                }
            }
        }

        /// <summary> Activate Nos
        /// </summary>
        private static void ActivateNos(EntityCache target)
        {
            if (target.IsEwarImmune)
            {
                if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "Ignoring NOS/NEUT Activation on [" + target.Name + "]IsEwarImmune[" + target.IsEwarImmune + "]", Logging.Debug);
                return;
            }

            List<ModuleCache> noses = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.NOS || m.GroupId == (int)Group.Neutralizer).ToList();

            //Logging.Log("Combat: we have " + noses.Count.ToString() + " Nos modules");
            // Find the first active weapon
            // Assist this weapon
            _weaponNumber = 0;
            foreach (ModuleCache nos in noses)
            {
                _weaponNumber++;

                if (Time.Instance.LastActivatedTimeStamp != null && Time.Instance.LastActivatedTimeStamp.ContainsKey(nos.ItemId))
                {
                    if (Time.Instance.LastActivatedTimeStamp[nos.ItemId].AddMilliseconds(Time.Instance.NosDelay_milliseconds) > DateTime.UtcNow)
                    {
                        continue;
                    }
                }

                // Are we on the right target?
                if (nos.IsActive)
                {
                    if (nos.TargetId != target.Id)
                    {
                        if (nos.Click()) return;

                        return;
                    }

                    continue;
                }

                // Are we deactivating?
                if (nos.IsDeactivating)
                    continue;

                //Logging.Log("Combat: Distances Target[ " + Math.Round(target.Distance,0) + " Optimal[" + nos.OptimalRange.ToString()+"]");
                // Target is out of Nos range
                if (target.Distance >= nos.MaxRange)
                    continue;

                if (CanActivate(nos, target, false))
                {
                    if (nos.Activate(target))
                    {
                        Logging.Log("Combat", "Activating [" + nos.TypeName + "][" + _weaponNumber + "] on [" + target.Name + "][" + target.MaskedId + "][" + Math.Round(target.Distance / 1000, 0) + "k away]", Logging.Teal);
                        return;
                    }

                    continue;
                }

                Logging.Log("Combat", "Cannot Activate [" + nos.TypeName + "][" + _weaponNumber + "] on [" + target.Name + "][" + target.MaskedId + "][" + Math.Round(target.Distance / 1000, 0) + "k away]", Logging.Teal);
            }
        }

        /// <summary> Activate StasisWeb
        /// </summary>
        private static void ActivateStasisWeb(EntityCache target)
        {
            if (target.IsEwarImmune)
            {
                if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "Ignoring StasisWeb Activation on [" + target.Name + "]IsEwarImmune[" + target.IsEwarImmune + "]", Logging.Debug);
                return;
            }

            List<ModuleCache> webs = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.StasisWeb).ToList();

            // Find the first active weapon
            // Assist this weapon
            _weaponNumber = 0;
            foreach (ModuleCache web in webs)
            {
                _weaponNumber++;

                if (Time.Instance.LastActivatedTimeStamp != null && Time.Instance.LastActivatedTimeStamp.ContainsKey(web.ItemId))
                {
                    if (Time.Instance.LastActivatedTimeStamp[web.ItemId].AddMilliseconds(Time.Instance.WebDelay_milliseconds) > DateTime.UtcNow)
                    {
                        continue;
                    }
                }

                // Are we on the right target?
                if (web.IsActive)
                {
                    if (web.TargetId != target.Id)
                    {
                        if (web.Click()) return;

                        return;
                    }

                    continue;
                }

                // Are we deactivating?
                if (web.IsDeactivating)
                    continue;

                // Target is out of web range
                if (target.Distance >= web.OptimalRange)
                    continue;

                if (CanActivate(web, target, false))
                {
                    if (web.Activate(target))
                    {
                        Logging.Log("Combat", "Activating [" + web.TypeName + "][" + _weaponNumber + "] on [" + target.Name + "][" + target.MaskedId + "]", Logging.Teal);
                        return;
                    }

                    continue;
                }
            }
        }

        public static bool ActivateBastion(bool activate = false)
        {
            List<ModuleCache> bastionModules = null;
            bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
            if (!bastionModules.Any()) return true;
            if (bastionModules.Any(i => i.IsActive && i.IsDeactivating)) return true;

            if (!Combat.PotentialCombatTargets.Where(e => e.Distance < QMCache.Instance.WeaponRange).Any(e => e.IsTarget || e.IsTargeting) && CombatMissionCtrl.DeactivateIfNothingTargetedWithinRange)
            {
                if (Logging.DebugActivateBastion) Logging.Log("ActivateBastion", "NextBastionModeDeactivate set to 2 sec ago: We have no targets in range and DeactivateIfNothingTargetedWithinRange [ " + CombatMissionCtrl.DeactivateIfNothingTargetedWithinRange + " ]", Logging.Debug);
                Time.Instance.NextBastionModeDeactivate = DateTime.UtcNow.AddSeconds(-2);
            }

            if (Combat.PotentialCombatTargets.Any(e => e.Distance < QMCache.Instance.WeaponRange && e.IsPlayer && e.IsTargetedBy && e.IsAttacking) && _States.CurrentCombatState != CombatState.OutOfAmmo)
            {
                if (Logging.DebugActivateBastion) Logging.Log("ActivateBastion", "We are being attacked by a player we should activate bastion", Logging.Debug);
                activate = true;
            }

            if (_States.CurrentPanicState == PanicState.Panicking || _States.CurrentPanicState == PanicState.StartPanicking)
            {
                if (Logging.DebugActivateBastion) Logging.Log("ActivateBastion", "NextBastionModeDeactivate set to 2 sec ago: We are in panic!", Logging.Debug);
                Time.Instance.NextBastionModeDeactivate = DateTime.UtcNow.AddSeconds(-2);
            }

            if (DateTime.UtcNow < Time.Instance.NextBastionAction)
            {
                if (Logging.DebugActivateBastion) Logging.Log("ActivateBastion", "NextBastionAction [" + Time.Instance.NextBastionAction.Subtract(DateTime.UtcNow).TotalSeconds + "] seconds, waiting...", Logging.Debug);
                return false;
            }

            // Find the first active weapon
            // Assist this weapon
            _weaponNumber = 0;
            foreach (ModuleCache bastionMod in bastionModules)
            {
                _weaponNumber++;

                if (Logging.DebugActivateBastion) Logging.Log("ActivateBastion", "[" + _weaponNumber + "] BastionModule: IsActive [" + bastionMod.IsActive + "] IsDeactivating [" + bastionMod.IsDeactivating + "] InLimboState [" + bastionMod.InLimboState + "] Duration [" + bastionMod.Duration + "] TypeId [" + bastionMod.TypeId + "]", Logging.Debug);

                //
                // Deactivate (if needed)
                //
                // Are we on the right target?
                if (bastionMod.IsActive && !bastionMod.IsDeactivating)
                {
                    if (Logging.DebugActivateBastion) Logging.Log("ActivateBastion", "IsActive and Is not yet deactivating (we only want one cycle), attempting to Click...", Logging.Debug);
                    if (bastionMod.Click()) return true;
                    return false;
                }

                if (bastionMod.IsActive)
                {
                    if (Logging.DebugActivateBastion) Logging.Log("ActivateBastion", "IsActive: assuming it is deactivating on the next cycle.", Logging.Debug);
                    return true;
                }

                //
                // Activate (if needed)
                //

                // Are we deactivating?
                if (bastionMod.IsDeactivating)
                    continue;

                if (!bastionMod.IsActive && activate)
                {
                    Logging.Log("Combat", "Activating bastion [" + _weaponNumber + "]", Logging.Teal);
                    if (bastionMod.Click())
                    {
                        Time.Instance.NextBastionAction = DateTime.UtcNow.AddSeconds(QMCache.Instance.RandomNumber(3, 20));
                        return true;
                    }

                    return false;
                }
            }

            return true; //if we got  this far we have done all we can do.
        }

        private static void ActivateWarpDisruptor(EntityCache target)
        {
            if (target.IsEwarImmune)
            {
                if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "Ignoring WarpDisruptor Activation on [" + target.Name + "]IsEwarImmune[" + target.IsEwarImmune + "]", Logging.Debug);
                return;
            }

            List<ModuleCache> WarpDisruptors = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.WarpDisruptor).ToList();

            // Find the first active weapon
            // Assist this weapon
            _weaponNumber = 0;
            foreach (ModuleCache WarpDisruptor in WarpDisruptors)
            {
                _weaponNumber++;

                if (Time.Instance.LastActivatedTimeStamp != null && Time.Instance.LastActivatedTimeStamp.ContainsKey(WarpDisruptor.ItemId))
                {
                    if (Time.Instance.LastActivatedTimeStamp[WarpDisruptor.ItemId].AddMilliseconds(Time.Instance.WebDelay_milliseconds) > DateTime.UtcNow)
                    {
                        continue;
                    }
                }

                // Are we on the right target?
                if (WarpDisruptor.IsActive)
                {
                    if (WarpDisruptor.TargetId != target.Id)
                    {
                        if (WarpDisruptor.Click()) return;

                        return;
                    }

                    continue;
                }

                // Are we deactivating?
                if (WarpDisruptor.IsDeactivating)
                    continue;

                // Target is out of web range
                if (target.Distance >= WarpDisruptor.OptimalRange)
                    continue;

                if (CanActivate(WarpDisruptor, target, false))
                {
                    if (WarpDisruptor.Activate(target))
                    {
                        Logging.Log("Combat", "Activating [" + WarpDisruptor.TypeName + "][" + _weaponNumber + "] on [" + target.Name + "][" + target.MaskedId + "]", Logging.Teal);
                        return;
                    }

                    continue;
                }
            }
        }

        private static void ActivateRemoteRepair(EntityCache target)
        {
            List<ModuleCache> RemoteRepairers = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.RemoteArmorRepairer
                                                                               || m.GroupId == (int)Group.RemoteShieldRepairer
                                                                               || m.GroupId == (int)Group.RemoteHullRepairer
                                                                               ).ToList();

            // Find the first active weapon
            // Assist this weapon
            _weaponNumber = 0;
            if (RemoteRepairers.Any())
            {
                if (Logging.DebugRemoteRepair) Logging.Log("ActivateRemoteRepair", "RemoteRepairers [" + RemoteRepairers.Count() + "] Target Distance [" + Math.Round(target.Distance / 1000, 0) + "] RemoteRepairDistance [" + Math.Round(((double)Combat.RemoteRepairDistance / 1000), digits: 0) + "]", Logging.Debug);
                foreach (ModuleCache RemoteRepairer in RemoteRepairers)
                {
                    _weaponNumber++;

                    if (Time.Instance.LastActivatedTimeStamp != null && Time.Instance.LastActivatedTimeStamp.ContainsKey(RemoteRepairer.ItemId))
                    {
                        if (Time.Instance.LastActivatedTimeStamp[RemoteRepairer.ItemId].AddMilliseconds(Time.Instance.RemoteRepairerDelay_milliseconds) > DateTime.UtcNow)
                        {
                            continue;
                        }
                    }

                    // Are we on the right target?
                    if (RemoteRepairer.IsActive)
                    {
                        if (RemoteRepairer.TargetId != target.Id)
                        {
                            if (RemoteRepairer.Click()) return;

                            return;
                        }

                        continue;
                    }

                    // Are we deactivating?
                    if (RemoteRepairer.IsDeactivating)
                        continue;

                    // Target is out of RemoteRepair range
                    if (target.Distance >= RemoteRepairer.MaxRange)
                        continue;

                    if (CanActivate(RemoteRepairer, target, false))
                    {
                        if (RemoteRepairer.Activate(target))
                        {
                            Logging.Log("Combat", "Activating [" + RemoteRepairer.TypeName + "][" + _weaponNumber + "] on [" + target.Name + "][" + target.MaskedId + "]", Logging.Teal);
                            return;
                        }

                        continue;
                    }
                }
            }

        }

        private static bool UnlockHighValueTarget(string module, string reason, bool OutOfRangeOnly = false)
        {
            EntityCache unlockThisHighValueTarget = null;
            long preferredId = Combat.PreferredPrimaryWeaponTarget != null ? Combat.PreferredPrimaryWeaponTarget.Id : -1;

            if (!OutOfRangeOnly)
            {
                if (lowValueTargetsTargeted.Count() > maxLowValueTargets && maxTotalTargets <= lowValueTargetsTargeted.Count() + highValueTargetsTargeted.Count())
                {
                    return UnlockLowValueTarget(module, reason, OutOfRangeOnly);    // We are using HighValueSlots for lowvaluetarget (which is ok)
                                                                                    // but we now need 1 slot back to target our PreferredTarget
                }

                try
                {
                    if (highValueTargetsTargeted.Count(t => t.Id != preferredId) >= maxHighValueTargets)
                    {
                        //unlockThisHighValueTarget = QMCache.Instance.GetBestWeaponTargets((double)Distances.OnGridWithMe).Where(t => t.IsTarget && highValueTargetsTargeted.Any(e => t.Id == e.Id)).LastOrDefault();

                        unlockThisHighValueTarget = highValueTargetsTargeted.Where(h =>  (h.IsTarget && h.IsIgnored)
                                                                                        || (h.IsTarget && (!h.isPreferredDroneTarget && !h.IsDronePriorityTarget && !h.isPreferredPrimaryWeaponTarget && !h.IsPrimaryWeaponPriorityTarget && !h.IsPriorityWarpScrambler && !h.IsInOptimalRange && Combat.PotentialCombatTargets.Count() >= 3))
                                                                                        || (h.IsTarget && (!h.isPreferredPrimaryWeaponTarget && !h.IsDronePriorityTarget && h.IsHigherPriorityPresent && !h.IsPrimaryWeaponPriorityTarget && highValueTargetsTargeted.Count() == maxHighValueTargets) && !h.IsPriorityWarpScrambler))
                                                                                        .OrderByDescending(t => t.Distance > Combat.MaxRange)
                                                                                        .ThenByDescending(t => t.Distance)
                                                                                        .FirstOrDefault();
                    }
                }
                catch (NullReferenceException) { }

            }
            else
            {
                try
                {
                    unlockThisHighValueTarget = highValueTargetsTargeted.Where(h => h.IsTarget && h.IsIgnored && !h.IsPriorityWarpScrambler)
                                                                        .OrderByDescending(t => t.Distance > Combat.MaxRange)
                                                                        .ThenByDescending(t => t.Distance)
                                                                        .FirstOrDefault();
                }
                catch (NullReferenceException) { }
            }

            if (unlockThisHighValueTarget != null)
            {
                Logging.Log("Combat [TargetCombatants]" + module, "Unlocking HighValue " + unlockThisHighValueTarget.Name + "[" + Math.Round(unlockThisHighValueTarget.Distance/1000,0) + "k] myTargtingRange:[" + Combat.MaxTargetRange + "] myWeaponRange[:" + QMCache.Instance.WeaponRange + "] to make room for [" + reason + "]", Logging.Orange);
                unlockThisHighValueTarget.UnlockTarget("Combat [TargetCombatants]");
                //QMCache.Instance.NextTargetAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.TargetDelay_milliseconds);
                return false;
            }

            if (!OutOfRangeOnly)
            {
                //Logging.Log("Combat [TargetCombatants]" + module, "We don't have a spot open to target [" + reason + "], this could be a problem", Logging.Orange);
                //QMCache.Instance.NextTargetAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.TargetDelay_milliseconds);
            }

            return true;

        }

        private static bool UnlockLowValueTarget(string module, string reason, bool OutOfWeaponsRange = false)
        {
            EntityCache unlockThisLowValueTarget = null;
            if (!OutOfWeaponsRange)
            {
                try
                {
                    unlockThisLowValueTarget = lowValueTargetsTargeted.Where(h => (h.IsTarget && h.IsIgnored)
                                                                                 || (h.IsTarget && (!h.isPreferredDroneTarget && !h.IsDronePriorityTarget && !h.isPreferredPrimaryWeaponTarget && !h.IsPrimaryWeaponPriorityTarget && !h.IsPriorityWarpScrambler && !h.IsInOptimalRange && Combat.PotentialCombatTargets.Count() >= 3))
                                                                                 || (h.IsTarget && (!h.isPreferredDroneTarget && !h.IsDronePriorityTarget && !h.isPreferredPrimaryWeaponTarget && !h.IsPrimaryWeaponPriorityTarget && !h.IsPriorityWarpScrambler && lowValueTargetsTargeted.Count() == maxLowValueTargets))
                                                                                 || (h.IsTarget && (!h.isPreferredDroneTarget && !h.IsDronePriorityTarget && !h.isPreferredPrimaryWeaponTarget && !h.IsPrimaryWeaponPriorityTarget && h.IsHigherPriorityPresent && !h.IsPriorityWarpScrambler && lowValueTargetsTargeted.Count() == maxLowValueTargets)))
                                                                                 .OrderByDescending(t => t.Distance < (Drones.UseDrones ? Drones.MaxDroneRange : QMCache.Instance.WeaponRange))
                                                                                .ThenByDescending(t => t.Nearest5kDistance)
                                                                                .FirstOrDefault();
                }
                catch (NullReferenceException) { }
            }
            else
            {
                try
                {
                    unlockThisLowValueTarget = lowValueTargetsTargeted.Where(h => (h.IsTarget && h.IsIgnored)
                                                                                 || (h.IsTarget && (!h.isPreferredDroneTarget && !h.IsDronePriorityTarget && !h.isPreferredPrimaryWeaponTarget  && !h.IsPrimaryWeaponPriorityTarget && h.IsHigherPriorityPresent && !h.IsPriorityWarpScrambler && !h.IsReadyToShoot  && lowValueTargetsTargeted.Count() == maxLowValueTargets)))
                                                                                 .OrderByDescending(t => t.Distance < (Drones.UseDrones ? Drones.MaxDroneRange : QMCache.Instance.WeaponRange))
                                                                                 .ThenByDescending(t => t.Nearest5kDistance)
                                                                                 .FirstOrDefault();
                }
                catch (NullReferenceException) { }
            }

            if (unlockThisLowValueTarget != null)
            {
                Logging.Log("Combat [TargetCombatants]" + module, "Unlocking LowValue " + unlockThisLowValueTarget.Name + "[" + Math.Round(unlockThisLowValueTarget.Distance / 1000, 0) + "k] myTargtingRange:[" + Combat.MaxTargetRange + "] myWeaponRange[:" + QMCache.Instance.WeaponRange + "] to make room for [" + reason + "]", Logging.Orange);
                unlockThisLowValueTarget.UnlockTarget("Combat [TargetCombatants]");
                //QMCache.Instance.NextTargetAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.TargetDelay_milliseconds);
                return false;
            }

            if (!OutOfWeaponsRange)
            {
                //Logging.Log("Combat [TargetCombatants]" + module, "We don't have a spot open to target [" + reason + "], this could be a problem", Logging.Orange);
                //QMCache.Instance.NextTargetAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.TargetDelay_milliseconds);
            }

            return true;
        }

        /// <summary> Target combatants
        /// </summary>
        private static void TargetCombatants()
        {

            if ((QMCache.Instance.InSpace && QMCache.Instance.InWarp) // When in warp we should not try to target anything
                    || QMCache.Instance.InStation //How can we target if we are in a station?
                    || DateTime.UtcNow < Time.Instance.NextTargetAction //if we just did something wait a fraction of a second
                    //|| !QMCache.Instance.OpenCargoHold("Combat.TargetCombatants") //If we can't open our cargohold then something MUST be wrong
                    || Logging.DebugDisableTargetCombatants
                )
            {
                if (Logging.DebugTargetCombatants) Logging.Log("InSpace [ " + QMCache.Instance.InSpace + " ] InWarp [ " + QMCache.Instance.InWarp + " ] InStation [ " + QMCache.Instance.InStation + " ] NextTargetAction [ " + Time.Instance.NextTargetAction.Subtract(DateTime.UtcNow).TotalSeconds + " seconds] DebugDisableTargetCombatants [ " + Logging.DebugDisableTargetCombatants + " ]", "", Logging.Debug);
                return;
            }

            #region ECM Jamming checks
            //
            // First, can we even target?
            // We are ECM'd / jammed, forget targeting anything...
            //
            if (QMCache.Instance.MaxLockedTargets == 0)
            {
                if (!_isJammed)
                {
                    Logging.Log("Combat", "We are jammed and can not target anything", Logging.Orange);
                }

                _isJammed = true;
                return;
            }

            if (_isJammed)
            {
                // Clear targeting list as it does not apply
                QMCache.Instance.TargetingIDs.Clear();
                Logging.Log("Combat", "We are no longer jammed, reTargeting", Logging.Teal);
            }

            _isJammed = false;
            #endregion

            #region Current active targets/targeting
            //
            // What do we currently have targeted?
            // Get our current targets/targeting
            //

            // Get lists of the current high and low value targets
            try
            {
                highValueTargetsTargeted = QMCache.Instance.EntitiesOnGrid.Where(t => (t.IsTarget || t.IsTargeting) && (t.IsHighValueTarget)).ToList();
            }
            catch (NullReferenceException) { }

            try
            {
                lowValueTargetsTargeted = QMCache.Instance.EntitiesOnGrid.Where(t => (t.IsTarget || t.IsTargeting) && (t.IsLowValueTarget)).ToList();
            }
            catch (NullReferenceException) { }

            int targetsTargeted = highValueTargetsTargeted.Count() + lowValueTargetsTargeted.Count();
            #endregion

            #region Remove any target that is out of range (lower of Weapon Range or targeting range, definitely matters if damped)
            if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: Remove any target that is out of range", Logging.Debug);
            //
            // If it is currently out of our weapon range unlock it for now, unless it is one of our preferred targets which should technically only happen during kill type actions
            //
            if (QMCache.Instance.Targets.Any() && QMCache.Instance.Targets.Count() > 1)
            {
                //
                // unlock low value targets that are out of range or ignored
                //
                if (!UnlockLowValueTarget("Combat.TargetCombatants", "[lowValue]OutOfRange or Ignored", true)) return;
                //
                // unlock high value targets that are out of range or ignored
                //
                if (!UnlockHighValueTarget("Combat.TargetCombatants", "[highValue]OutOfRange or Ignored", true)) return;
            }
            #endregion Remove any target that is too far out of range (Weapon Range)

            #region Priority Target Handling
            if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: Priority Target Handling", Logging.Debug);
            //
            // Now lets deal with the priority targets
            //
            if (Combat.PrimaryWeaponPriorityEntities != null && Combat.PrimaryWeaponPriorityEntities.Any())
            {
                if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "We have [" + Combat.PrimaryWeaponPriorityEntities.Count() + "] PWPT. We have [" + QMCache.Instance.TotalTargetsandTargeting.Count() + "] TargetsAndTargeting. We have [" + Combat.PrimaryWeaponPriorityEntities.Count(i => i.IsTarget) + "] PWPT that are already targeted", Logging.Debug);
                int PrimaryWeaponsPriorityTargetUnTargeted = Combat.PrimaryWeaponPriorityEntities.Count() - QMCache.Instance.TotalTargetsandTargeting.Count(t => Combat.PrimaryWeaponPriorityEntities.Contains(t));

                if (PrimaryWeaponsPriorityTargetUnTargeted > 0)
                {
                    if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "if (PrimaryWeaponsPriorityTargetUnTargeted > 0)", Logging.Debug);
                    //
                    // unlock a lower priority entity if needed
                    //
                    if (!UnlockHighValueTarget("Combat.TargetCombatants", "PrimaryWeaponPriorityTargets")) return;

                    if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "if (!UnlockHighValueTarget(Combat.TargetCombatants, PrimaryWeaponPriorityTargets return;", Logging.Debug);

                    IEnumerable<EntityCache> __primaryWeaponPriorityEntities = Combat.PrimaryWeaponPriorityEntities.Where(t => t.IsTargetWeCanShootButHaveNotYetTargeted)
                                                                                                                     .OrderByDescending(c => c.IsLastTargetPrimaryWeaponsWereShooting)
                                                                                                                     .ThenByDescending(c => c.IsInOptimalRange)
                                                                                                                     .ThenBy(c => c.Distance);

                    if (__primaryWeaponPriorityEntities.Any())
                    {
                        if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: [" + __primaryWeaponPriorityEntities.Count() + "] primaryWeaponPriority targets", Logging.Debug);

                        foreach (EntityCache primaryWeaponPriorityEntity in __primaryWeaponPriorityEntities)
                        {
                            // Have we reached the limit of high value targets?
                            if (highValueTargetsTargeted.Count() >= maxHighValueTargets)
                            {
                                if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: __highValueTargetsTargeted [" + highValueTargetsTargeted.Count() + "] >= maxHighValueTargets [" + maxHighValueTargets + "]", Logging.Debug);
                                break;
                            }

                            if (primaryWeaponPriorityEntity.Distance < Combat.MaxRange
                                && primaryWeaponPriorityEntity.IsReadyToTarget)
                            {
                                if (QMCache.Instance.TotalTargetsandTargetingCount < QMCache.Instance.TargetingSlotsNotBeingUsedBySalvager)
                                {
                                    if (primaryWeaponPriorityEntity.LockTarget("TargetCombatants.PrimaryWeaponPriorityEntity"))
                                    {
                                        Logging.Log("Combat", "Targeting primary weapon priority target [" + primaryWeaponPriorityEntity.Name + "][" + primaryWeaponPriorityEntity.MaskedId + "][" + Math.Round(primaryWeaponPriorityEntity.Distance / 1000, 0) + "k away]", Logging.Teal);
                                        Time.Instance.NextTargetAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.TargetDelay_milliseconds);
                                        if (QMCache.Instance.TotalTargetsandTargeting.Any() && (QMCache.Instance.TotalTargetsandTargeting.Count() >= QMCache.Instance.MaxLockedTargets))
                                        {
                                            Time.Instance.NextTargetAction = DateTime.UtcNow.AddSeconds(Time.Instance.TargetsAreFullDelay_seconds);
                                        }

                                        return;
                                    }
                                }

                                if (QMCache.Instance.TotalTargetsandTargetingCount >= QMCache.Instance.TargetingSlotsNotBeingUsedBySalvager)
                                {
                                    if (lowValueTargetsTargeted.Any())
                                    {
                                        Combat.UnlockLowValueTarget("TargetCombatants", "PriorityTarget Needs to be targeted");
                                        return;
                                    }

                                    if (highValueTargetsTargeted.Any())
                                    {
                                        Combat.UnlockHighValueTarget("TargetCombatants", "PriorityTarget Needs to be targeted");
                                        return;
                                    }

                                    //
                                    // if we have nothing to unlock just continue...
                                    //
                                }
                            }

                            continue;
                        }
                    }
                    else
                    {
                        if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: 0 primaryWeaponPriority targets", Logging.Debug);
                    }
                }
            }
            #endregion

            #region Drone Priority Target Handling
            if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: Drone Priority Target Handling", Logging.Debug);
            //
            // Now lets deal with the priority targets
            //
            if (Drones.DronePriorityTargets.Any())
            {
                int DronesPriorityTargetUnTargeted = Drones.DronePriorityEntities.Count() - QMCache.Instance.TotalTargetsandTargeting.Count(t => Drones.DronePriorityEntities.Contains(t));

                if (DronesPriorityTargetUnTargeted > 0)
                {
                    if (!UnlockLowValueTarget("Combat.TargetCombatants", "DronePriorityTargets")) return;

                    IEnumerable<EntityCache> _dronePriorityTargets = Drones.DronePriorityEntities.Where(t => t.IsTargetWeCanShootButHaveNotYetTargeted)
                                                                                                                         .OrderByDescending(c => c.IsInDroneRange)
                                                                                                                         .ThenByDescending(c => c.IsLastTargetPrimaryWeaponsWereShooting)
                                                                                                                         .ThenBy(c => c.Nearest5kDistance);

                    if (_dronePriorityTargets.Any())
                    {
                        if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: [" + _dronePriorityTargets.Count() + "] dronePriority targets", Logging.Debug);

                        foreach (EntityCache dronePriorityEntity in _dronePriorityTargets)
                        {
                            // Have we reached the limit of low value targets?
                            if (lowValueTargetsTargeted.Count() >= maxLowValueTargets)
                            {
                                if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: __lowValueTargetsTargeted [" + lowValueTargetsTargeted.Count() + "] >= maxLowValueTargets [" + maxLowValueTargets + "]", Logging.Debug);
                                break;
                            }

                            if (dronePriorityEntity.Nearest5kDistance < Drones.MaxDroneRange
                                && dronePriorityEntity.IsReadyToTarget
                                && dronePriorityEntity.Nearest5kDistance < LowValueTargetsHaveToBeWithinDistance
                                && !dronePriorityEntity.IsIgnored)
                            {
                                if (QMCache.Instance.TotalTargetsandTargetingCount < QMCache.Instance.TargetingSlotsNotBeingUsedBySalvager)
                                {
                                    if (dronePriorityEntity.LockTarget("TargetCombatants.PrimaryWeaponPriorityEntity"))
                                    {
                                        Logging.Log("Combat", "Targeting primary weapon priority target [" + dronePriorityEntity.Name + "][" + dronePriorityEntity.MaskedId + "][" + Math.Round(dronePriorityEntity.Distance / 1000, 0) + "k away]", Logging.Teal);
                                        return;
                                    }
                                }

                                if (QMCache.Instance.TotalTargetsandTargetingCount >= QMCache.Instance.TargetingSlotsNotBeingUsedBySalvager)
                                {
                                    if (lowValueTargetsTargeted.Any())
                                    {
                                        Combat.UnlockLowValueTarget("TargetCombatants", "PriorityTarget Needs to be targeted");
                                        return;
                                    }

                                    if (highValueTargetsTargeted.Any())
                                    {
                                        Combat.UnlockHighValueTarget("TargetCombatants", "PriorityTarget Needs to be targeted");
                                        return;
                                    }

                                    Time.Instance.NextTargetAction = DateTime.UtcNow.AddSeconds(Time.Instance.TargetsAreFullDelay_seconds);
                                    //
                                    // if we have nothing to unlock just continue...
                                    //
                                }
                            }

                            continue;
                        }
                    }
                    else
                    {
                        if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: 0 primaryWeaponPriority targets", Logging.Debug);
                    }
                }
            }
            #endregion

            #region Preferred Primary Weapon target handling
            if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: Preferred Primary Weapon target handling", Logging.Debug);
            //
            // Lets deal with our preferred targets next (in other words what Q is actively trying to shoot or engage drones on)
            //

            if (Combat.PreferredPrimaryWeaponTarget != null)
            {

                if (Combat.PreferredPrimaryWeaponTarget.IsIgnored)
                {
                    Logging.Log("TargetCombatants", "if (Combat.PreferredPrimaryWeaponTarget.IsIgnored) Combat.PreferredPrimaryWeaponTarget = null;", Logging.Red);
                    //Combat.PreferredPrimaryWeaponTarget = null;
                }

                if (Combat.PreferredPrimaryWeaponTarget != null)
                {
                    if (Logging.DebugTargetCombatants) Logging.Log("TargetCombatants", "if (Combat.PreferredPrimaryWeaponTarget != null)", Logging.Debug);
                    if (QMCache.Instance.EntitiesOnGrid.Any(e => e.Id == Combat.PreferredPrimaryWeaponTarget.Id))
                    {
                        if (Logging.DebugTargetCombatants) Logging.Log("TargetCombatants", "if (QMCache.Instance.Entities.Any(i => i.Id == Combat.PreferredPrimaryWeaponTarget.Id))", Logging.Debug);

                        if (Logging.DebugTargetCombatants)
                        {
                            Logging.Log("[" + Combat.PreferredPrimaryWeaponTarget.Name + "] Distance [" + Math.Round(Combat.PreferredPrimaryWeaponTarget.Distance / 1000, 0) + "] HasExploded:" + Combat.PreferredPrimaryWeaponTarget.HasExploded + " IsTarget: [" + Combat.PreferredPrimaryWeaponTarget.IsTarget + "] IsTargeting: [" + Combat.PreferredPrimaryWeaponTarget.IsTargeting + "] IsReady [" + Combat.PreferredPrimaryWeaponTarget.IsReadyToTarget + "]", "", Logging.Debug);
                        }

                        if (Combat.PreferredPrimaryWeaponTarget.IsReadyToTarget)
                        {
                            if (Logging.DebugTargetCombatants) Logging.Log("TargetCombatants", "if (Combat.PreferredPrimaryWeaponTarget.IsReadyToTarget)", Logging.Debug);
                            if (Combat.PreferredPrimaryWeaponTarget.Distance <= Combat.MaxRange)
                            {
                                if (Logging.DebugTargetCombatants) Logging.Log("TargetCombatants", "if (Combat.PreferredPrimaryWeaponTarget.Distance <= Combat.MaxRange)", Logging.Debug);
                                //
                                // unlock a lower priority entity if needed
                                //
                                if (highValueTargetsTargeted.Count() >= maxHighValueTargets && maxHighValueTargets > 1)
                                {
                                    if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: we have enough targets targeted [" + QMCache.Instance.TotalTargetsandTargeting.Count() + "]", Logging.Debug);
                                    if (!UnlockLowValueTarget("Combat.TargetCombatants", "PreferredPrimaryWeaponTarget")
                                        || !UnlockHighValueTarget("Combat.TargetCombatants", "PreferredPrimaryWeaponTarget"))
                                    {
                                        return;
                                    }

                                    return;
                                }

                                if (Combat.PreferredPrimaryWeaponTarget.LockTarget("TargetCombatants.PreferredPrimaryWeaponTarget"))
                                {
                                    Logging.Log("Combat", "Targeting preferred primary weapon target [" + Combat.PreferredPrimaryWeaponTarget.Name + "][" + Combat.PreferredPrimaryWeaponTarget.MaskedId + "][" + Math.Round(Combat.PreferredPrimaryWeaponTarget.Distance / 1000, 0) + "k away]", Logging.Teal);
                                    Time.Instance.NextTargetAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.TargetDelay_milliseconds);
                                    if (QMCache.Instance.TotalTargetsandTargeting.Any() && (QMCache.Instance.TotalTargetsandTargeting.Count() >= QMCache.Instance.MaxLockedTargets))
                                    {
                                        Time.Instance.NextTargetAction = DateTime.UtcNow.AddSeconds(Time.Instance.TargetsAreFullDelay_seconds);
                                    }

                                    return;
                                }
                            }

                            return;
                        }
                    }
                }
            }

            #endregion

            //if (Logging.DebugTargetCombatants)
            //{
            //    Logging.Log("Combat.TargetCombatants", "LCOs [" + QMCache.Instance.Entities.Count(i => i.IsLargeCollidable) + "]", Logging.Debug);
            //    if (QMCache.Instance.Entities.Any(i => i.IsLargeCollidable))
            //    {
            //        foreach (EntityCache LCO in QMCache.Instance.Entities.Where(i => i.IsLargeCollidable))
            //        {
            //            Logging.Log("Combat.TargetCombatants", "LCO name [" + LCO.Name + "] Distance [" + Math.Round(LCO.Distance /1000,2) + "] TypeID [" + LCO.TypeId + "] GroupID [" + LCO.GroupId + "]", Logging.Debug);
            //        }
            //    }
            //}


            #region Preferred Drone target handling
            if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: Preferred Drone target handling", Logging.Debug);
            //
            // Lets deal with our preferred targets next (in other words what Q is actively trying to shoot or engage drones on)
            //

            if (Drones.PreferredDroneTarget != null)
            {
                if (Drones.PreferredDroneTarget.IsIgnored)
                {
                    Drones.PreferredDroneTarget = null;
                }

                if (Drones.PreferredDroneTarget != null
                    && QMCache.Instance.EntitiesOnGrid.Any(I => I.Id == Drones.PreferredDroneTarget.Id)
                    && Drones.UseDrones
                    && Drones.PreferredDroneTarget.IsReadyToTarget
                    && Drones.PreferredDroneTarget.Distance < QMCache.Instance.WeaponRange
                    && Drones.PreferredDroneTarget.Nearest5kDistance <= Drones.MaxDroneRange)
                {
                    //
                    // unlock a lower priority entity if needed
                    //
                    if (lowValueTargetsTargeted.Count() >= maxLowValueTargets)
                    {
                        if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: we have enough targets targeted [" + QMCache.Instance.TotalTargetsandTargeting.Count() + "]", Logging.Debug);
                        if (!UnlockLowValueTarget("Combat.TargetCombatants", "PreferredPrimaryWeaponTarget")
                            || !UnlockHighValueTarget("Combat.TargetCombatants", "PreferredPrimaryWeaponTarget"))
                        {
                            return;
                        }

                        return;
                    }

                    if (Drones.PreferredDroneTarget.LockTarget("TargetCombatants.PreferredDroneTarget"))
                    {
                        Logging.Log("Combat", "Targeting preferred drone target [" + Drones.PreferredDroneTarget.Name + "][" + Drones.PreferredDroneTarget.MaskedId + "][" + Math.Round(Drones.PreferredDroneTarget.Distance / 1000, 0) + "k away]", Logging.Teal);
                        //highValueTargets.Add(primaryWeaponPriorityEntity);
                        Time.Instance.NextTargetAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.TargetDelay_milliseconds);
                        if (QMCache.Instance.TotalTargetsandTargeting.Any() && (QMCache.Instance.TotalTargetsandTargeting.Count() >= QMCache.Instance.MaxLockedTargets))
                        {
                            Time.Instance.NextTargetAction = DateTime.UtcNow.AddSeconds(Time.Instance.TargetsAreFullDelay_seconds);
                        }

                        return;
                    }
                }
            }

            #endregion

            #region Do we have enough targets?
            if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: Do we have enough targets? Locked [" + QMCache.Instance.Targets.Count() + "] Locking [" + QMCache.Instance.Targeting.Count() + "] Total [" + QMCache.Instance.TotalTargetsandTargeting.Count() + "] Slots Total [" + QMCache.Instance.MaxLockedTargets + "]", Logging.Debug);
            //
            // OK so now that we are done dealing with preferred and priorities for now, lets see if we can target anything else
            // First lets see if we have enough targets already
            //

            int highValueSlotsreservedForPriorityTargets = 1;
            int lowValueSlotsreservedForPriorityTargets = 1;

            if (QMCache.Instance.MaxLockedTargets <= 4)
            {
                //
                // With a ship/toon combination that has 4 or less slots you really do not have room to reserve 2 slots for priority targets
                //
                highValueSlotsreservedForPriorityTargets = 0;
                lowValueSlotsreservedForPriorityTargets = 0;
            }

            if (maxHighValueTargets <= 2)
            {
                //
                // do not reserve targeting slots if we have none to spare
                //
                highValueSlotsreservedForPriorityTargets = 0;
            }

            if (maxLowValueTargets <= 2)
            {
                //
                // do not reserve targeting slots if we have none to spare
                //
                lowValueSlotsreservedForPriorityTargets = 0;
            }


            if ((highValueTargetsTargeted.Count() >= maxHighValueTargets - highValueSlotsreservedForPriorityTargets)
                && lowValueTargetsTargeted.Count() >= maxLowValueTargets - lowValueSlotsreservedForPriorityTargets)
            {
                if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: we have enough targets targeted [" + QMCache.Instance.TotalTargetsandTargeting.Count() + "] __highValueTargetsTargeted [" + highValueTargetsTargeted.Count() + "] __lowValueTargetsTargeted [" + lowValueTargetsTargeted.Count() + "] maxHighValueTargets [" + maxHighValueTargets + "] maxLowValueTargets [" + maxLowValueTargets + "]", Logging.Debug);
                if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: __highValueTargetsTargeted [" + highValueTargetsTargeted.Count() + "] maxHighValueTargets [" + maxHighValueTargets + "] highValueSlotsreservedForPriorityTargets [" + highValueSlotsreservedForPriorityTargets + "]", Logging.Debug);
                if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: __lowValueTargetsTargeted [" + lowValueTargetsTargeted.Count() + "] maxLowValueTargets [" + maxLowValueTargets + "] lowValueSlotsreservedForPriorityTargets [" + lowValueSlotsreservedForPriorityTargets + "]", Logging.Debug);
                //QMCache.Instance.NextTargetAction = DateTime.UtcNow.AddSeconds(Time.Instance.TargetsAreFullDelay_seconds);
                return;
            }

            if (QMCache.Instance.TotalTargetsandTargetingCount >= QMCache.Instance.MaxLockedTargets)
            {
                if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: we have enough targets targeted... [" + QMCache.Instance.TotalTargetsandTargeting.Count() + "] __highValueTargetsTargeted [" + highValueTargetsTargeted.Count() + "] __lowValueTargetsTargeted [" + lowValueTargetsTargeted.Count() + "] maxHighValueTargets [" + maxHighValueTargets + "] maxLowValueTargets [" + maxLowValueTargets + "]", Logging.Debug);
                return;
            }

            #endregion

            #region Aggro Handling
            if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: Aggro Handling", Logging.Debug);
            //
            // OHHHH We are still here? OK Cool lets deal with things that are already targeting me
            //
            TargetingMe = Combat.TargetedBy.Where(t => t.Distance < (double)Distances.OnGridWithMe
                                                            && t.CategoryId != (int)CategoryID.Asteroid
                                                            && t.IsTargetingMeAndNotYetTargeted
                                                            && (!t.IsSentry || (t.IsSentry && Combat.KillSentries) || (t.IsSentry && t.IsEwarTarget))
                                                            && t.Nearest5kDistance < Combat.MaxRange)
                                                            .ToList();

            List<EntityCache> highValueTargetingMe = TargetingMe.Where(t => (t.IsHighValueTarget))
                                                                .OrderByDescending(t => !t.IsNPCCruiser) //prefer battleships
                                                                .ThenByDescending(t => t.IsBattlecruiser && t.IsLastTargetPrimaryWeaponsWereShooting)
                                                                .ThenByDescending(t => t.IsBattleship && t.IsLastTargetPrimaryWeaponsWereShooting)
                                                                .ThenByDescending(t => t.IsBattlecruiser)
                                                                .ThenByDescending(t => t.IsBattleship)
                                                                .ThenBy(t => t.Nearest5kDistance).ToList();

            int LockedTargetsThatHaveHighValue = QMCache.Instance.Targets.Count(t => (t.IsHighValueTarget));

            List<EntityCache> lowValueTargetingMe = TargetingMe.Where(t => t.IsLowValueTarget)
                                                               .OrderByDescending(t => !t.IsNPCCruiser) //prefer frigates
                                                               .ThenBy(t => t.Nearest5kDistance).ToList();

            int LockedTargetsThatHaveLowValue = QMCache.Instance.Targets.Count(t => (t.IsLowValueTarget));

            if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "TargetingMe [" + TargetingMe.Count() + "] lowValueTargetingMe [" + lowValueTargetingMe.Count() + "] targeted [" + LockedTargetsThatHaveLowValue + "] :::  highValueTargetingMe [" + highValueTargetingMe.Count() + "] targeted [" + LockedTargetsThatHaveHighValue + "] LCOs [" + QMCache.Instance.EntitiesOnGrid.Count(e => e.IsLargeCollidable) + "]", Logging.Debug);

            // High Value
            if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: foreach (EntityCache entity in highValueTargetingMe)", Logging.Debug);

            if (highValueTargetingMe.Any())
            {
                if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: [" + highValueTargetingMe.Count() + "] highValueTargetingMe targets", Logging.Debug);

                int HighValueTargetsTargetedThisCycle = 1;
                foreach (EntityCache highValueTargetingMeEntity in highValueTargetingMe.Where(t => t.IsReadyToTarget && t.Nearest5kDistance < Combat.MaxRange))
                {
                    if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: [" + HighValueTargetsTargetedThisCycle + "][" + highValueTargetingMeEntity.Name + "][" + Math.Round(highValueTargetingMeEntity.Distance / 1000, 2) + "k][groupID" + highValueTargetingMeEntity.GroupId + "]", Logging.Debug);
                    // Have we reached the limit of high value targets?
                    if (highValueTargetsTargeted.Count() >= maxHighValueTargets)
                    {
                        if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: __highValueTargetsTargeted.Count() [" + highValueTargetsTargeted.Count() + "] maxHighValueTargets [" + maxHighValueTargets + "], done for this iteration", Logging.Debug);
                        break;
                    }

                    if (HighValueTargetsTargetedThisCycle >= 4)
                    {
                        if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: HighValueTargetsTargetedThisCycle [" + HighValueTargetsTargetedThisCycle + "], done for this iteration", Logging.Debug);
                        break;
                    }

                    //We need to make sure we do not have too many low value targets filling our slots
                    if (highValueTargetsTargeted.Count() < maxHighValueTargets && lowValueTargetsTargeted.Count() > maxLowValueTargets)
                    {
                        if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: __highValueTargetsTargeted [" + highValueTargetsTargeted.Count() + "] < maxHighValueTargets [" + maxHighValueTargets + "] && __lowValueTargetsTargeted [" + lowValueTargetsTargeted.Count() + "] > maxLowValueTargets [" + maxLowValueTargets + "], try to unlock a low value target, and return.", Logging.Debug);
                        UnlockLowValueTarget("Combat.TargetCombatants", "HighValueTarget");
                        return;
                    }

                    if (highValueTargetingMeEntity != null
                        && highValueTargetingMeEntity.Distance < Combat.MaxRange
                        && highValueTargetingMeEntity.IsReadyToTarget
                        && highValueTargetingMeEntity.IsInOptimalRangeOrNothingElseAvail
                        && !highValueTargetingMeEntity.IsIgnored
                        && highValueTargetingMeEntity.LockTarget("TargetCombatants.HighValueTargetingMeEntity"))
                    {
                        HighValueTargetsTargetedThisCycle++;
                        Logging.Log("Combat", "Targeting high value target [" + highValueTargetingMeEntity.Name + "][" + highValueTargetingMeEntity.MaskedId + "][" + Math.Round(highValueTargetingMeEntity.Distance / 1000, 0) + "k away] highValueTargets.Count [" + highValueTargetsTargeted.Count() + "]", Logging.Teal);
                        Time.Instance.NextTargetAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.TargetDelay_milliseconds);
                        if (QMCache.Instance.TotalTargetsandTargeting.Any() && (QMCache.Instance.TotalTargetsandTargeting.Count() >= QMCache.Instance.MaxLockedTargets))
                        {
                            Time.Instance.NextTargetAction = DateTime.UtcNow.AddSeconds(Time.Instance.TargetsAreFullDelay_seconds);
                        }

                        if (HighValueTargetsTargetedThisCycle > 2)
                        {
                            if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: HighValueTargetsTargetedThisCycle [" + HighValueTargetsTargetedThisCycle + "] > 3, return", Logging.Debug);
                            return;
                        }
                    }

                    continue;
                }

                if (HighValueTargetsTargetedThisCycle > 1)
                {
                    if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: HighValueTargetsTargetedThisCycle [" + HighValueTargetsTargetedThisCycle + "] > 1, return", Logging.Debug);
                    return;
                }
            }
            else
            {
                if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: 0 highValueTargetingMe targets", Logging.Debug);
            }

            // Low Value
            if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: foreach (EntityCache entity in lowValueTargetingMe)", Logging.Debug);

            if (lowValueTargetingMe.Any())
            {
                if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: [" + lowValueTargetingMe.Count() + "] lowValueTargetingMe targets", Logging.Debug);

                int LowValueTargetsTargetedThisCycle = 1;
                foreach (EntityCache lowValueTargetingMeEntity in lowValueTargetingMe.Where(t => !t.IsTarget && !t.IsTargeting && t.Nearest5kDistance < LowValueTargetsHaveToBeWithinDistance).OrderByDescending(i => i.IsLastTargetDronesWereShooting).ThenBy(i => i.IsLastTargetPrimaryWeaponsWereShooting))
                {

                    if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: lowValueTargetingMe [" + LowValueTargetsTargetedThisCycle + "][" + lowValueTargetingMeEntity.Name + "][" + Math.Round(lowValueTargetingMeEntity.Distance / 1000, 2) + "k] groupID [ " + lowValueTargetingMeEntity.GroupId + "]", Logging.Debug);

                    // Have we reached the limit of low value targets?
                    if (lowValueTargetsTargeted.Count() >= maxLowValueTargets)
                    {
                        if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: __lowValueTargetsTargeted.Count() [" + lowValueTargetsTargeted.Count() + "] maxLowValueTargets [" + maxLowValueTargets + "], done for this iteration", Logging.Debug);
                        break;
                    }

                    if (LowValueTargetsTargetedThisCycle >= 3)
                    {
                        if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: LowValueTargetsTargetedThisCycle [" + LowValueTargetsTargetedThisCycle + "], done for this iteration", Logging.Debug);
                        break;
                    }

                    //We need to make sure we do not have too many high value targets filling our slots
                    if (lowValueTargetsTargeted.Count() < maxLowValueTargets && highValueTargetsTargeted.Count() > maxHighValueTargets)
                    {
                        UnlockLowValueTarget("Combat.TargetCombatants", "HighValueTarget");
                        return;
                    }

                    if (lowValueTargetingMeEntity != null
                        && lowValueTargetingMeEntity.Distance < QMCache.Instance.WeaponRange
                        && lowValueTargetingMeEntity.IsReadyToTarget
                        && lowValueTargetingMeEntity.IsInOptimalRangeOrNothingElseAvail
                        && lowValueTargetingMeEntity.Nearest5kDistance < LowValueTargetsHaveToBeWithinDistance
                        && !lowValueTargetingMeEntity.IsIgnored
                        && lowValueTargetingMeEntity.LockTarget("TargetCombatants.LowValueTargetingMeEntity"))
                    {
                        LowValueTargetsTargetedThisCycle++;
                        Logging.Log("Combat", "Targeting low  value target [" + lowValueTargetingMeEntity.Name + "][" + lowValueTargetingMeEntity.MaskedId + "][" + Math.Round(lowValueTargetingMeEntity.Distance / 1000, 0) + "k away] lowValueTargets.Count [" + lowValueTargetsTargeted.Count() + "]", Logging.Teal);
                        //lowValueTargets.Add(lowValueTargetingMeEntity);
                        Time.Instance.NextTargetAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.TargetDelay_milliseconds);
                        if (QMCache.Instance.TotalTargetsandTargeting.Any() && (QMCache.Instance.TotalTargetsandTargeting.Count() >= QMCache.Instance.MaxLockedTargets))
                        {
                            Time.Instance.NextTargetAction = DateTime.UtcNow.AddSeconds(Time.Instance.TargetsAreFullDelay_seconds);
                        }
                        if (LowValueTargetsTargetedThisCycle > 2)
                        {
                            if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: LowValueTargetsTargetedThisCycle [" + LowValueTargetsTargetedThisCycle + "] > 2, return", Logging.Debug);
                            return;
                        }
                    }

                    continue;
                }

                if (LowValueTargetsTargetedThisCycle > 1)
                {
                    if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: if (LowValueTargetsTargetedThisCycle > 1)", Logging.Debug);
                    return;
                }
            }
            else
            {
                if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: 0 lowValueTargetingMe targets", Logging.Debug);
            }

            //
            // If we have 2 PotentialCombatTargets targeted at this point return... we do not want to target anything that is not yet aggressed if we have something aggressed.
            // or are in the middle of attempting to aggro something
            //
            if (Combat.PotentialCombatTargets.Count(e => e.IsTarget) > 1 || (QMCache.Instance.MaxLockedTargets < 2 && QMCache.Instance.Targets.Any()))
            {
                if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: We already have [" + Combat.PotentialCombatTargets.Count(e => e.IsTarget) + "] PotentialCombatTargets Locked. Do not aggress non aggressed NPCs until we have no targets", Logging.Debug);
                return;
            }

            #endregion

            #region All else fails grab an unlocked target that is not yet targeting me
            //
            // Ok, now that that is all handled lets grab the closest non aggressed mob and pew
            // Build a list of things not yet targeting me and not yet targeted
            //

            NotYetTargetingMe = Combat.PotentialCombatTargets.Where(e => e.IsNotYetTargetingMeAndNotYetTargeted)
                                                                        .OrderBy(t => t.Nearest5kDistance)
                                                                        .ToList();

            if (NotYetTargetingMe.Any())
            {
                if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: [" + NotYetTargetingMe.Count() + "] NotYetTargetingMe targets", Logging.Debug);

                foreach (EntityCache TargetThisNotYetAggressiveNPC in NotYetTargetingMe)
                {
                    if (TargetThisNotYetAggressiveNPC != null
                     && TargetThisNotYetAggressiveNPC.IsReadyToTarget
                     && TargetThisNotYetAggressiveNPC.IsInOptimalRangeOrNothingElseAvail
                     && TargetThisNotYetAggressiveNPC.Nearest5kDistance < Combat.MaxRange
                     && !TargetThisNotYetAggressiveNPC.IsIgnored
                     && TargetThisNotYetAggressiveNPC.LockTarget("TargetCombatants.TargetThisNotYetAggressiveNPC"))
                    {
                        Logging.Log("Combat", "Targeting non-aggressed NPC target [" + TargetThisNotYetAggressiveNPC.Name + "][GroupID: " + TargetThisNotYetAggressiveNPC.GroupId + "][TypeID: " + TargetThisNotYetAggressiveNPC.TypeId + "][" + TargetThisNotYetAggressiveNPC.MaskedId + "][" + Math.Round(TargetThisNotYetAggressiveNPC.Distance / 1000, 0) + "k away]", Logging.Teal);
                        Time.Instance.NextTargetAction = DateTime.UtcNow.AddMilliseconds(4000);
                        if (QMCache.Instance.TotalTargetsandTargeting.Any() && (QMCache.Instance.TotalTargetsandTargeting.Count() >= QMCache.Instance.MaxLockedTargets))
                        {
                            Time.Instance.NextTargetAction = DateTime.UtcNow.AddSeconds(Time.Instance.TargetsAreFullDelay_seconds);
                        }

                        return;
                    }
                }
            }
            else
            {
                if (Logging.DebugTargetCombatants) Logging.Log("Combat.TargetCombatants", "DebugTargetCombatants: 0 NotYetTargetingMe targets", Logging.Debug);
            }

            return;
            #endregion
        }

        public static void ProcessState()
        {
            try
            {
                if (DateTime.UtcNow < _lastCombatProcessState.AddMilliseconds(350) || Logging.DebugDisableCombat) //if it has not been 500ms since the last time we ran this ProcessState return. We can't do anything that close together anyway
                {
                    if (Logging.DebugCombat) Logging.Log("Combat.ProcessState", "if (DateTime.UtcNow < _lastCombatProcessState.AddMilliseconds(350) || Logging.DebugDisableCombat)", Logging.Debug);
                    return;
                }

                _lastCombatProcessState = DateTime.UtcNow;

                if (QMCache.Instance.InSpace && QMCache.Instance.InWarp)
                {
                    icount = 0;
                }

                if ((_States.CurrentCombatState != CombatState.Idle ||
                    _States.CurrentCombatState != CombatState.OutOfAmmo) &&
                    (QMCache.Instance.InStation ||// There is really no combat in stations (yet)
                    !QMCache.Instance.InSpace || // if we are not in space yet, wait...
                    QMCache.Instance.ActiveShip.Entity == null || // What? No ship entity?
                    QMCache.Instance.ActiveShip.Entity.IsCloaked))  // There is no combat when cloaked
                {
                    _States.CurrentCombatState = CombatState.Idle;
                    if (Logging.DebugCombat) Logging.Log("Combat.ProcessState", "NotIdle, NotOutOfAmmo and InStation or NotInspace or ActiveShip is null or cloaked", Logging.Debug);
                    return;
                }

                if (QMCache.Instance.InStation)
                {
                    _States.CurrentCombatState = CombatState.Idle;
                    if (Logging.DebugCombat) Logging.Log("Combat.ProcessState", "We are in station, do nothing", Logging.Debug);
                    return;
                }

                try
                {
                    if (!QMCache.Instance.InWarp &&
                        !QMCache.Instance.MyShipEntity.IsFrigate &&
                        !QMCache.Instance.MyShipEntity.IsCruiser &&
                        QMCache.Instance.ActiveShip.GivenName != QMSettings.Instance.SalvageShipName &&
                        QMCache.Instance.ActiveShip.GivenName != QMSettings.Instance.TransportShipName &&
                        QMCache.Instance.ActiveShip.GivenName != QMSettings.Instance.TravelShipName &&
                        QMCache.Instance.ActiveShip.GivenName != QMSettings.Instance.MiningShipName)
                    {
                        //
                        // we are not in something light and fast so assume we need weapons and assume we should be in the defined combatship
                        //
                        if (!QMCache.Instance.Weapons.Any() && QMCache.Instance.InSpace && DateTime.UtcNow > Time.Instance.LastInStation.AddSeconds(30))
                        {
                            Logging.Log("Combat", "Your Current ship [" + QMCache.Instance.ActiveShip.GivenName + "] has no weapons!", Logging.Red);
                            _States.CurrentCombatState = CombatState.OutOfAmmo;
                        }

                        if (QMCache.Instance.ActiveShip.GivenName.ToLower() != CombatShipName.ToLower())
                        {
                            Logging.Log("Combat", "Your Current ship [" + QMCache.Instance.ActiveShip.GivenName + "] GroupID [" + QMCache.Instance.MyShipEntity.GroupId + "] TypeID [" + QMCache.Instance.MyShipEntity.TypeId + "] is not the CombatShipName [" + CombatShipName + "]", Logging.Red);
                            _States.CurrentCombatState = CombatState.OutOfAmmo;
                        }
                    }

                    //
                    // we are in something light and fast so assume we do not need weapons and assume we do not need to be in the defined combatship
                    //
                }
                catch (Exception exception)
                {
                    if (Logging.DebugExceptions) Logging.Log("Combat", "if (!QMCache.Instance.Weapons.Any() && QMCache.Instance.ActiveShip.GivenName == QMSettings.Instance.CombatShipName ) - exception [" + exception + "]", Logging.White);
                }

                switch (_States.CurrentCombatState)
                {
                    case CombatState.CheckTargets:
                        _States.CurrentCombatState = CombatState.KillTargets; //this MUST be before TargetCombatants() or the combat state will potentially get reset (important for the OutOfAmmo state)
                        //if (QMSettings.Instance.TargetSelectionMethod == "isdp")
                        //{
                            TargetCombatants();
                        //}
                        //else //use new target selection method
                        //{
                        //    TargetCombatants2();
                        //}

                        break;

                    case CombatState.KillTargets:

                        _States.CurrentCombatState = CombatState.CheckTargets;

                        if (Logging.DebugPreferredPrimaryWeaponTarget || Logging.DebugKillTargets)
                        {
                            if (QMCache.Instance.Targets.Any())
                            {
                                if (Combat.PreferredPrimaryWeaponTarget != null)
                                {
                                    Logging.Log("Combat.KillTargets", "PreferredPrimaryWeaponTarget [" + Combat.PreferredPrimaryWeaponTarget.Name + "][" + Math.Round(Combat.PreferredPrimaryWeaponTarget.Distance / 1000, 0) + "k][" + Combat.PreferredPrimaryWeaponTarget.MaskedId + "]", Logging.Teal);
                                }
                                else
                                {
                                    Logging.Log("Combat.KillTargets", "PreferredPrimaryWeaponTarget [ null ]", Logging.Teal);
                                }

                                //if (QMCache.Instance.PreferredDroneTarget != null) Logging.Log("Combat.KillTargets", "PreferredPrimaryWeaponTarget [" + QMCache.Instance.PreferredDroneTarget.Name + "][" + Math.Round(QMCache.Instance.PreferredDroneTarget.Distance / 1000, 0) + "k][" + QMCache.Instance.MaskedID(QMCache.Instance.PreferredDroneTargetID) + "]", Logging.Teal);
                            }
                        }

                        //lets at the least make sure we have a fresh entity this frame to check against so we are not trying to navigate to things that no longer exist
                        EntityCache killTarget = null;
                        if (Combat.PreferredPrimaryWeaponTarget != null)
                        {
                            if (QMCache.Instance.Targets.Any(t => t.Id == Combat.PreferredPrimaryWeaponTarget.Id))
                            {
                                killTarget = QMCache.Instance.Targets.FirstOrDefault(t => t.Id == Combat.PreferredPrimaryWeaponTarget.Id && t.Distance < Combat.MaxRange);
                            }
                            else
                            {
                                //Logging.Log("Combat.Killtargets", "Unable to find the PreferredPrimaryWeaponTarget in the Entities list... PPWT.Name[" + Combat.PreferredPrimaryWeaponTarget.Name + "] PPWTID [" + QMCache.Instance.MaskedID(Combat.PreferredPrimaryWeaponTargetID) + "]", Logging.Debug);
                                //Combat.PreferredPrimaryWeaponTarget = null;
                                //QMCache.Instance.NextGetBestCombatTarget = DateTime.UtcNow;
                            }
                        }

                        if (killTarget == null)
                        {
                            if (QMCache.Instance.Targets.Any(i => !i.IsContainer && !i.IsBadIdea))
                            {
                                killTarget = QMCache.Instance.Targets.Where(i => !i.IsContainer && !i.IsBadIdea && i.Distance < Combat.MaxRange).OrderByDescending(i => i.IsInOptimalRange).ThenByDescending(i => i.IsCorrectSizeForMyWeapons).ThenBy(i => i.Distance).FirstOrDefault();
                            }
                        }

                        if (killTarget != null)
                        {
                            if (!QMCache.Instance.InMission || NavigateOnGrid.SpeedTank)
                            {
                                if (Logging.DebugNavigateOnGrid) Logging.Log("Combat.KillTargets", "Navigate Toward the Closest Preferred PWPT", Logging.Debug);
                                NavigateOnGrid.NavigateIntoRange(killTarget, "Combat", QMCache.Instance.normalNav);
                            }

                            if (killTarget.IsReadyToShoot)
                            {
                                icount++;
                                if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "[" + icount + "] Activating Bastion", Logging.Debug);
                                ActivateBastion(false); //by default this will deactivate bastion when needed, but NOT activate it, activation needs activate = true
                                if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "[" + icount + "] Activating Painters", Logging.Debug);
                                ActivateTargetPainters(killTarget);
                                if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "[" + icount + "] Activating Webs", Logging.Debug);
                                ActivateStasisWeb(killTarget);
                                if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "[" + icount + "] Activating WarpDisruptors", Logging.Debug);
                                ActivateWarpDisruptor(killTarget);
                                if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "[" + icount + "] Activating RemoteRepairers", Logging.Debug);
                                ActivateRemoteRepair(killTarget);
                                if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "[" + icount + "] Activating NOS/Neuts", Logging.Debug);
                                ActivateNos(killTarget);
                                if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "[" + icount + "] Activating SensorDampeners", Logging.Debug);
                                ActivateSensorDampeners(killTarget);
                                if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "[" + icount + "] Activating Weapons", Logging.Debug);
                                ActivateWeapons(killTarget);
                                return;
                            }

                            if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "killTarget [" + killTarget.Name + "][" + Math.Round(killTarget.Distance/1000,0) + "k][" + killTarget.MaskedId + "] is not yet ReadyToShoot, LockedTarget [" + killTarget.IsTarget + "] My MaxRange [" + Math.Round(Combat.MaxRange/1000,0) + "]", Logging.Debug);
                            return;
                        }

                        if (Logging.DebugKillTargets) Logging.Log("Combat.KillTargets", "We do not have a killTarget targeted, waiting", Logging.Debug);

                        //ok so we do need this, but only use it if we actually have some potential targets
                        if (Combat.PrimaryWeaponPriorityTargets.Any() || (Combat.PotentialCombatTargets.Any() && QMCache.Instance.Targets.Any() && (!QMCache.Instance.InMission || NavigateOnGrid.SpeedTank)))
                        {
                            //if (QMSettings.Instance.TargetSelectionMethod == "isdp")
                            //{
                                Combat.GetBestPrimaryWeaponTarget(Combat.MaxRange, false, "Combat");
                            //}
                            //else //use new target selection method
                            //{
                            //    QMCache.Instance.__GetBestWeaponTargets(QMCache.Instance.MaxDroneRange);
                            //}

                            icount = 0;
                        }

                        break;

                    case CombatState.OutOfAmmo:
                        break;

                    case CombatState.Idle:

                        //
                        // below is the reasons we will start the combat state(s) - if the below is not met do nothing
                        //
                        //Logging.Log("QMCache.Instance.InSpace: " + QMCache.Instance.InSpace);
                        //Logging.Log("QMCache.Instance.ActiveShip.Entity.IsCloaked: " + QMCache.Instance.ActiveShip.Entity.IsCloaked);
                        //Logging.Log("QMCache.Instance.ActiveShip.GivenName.ToLower(): " + QMCache.Instance.ActiveShip.GivenName.ToLower());
                        //Logging.Log("QMCache.Instance.InSpace: " + QMCache.Instance.InSpace);
                        if (QMCache.Instance.InSpace && //we are in space (as opposed to being in station or in limbo between systems when jumping)
                            (QMCache.Instance.ActiveShip.Entity != null &&  // we are in a ship!
                            !QMCache.Instance.ActiveShip.Entity.IsCloaked && //we are not cloaked anymore
                            QMCache.Instance.ActiveShip.GivenName.ToLower() == CombatShipName.ToLower() && //we are in our combat ship
                            !QMCache.Instance.InWarp)) // no longer in warp
                        {
                            _States.CurrentCombatState = CombatState.CheckTargets;
                            if (Logging.DebugCombat) Logging.Log("Combat.ProcessState", "We are in space and ActiveShip is null or Cloaked or we arent in the combatship or we are in warp", Logging.Debug);
                            return;
                        }
                        break;

                    default:

                        // Next state
                        Logging.Log("Combat", "CurrentCombatState was not set thus ended up at default", Logging.Orange);
                        _States.CurrentCombatState = CombatState.CheckTargets;
                        break;
                }
            }
            catch (Exception exception)
            {
                Logging.Log("Combat.ProcessState", "Exception [" + exception + "]", Logging.Debug);
            }
        }

        ///
        ///   Invalidate the cached items every pulse (called from cache.invalidatecache, which itself is called every frame in questor.cs)
        ///
        public static void InvalidateCache()
        {
            try
            {
                //
                // this list of variables is cleared every pulse.
                //
                _aggressed = null;
                _combatTargets = null;
                _maxrange = null;
                _maxTargetRange = null;
                _potentialCombatTargets = null;
                _primaryWeaponPriorityTargetsPerFrameCaching = null;
                _targetedBy = null;

                _primaryWeaponPriorityEntities = null;
                _preferredPrimaryWeaponTarget = null;

                if (_primaryWeaponPriorityTargets != null && _primaryWeaponPriorityTargets.Any())
                {
                    _primaryWeaponPriorityTargets.ForEach(pt => pt.ClearCache());
                }
            }
            catch (Exception exception)
            {
                Logging.Log("Combat.InvalidateCache", "Exception [" + exception + "]", Logging.Debug);
            }
        }
    }
}
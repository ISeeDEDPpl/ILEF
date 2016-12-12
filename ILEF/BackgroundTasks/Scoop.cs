﻿// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------

namespace Questor.Modules.BackgroundTasks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DirectEve;
    using global::Questor.Modules.Caching;
    using global::Questor.Modules.Logging;
    using global::Questor.Modules.Lookup;
    using global::Questor.Modules.States;

    public class Scoop
    {
        public static HashSet<int> Salvagers = new HashSet<int> { 25861, 26983, 30836 };
        public static HashSet<int> TractorBeams = new HashSet<int> { 24348, 24620, 24622, 24644 };

        private DateTime _lastJettison = DateTime.UtcNow;
        private DateTime _nextAction;

        /// <summary>
        ///   Keep a list of times that we have tried to open a container (do not try to open the same container twice within 10 seconds)
        /// </summary>
        private readonly Dictionary<long, DateTime> _openedContainers;

        public Scoop()
        {
            _openedContainers = new Dictionary<long, DateTime>();
        }

        public int MaximumWreckTargets { get; set; }

        public int ReserveCargoCapacity { get; set; }

        public List<Ammo> Ammo { get; set; }

        /// <summary>
        ///   Activate salvagers on targeted wreck
        /// </summary>
        private void ActivateSalvagers()
        {
            // We are not in space yet, wait...
            if (!Cache.Instance.InSpace)
                return;

            List<ModuleCache> salvagers = Cache.Instance.Modules.Where(m => Salvagers.Contains(m.TypeId)).ToList();
            if (salvagers.Count == 0)
                return;

            double salvagerRange = salvagers.Min(s => s.OptimalRange);

            List<EntityCache> wrecks = Cache.Instance.Targets.Where(t => t.GroupId == (int)Group.Wreck && t.Distance < salvagerRange).ToList();
            if (wrecks.Count == 0)
                return;

            foreach (ModuleCache salvager in salvagers)
            {
                if (salvager.IsActive || salvager.IsDeactivating)
                    continue;

                // Spread the salvagers around
                EntityCache wreck = wrecks.OrderBy(w => salvagers.Count(s => s.LastTargetId == w.Id)).FirstOrDefault();
                if (wreck == null)
                    return;

                Logging.Log("Salvage", "Activating salvager [" + salvager.ItemId + "] on [" + wreck.Name + "][ID: " + wreck.MaskedId + "]", Logging.White);
                salvager.Activate(wreck);
            }
        }

        /// <summary>
        ///   Target Hostile (no loot rights) wrecks within range
        /// </summary>
        private void TargetHostileWrecks()
        {
            // We are not in space yet, wait...
            if (!Cache.Instance.InSpace)
                return;

            // We are jammed, we do not need to log (Combat does this already)
            if (Cache.Instance.MaxLockedTargets == 0)
                return;

            List<EntityCache> targets = new List<EntityCache>();
            targets.AddRange(Cache.Instance.Targets);
            targets.AddRange(Cache.Instance.Targeting);

            bool hasSalvagers = Cache.Instance.Modules.Any(m => Salvagers.Contains(m.TypeId));
            List<EntityCache> wreckTargets = targets.Where(t => (t.GroupId == (int)Group.Wreck) && t.CategoryId == (int)CategoryID.Celestial).ToList();

            // Check for cargo containers
            foreach (EntityCache wreck in wreckTargets)
            {
                if (wreck.IsIgnored)
                {
                    Logging.Log("Salvage", "Cargo Container [" + wreck.Name + "][ID: " + wreck.MaskedId + "] on the ignore list, ignoring.", Logging.White);
                    //wreck.UnlockTarget();
                    continue;
                }

                if (hasSalvagers && wreck.GroupId != (int)Group.CargoContainer)
                    continue;

                // Unlock if within loot range - - - - is this needed or wanted in this mode?!
                //if (wreck.Distance < (int)Distance.SafeScoopRange)
                //{
                //    Logging.Log("Salvage: Cargo Container [" + wreck.Name + "][" + wreck.Id + "] within loot range, unlocking container.");
                //    wreck.UnlockTarget();
                //}
            }

            if (wreckTargets.Count >= MaximumWreckTargets)
                return;

            List<ModuleCache> tractorBeams = Cache.Instance.Modules.Where(m => TractorBeams.Contains(m.TypeId)).ToList();
            double tractorBeamRange = 0d;
            if (tractorBeams.Count > 0)
                tractorBeamRange = tractorBeams.Min(t => t.OptimalRange);

            IEnumerable<EntityCache> wrecks = Cache.Instance.UnlootedContainers;
            foreach (EntityCache wreck in wrecks.Where(w => !w.IsIgnored))
            {
                // Its already a target, ignore it
                if (wreck.IsTarget || wreck.IsTargeting)
                    continue;

                if (wreck.Distance > tractorBeamRange)
                    continue;

                //if (!wreck.HaveLootRights)
                //    continue;

                // No need to tractor a non-wreck within loot range
                if (wreck.GroupId != (int)Group.Wreck && wreck.Distance < (int)Distances.SafeScoopRange)
                    continue;

                if (wreck.GroupId != (int)Group.Wreck && wreck.GroupId != (int)Group.CargoContainer)
                    continue;

                if (!hasSalvagers)
                {
                    // Ignore already looted wreck
                    if (Cache.Instance.LootedContainers.Contains(wreck.Id))
                        continue;

                    // Ignore empty wrecks
                    if (wreck.IsWreckEmpty) //this only returns true if it is a wreck, not for cargo containers, spawn containers, etc.
                        continue;
                }

                Logging.Log("Salvage", "Locking [" + wreck.Name + "][ID: " + wreck.MaskedId + "][" + Math.Round(wreck.Distance / 1000, 0) + "k away]", Logging.White);

                wreck.LockTarget("Salvage");
                wreckTargets.Add(wreck);

                if (wreckTargets.Count >= MaximumWreckTargets)
                    break;
            }
        }

        /// <summary>
        ///   Loot any wrecks close by
        /// </summary>
        private void LootHostileWrecks()
        {
            // We are not in space yet, wait...
            if (!Cache.Instance.InSpace)
                return;

            if (Cache.Instance.CurrentShipsCargo == null) return;

            List<ItemCache> shipsCargo = Cache.Instance.CurrentShipsCargo.Items.Select(i => new ItemCache(i)).ToList();
            double freeCargoCapacity = Cache.Instance.CurrentShipsCargo.Capacity - Cache.Instance.CurrentShipsCargo.UsedCapacity;
            IEnumerable<DirectContainerWindow> lootWindows = Cache.Instance.Windows.OfType<DirectContainerWindow>().Where(w => !string.IsNullOrEmpty(w.Name) && w.Name.StartsWith("loot_")).ToList();
            foreach (DirectContainerWindow window in lootWindows)
            {
                // The window is not ready, then continue
                if (!window.IsReady)
                    continue;

                // Get the container
                EntityCache containerEntity = Cache.Instance.EntityById(window.ItemId);

                // Does it no longer exist or is it out of transfer range or its looted
                if (containerEntity == null || containerEntity.Distance > (int)Distances.SafeScoopRange || Cache.Instance.LootedContainers.Contains(containerEntity.Id))
                {
                    Logging.Log("Salvage", "Closing loot window [" + window.ItemId + "]", Logging.White);
                    window.Close();
                    continue;
                }

                // Get the container that is associated with the cargo container
                DirectContainer container = Cache.Instance.DirectEve.GetContainer(window.ItemId);

                // List its items
                IEnumerable<ItemCache> items = container.Items.Select(i => new ItemCache(i));

                // Build a list of items to loot
                List<ItemCache> lootItems = new List<ItemCache>();

                //can we loot all items in one go here?
                //
                // bulk loot code would go here
                //

                // Walk through the list of items ordered by highest value item first
                foreach (ItemCache item in items) //.OrderByDescending(i => i.IskPerM3))
                {
                    // We never want to pick up Bookmarks
                    if (item.IsBookmark)
                        continue;

                    // We never want to pick up a cap booster
                    if (item.GroupID == (int)Group.CapacitorGroupCharge)
                        continue;

                    // We never want to pick up metal scraps
                    if (item.IsScrapMetal)
                        continue;

                    // We never want to pick up Ore...
                    if (item.IsOre)
                        continue;

                    // We never want to pick up Low End Minerals
                    if (item.IsLowEndMineral)
                        continue;

                    // Never pick up contraband
                    if (item.IsContraband)
                        continue;

                    // We pick up loot depending on isk per m3
                    bool isMissionItem = MissionSettings.MissionItems.Contains((item.Name ?? string.Empty).ToLower());

                    // We are at our max, either make room or skip the item
                    if ((freeCargoCapacity - item.TotalVolume) <= (isMissionItem ? 0 : ReserveCargoCapacity))
                    {
                        // We can't drop items in this container anyway, well get it after its salvaged
                        if (!isMissionItem && containerEntity.GroupId != (int)Group.CargoContainer)
                            continue;

                        // Make a list of items which are worth less
                        List<ItemCache> worthLess;
                        if (item.IskPerM3.HasValue)
                            worthLess = shipsCargo.Where(sc => sc.IskPerM3.HasValue && sc.IskPerM3 < item.IskPerM3).ToList();
                        else
                            worthLess = shipsCargo.Where(sc => sc.IskPerM3.HasValue).ToList();

                        // Remove mission item from this list
                        worthLess.RemoveAll(wl => MissionSettings.MissionItems.Contains((wl.Name ?? string.Empty).ToLower()));
                        worthLess.RemoveAll(wl => (wl.Name ?? string.Empty).ToLower() == MissionSettings.BringMissionItem.ToLower());

                        // Nothing is worth less then the current item
                        if (!worthLess.Any())
                            continue;

                        // Not enough space even if we dumped the crap
                        if ((freeCargoCapacity + worthLess.Sum(wl => wl.TotalVolume)) < item.TotalVolume)
                        {
                            if (isMissionItem)
                                Logging.Log("Scoop", "Not enough space for [" + item.Name + "] Need [" + item.TotalVolume + "]m3 - maximum available [" + (freeCargoCapacity + worthLess.Sum(wl => wl.TotalVolume)) + "]m3", Logging.White);

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
                                break;
                        }

                        if (moveTheseItems.Count > 0)
                        {
                            // If this is not a cargo container, then jettison loot
                            if (containerEntity.GroupId != (int)Group.CargoContainer)
                            {
                                if (DateTime.UtcNow.Subtract(_lastJettison).TotalSeconds < 185)
                                    return;

                                Logging.Log("Scoop", "Jettisoning [" + moveTheseItems.Count + "] items to make room for the more valuable loot", Logging.White);

                                // Note: This could (in theory) fuck up with the bot jettison an item and
                                // then picking it up again :/ (granted it should never happen unless
                                // mission item volume > reserved volume
                                Cache.Instance.CurrentShipsCargo.Jettison(moveTheseItems.Select(i => i.ItemId));
                                _lastJettison = DateTime.UtcNow;
                                return;
                            }

                            // Move items to the cargo container
                            container.Add(moveTheseItems);

                            // Remove it from the ships cargo list
                            shipsCargo.RemoveAll(i => moveTheseItems.Any(wl => wl.ItemId == i.Id));
                            Logging.Log("Scoop", "Moving [" + moveTheseItems.Count + "] items into the cargo container to make room for the more valuable loot", Logging.White);
                        }
                    }

                    // Update free space
                    freeCargoCapacity -= item.TotalVolume;
                    lootItems.Add(item);
                }

                // Mark container as looted
                Cache.Instance.LootedContainers.Add(containerEntity.Id);

                // Loot actual items
                if (lootItems.Count != 0)
                {
                    Cache.Instance.CurrentShipsCargo.Add(lootItems.Select(i => i.DirectItem));
                    //Logging.Log("Scoop: Looting container [" + containerEntity.Name + "][" + containerEntity.Id + "], [" + lootItems.Count + "] valuable items");
                }
                else
                    Logging.Log("Scoop", "Container [" + containerEntity.Name + "][ID: " + containerEntity.MaskedId + "] contained no valuable items", Logging.White);
            }

            // Open a container in range
            foreach (EntityCache containerEntity in Cache.Instance.UnlootedWrecksAndSecureCans.Where(e => e.Distance <= (int)Distances.SafeScoopRange))
            {
                // Empty wreck, ignore
                if (containerEntity.IsWreckEmpty) //this only returns true if it is a wreck, not for cargo containers, spawn containers, etc.
                    continue;

                // We looted this container
                if (Cache.Instance.LootedContainers.Contains(containerEntity.Id))
                    continue;

                // We already opened the loot window
                DirectContainerWindow window = lootWindows.FirstOrDefault(w => w.ItemId == containerEntity.Id);
                if (window != null)
                    continue;

                // Ignore open request within 10 seconds
                if (_openedContainers.ContainsKey(containerEntity.Id) && DateTime.UtcNow.Subtract(_openedContainers[containerEntity.Id]).TotalSeconds < 10)
                    continue;

                // Open the container
                if (containerEntity.OpenCargo())
                {
                    Logging.Log("Scoop", "Opening container [" + containerEntity.Name + "][ID: " + containerEntity.MaskedId + "]", Logging.White);
                    _openedContainers[containerEntity.Id] = DateTime.UtcNow;    
                }
                
                break;
            }
        }

        public void ProcessState()
        {
            // Nothing to salvage in stations
            if (Cache.Instance.InStation)
                return;

            switch (_States.CurrentScoopState)
            {
                case ScoopState.TargetHostileWrecks:
                    //TargetHostileWrecks();

                    // Next state
                    _States.CurrentScoopState = ScoopState.LootHostileWrecks;
                    break;

                case ScoopState.LootHostileWrecks:
                    LootHostileWrecks();

                    //State = ScoopState.SalvageHostileWrecks;
                    break;

                case ScoopState.SalvageHostileWrecks:
                    ActivateSalvagers();

                    // Default action
                    _States.CurrentScoopState = ScoopState.TargetHostileWrecks;
                    //if (cargo.Window.IsReady && cargo.Items.Any() && _nextAction < DateTime.UtcNow)
                    //{
                    // Check if there are actually duplicates
                    //    var duplicates = cargo.Items.Where(i => i.Quantity > 0).GroupBy(i => i.TypeId).Any(t => t.Count() > 1);
                    //    if (duplicates)
                    //        State = SalvageState.StackItems;
                    //    else
                    //        _nextAction = DateTime.UtcNow.AddSeconds(150);
                    //}
                    break;

                case ScoopState.StackItemsWhileAggressed:
                    Logging.Log("Salvage", "Stacking items", Logging.White);

                    if (Cache.Instance.CurrentShipsCargo != null && (Cache.Instance.CurrentShipsCargo.Window.IsReady))
                        Cache.Instance.CurrentShipsCargo.StackAll();

                    _nextAction = DateTime.UtcNow.AddSeconds(5);
                    _States.CurrentScoopState = ScoopState.WaitForStackingWhileAggressed;
                    break;

                case ScoopState.WaitForStackingWhileAggressed:
                    // Wait 5 seconds after stacking
                    if (_nextAction > DateTime.UtcNow)
                        break;

                    if (Cache.Instance.DirectEve.GetLockedItems().Count == 0)
                    {
                        Logging.Log("Salvage", "Done stacking", Logging.White);
                        _States.CurrentScoopState = ScoopState.TargetHostileWrecks;
                        break;
                    }

                    if (DateTime.UtcNow.Subtract(_nextAction).TotalSeconds > 120)
                    {
                        Logging.Log("Salvage", "Stacking items timed out, clearing item locks", Logging.Orange);
                        Cache.Instance.DirectEve.UnlockItems();

                        Logging.Log("Salvage", "Done stacking", Logging.White);
                        _States.CurrentScoopState = ScoopState.TargetHostileWrecks;
                        break;
                    }
                    break;

                case ScoopState.Error:

                    // Wait indefinitely
                    break;

                default:
                    // Unknown state, goto first state
                    _States.CurrentScoopState = ScoopState.LootHostileWrecks;
                    break;
            }
        }
    }
}

namespace ILEF.Activities
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Globalization;
    using System.Linq;
    using System.Xml.Linq;
    using ILoveEVE.Framework;
    using global::ILEF.Actions;
    using global::ILEF.BackgroundTasks;
    using global::ILEF.Combat;
    using global::ILEF.Caching;
    using global::ILEF.Logging;
    using global::ILEF.Lookup;
    using global::ILEF.States;

    public class CombatMissionCtrl
    {
        private DateTime? _clearPocketTimeout;
        private static int _currentAction;

        private double _lastX;
        private double _lastY;
        private double _lastZ;
        private static List<Actions.Action> _pocketActions;
        private bool _waiting;
        private DateTime _waitingSince;
        private DateTime _moveToNextPocket = DateTime.UtcNow.AddHours(10);
        private DateTime _nextCombatMissionCtrlAction = DateTime.UtcNow;
        private int AttemptsToActivateGateTimer;
        private int AttemptsToGetAwayFromGate;
        private bool ItemsHaveBeenMoved;
        private bool CargoHoldHasBeenStacked;
        public static bool DeactivateIfNothingTargetedWithinRange;
        /// <summary>
        ///   List of targets to ignore
        /// </summary>
        public static HashSet<string> IgnoreTargets { get; private set; }

        public CombatMissionCtrl()
        {
            _pocketActions = new List<Actions.Action>();
            IgnoreTargets = new HashSet<string>();
        }

        //public string Mission { get; set; }
        public static int PocketNumber { get; set; }

        private void Nextaction()
        {
            // make sure all approach / orbit / align timers are reset (why cant we wait them out in the next action!?)
            Time.Instance.NextApproachAction = DateTime.UtcNow;
            Time.Instance.NextOrbit = DateTime.UtcNow;
            Time.Instance.NextAlign = DateTime.UtcNow;

            // now that we have completed this action revert OpenWrecks to false
            if (NavigateOnGrid.SpeedTank) Salvage.OpenWrecks = false;
            Salvage.MissionLoot = false;
            QMCache.Instance.normalNav = true;
            QMCache.Instance.onlyKillAggro = false;
            MissionSettings.MissionActivateRepairModulesAtThisPerc = null;
            MissionSettings.PocketUseDrones = null;
            ItemsHaveBeenMoved = false;
            CargoHoldHasBeenStacked = false;
            _currentAction++;
            return;
        }

        private bool BookmarkPocketForSalvaging()
        {
            if (Logging.DebugSalvage) Logging.Log("BookmarkPocketForSalvaging", "Entered: BookmarkPocketForSalvaging", Logging.Debug);
            double RangeToConsiderWrecksDuringLootAll;
            List<ModuleCache> tractorBeams = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.TractorBeam).ToList();
            if (tractorBeams.Count > 0)
            {
                RangeToConsiderWrecksDuringLootAll = Math.Min(tractorBeams.Min(t => t.OptimalRange), QMCache.Instance.ActiveShip.MaxTargetRange);
            }
            else
            {
                RangeToConsiderWrecksDuringLootAll = 1500;
            }

            if ((Salvage.LootEverything || Salvage.LootOnlyWhatYouCanWithoutSlowingDownMissionCompletion) && QMCache.Instance.UnlootedContainers.Count(i => i.Distance < RangeToConsiderWrecksDuringLootAll) > Salvage.MinimumWreckCount)
            {
                if (Logging.DebugSalvage) Logging.Log("BookmarkPocketForSalvaging", "LootEverything [" + Salvage.LootEverything + "] UnLootedContainers [" + QMCache.Instance.UnlootedContainers.Count() + "LootedContainers [" + QMCache.Instance.LootedContainers.Count() + "] MinimumWreckCount [" + Salvage.MinimumWreckCount + "] We will wait until everything in range is looted.", Logging.Debug);

                if (QMCache.Instance.UnlootedContainers.Count(i => i.Distance < RangeToConsiderWrecksDuringLootAll) > 0)
                {
                    if (Logging.DebugSalvage) Logging.Log("BookmarkPocketForSalvaging", "if (QMCache.Instance.UnlootedContainers.Count [" + QMCache.Instance.UnlootedContainers.Count(i => i.Distance < RangeToConsiderWrecksDuringLootAll) + "] (w => w.Distance <= RangeToConsiderWrecksDuringLootAll [" + RangeToConsiderWrecksDuringLootAll + "]) > 0)", Logging.Debug);
                    return false;
                }

                if (Logging.DebugSalvage) Logging.Log("BookmarkPocketForSalvaging", "LootEverything [" + Salvage.LootEverything + "] We have LootEverything set to on. We cant have any need for the pocket bookmarks... can we?!", Logging.Debug);
                return true;
            }

            if (Salvage.CreateSalvageBookmarks)
            {
                if (Logging.DebugSalvage) Logging.Log("BookmarkPocketForSalvaging", "CreateSalvageBookmarks [" + Salvage.CreateSalvageBookmarks + "]", Logging.Debug);

                if (MissionSettings.ThisMissionIsNotWorthSalvaging())
                {
                    Logging.Log("BookmarkPocketForSalvaging", "[" + MissionSettings.MissionName + "] is a mission not worth salvaging, skipping salvage bookmark creation", Logging.Debug);
                    return true;
                }

                // Nothing to loot
                if (QMCache.Instance.UnlootedContainers.Count() < Salvage.MinimumWreckCount)
                {
                    if (Logging.DebugSalvage) Logging.Log("BookmarkPocketForSalvaging", "LootEverything [" + Salvage.LootEverything + "] UnlootedContainers [" + QMCache.Instance.UnlootedContainers.Count() + "] MinimumWreckCount [" + Salvage.MinimumWreckCount + "] We will wait until everything in range is looted.", Logging.Debug);
                    // If QMSettings.Instance.LootEverything is false we may leave behind a lot of unlooted containers.
                    // This scenario only happens when all wrecks are within tractor range and you have a salvager
                    // ( typically only with a Golem ).  Check to see if there are any cargo containers in space.  Cap
                    // boosters may cause an unneeded salvage trip but that is better than leaving millions in loot behind.
                    if (DateTime.UtcNow > Time.Instance.NextBookmarkPocketAttempt)
                    {
                        Time.Instance.NextBookmarkPocketAttempt = DateTime.UtcNow.AddSeconds(Time.Instance.BookmarkPocketRetryDelay_seconds);
                        if (!Salvage.LootEverything && QMCache.Instance.Containers.Count() < Salvage.MinimumWreckCount)
                        {
                            Logging.Log("CombatMissionCtrl", "No bookmark created because the pocket has [" + QMCache.Instance.Containers.Count() + "] wrecks/containers and the minimum is [" + Salvage.MinimumWreckCount + "]", Logging.Teal);
                            return true;
                        }

                        Logging.Log("CombatMissionCtrl", "No bookmark created because the pocket has [" + QMCache.Instance.UnlootedContainers.Count() + "] wrecks/containers and the minimum is [" + Salvage.MinimumWreckCount + "]", Logging.Teal);
                        return true;
                    }

                    if (Logging.DebugSalvage) Logging.Log("BookmarkPocketForSalvaging", "QMCache.Instance.NextBookmarkPocketAttempt is in [" + Time.Instance.NextBookmarkPocketAttempt.Subtract(DateTime.UtcNow).TotalSeconds + "sec] waiting", Logging.Debug);
                    return false;
                }

                // Do we already have a bookmark?
                List<DirectBookmark> bookmarks = QMCache.Instance.BookmarksByLabel(QMSettings.Instance.BookmarkPrefix + " ");
                if (bookmarks != null && bookmarks.Any())
                {
                    DirectBookmark bookmark = bookmarks.FirstOrDefault(b => QMCache.Instance.DistanceFromMe(b.X ?? 0, b.Y ?? 0, b.Z ?? 0) < (int)Distances.OnGridWithMe);
                    if (bookmark != null)
                    {
                        Logging.Log("CombatMissionCtrl", "salvaging bookmark for this pocket is done [" + bookmark.Title + "]", Logging.Teal);
                        return true;
                    }

                    //
                    // if we have bookmarks but there is no bookmark on grid we need to continue and create the salvage bookmark.
                    //
                }

                // No, create a bookmark
                string label = string.Format("{0} {1:HHmm}", QMSettings.Instance.BookmarkPrefix, DateTime.UtcNow);
                Logging.Log("CombatMissionCtrl", "Bookmarking pocket for salvaging [" + label + "]", Logging.Teal);
                QMCache.Instance.CreateBookmark(label);
                return true;
            }

            return true;
        }

        private void DoneAction()
        {
            // Tell the drones module to retract drones
            Drones.IsMissionPocketDone = true;
            MissionSettings.MissionUseDrones = null;

            if (Drones.ActiveDrones.Any())
            {
                if (Logging.DebugDoneAction) Logging.Log("CombatMissionCtrl.Done", "We still have drones out! Wait for them to return.", Logging.Debug);
                return;
            }

            // Add bookmark (before we're done)
            if (Salvage.CreateSalvageBookmarks)
            {
                if (!BookmarkPocketForSalvaging())
                {
                    if (Logging.DebugDoneAction) Logging.Log("CombatMissionCtrl.Done", "Wait for CreateSalvageBookmarks to return true (it just returned false!)", Logging.Debug);
                    return;
                }
            }

            //
            // we are ready and can set the "done" State.
            //
            Salvage.CurrentlyShouldBeSalvaging = false;
            _States.CurrentCombatMissionCtrlState = CombatMissionCtrlState.Done;
            if (Logging.DebugDoneAction) Logging.Log("CombatMissionCtrl.Done", "we are ready and have set [ _States.CurrentCombatMissionCtrlState = CombatMissionCtrlState.Done ]", Logging.Debug);
            return;
        }

        private void LogWhatIsOnGridAction(Actions.Action action)
        {

            Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Log Entities on Grid.", Logging.Teal);
            if (!Statistics.EntityStatistics(QMCache.Instance.EntitiesOnGrid)) return;
            Nextaction();
            return;
        }

        private void ActivateAction(Actions.Action action)
        {
            if (DateTime.UtcNow < _nextCombatMissionCtrlAction)
                return;

            //we cant move in bastion mode, do not try
            List<ModuleCache> bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
            if (bastionModules.Any(i => i.IsActive))
            {
                Logging.Log("CombatMissionCtrl.Activate", "BastionMode is active, we cannot move, aborting attempt to Activate until bastion deactivates", Logging.Debug);
                _nextCombatMissionCtrlAction = DateTime.UtcNow.AddSeconds(15);
                return;
            }

            bool optional;
            if (!bool.TryParse(action.GetParameterValue("optional"), out optional))
            {
                optional = false;
            }

            string target = action.GetParameterValue("target");

            // No parameter? Although we should not really allow it, assume its the acceleration gate :)
            if (string.IsNullOrEmpty(target))
            {
                target = "Acceleration Gate";
            }

            IEnumerable<EntityCache> targets = QMCache.Instance.EntitiesByName(target, QMCache.Instance.EntitiesOnGrid.Where(i => i.Distance < (int)Distances.OnGridWithMe)).ToList();
            if (!targets.Any())
            {
                if (!_waiting)
                {
                    Logging.Log("CombatMissionCtrl", "Activate: Can't find [" + target + "] to activate! Waiting 30 seconds before giving up", Logging.Teal);
                    _waitingSince = DateTime.UtcNow;
                    _waiting = true;
                }
                else if (_waiting)
                {
                    if (DateTime.UtcNow.Subtract(_waitingSince).TotalSeconds > Time.Instance.NoGateFoundRetryDelay_seconds)
                    {
                        Logging.Log("CombatMissionCtrl",
                                    "Activate: After 30 seconds of waiting the gate is still not on grid: CombatMissionCtrlState.Error",
                                    Logging.Teal);
                        if (optional) //if this action has the optional parameter defined as true then we are done if we cant find the gate
                        {
                            DoneAction();
                        }
                        else
                        {
                            _States.CurrentCombatMissionCtrlState = CombatMissionCtrlState.Error;
                        }
                    }
                }
                return;
            }

            //if (closest.Distance <= (int)Distance.CloseToGateActivationRange) // if your distance is less than the 'close enough' range, default is 7000 meters
            EntityCache closest = targets.OrderBy(t => t.Distance).FirstOrDefault();

            if (closest != null)
            {
                if (closest.Distance <= (int)Distances.GateActivationRange)
                {
                    if (Logging.DebugActivateGate) Logging.Log("CombatMissionCtrl", "if (closest.Distance [" + closest.Distance + "] <= (int)Distances.GateActivationRange [" + (int)Distances.GateActivationRange + "])", Logging.Green);

                    // Tell the drones module to retract drones
                    Drones.IsMissionPocketDone = true;

                    // We cant activate if we have drones out
                    if (Drones.ActiveDrones.Any())
                    {
                        if (Logging.DebugActivateGate) Logging.Log("CombatMissionCtrl", "if (QMCache.Instance.ActiveDrones.Any())", Logging.Green);
                        return;
                    }

                    //
                    // this is a bad idea for a speed tank, we ought to somehow cache the object they are orbiting/approaching, etc
                    // this seemingly slowed down the exit from certain missions for me for 2-3min as it had a command to orbit some random object
                    // after the "done" command
                    //
                    if (closest.Distance < -10100)
                    {
                        if (Logging.DebugActivateGate) Logging.Log("CombatMissionCtrl", "if (closest.Distance < -10100)", Logging.Green);

                        AttemptsToGetAwayFromGate++;
                        if (AttemptsToGetAwayFromGate > 30)
                        {
                            if (closest.Orbit(1000))
                            {
                                Logging.Log("CombatMissionCtrl", "Activate: We are too close to [" + closest.Name + "] Initiating orbit", Logging.Orange);
                                return;
                            }

                            return;
                        }
                    }

                    if (Logging.DebugActivateGate) Logging.Log("CombatMissionCtrl", "if (closest.Distance >= -10100)", Logging.Green);

                    // Add bookmark (before we activate)
                    if (Salvage.CreateSalvageBookmarks)
                    {
                        BookmarkPocketForSalvaging();
                    }

                    if (Logging.DebugActivateGate) Logging.Log("CombatMissionCtrl", "Activate: Reload before moving to next pocket", Logging.Teal);
                    if (!Combat.ReloadAll(QMCache.Instance.MyShipEntity, true)) return;
                    if (Logging.DebugActivateGate) Logging.Log("CombatMissionCtrl", "Activate: Done reloading", Logging.Teal);
                    AttemptsToActivateGateTimer++;

                    if (DateTime.UtcNow > Time.Instance.NextActivateAction || AttemptsToActivateGateTimer > 30)
                    {
                        if (closest.Activate())
                        {
                            Logging.Log("CombatMissionCtrl", "Activate: [" + closest.Name + "] Move to next pocket after reload command and change state to 'NextPocket'", Logging.Green);
                            AttemptsToActivateGateTimer = 0;
                            // Do not change actions, if NextPocket gets a timeout (>2 mins) then it reverts to the last action
                            _moveToNextPocket = DateTime.UtcNow;
                            _States.CurrentCombatMissionCtrlState = CombatMissionCtrlState.NextPocket;
                        }
                    }

                    if (Logging.DebugActivateGate) Logging.Log("CombatMissionCtrl", "------------------", Logging.Green);
                    return;
                }

                AttemptsToActivateGateTimer = 0;
                AttemptsToGetAwayFromGate = 0;

                if (closest.Distance < (int)Distances.WarptoDistance) //else if (closest.Distance < (int)Distances.WarptoDistance) //if we are inside warpto distance then approach
                {
                    if (Logging.DebugActivateGate) Logging.Log("CombatMissionCtrl", "if (closest.Distance < (int)Distances.WarptoDistance)", Logging.Green);

                    // Move to the target
                    if (DateTime.UtcNow > Time.Instance.NextApproachAction)
                    {
                        if (QMCache.Instance.IsOrbiting(closest.Id) || QMCache.Instance.Approaching == null || QMCache.Instance.Approaching.Id != closest.Id || QMCache.Instance.MyShipEntity.Velocity < 50)
                        {
                            if (closest.Approach())
                            {
                                Logging.Log("CombatMissionCtrl.Activate", "Approaching target [" + closest.Name + "][" + closest.MaskedId + "][" + Math.Round(closest.Distance / 1000, 0) + "k away]", Logging.Teal);
                                return;
                            }

                            return;
                        }

                        if (Logging.DebugActivateGate) Logging.Log("CombatMissionCtrl", "QMCache.Instance.IsOrbiting [" + QMCache.Instance.IsOrbiting(closest.Id) + "] QMCache.Instance.MyShip.Velocity [" + Math.Round(QMCache.Instance.MyShipEntity.Velocity,0) + "m/s]", Logging.Green);
                        if (Logging.DebugActivateGate) if (QMCache.Instance.Approaching != null) Logging.Log("CombatMissionCtrl", "QMCache.Instance.Approaching.Id [" + QMCache.Instance.Approaching.Id + "][closest.Id: " + closest.Id + "]", Logging.Green);
                        if (Logging.DebugActivateGate) Logging.Log("CombatMissionCtrl", "------------------", Logging.Green);
                        return;
                    }

                    if (QMCache.Instance.IsOrbiting(closest.Id) || QMCache.Instance.Approaching == null || QMCache.Instance.Approaching.Id != closest.Id)
                    {
                        Logging.Log("CombatMissionCtrl", "Activate: Delaying approach for: [" + Math.Round(Time.Instance.NextApproachAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + "] seconds", Logging.Teal);
                        return;
                    }

                    if (Logging.DebugActivateGate) Logging.Log("CombatMissionCtrl", "------------------", Logging.Green);
                    return;
                }

                if (closest.Distance > (int)Distances.WarptoDistance)//we must be outside warpto distance, but we are likely in a DeadSpace so align to the target
                {
                    // We cant warp if we have drones out - but we are aligning not warping so we do not care
                    //if (QMCache.Instance.ActiveDrones.Count() > 0)
                    //    return;

                    if (closest.AlignTo())
                    {
                        Logging.Log("CombatMissionCtrl", "Activate: AlignTo: [" + closest.Name + "] This only happens if we are asked to Activate something that is outside [" + Distances.CloseToGateActivationRange + "]", Logging.Teal);
                        return;
                    }

                    return;
                }

                Logging.Log("CombatMissionCtrl", "Activate: Error: [" + closest.Name + "] at [" + closest.Distance + "] is not within jump distance, within warpable distance or outside warpable distance, (!!!), retrying action.", Logging.Teal);
            }

            return;
        }

        private void ClearAggroAction(Actions.Action action)
        {
            if (!QMCache.Instance.NormalApproach) QMCache.Instance.NormalApproach = true;

            // Get lowest range
            int DistanceToClear;
            if (!int.TryParse(action.GetParameterValue("distance"), out DistanceToClear))
            {
                DistanceToClear = (int)Distances.OnGridWithMe;
            }

            if (DistanceToClear != 0 && DistanceToClear != -2147483648 && DistanceToClear != 2147483647)
            {
                DistanceToClear = (int)Distances.OnGridWithMe;
            }

            //if (QMSettings.Instance.TargetSelectionMethod == "isdp")
            //{
            if (Combat.GetBestPrimaryWeaponTarget(DistanceToClear, false, "combat", Combat.combatTargets.Where(t => t.IsTargetedBy).ToList()))
                    _clearPocketTimeout = null;
            //}
            //else //use new target selection method
            //{
            //    if (QMCache.Instance.__GetBestWeaponTargets(DistanceToClear, Combat.combatTargets.Where(t => t.IsTargetedBy)).Any())
            //        _clearPocketTimeout = null;
            //}

            // Do we have a timeout?  No, set it to now + 5 seconds
            if (!_clearPocketTimeout.HasValue) _clearPocketTimeout = DateTime.UtcNow.AddSeconds(5);

            // Are we in timeout?
            if (DateTime.UtcNow < _clearPocketTimeout.Value) return;

            // We have cleared the Pocket, perform the next action \o/ - reset the timers that we had set for actions...
            Nextaction();

            // Reset timeout
            _clearPocketTimeout = null;
        }

        private void ClearPocketAction(Actions.Action action)
        {
            if (!QMCache.Instance.NormalApproach)
            {
                QMCache.Instance.NormalApproach = true;
            }

            // Get lowest range
            int DistanceToClear;
            if (!int.TryParse(action.GetParameterValue("distance"), out DistanceToClear))
            {
                DistanceToClear = (int)Combat.MaxRange;
            }

            if (DistanceToClear != 0 && DistanceToClear != -2147483648 && DistanceToClear != 2147483647)
            {
                DistanceToClear = (int)Distances.OnGridWithMe;
            }

            //panic handles adding any priority targets and combat will prefer to kill any priority targets

            //If the closest target is out side of our max range, combat cant target, which means GetBest cant return true, so we are going to try and use potentialCombatTargets instead
            if (Combat.PotentialCombatTargets.Any())
            {
                //we may be too far out of range of the closest target to get combat to kick in, lets move us into range here
                EntityCache ClosestPotentialCombatTarget = null;

                if (Logging.DebugClearPocket) Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "QMCache.Instance.__GetBestWeaponTargets(DistanceToClear);", Logging.Debug);

                // Target
                //if (QMSettings.Instance.TargetSelectionMethod == "isdp")
                //{
                    if (Combat.GetBestPrimaryWeaponTarget(DistanceToClear, false, "combat"))
                        _clearPocketTimeout = null;

                //}
                //else //use new target selection method
                //{
                //    if (QMCache.Instance.__GetBestWeaponTargets(DistanceToClear).Any())
                //        _clearPocketTimeout = null;
                //}

                //
                // grab the preferredPrimaryWeaponsTarget if its defined and exists on grid as our navigation point
                //
                if (Combat.PreferredPrimaryWeaponTargetID != null && Combat.PreferredPrimaryWeaponTarget != null)
                {
                    if (Combat.PreferredPrimaryWeaponTarget.IsOnGridWithMe)
                    {
                        if (Logging.DebugClearPocket) Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "ClosestPotentialCombatTarget = Combat.PreferredPrimaryWeaponTarget [" + Combat.PreferredPrimaryWeaponTarget.Name + "]", Logging.Debug);
                        ClosestPotentialCombatTarget = Combat.PreferredPrimaryWeaponTarget;
                    }
                }

                //
                // retry to use PreferredPrimaryWeaponTarget
                //
                if (ClosestPotentialCombatTarget == null && Combat.PreferredPrimaryWeaponTargetID != null && Combat.PreferredPrimaryWeaponTarget != null)
                {
                    if (Combat.PreferredPrimaryWeaponTarget.IsOnGridWithMe)
                    {
                        if (Logging.DebugClearPocket) Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "ClosestPotentialCombatTarget = Combat.PreferredPrimaryWeaponTarget [" + Combat.PreferredPrimaryWeaponTarget.Name + "]", Logging.Debug);
                        ClosestPotentialCombatTarget = Combat.PreferredPrimaryWeaponTarget;
                    }
                }

                if (ClosestPotentialCombatTarget == null) //otherwise just grab something close (excluding sentries)
                {
                    if (Combat.PotentialCombatTargets.Any())
                    {
                        if (Combat.PotentialCombatTargets.OrderBy(t => t.Nearest5kDistance).FirstOrDefault() != null)
                        {
                            EntityCache closestPCT = Combat.PotentialCombatTargets.OrderBy(t => t.Nearest5kDistance).FirstOrDefault();
                            if (closestPCT != null)
                            {
                                if (Logging.DebugClearPocket) Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "ClosestPotentialCombatTarget = Combat.PotentialCombatTargets.OrderBy(t => t.Nearest5kDistance).FirstOrDefault(); [" + closestPCT.Name + "]", Logging.Debug);
                            }
                        }
                    }

                    ClosestPotentialCombatTarget = Combat.PotentialCombatTargets.OrderBy(t => t.Nearest5kDistance).FirstOrDefault();
                }

                if (ClosestPotentialCombatTarget != null && (ClosestPotentialCombatTarget.Distance > Combat.MaxRange || !ClosestPotentialCombatTarget.IsInOptimalRange))
                {
                    if (!QMCache.Instance.IsApproachingOrOrbiting(ClosestPotentialCombatTarget.Id))
                    {
                        NavigateOnGrid.NavigateIntoRange(ClosestPotentialCombatTarget, "combatMissionControl", true);
                    }
                }

                _clearPocketTimeout = null;
            }
            //QMCache.Instance.AddPrimaryWeaponPriorityTargets(Combat.PotentialCombatTargets.Where(t => targetNames.Contains(t.Name)).OrderBy(t => t.Distance).ToList(), PrimaryWeaponPriority.PriorityKillTarget, "CombatMissionCtrl.KillClosestByName");

            // Do we have a timeout?  No, set it to now + 5 seconds
            if (!_clearPocketTimeout.HasValue) _clearPocketTimeout = DateTime.UtcNow.AddSeconds(5);

            // Are we in timeout?
            if (DateTime.UtcNow < _clearPocketTimeout.Value) return;

            // We have cleared the Pocket, perform the next action \o/ - reset the timers that we had set for actions...
            Nextaction();

            // Reset timeout
            _clearPocketTimeout = null;
            return;
        }

        private void ClearWithinWeaponsRangeOnlyAction(Actions.Action action)
        {
            // Get lowest range
            int DistanceToClear;
            if (!int.TryParse(action.GetParameterValue("distance"), out DistanceToClear))
            {
                DistanceToClear = (int)Combat.MaxRange - 1000;
            }

            if (DistanceToClear == 0 || DistanceToClear == -2147483648 || DistanceToClear == 2147483647)
            {
                DistanceToClear = (int)Distances.OnGridWithMe;
            }

            //
            // note this WILL clear sentries within the range given... it does NOT respect the KillSentries setting. 75% of the time this wont matter as sentries will be outside the range
            //

            // Target
            //if (QMSettings.Instance.TargetSelectionMethod == "isdp")
            //{
                if (Combat.GetBestPrimaryWeaponTarget(DistanceToClear, false, "combat"))
                    _clearPocketTimeout = null;

            //}
            //else //use new target selection method
            //{
            //    if (QMCache.Instance.__GetBestWeaponTargets(DistanceToClear).Any())
            //        _clearPocketTimeout = null;
            //}

            // Do we have a timeout?  No, set it to now + 5 seconds
            if (!_clearPocketTimeout.HasValue)
            {
                _clearPocketTimeout = DateTime.UtcNow.AddSeconds(5);
            }

            // Are we in timeout?
            if (DateTime.UtcNow < _clearPocketTimeout.Value)
            {
                return;
            }

            Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "is complete: no more targets in weapons range", Logging.Teal);
            Nextaction();

            // Reset timeout
            _clearPocketTimeout = null;
            return;
        }

        private void ClearWithinWeaponsRangeWithAggroOnlyAction(Actions.Action action)
        {
            if (QMCache.Instance.NormalApproach)
            {
                QMCache.Instance.NormalApproach = false;
            }

            // Get lowest range
            int DistanceToClear;
            if (!int.TryParse(action.GetParameterValue("distance"), out DistanceToClear))
            {
                DistanceToClear = (int)Combat.MaxRange;
            }

            if (DistanceToClear != 0 && DistanceToClear != -2147483648 && DistanceToClear != 2147483647)
            {
                DistanceToClear = (int)Distances.OnGridWithMe;
            }

            //
            // the important bit is here... Adds target to the PrimaryWeapon or Drone Priority Target Lists so that they get killed (we basically wait for combat.cs to do that before proceeding)
            //
            //if (QMSettings.Instance.TargetSelectionMethod == "isdp")
            //{
                if (Combat.GetBestPrimaryWeaponTarget(DistanceToClear, false, "combat", Combat.combatTargets.Where(t => t.IsTargetedBy).ToList()))
                    _clearPocketTimeout = null;
            //}
            //else //use new target selection method
            //{
            //    if (QMCache.Instance.__GetBestWeaponTargets(DistanceToClear, Combat.combatTargets.Where(t => t.IsTargetedBy)).Any())
            //        _clearPocketTimeout = null;
            //}

            // Do we have a timeout?  No, set it to now + 5 seconds
            if (!_clearPocketTimeout.HasValue)
            {
                _clearPocketTimeout = DateTime.UtcNow.AddSeconds(5);
            }

            // Are we in timeout?
            if (DateTime.UtcNow < _clearPocketTimeout.Value)
            {
                return;
            }

            Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "is complete: no more targets that are targeting us", Logging.Teal);
            Nextaction();

            // Reset timeout
            _clearPocketTimeout = null;
            return;
        }

        private void OrbitEntityAction(Actions.Action action)
        {
            if (QMCache.Instance.NormalApproach)
            {
                QMCache.Instance.NormalApproach = false;
            }

            QMCache.Instance.normalNav = false;

            string target = action.GetParameterValue("target");

            bool notTheClosest;
            if (!bool.TryParse(action.GetParameterValue("notclosest"), out notTheClosest))
            {
                notTheClosest = false;
            }

            // No parameter? Although we should not really allow it, assume its the acceleration gate :)
            if (string.IsNullOrEmpty(target))
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "No Entity Specified to orbit: skipping OrbitEntity Action", Logging.Teal);
                Nextaction();
                return;
            }

            IEnumerable<EntityCache> targets = QMCache.Instance.EntitiesByPartialName(target).ToList();
            if (!targets.Any())
            {
                // Unlike activate, no target just means next action
                Nextaction();
                return;
            }

            EntityCache closest = targets.OrderBy(t => t.Distance).FirstOrDefault();

            if (notTheClosest)
            {
                closest = targets.OrderByDescending(t => t.Distance).FirstOrDefault();
            }

            if (closest != null)
            {
                // Move to the target
                if (closest.Orbit(NavigateOnGrid.OrbitDistance))
                {
                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Setting [" + closest.Name + "][" + closest.MaskedId + "][" + Math.Round(closest.Distance / 1000, 0) + "k away as the Orbit Target]", Logging.Teal);
                    Nextaction();
                    return;
                }
            }
            else
            {
                Nextaction();
                return;
            }

            return;
        }

        private void MoveToBackgroundAction(Actions.Action action)
        {
            if (DateTime.UtcNow < _nextCombatMissionCtrlAction)
                return;

            //we cant move in bastion mode, do not try
            List<ModuleCache> bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
            if (bastionModules.Any(i => i.IsActive))
            {
                Logging.Log("CombatMissionCtrl.MoveToBackground", "BastionMode is active, we cannot move, aborting attempt to Activate until bastion deactivates", Logging.Debug);
                _nextCombatMissionCtrlAction = DateTime.UtcNow.AddSeconds(15);
                return;
            }

            if (QMCache.Instance.NormalApproach)
            {
                QMCache.Instance.NormalApproach = false;
            }

            QMCache.Instance.normalNav = false;

            int DistanceToApproach;
            if (!int.TryParse(action.GetParameterValue("distance"), out DistanceToApproach))
            {
                DistanceToApproach = (int)Distances.GateActivationRange;
            }

            string target = action.GetParameterValue("target");

            // No parameter? Although we should not really allow it, assume its the acceleration gate :)
            if (string.IsNullOrEmpty(target))
            {
                target = "Acceleration Gate";
            }

            IEnumerable<EntityCache> targets = QMCache.Instance.EntitiesByName(target, QMCache.Instance.EntitiesOnGrid).ToList();
            if (!targets.Any())
            {
                // Unlike activate, no target just means next action
                Nextaction();
                return;
            }

            EntityCache closest = targets.OrderBy(t => t.Distance).FirstOrDefault();

            if (closest != null)
            {
                // Move to the target
                if (closest.KeepAtRange(DistanceToApproach))
                {
                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Approaching target [" + closest.Name + "][" +closest.MaskedId + "][" + Math.Round(closest.Distance / 1000, 0) + "k away]", Logging.Teal);
                    Nextaction();
                    _nextCombatMissionCtrlAction = DateTime.UtcNow.AddSeconds(5);
                    return;
                }

                return;
            }

            return;
        }

        private void MoveToAction(Actions.Action action)
        {
            if (DateTime.UtcNow < _nextCombatMissionCtrlAction)
                return;

            //we cant move in bastion mode, do not try
            List<ModuleCache> bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
            if (bastionModules.Any(i => i.IsActive))
            {
                Logging.Log("CombatMissionCtrl.MoveTo", "BastionMode is active, we cannot move, aborting attempt to Activate until bastion deactivates", Logging.Debug);
                _nextCombatMissionCtrlAction = DateTime.UtcNow.AddSeconds(15);
                return;
            }

            if (QMCache.Instance.NormalApproach)
            {
                QMCache.Instance.NormalApproach = false;
            }

            QMCache.Instance.normalNav = false;

            string target = action.GetParameterValue("target");

            // No parameter? Although we should not really allow it, assume its the acceleration gate :)
            if (string.IsNullOrEmpty(target))
            {
                target = "Acceleration Gate";
            }

            int DistanceToApproach;
            if (!int.TryParse(action.GetParameterValue("distance"), out DistanceToApproach))
            {
                DistanceToApproach = (int)Distances.GateActivationRange;
            }

            bool stopWhenTargeted;
            if (!bool.TryParse(action.GetParameterValue("StopWhenTargeted"), out stopWhenTargeted))
            {
                stopWhenTargeted = false;
            }

            bool stopWhenAggressed;
            if (!bool.TryParse(action.GetParameterValue("StopWhenAggressed"), out stopWhenAggressed))
            {
                stopWhenAggressed = false;
            }

            List<EntityCache> targets = new List<EntityCache>();
            if (QMCache.Instance.EntitiesOnGrid != null && QMCache.Instance.EntitiesOnGrid.Any())
            {
                //Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Looking for Target [" + target + "] in List of Entities On Grid. EntitiesOnGrid.Count [" + QMCache.Instance.EntitiesOnGrid.Count() + "]", Logging.Debug);
                if (QMCache.Instance.EntitiesByName(target, QMCache.Instance.EntitiesOnGrid) != null && QMCache.Instance.EntitiesByName(target, QMCache.Instance.EntitiesOnGrid).Any())
                {
                    targets = QMCache.Instance.EntitiesByName(target, QMCache.Instance.EntitiesOnGrid).ToList();
                }
            }

            if (!targets.Any())
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "no entities found named [" + target + "] proceeding to next action", Logging.Teal);
                Nextaction();
                return;
            }

            EntityCache closest = targets.OrderBy(t => t.Distance).FirstOrDefault();

            //if (QMSettings.Instance.TargetSelectionMethod == "isdp")
            //{
                Combat.GetBestPrimaryWeaponTarget(Combat.MaxRange, false, "Combat");
            //}
            //else //use new target selection method
            //{
            //    QMCache.Instance.__GetBestWeaponTargets(Combat.MaxRange);
            //}

            if (closest != null)
            {
                if (stopWhenTargeted)
                {
                    if (Combat.TargetedBy != null && Combat.TargetedBy.Any())
                    {
                        if (QMCache.Instance.Approaching != null)
                        {
                            if (QMCache.Instance.MyShipEntity.Velocity != 0 && DateTime.UtcNow > Time.Instance.NextApproachAction)
                            {
                                NavigateOnGrid.StopMyShip();
                                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Stop ship, we have been targeted and are [" + DistanceToApproach + "] from [ID: " + closest.Name + "][" + Math.Round(closest.Distance / 1000, 0) + "k away]", Logging.Teal);
                            }
                        }
                    }
                }

                if (stopWhenAggressed)
                {
                    if (Combat.Aggressed.Any(t => !t.IsSentry))
                    {
                        if (QMCache.Instance.Approaching != null)
                        {
                            if (QMCache.Instance.MyShipEntity.Velocity != 0 && DateTime.UtcNow > Time.Instance.NextApproachAction)
                            {
                                NavigateOnGrid.StopMyShip();
                                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Stop ship, we have been targeted and are [" + DistanceToApproach + "] from [ID: " + closest.Name + "][" + Math.Round(closest.Distance / 1000, 0) + "k away]", Logging.Teal);
                            }
                        }
                    }
                }

                if (closest.Distance < DistanceToApproach) // if we are inside the range that we are supposed to approach assume we are done
                {
                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "We are [" + Math.Round(closest.Distance, 0) + "] from a [" + target + "] we do not need to go any further", Logging.Teal);
                    Nextaction();

                    if (QMCache.Instance.Approaching != null)
                    {
                        if (QMCache.Instance.MyShipEntity.Velocity != 0 && DateTime.UtcNow > Time.Instance.NextApproachAction)
                        {
                            NavigateOnGrid.StopMyShip();
                            Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Stop ship, we have been targeted and are [" + DistanceToApproach + "] from [ID: " + closest.Name + "][" + Math.Round(closest.Distance / 1000, 0) + "k away]", Logging.Teal);
                        }
                    }

                    //if (QMSettings.Instance.SpeedTank)
                    //{
                    //    //this should at least keep speed tanked ships from going poof if a mission XML uses moveto
                    //    closest.Orbit(QMCache.Instance.OrbitDistance);
                    //    Logging.Log("CombatMissionCtrl","MoveTo: Initiating orbit after reaching target")
                    //}
                    return;
                }

                if (closest.Distance < (int)Distances.WarptoDistance) // if we are inside warpto range you need to approach (you cant warp from here)
                {
                    if (Logging.DebugMoveTo) Logging.Log("CombatMissionCtrl.MoveTo", "if (closest.Distance < (int)Distances.WarptoDistance)] -  NextApproachAction [" + Time.Instance.NextApproachAction + "]", Logging.Teal);

                    // Move to the target

                    if (Logging.DebugMoveTo) if (QMCache.Instance.Approaching == null) Logging.Log("CombatMissionCtrl.MoveTo", "if (QMCache.Instance.Approaching == null)", Logging.Teal);
                    if (Logging.DebugMoveTo) if (QMCache.Instance.Approaching != null) Logging.Log("CombatMissionCtrl.MoveTo", "QMCache.Instance.Approaching.Id [" + QMCache.Instance.Approaching.Id + "]", Logging.Teal);
                    if (QMCache.Instance.Approaching == null || QMCache.Instance.Approaching.Id != closest.Id || QMCache.Instance.MyShipEntity.Velocity < 50)
                    {
                        if (closest.Approach())
                        {
                            Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Approaching target [" + closest.Name + "][" + closest.MaskedId + "][" + Math.Round(closest.Distance / 1000, 0) + "k away]", Logging.Teal);
                            _nextCombatMissionCtrlAction = DateTime.UtcNow.AddSeconds(5);
                            return;
                        }

                        return;
                    }
                    if (Logging.DebugMoveTo) if (QMCache.Instance.Approaching != null) Logging.Log("CombatMissionCtrl.MoveTo", "-----------", Logging.Teal);
                    return;
                }

                // Probably never happens
                if (closest.AlignTo())
                {
                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Aligning to target [" + closest.Name + "][" + closest.MaskedId + "][" + Math.Round(closest.Distance / 1000, 0) + "k away]", Logging.Teal);
                    _nextCombatMissionCtrlAction = DateTime.UtcNow.AddSeconds(5);
                    return;
                }

                return;
            }

            return;
        }

        private void WaitUntilTargeted(Actions.Action action)
        {
            IEnumerable<EntityCache> targetedBy = Combat.TargetedBy;
            if (targetedBy != null && targetedBy.Any())
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "We have been targeted!", Logging.Teal);

                // We have been locked, go go go ;)
                _waiting = false;
                Nextaction();
                return;
            }

            // Default timeout is 30 seconds
            int timeout;
            if (!int.TryParse(action.GetParameterValue("timeout"), out timeout))
            {
                timeout = 30;
            }

            if (_waiting)
            {
                if (DateTime.UtcNow < _waitingSince.AddSeconds(timeout))
                {
                    //
                    // Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Still WaitingUntilTargeted...", Logging.Debug);
                    //
                    return;
                }

                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Nothing targeted us within [ " + timeout + "sec]!", Logging.Teal);

                // Nothing has targeted us in the specified timeout
                _waiting = false;
                Nextaction();
                return;
            }

            Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Nothing has us targeted yet: waiting up to [ " + timeout + "sec] starting now.", Logging.Teal);
            // Start waiting
            _waiting = true;
            _waitingSince = DateTime.UtcNow;
            return;
        }

        private void WaitUntilAggressed(Actions.Action action)
        {
            // Default timeout is 60 seconds
            int timeout;
            if (!int.TryParse(action.GetParameterValue("timeout"), out timeout))
            {
                timeout = 60;
            }

            int WaitUntilShieldsAreThisLow;
            if (int.TryParse(action.GetParameterValue("WaitUntilShieldsAreThisLow"), out WaitUntilShieldsAreThisLow))
            {
                MissionSettings.MissionActivateRepairModulesAtThisPerc = WaitUntilShieldsAreThisLow;
            }

            int WaitUntilArmorIsThisLow;
            if (int.TryParse(action.GetParameterValue("WaitUntilArmorIsThisLow"), out WaitUntilArmorIsThisLow))
            {
                MissionSettings.MissionActivateRepairModulesAtThisPerc = WaitUntilArmorIsThisLow;
            }

            if (_waiting)
            {
                if (DateTime.UtcNow.Subtract(_waitingSince).TotalSeconds < timeout)
                {
                    return;
                }

                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Nothing targeted us within [ " + timeout + "sec]!", Logging.Teal);

                // Nothing has targeted us in the specified timeout
                _waiting = false;
                Nextaction();
                return;
            }

            // Start waiting
            _waiting = true;
            _waitingSince = DateTime.UtcNow;
            return;
        }
        private void ActivateBastionAction(Actions.Action action)
        {
            bool _done = false;

            if (QMCache.Instance.Modules.Any())
            {
                List<ModuleCache> bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
                if (!bastionModules.Any() || bastionModules.Any(i => i.IsActive))
                {
                    _done = true;
                }
            }
            else
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "no bastion modules fitted!", Logging.Teal);
                _done = true;
            }

            if (_done)
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "ActivateBastion Action completed.", Logging.Teal);

                // Nothing has targeted us in the specified timeout
                _waiting = false;
                Nextaction();
                return;
            }

            // Default timeout is 60 seconds
            int DeactivateAfterSeconds;
            if (!int.TryParse(action.GetParameterValue("DeactivateAfterSeconds"), out DeactivateAfterSeconds))
            {
                DeactivateAfterSeconds = 5;
            }

            Time.Instance.NextBastionModeDeactivate = DateTime.UtcNow.AddSeconds(DeactivateAfterSeconds);

            DeactivateIfNothingTargetedWithinRange = false;
            if (!bool.TryParse(action.GetParameterValue("DeactivateIfNothingTargetedWithinRange"), out DeactivateIfNothingTargetedWithinRange))
            {
                DeactivateIfNothingTargetedWithinRange = false;
            }

            // Start bastion mode
            if (!Combat.ActivateBastion(true)) return;
            return;
        }

        private void DebuggingWait(Actions.Action action)
        {
            // Default timeout is 1200 seconds
            int timeout;
            if (!int.TryParse(action.GetParameterValue("timeout"), out timeout))
            {
                timeout = 1200;
            }

            if (_waiting)
            {
                if (DateTime.UtcNow.Subtract(_waitingSince).TotalSeconds < timeout)
                {
                    return;
                }

                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Nothing targeted us within [ " + timeout + "sec]!", Logging.Teal);

                // Nothing has targeted us in the specified timeout
                _waiting = false;
                Nextaction();
                return;
            }

            // Start waiting
            _waiting = true;
            _waitingSince = DateTime.UtcNow;
            return;
        }

        private void AggroOnlyAction(Actions.Action action)
        {
            if (QMCache.Instance.NormalApproach)
            {
                QMCache.Instance.NormalApproach = false;
            }

            // Get lowest range
            int DistanceToClear;
            if (!int.TryParse(action.GetParameterValue("distance"), out DistanceToClear))
            {
                DistanceToClear = (int)Distances.OnGridWithMe;
            }

            if (DistanceToClear != 0 && DistanceToClear != -2147483648 && DistanceToClear != 2147483647)
            {
                DistanceToClear = (int)Distances.OnGridWithMe;
            }

            //
            // the important bit is here... Adds target to the PrimaryWeapon or Drone Priority Target Lists so that they get killed (we basically wait for combat.cs to do that before proceeding)
            //
            //if (QMSettings.Instance.TargetSelectionMethod == "isdp")
            //{
                if (Combat.GetBestPrimaryWeaponTarget(DistanceToClear, false, "combat", Combat.combatTargets.Where(t => t.IsTargetedBy).ToList()))
                    _clearPocketTimeout = null;
            //}
            //else //use new target selection method
            //{
            //    if (QMCache.Instance.__GetBestWeaponTargets(DistanceToClear, Combat.combatTargets.Where(t => t.IsTargetedBy).ToList()).Any())
            //        _clearPocketTimeout = null;
            //}

            // Do we have a timeout?  No, set it to now + 5 seconds
            if (!_clearPocketTimeout.HasValue)
            {
                _clearPocketTimeout = DateTime.UtcNow.AddSeconds(5);
            }

            // Are we in timeout?
            if (DateTime.UtcNow < _clearPocketTimeout.Value)
            {
                return;
            }

            Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "is complete: no more targets that are targeting us", Logging.Teal);
            Nextaction();

            // Reset timeout
            _clearPocketTimeout = null;
            return;
        }

        private void AddWarpScramblerByNameAction(Actions.Action action)
        {
            bool notTheClosest;
            if (!bool.TryParse(action.GetParameterValue("notclosest"), out notTheClosest))
            {
                notTheClosest = false;
            }

            int numberToIgnore;
            if (!int.TryParse(action.GetParameterValue("numbertoignore"), out numberToIgnore))
            {
                numberToIgnore = 0;
            }

            List<string> targetNames = action.GetParameterValues("target");

            // No parameter? Ignore kill action
            if (!targetNames.Any())
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "No targets defined in AddWarpScramblerByName action!", Logging.Teal);
                Nextaction();
                return;
            }

            Combat.AddWarpScramblerByName(targetNames.FirstOrDefault(), numberToIgnore, notTheClosest);

            //
            // this action is passive and only adds things to the WarpScramblers list )before they have a chance to scramble you, so you can target them early
            //
            Nextaction();
            return;
        }

        private void AddEcmNpcByNameAction(Actions.Action action)
        {
            bool notTheClosest;
            if (!bool.TryParse(action.GetParameterValue("notclosest"), out notTheClosest))
            {
                notTheClosest = false;
            }

            int numberToIgnore;
            if (!int.TryParse(action.GetParameterValue("numbertoignore"), out numberToIgnore))
            {
                numberToIgnore = 0;
            }

            List<string> targetNames = action.GetParameterValues("target");

            // No parameter? Ignore kill action
            if (!targetNames.Any())
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "No targets defined in AddWarpScramblerByName action!", Logging.Teal);
                Nextaction();
                return;
            }

            Combat.AddWarpScramblerByName(targetNames.FirstOrDefault(), numberToIgnore, notTheClosest);

            //
            // this action is passive and only adds things to the WarpScramblers list )before they have a chance to scramble you, so you can target them early
            //
            Nextaction();
            return;
        }

        private void AddWebifierByNameAction(Actions.Action action)
        {
            bool notTheClosest;
            if (!bool.TryParse(action.GetParameterValue("notclosest"), out notTheClosest))
            {
                notTheClosest = false;
            }

            int numberToIgnore;
            if (!int.TryParse(action.GetParameterValue("numbertoignore"), out numberToIgnore))
            {
                numberToIgnore = 0;
            }

            List<string> targetNames = action.GetParameterValues("target");

            // No parameter? Ignore kill action
            if (!targetNames.Any())
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "No targets defined in AddWebifierByName action!", Logging.Teal);
                Nextaction();
                return;
            }

            Combat.AddWebifierByName(targetNames.FirstOrDefault(), numberToIgnore, notTheClosest);

            //
            // this action is passive and only adds things to the WarpScramblers list )before they have a chance to scramble you, so you can target them early
            //
            Nextaction();
            return;
        }

        private void KillAction(Actions.Action action)
        {
            if (QMCache.Instance.NormalApproach) QMCache.Instance.NormalApproach = false;

            bool ignoreAttackers;
            if (!bool.TryParse(action.GetParameterValue("ignoreattackers"), out ignoreAttackers))
            {
                ignoreAttackers = false;
            }

            bool breakOnAttackers;
            if (!bool.TryParse(action.GetParameterValue("breakonattackers"), out breakOnAttackers))
            {
                breakOnAttackers = false;
            }

            bool notTheClosest;
            if (!bool.TryParse(action.GetParameterValue("notclosest"), out notTheClosest))
            {
                notTheClosest = false;
            }

            int numberToIgnore;
            if (!int.TryParse(action.GetParameterValue("numbertoignore"), out numberToIgnore))
            {
                numberToIgnore = 0;
            }

            int attackUntilBelowShieldPercentage;
            if (!int.TryParse(action.GetParameterValue("attackUntilBelowShieldPercentage"), out attackUntilBelowShieldPercentage))
            {
                attackUntilBelowShieldPercentage = 0;
            }

            int attackUntilBelowArmorPercentage;
            if (!int.TryParse(action.GetParameterValue("attackUntilBelowArmorPercentage"), out attackUntilBelowArmorPercentage))
            {
                attackUntilBelowArmorPercentage = 0;
            }

            int attackUntilBelowHullPercentage;
            if (!int.TryParse(action.GetParameterValue("attackUntilBelowHullPercentage"), out attackUntilBelowHullPercentage))
            {
                attackUntilBelowHullPercentage = 0;
            }

            List<string> targetNames = action.GetParameterValues("target");

            // No parameter? Ignore kill action
            if (!targetNames.Any())
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "No targets defined in kill action!", Logging.Teal);
                Nextaction();
                return;
            }

            if (Logging.DebugKillAction)
            {
                int targetNameCount = 0;
                foreach (string targetName in targetNames)
                {
                    targetNameCount++;
                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "targetNames [" + targetNameCount + "][" + targetName + "]", Logging.Debug);
                }
            }

            List<EntityCache> killTargets = QMCache.Instance.EntitiesOnGrid.Where(e => targetNames.Contains(e.Name)).OrderBy(t => t.Nearest5kDistance).ToList();

            if (notTheClosest) killTargets = QMCache.Instance.EntitiesOnGrid.Where(e => targetNames.Contains(e.Name)).OrderByDescending(t => t.Nearest5kDistance).ToList();

            if (!killTargets.Any() || killTargets.Count() <= numberToIgnore)
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "All targets killed " + targetNames.Aggregate((current, next) => current + "[" + next + "] NumToIgnore [" + numberToIgnore + "]"), Logging.Teal);

                // We killed it/them !?!?!? :)
                IgnoreTargets.RemoveWhere(targetNames.Contains);
                if (ignoreAttackers)
                {
                    //
                    // UNIgnore attackers when kill is done.
                    //
                    foreach (EntityCache target in Combat.PotentialCombatTargets.Where(e => !targetNames.Contains(e.Name)))
                    {
                        if (target.IsTargetedBy && target.IsAttacking)
                        {
                            Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "UN-Ignoring [" + target.Name + "][" + target.MaskedId + "][" + Math.Round(target.Distance / 1000, 0) + "k away] due to ignoreAttackers parameter (and kill action being complete)", Logging.Teal);
                            IgnoreTargets.Remove(target.Name.Trim());
                        }
                    }
                }
                Nextaction();
                return;
            }

            if (ignoreAttackers)
            {
                foreach (EntityCache target in Combat.PotentialCombatTargets.Where(e => !targetNames.Contains(e.Name)))
                {
                    if (target.IsTargetedBy && target.IsAttacking)
                    {
                        Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Ignoring [" + target.Name + "][" + target.MaskedId + "][" + Math.Round(target.Distance / 1000, 0) + "k away] due to ignoreAttackers parameter", Logging.Teal);
                        IgnoreTargets.Add(target.Name.Trim());
                    }
                }
            }

            if (breakOnAttackers && Combat.TargetedBy.Count(t => (!t.IsSentry || (t.IsSentry && Combat.KillSentries) || (t.IsSentry && t.IsEwarTarget)) && !t.IsIgnored) > killTargets.Count(e => e.IsTargetedBy))
            {
                //
                // We are being attacked, break the kill order
                // which involves removing the named targets as PrimaryWeaponPriorityTargets, PreferredPrimaryWeaponTarget, DronePriorityTargets, and PreferredDroneTarget
                //
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Breaking off kill order, new spawn has arrived!", Logging.Teal);
                targetNames.ForEach(t => IgnoreTargets.Add(t));

                if (killTargets.Any())
                {
                    Combat.RemovePrimaryWeaponPriorityTargets(killTargets.ToList());

                    if (Combat.PreferredPrimaryWeaponTarget != null && killTargets.Any(i => i.Name == Combat.PreferredPrimaryWeaponTarget.Name))
                    {
                        List<EntityCache> PreferredPrimaryWeaponTargetsToRemove = killTargets.Where(i => i.Name == Combat.PreferredPrimaryWeaponTarget.Name).ToList();
                        Combat.RemovePrimaryWeaponPriorityTargets(PreferredPrimaryWeaponTargetsToRemove);
                        if (Drones.UseDrones)
                        {
                            Drones.RemoveDronePriorityTargets(PreferredPrimaryWeaponTargetsToRemove);
                        }
                    }

                    if (Combat.PreferredPrimaryWeaponTargetID != null)
                    {
                        foreach (EntityCache killTarget in killTargets.Where(e => e.Id == Combat.PreferredPrimaryWeaponTargetID))
                        {
                            if (Combat.PreferredPrimaryWeaponTargetID == null) continue;
                            Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Breaking Kill Order in: [" + killTarget.Name + "][" + Math.Round(killTarget.Distance/1000,0) + "k][" + Combat.PreferredPrimaryWeaponTarget.MaskedId + "]", Logging.Red);
                            Combat.PreferredPrimaryWeaponTarget = null;
                        }
                    }

                    if (Drones.PreferredDroneTargetID != null)
                    {
                        foreach (EntityCache killTarget in killTargets.Where(e => e.Id == Drones.PreferredDroneTargetID))
                        {
                            if (Drones.PreferredDroneTargetID == null) continue;
                            Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Breaking Kill Order in: [" + killTarget.Name + "][" + Math.Round(killTarget.Distance / 1000, 0) + "k][" + Drones.PreferredDroneTarget.MaskedId + "]", Logging.Red);
                            Drones.PreferredDroneTarget = null;
                        }
                    }
                }


                foreach (EntityCache KillTargetEntity in QMCache.Instance.Targets.Where(e => targetNames.Contains(e.Name) && (e.IsTarget || e.IsTargeting)))
                {
                    if (Combat.PreferredPrimaryWeaponTarget != null)
                    {
                        if (KillTargetEntity.Id == Combat.PreferredPrimaryWeaponTarget.Id)
                        {
                            continue;
                        }
                    }

                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Unlocking [" + KillTargetEntity.Name + "][" + KillTargetEntity.MaskedId + "][" + Math.Round(KillTargetEntity.Distance / 1000, 0) + "k away] due to kill order being put on hold", Logging.Teal);
                    KillTargetEntity.UnlockTarget("CombatMissionCtrl");
                }
            }
            else //Do not break aggression on attackers (attack normally)
            {

                //
                // check to see if we have priority targets (ECM, warp scramblers, etc, and let combat process those first)
                //
                EntityCache primaryWeaponPriorityTarget = null;
                if (Combat.PrimaryWeaponPriorityEntities.Any())
                {
                    try
                    {
                        primaryWeaponPriorityTarget = Combat.PrimaryWeaponPriorityEntities.Where(p => p.Distance < Combat.MaxRange
                                                                                    && p.IsReadyToShoot
                                                                                    && p.IsOnGridWithMe
                                                                                    && ((!p.IsNPCFrigate && !p.IsFrigate) || (!Drones.UseDrones && !p.IsTooCloseTooFastTooSmallToHit)))
                                                                                   .OrderByDescending(pt => pt.IsTargetedBy)
                                                                                   .ThenByDescending(pt => pt.IsInOptimalRange)
                                                                                   .ThenByDescending(pt => pt.IsEwarTarget)
                                                                                   .ThenBy(pt => pt.PrimaryWeaponPriorityLevel)
                                                                                   .ThenBy(pt => pt.Distance)
                                                                                   .FirstOrDefault();
                    }
                    catch (Exception ex)
                    {
                        Logging.Log("CombatMissionCtrl.Kill","Exception [" + ex + "]",Logging.Debug);
                    }
                }

                if (primaryWeaponPriorityTarget != null && primaryWeaponPriorityTarget.IsOnGridWithMe)
                {
                    if (Logging.DebugKillAction)
                    {
                        if (Combat.PrimaryWeaponPriorityTargets.Any())
                        {
                            int icount = 0;
                            foreach (EntityCache primaryWeaponPriorityEntity in Combat.PrimaryWeaponPriorityEntities.Where(i => i.IsOnGridWithMe))
                            {
                                icount++;
                                if (Logging.DebugKillAction) Logging.Log("Combat", "[" + icount + "] PrimaryWeaponPriorityTarget Named [" + primaryWeaponPriorityEntity.Name + "][" + primaryWeaponPriorityEntity.MaskedId + "][" + Math.Round(primaryWeaponPriorityEntity.Distance / 1000, 0) + "k away]", Logging.Teal);
                                continue;
                            }
                        }
                    }
                    //
                    // GetBestTarget below will choose to assign PriorityTargets over preferred targets, so we might as well wait... (and not approach the wrong target)
                    //
                }
                else
                {
                    //
                    // then proceed to kill the target
                    //
                    IgnoreTargets.RemoveWhere(targetNames.Contains);

                    if (killTargets.FirstOrDefault() != null) //if it is not null is HAS to be OnGridWithMe as all killTargets are verified OnGridWithMe
                    {
                        if (attackUntilBelowShieldPercentage > 0 && (killTargets.FirstOrDefault().ShieldPct * 100) < attackUntilBelowShieldPercentage)
                        {
                            Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Kill target [" + killTargets.FirstOrDefault().Name + "] at [" + Math.Round(killTargets.FirstOrDefault().Distance / 1000, 2) + "k] Armor % is [" + killTargets.FirstOrDefault().ShieldPct * 100 + "] which is less then attackUntilBelowShieldPercentage [" + attackUntilBelowShieldPercentage + "] Kill Action Complete, Next Action.", Logging.Yellow);
                            Combat.RemovePrimaryWeaponPriorityTargets(killTargets);
                            Combat.PreferredPrimaryWeaponTarget = null;
                            Nextaction();
                            return;
                        }

                        if (attackUntilBelowArmorPercentage > 0 && (killTargets.FirstOrDefault().ArmorPct * 100) < attackUntilBelowArmorPercentage)
                        {
                            Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Kill target [" + killTargets.FirstOrDefault().Name + "] at [" + Math.Round(killTargets.FirstOrDefault().Distance / 1000,2) + "k] Armor % is [" + killTargets.FirstOrDefault().ArmorPct * 100 + "] which is less then attackUntilBelowArmorPercentage [" + attackUntilBelowArmorPercentage + "] Kill Action Complete, Next Action.", Logging.Yellow);
                            Combat.RemovePrimaryWeaponPriorityTargets(killTargets);
                            Combat.PreferredPrimaryWeaponTarget = null;
                            Nextaction();
                            return;
                        }

                        if (attackUntilBelowHullPercentage > 0 && (killTargets.FirstOrDefault().ArmorPct * 100) < attackUntilBelowHullPercentage)
                        {
                            Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Kill target [" + killTargets.FirstOrDefault().Name + "] at [" + Math.Round(killTargets.FirstOrDefault().Distance / 1000, 2) + "k] Armor % is [" + killTargets.FirstOrDefault().StructurePct * 100 + "] which is less then attackUntilBelowHullPercentage [" + attackUntilBelowHullPercentage + "] Kill Action Complete, Next Action.", Logging.Yellow);
                            Combat.RemovePrimaryWeaponPriorityTargets(killTargets);
                            Combat.PreferredPrimaryWeaponTarget = null;
                            Nextaction();
                            return;
                        }

                        if (Logging.DebugKillAction) Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], " proceeding to kill [" + killTargets.FirstOrDefault().Name + "] at [" + Math.Round(killTargets.FirstOrDefault().Distance / 1000, 2) + "k] (this is spammy, but useful debug info)", Logging.White);
                        //if (Combat.PreferredPrimaryWeaponTarget == null || String.IsNullOrEmpty(QMCache.Instance.PreferredDroneTarget.Name) || Combat.PreferredPrimaryWeaponTarget.IsOnGridWithMe && Combat.PreferredPrimaryWeaponTarget != currentKillTarget)
                        //{
                            //Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Adding [" + currentKillTarget.Name + "][" + Math.Round(currentKillTarget.Distance / 1000, 0) + "][" + QMCache.Instance.MaskedID(currentKillTarget.Id) + "] groupID [" + currentKillTarget.GroupId + "] TypeID[" + currentKillTarget.TypeId + "] as PreferredPrimaryWeaponTarget", Logging.Teal);
                            Combat.AddPrimaryWeaponPriorityTarget(killTargets.FirstOrDefault(), PrimaryWeaponPriority.PriorityKillTarget, "CombatMissionCtrl.Kill[" + PocketNumber + "]." + _pocketActions[_currentAction]);
                            Combat.PreferredPrimaryWeaponTarget = killTargets.FirstOrDefault();
                        //}
                        //else
                        if (Logging.DebugKillAction)
                        {
                            if (Logging.DebugKillAction) Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Combat.PreferredPrimaryWeaponTarget =[ " + Combat.PreferredPrimaryWeaponTarget.Name + " ][" + Combat.PreferredPrimaryWeaponTarget.MaskedId + "]", Logging.Debug);

                            if (Combat.PrimaryWeaponPriorityTargets.Any())
                            {
                                if (Logging.DebugKillAction) Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "PrimaryWeaponPriorityTargets Below (if any)", Logging.Debug);
                                int icount = 0;
                                foreach (EntityCache PT in Combat.PrimaryWeaponPriorityEntities)
                                {
                                    icount++;
                                    if (Logging.DebugKillAction) Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "PriorityTarget [" + icount + "] [ " + PT.Name + " ][" + PT.MaskedId + "] IsOnGridWithMe [" + PT.IsOnGridWithMe + "]", Logging.Debug);
                                }
                                if (Logging.DebugKillAction) Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "PrimaryWeaponPriorityTargets Above (if any)", Logging.Debug);
                            }
                        }

                        EntityCache NavigateTowardThisTarget = null;
                        if (Combat.PreferredPrimaryWeaponTarget != null)
                        {
                            NavigateTowardThisTarget = Combat.PreferredPrimaryWeaponTarget;
                        }
                        if (Combat.PreferredPrimaryWeaponTarget != null)
                        {
                            NavigateTowardThisTarget = killTargets.FirstOrDefault();
                        }
                        //we may need to get closer so combat will take over
                        if (NavigateTowardThisTarget.Distance > Combat.MaxRange || !NavigateTowardThisTarget.IsInOptimalRange)
                        {
                            if (Logging.DebugKillAction) Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "if (Combat.PreferredPrimaryWeaponTarget.Distance > Combat.MaxRange)", Logging.Debug);
                            //if (!QMCache.Instance.IsApproachingOrOrbiting(Combat.PreferredPrimaryWeaponTarget.Id))
                            //{
                            //    if (Logging.DebugKillAction) Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "if (!QMCache.Instance.IsApproachingOrOrbiting(Combat.PreferredPrimaryWeaponTarget.Id))", Logging.Debug);
                                  NavigateOnGrid.NavigateIntoRange(NavigateTowardThisTarget, "combatMissionControl", true);
                            //}
                        }
                    }
                }

                if (Combat.PreferredPrimaryWeaponTarget != killTargets.FirstOrDefault())
                {
                    // GetTargets
                    //if (QMSettings.Instance.TargetSelectionMethod == "isdp")
                    //{
                        Combat.GetBestPrimaryWeaponTarget(Combat.MaxRange, false, "Combat");
                    //}
                    //else //use new target selection method
                    //{
                    //    QMCache.Instance.__GetBestWeaponTargets(Combat.MaxRange);
                    //}
                }
            }

            // Don't use NextAction here, only if target is killed (checked further up)
            return;
        }

        private void UseDrones(Actions.Action action)
        {
            bool usedrones;
            if (!bool.TryParse(action.GetParameterValue("use"), out usedrones))
            {
                usedrones = false;
            }

            if (!usedrones)
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Disable launch of drones", Logging.Teal);
                MissionSettings.PocketUseDrones = false;
            }
            else
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Enable launch of drones", Logging.Teal);
                MissionSettings.PocketUseDrones = true;
            }
            Nextaction();
            return;
        }

        private void KillClosestByNameAction(Actions.Action action)
        {
            if (QMCache.Instance.NormalApproach) QMCache.Instance.NormalApproach = false;

            List<string> targetNames = action.GetParameterValues("target");

            // No parameter? Ignore kill action
            if (targetNames.Count == 0)
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "No targets defined!", Logging.Teal);
                Nextaction();
                return;
            }

            //
            // the way this is currently written is will NOT stop after killing the first target as intended, it will clear all targets with the Name given
            //

            Combat.AddPrimaryWeaponPriorityTarget(Combat.PotentialCombatTargets.Where(t => targetNames.Contains(t.Name)).OrderBy(t => t.Distance).Take(1).FirstOrDefault(),PrimaryWeaponPriority.PriorityKillTarget, "CombatMissionCtrl.KillClosestByName");

            //if (QMSettings.Instance.TargetSelectionMethod == "isdp")
            //{
                if (Combat.GetBestPrimaryWeaponTarget((double)Distances.OnGridWithMe, false, "combat", Combat.PotentialCombatTargets.OrderBy(t => t.Distance).Take(1).ToList()))
                    _clearPocketTimeout = null;
            //}
            //else //use new target selection method
            //{
            //    if (QMCache.Instance.__GetBestWeaponTargets((double)Distances.OnGridWithMe, Combat.PotentialCombatTargets.Where(e => !e.IsSentry || (e.IsSentry && QMSettings.Instance.KillSentries)).OrderBy(t => t.Distance).Take(1).ToList()).Any())
            //        _clearPocketTimeout = null;
            //}

            // Do we have a timeout?  No, set it to now + 5 seconds
            if (!_clearPocketTimeout.HasValue) _clearPocketTimeout = DateTime.UtcNow.AddSeconds(5);

            // Are we in timeout?
            if (DateTime.UtcNow < _clearPocketTimeout.Value) return;

            // We have cleared the Pocket, perform the next action \o/ - reset the timers that we had set for actions...
            Nextaction();

            // Reset timeout
            _clearPocketTimeout = null;
            return;
        }

        private void KillClosestAction(Actions.Action action)
        {
            if (QMCache.Instance.NormalApproach) QMCache.Instance.NormalApproach = false;

            //
            // the way this is currently written is will NOT stop after killing the first target as intended, it will clear all targets with the Name given, in this everything on grid
            //

            //if (QMSettings.Instance.TargetSelectionMethod == "isdp")
            //{
                if (Combat.GetBestPrimaryWeaponTarget((double)Distances.OnGridWithMe, false, "combat", Combat.PotentialCombatTargets.OrderBy(t => t.Distance).Take(1).ToList()))
                    _clearPocketTimeout = null;
            //}
            //else //use new target selection method
            //{
            //    if (QMCache.Instance.__GetBestWeaponTargets((double)Distances.OnGridWithMe, Combat.PotentialCombatTargets.Where(e => !e.IsSentry || (e.IsSentry && QMSettings.Instance.KillSentries)).OrderBy(t => t.Distance).Take(1).ToList()).Any())
            //        _clearPocketTimeout = null;
            //}

            // Do we have a timeout?  No, set it to now + 5 seconds
            if (!_clearPocketTimeout.HasValue) _clearPocketTimeout = DateTime.UtcNow.AddSeconds(5);

            // Are we in timeout?
            if (DateTime.UtcNow < _clearPocketTimeout.Value) return;

            // We have cleared the Pocket, perform the next action \o/ - reset the timers that we had set for actions...
            Nextaction();

            // Reset timeout
            _clearPocketTimeout = null;
            return;
        }

        private void DropItemAction(Actions.Action action)
        {
            try
            {
                //QMCache.Instance.DropMode = true;
                List<string> items = action.GetParameterValues("item");
                string targetName = action.GetParameterValue("target");

                int quantity;
                if (!int.TryParse(action.GetParameterValue("quantity"), out quantity))
                {
                    quantity = 1;
                }

                if (!CargoHoldHasBeenStacked)
                {
                    Logging.Log("MissionController.DropItem", "Stack CargoHold", Logging.Orange);
                    if (!QMCache.Instance.StackCargoHold("DropItem")) return;
                    CargoHoldHasBeenStacked = true;
                    return;
                }

                IEnumerable<EntityCache> targetEntities = QMCache.Instance.EntitiesByName(targetName, QMCache.Instance.EntitiesOnGrid).ToList();
                if (targetEntities.Any())
                {
                    Logging.Log("MissionController.DropItem", "We have [" + targetEntities.Count() + "] entities on grid that match our target by name: [" + targetName.FirstOrDefault() + "]", Logging.Orange);
                    targetEntities = targetEntities.Where(i => i.IsContainer || i.GroupId == (int)Group.LargeColidableObject); //some missions (like: Onslaught - lvl1) have LCOs that can hold and take cargo, note that same mission has a LCS with the same name!

                    if (!targetEntities.Any())
                    {
                        Logging.Log("MissionController.DropItem", "No entity on grid named: [" + targetEntities.FirstOrDefault() + "] that is also a container", Logging.Orange);

                        // now that we have completed this action revert OpenWrecks to false
                        //QMCache.Instance.DropMode = false;
                        Nextaction();
                        return;
                    }

                    EntityCache closest = targetEntities.OrderBy(t => t.Distance).FirstOrDefault();

                    if (closest == null)
                    {
                        Logging.Log("MissionController.DropItem", "closest: target named [" + targetName.FirstOrDefault() + "] was null" + targetEntities, Logging.Orange);

                        // now that we have completed this action revert OpenWrecks to false
                        //QMCache.Instance.DropMode = false;
                        Nextaction();
                        return;
                    }

                    if (closest.Distance > (int)Distances.SafeScoopRange)
                    {
                        if (DateTime.UtcNow > Time.Instance.NextApproachAction)
                        {
                            if (QMCache.Instance.Approaching == null || QMCache.Instance.Approaching.Id != closest.Id || QMCache.Instance.MyShipEntity.Velocity < 50)
                            {
                                if (closest.KeepAtRange(1000))
                                {
                                    Logging.Log("MissionController.DropItem", "Approaching target [" + closest.Name + "][" + closest.MaskedId + "] which is at [" + Math.Round(closest.Distance / 1000, 0) + "k away]", Logging.White);
                                }
                            }
                        }
                    }
                    else if (QMCache.Instance.MyShipEntity.Velocity < 50) //nearly stopped
                    {
                        if (DateTime.UtcNow > Time.Instance.NextOpenContainerInSpaceAction)
                        {
                            DirectContainer containerWeWillDropInto = null;

                            containerWeWillDropInto = QMCache.Instance.DirectEve.GetContainer(closest.Id);
                            //
                            // the container we are going to drop something into must exist
                            //
                            if (containerWeWillDropInto == null)
                            {
                                Logging.Log("MissionController.DropItem", "if (container == null)", Logging.White);
                                return;
                            }

                            //
                            // open the container so we have a window!
                            //
                            if (containerWeWillDropInto.Window == null)
                            {
                                if (closest.OpenCargo())
                                {
                                    Time.Instance.NextOpenContainerInSpaceAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.LootingDelay_milliseconds);
                                }

                                return;
                            }

                            if (!containerWeWillDropInto.Window.IsReady)
                            {
                                Logging.Log("MissionController.DropItem", "LootWrecks: containerWeWillDropInto.Window is not ready, waiting", Logging.White);
                                return;
                            }

                            if (ItemsHaveBeenMoved)
                            {
                                Logging.Log("MissionController.DropItem", "We have Dropped the items: ItemsHaveBeenMoved [" + ItemsHaveBeenMoved + "]", Logging.White);
                                // now that we have completed this action revert OpenWrecks to false
                                //QMCache.Instance.DropMode = false;
                                Nextaction();
                                return;
                            }

                            //
                            // if we are going to drop something into the can we MUST already have it in our cargohold
                            //
                            if (QMCache.Instance.CurrentShipsCargo != null && QMCache.Instance.CurrentShipsCargo.Items.Any())
                            {
                                //int CurrentShipsCargoItemCount = 0;
                                //CurrentShipsCargoItemCount = QMCache.Instance.CurrentShipsCargo.Items.Count();

                                //DirectItem itemsToMove = null;
                                //itemsToMove = QMCache.Instance.CurrentShipsCargo.Items.FirstOrDefault(i => i.TypeName.ToLower() == items.FirstOrDefault().ToLower());
                                //if (itemsToMove == null)
                                //{
                                //    Logging.Log("MissionController.DropItem", "CurrentShipsCargo has [" + CurrentShipsCargoItemCount + "] items. Item We are supposed to move is: [" + items.FirstOrDefault() + "]", Logging.White);
                                //    return;
                                //}

                                int ItemNumber = 0;
                                foreach (DirectItem CurrentShipsCargoItem in QMCache.Instance.CurrentShipsCargo.Items)
                                {
                                    ItemNumber++;
                                    Logging.Log("MissionController.DropItem", "[" + ItemNumber + "] Found [" + CurrentShipsCargoItem.Quantity + "][" + CurrentShipsCargoItem.TypeName + "] in Current Ships Cargo: StackSize: [" + CurrentShipsCargoItem.Stacksize + "] We are looking for: [" + items.FirstOrDefault() + "]", Logging.Debug);
                                    if (items.Any() && items.FirstOrDefault() != null)
                                    {
                                        string NameOfItemToDropIntoContainer = items.FirstOrDefault();
                                        if (NameOfItemToDropIntoContainer != null)
                                        {
                                            if (CurrentShipsCargoItem.TypeName.ToLower() == NameOfItemToDropIntoContainer.ToLower())
                                            {
                                                Logging.Log("MissionController.DropItem", "[" + ItemNumber + "] container.Capacity [" + containerWeWillDropInto.Capacity + "] ItemsHaveBeenMoved [" + ItemsHaveBeenMoved + "]", Logging.Debug);
                                                if (!ItemsHaveBeenMoved)
                                                {
                                                    Logging.Log("MissionController.DropItem", "Moving Items: " + items.FirstOrDefault() + " from cargo ship to " + containerWeWillDropInto.TypeName, Logging.White);
                                                    //
                                                    // THIS IS NOT WORKING - EXCEPTION/ERROR IN CLIENT...
                                                    //
                                                    containerWeWillDropInto.Add(CurrentShipsCargoItem, quantity);
                                                    Time.Instance.NextOpenContainerInSpaceAction = DateTime.UtcNow.AddSeconds(QMCache.Instance.RandomNumber(4, 6));
                                                    ItemsHaveBeenMoved = true;
                                                    return;
                                                }

                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Logging.Log("MissionController.DropItem", "No Items: QMCache.Instance.CurrentShipsCargo.Items.Any()", Logging.Debug);
                            }
                        }
                    }

                    return;
                }

                Logging.Log("MissionController.DropItem", "No entity on grid named: [" + targetEntities.FirstOrDefault() + "]", Logging.Orange);
                // now that we have completed this action revert OpenWrecks to false
                //QMCache.Instance.DropMode = false;
                Nextaction();
                return;
            }
            catch (Exception exception)
            {
                Logging.Log("DropItemAction", "Exception: [" + exception + "]", Logging.Debug);
            }

            return;
        }

        private void LootItemAction(Actions.Action action)
        {
            try
            {
                Salvage.CurrentlyShouldBeSalvaging = true;
                Salvage.MissionLoot = true;
                List<string> targetContainerNames = null;
                if (action.GetParameterValues("target") != null)
                {
                    targetContainerNames = action.GetParameterValues("target");
                }

                if ((targetContainerNames == null || !targetContainerNames.Any()) && Salvage.LootItemRequiresTarget)
                {
                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], " *** No Target Was Specified In the LootItem Action! ***", Logging.Debug);
                }

                List<string> itemsToLoot = null;
                if (action.GetParameterValues("item") != null)
                {
                    itemsToLoot = action.GetParameterValues("item");
                }

                if (itemsToLoot == null)
                {
                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], " *** No Item Was Specified In the LootItem Action! ***", Logging.Debug);
                    Nextaction();
                }

                // if we are not generally looting we need to re-enable the opening of wrecks to
                // find this LootItems we are looking for
                if (NavigateOnGrid.SpeedTank || !NavigateOnGrid.SpeedTank) Salvage.OpenWrecks = true;

                int quantity;
                if (!int.TryParse(action.GetParameterValue("quantity"), out quantity))
                {
                    quantity = 1;
                }

                if (QMCache.Instance.CurrentShipsCargo != null && QMCache.Instance.CurrentShipsCargo.Items.Any(i => itemsToLoot != null && (itemsToLoot.Contains(i.TypeName) && (i.Quantity >= quantity))))
                {
                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "We are done - we have the item(s)", Logging.Teal);

                    // now that we have completed this action revert OpenWrecks to false
                    if (NavigateOnGrid.SpeedTank) Salvage.OpenWrecks = false;
                    Salvage.MissionLoot = false;
                    Salvage.CurrentlyShouldBeSalvaging = false;
                    Nextaction();
                    return;
                }

                //
                // we re-sot by distance on every pulse. The order will be potentially different on each pulse as we move around the field. this is ok and desirable.
                //
                IOrderedEnumerable<EntityCache> containers = QMCache.Instance.Containers.Where(e => !QMCache.Instance.LootedContainers.Contains(e.Id)).OrderByDescending(e => e.GroupId == (int)Group.CargoContainer).ThenBy(e => e.Distance);

                if (!containers.Any())
                {
                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "no containers left to loot, next action", Logging.Teal);
                    if (NavigateOnGrid.SpeedTank) Salvage.OpenWrecks = false;
                    Salvage.MissionLoot = false;
                    Salvage.CurrentlyShouldBeSalvaging = false;
                    Nextaction();
                    return;
                }

                //
                // add containers that we were told to loot into the ListofContainersToLoot so that they are prioritized by the background salvage routine
                //
                if (targetContainerNames != null && targetContainerNames.Any())
                {
                    foreach (EntityCache continerToLoot in containers)
                    {
                        if (targetContainerNames.Any())
                        {
                            foreach (string targetContainerName in targetContainerNames)
                            {
                                if (continerToLoot.Name == targetContainerName)
                                {
                                    if (!QMCache.Instance.ListofContainersToLoot.Contains(continerToLoot.Id))
                                    {
                                        QMCache.Instance.ListofContainersToLoot.Add(continerToLoot.Id);
                                    }
                                }

                                continue;
                            }
                        }
                        else
                        {
                            foreach (EntityCache _unlootedcontainer in QMCache.Instance.UnlootedContainers)
                            {
                                if (continerToLoot.Name == _unlootedcontainer.Name)
                                {
                                    if (!QMCache.Instance.ListofContainersToLoot.Contains(continerToLoot.Id))
                                    {
                                        QMCache.Instance.ListofContainersToLoot.Add(continerToLoot.Id);
                                    }
                                }

                                continue;
                            }
                        }

                        continue;
                    }
                }

                if (itemsToLoot !=null && itemsToLoot.Any())
                {
                    foreach (string _itemToLoot in itemsToLoot)
                    {
                        if (!QMCache.Instance.ListofMissionCompletionItemsToLoot.Contains(_itemToLoot))
                        {
                            QMCache.Instance.ListofMissionCompletionItemsToLoot.Add(_itemToLoot);
                        }
                    }
                }

                EntityCache container;
                if (targetContainerNames != null && targetContainerNames.Any())
                {
                    container = containers.FirstOrDefault(c => targetContainerNames.Contains(c.Name));
                }
                else
                {
                    container = containers.FirstOrDefault();
                }

                if (container != null)
                {
                    if (container.Distance > (int)Distances.SafeScoopRange)
                    {
                        if (QMCache.Instance.Approaching == null || QMCache.Instance.Approaching.Id != container.Id || QMCache.Instance.MyShipEntity.Velocity < 50)
                        {
                            if (container.Approach())
                            {
                                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Approaching target [" + container.Name + "][" + container.MaskedId + "] which is at [" + Math.Round(container.Distance / 1000, 0) + "k away]", Logging.Teal);
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Logging.Log("CombatMissionCtrl.LootItemAction","Exception logged was [" + exception +  "]",Logging.Teal);
                return;
            }
        }

        private void SalvageAction(Actions.Action action)
        {
            List<string> itemsToLoot = null;
            if (action.GetParameterValues("item") != null)
            {
                itemsToLoot = action.GetParameterValues("item");
            }

            int quantity;
            if (!int.TryParse(action.GetParameterValue("quantity"), out quantity))
            {
                quantity = 1;
            }

            if (QMCache.Instance.NormalApproach) QMCache.Instance.NormalApproach = false;

            List<string> targetNames = action.GetParameterValues("target");

            // No parameter? Ignore salvage action
            if (targetNames.Count == 0)
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "No targets defined!", Logging.Teal);
                Nextaction();
                return;
            }

            if (itemsToLoot == null)
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], " *** No Item Was Specified In the Salvage Action! ***", Logging.Debug);
                Nextaction();
            }
            else if (QMCache.Instance.CurrentShipsCargo != null && QMCache.Instance.CurrentShipsCargo.Window.IsReady)
            {
                if (QMCache.Instance.CurrentShipsCargo.Items.Any(i => (itemsToLoot.Contains(i.TypeName) && (i.Quantity >= quantity))))
                {
                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "We are done - we have the item(s)", Logging.Teal);

                    // now that we have completed this action revert OpenWrecks to false
                    if (NavigateOnGrid.SpeedTank) Salvage.OpenWrecks = false;
                    Salvage.MissionLoot = false;
                    Salvage.CurrentlyShouldBeSalvaging = false;
                    Nextaction();
                    return;
                }
            }

            IEnumerable<EntityCache> targets = QMCache.Instance.EntitiesByName(targetNames.FirstOrDefault(), QMCache.Instance.EntitiesOnGrid.ToList()).ToList();
            if (!targets.Any())
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "no entities found named [" + targets.FirstOrDefault() + "] proceeding to next action", Logging.Teal);
                Nextaction();
                return;
            }

            if (Combat.GetBestPrimaryWeaponTarget((double)Distances.OnGridWithMe, false, "combat", Combat.PotentialCombatTargets.OrderBy(t => t.Distance).Take(1).ToList()))
                _clearPocketTimeout = null;

            // Do we have a timeout?  No, set it to now + 5 seconds
            if (!_clearPocketTimeout.HasValue) _clearPocketTimeout = DateTime.UtcNow.AddSeconds(5);

            //
            // how do we determine success here? we assume the 'reward' for salvaging will appear in your cargo, we also assume the mission action will know what that item is called!
            //

            EntityCache closest = targets.OrderBy(t => t.Distance).FirstOrDefault();
            if (closest != null)
            {
                if (!NavigateOnGrid.NavigateToTarget(targets.FirstOrDefault(), "", true, 500)) return;

                if (Salvage.salvagers == null || !Salvage.salvagers.Any())
                {
                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "this action REQUIRES at least 1 salvager! - you may need to use Mission specific fittings to accomplish this", Logging.Teal);
                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "this action REQUIRES at least 1 salvager! - disabling autostart", Logging.Teal);
                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "this action REQUIRES at least 1 salvager! - setting CombatMissionsBehaviorState to GotoBase", Logging.Teal);
                    _States.CurrentCombatMissionBehaviorState = CombatMissionsBehaviorState.GotoBase;
                    QMSettings.Instance.AutoStart = false;
                }
                else if (closest.Distance < Salvage.salvagers.Min(s => s.OptimalRange))
                {
                    if (NavigateOnGrid.SpeedTank) Salvage.OpenWrecks = true;
                    Salvage.CurrentlyShouldBeSalvaging = true;
                    Salvage.TargetWrecks(targets);
                    Salvage.ActivateSalvagers(targets);
                }

                return;
            }

            // Are we in timeout?
            if (DateTime.UtcNow < _clearPocketTimeout.Value) return;

            // We have cleared the Pocket, perform the next action \o/ - reset the timers that we had set for actions...
            Nextaction();

            // Reset timeout
            _clearPocketTimeout = null;
            return;

        }


        private void LootAction(Actions.Action action)
        {
            try
            {
                List<string> items = action.GetParameterValues("item");
                List<string> targetNames = action.GetParameterValues("target");

                // if we are not generally looting we need to re-enable the opening of wrecks to
                // find this LootItems we are looking for
                if (NavigateOnGrid.SpeedTank || !NavigateOnGrid.SpeedTank) Salvage.OpenWrecks = true;
                Salvage.CurrentlyShouldBeSalvaging = true;

                if (!Salvage.LootEverything)
                {
                    if (QMCache.Instance.CurrentShipsCargo != null && QMCache.Instance.CurrentShipsCargo.Items.Any(i => items.Contains(i.TypeName)))
                    {
                        Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "LootEverything:  We are done looting", Logging.Teal);

                        // now that we are done with this action revert OpenWrecks to false
                        if (NavigateOnGrid.SpeedTank) Salvage.OpenWrecks = false;
                        Salvage.MissionLoot = false;
                        Salvage.CurrentlyShouldBeSalvaging = false;
                        Nextaction();
                        return;
                    }
                }

                // unlock targets count
                Salvage.MissionLoot = true;

                //
                // sorting by distance is bad if we are moving (we'd change targets unpredictably)... sorting by ID should be better and be nearly the same(?!)
                //
                IOrderedEnumerable<EntityCache> containers = QMCache.Instance.Containers.Where(e => !QMCache.Instance.LootedContainers.Contains(e.Id)).OrderBy(e => e.Distance);

                if (Logging.DebugLootWrecks)
                {
                    int i = 0;
                    foreach (EntityCache _container in containers)
                    {
                        i++;
                        Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "[" + i + "] " + _container.Name + "[" + Math.Round(_container.Distance / 1000, 0) + "k] isWreckEmpty [" + _container.IsWreckEmpty + "] IsTarget [" + _container.IsTarget + "]", Logging.Debug);
                    }
                }

                if (!containers.Any())
                {
                    // lock targets count
                    Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "We are done looting", Logging.Teal);

                    // now that we are done with this action revert OpenWrecks to false
                    if (NavigateOnGrid.SpeedTank) Salvage.OpenWrecks = false;
                    Salvage.MissionLoot = false;
                    Salvage.CurrentlyShouldBeSalvaging = false;
                    Nextaction();
                    return;
                }

                //
                // add containers that we were told to loot into the ListofContainersToLoot so that they are prioritized by the background salvage routine
                //
                if (targetNames != null && targetNames.Any())
                {
                    foreach (EntityCache continerToLoot in containers)
                    {
                        if (continerToLoot.Name == targetNames.FirstOrDefault())
                        {
                            if (!QMCache.Instance.ListofContainersToLoot.Contains(continerToLoot.Id))
                            {
                                QMCache.Instance.ListofContainersToLoot.Add(continerToLoot.Id);
                            }
                        }
                    }
                }

                EntityCache container = containers.FirstOrDefault(c => targetNames != null && targetNames.Contains(c.Name)) ?? containers.FirstOrDefault();
                if (container != null)
                {
                    if (container.Distance > (int)Distances.SafeScoopRange)
                    {
                        if (QMCache.Instance.Approaching == null || QMCache.Instance.Approaching.Id != container.Id || QMCache.Instance.MyShipEntity.Velocity < 50)
                        {
                            if (container.Approach())
                            {
                                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Approaching target [" + container.Name + "][" + container.MaskedId + "][" + Math.Round(container.Distance / 1000, 0) + "k away]", Logging.Teal);
                                return;
                            }

                            return;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Logging.Log("CombatMissionCtrl.LootAction", "Exception logged was [" + exception + "]", Logging.Teal);
                return;
            }
        }

        private void IgnoreAction(Actions.Action action)
        {
            bool clear;
            if (!bool.TryParse(action.GetParameterValue("clear"), out clear))
                clear = false;

            //List<string> removehighestbty = action.GetParameterValues("RemoveHighestBty");
            //List<string> addhighestbty = action.GetParameterValues("AddHighestBty");

            List<string> add = action.GetParameterValues("add");
            List<string> remove = action.GetParameterValues("remove");

            //string targetNames = action.GetParameterValue("target");

            //int distancetoapp;
            //if (!int.TryParse(action.GetParameterValue("distance"), out distancetoapp))
            //    distancetoapp = 1000;

            //IEnumerable<EntityCache> targets = QMCache.Instance.Entities.Where(e => targetNames.Contains(e.Name));
            // EntityCache target = targets.OrderBy(t => t.Distance).FirstOrDefault();

            //IEnumerable<EntityCache> targetsinrange = QMCache.Instance.Entities.Where(b => QMCache.Instance.DistanceFromEntity(b.X ?? 0, b.Y ?? 0, b.Z ?? 0,target) < distancetoapp);
            //IEnumerable<EntityCache> targetsoutofrange = QMCache.Instance.Entities.Where(b => QMCache.Instance.DistanceFromEntity(b.X ?? 0, b.Y ?? 0, b.Z ?? 0, target) < distancetoapp);

            if (clear)
            {
                IgnoreTargets.Clear();
            }
            else
            {
                add.ForEach(a => IgnoreTargets.Add(a.Trim()));
                remove.ForEach(a => IgnoreTargets.Remove(a.Trim()));
            }
            Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Updated ignore list", Logging.Teal);
            if (IgnoreTargets.Any())
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Currently ignoring: " + IgnoreTargets.Aggregate((current, next) => "[" + current + "][" + next + "]"), Logging.Teal);
            }
            else
            {
                Logging.Log("CombatMissionCtrl[" + PocketNumber + "]." + _pocketActions[_currentAction], "Your ignore list is empty", Logging.Teal);
            }

            Nextaction();
            return;
        }

        private void PerformAction(Actions.Action action)
        {
            switch (action.State)
            {
                case ActionState.LogWhatIsOnGrid:
                    LogWhatIsOnGridAction(action);
                    break;

                case ActionState.Activate:
                    ActivateAction(action);
                    break;

                case ActionState.ClearPocket:
                    ClearPocketAction(action);
                    break;

                case ActionState.ClearAggro:
                    ClearAggroAction(action);
                    break;

                case ActionState.SalvageBookmark:
                    BookmarkPocketForSalvaging();

                    Nextaction();
                    break;

                case ActionState.Done:
                    DoneAction();
                    break;

                case ActionState.AddEcmNpcByName:
                    AddEcmNpcByNameAction(action);
                    break;

                case ActionState.AddWarpScramblerByName:
                    AddWarpScramblerByNameAction(action);
                    break;

                case ActionState.AddWebifierByName:
                    AddWebifierByNameAction(action);
                    break;

                case ActionState.Kill:
                    KillAction(action);
                    break;

                case ActionState.KillOnce:
                    KillAction(action); // TODO Implement
                    break;

                case ActionState.UseDrones:
                    UseDrones(action);
                    break;

                case ActionState.AggroOnly:
                    AggroOnlyAction(action);
                    break;

                case ActionState.KillClosestByName:
                    KillClosestByNameAction(action);
                    break;

                case ActionState.KillClosest:
                    KillClosestAction(action);
                    break;

                case ActionState.MoveTo:
                    MoveToAction(action);
                    break;

                case ActionState.OrbitEntity:
                    OrbitEntityAction(action);
                    break;

                case ActionState.MoveToBackground:
                    MoveToBackgroundAction(action);
                    break;

                case ActionState.ClearWithinWeaponsRangeOnly:
                    ClearWithinWeaponsRangeOnlyAction(action);
                    break;

                case ActionState.ClearWithinWeaponsRangewAggroOnly:
                    ClearWithinWeaponsRangeWithAggroOnlyAction(action);
                    break;

                case ActionState.Salvage:
                    SalvageAction(action);
                    break;

                //case ActionState.Analyze:
                //    AnalyzeAction(action);
                //    break;

                case ActionState.Loot:
                    LootAction(action);
                    break;

                case ActionState.LootItem:
                    LootItemAction(action);
                    break;

                case ActionState.ActivateBastion:
                    ActivateBastionAction(action);
                    break;

                case ActionState.DropItem:
                    DropItemAction(action);
                    break;

                case ActionState.Ignore:
                    IgnoreAction(action);
                    break;

                case ActionState.WaitUntilTargeted:
                    WaitUntilTargeted(action);
                    break;

                case ActionState.WaitUntilAggressed:
                    WaitUntilAggressed(action);
                    break;

                case ActionState.DebuggingWait:
                    DebuggingWait(action);
                    break;
            }
        }

        public static void ReplaceMissionsActions()
        {
            _pocketActions.Clear();

            //
            // Adds actions specified in the Mission XML
            //
            //
            // Clear the Pocket
            _pocketActions.Add(new Actions.Action { State = ActionState.ClearPocket });
            _pocketActions.Add(new Actions.Action { State = ActionState.ClearPocket });
            _pocketActions.AddRange(LoadMissionActions(AgentInteraction.AgentId, PocketNumber, true));

            //we manually add 2 ClearPockets above, then we try to load other mission XMLs for this pocket, if we fail Count will be 2 and we know we need to add an activate and/or a done action.
            if (_pocketActions.Count() == 2)
            {
                // Is there a gate?
                if (QMCache.Instance.AccelerationGates != null && QMCache.Instance.AccelerationGates.Any())
                {
                    // Activate it (Activate action also moves to the gate)
                    _pocketActions.Add(new Actions.Action {State = ActionState.Activate});
                    _pocketActions[_pocketActions.Count - 1].AddParameter("target", "Acceleration Gate");
                }
                else // No, were done
                {
                    _pocketActions.Add(new Actions.Action {State = ActionState.Done});
                }
            }
        }

        public void ProcessState()
        {
            // There is really no combat in stations (yet)
            if (QMCache.Instance.InStation || Logging.DebugDisableCombatMissionCtrl)
                return;

            // if we are not in space yet, wait...
            if (!QMCache.Instance.InSpace)
                return;

            // What? No ship entity?
            if (QMCache.Instance.ActiveShip.Entity == null)
                return;

            // There is no combat when cloaked
            if (QMCache.Instance.ActiveShip.Entity.IsCloaked)
                return;

            switch (_States.CurrentCombatMissionCtrlState)
            {
                case CombatMissionCtrlState.Idle:
                    break;

                case CombatMissionCtrlState.Done:
                    Statistics.WritePocketStatistics();

                    if (!QMCache.Instance.NormalApproach)
                        QMCache.Instance.NormalApproach = true;

                    IgnoreTargets.Clear();
                    break;

                case CombatMissionCtrlState.Error:
                    break;

                case CombatMissionCtrlState.Start:
                    PocketNumber = 0;

                    // Update statistic values
                    QMCache.Instance.WealthatStartofPocket = QMCache.Instance.DirectEve.Me.Wealth;
                    Statistics.StartedPocket = DateTime.UtcNow;

                    // Update UseDrones from settings (this can be overridden with a mission action named UseDrones)
                    MissionSettings.MissionUseDrones = null;
                    MissionSettings.PocketUseDrones = null;

                    // Reset notNormalNav and onlyKillAggro to false
                    QMCache.Instance.normalNav = true;
                    QMCache.Instance.onlyKillAggro = false;

                    // Update x/y/z so that NextPocket wont think we are there yet because its checking (very) old x/y/z cords
                    _lastX = QMCache.Instance.ActiveShip.Entity.X;
                    _lastY = QMCache.Instance.ActiveShip.Entity.Y;
                    _lastZ = QMCache.Instance.ActiveShip.Entity.Z;

                    _States.CurrentCombatMissionCtrlState = CombatMissionCtrlState.LoadPocket;
                    break;

                case CombatMissionCtrlState.LoadPocket:
                    _pocketActions.Clear();
                    _pocketActions.AddRange(LoadMissionActions(AgentInteraction.AgentId, PocketNumber, true));

                    //
                    // LogStatistics();
                    //
                    if (_pocketActions.Count == 0)
                    {
                        // No Pocket action, load default actions
                        Logging.Log("CombatMissionCtrl", "No mission actions specified, loading default actions", Logging.Orange);

                        // Wait for 30 seconds to be targeted
                        _pocketActions.Add(new Actions.Action { State = ActionState.WaitUntilTargeted });
                        _pocketActions[0].AddParameter("timeout", "15");

                        // Clear the Pocket
                        _pocketActions.Add(new Actions.Action { State = ActionState.ClearPocket });

                        // Is there a gate?
                        if (QMCache.Instance.AccelerationGates != null && QMCache.Instance.AccelerationGates.Any())
                        {
                            // Activate it (Activate action also moves to the gate)
                            _pocketActions.Add(new Actions.Action { State = ActionState.Activate });
                            _pocketActions[_pocketActions.Count - 1].AddParameter("target", "Acceleration Gate");
                        }
                        else // No, were done
                            _pocketActions.Add(new Actions.Action { State = ActionState.Done });
                    }

                    Logging.Log("-", "-----------------------------------------------------------------", Logging.Teal);
                    Logging.Log("-", "-----------------------------------------------------------------", Logging.Teal);
                    Logging.Log("CombatMissionCtrl", "Mission Timer Currently At: [" + Math.Round(DateTime.UtcNow.Subtract(Statistics.StartedMission).TotalMinutes, 0) + "] min", Logging.Teal);

                    //if (QMCache.Instance.OptimalRange != 0)
                    //    Logging.Log("Optimal Range is set to: " + (QMCache.Instance.OrbitDistance / 1000).ToString(CultureInfo.InvariantCulture) + "k");
                    Logging.Log("CombatMissionCtrl", "Max Range is currently: " + (Combat.MaxRange / 1000).ToString(CultureInfo.InvariantCulture) + "k", Logging.Teal);
                    Logging.Log("-", "-----------------------------------------------------------------", Logging.Teal);
                    Logging.Log("-", "-----------------------------------------------------------------", Logging.Teal);
                    Logging.Log("CombatMissionCtrl", "Pocket [" + PocketNumber + "] loaded, executing the following actions", Logging.Orange);
                    int pocketActionCount = 1;
                    foreach (Actions.Action a in _pocketActions)
                    {
                        Logging.Log("CombatMissionCtrl", "Action [ " + pocketActionCount + " ] " + a, Logging.Teal);
                        pocketActionCount++;
                    }
                    Logging.Log("-", "-----------------------------------------------------------------", Logging.Teal);
                    Logging.Log("-", "-----------------------------------------------------------------", Logging.Teal);

                    // Reset pocket information
                    _currentAction = 0;
                    Drones.IsMissionPocketDone = false;
                    if (NavigateOnGrid.SpeedTank) Salvage.OpenWrecks = false;
                    if (!NavigateOnGrid.SpeedTank) Salvage.OpenWrecks = true;

                    IgnoreTargets.Clear();
                    Statistics.PocketObjectStatistics(QMCache.Instance.Objects.ToList());
                    _States.CurrentCombatMissionCtrlState = CombatMissionCtrlState.ExecutePocketActions;
                    break;

                case CombatMissionCtrlState.ExecutePocketActions:
                    if (_currentAction >= _pocketActions.Count)
                    {
                        // No more actions, but we're not done?!?!?!
                        Logging.Log("CombatMissionCtrl", "We're out of actions but did not process a 'Done' or 'Activate' action", Logging.Red);

                        _States.CurrentCombatMissionCtrlState = CombatMissionCtrlState.Error;
                        break;
                    }

                    Actions.Action action = _pocketActions[_currentAction];
                    if (action.ToString() != QMCache.Instance.CurrentPocketAction)
                    {
                        QMCache.Instance.CurrentPocketAction = action.ToString();
                    }
                    int currentAction = _currentAction;
                    PerformAction(action);

                    if (currentAction != _currentAction)
                    {
                        Logging.Log("CombatMissionCtrl", "Finished Action." + action, Logging.Yellow);

                        if (_currentAction < _pocketActions.Count)
                        {
                            action = _pocketActions[_currentAction];
                            Logging.Log("CombatMissionCtrl", "Starting Action." + action, Logging.Yellow);
                        }
                    }

                    break;

                case CombatMissionCtrlState.NextPocket:
                    double distance = QMCache.Instance.DistanceFromMe(_lastX, _lastY, _lastZ);
                    if (distance > (int)Distances.NextPocketDistance)
                    {
                        Logging.Log("CombatMissionCtrl", "We have moved to the next Pocket [" + Math.Round(distance / 1000, 0) + "k away]", Logging.Green);

                        // If we moved more then 100km, assume next Pocket
                        PocketNumber++;
                        _States.CurrentCombatMissionCtrlState = CombatMissionCtrlState.LoadPocket;
                        Statistics.WritePocketStatistics();
                    }
                    else if (DateTime.UtcNow.Subtract(_moveToNextPocket).TotalMinutes > 2)
                    {
                        Logging.Log("CombatMissionCtrl", "We have timed out, retry last action", Logging.Orange);

                        // We have reached a timeout, revert to ExecutePocketActions (e.g. most likely Activate)
                        _States.CurrentCombatMissionCtrlState = CombatMissionCtrlState.ExecutePocketActions;
                    }
                    break;
            }

            double newX = QMCache.Instance.ActiveShip.Entity.X;
            double newY = QMCache.Instance.ActiveShip.Entity.Y;
            double newZ = QMCache.Instance.ActiveShip.Entity.Z;

            // For some reason x/y/z returned 0 sometimes
            if (newX != 0 && newY != 0 && newZ != 0)
            {
                // Save X/Y/Z so that NextPocket can check if we actually went to the next Pocket :)
                _lastX = newX;
                _lastY = newY;
                _lastZ = newZ;
            }
        }

        /// <summary>
        ///   Loads mission objectives from XML file
        /// </summary>
        /// <param name = "agentId"> </param>
        /// <param name = "pocketId"> </param>
        /// <param name = "missionMode"> </param>
        /// <returns></returns>
        public static IEnumerable<Actions.Action> LoadMissionActions(long agentId, int pocketId, bool missionMode)
        {
            try
            {

                DirectAgentMission missiondetails = QMCache.Instance.GetAgentMission(agentId, false);
                if (missiondetails == null && missionMode)
                {
                    return new Actions.Action[0];
                }

                if (missiondetails != null)
                {
                    MissionSettings.SetmissionXmlPath(Logging.FilterPath(missiondetails.Name));
                    if (!File.Exists(MissionSettings.MissionXmlPath))
                    {
                        //No mission file but we need to set some cache settings
                        MissionSettings.MissionOrbitDistance = null;
                        MissionSettings.MissionOptimalRange = null;
                        MissionSettings.MissionUseDrones = null;
                        QMCache.Instance.AfterMissionSalvaging = Salvage.AfterMissionSalvaging;
                        return new Actions.Action[0];
                    }

                    //
                    // this loads the settings from each pocket... but NOT any settings global to the mission
                    //
                    try
                    {
                        XDocument xdoc = XDocument.Load(MissionSettings.MissionXmlPath);
                        if (xdoc.Root != null)
                        {
                            XElement xElement = xdoc.Root.Element("pockets");
                            if (xElement != null)
                            {
                                IEnumerable<XElement> pockets = xElement.Elements("pocket");
                                foreach (XElement pocket in pockets)
                                {
                                    if ((int)pocket.Attribute("id") != pocketId)
                                    {
                                        continue;
                                    }

                                    if (pocket.Element("orbitentitynamed") != null)
                                    {
                                        QMCache.Instance.OrbitEntityNamed = (string)pocket.Element("orbitentitynamed");
                                    }

                                    if (pocket.Element("damagetype") != null)
                                    {
                                        MissionSettings.PocketDamageType = (DamageType)Enum.Parse(typeof(DamageType), (string)pocket.Element("damagetype"), true);
                                    }

                                    if (pocket.Element("orbitdistance") != null) 	//Load OrbitDistance from mission.xml, if present
                                    {
                                        MissionSettings.MissionOrbitDistance = (int)pocket.Element("orbitdistance");
                                        Logging.Log("Cache", "Using Mission Orbit distance [" + NavigateOnGrid.OrbitDistance + "]", Logging.White);
                                    }
                                    else //Otherwise, use value defined in charname.xml file
                                    {
                                        MissionSettings.MissionOrbitDistance = null;
                                        Logging.Log("Cache", "Using Settings Orbit distance [" + NavigateOnGrid.OrbitDistance + "]", Logging.White);
                                    }

                                    if (pocket.Element("optimalrange") != null) 	//Load OrbitDistance from mission.xml, if present
                                    {
                                        MissionSettings.MissionOptimalRange = (int)pocket.Element("optimalrange");
                                        Logging.Log("Cache", "Using Mission OptimalRange [" + NavigateOnGrid.OptimalRange + "]", Logging.White);
                                    }
                                    else //Otherwise, use value defined in charname.xml file
                                    {
                                        MissionSettings.MissionOptimalRange = null;
                                        Logging.Log("Cache", "Using Settings OptimalRange [" + NavigateOnGrid.OptimalRange + "]", Logging.White);
                                    }

                                    if (pocket.Element("afterMissionSalvaging") != null) 	//Load afterMissionSalvaging setting from mission.xml, if present
                                    {
                                        QMCache.Instance.AfterMissionSalvaging = (bool)pocket.Element("afterMissionSalvaging");
                                    }

                                    if (pocket.Element("dronesKillHighValueTargets") != null) 	//Load afterMissionSalvaging setting from mission.xml, if present
                                    {
                                        MissionSettings.MissionDronesKillHighValueTargets = (bool)pocket.Element("dronesKillHighValueTargets");
                                    }
                                    else //Otherwise, use value defined in charname.xml file
                                    {
                                        MissionSettings.MissionDronesKillHighValueTargets = null;
                                    }

                                    List<Actions.Action> actions = new List<Actions.Action>();
                                    XElement elements = pocket.Element("actions");
                                    if (elements != null)
                                    {
                                        foreach (XElement element in elements.Elements("action"))
                                        {
                                            Actions.Action action = new Actions.Action
                                            {
                                                State = (ActionState)Enum.Parse(typeof(ActionState), (string)element.Attribute("name"), true)
                                            };
                                            XAttribute xAttribute = element.Attribute("name");
                                            if (xAttribute != null && xAttribute.Value == "ClearPocket")
                                            {
                                                action.AddParameter("", "");
                                            }
                                            else
                                            {
                                                foreach (XElement parameter in element.Elements("parameter"))
                                                {
                                                    action.AddParameter((string)parameter.Attribute("name"), (string)parameter.Attribute("value"));
                                                }
                                            }
                                            actions.Add(action);
                                        }
                                    }

                                    return actions;
                                }

                                //actions.Add(action);
                            }
                            else
                            {
                                return new Actions.Action[0];
                            }
                        }
                        else
                        {
                            { return new Actions.Action[0]; }
                        }

                        // if we reach this code there is no mission XML file, so we set some things -- Assail

                        MissionSettings.MissionOptimalRange = null;
                        MissionSettings.MissionOrbitDistance = null;
                        Logging.Log("Cache", "Using Settings Orbit distance [" + NavigateOnGrid.OrbitDistance + "]", Logging.White);

                        return new Actions.Action[0];
                    }
                    catch (Exception ex)
                    {
                        Logging.Log("Cache", "Error loading mission XML file [" + ex.Message + "]", Logging.Orange);
                        return new Actions.Action[0];
                    }
                }
                return new Actions.Action[0];
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.LoadMissionActions", "Exception [" + exception + "]", Logging.Debug);
                return null;
            }
        }
    }
}


namespace ILEF.BackgroundTasks
{
    using System;
    using global::ILEF.Caching;
    using global::ILEF.Combat;
    using global::ILEF.Logging;
    using global::ILEF.Lookup;
    using System.Linq;
    using System.Collections.Generic;
    using ILoveEVE.Framework;

    public static class NavigateOnGrid
    {
        public static DateTime AvoidBumpingThingsTimeStamp = Time.Instance.StartTime;
        public static int SafeDistanceFromStructureMultiplier = 1;
        public static bool AvoidBumpingThingsWarningSent = false;
        public static DateTime NextNavigateIntoRange = DateTime.UtcNow;
        public static bool AvoidBumpingThingsBool { get; set; }
        public static bool SpeedTank { get; set; }
        private static int? _orbitDistance;
        public static int OrbitDistance
        {
            get
            {
                if (MissionSettings.MissionOrbitDistance != null)
                {
                    return (int)MissionSettings.MissionOrbitDistance;
                }

                return _orbitDistance ?? 2000;
            }
            set
            {
                _orbitDistance = value;
            }
        }
        public static bool OrbitStructure { get; set; }
        private static int? _optimalRange { get; set; }
        public static int OptimalRange
        {
            get
            {
                if (MissionSettings.MissionOptimalRange != null)
                {
                    return (int)MissionSettings.MissionOptimalRange;
                }

                return _optimalRange ?? 10000;
            }
            set
            {
                _optimalRange = value;
            }
        }

        public static bool AvoidBumpingThings(EntityCache thisBigObject, string module)
        {
            if (AvoidBumpingThingsBool)
            {
                //if It has not been at least 60 seconds since we last session changed do not do anything
                if (QMCache.Instance.InStation || !QMCache.Instance.InSpace || QMCache.Instance.ActiveShip.Entity.IsCloaked || (QMCache.Instance.InSpace && Time.Instance.LastSessionChange.AddSeconds(60) < DateTime.UtcNow))
                    return false;

                //we cant move in bastion mode, do not try
                List<ModuleCache> bastionModules = null;
                bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
                if (bastionModules.Any(i => i.IsActive)) return false;


                if (QMCache.Instance.ClosestStargate != null && QMCache.Instance.ClosestStargate.Distance < 9000)
                {
                    //
                    // if we are 'close' to a stargate or a station do not attempt to do any collision avoidance, as its unnecessary that close to a station or gate!
                    //
                    return false;
                }

                if (QMCache.Instance.ClosestStation != null && QMCache.Instance.ClosestStation.Distance < 11000)
                {
                    //
                    // if we are 'close' to a stargate or a station do not attempt to do any collision avoidance, as its unnecessary that close to a station or gate!
                    //
                    return false;
                }

                //EntityCache thisBigObject = Cache.Instance.BigObjects.FirstOrDefault();
                if (thisBigObject != null)
                {
                    //
                    // if we are "too close" to the bigObject move away... (is orbit the best thing to do here?)
                    //
                    if (thisBigObject.Distance >= (int)Distances.TooCloseToStructure)
                    {
                        //we are no longer "too close" and can proceed.
                        AvoidBumpingThingsTimeStamp = DateTime.UtcNow;
                        SafeDistanceFromStructureMultiplier = 1;
                        AvoidBumpingThingsWarningSent = false;
                    }
                    else
                    {
                        if (DateTime.UtcNow > Time.Instance.NextOrbit)
                        {
                            if (DateTime.UtcNow > AvoidBumpingThingsTimeStamp.AddSeconds(30))
                            {
                                if (SafeDistanceFromStructureMultiplier <= 4)
                                {
                                    //
                                    // for simplicities sake we reset this timestamp every 30 sec until the multiplier hits 5 then it should stay static until we are not "too close" anymore
                                    //
                                    AvoidBumpingThingsTimeStamp = DateTime.UtcNow;
                                    SafeDistanceFromStructureMultiplier++;
                                }

                                if (DateTime.UtcNow > AvoidBumpingThingsTimeStamp.AddMinutes(5) && !AvoidBumpingThingsWarningSent)
                                {
                                    Logging.Log("NavigateOnGrid", "We are stuck on a object and have been trying to orbit away from it for over 5 min", Logging.Orange);
                                    AvoidBumpingThingsWarningSent = true;
                                }

                                if (DateTime.UtcNow > AvoidBumpingThingsTimeStamp.AddMinutes(15))
                                {
                                    QMCache.Instance.CloseQuestorCMDLogoff = false;
                                    QMCache.Instance.CloseQuestorCMDExitGame = true;
                                    Cleanup.ReasonToStopQuestor = "navigateOnGrid: We have been stuck on an object for over 15 min";
                                    Logging.Log("ReasonToStopQuestor", Cleanup.ReasonToStopQuestor, Logging.Yellow);
                                    Cleanup.SignalToQuitQuestorAndEVEAndRestartInAMoment = true;
                                }
                            }

                            if (thisBigObject.Orbit((int) Distances.SafeDistancefromStructure * SafeDistanceFromStructureMultiplier))
                            {
                                Logging.Log(module, ": initiating Orbit of [" + thisBigObject.Name + "] orbiting at [" + ((int)Distances.SafeDistancefromStructure * SafeDistanceFromStructureMultiplier) + "]", Logging.White);
                                return true;
                            }

                            return false;
                        }

                        return false;
                        //we are still too close, do not continue through the rest until we are not "too close" anymore
                    }

                    return false;
                }

                return false;
            }

            return false;
        }

        public static void OrbitGateorTarget(EntityCache target, string module)
        {
            int OrbitDistanceToUse = NavigateOnGrid.OrbitDistance;
            if (!Combat.PotentialCombatTargets.Any())
            {
                OrbitDistanceToUse = 500;
            }

            if (DateTime.UtcNow > Time.Instance.NextOrbit)
            {
                //we cant move in bastion mode, do not try
                List<ModuleCache> bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
                if (bastionModules.Any(i => i.IsActive)) return;

                if (Logging.DebugNavigateOnGrid) Logging.Log("NavigateOnGrid", "OrbitGateorTarget Started", Logging.White);
                if (OrbitDistanceToUse == 0)
                {
                    OrbitDistanceToUse = 2000;
                }

                if (target.Distance + OrbitDistanceToUse < Combat.MaxRange - 5000)
                {
                    if (Logging.DebugNavigateOnGrid) Logging.Log("NavigateOnGrid", "if (target.Distance + QMCache.Instance.OrbitDistance < Combat.MaxRange - 5000)", Logging.White);

                    //Logging.Log("CombatMissionCtrl." + _pocketActions[_currentAction] ,"StartOrbiting: Target in range");
                    if (!QMCache.Instance.IsApproachingOrOrbiting(target.Id))
                    {
                        if (Logging.DebugNavigateOnGrid) Logging.Log("CombatMissionCtrl.NavigateIntoRange", "We are not approaching nor orbiting", Logging.Teal);

                        //
                        // Prefer to orbit the last structure defined in
                        // QMCache.Instance.OrbitEntityNamed
                        //
                        EntityCache structure = null;
                        if (!string.IsNullOrEmpty(QMCache.Instance.OrbitEntityNamed))
                        {
                            structure = QMCache.Instance.EntitiesOnGrid.Where(i => i.Name.Contains(QMCache.Instance.OrbitEntityNamed)).OrderBy(t => t.Distance).FirstOrDefault();
                        }

                        if (structure == null)
                        {
                            structure = QMCache.Instance.EntitiesOnGrid.Where(i => i.Name.Contains("Gate")).OrderBy(t => t.Distance).FirstOrDefault();
                        }

                        if (NavigateOnGrid.OrbitStructure && structure != null)
                        {
                            if (structure.Orbit(OrbitDistanceToUse))
                            {
                                Logging.Log(module, "Initiating Orbit [" + structure.Name + "][at " + Math.Round((double)OrbitDistanceToUse / 1000, 0) + "k][" + structure.MaskedId + "]", Logging.Teal);
                                return;
                            }

                            return;
                        }

                        //
                        // OrbitStructure is false
                        //
                        if (NavigateOnGrid.SpeedTank)
                        {
                            if (target.Orbit(OrbitDistanceToUse))
                            {
                                Logging.Log(module, "Initiating Orbit [" + target.Name + "][at " + Math.Round((double)OrbitDistanceToUse / 1000, 0) + "k][ID: " + target.MaskedId + "]", Logging.Teal);
                                return;
                            }

                            return;
                        }

                        //
                        // OrbitStructure is false
                        // SpeedTank is false
                        //
                        if (QMCache.Instance.MyShipEntity.Velocity < 300) //this will spam a bit until we know what "mode" our active ship is when aligning
                        {
                            if (Combat.DoWeCurrentlyHaveTurretsMounted())
                            {
                                if (QMCache.Instance.Star.AlignTo())
                                {
                                    Logging.Log(module, "Aligning to the Star so we might possibly hit [" + target.Name + "][ID: " + target.MaskedId + "][ActiveShip.Entity.Mode:[" + QMCache.Instance.ActiveShip.Entity.Mode + "]", Logging.Teal);
                                    return;
                                }

                                return;
                            }
                        }
                    }
                }
                else
                {
                    if (target.Orbit(OrbitDistanceToUse))
                    {
                        Logging.Log(module, "Out of range. ignoring orbit around structure.", Logging.Teal);
                        return;
                    }

                    return;
                }

                return;
            }
        }

        public static void NavigateIntoRange(EntityCache target, string module, bool moveMyShip)
        {
            if (!QMCache.Instance.InSpace || (QMCache.Instance.InSpace && QMCache.Instance.InWarp) || !moveMyShip)
                return;

            if (DateTime.UtcNow < NextNavigateIntoRange || Logging.DebugDisableNavigateIntoRange)
                return;

            NextNavigateIntoRange = DateTime.UtcNow.AddSeconds(5);

            //we cant move in bastion mode, do not try
            List<ModuleCache> bastionModules = null;
            bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
            if (bastionModules.Any(i => i.IsActive)) return;

            if (Logging.DebugNavigateOnGrid) Logging.Log("NavigateOnGrid", "NavigateIntoRange Started", Logging.White);

            //if (QMCache.Instance.OrbitDistance != 0)
            //    Logging.Log("CombatMissionCtrl", "Orbit Distance is set to: " + (QMCache.Instance.OrbitDistance / 1000).ToString(CultureInfo.InvariantCulture) + "k", Logging.teal);

            NavigateOnGrid.AvoidBumpingThings(QMCache.Instance.BigObjectsandGates.FirstOrDefault(), "NavigateOnGrid: NavigateIntoRange");

            if (NavigateOnGrid.SpeedTank)
            {
                if (target.Distance > Combat.MaxRange && !QMCache.Instance.IsApproaching(target.Id))
                {
                    if (target.KeepAtRange((int) (Combat.MaxRange*0.8d)))
                    {
                        if (Logging.DebugNavigateOnGrid) Logging.Log("NavigateOnGrid", "NavigateIntoRange: SpeedTank: Moving into weapons range before initiating orbit", Logging.Teal);
                    }

                    return;
                }

                if (target.Distance < Combat.MaxRange && !QMCache.Instance.IsOrbiting(target.Id))
                {
                    if (Logging.DebugNavigateOnGrid) Logging.Log("NavigateOnGrid", "NavigateIntoRange: SpeedTank: orbitdistance is [" + NavigateOnGrid.OrbitDistance + "]", Logging.White);
                    OrbitGateorTarget(target, module);
                    return;
                }

                return;
            }
            else //if we are not speed tanking then check optimalrange setting, if that is not set use the less of targeting range and weapons range to dictate engagement range
            {
                if (DateTime.UtcNow > Time.Instance.NextApproachAction)
                {
                    //if optimalrange is set - use it to determine engagement range
                    if (NavigateOnGrid.OptimalRange != 0)
                    {
                        if (Logging.DebugNavigateOnGrid) Logging.Log("NavigateOnGrid", "NavigateIntoRange: OptimalRange [ " + NavigateOnGrid.OptimalRange + "] Current Distance to [" + target.Name + "] is [" + Math.Round(target.Distance / 1000, 0) + "]", Logging.White);

                        if (target.Distance > NavigateOnGrid.OptimalRange + (int)Distances.OptimalRangeCushion)
                        {
                            if ((QMCache.Instance.Approaching == null || QMCache.Instance.Approaching.Id != target.Id) || QMCache.Instance.MyShipEntity.Velocity < 50)
                            {
                                if (target.IsNPCFrigate && Combat.DoWeCurrentlyHaveTurretsMounted())
                                {
                                    if (Logging.DebugNavigateOnGrid) Logging.Log("NavigateOnGrid", "NavigateIntoRange: target is NPC Frigate [" + target.Name + "][" + Math.Round(target.Distance / 1000, 0) + "]", Logging.White);
                                    OrbitGateorTarget(target, module);
                                    return;
                                }

                                if (target.KeepAtRange(NavigateOnGrid.OptimalRange))
                                {
                                    Logging.Log(module, "Using Optimal Range: Approaching target [" + target.Name + "][ID: " + target.MaskedId + "][" + Math.Round(target.Distance / 1000, 0) + "k away]", Logging.Teal);
                                }

                                return;
                            }
                        }

                        if (target.Distance <= NavigateOnGrid.OptimalRange)
                        {
                            if (target.IsNPCFrigate && Combat.DoWeCurrentlyHaveTurretsMounted())
                            {
                                if ((QMCache.Instance.Approaching == null || QMCache.Instance.Approaching.Id != target.Id) || QMCache.Instance.MyShipEntity.Velocity < 50)
                                {
                                    if (target.KeepAtRange(NavigateOnGrid.OptimalRange))
                                    {
                                        Logging.Log(module, "Target is NPC Frigate and we got Turrets. Keeping target at Range to hit it.", Logging.Teal);
                                        Logging.Log(module, "Initiating KeepAtRange [" + target.Name + "][at " + Math.Round((double)NavigateOnGrid.OptimalRange / 1000, 0) + "k][ID: " + target.MaskedId + "]", Logging.Teal);
                                    }
                                    return;
                                }
                            }
                            else if (QMCache.Instance.Approaching != null && QMCache.Instance.MyShipEntity.Velocity != 0)
                            {
                                if (target.IsNPCFrigate && Combat.DoWeCurrentlyHaveTurretsMounted()) return;

                                StopMyShip();
                                Logging.Log(module, "Using Optimal Range: Stop ship, target at [" + Math.Round(target.Distance / 1000, 0) + "k away] is inside optimal", Logging.Teal);
                                return;
                            }
                        }
                    }
                    else //if optimalrange is not set use MaxRange (shorter of weapons range and targeting range)
                    {
                        if (Logging.DebugNavigateOnGrid) Logging.Log("NavigateOnGrid", "NavigateIntoRange: using MaxRange [" + Combat.MaxRange + "] target is [" + target.Name + "][" + target.Distance + "]", Logging.White);

                        if (target.Distance > Combat.MaxRange)
                        {
                            if (QMCache.Instance.Approaching == null || QMCache.Instance.Approaching.Id != target.Id || QMCache.Instance.MyShipEntity.Velocity < 50)
                            {
                                if (target.IsNPCFrigate && Combat.DoWeCurrentlyHaveTurretsMounted())
                                {
                                    if (Logging.DebugNavigateOnGrid) Logging.Log("NavigateOnGrid", "NavigateIntoRange: target is NPC Frigate [" + target.Name + "][" + target.Distance + "]", Logging.White);
                                    OrbitGateorTarget(target, module);
                                    return;
                                }

                                if (target.KeepAtRange((int) (Combat.MaxRange*0.8d)))
                                {
                                    Logging.Log(module, "Using Weapons Range * 0.8d [" + Math.Round(Combat.MaxRange * 0.8d / 1000, 0) + " k]: Approaching target [" + target.Name + "][ID: " + target.MaskedId + "][" + Math.Round(target.Distance / 1000, 0) + "k away]", Logging.Teal);
                                }

                                return;
                            }
                        }

                        //I think when approach distance will be reached ship will be stopped so this is not needed
                        if (target.Distance <= Combat.MaxRange - 5000 && QMCache.Instance.Approaching != null)
                        {
                            if (target.IsNPCFrigate && Combat.DoWeCurrentlyHaveTurretsMounted())
                            {
                                if (Logging.DebugNavigateOnGrid) Logging.Log("NavigateOnGrid", "NavigateIntoRange: target is NPC Frigate [" + target.Name + "][" + target.Distance + "]", Logging.White);
                                OrbitGateorTarget(target, module);
                                return;
                            }
                            if (QMCache.Instance.MyShipEntity.Velocity != 0) StopMyShip();
                            Logging.Log(module, "Using Weapons Range: Stop ship, target is more than 5k inside weapons range", Logging.Teal);
                            return;
                        }

                        if (target.Distance <= Combat.MaxRange && QMCache.Instance.Approaching == null)
                        {
                            if (target.IsNPCFrigate && Combat.DoWeCurrentlyHaveTurretsMounted())
                            {
                                if (Logging.DebugNavigateOnGrid) Logging.Log("NavigateOnGrid", "NavigateIntoRange: target is NPC Frigate [" + target.Name + "][" + target.Distance + "]", Logging.White);
                                OrbitGateorTarget(target, module);
                                return;
                            }
                        }
                    }
                    return;
                }
            }
        }

        public static void StopMyShip()
        {
            if (DateTime.UtcNow > Time.Instance.NextApproachAction)
            {
                Time.Instance.NextApproachAction = DateTime.UtcNow.AddSeconds(Time.Instance.ApproachDelay_seconds);
                QMCache.Instance.DirectEve.ExecuteCommand(DirectCmd.CmdStopShip);
                QMCache.Instance.Approaching = null;
            }
        }

        public static bool NavigateToTarget(EntityCache target, string module, bool orbit, int DistanceFromTarget)  //this needs to accept a distance parameter....
        {
            // if we are inside warpto range you need to approach (you cant warp from here)
            if (target.Distance < (int) Distances.WarptoDistance)
            {
                if (orbit)
                {
                    if (target.Distance < DistanceFromTarget)
                    {
                        return true;
                    }

                    if (DateTime.UtcNow > Time.Instance.NextOrbit)
                    {
                        //we cant move in bastion mode, do not try
                        List<ModuleCache> bastionModules = null;
                        bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
                        if (bastionModules.Any(i => i.IsActive)) return false;

                        Logging.Log(module, "StartOrbiting: Target in range", Logging.Teal);
                        if (!QMCache.Instance.IsApproachingOrOrbiting(target.Id))
                        {
                            Logging.Log("CombatMissionCtrl.NavigateToObject", "We are not approaching nor orbiting", Logging.Teal);
                            if (target.Orbit(DistanceFromTarget - 1500))
                            {
                                Logging.Log(module, "Initiating Orbit [" + target.Name + "][ID: " + target.MaskedId + "]", Logging.Teal);
                                return false;
                            }

                            return false;
                        }
                    }
                }
                else //if we are not speed tanking then check optimalrange setting, if that is not set use the less of targeting range and weapons range to dictate engagement range
                {
                    if (DateTime.UtcNow > Time.Instance.NextApproachAction)
                    {
                        if (target.Distance < DistanceFromTarget)
                        {
                            return true;
                        }

                        if (QMCache.Instance.Approaching == null || QMCache.Instance.Approaching.Id != target.Id || QMCache.Instance.MyShipEntity.Velocity < 50)
                        {
                            if (target.KeepAtRange(DistanceFromTarget - 1500))
                            {
                                Logging.Log(module, "Using SafeDistanceFromStructure: Approaching target [" + target.Name + "][ID: " + target.MaskedId + "][" + Math.Round(target.Distance / 1000, 0) + "k away]", Logging.Teal);
                            }

                            return false;
                        }

                        return false;
                    }

                    return false;
                }

            }
            // Probably never happens
            if (target.AlignTo())
            {
                return false;
            }

            return false;
        }
    }
}
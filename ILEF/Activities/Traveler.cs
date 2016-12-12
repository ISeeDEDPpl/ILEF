// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------

namespace Questor.Modules.Activities
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using DirectEve;
    using global::Questor.Modules.Actions;
    using global::Questor.Modules.BackgroundTasks;
    using global::Questor.Modules.Caching;
    using global::Questor.Modules.Combat;
    using global::Questor.Modules.Logging;
    using global::Questor.Modules.Lookup;
    using global::Questor.Modules.States;

    public static class Traveler
    {
        private static TravelerDestination _destination;
        private static DateTime _lastTravelerPulse;
        private static DateTime _nextGetLocation;
        
        private static List<int> _destinationRoute;
        public static DirectLocation _location;
        private static IEnumerable<DirectBookmark> myHomeBookmarks;
        private static string _locationName;
        private static int _locationErrors;
        
        static Traveler()
        {
            _lastTravelerPulse = DateTime.MinValue;
        }

        public static TravelerDestination Destination
        {
            get { return _destination; }
            set
            {
                _destination = value;
                _States.CurrentTravelerState = _destination == null ? TravelerState.AtDestination : TravelerState.Idle;
            }
        }

        /// <summary>
        ///   Set destination to a solar system
        /// </summary>
        public static bool SetStationDestination(long stationId)
        {
            _location = Cache.Instance.DirectEve.Navigation.GetLocation(stationId);
            if (Logging.DebugTraveler) Logging.Log("Traveler", "Location = [" + Logging.Yellow + Cache.Instance.DirectEve.Navigation.GetLocationName(stationId) + Logging.Green + "]", Logging.Green);
            if (_location != null && _location.IsValid)
            {
                _locationErrors = 0;
                if (Logging.DebugTraveler) Logging.Log("Traveler", "Setting destination to [" + Logging.Yellow + _location.Name + Logging.Green + "]", Logging.Teal);
                try
                {
                    _location.SetDestination();
                }
                catch (Exception)
                {
                    Logging.Log("Traveler", "SetStationDestination: set destination to [" + _location.ToString() + "] failed ", Logging.Debug);
                }
                return true;
            }

            Logging.Log("Traveler", "Error setting station destination [" + Logging.Yellow + stationId + Logging.Green + "]", Logging.Green);
            _locationErrors++;
            if (_locationErrors > 20)
            {
                return false;
            }
            return false;
        }

        /// <summary>
        ///   Navigate to a solar system
        /// </summary>
        /// <param name = "solarSystemId"></param>
        private static void NavigateToBookmarkSystem(long solarSystemId)
        {
            if (Time.Instance.NextTravelerAction > DateTime.UtcNow)
            {
                if (Logging.DebugTraveler) Logging.Log("Traveler", "NavigateToBookmarkSystem: will continue in [ " + Math.Round(Time.Instance.NextTravelerAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + " ]sec", Logging.Debug);
                return;
            }

            if (DateTime.UtcNow < Time.Instance.LastSessionChange.AddSeconds(10))
            {
                if (Logging.DebugTraveler) Logging.Log("Traveler", "NavigateToBookmarkSystem: We just session changed less than 10 sec go, wait.", Logging.Teal);
                return;
            }

            Time.Instance.NextTravelerAction = DateTime.UtcNow.AddSeconds(2);
            if (Logging.DebugTraveler) Logging.Log("Traveler", "NavigateToBookmarkSystem - Iterating- next iteration should be in no less than [1] second ", Logging.Teal);
            
            _destinationRoute = null;
            _destinationRoute = Cache.Instance.DirectEve.Navigation.GetDestinationPath();
            
            if (_destinationRoute == null || _destinationRoute.Count == 0 || _destinationRoute.All(d => d != solarSystemId))
            {
                if (_destinationRoute != null || (_destinationRoute != null && _destinationRoute.Count == 0)) Logging.Log("Traveler", "NavigateToBookmarkSystem: We have no destination", Logging.Teal);
                if (_destinationRoute != null || (_destinationRoute != null && _destinationRoute.All(d => d != solarSystemId))) Logging.Log("Traveler", "NavigateToBookmarkSystem: the destination is not currently set to solarsystemId [" + solarSystemId + "]", Logging.Teal);

                // We do not have the destination set
                if (DateTime.UtcNow > _nextGetLocation || _location == null)
                {
                    Logging.Log("Traveler", "NavigateToBookmarkSystem: getting Location of solarSystemId [" + solarSystemId + "]", Logging.Teal);
                    _nextGetLocation = DateTime.UtcNow.AddSeconds(10);
                    _location = Cache.Instance.DirectEve.Navigation.GetLocation(solarSystemId);
                    Time.Instance.NextTravelerAction = DateTime.UtcNow.AddSeconds(2);
                    return;
                }

                if (_location != null && _location.IsValid)
                {
                    _locationErrors = 0;
                    Logging.Log("Traveler", "Setting destination to [" + Logging.Yellow + _location.Name + Logging.Green + "]", Logging.Green);
                    try
                    {
                        _location.SetDestination();
                    }
                    catch (Exception)
                    {
                        Logging.Log("Traveler", "NavigateToBookmarkSystem: set destination to [" + _location.ToString() + "] failed ", Logging.Debug);
                    }

                    Time.Instance.NextTravelerAction = DateTime.UtcNow.AddSeconds(3);
                    return;
                }

                Logging.Log("Traveler", "NavigateToBookmarkSystem: Error setting solar system destination [" + Logging.Yellow + solarSystemId + Logging.Green + "]", Logging.Green);
                _locationErrors++;
                if (_locationErrors > 20)
                {
                    _States.CurrentTravelerState = TravelerState.Error;
                    return;
                }

                return;
            }

            _locationErrors = 0;
            if (!Cache.Instance.InSpace)
            {
                if (Cache.Instance.InStation)
                {
                    if (DateTime.UtcNow > Time.Instance.LastInSpace.AddSeconds(45)) //do not try to leave the station until you have been docked for at least 45seconds! (this gives some overhead to load the station env + session change timer)
                    {
                        Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.CmdExitStation);
                        Time.Instance.NextTravelerAction = DateTime.UtcNow.AddSeconds(Time.Instance.TravelerExitStationAmIInSpaceYet_seconds);
                    }
                }

                Time.Instance.NextActivateModules = DateTime.UtcNow.AddSeconds(Cache.Instance.RandomNumber(2, 3));

                // We are not yet in space, wait for it
                return;
            }

            // We are apparently not really in space yet...
            if (Cache.Instance.ActiveShip == null || Cache.Instance.ActiveShip.Entity == null)
                return;

            //if (Logging.DebugTraveler) Logging.Log("Traveler", "Destination is set: processing...", Logging.Teal);

            // Find the first waypoint
            int waypoint = _destinationRoute.FirstOrDefault();

            //if (Logging.DebugTraveler) Logging.Log("Traveler", "NavigateToBookmarkSystem: getting next way-points locationName", Logging.Teal);
            _locationName = Cache.Instance.DirectEve.Navigation.GetLocationName(waypoint);
            if (Logging.DebugTraveler) Logging.Log("Traveler", "NavigateToBookmarkSystem: Next Waypoint is: [" + _locationName + "]", Logging.Teal);

            if (waypoint > 60000000) // this MUST be a station
            {
                //insert code to handle station destinations here
            }

            if (waypoint < 60000000) // this is not a station, probably a system
            {
                //useful?a
            }

            DirectSolarSystem solarSystemInRoute = Cache.Instance.DirectEve.SolarSystems[waypoint];

            if (_States.CurrentQuestorState == QuestorState.CombatMissionsBehavior || _States.CurrentQuestorState == QuestorState.DedicatedBookmarkSalvagerBehavior)
            {
                if (solarSystemInRoute != null && solarSystemInRoute.Security < 0.45 && (Cache.Instance.ActiveShip.GroupId != (int)Group.Shuttle || Cache.Instance.ActiveShip.GroupId != (int)Group.Frigate || Cache.Instance.ActiveShip.GroupId != (int)Group.Interceptor))
                {
                    Logging.Log("Traveler", "NavigateToBookmarkSystem: Next Waypoint is: [" + _locationName + "] which is LOW SEC! This should never happen. Turning off AutoStart and going home.", Logging.Teal);
                    Settings.Instance.AutoStart = false;
                    if (_States.CurrentQuestorState == QuestorState.CombatMissionsBehavior)
                    {
                        _States.CurrentCombatMissionBehaviorState = CombatMissionsBehaviorState.GotoBase;
                    }
                    if (_States.CurrentQuestorState == QuestorState.DedicatedBookmarkSalvagerBehavior)
                    {
                        _States.CurrentDedicatedBookmarkSalvagerBehaviorState = DedicatedBookmarkSalvagerBehaviorState.GotoBase;
                    }
                    //if (_States.CurrentQuestorState == QuestorState.CombatHelperBehavior)
                    //{
                    //    _States.CurrentCombatHelperBehaviorState = CombatHelperBehaviorState.GotoBase;
                    //}
                    return;
                }
            }
            
            // Find the stargate associated with it

            if (!Cache.Instance.Stargates.Any())
            {
                // not found, that cant be true?!?!?!?!
                Logging.Log("Traveler", "Error [" + Logging.Yellow + _locationName + Logging.Green + "] not found, most likely lag waiting [" + Time.Instance.TravelerNoStargatesFoundRetryDelay_seconds + "] seconds.", Logging.Red);
                Time.Instance.NextTravelerAction = DateTime.UtcNow.AddSeconds(Time.Instance.TravelerNoStargatesFoundRetryDelay_seconds);
                return;
            }

            // Warp to, approach or jump the stargate
            EntityCache MyNextStargate = Cache.Instance.StargateByName(_locationName);
            if (MyNextStargate != null)
            {
                if ((MyNextStargate.Distance < (int)Distances.DecloakRange && !Cache.Instance.ActiveShip.Entity.IsCloaked) || (MyNextStargate.Distance < (int)Distances.JumpRange && Cache.Instance.Modules.Any(i => i.GroupId != (int)Group.CloakingDevice)))
                {
                    if (MyNextStargate.Jump())
                    {
                        Logging.Log("Traveler", "Jumping to [" + Logging.Yellow + _locationName + Logging.Green + "]", Logging.Green);
                        return;
                    }

                    return;
                }

                if (MyNextStargate.Distance < (int)Distances.WarptoDistance && MyNextStargate.Distance != 0)
                {
                    if (DateTime.UtcNow > Time.Instance.NextApproachAction && !Cache.Instance.IsApproaching(MyNextStargate.Id))
                    {
                        if (Logging.DebugTraveler) Logging.Log("Traveler", "NavigateToBookmarkSystem: approaching the stargate named [" + MyNextStargate.Name + "]", Logging.Teal);
                        MyNextStargate.Approach(); //you could use a negative approach distance here but ultimately that is a bad idea.. Id like to go toward the entity without approaching it so we would end up inside the docking ring (eventually)
                        return;
                    }

                    if (Logging.DebugTraveler) Logging.Log("Traveler", "NavigateToBookmarkSystem: we are already approaching the stargate named [" + MyNextStargate.Name + "]", Logging.Teal);
                    return;
                }
                
                if (Cache.Instance.InSpace && !Combat.TargetedBy.Any(t => t.IsWarpScramblingMe))
                {
                    if (MyNextStargate.WarpTo())
                    {
                        Logging.Log("Traveler", "Warping to [" + Logging.Yellow + _locationName + Logging.Green + "][" + Logging.Yellow + Math.Round((MyNextStargate.Distance / 1000) / 149598000, 2) + Logging.Green + " AU away]", Logging.Green);
                        return;    
                    }

                    return;
                }
            }

            Time.Instance.NextTravelerAction = DateTime.UtcNow.AddSeconds(Time.Instance.WarptoDelay_seconds); //this should probably use a different Time definition, but this works for now. (5 seconds)
            if (!Combat.ReloadAll(Cache.Instance.MyShipEntity)) return;
            return;
        }

        public static void TravelHome(string module)
        {
            //only call bookmark stuff if UseHomebookmark is true
            if (Settings.Instance.UseHomebookmark)
            {
                // if we can't travel to bookmark, travel to agent's station
                if (!TravelToBookmarkName(Settings.Instance.HomeBookmarkName, module))
                {
                    TravelToAgentsStation(module);
                }

                return;
            }
            
            TravelToAgentsStation(module);
        }

        public static void TravelToAgentsStation(string module)
        {
            // if we can't warp because we are scrambled, prevent next actions
            if (!_defendOnTravel(module))
                return;

            if (Logging.DebugGotobase) Logging.Log(module, "TravelToAgentsStation:      Cache.Instance.AgentStationId [" + Cache.Instance.AgentStationID + "]", Logging.White);
            if (Logging.DebugGotobase) Logging.Log(module, "TravelToAgentsStation:  Cache.Instance.AgentSolarSystemId [" + Cache.Instance.AgentSolarSystemID + "]", Logging.White);

            if (_destination == null || _destination.SolarSystemId != Cache.Instance.AgentSolarSystemID)
            {
                Logging.Log(module, "Destination: [" + Cache.Instance.AgentStationName + "]", Logging.White);
                _destination = new StationDestination(Cache.Instance.AgentSolarSystemID, Cache.Instance.AgentStationID, Cache.Instance.AgentStationName);
                _States.CurrentTravelerState = TravelerState.Idle;
                return;
            }
            
            if (Logging.DebugGotobase) if (Traveler.Destination != null) Logging.Log("CombatMissionsBehavior", "TravelToAgentsStation: Traveler.Destination.SolarSystemId [" + Traveler.Destination.SolarSystemId + "]", Logging.White);
            Traveler.ProcessState();
            _processAtDestinationActions(module);
            
            return;
        }

        public static bool TravelToBookmarkName(string bookmarkName, string module)
        {
            bool travel = false;

            myHomeBookmarks = Cache.Instance.BookmarksByLabel(bookmarkName).ToList();

            if (myHomeBookmarks.Any())
            {
                DirectBookmark oldestHomeBookmark = myHomeBookmarks.OrderBy(b => b.CreatedOn).FirstOrDefault();
                if (oldestHomeBookmark != null && oldestHomeBookmark.LocationId != null)
                {
                    TravelToBookmark(oldestHomeBookmark, module);
                    travel = true;
                }
            }
            else
            {
                Logging.Log("Traveler.TravelToBookmarkName", "bookmark not found! We were Looking for bookmark starting with [" + bookmarkName + "] found none.", Logging.Orange);
            }

            return travel;
        }

        public static void TravelToBookmark(DirectBookmark bookmark, string module)
        {
            // if we can't warp because we are scrambled, prevent next actions
            if(!_defendOnTravel(module))
                return;

            if (Logging.DebugGotobase) Logging.Log(module, "TravelToBookmark:      bookmark [" + bookmark.Title + "]", Logging.White);

            if (_destination == null)
            {
                Logging.Log(module, "Destination: bookmark[" + bookmark.Description + "]", Logging.White);

                _destination = new BookmarkDestination(bookmark);
                _States.CurrentTravelerState = TravelerState.Idle;
                return;
            }
            
            if (Logging.DebugGotobase) if (Traveler.Destination != null) Logging.Log("CombatMissionsBehavior", "TravelToAgentsStation: Traveler.Destination.SolarSystemId [" + Traveler.Destination.SolarSystemId + "]", Logging.White);
            Traveler.ProcessState();
            _processAtDestinationActions(module);
            
            return;
        }

        public static void ProcessState()
        {
            // Only pulse state changes every 1.5s
            if (DateTime.UtcNow.Subtract(_lastTravelerPulse).TotalMilliseconds < 1000) //default: 1000ms
                return;

            _lastTravelerPulse = DateTime.UtcNow;

            switch (_States.CurrentTravelerState)
            {
                case TravelerState.Idle:
                    _States.CurrentTravelerState = TravelerState.Traveling;
                    break;

                case TravelerState.Traveling:
                    if ((!Cache.Instance.InSpace && !Cache.Instance.InStation) || Cache.Instance.InWarp) //if we are in warp, do nothing, as nothing can actually be done until we are out of warp anyway.
                        return;

                    if (Destination == null)
                    {
                        _States.CurrentTravelerState = TravelerState.Error;
                        break;
                    }

                    if (Destination.SolarSystemId != Cache.Instance.DirectEve.Session.SolarSystemId)
                    {
                        //Logging.Log("traveler: NavigateToBookmarkSystem(Destination.SolarSystemId);");
                        NavigateToBookmarkSystem(Destination.SolarSystemId);
                    }
                    else if (Destination.PerformFinalDestinationTask())
                    {
                        _destinationRoute = null;
                        _location = null;
                        _locationName = string.Empty;
                        _locationErrors = 0;

                        //Logging.Log("traveler: _States.CurrentTravelerState = TravelerState.AtDestination;");
                        _States.CurrentTravelerState = TravelerState.AtDestination;
                    }
                    break;

                case TravelerState.AtDestination:

                    //do nothing when at destination
                    //Traveler sits in AtDestination when it has nothing to do, NOT in idle.
                    break;

                default:
                    break;
            }
        }

        private static bool _defendOnTravel(string module)
        {
            bool canWarp = true;
            //
            // defending yourself is more important that the traveling part... so it comes first.
            //
            if (Cache.Instance.InSpace && Settings.Instance.DefendWhileTraveling)
            {
                if (!Cache.Instance.ActiveShip.Entity.IsCloaked || (Time.Instance.LastSessionChange.AddSeconds(60) > DateTime.UtcNow))
                {
                    if (Logging.DebugGotobase) Logging.Log(module, "Travel: _combat.ProcessState()", Logging.White);

                    try
                    {
                        Combat.ProcessState();
                    }
                    catch (Exception exception)
                    {
                        Logging.Log("Travel.Travel", "Exception [" + exception + "]", Logging.Debug);
                    }

                    if (!Combat.TargetedBy.Any(t => t.IsWarpScramblingMe))
                    {
                        if (Logging.DebugGotobase) Logging.Log(module, "Travel: we are not scrambled - pulling drones.", Logging.White);
                        Drones.IsMissionPocketDone = true; //tells drones.cs that we can pull drones

                        //Logging.Log("CombatmissionBehavior","TravelToAgentStation: not pointed",Logging.White);
                    }
                    else if (Combat.TargetedBy.Any(t => t.IsWarpScramblingMe))
                    {
                        Drones.IsMissionPocketDone = false;
                        if (Logging.DebugGotobase) Logging.Log(module, "Travel: we are scrambled", Logging.Teal);
                        Drones.ProcessState();

                        canWarp = false;
                    }
                }
            }

            if (NavigateOnGrid.SpeedTank) Salvage.OpenWrecks = false;

            return canWarp;
        }

        private static void _processAtDestinationActions(string module)
        {
            if (!Cache.Instance.UpdateMyWalletBalance()) return;

            if (_States.CurrentTravelerState == TravelerState.AtDestination)
            {
                if (_States.CurrentCombatMissionCtrlState == CombatMissionCtrlState.Error)
                {
                    Logging.Log(module, "an error has occurred", Logging.White);
                    if (_States.CurrentCombatMissionBehaviorState == CombatMissionsBehaviorState.Traveler)
                    {
                        _States.CurrentCombatMissionBehaviorState = CombatMissionsBehaviorState.Error;
                    }

                    return;
                }

                if (Cache.Instance.InSpace)
                {
                    Logging.Log(module, "Arrived at destination (in space, Questor stopped)", Logging.White);
                    Cache.Instance.Paused = true;
                    return;
                }

                if (Logging.DebugTraveler) Logging.Log(module, "Arrived at destination", Logging.White);
                if (_States.CurrentCombatMissionBehaviorState == CombatMissionsBehaviorState.Traveler)
                {
                    _States.CurrentCombatMissionBehaviorState = CombatMissionsBehaviorState.Idle;
                    _lastTravelerPulse = DateTime.UtcNow;
                    return;
                }

                if (_States.CurrentDedicatedBookmarkSalvagerBehaviorState == DedicatedBookmarkSalvagerBehaviorState.Traveler)
                {
                    _States.CurrentDedicatedBookmarkSalvagerBehaviorState = DedicatedBookmarkSalvagerBehaviorState.Idle;
                    _lastTravelerPulse = DateTime.UtcNow;
                    return;
                }

                if (_States.CurrentCombatHelperBehaviorState == CombatHelperBehaviorState.Traveler)
                {
                    _States.CurrentCombatHelperBehaviorState = CombatHelperBehaviorState.Idle;
                    _lastTravelerPulse = DateTime.UtcNow;
                    return;
                }

                return;
            }
        }
    }
}
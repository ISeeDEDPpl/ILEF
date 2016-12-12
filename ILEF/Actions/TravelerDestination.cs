// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------

namespace Questor.Modules.Actions
{
    using System;
    using System.Linq;
    using DirectEve;
    using global::Questor.Modules.BackgroundTasks;
    using global::Questor.Modules.Caching;
    using global::Questor.Modules.Logging;
    using global::Questor.Modules.Lookup;

    public abstract class TravelerDestination
    {
        internal static int _undockAttempts;
        internal static DateTime _nextTravelerDestinationAction;
        public long SolarSystemId { get; protected set; }

        /// <summary>
        ///   This function returns true if we are at the final destination and false if the task is not yet complete
        /// </summary>
        /// <returns></returns>
        public abstract bool PerformFinalDestinationTask();

        internal static void Undock()
        {
            if (Cache.Instance.InStation && !Cache.Instance.InSpace)
            {
                
                if (_undockAttempts + Cache.Instance.RandomNumber(0, 4) > 10) //If we are having to retry at all there is likely something very wrong. Make it non-obvious if we do have to restart by restarting at diff intervals.
                {
                    Logging.Log("TravelerDestination.StationDestination", "This is not the destination station, we have tried to undock [" + _undockAttempts + "] times - and it is evidentially not working (lag?) - restarting Questor (and EVE)", Logging.Green);
                    Cleanup.SignalToQuitQuestorAndEVEAndRestartInAMoment = true; //this will perform a graceful restart
                    return;
                }

                if (DateTime.UtcNow > Time.Instance.LastInSpace.AddSeconds(45)) //do not try to leave the station until you have been docked for at least 45seconds! (this gives some overhead to load the station env + session change timer)
                {
                    if (DateTime.UtcNow > Time.Instance.NextUndockAction)
                    {
                        Logging.Log("TravelerDestination.SolarSystemDestination", "Exiting station", Logging.White);
                        Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.CmdExitStation);
                        Time.Instance.LastSessionChange = DateTime.UtcNow;
                        _undockAttempts++;
                        Time.Instance.NextUndockAction = DateTime.UtcNow.AddSeconds(Time.Instance.TravelerExitStationAmIInSpaceYet_seconds + Cache.Instance.RandomNumber(0, 20));
                        return;
                    }

                    if (Logging.DebugTraveler) Logging.Log("TravelerDestination.SolarSystemDestination", "LastInSpace is more than 45 sec old (we are docked), but NextUndockAction is still in the future [" + Time.Instance.NextUndockAction.Subtract(DateTime.UtcNow).TotalSeconds + "seconds]", Logging.White);
                    return;
                }
                
                // We are not UnDocked yet
                return;
            }
        }

        internal static bool useInstaBookmark()
        {
            try
            {
                if (Cache.Instance.InWarp) return false;

                if (Cache.Instance.InSpace && DateTime.UtcNow > Time.Instance.LastInStation.AddSeconds(10))
                {
                    if ((Cache.Instance.ClosestStargate != null && Cache.Instance.ClosestStargate.IsOnGridWithMe) || (Cache.Instance.ClosestStation != null && Cache.Instance.ClosestStation.IsOnGridWithMe))
                    {
                        if (Cache.Instance.UndockBookmark != null)
                        {
                            if (Cache.Instance.UndockBookmark.LocationId == Cache.Instance.DirectEve.Session.LocationId)
                            {
                                double distance = Cache.Instance.DistanceFromMe(Cache.Instance.UndockBookmark.X ?? 0, Cache.Instance.UndockBookmark.Y ?? 0, Cache.Instance.UndockBookmark.Z ?? 0);
                                if (distance < (int)Distances.WarptoDistance)
                                {
                                    Logging.Log("TravelerDestination.useInstaBookmark", "Arrived at undock bookmark [" + Logging.Yellow + Cache.Instance.UndockBookmark.Title + Logging.Green + "]", Logging.White);
                                    Cache.Instance.UndockBookmark = null;
                                    return true;
                                }

                                if (distance >= (int)Distances.WarptoDistance)
                                {
                                    if (Cache.Instance.UndockBookmark.WarpTo())
                                    {
                                        Logging.Log("TravelerDestination.useInstaBookmark", "Warping to undock bookmark [" + Logging.Yellow + Cache.Instance.UndockBookmark.Title + Logging.Green + "][" + Math.Round((distance / 1000) / 149598000, 2) + " AU away]", Logging.White);
                                        //if (!Combat.ReloadAll(Cache.Instance.EntitiesNotSelf.OrderBy(t => t.Distance).FirstOrDefault(t => t.Distance < (double)Distance.OnGridWithMe))) return false;
                                        _nextTravelerDestinationAction = DateTime.UtcNow.AddSeconds(10);
                                        return true;
                                    }

                                    return false;
                                }

                                return false;    
                            }
                            
                            if (Logging.DebugUndockBookmarks) Logging.Log("useInstaBookmark", "Bookmark Named [" + Cache.Instance.UndockBookmark.Title + "] was somehow picked as an UndockBookmark but it is not in local with us! continuing without it.", Logging.Debug);
                            return true;
                        }

                        if (Logging.DebugUndockBookmarks) Logging.Log("useInstaBookmark", "No undock bookmarks in local matching our undockPrefix [" + Settings.Instance.UndockBookmarkPrefix + "] continuing without it.", Logging.Debug);
                        return true;
                    }

                    if (Logging.DebugUndockBookmarks) Logging.Log("useInstaBookmark", "Not currently on grid with a station or a stargate: continue traveling", Logging.Debug);
                    return true;
                }

                if (Logging.DebugUndockBookmarks) Logging.Log("useInstaBookmark", "InSpace [" + Cache.Instance.InSpace + "]: waiting until we have been undocked or in system a few seconds", Logging.Debug);
                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("TravelerDestination.SolarSystemDestination", "Exception [" + exception + "]", Logging.White);
                return false;
            }
        }
    }

    public class SolarSystemDestination : TravelerDestination
    {
        public SolarSystemDestination(long solarSystemId)
        {
            Logging.Log("TravelerDestination.SolarSystemDestination", "Destination set to solar system id [" + solarSystemId + "]", Logging.White);
            SolarSystemId = solarSystemId;
        }

        public override bool PerformFinalDestinationTask()
        {
            // The destination is the solar system, not the station in the solar system.
            if (Cache.Instance.InStation && !Cache.Instance.InSpace)
            {
                if (_nextTravelerDestinationAction < DateTime.UtcNow)
                {
                    TravelerDestination.Undock();
                    return false;
                }

                // We are not there yet
                return false;
            }

            if (_nextTravelerDestinationAction > DateTime.UtcNow)
                return false;

            _undockAttempts = 0;

            if (!useInstaBookmark()) return false;

            // The task was to get to the solar system, we're there :)
            Logging.Log("TravelerDestination.SolarSystemDestination", "Arrived in system", Logging.White);
            Cache.Instance.MissionBookmarkTimerSet = false;
            return true;
        }
    }

    public class StationDestination : TravelerDestination
    {
        public StationDestination(long stationId)
        {
            DirectLocation station = Cache.Instance.DirectEve.Navigation.GetLocation(stationId);
            if (station == null || !station.ItemId.HasValue || !station.SolarSystemId.HasValue)
            {
                Logging.Log("TravelerDestination.StationDestination", "Invalid station id [" + Logging.Yellow + StationId + Logging.Green + "]", Logging.Red);
                SolarSystemId = Cache.Instance.DirectEve.Session.SolarSystemId ?? -1;
                StationId = -1;
                StationName = "";
                return;
            }

            Logging.Log("TravelerDestination.StationDestination", "Destination set to [" + Logging.Yellow + station.Name + Logging.Green + "]", Logging.Green);
            StationId = stationId;
            StationName = station.Name;
            SolarSystemId = station.SolarSystemId.Value;
        }

        public StationDestination(long solarSystemId, long stationId, string stationName)
        {
            Logging.Log("TravelerDestination.StationDestination", "Destination set to [" + Logging.Yellow + stationName + Logging.Green + "]", Logging.Green);
            SolarSystemId = solarSystemId;
            StationId = stationId;
            StationName = stationName;
        }

        public long StationId { get; set; }

        public string StationName { get; set; }

        public override bool PerformFinalDestinationTask()
        {
            bool arrived = PerformFinalDestinationTask(StationId, StationName);
            return arrived;
        }

        internal static bool PerformFinalDestinationTask(long stationId, string stationName)
        {
            if (Cache.Instance.InStation && Cache.Instance.DirectEve.Session.StationId == stationId)
            {
                Logging.Log("TravelerDestination.StationDestination", "Arrived in station", Logging.Green);
                return true;
            }

            if (Cache.Instance.InStation && DateTime.UtcNow > Time.Instance.LastInSpace.AddSeconds(10))
            {
                // We are in a station, but not the correct station!
                if (DateTime.UtcNow > Time.Instance.NextUndockAction)
                {
                    TravelerDestination.Undock();
                    return false;
                }

                // We are not there yet
                return false;
            }

            if ((DateTime.UtcNow > Time.Instance.LastInStation.AddSeconds(10)) && !Cache.Instance.InSpace)
            {
                // We are not in station and not in space?  Wait for a bit
                return false;
            }

            if (_nextTravelerDestinationAction > DateTime.UtcNow)
                return false;

            _undockAttempts = 0;

            if (!useInstaBookmark()) return false;

            //else Logging.Log("TravelerDestination.BookmarkDestination","undock bookmark missing: " + Cache.Instance.DirectEve.GetLocationName((long)Cache.Instance.DirectEve.Session.StationId) + " and " + Settings.Instance.UndockPrefix + " did not both exist in a bookmark");

            if (Cache.Instance.Stations == null)
            {
                // We are there but no stations? Wait a bit
                return false;
            }

            EntityCache station = Cache.Instance.EntitiesByName(stationName, Cache.Instance.Stations).FirstOrDefault();
            if (station == null)
            {
                // We are there but no station? Wait a bit
                return false;
            }

            if (station.Distance < (int)Distances.DockingRange)
            {
                if (station.Dock())
                {
                    Logging.Log("TravelerDestination.StationDestination", "Dock at [" + Logging.Yellow + station.Name + Logging.Green + "] which is [" + Math.Round(station.Distance / 1000, 0) + "k away]", Logging.Green);
                    return false; //we do not return true until we actually appear in the destination (station in this case)
                }

                return false;
            }

            if (station.Distance < (int)Distances.WarptoDistance)
            {
                if (station.Approach())
                {
                    Logging.Log("TravelerDestination.StationDestination", "Approaching [" + station.Name + "] which is [" + Math.Round(station.Distance / 1000, 0) + "k away]", Logging.White);
                }

                return false;
            }

            if (station.WarpTo())
            {
                Logging.Log("TravelerDestination.StationDestination", "Warp to and dock at [" + Logging.Yellow + station.Name + Logging.Green + "][" + Math.Round((station.Distance / 1000) / 149598000, 2) + " AU away]", Logging.Green);
                return false;    
            }
            
            return false;
        }
    }

    public class BookmarkDestination : TravelerDestination
    {
        public BookmarkDestination(DirectBookmark bookmark)
        {
            if (bookmark == null)
            {
                Logging.Log("TravelerDestination.BookmarkDestination", "Invalid bookmark destination!", Logging.Red);

                SolarSystemId = Cache.Instance.DirectEve.Session.SolarSystemId ?? -1;
                BookmarkId = -1;
                return;
            }

            Logging.Log("TravelerDestination.BookmarkDestination", "Destination set to bookmark [" + Logging.Yellow + bookmark.Title + Logging.Green + "]", Logging.Green);
            BookmarkId = bookmark.BookmarkId ?? -1;
            SolarSystemId = bookmark.LocationId ?? -1;
        }

        public BookmarkDestination(long bookmarkId)
            : this(Cache.Instance.BookmarkById(bookmarkId))
        {
        }

        public long BookmarkId { get; set; }

        public override bool PerformFinalDestinationTask()
        {
            DirectBookmark bookmark = Cache.Instance.BookmarkById(BookmarkId);
            bool arrived = PerformFinalDestinationTask(bookmark, 150000);

            return arrived;
        }

        internal static bool PerformFinalDestinationTask(DirectBookmark bookmark, int warpDistance)
        {
            // The bookmark no longer exists, assume we are not there
            if (bookmark == null)
                return false;

            // Is this a station bookmark?
            if (bookmark.Entity != null && bookmark.Entity.GroupId == (int)Group.Station)
            {
                bool arrived = StationDestination.PerformFinalDestinationTask(bookmark.Entity.Id, bookmark.Entity.Name);
                if (arrived)
                {
                    Logging.Log("TravelerDestination.BookmarkDestination", "Arrived at bookmark [" + Logging.Yellow + bookmark.Title + Logging.Green + "]", Logging.Green);
                }

                return arrived;
            }

            if (Cache.Instance.InStation)
            {
                // We have arrived
                if (bookmark.ItemId.HasValue && bookmark.ItemId == Cache.Instance.DirectEve.Session.StationId)
                    return true;

                // We are in a station, but not the correct station!
                if (DateTime.UtcNow > Time.Instance.NextUndockAction)
                {
                    TravelerDestination.Undock();
                    return false;
                }

                return false;
            }

            if (!Cache.Instance.InSpace)
            {
                // We are not in space and not in a station, wait a bit
                return false;
            }

            if (_nextTravelerDestinationAction > DateTime.UtcNow)
                return false;

            _undockAttempts = 0;

            if (Cache.Instance.UndockBookmark != null)
            {
                double distanceToUndockBookmark = Cache.Instance.DistanceFromMe(bookmark.X ?? 0, bookmark.Y ?? 0, bookmark.Z ?? 0);
                if (distanceToUndockBookmark < (int)Distances.WarptoDistance)
                {
                    Logging.Log("TravelerDestination.BookmarkDestination", "Arrived at undock bookmark [" + Logging.Yellow + Cache.Instance.UndockBookmark.Title + Logging.Green + "]", Logging.Green);
                    Cache.Instance.UndockBookmark = null;
                }
                else
                {
                    if (Cache.Instance.UndockBookmark.WarpTo())
                    {
                        Logging.Log("TravelerDestination.BookmarkDestination", "Warping to undock bookmark [" + Logging.Yellow + Cache.Instance.UndockBookmark.Title + Logging.Green + "][" + Logging.Yellow + Math.Round((distanceToUndockBookmark / 1000) / 149598000, 2) + Logging.Green + " AU away]", Logging.Green);
                        _nextTravelerDestinationAction = DateTime.UtcNow.AddSeconds(Time.Instance.TravelerInWarpedNextCommandDelay_seconds);
                        //if (!Combat.ReloadAll(Cache.Instance.EntitiesNotSelf.OrderBy(t => t.Distance).FirstOrDefault(t => t.Distance < (double)Distance.OnGridWithMe))) return false;
                        return false;    
                    }
                }
            }

            // This bookmark has no x / y / z, assume we are there.
            if (bookmark.X == -1 || bookmark.Y == -1 || bookmark.Z == -1)
            {
                Logging.Log("TravelerDestination.BookmarkDestination", "Arrived at the bookmark [" + Logging.Yellow + bookmark.Title + Logging.Green + "][No XYZ]", Logging.Green);
                return true;
            }

            double distance = Cache.Instance.DistanceFromMe(bookmark.X ?? 0, bookmark.Y ?? 0, bookmark.Z ?? 0);
            if (distance < warpDistance)
            {
                Logging.Log("TravelerDestination.BookmarkDestination", "Arrived at the bookmark [" + Logging.Yellow + bookmark.Title + Logging.Green + "]", Logging.Green);
                return true;
            }

            if (_nextTravelerDestinationAction > DateTime.UtcNow)
                return false;

            if (Math.Round((distance / 1000)) < (int)Distances.MaxPocketsDistanceKm && Cache.Instance.AccelerationGates.Count() != 0)
            {
                Logging.Log("TravelerDestination.BookmarkDestination", "Warp to bookmark in same pocket requested but acceleration gate found delaying.", Logging.White);
                return true;
            }

            Defense.DoNotBreakInvul = false;
            string nameOfBookmark = "";
            if (Settings.Instance.EveServerName == "Tranquility") nameOfBookmark = "Encounter";
            if (Settings.Instance.EveServerName == "Serenity") nameOfBookmark = "遭遇战";
            if (nameOfBookmark == "") nameOfBookmark = "Encounter";
            //if (!Combat.ReloadAll(Cache.Instance.EntitiesNotSelf.OrderBy(t => t.Distance).FirstOrDefault(t => t.Distance < (double)Distance.OnGridWithMe))) return false;
            if (MissionSettings.MissionWarpAtDistanceRange != 0 && bookmark.Title.Contains(nameOfBookmark))
            {
                if (bookmark.WarpTo(MissionSettings.MissionWarpAtDistanceRange*1000))
                {
                    Logging.Log("TravelerDestination.BookmarkDestination", "Warping to bookmark [" + Logging.Yellow + bookmark.Title + Logging.Green + "][" + Logging.Yellow + " At " + MissionSettings.MissionWarpAtDistanceRange + Logging.Green + " km]", Logging.Green);
                    return true;
                }
            }
            else
            {
                if (bookmark.WarpTo())
                {
                    Logging.Log("TravelerDestination.BookmarkDestination", "Warping to bookmark [" + Logging.Yellow + bookmark.Title + Logging.Green + "][" + Logging.Yellow + Math.Round((distance / 1000) / 149598000, 2) + Logging.Green + " AU away]", Logging.Green);
                    return true;
                }
            }

            return false;
        }
    }

    public class MissionBookmarkDestination : TravelerDestination
    {
        public MissionBookmarkDestination(DirectAgentMissionBookmark bookmark)
        {
            if (bookmark == null)
            {
                if (DateTime.UtcNow > Time.Instance.MissionBookmarkTimeout.AddMinutes(2))
                {
                    Logging.Log("TravelerDestination", "MissionBookmarkTimeout [ " + Time.Instance.MissionBookmarkTimeout.ToShortTimeString() + " ] did not get reset from last usage: resetting it now", Logging.Red);
                    Time.Instance.MissionBookmarkTimeout = DateTime.UtcNow.AddYears(1); 
                }

                if (!Cache.Instance.MissionBookmarkTimerSet)
                {
                    Cache.Instance.MissionBookmarkTimerSet = true;
                    Time.Instance.MissionBookmarkTimeout = DateTime.UtcNow.AddSeconds(10);
                }

                if (DateTime.UtcNow > Time.Instance.MissionBookmarkTimeout) //if CurrentTime is after the TimeOut value, freak out
                {
                    AgentId = -1;
                    Title = null;
                    SolarSystemId = Cache.Instance.DirectEve.Session.SolarSystemId ?? -1;

                    Cache.Instance.CloseQuestorCMDLogoff = false;
                    Cache.Instance.CloseQuestorCMDExitGame = true;
                    Cleanup.ReasonToStopQuestor = "TravelerDestination.MissionBookmarkDestination: Invalid mission bookmark! - Lag?! Closing EVE";
                    Logging.Log("TravelerDestination", Cleanup.ReasonToStopQuestor, Logging.Red);
                    Cleanup.SignalToQuitQuestorAndEVEAndRestartInAMoment = true;
                }
                else
                {
                    Logging.Log("TravelDestination.MissionBookmarkDestination", "Invalid Mission Bookmark! retrying for another [ " + Math.Round(Time.Instance.MissionBookmarkTimeout.Subtract(DateTime.UtcNow).TotalSeconds, 0) + " ]sec", Logging.Green);
                }
            }

            if (bookmark != null)
            {
                Logging.Log("TravelerDestination.MissionBookmarkDestination", "Destination set to mission bookmark [" + Logging.Yellow + bookmark.Title + Logging.Green + "]", Logging.Green);
                AgentId = bookmark.AgentId ?? -1;
                Title = bookmark.Title;
                SolarSystemId = bookmark.SolarSystemId ?? -1;
                Cache.Instance.MissionBookmarkTimerSet = false;
            }
        }

        public MissionBookmarkDestination(int agentId, string title)
            : this(GetMissionBookmark(agentId, title))
        {
        }

        public long AgentId { get; set; }

        public string Title { get; set; }

        private static DirectAgentMissionBookmark GetMissionBookmark(long agentId, string title)
        {
            DirectAgentMission mission = Cache.Instance.GetAgentMission(agentId, true);
            if (mission == null)
                return null;

            return mission.Bookmarks.FirstOrDefault(b => b.Title.ToLower() == title.ToLower());
        }

        public override bool PerformFinalDestinationTask()
        {
            bool arrived = BookmarkDestination.PerformFinalDestinationTask(GetMissionBookmark(AgentId, Title), (int)Distances.MissionWarpLimit);
            return arrived;// Mission bookmarks have a 1.000.000 distance warp-to limit (changed it to 150.000.000 as there are some bugged missions around)
        }
    }
}
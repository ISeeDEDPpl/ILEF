﻿// ------------------------------------------------------------------------------
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
    using DirectEve;
    using global::Questor.Modules.Logging;
    using global::Questor.Modules.Lookup;
    using global::Questor.Modules.Caching;

    public class BookmarkDestination2 : TravelerDestination
    {
        private DateTime _nextAction;

        public BookmarkDestination2(DirectBookmark bookmark)
        {
            if (bookmark == null)
            {
                Logging.Log("QuestorManager.BookmarkDestination", "Invalid bookmark destination!", Logging.White);

                SolarSystemId = Cache.Instance.DirectEve.Session.SolarSystemId ?? -1;
                BookmarkId = -1;
                return;
            }

            Logging.Log("QuestorManager.BookmarkDestination", "Destination set to bookmark [" + bookmark.Title + "]", Logging.White);
            DirectLocation location = GetBookmarkLocation(bookmark);
            if (location == null)
            {
                Logging.Log("QuestorManager.BookmarkDestination", "Invalid bookmark destination!", Logging.White);

                SolarSystemId = Cache.Instance.DirectEve.Session.SolarSystemId ?? -1;
                BookmarkId = -1;
                return;
            }

            BookmarkId = bookmark.BookmarkId ?? -1;
            SolarSystemId = location.SolarSystemId ?? Cache.Instance.DirectEve.Session.SolarSystemId ?? -1;
        }

        public BookmarkDestination2(long bookmarkId)
            : this(Cache.Instance.BookmarkById(bookmarkId))
        {
        }

        public long BookmarkId { get; set; }

        private static DirectLocation GetBookmarkLocation(DirectBookmark bookmark)
        {
            DirectLocation location = Cache.Instance.DirectEve.Navigation.GetLocation(bookmark.ItemId ?? -1);
            if (!location.IsValid)
                location = Cache.Instance.DirectEve.Navigation.GetLocation(bookmark.LocationId ?? -1);
            if (!location.IsValid)
                return null;

            return location;
        }

        public override bool PerformFinalDestinationTask()
        {
            DirectBookmark bookmark = Cache.Instance.BookmarkById(BookmarkId);
            return PerformFinalDestinationTask2(bookmark, 150000, ref _nextAction);
        }

        internal static bool PerformFinalDestinationTask2(DirectBookmark bookmark, int warpDistance, ref DateTime nextAction)
        {
            // The bookmark no longer exists, assume we are there
            if (bookmark == null)
                return true;

            DirectLocation location = GetBookmarkLocation(bookmark);
            if (Cache.Instance.DirectEve.Session.IsInStation)
            {
                // We have arrived
                if (location != null && location.ItemId == Cache.Instance.DirectEve.Session.StationId)
                    return true;

                if (DateTime.UtcNow > Time.Instance.LastInSpace.AddSeconds(45)) //do not try to leave the station until you have been docked for at least 45seconds! (this gives some overhead to load the station env + session change timer)
                {
                    // We are apparently in a station that is incorrect
                    Logging.Log("QuestorManager.BookmarkDestination", "We're docked in the wrong station, undocking", Logging.White);

                    Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.CmdExitStation);
                    nextAction = DateTime.UtcNow.AddSeconds(30);
                    return false;
                }

                return false;
            }

            // Is this a station bookmark?
            if (bookmark.Entity != null && bookmark.Entity.GroupId == (int)Group.Station)
            {
                bool arrived = StationDestination2.PerformFinalDestinationTask(bookmark.Entity.Id, bookmark.Entity.Name, ref nextAction);
                if (arrived)
                    Logging.Log("QuestorManager.BookmarkDestination", "Arrived at bookmark [" + bookmark.Title + "]", Logging.White);
                return arrived;
            }

            // Its not a station bookmark, make sure we are in space
            if (Cache.Instance.DirectEve.Session.IsInStation)
            {
                // We are in a station, but not the correct station!
                if (nextAction < DateTime.UtcNow)
                {
                    if (DateTime.UtcNow > Time.Instance.LastInSpace.AddSeconds(45)) //do not try to leave the station until you have been docked for at least 45seconds! (this gives some overhead to load the station env + session change timer)
                    {
                        Logging.Log("QuestorManager.BookmarkDestination", "We're docked but our destination is in space, undocking", Logging.White);
                        Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.CmdExitStation);
                        nextAction = DateTime.UtcNow.AddSeconds(30);
                    }
                }

                // We are not there yet
                return false;
            }

            if (!Cache.Instance.DirectEve.Session.IsInSpace)
            {
                // We are not in space and not in a station, wait a bit
                return false;
            }

            // This bookmark has no x / y / z, assume we are there.
            if (bookmark.X == -1 || bookmark.Y == -1 || bookmark.Z == -1)
            {
                Logging.Log("QuestorManager.BookmarkDestination", "Arrived at the bookmark [" + Logging.Yellow + bookmark.Title + Logging.White + "][No XYZ]", Logging.White);
                return true;
            }

            double distance = Cache.Instance.DistanceFromMe(bookmark.X ?? 0, bookmark.Y ?? 0, bookmark.Z ?? 0);
            if (distance < warpDistance)
            {
                Logging.Log("QuestorManager.BookmarkDestination", "Arrived at the bookmark [" + Logging.Yellow + bookmark.Title + Logging.White + "]", Logging.White);
                return true;
            }

            if (nextAction > DateTime.UtcNow)
                return false;

            if (Cache.Instance.GateInGrid() && (distance / 1000) < (int)Distances.MaxPocketsDistanceKm)
            {
                Logging.Log("QuestorManager.BookmarkDestination", "Bookmark [" + Logging.Yellow + bookmark.Title + Logging.White + "][" + Logging.Yellow + Math.Round((distance / 1000) / 149598000, 2) + Logging.White + "] AU away. Which is [" + Logging.Yellow + Math.Round((distance / 1000), 2) + Logging.White + "].", Logging.White);
            }

            if (bookmark.WarpTo())
            {
                Logging.Log("QuestorManager.BookmarkDestination", "Warping to bookmark [" + Logging.Yellow + bookmark.Title + Logging.White + "][" + Math.Round((distance / 1000) / 149598000, 2) + "] AU away. Which is [" + Math.Round((distance / 1000), 2) + "]", Logging.White);
                nextAction = DateTime.UtcNow.AddSeconds(30);
                Time.Instance.NextWarpAction = DateTime.UtcNow.AddSeconds(5);
                return false;
            }
            
            return false;
        }
    }
}
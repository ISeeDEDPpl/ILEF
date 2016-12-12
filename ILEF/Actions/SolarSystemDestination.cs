// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------

using Questor.Modules.Lookup;

namespace Questor.Modules.Actions
{
    using System;
    using DirectEve;
    using global::Questor.Modules.Logging;
    using global::Questor.Modules.Caching;

    public class SolarSystemDestination2 : TravelerDestination
    {
        private DateTime _nextAction;

        public SolarSystemDestination2(long solarSystemId)
        {
            Logging.Log("QuestorManager.SolarSystemDestination", "Destination set to solar system id [" + solarSystemId + "]", Logging.White);
            SolarSystemId = solarSystemId;
        }

        public override bool PerformFinalDestinationTask()
        {
            // The destination is the solar system, not the station in the solar system.
            if (Cache.Instance.DirectEve.Session.IsInStation)
            {
                if (_nextAction < DateTime.UtcNow)
                {
                    if (DateTime.UtcNow > Time.Instance.LastInSpace.AddSeconds(45)) //do not try to leave the station until you have been docked for at least 45seconds! (this gives some overhead to load the station env + session change timer)
                    {
                        Logging.Log("QuestorManager.SolarSystemDestination", "Exiting station", Logging.White);
                        Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.CmdExitStation);
                        _nextAction = DateTime.UtcNow.AddSeconds(30);
                    }
                }

                // We are not there yet
                return false;
            }

            // The task was to get to the solar system, we are there :)
            Logging.Log("QuestorManager.SolarSystemDestination", "Arrived in system", Logging.White);
            return true;
        }
    }
}
﻿using EveCom;

namespace EveComFramework.KanedaToolkit
{
    /// <summary>
    /// extension methods for AgentMission
    /// </summary>
    public static class KAgentMission
    {
        /// <summary>
        /// Get Objective state for mission
        /// </summary>
        /// <param name="mission">AgentMission</param>
        /// <returns></returns>
        public static MissionToolkit.MissionObjectiveState MissionObjectiveState(this AgentMission mission)
        {
            if (!mission.Accepted)
                return MissionToolkit.MissionObjectiveState._NotAccepted;

            switch (mission.Objective.Action)
            {
                case "TravelTo":
                    return MissionToolkit.MissionObjectiveState.TravelTo;
                case "MissionFetch":
                    return MissionToolkit.MissionObjectiveState.MissionFetch;
                case "MissionFetchContainer":
                    return MissionToolkit.MissionObjectiveState.MissionFetchContainer;
                case "MissionFetchMine":
                    return MissionToolkit.MissionObjectiveState.MissionFetchMine;
                case "MissionFetchMineTrigger":
                    return MissionToolkit.MissionObjectiveState.MissionFetchMineTrigger;
                case "MissionFetchTarget":
                    return MissionToolkit.MissionObjectiveState.MissionFetchTarget;
                case "AllObjectivesComplete":
                    return MissionToolkit.MissionObjectiveState.AllObjectivesComplete;
                case "TransportItemsPresent":
                    return MissionToolkit.MissionObjectiveState.TransportItemsPresent;
                case "TransportItemsMissing":
                    return MissionToolkit.MissionObjectiveState.TransportItemsMissing;
                case "FetchObjectAcquiredDungeonDone":
                    return MissionToolkit.MissionObjectiveState.FetchObjectAcquiredDungeonDone;
                case "GoToGate":
                    return MissionToolkit.MissionObjectiveState.GoToGate;
                case "KillTrigger":
                    return MissionToolkit.MissionObjectiveState.KillTrigger;
                case "DestroyLCSAndAll":
                    return MissionToolkit.MissionObjectiveState.DestroyLCSAndAll;
                case "Destroy":
                    return MissionToolkit.MissionObjectiveState.Destroy;
                case "Attack":
                    return MissionToolkit.MissionObjectiveState.Attack;
                case "Approach":
                    return MissionToolkit.MissionObjectiveState.Approach;
                case "Hack":
                    return MissionToolkit.MissionObjectiveState.Hack;
                case "Salvage":
                    return MissionToolkit.MissionObjectiveState.Salvage;
                case "DestroyAll":
                    return MissionToolkit.MissionObjectiveState.DestroyAll;
            }
            return MissionToolkit.MissionObjectiveState._Unknown;
        }

    }

}

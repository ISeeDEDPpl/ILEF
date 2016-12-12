using System;
using Questor.Modules.Lookup;

namespace Questor.Modules.Activities
{
    using System.Linq;
    using DirectEve;
    using global::Questor.Modules.Actions;
    using global::Questor.Modules.Logging;
    using global::Questor.Modules.States;
    using global::Questor.Modules.Caching;

    public class CourierMissionCtrl
    {
        //private DateTime _nextCourierAction;
        //private readonly Traveler _traveler;
        //private readonly AgentInteraction _agentInteraction;
        private int moveItemRetryCounter;
        private DateTime _nextCourierMissionCtrlPulse = DateTime.UtcNow.AddDays(-30);
        /// <summary>
        ///   Arm does nothing but get into a (assembled) shuttle
        /// </summary>
        /// <returns></returns>
        ///
        public CourierMissionCtrl()
        {
            //_traveler = new Traveler();
            //_agentInteraction = new AgentInteraction();
        }

        private bool GotoMissionBookmark(long agentId, string title)
        {
            MissionBookmarkDestination destination = Traveler.Destination as MissionBookmarkDestination;
            if (destination == null || destination.AgentId != agentId || !destination.Title.StartsWith(title))
                Traveler.Destination = new MissionBookmarkDestination(MissionSettings.GetMissionBookmark(agentId, title));

            Traveler.ProcessState();

            if (_States.CurrentTravelerState == TravelerState.AtDestination)
            {
                if (destination != null)
                {
                    Logging.Log("CourierMissionCtrl", "Arrived at Mission Bookmark Destination [ " + destination.Title + " ]", Logging.White);
                }
                else
                {
                    Logging.Log("CourierMissionCtrl", "destination is null", Logging.White); //how would this occur exactly?
                }
                Traveler.Destination = null;
                return true;
            }

            return false;
        }

        private bool MoveItem(bool pickup)
        {
            DirectEve directEve = Cache.Instance.DirectEve;

            // Open the item hangar (should still be open)
            if (Cache.Instance.ItemHangar == null) return false;

            if (Cache.Instance.CurrentShipsCargo == null) return false;
            string missionItem;

            switch (MissionSettings.Mission.Name)
            {
                case "Enemies Abound (2 of 5)":                       //lvl4 courier
                    missionItem = "Encoded Data Chip";
                    break;

                case "In the Midst of Deadspace (2 of 5)":            //lvl4 courier
                    missionItem = "Amarr Light Marines";
                    break;

                case "Pot and Kettle - Delivery (3 of 5)":            //lvl4 courier
                    missionItem = "Large EMP Smartbomb I";
                    break;

                case "Pot and Kettle - Making Amends (5 of 5)":       //lvl4 courier
                    missionItem = "Mexallon";
                    break;

                case "Technological Secrets (2 of 3)":               //lvl4 courier
                    missionItem = "DNA Sample"; //typeid: 13288	 groupID: 314
                    break;

                case "Interstellar Railroad (2 of 4)":                //lvl1 courier
                    missionItem = "Reports"; //not correct here
                    break;

                case "New Frontiers - Toward a Solution (3 of 7)":    //lvl3 courier - this likely needs to be corrected to be the correct mission name
                case "New Frontiers - Nanite Express (6 of 7)":       //lvl3 courier - this likely needs to be corrected to be the correct mission name
                case "Portal to War (3 of 5)":                        //lvl3 courier - this likely needs to be corrected to be the correct mission name
                case "Guristas Strike - The Interrogation (2 of 10)": //lvl3 courier - this likely needs to be corrected to be the correct mission name
                case "Guristas Strike - Possible Leads (4 of 10)":    //lvl3 courier - this likely needs to be corrected to be the correct mission name
                case "Guristas Strike - The Flu Outbreak (6 of 10)":  //lvl3 courier - this likely needs to be corrected to be the correct mission name
                case "Angel Strike - The Interrogation (2 of 10)":    //lvl3 courier - this likely needs to be corrected to be the correct mission name
                case "Angel Strike - Possible Leads (4 of 10)":       //lvl3 courier - this likely needs to be corrected to be the correct mission name
                case "Angel Strike - The Flu Outbreak (6 of 10)":     //lvl3 courier - this likely needs to be corrected to be the correct mission name
                    missionItem = "Encoded Data Chip"; //not correct here
                    break;

                default:
                    missionItem = "Encoded Data Chip"; //likely not correct - add an entry above for the courier mission in question
                    break;
            }

            Logging.Log("CourierMissionCtrl", "mission item is: " + missionItem, Logging.White);

            DirectContainer from = null; // = pickup ? Cache.Instance.ItemHangar : Cache.Instance.CargoHold;
            DirectContainer to = null; // = pickup ? Cache.Instance.CargoHold : Cache.Instance.ItemHangar;

            if (_States.CurrentCourierMissionCtrlState == CourierMissionCtrlState.PickupItem || pickup)
            {
                try
                {
                    // We moved the item
                    if (Cache.Instance.CurrentShipsCargo.Items.Any(i => i.TypeName == missionItem))
                    {
                        moveItemRetryCounter = 0;
                        _nextCourierMissionCtrlPulse = DateTime.UtcNow.AddSeconds(3);
                        return true;
                    }

                    //
                    // be flexible on the "from" as we might have the item needed in the ammohangar or loothangar if it is not available in the itemhangar
                    //
                    //from = Cache.Instance.ItemHangar;
                    if (Cache.Instance.ItemHangar.Items.OrderBy(i => i.IsSingleton).ThenBy(i => i.Quantity).Any(i => i.TypeName == missionItem))
                    {
                        from = Cache.Instance.ItemHangar;
                    }
                    else if (!string.IsNullOrEmpty(Settings.Instance.AmmoHangarTabName) && Cache.Instance.DirectEve.Session.SolarSystemId == Cache.Instance.AgentSolarSystemID && Cache.Instance.AmmoHangar.Items.OrderBy(i => i.IsSingleton).ThenBy(i => i.Quantity).Any(i => i.TypeName == missionItem))
                    {
                        from = Cache.Instance.AmmoHangar;
                    }
                    else if (!string.IsNullOrEmpty(Settings.Instance.LootHangarTabName) && Cache.Instance.DirectEve.Session.SolarSystemId == Cache.Instance.AgentSolarSystemID && Cache.Instance.LootHangar.Items.OrderBy(i => i.IsSingleton).ThenBy(i => i.Quantity).Any(i => i.TypeName == missionItem))
                    {
                        from = Cache.Instance.LootHangar;
                    }
                    else
                    {
                        from = Cache.Instance.ItemHangar;
                        //
                        // we cant do the below because we run this routine multiple times after asking the items to move... maybe we need to track that
                        //
                        //Logging.Log("CourierMissionCtrl","Unable to find [" + missionItem + "] in any of the defined hangars - pausing",Logging.Teal);
                        //Cache.Instance.Paused = true;
                    }
                    to = Cache.Instance.CurrentShipsCargo;
                }
                catch (Exception exception)
                {
                    Logging.Log("CourierMissionCtrl", "MoveItem: exception [" + exception + "]", Logging.Red);
                    return false;
                }
            }

            if (_States.CurrentCourierMissionCtrlState == CourierMissionCtrlState.DropOffItem || !pickup)
            {
                from = Cache.Instance.CurrentShipsCargo;
                to = Cache.Instance.ItemHangar;
            }

            // We moved the item
            if (to.Items.Any(i => i.TypeName == missionItem))
            {
                moveItemRetryCounter = 0;
                _nextCourierMissionCtrlPulse = DateTime.UtcNow.AddSeconds(3);
                return true;
            }

            if (directEve.GetLockedItems().Count != 0)
            {
                moveItemRetryCounter++;
                _nextCourierMissionCtrlPulse = DateTime.UtcNow.AddSeconds(3);
                return false;
            }

            // Move items
            foreach (DirectItem item in from.Items.Where(i => i.TypeName == missionItem))
            {
                Logging.Log("CourierMissionCtrl", "Moving [" + item.TypeName + "][" + item.ItemId + "] to " + (pickup ? "cargo" : "hangar"), Logging.White);
                to.Add(item);
                _nextCourierMissionCtrlPulse = DateTime.UtcNow.AddSeconds(7);
                continue;
            }

            //_nextCourierAction = DateTime.UtcNow.AddSeconds(8);
            moveItemRetryCounter++;
            return false;
        }

        /// <summary>
        ///   Goto the pickup location
        ///   Pickup the item
        ///   Goto drop off location
        ///   Drop the item
        ///   Goto Agent
        ///   Complete mission
        /// </summary>
        /// <returns></returns>
        public void ProcessState()
        {
            if (DateTime.UtcNow < _nextCourierMissionCtrlPulse)
                return;

            if (Logging.DebugCourierMissions) Logging.Log("CourierMissionCtrl","CourierMissionCtrlState: [" + _States.CurrentCourierMissionCtrlState.ToString() + "]",Logging.Debug);

            switch (_States.CurrentCourierMissionCtrlState)
            {
                case CourierMissionCtrlState.Idle:
                    break;

                case CourierMissionCtrlState.GotoPickupLocation:
                    if (GotoMissionBookmark(AgentInteraction.AgentId, "Objective (Pick Up)"))
                    {
                        _States.CurrentCourierMissionCtrlState = CourierMissionCtrlState.PickupItem;
                        return;
                    }
                    break;

                case CourierMissionCtrlState.PickupItem:
                    if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20)) return;

                    if (moveItemRetryCounter > 20)
                    {
                        Cache.Instance.Paused = true;
                        Logging.Log("CourierMissionCtrl","MoveItem has tried 20x to Pickup the missionitem and failed. Pausing: please debug the cause of this error",Logging.Red);
                        _States.CurrentCourierMissionCtrlState = CourierMissionCtrlState.Error;
                        return;
                    }

                    if (MoveItem(true))
                    {
                        _States.CurrentCourierMissionCtrlState = CourierMissionCtrlState.GotoDropOffLocation;
                        moveItemRetryCounter = 0;
                        return;
                    }
                    break;

                case CourierMissionCtrlState.GotoDropOffLocation:
                    if (GotoMissionBookmark(AgentInteraction.AgentId, "Objective (Drop Off)"))
                    {
                        _States.CurrentCourierMissionCtrlState = CourierMissionCtrlState.DropOffItem;
                        return;
                    }
                    break;

                case CourierMissionCtrlState.DropOffItem:
                    if (moveItemRetryCounter > 20)
                    {
                        Cache.Instance.Paused = true;
                        Logging.Log("CourierMissionCtrl", "MoveItem has tried 20x to Dropoff the missionitem and failed. Pausing: please debug the cause of this error", Logging.Red);
                        _States.CurrentCourierMissionCtrlState = CourierMissionCtrlState.Error;
                    }

                    if (MoveItem(false))
                    {
                        _States.CurrentCourierMissionCtrlState = CourierMissionCtrlState.CompleteMission;
                        moveItemRetryCounter = 0;
                        return;
                    }
                    break;

                case CourierMissionCtrlState.CompleteMission:
                    //
                    // this state should never be reached in space. if we are in space and in this state we should switch to gotomission
                    //
                    if (Cache.Instance.InSpace)
                    {
                        Logging.Log(_States.CurrentCourierMissionCtrlState.ToString(), "We are in space, how did we get set to this state while in space? Changing state to: GotoBase", Logging.White);
                        _States.CurrentCourierMissionCtrlState = CourierMissionCtrlState.GotoDropOffLocation;
                        return;
                    }

                    if (_States.CurrentAgentInteractionState == AgentInteractionState.Idle)
                    {
                        if (DateTime.UtcNow > Time.Instance.LastInStation.AddSeconds(5) && Cache.Instance.InStation) //do not proceed until we have been docked for at least a few seconds
                        {
                            return;
                        }

                        Logging.Log("AgentInteraction", "Start Conversation [Complete Mission]", Logging.White);

                        _States.CurrentAgentInteractionState = AgentInteractionState.StartConversation;
                        AgentInteraction.Purpose = AgentInteractionPurpose.CompleteMission;
                        return;
                    }

                    AgentInteraction.ProcessState();

                    if (_States.CurrentAgentInteractionState == AgentInteractionState.Done)
                    {
                        _States.CurrentAgentInteractionState = AgentInteractionState.Idle;
                        _States.CurrentCourierMissionCtrlState = CourierMissionCtrlState.Done;
                        return;
                    }
                    break;

                case CourierMissionCtrlState.Done:
                    Logging.Log("CourierMissionCtrl", "Done", Logging.White);
                    break;
            }
        }
    }
}
using EveCom;
using LavishScriptAPI;

namespace EveComFramework.GroupControl
{
    /// <summary>
    /// Fleet autodiscovery
    /// </summary>
    public class FleetAutodiscover
    {
        /// <summary>
        /// Request fleet invite from booster
        /// </summary>
        public static void Request()
        {
            if (Session.InFleet) return;

            try
            {
                LavishScript.ExecuteCommand("relay \"all other\" FleetAutodiscoverRequest " + Me.CharID + " " + Session.SolarSystem.ID);
            }
            catch { }
        }

        /// <summary>
        /// Respond to fleet invite request, telling the character to accept a fleet invite once
        /// </summary>
        /// <param name="CharacterID"></param>
        public static void Respond(long CharacterID)
        {
            if (Session.InFleet) return;

            try
            {
                LavishScript.ExecuteCommand("relay \"all other\" FleetAutodiscoverResponse " + Me.CharID + " " + CharacterID);
            }
            catch { }

            // @TODO: Send Fleet Invite
        }

    }
}

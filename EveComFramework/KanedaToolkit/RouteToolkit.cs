using System.Collections.Generic;
using System.Linq;
using EveCom;

namespace EveComFramework.KanedaToolkit
{
    /// <summary>
    /// Route Toolkit
    /// </summary>
    public class RouteToolkit
    {
        /// <summary>
        /// Get minimum security status along a route
        /// </summary>
        /// <param name="routeList">List of solarSystemIDs along the route</param>
        /// <returns>minimum security status</returns>
        public static double RouteSecurity(List<long> routeList)
        {
            return SolarSystem.All.Where(a => routeList.Contains(a.ID)).Select(a => a.SecurityStatus).Min();
        }
    }
}

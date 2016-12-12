using System.Collections.Generic;
using System.Linq;
using ILoveEVE.Framework;
using ILEF.Caching;
using ILEF.Data;

namespace ILEF.KanedaToolkit
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
            return QMCache.Instance.SolarSystems.Where(a => routeList.Contains(a.Id)).Select(a => a.Security).Min();
        }
    }
}

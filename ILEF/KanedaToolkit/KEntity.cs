#pragma warning disable 1591
using System.Linq;
using ILoveEVE.Framework;
using ILEF.Caching;
using ILEF.Lookup;

namespace ILEF.KanedaToolkit
{
    /// <summary>
    /// extension methods for Entity
    /// </summary>
    public static class KEntity
    {
        /// <summary>
        /// Is this entity warpable? (range, type)
        /// </summary>
        public static bool Warpable(this EntityCache entity)
        {
            if (entity.Distance <= Constants.WarpMinDistance) return false;
            //if (QMCache.Instance.InFleet && entity.IsPlayer && Fleet.Members.Any(a => a.Name == entity.Name)) return true;
            if (entity.CategoryId == (int)CategoryID.Asteroid || entity.CategoryId == (int)CategoryID.Structure ||
                entity.CategoryId == (int)CategoryID.Station || entity.GroupId == (int)Group.CargoContainer ||
                entity.GroupId == (int)Group.Wreck || entity.GroupId == (int)Group.Citadel)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Is this entity a collidable object
        /// </summary>
        public static bool Collidable(this EntityCache entity)
        {
            if (entity.TypeName == "Beacon") return false;
            if (entity.GroupId == (int)Group.LargeCollidableObject || entity.GroupId == (int)Group.LargeCollidableShip ||
                entity.GroupId == (int)Group.LargeCollidableStructure || entity.CategoryId == (int)CategoryID.Asteroid) return true;
            return false;
        }

        /// <summary>
        /// Is this entity dockable
        /// </summary>
        public static bool Dockable(this EntityCache entity)
        {

            if (entity.GroupId == (int)Group.Citadel) return true;
            if (QMCache.Instance.MyShipEntity.GroupId == (int)Group.Titan || QMCache.Instance.MyShipEntity.GroupId == (int)Group.Supercarrier) return false;
            if (entity.GroupId == (int)Group.Station) return true;
            if (QMCache.Instance.MyShipEntity.GroupId == (int)Group.Carrier || QMCache.Instance.MyShipEntity.GroupId == (int)Group.Dreadnought || QMCache.Instance.MyShipEntity.GroupId == (int)Group.ForceAuxiliary) return false;
            return false;
        }

        /// <summary>
        /// Does the entity have a forcefield we are in?
        /// </summary>
        public static bool InsideForcefield(this EntityCache entity)
        {
            if (entity.GroupId != (int)Group.ControlTower) return false;
            return (entity.Distance < (double) entity.ShieldRadius);
        }
    }
}

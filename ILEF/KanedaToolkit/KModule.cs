using System;
using ILoveEVE.Framework;
using ILEF.Caching;
using ILEF.Lookup;

namespace ILEF.KanedaToolkit
{
    /// <summary>
    /// extension methods for Module
    /// </summary>
    public static class KModule
    {
        /**
        /// <summary>
        /// Capacitor required to enable the module
        /// </summary>
        public static double CapacitorNeed(this ModuleCache module)
        {
            return GetDogmaDouble(module, "capacitorNeed");
        }

        /// <summary>
        /// Get module attribute as integer
        /// </summary>
        public static int GetDogmaInt(this DirectModule module, string keyName)
        {
            return (int) module[keyName];
        }

        /// <summary>
        /// Get module attribute as double
        /// </summary>
        public static double GetDogmaDouble(this ModuleCache module, string keyName)
        {
            return (double) module[keyName];
        }

        /// <summary>
        /// Is this module valid for this target?
        /// </summary>
        /// <param name="module"></param>
        /// <param name="target">Entity to check against</param>
        public static bool ValidTarget(this ModuleCache module, EntityCache target)
        {
            // Gas Cloud Harvester
            if (module.GroupId == (int)Group.GasCloudHarvester && target.GroupID != Group.HarvestableCloud) return false;

            // Ice Harvester
            if (MiningToolkit.IceModules.Contains(module.TypeID) && target.GroupID != Group.Ice) return false;

            // Mercoxit
            if (target.GroupID == Group.Mercoxit) return MiningToolkit.MercoxitModules.Contains(module.TypeID);

            // Mining Lasers / Ore
            if (!MiningToolkit.OreGroups.Contains(target.GroupID) &&
                (module.GroupID == Group.MiningLaser ||
                 (module.GroupID == Group.StripMiner && !MiningToolkit.IceModules.Contains(module.TypeID)) ||
                 module.GroupID == Group.FrequencyMiningLaser)) return false;

            // Target painters don't work with control towers
            if (module.GroupID == Group.TargetPainter && target.GroupID == Group.ControlTower) return false;

            // Salvager
            if (module.GroupID == Group.Salvager && target.GroupID != Group.Wreck) return false;

            // Tractor Beam
            if (module.GroupID == Group.TractorBeam && target.GroupID != Group.Wreck &&
                target.GroupID != Group.CargoContainer) return false;

            // return true if we don't know about module copatiblity
            return true;
        }
        **/
    }
}

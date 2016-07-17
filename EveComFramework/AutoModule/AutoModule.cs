#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Linq;
using EveCom;
using EveComFramework.Core;
using EveComFramework.KanedaToolkit;
using EveComFramework.Move;
using EveComFramework.Targets;

namespace EveComFramework.AutoModule
{
    #region Settings

    /// <summary>
    /// Configuration settings for this AutoModule
    /// </summary>
    public class AutoModuleSettings : Settings
    {
        public bool Enabled = false;
        public bool ActiveHardeners = true;
        public bool ShieldBoosters = true;
        public bool ArmorRepairs = true;
        public bool Cloaks = true;
        public bool GangLinks = true;
        public bool SensorBoosters = true;
        public bool TrackingComputers = true;
        public bool ECCMs = true;
        public bool ECMBursts = false;
        public bool NetworkedSensorArray = true;
        public bool DroneTrackingModules = true;
        public bool AutoTargeters = true;
        public bool PropulsionModules = false;
        public bool PropulsionModulesAlwaysOn = false;
        public bool PropulsionModulesApproaching = false;
        public bool PropulsionModulesOrbiting = false;

        public int CapActiveHardeners = 30;
        public int CapShieldBoosters = 30;
        public int CapArmorRepairs = 30;
        public int CapCloaks = 30;
        public int CapGangLinks = 30;
        public int CapSensorBoosters = 30;
        public int CapTrackingComputers = 30;
        public int CapECCMs = 30;
        public int CapECMBursts = 30;
        public int CapNetworkedSensorArray = 30;
        public int CapAutoTargeters = 30;
        public int CapPropulsionModules = 30;
        public int CapDroneTrackingModules = 30;

        public int MaxShieldBoosters = 95;
        public int MaxArmorRepairs = 95;
        public int MinShieldBoosters = 80;
        public int MinArmorRepairs = 80;
        public int MinActiveThreshold = 100;
    }

    #endregion

    /// <summary>
    /// This class manages your ships modules intelligently
    /// </summary>
    public class AutoModule : State
    {
        #region Instantiation

        static AutoModule _Instance;
        /// <summary>
        /// Singletoner
        /// </summary>
        public static AutoModule Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new AutoModule();
                }
                return _Instance;
            }
        }

        private AutoModule()
        {
            DefaultFrequency = 100;
            if (Config.Enabled) QueueState(Control);
        }

        #endregion

        #region Variables

        public Targets.Targets Rats = new Targets.Targets();
        public Logger Console = new Logger("AutoModule");

        /// <summary>
        /// Configuration for this module
        /// </summary>
        public AutoModuleSettings Config = new AutoModuleSettings();

        /// <summary>
        /// Set to true to force automodule to decloak you.  Useful for handling non-covops cloaks.
        /// </summary>
        public bool Decloak = false;

        /// <summary>
        /// Set to true to force automodule to keep your propmod online regardless of state
        /// </summary>
        public bool KeepPropulsionModuleActive = false;

        private bool? _insidePosForceField = false;
        public bool InsidePosForceField
        {
            get
            {
                try
                {
                    _insidePosForceField = Entity.All.Where(i => i.Distance < 60000).Any(b => b.GroupID == Group.ForceField && b.SurfaceDistance <= 0);
                    return _insidePosForceField ?? false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
        #endregion

        #region Actions

        /// <summary>
        /// Start this module
        /// </summary>
        public void Start()
        {
            if (Idle)
            {
                QueueState(Control);
            }

        }

        /// <summary>
        /// Stop this module
        /// </summary>
        public void Stop()
        {
            Clear();
        }

        /// <summary>
        /// Starts/stops this module
        /// </summary>
        /// <param name="Val">True=Start</param>
        public void Enabled(bool Val)
        {
            if (Val)
            {
                if (Idle)
                {
                    QueueState(Control);
                }
            }
            else
            {
                Clear();
            }
        }

        #endregion

        #region States

        private readonly Dictionary<string, DateTime> nextArmorRepAttemptTime = new Dictionary<string, DateTime>();

        bool Control(object[] Params)
        {
            try
            {
                if (!Session.InSpace || !Session.Safe || (Session.InSpace && Session.Safe && MyShip.ToEntity == null))
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            if (UndockWarp.Instance != null && !UndockWarp.Instance.Idle && UndockWarp.Instance.CurState.ToString() != "WaitStation") return false;

            #region Cloaks

            if (Config.Cloaks)
            {
                Module cloakingDevice = MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.CloakingDevice && a.IsOnline);
                if (cloakingDevice != null)
                {
                    if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapCloaks || Decloak)
                    {
                        if (cloakingDevice.IsActive && !cloakingDevice.IsDeactivating)
                        {
                            cloakingDevice.Deactivate();
                        }
                    }
                }
            }

            if (MyShip.ToEntity == null || (MyShip.ToEntity != null && MyShip.ToEntity.Cloaked))
            {
                return false;
            }

            if (Config.Cloaks)
            {
                Module cloakingDevice = MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.CloakingDevice && a.IsOnline);
                if (cloakingDevice != null && (cloakingDevice.TypeID == 11578 || MyShip.ToEntity.Mode != EntityMode.Warping))
                {
                    if (!InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapCloaks && !Decloak && !Entity.All.Any(a => a.Distance < 2000 && a.ID != MyShip.ToEntity.ID))
                    {
                        if (!Entity.All.Any(a => a.IsTargetingMe && !a.Released && !a.Exploded))
                        {
                            if (!cloakingDevice.IsActive && !cloakingDevice.IsActivating && !cloakingDevice.IsDeactivating)
                            {
                                cloakingDevice.Activate();
                            }
                            return false;
                        }
                    }
                }
            }

            if (MyShip.Modules.Any(a => a.GroupID == Group.CloakingDevice && a.IsActive && a.IsOnline)) return false;

            #endregion

            #region Shield Boosters

            if (Config.ShieldBoosters)
            {
                try
                {
                    List<Module> shieldBoosters = MyShip.Modules.Where(a => a.GroupID == Group.ShieldBooster && a.IsOnline).ToList();
                    if (shieldBoosters.Any())
                    {
                        if (shieldBoosters.Any(i => i.AllowsActivate))
                        {
                            if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapShieldBoosters && MyShip.ToEntity.ShieldPct <= Config.MinShieldBoosters)
                            {
                                IEnumerable<Module> activatableShieldBoosters = shieldBoosters.Where(i => i.AllowsActivate);
                                foreach (Module activatableShieldBooster in activatableShieldBoosters)
                                {
                                    //only run one booster per iteration,
                                    //this will potentially save on cap in situations where we have multiple boosters but only need one cycle of one booster at the time
                                    activatableShieldBooster.Activate();
                                    return false;
                                }
                            }
                        }

                        if (shieldBoosters.Any(i => i.AllowsDeactivate))
                        {
                            if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapShieldBoosters && MyShip.ToEntity.ShieldPct <= Config.MinShieldBoosters)
                            {
                                IEnumerable<Module> deactivatableShieldBoosters = shieldBoosters.Where(i => i.AllowsDeactivate);
                                foreach (Module deactivatableShieldBooster in deactivatableShieldBoosters)
                                {
                                    //only turn off one booster per iteration, if we had 2 on its because incomming damage was high...
                                    deactivatableShieldBooster.Deactivate();
                                    return false;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Log("Exception [" + ex + "]");
                }
            }

            #endregion

            #region Armor Repairers

            if (Config.ArmorRepairs)
            {
                try
                {
                    List<Module> armorRepairers = MyShip.Modules.Where(a => a.GroupID == Group.ArmorRepairUnit && a.IsOnline).ToList();
                    if (armorRepairers.Any())
                    {
                        if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapArmorRepairs || MyShip.ToEntity.ArmorPct > Config.MaxArmorRepairs)
                        {
                            foreach (var armorRepairer in armorRepairers.Where(a => a.AllowsDeactivate))
                            {
                                Console.Log("|o[|gArmorRepairer|o] deactivated. ArmorPct [|g" + Math.Round(MyShip.ToEntity.ArmorPct, 1) + "|o] MaxArmorRepairs [|g" + Config.MinArmorRepairs + "|o] C[|g" + Math.Round((MyShip.Capacitor / MyShip.MaxCapacitor * 100), 0) + "|o] CapArmorRepairs [|g" + Config.CapArmorRepairs + "|o]");
                                armorRepairer.Deactivate();
                            }
                        }
                        else if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapArmorRepairs && MyShip.ToEntity.ArmorPct <= Config.MinArmorRepairs)
                        {
                            foreach (var armorRepairer in armorRepairers.Where(a => a.AllowsActivate))
                            {
                                if (!nextArmorRepAttemptTime.ContainsKey(armorRepairer.ID) || (nextArmorRepAttemptTime.ContainsKey(armorRepairer.ID) && DateTime.UtcNow > nextArmorRepAttemptTime[armorRepairer.ID]))
                                {
                                    Console.Log("|o[|gArmorRepairer|o] activated. ArmorPct [|g" + Math.Round(MyShip.ToEntity.ArmorPct, 1) + "|o] is less than MinArmorRepairs [|g" + Config.MinArmorRepairs + "|o]");
                                    armorRepairer.Activate();
                                    //
                                    // if a capital rep - add a timestamp for the next armo rep time to try to avoid cycling the armor rep twice during every repair
                                    // it cycles twice because after the 1st cycle completes there is a slight delay in repairing the armor (milliseconds?)
                                    // remember: armor reps repair at the end of the cyctel, where as shields repair at the beginning of the cycle.
                                    //
                                    if (armorRepairer.Volume > 1000) nextArmorRepAttemptTime.AddOrUpdate(armorRepairer.ID, DateTime.UtcNow.AddSeconds(31));
                                }

                                continue;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Log("Exception [" + ex + "]");
                }
            }

            #endregion

            #region Active Hardeners

            if (Config.ActiveHardeners)
            {

                List<Module> shieldHardeners = MyShip.Modules.Where(a => a.GroupID == Group.ShieldHardener && a.IsOnline).ToList();
                if (shieldHardeners.Any())
                {
                    if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapActiveHardeners && MyShip.ToEntity.ShieldPct <= Config.MinActiveThreshold)
                    {
                        shieldHardeners.Where(a => a.AllowsActivate).ForEach(m => m.Activate());
                    }
                    if (((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapActiveHardeners || MyShip.ToEntity.ShieldPct > Config.MinActiveThreshold))
                    {
                        shieldHardeners.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                    }
                }

                List<Module> armorHardeners = MyShip.Modules.Where(a => (a.GroupID == Group.ArmorHardener || a.GroupID == Group.ArmorResistanceShiftHardener) && a.IsOnline).ToList();
                if (armorHardeners.Any())
                {
                    if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapActiveHardeners && MyShip.ToEntity.ArmorPct <= Config.MinActiveThreshold)
                    {
                        armorHardeners.Where(a => a.AllowsActivate).ForEach(m => m.Activate());
                    }
                    if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapActiveHardeners || MyShip.ToEntity.ArmorPct > Config.MinActiveThreshold)
                    {
                        armorHardeners.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                    }
                }
            }

            #endregion

            #region Gang Links

            if (Config.GangLinks && MyShip.ToEntity.Mode != EntityMode.Warping)
            {
                List<Module> gangLinks = MyShip.Modules.Where(a => a.GroupID == Group.GangCoordinator && a.TypeID != 11014 && a.IsOnline).ToList();
                if (gangLinks.Any())
                {
                    if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapGangLinks)
                    {
                        gangLinks.Where(a => a.AllowsActivate).ForEach(m => m.Activate());
                    }
                    if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapGangLinks)
                    {
                        gangLinks.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                    }
                }
            }

            #endregion

            #region Sensor Boosters

            if (Config.SensorBoosters)
            {
                List<Module> sensorBoosters = MyShip.Modules.Where(a => a.GroupID == Group.SensorBooster && a.IsOnline).ToList();
                if (sensorBoosters.Any())
                {
                    if (!InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapSensorBoosters)
                    {
                        sensorBoosters.Where(a => a.AllowsActivate).ForEach(m => m.Activate());
                    }
                    if (InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapSensorBoosters)
                    {
                        sensorBoosters.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                    }
                }
            }

            #endregion

            #region Tracking Computers

            if (Config.TrackingComputers)
            {
                List<Module> trackingComputers = MyShip.Modules.Where(a => (a.GroupID == Group.TrackingComputer || a.GroupID == Group.MissileGuidanceComputer) && a.IsOnline).ToList();
                if (trackingComputers.Any())
                {
                    if (!InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapTrackingComputers)
                    {
                        trackingComputers.Where(a => a.AllowsActivate).ForEach(m => m.Activate());
                    }
                    if (InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapTrackingComputers)
                    {
                        trackingComputers.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                    }
                }
            }

            #endregion

            #region Drone Tracking Modules

            if (Config.DroneTrackingModules)
            {
                List<Module> droneTrackingModules = MyShip.Modules.Where(a => a.GroupID == Group.DroneTrackingModules && a.IsOnline).ToList();
                if (droneTrackingModules.Any())
                {
                    foreach (Module droneTrackingModule in droneTrackingModules)
                    {
                        if (!InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapDroneTrackingModules && droneTrackingModule.AllowsActivate)
                        {
                            droneTrackingModule.Activate();
                        }
                        if (InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapDroneTrackingModules && droneTrackingModule.AllowsDeactivate)
                        {
                            droneTrackingModule.Deactivate();
                        }
                    }
                }
            }

            #endregion

            #region ECCMs

            if (Config.ECCMs && MyShip.ToEntity.Mode != EntityMode.Warping)
            {
                List<Module> ECCM = MyShip.Modules.Where(a => a.GroupID == Group.ECCM && a.IsOnline).ToList();
                if (ECCM.Any())
                {
                    if (!InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapECCMs)
                    {
                        ECCM.Where(a => a.AllowsActivate).ForEach(m => m.Activate());
                    }
                    if (InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapECCMs)
                    {
                        ECCM.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                    }
                }
            }

            #endregion

            #region ECMBursts

            if (Config.ECMBursts && MyShip.ToEntity.Mode != EntityMode.Warping)
            {
                List<Module> ECMBursts = MyShip.Modules.Where(a => a.GroupID == Group.BurstJammer && a.IsOnline).ToList();
                if (ECMBursts.Any())
                {
                    if (!InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapECMBursts)
                    {
                        ECMBursts.Where(a => a.AllowsActivate).ForEach(m => m.Activate());
                    }
                    if (InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapECMBursts)
                    {
                        ECMBursts.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                    }
                }
            }

            #endregion

            #region Networked Sensor Array

            if (Config.NetworkedSensorArray && MyShip.ToEntity.Mode != EntityMode.Warping)
            {
                List<Module> networkedSensorArrays = MyShip.Modules.Where(a => (int) a.GroupID == 1706 && a.IsOnline).ToList();
                if (networkedSensorArrays.Any())
                {
                    if (!InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapNetworkedSensorArray + 2)
                    {
                        //if (targets.LockedAndLockingTargetList.Count >= targets.LockedTargetList.Count ||
                        //    (targets.LockedTargetList.Count < Entity.All.Where(i => i.Distance < 100000).Count(i => (i.IsNPC || i.IsPC))))
                        //{
                            foreach (var networkedScannerArray in networkedSensorArrays)
                            {
                                if (networkedScannerArray.AllowsActivate)
                                {
                                    Console.Log("|o[|gNetworkedSensorArray|o] activated");
                                    networkedScannerArray.Activate();
                                }
                            }
                        //}
                    }

                    //if (InsidePosForceField) Console.Log("|wAutoModule: InsidePosForceField == true");
                    //if (Rats.LockedTargetList.Count == Entity.All.Where(i => i.Distance < 100000).Count(i => (i.IsNPC || i.IsPC || i.IsAttackingMe || i.IsActiveTarget || i.IsHostile || i.IsTargetingMe))) Console.Log("|wAutoModule: We have everthing locked");
                    //Console.Log("|wAutoModule: targets.LockedTargetList.Count [" + Rats.LockedTargetList.Count + "] NPCs [" + Entity.All.Where(i => i.Distance < 100000).Count(i => (i.IsNPC || i.IsPC || i.IsAttackingMe || i.IsActiveTarget || i.IsHostile || i.IsTargetingMe)) + "]");
                    if (InsidePosForceField || (MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapNetworkedSensorArray)
                    {
                        foreach (var networkedScannerArray in networkedSensorArrays)
                        {
                            if (networkedScannerArray.AllowsDeactivate)
                            {
                                Console.Log("|o[|gNetworkedSensorArray|o] deactivated  InsidePosForceField [|g" + InsidePosForceField + "|o] C[|g" + Math.Round(MyShip.Capacitor / MyShip.MaxCapacitor * 100, 0) + "|o] MinCap [|g" + Config.CapNetworkedSensorArray + "|o]");
                                networkedScannerArray.Deactivate();
                            }
                        }
                    }
                }
            }

            #endregion

            #region AutoTargeters

            if (Config.AutoTargeters && MyShip.ToEntity.Mode != EntityMode.Warping)
            {
                List<Module> autoTargeters = MyShip.Modules.Where(a => a.GroupID == Group.AutomatedTargetingSystem && a.IsOnline).ToList();
                if (autoTargeters.Any())
                {
                    if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapAutoTargeters)
                    {
                        autoTargeters.Where(a => a.AllowsActivate).ForEach(m => m.Activate());
                    }
                    if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapAutoTargeters)
                    {
                        autoTargeters.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                    }
                }
            }

            #endregion

            #region Propulsion Modules

            List<Module> propulsionModules = MyShip.Modules.Where(a => a.GroupID == Group.PropulsionModule && a.IsOnline).ToList();

            if (MyShip.ToEntity.Mode == EntityMode.Warping && !KeepPropulsionModuleActive)
            {
                propulsionModules.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                return false;
            }

            if (Config.PropulsionModules && propulsionModules.Any())
            {
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapPropulsionModules &&
                        ((Config.PropulsionModulesApproaching && MyShip.ToEntity.Mode == EntityMode.Approaching) ||
                        (Config.PropulsionModulesOrbiting && MyShip.ToEntity.Mode == EntityMode.Orbiting) ||
                        Config.PropulsionModulesAlwaysOn))
                {
                    propulsionModules.Where(a => a.AllowsActivate).ForEach(m => m.Activate());
                }
                if (!KeepPropulsionModuleActive && !Config.PropulsionModulesAlwaysOn && ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapPropulsionModules) ||
                    MyShip.ToEntity.Mode == EntityMode.Stopped || MyShip.ToEntity.Mode == EntityMode.Aligned)
                {
                    propulsionModules.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                }
            }

            #endregion

            return false;
        }

        #endregion
    }

}

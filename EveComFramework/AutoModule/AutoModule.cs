#pragma warning disable 1591

namespace EveComFramework.AutoModule
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using EveCom;
    using EveComFramework.Core;
    using EveComFramework.KanedaToolkit;
    using EveComFramework.Move;

    #region Settings

    /// <summary>
    /// Configuration settings for this AutoModule
    /// </summary>
    public class AutoModuleSettings : Settings
    {
        public bool Enabled = true;
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
        public bool KeepPropulsionModuleActive = false;
        public bool CombatBoosters = false;

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
            DefaultFrequency = 400;
            if (Config.Enabled)
            {
                QueueState(WaitForEve, 2000);
                QueueState(Control);
            }
        }

        #endregion

        #region Variables

        public Targets.Targets Rats = new Targets.Targets();
        public Logger Console = new Logger("AutoModule");
        DateTime _evecomSessionIsReady = DateTime.MinValue;
        private readonly Dictionary<string, DateTime> _nextArmorRepAttemptTime = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, DateTime> _nextBoosterAttemptTime = new Dictionary<string, DateTime>();
        public bool UseNetworkedSensorArray = true;
        bool armorRepairerAttributeLogging = false;
        bool shieldRepairerAttributeLogging = false;

        /// <summary>
        /// Configuration for this module
        /// </summary>
        public AutoModuleSettings Config = new AutoModuleSettings();

        /// <summary>
        /// Set to true to force automodule to decloak you.  Useful for handling non-covops cloaks.
        /// </summary>
        public bool Decloak = false;

        private bool? _insidePosForceField = null;
        public bool InsidePosForceField
        {
            get
            {
                try
                {
                    if (_insidePosForceField == null)
                    {
                        _insidePosForceField = Cache.Instance.AllEntities.Any(b => b.GroupID == Group.ForceField && b.SurfaceDistance <= 0);
                        return (bool)_insidePosForceField;
                    }

                    if (_insidePosForceField == null)
                    {
                        return false;
                    }

                    return (bool)_insidePosForceField;
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
                QueueState(WaitForEve, 10000);
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
                    UseNetworkedSensorArray = Config.NetworkedSensorArray;
                    QueueState(WaitForEve, 2000);
                    QueueState(Control);
                }
            }
            else
            {
                Clear();
            }
        }

        private void ClearCachedDataEveryPulse()
        {
            _insidePosForceField = null;
        }

        public void PrepareToDock()
        {
            //
            // eventually seige / bastion / triage all belong here
            //
            Console.Log("Preparing to dock: Disabling use of any NetworkedSensorArray");
            UseNetworkedSensorArray = false;
        }

        public void PrepareToUnDock()
        {
            //
            // eventually seige / bastion / triage all belong here
            //
            Console.Log("Preparing to undock: Enabling use of any NetworkedSensorArray");
            UseNetworkedSensorArray = Config.NetworkedSensorArray;
        }

        #endregion

        #region States

        bool WaitForEve(object[] Params)
        {
            try
            {
                if (Login.AtLogin || CharSel.AtCharSel)
                {
                    //Log.Log("Waiting for Login to complete");
                    return false;
                }

                if (!Session.Safe)
                {
                    Console.Log("Waiting for Session to be safe");
                    return false;
                }

                if (Session.Safe && (Session.InSpace || Session.InStation) && _evecomSessionIsReady.AddSeconds(30) < DateTime.Now)
                {
                    _evecomSessionIsReady = DateTime.Now;
                    //Console.Log("We are InSpace [" + Session.InSpace + "] InStation [" + Session.InStation + "] waiting a few sec");
                    return false;
                }

                if (_evecomSessionIsReady.AddSeconds(3) > DateTime.Now)
                {
                    return false;
                }

                //Console.Log("starting...");
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        bool Control(object[] Params)
        {
            try
            {
                if (!Session.InSpace || !Session.Safe || (Session.InSpace && Session.Safe && Cache.Instance.MyShipAsEntity == null))
                {
                    return false;
                }

                if (!MyShip.ModulesReady)
                {
                    MyShip.PrimeModules();
                    return false;
                }
                ClearCachedDataEveryPulse();

                if (UndockWarp.Instance != null && !UndockWarp.Instance.Idle && UndockWarp.Instance.CurState.ToString() != "WaitStation") return false;
            }
            catch (Exception)
            {
                return false;
            }

            #region Cloaks

            if (Config.Cloaks)
            {
                Module cloakingDevice = Cache.Instance.MyShipsModules.FirstOrDefault(a => a.GroupID == Group.CloakingDevice && a.IsOnline);
                if (cloakingDevice != null)
                {
                    if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapCloaks || Decloak)
                    {
                        if (cloakingDevice.IsActive && !cloakingDevice.IsDeactivating)
                        {
                            cloakingDevice.Deactivate();
                        }
                    }

                    if (Cache.Instance.MyShipAsEntity == null || (Cache.Instance.MyShipAsEntity != null && Cache.Instance.MyShipAsEntity.Cloaked))
                    {
                        return false;
                    }

                    if (cloakingDevice.TypeID == 11578 || Cache.Instance.MyShipAsEntity.Mode != EntityMode.Warping)
                    {
                        try
                        {
                            if (!InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapCloaks && !Decloak && !Cache.Instance.AllEntities.Any(a => a.Distance < 2000 && a.ID != Cache.Instance.MyShipAsEntity.ID))
                            {
                                if (!Cache.Instance.AllEntities.Any(a => a.IsTargetingMe && !a.Released && !a.Exploded))
                                {
                                    if (!cloakingDevice.IsActive && !cloakingDevice.IsActivating && !cloakingDevice.IsDeactivating)
                                    {
                                        cloakingDevice.Activate();
                                    }
                                    return false;
                                }
                            }
                        }
                        catch (Exception) { }
                    }
                }
            }

            //
            // if we are cloaked assume we cannot activate any other modules: return
            //
            if (Cache.Instance.MyShipsModules.Any(a => a.GroupID == Group.CloakingDevice && a.IsActive && a.IsOnline)) return false;

            #endregion

            #region Shield Boosters

            if (Config.ShieldBoosters)
            {
                try
                {
                    if (Config.MinShieldBoosters == Config.MaxShieldBoosters)
                    {
                        Console.Log("ShieldBoosters: MinShieldBoosters[" + Config.MinShieldBoosters + "] cannot be equal to MaxArmorRepairs[" + Config.MaxArmorRepairs + "]: Setting MinShieldBoosters to [" + (Config.MinShieldBoosters - 1) + "]");
                        Config.MinShieldBoosters = Config.MinShieldBoosters - 1;
                    }
                    List<Module> shieldBoosters = Cache.Instance.MyShipsModules.Where(a => a.GroupID == Group.ShieldBooster && a.IsOnline).ToList();
                    if (shieldBoosters.Any())
                    {
                        try
                        {
                            int intAttribute = 0;
                            if (shieldRepairerAttributeLogging)
                            {
                                Console.Log("|oModule is [|g" + shieldBoosters.FirstOrDefault().Name + "|o] Type[|g" + shieldBoosters.FirstOrDefault().Type + "|o] TypeID [|g" + shieldBoosters.FirstOrDefault().TypeID + "|o] GroupID [|g" + shieldBoosters.FirstOrDefault().GroupID + "|o]");
                                foreach (KeyValuePair<string, object> a in shieldBoosters.FirstOrDefault())
                                {
                                    intAttribute++;
                                    Console.Log("Module Attribute [|g" + intAttribute + "|o] Key[|g" + a.Key + "|o] Value [|g" + a.Value.ToString() + "|o]");
                                    shieldRepairerAttributeLogging = false;
                                }
                            }

                            if (shieldBoosters.Any(i => i.AllowsActivate))
                            {
                                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapShieldBoosters && Cache.Instance.MyShipAsEntity.ShieldPct < Config.MinShieldBoosters)
                                {
                                    IEnumerable<Module> activatableShieldBoosters = shieldBoosters.Where(i => i.AllowsActivate);
                                    foreach (Module activatableShieldBooster in activatableShieldBoosters)
                                    {
                                        if (shieldBoosters.Any(i => i.IsActive || i.IsActivating))
                                        {
                                            if (Cache.Instance.MyShipAsEntity.ShieldPct > Config.MinShieldBoosters - ((int) activatableShieldBooster["shieldBonus"] * 2))
                                            {
                                                continue;
                                            }
                                        }
                                        //only run one booster per iteration,
                                        //this will potentially save on cap in situations where we have multiple boosters but only need one cycle of one booster at the time
                                        Console.Log("|o[|gShieldRepairer|o] activated. ShieldPct [|g" + Math.Round(Cache.Instance.MyShipAsEntity.ShieldPct, 1) + "|o] MinShieldRepairs [|g" + Config.MinShieldBoosters + "|o] C[|g" + Math.Round((MyShip.Capacitor / MyShip.MaxCapacitor * 100), 0) + "|o] CapShieldRepairs [|g" + Config.CapShieldBoosters + "|o]");
                                        activatableShieldBooster.Activate();
                                        return false;
                                    }
                                }
                            }

                            if (shieldBoosters.Any(i => i.AllowsDeactivate))
                            {
                                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapShieldBoosters && Cache.Instance.MyShipAsEntity.ShieldPct >= Config.MaxShieldBoosters)
                                {
                                    IEnumerable<Module> deactivatableShieldBoosters = shieldBoosters.Where(i => i.AllowsDeactivate);
                                    foreach (Module deactivatableShieldBooster in deactivatableShieldBoosters)
                                    {
                                        //only turn off one booster per iteration, if we had 2 on its because incomming damage was high...
                                        Console.Log("|o[|gShieldRepairer|o] deactivated. ShieldPct [|g" + Math.Round(Cache.Instance.MyShipAsEntity.ShieldPct, 1) + "|o] MaxShieldRepairs [|g" + Config.MaxShieldBoosters + "|o] C[|g" + Math.Round((MyShip.Capacitor / MyShip.MaxCapacitor * 100), 0) + "|o] CapShieldRepairs [|g" + Config.CapShieldBoosters + "|o]");
                                        deactivatableShieldBooster.Deactivate();
                                        return false;
                                    }
                                }
                            }
                        }
                        catch (Exception){}
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
                    if (Config.MinArmorRepairs == Config.MaxArmorRepairs)
                    {
                        Console.Log("|oArmorRepairs: MinArmorRepairs[|g" + Config.MinArmorRepairs + "|o] cannot be equal to MaxArmorRepairs[|g" + Config.MaxArmorRepairs + "|o]: Setting MinArmorRepaires to [|g" + (Config.MinArmorRepairs - 1) + "o]");
                        Config.MinArmorRepairs = Config.MinArmorRepairs - 1;
                    }
                    List<Module> armorRepairers = Cache.Instance.MyShipsModules.Where(a => a.GroupID == Group.ArmorRepairUnit && a.IsOnline).ToList();
                    if (armorRepairers.Any())
                    {
                        try
                        {
                            int intAttribute = 0;
                            if (armorRepairerAttributeLogging)
                            {
                                Console.Log("|oModule is [|g" + armorRepairers.FirstOrDefault().Name + "|o] Type[|g" + armorRepairers.FirstOrDefault().Type + "|o] TypeID [|g" + armorRepairers.FirstOrDefault().TypeID + "|o] GroupID [|g" + armorRepairers.FirstOrDefault().GroupID + "|o]");
                                foreach (KeyValuePair<string, object> a in armorRepairers.FirstOrDefault())
                                {
                                    intAttribute++;
                                    Console.Log("Module Attribute [|g" + intAttribute + "|o] Key[|g" + a.Key + "|o] Value [|g" + a.Value.ToString() + "|o]");
                                    armorRepairerAttributeLogging = false;
                                }
                            }

                            if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapArmorRepairs || Cache.Instance.MyShipAsEntity.ArmorPct > Config.MaxArmorRepairs)
                            {
                                foreach (Module armorRepairer in armorRepairers.Where(a => a.AllowsDeactivate))
                                {
                                    Console.Log("|o[|gArmorRepairer|o] deactivated. ArmorPct [|g" + Math.Round(Cache.Instance.MyShipAsEntity.ArmorPct, 1) + "|o] MaxArmorRepairs [|g" + Config.MinArmorRepairs + "|o] C[|g" + Math.Round((MyShip.Capacitor / MyShip.MaxCapacitor * 100), 0) + "|o] CapArmorRepairs [|g" + Config.CapArmorRepairs + "|o]");
                                    armorRepairer.Deactivate();
                                }
                            }
                            else if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapArmorRepairs && Cache.Instance.MyShipAsEntity.ArmorPct <= Config.MinArmorRepairs)
                            {
                                foreach (Module armorRepairer in armorRepairers.Where(a => a.AllowsActivate))
                                {
                                    if (armorRepairers.Any(i => i.IsActive || i.IsActivating))
                                    {
                                        try
                                        {
                                            if (Cache.Instance.MyShipAsEntity.ShieldPct > Config.MinShieldBoosters - ((double)armorRepairer["armorDamageAmount"] * 2))
                                            {
                                                continue;
                                            }

                                            Console.Log("|oAnother [|gArmorRepairer|o] rep cycle needed. ArmorPct [|g" + Math.Round(Cache.Instance.MyShipAsEntity.ArmorPct, 1) + "|o] is less than MinArmorRepairs - armorRepairer[armorDamageAmount] / 2 [|g" + (Config.MinArmorRepairs - ((double)armorRepairer["armorDamageAmount"] / 2)) + "|o]");
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.Log("Exception [" + ex + "]");
                                        }
                                    }

                                    if (!_nextArmorRepAttemptTime.ContainsKey(armorRepairer.ID) || (_nextArmorRepAttemptTime.ContainsKey(armorRepairer.ID) && DateTime.UtcNow > _nextArmorRepAttemptTime[armorRepairer.ID]))
                                    {
                                        Console.Log("|o[|gArmorRepairer|o] activated. ArmorPct [|g" + Math.Round(Cache.Instance.MyShipAsEntity.ArmorPct, 1) + "|o] is less than MinArmorRepairs [|g" + Config.MinArmorRepairs + "|o]");
                                        armorRepairer.Activate();
                                        //
                                        // if a capital rep - add a timestamp for the next armo rep time to try to avoid cycling the armor rep twice during every repair
                                        // it cycles twice because after the 1st cycle completes there is a slight delay in repairing the armor (milliseconds?)
                                        // remember: armor reps repair at the end of the cyctel, where as shields repair at the beginning of the cycle.
                                        //
                                        if (armorRepairer.Volume > 1000) _nextArmorRepAttemptTime.AddOrUpdate(armorRepairer.ID, DateTime.UtcNow.AddSeconds(31));
                                    }

                                    continue;
                                }
                            }
                        }
                        catch (Exception) {}
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
                try
                {
                    List<Module> shieldHardeners = Cache.Instance.MyShipsModules.Where(a => a.GroupID == Group.ShieldHardener && a.IsOnline).ToList();
                    if (shieldHardeners.Any())
                    {
                        if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapActiveHardeners && Cache.Instance.MyShipAsEntity.ShieldPct <= Config.MinActiveThreshold)
                        {
                            shieldHardeners.Where(a => a.AllowsActivate).ForEach(m => m.Activate());
                        }
                        if (((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapActiveHardeners || Cache.Instance.MyShipAsEntity.ShieldPct > Config.MinActiveThreshold))
                        {
                            shieldHardeners.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                        }
                    }
                }
                catch (Exception){}

                try
                {
                    List<Module> armorHardeners = Cache.Instance.MyShipsModules.Where(a => (a.GroupID == Group.ArmorHardener || a.GroupID == Group.ArmorResistanceShiftHardener) && a.IsOnline).ToList();
                    if (armorHardeners.Any())
                    {
                        if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapActiveHardeners && Cache.Instance.MyShipAsEntity.ArmorPct <= Config.MinActiveThreshold)
                        {
                            armorHardeners.Where(a => a.AllowsActivate).ForEach(m => m.Activate());
                        }
                        if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapActiveHardeners || Cache.Instance.MyShipAsEntity.ArmorPct > Config.MinActiveThreshold)
                        {
                            armorHardeners.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                        }
                    }
                }
                catch (Exception){}
            }

            #endregion

            #region Gang Link Modules
            List<Module> gangLinkModules = Cache.Instance.MyShipsModules.Where(a => a.GroupID == Group.GangCoordinator && a.TypeID != 11014 && a.IsOnline).ToList();
            if (Config.GangLinks && gangLinkModules.Any())
            {
                try
                {
                    if (Cache.Instance.MyShipAsEntity.Mode != EntityMode.Warping)
                    {
                        if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapGangLinks)
                        {
                            gangLinkModules.Where(a => a.AllowsActivate).ForEach(m => m.Activate());
                        }
                        if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapGangLinks)
                        {
                            gangLinkModules.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                        }
                    }
                }
                catch (Exception){}
            }

            #endregion

            #region Sensor Boosters

            if (Config.SensorBoosters)
            {
                try
                {
                    List<Module> sensorBoosters = Cache.Instance.MyShipsModules.Where(a => a.GroupID == Group.SensorBooster && a.IsOnline).ToList();
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
                catch (Exception){}
            }

            #endregion

            #region Tracking Computers

            if (Config.TrackingComputers)
            {
                try
                {
                    List<Module> trackingComputers = Cache.Instance.MyShipsModules.Where(a => (a.GroupID == Group.TrackingComputer || a.GroupID == Group.MissileGuidanceComputer) && a.IsOnline).ToList();
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
                catch (Exception){}
            }

            #endregion

            #region Drone Tracking Modules

            if (Config.DroneTrackingModules)
            {
                try
                {
                    List<Module> droneTrackingModules = Cache.Instance.MyShipsModules.Where(a => a.GroupID == Group.DroneTrackingModules && a.IsOnline && !a.IsActivating && !a.IsDeactivating).ToList();
                    if (droneTrackingModules.Any())
                    {
                        foreach (Module droneTrackingModule in droneTrackingModules)
                        {
                            if (!InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapDroneTrackingModules)
                            {
                                if (droneTrackingModule.AllowsActivate)
                                {
                                    droneTrackingModule.Activate();
                                    return false;
                                }
                            }
                            if (InsidePosForceField && (MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapDroneTrackingModules)
                            {
                                if (droneTrackingModule.AllowsDeactivate)
                                {
                                    droneTrackingModule.Deactivate();
                                    return false;
                                }
                            }
                        }
                    }
                }
                catch (Exception ){} //swallow the exception here: this seems to generate an exception once on every startup
            }

            #endregion

            #region ECCMs

            if (Config.ECCMs && Cache.Instance.MyShipAsEntity.Mode != EntityMode.Warping)
            {
                try
                {
                    List<Module> ECCM = Cache.Instance.MyShipsModules.Where(a => a.GroupID == Group.ECCM && a.IsOnline).ToList();
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
                catch (Exception){}
            }

            #endregion

            #region ECMBursts

            if (Config.ECMBursts && Cache.Instance.MyShipAsEntity.Mode != EntityMode.Warping)
            {
                try
                {
                    List<Module> ECMBursts = Cache.Instance.MyShipsModules.Where(a => a.GroupID == Group.BurstJammer && a.IsOnline).ToList();
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
                catch (Exception){}
            }

            #endregion

            #region Networked Sensor Array

            if (UseNetworkedSensorArray && Cache.Instance.MyShipAsEntity.Mode != EntityMode.Warping)
            {
                try
                {
                    List<Module> networkedSensorArrays = Cache.Instance.MyShipsModules.Where(a => (int)a.GroupID == 1706 && a.IsOnline).ToList();
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
                catch (Exception){}
            }

            #endregion

            #region AutoTargeters

            if (Config.AutoTargeters && Cache.Instance.MyShipAsEntity.Mode != EntityMode.Warping)
            {
                try
                {
                    List<Module> autoTargeters = Cache.Instance.MyShipsModules.Where(a => a.GroupID == Group.AutomatedTargetingSystem && a.IsOnline).ToList();
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
                catch (Exception){}
            }

            #endregion

            #region Propulsion Modules
            List<Module> propulsionModules = Cache.Instance.MyShipsModules.Where(a => a.GroupID == Group.PropulsionModule && a.IsOnline).ToList();
            if (Config.PropulsionModules && propulsionModules.Any())
            {
                try
                {
                    if (Cache.Instance.MyShipAsEntity.Mode == EntityMode.Warping && !Config.KeepPropulsionModuleActive)
                    {
                        propulsionModules.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                        return false;
                    }

                    if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapPropulsionModules && ((Config.PropulsionModulesApproaching && Cache.Instance.MyShipAsEntity.Mode == EntityMode.Approaching) || (Config.PropulsionModulesApproaching && Cache.Instance.MyShipAsEntity.Mode == EntityMode.Aligned) || (Config.PropulsionModulesOrbiting && Cache.Instance.MyShipAsEntity.Mode == EntityMode.Orbiting) || Config.PropulsionModulesAlwaysOn))
                    {
                        propulsionModules.Where(a => a.AllowsActivate).ForEach(m => m.Activate());
                    }

                    if (!Config.KeepPropulsionModuleActive && !Config.PropulsionModulesAlwaysOn && ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapPropulsionModules) || Cache.Instance.MyShipAsEntity.Mode == EntityMode.Stopped || Cache.Instance.MyShipAsEntity.Mode == EntityMode.Aligned)
                    {
                        propulsionModules.Where(a => a.AllowsDeactivate).ForEach(m => m.Deactivate());
                    }
                }
                catch (Exception) {}
            }

            #endregion

            #region Combat Boosters

            if (Config.CombatBoosters)
            {
                try
                {
                    DateTime timeBoostersShouldWearOff = Session.BoosterTimer;
                    if (timeBoostersShouldWearOff == null || timeBoostersShouldWearOff < DateTime.Now)
                    {
                        //
                        // find a booster to take in the local cargo: this needs to eventually be based on a setting to choose which booster to take
                        // as we could have multiple choices of boosters in our cargo!
                        //
                    }
                }
                catch (Exception ex)
                {
                    Console.Log("Exception [" + ex + "]");
                }
            }

            #endregion

            return false;
        }

        #endregion
    }

}

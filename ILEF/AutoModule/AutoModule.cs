#pragma warning disable 1591

namespace ILEF.AutoModule
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    //using ILoveEVE.Framework;
    using ILEF.Caching;
    using ILEF.Core;
    using ILEF.KanedaToolkit;
    using ILEF.Move;
    using ILEF.Lookup;

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
        private readonly Dictionary<long, DateTime> _nextArmorRepAttemptTime = new Dictionary<long, DateTime>();
        private readonly Dictionary<long, DateTime> _nextBoosterAttemptTime = new Dictionary<long, DateTime>();
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
                        _insidePosForceField = QMCache.Instance.Entities.Any(b => b.GroupId == (int)Group.ForceField && b.Distance <= 0);
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
                if (Cache.Instance.DirectEve.Login.AtLogin || Cache.Instance.DirectEve.Login.AtCharacterSelection)
                {
                    //Log.Log("Waiting for Login to complete");
                    return false;
                }

                if ((QMCache.Instance.InSpace || QMCache.Instance.InStation) && _evecomSessionIsReady.AddSeconds(30) < DateTime.Now)
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
                if (!QMCache.Instance.InSpace)
                {
                    return false;
                }

                if (!QMCache.Instance.Modules.Any())
                {
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
                ModuleCache cloakingDevice = Cache.Instance.MyShipsModules.FirstOrDefault(a => a.GroupId == (int)Group.CloakingDevice && a.IsOnline);
                if (cloakingDevice != null)
                {
                    if ((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) < Config.CapCloaks || Decloak)
                    {
                        if (cloakingDevice.IsActive && !cloakingDevice.IsDeactivating)
                        {
                            cloakingDevice.Deactivate();
                        }
                    }

                    if (QMCache.Instance.ActiveShip.Entity == null || (QMCache.Instance.ActiveShip.Entity != null && QMCache.Instance.ActiveShip.Entity.IsCloaked))
                    {
                        return false;
                    }

                    if (cloakingDevice.TypeId == 11578 || QMCache.Instance.MyShipEntity.Mode != 3)
                    {
                        try
                        {
                            if (!InsidePosForceField && (QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapCloaks && !Decloak && !QMCache.Instance.Entities.Any(a => a.Distance < 2000 && a.Id != QMCache.Instance.MyShipEntity.Id))
                            {
                                if (!QMCache.Instance.Entities.Any(a => a.IsTargetedBy && !a.HasReleased && !a.HasExploded))
                                {
                                    if (!cloakingDevice.IsActive && !cloakingDevice.IsDeactivating)
                                    {
                                        cloakingDevice.Click();
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
            if (Cache.Instance.MyShipsModules.Any(a => a.GroupId == (int)Group.CloakingDevice && a.IsActive && a.IsOnline)) return false;

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

                    List<ModuleCache> shieldBoosters = Cache.Instance.MyShipsModules.Where(a => a.GroupId == (int)Group.ShieldBooster && a.IsOnline).ToList();
                    if (shieldBoosters.Any())
                    {
                        try
                        {
                            //int intAttribute = 0;
                            //if (shieldRepairerAttributeLogging)
                            //{
                                //Console.Log("|oModule is [|g" + shieldBoosters.FirstOrDefault().TypeName + "|o] Type[|g" + shieldBoosters.FirstOrDefault().TypeId + "|o] TypeID [|g" + shieldBoosters.FirstOrDefault().TypeId + "|o] GroupID [|g" + shieldBoosters.FirstOrDefault().GroupId + "|o]");
                                //foreach (KeyValuePair<string, object> a in shieldBoosters.FirstOrDefault())
                                //{
                                //    intAttribute++;
                                //    Console.Log("Module Attribute [|g" + intAttribute + "|o] Key[|g" + a.Key + "|o] Value [|g" + a.Value.ToString() + "|o]");
                                //    shieldRepairerAttributeLogging = false;
                                //}
                            //}

                            if (shieldBoosters.Any(i => i.IsActivatable))
                            {
                                if ((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapShieldBoosters && QMCache.Instance.MyShipEntity.ShieldPct < Config.MinShieldBoosters)
                                {
                                    IEnumerable<ModuleCache> activatableShieldBoosters = shieldBoosters.Where(i => i.IsActivatable);
                                    foreach (ModuleCache activatableShieldBooster in activatableShieldBoosters)
                                    {
                                        if (shieldBoosters.Any(i => i.IsActive))
                                        {
                                            if (QMCache.Instance.MyShipEntity.ShieldPct > Config.MinShieldBoosters - ((int) activatableShieldBooster.ShieldBonus * 2)) //["shieldBonus"]
                                            {
                                                continue;
                                            }
                                        }
                                        //only run one booster per iteration,
                                        //this will potentially save on cap in situations where we have multiple boosters but only need one cycle of one booster at the time
                                        Console.Log("|o[|gShieldRepairer|o] activated. ShieldPct [|g" + Math.Round(QMCache.Instance.MyShipEntity.ShieldPct, 1) + "|o] MinShieldRepairs [|g" + Config.MinShieldBoosters + "|o] C[|g" + Math.Round((Cache.Instance.ActiveShip.Capacitor / Cache.Instance.ActiveShip.MaxCapacitor * 100), 0) + "|o] CapShieldRepairs [|g" + Config.CapShieldBoosters + "|o]");
                                        activatableShieldBooster.Click();
                                        return false;
                                    }
                                }
                            }

                            if (shieldBoosters.Any(i => i.IsActive && !i.InLimboState))
                            {
                                if ((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapShieldBoosters && QMCache.Instance.MyShipEntity.ShieldPct >= Config.MaxShieldBoosters)
                                {
                                    IEnumerable<ModuleCache> deactivatableShieldBoosters = shieldBoosters.Where(i => !i.InLimboState && i.IsActive);
                                    foreach (ModuleCache deactivatableShieldBooster in deactivatableShieldBoosters)
                                    {
                                        //only turn off one booster per iteration, if we had 2 on its because incomming damage was high...
                                        Console.Log("|o[|gShieldRepairer|o] deactivated. ShieldPct [|g" + Math.Round(QMCache.Instance.MyShipEntity.ShieldPct, 1) + "|o] MaxShieldRepairs [|g" + Config.MaxShieldBoosters + "|o] C[|g" + Math.Round((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100), 0) + "|o] CapShieldRepairs [|g" + Config.CapShieldBoosters + "|o]");
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
                    List<ModuleCache> armorRepairers = Cache.Instance.MyShipsModules.Where(a => a.GroupId == (int)Group.ArmorRepairUnit && a.IsOnline).ToList();
                    if (armorRepairers.Any())
                    {
                        try
                        {
                            int intAttribute = 0;
                            //if (armorRepairerAttributeLogging)
                            //{
                            //    Console.Log("|oModule is [|g" + armorRepairers.FirstOrDefault().TypeName + "|o] Type[|g" + armorRepairers.FirstOrDefault().Type + "|o] TypeID [|g" + armorRepairers.FirstOrDefault().TypeID + "|o] GroupID [|g" + armorRepairers.FirstOrDefault().GroupID + "|o]");
                            //    foreach (KeyValuePair<string, object> a in armorRepairers.FirstOrDefault())
                            //    {
                            //        intAttribute++;
                            //        Console.Log("Module Attribute [|g" + intAttribute + "|o] Key[|g" + a.Key + "|o] Value [|g" + a.Value.ToString() + "|o]");
                            //        armorRepairerAttributeLogging = false;
                            //    }
                            //}

                            if ((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) < Config.CapArmorRepairs || QMCache.Instance.MyShipEntity.ArmorPct > Config.MaxArmorRepairs)
                            {
                                foreach (ModuleCache armorRepairer in armorRepairers.Where(a => a.IsActive && !a.InLimboState))
                                {
                                    Console.Log("|o[|gArmorRepairer|o] deactivated. ArmorPct [|g" + Math.Round(QMCache.Instance.MyShipEntity.ArmorPct, 1) + "|o] MaxArmorRepairs [|g" + Config.MinArmorRepairs + "|o] C[|g" + Math.Round((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100), 0) + "|o] CapArmorRepairs [|g" + Config.CapArmorRepairs + "|o]");
                                    armorRepairer.Deactivate();
                                }
                            }
                            else if ((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapArmorRepairs && QMCache.Instance.MyShipEntity.ArmorPct <= Config.MinArmorRepairs)
                            {
                                foreach (ModuleCache  armorRepairer in armorRepairers.Where(a => a.IsActive && !a.InLimboState))
                                {
                                    if (armorRepairers.Any(i => i.IsActive))
                                    {
                                        try
                                        {
                                            if (QMCache.Instance.MyShipEntity.ShieldPct > Config.MinShieldBoosters - ((double)armorRepairer.ArmorDamageAmount * 2)) //["armorDamageAmount"]
                                            {
                                                continue;
                                            }

                                            Console.Log("|oAnother [|gArmorRepairer|o] rep cycle needed. ArmorPct [|g" + Math.Round(QMCache.Instance.MyShipEntity.ArmorPct, 1) + "|o] is less than MinArmorRepairs - armorRepairer[armorDamageAmount] / 2 [|g" + (Config.MinArmorRepairs - ((double)armorRepairer.ArmorDamageAmount / 2)) + "|o]"); //["armorDamageAmount"]
    }
                                        catch (Exception ex)
                                        {
                                            Console.Log("Exception [" + ex + "]");
                                        }
                                    }

                                    if (!_nextArmorRepAttemptTime.ContainsKey(armorRepairer.ItemId) || (_nextArmorRepAttemptTime.ContainsKey(armorRepairer.ItemId) && DateTime.UtcNow > _nextArmorRepAttemptTime[armorRepairer.ItemId]))
                                    {
                                        Console.Log("|o[|gArmorRepairer|o] activated. ArmorPct [|g" + Math.Round(QMCache.Instance.MyShipEntity.ArmorPct, 1) + "|o] is less than MinArmorRepairs [|g" + Config.MinArmorRepairs + "|o]");
                                        armorRepairer.Click();
                                        //
                                        // if a capital rep - add a timestamp for the next armo rep time to try to avoid cycling the armor rep twice during every repair
                                        // it cycles twice because after the 1st cycle completes there is a slight delay in repairing the armor (milliseconds?)
                                        // remember: armor reps repair at the end of the cyctel, where as shields repair at the beginning of the cycle.
                                        //
                                        if (armorRepairer.Duration > 30) _nextArmorRepAttemptTime.AddOrUpdate(armorRepairer.ItemId, DateTime.UtcNow.AddSeconds(31));
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
                    List<ModuleCache> shieldHardeners = Cache.Instance.MyShipsModules.Where(a => a.GroupId == (int)Group.ShieldHardener && a.IsOnline).ToList();
                    if (shieldHardeners.Any())
                    {
                        if ((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapActiveHardeners && QMCache.Instance.MyShipEntity.ShieldPct <= Config.MinActiveThreshold)
                        {
                            shieldHardeners.Where(a => !a.IsActive && !a.InLimboState).ForEach(m => m.Click());
                        }
                        if (((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) < Config.CapActiveHardeners || QMCache.Instance.MyShipEntity.ShieldPct > Config.MinActiveThreshold))
                        {
                            shieldHardeners.Where(a => a.IsActive && !a.InLimboState).ForEach(m => m.Deactivate());
                        }
                    }
                }
                catch (Exception){}

                try
                {
                    List<ModuleCache> armorHardeners = Cache.Instance.MyShipsModules.Where(a => (a.GroupId == (int)Group.ArmorHardener || a.GroupId == (int)Group.ArmorResistanceShiftHardener) && a.IsOnline).ToList();
                    if (armorHardeners.Any())
                    {
                        if ((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapActiveHardeners && QMCache.Instance.MyShipEntity.ArmorPct <= Config.MinActiveThreshold)
                        {
                            armorHardeners.Where(a => !a.IsActive && !a.InLimboState).ForEach(m => m.Click());
                        }
                        if ((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) < Config.CapActiveHardeners || QMCache.Instance.MyShipEntity.ArmorPct > Config.MinActiveThreshold)
                        {
                            armorHardeners.Where(a => a.IsActive && !a.InLimboState).ForEach(m => m.Deactivate());
                        }
                    }
                }
                catch (Exception){}
            }

            #endregion

            /**
            #region Gang Link Modules
            List<ModuleCache> gangLinkModules = Cache.Instance.MyShipsModules.Where(a => a.GroupId == (int)Group.GangCoordinator && a.TypeId != 11014 && a.IsOnline).ToList();
            if (Config.GangLinks && gangLinkModules.Any())
            {
                try
                {
                    if (QMCache.Instance.MyShipEntity.Mode != 3)
                    {
                        if ((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapGangLinks)
                        {
                            gangLinkModules.Where(a => !a.IsActive && !a.InLimboState).ForEach(m => m.Click());
                        }
                        if ((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) < Config.CapGangLinks)
                        {
                            gangLinkModules.Where(a => a.IsActive && !a.InLimboState).ForEach(m => m.Deactivate());
                        }
                    }
                }
                catch (Exception){}
            }
            **/
            #endregion

            #region Sensor Boosters

            if (Config.SensorBoosters)
            {
                try
                {
                    List<ModuleCache> sensorBoosters = Cache.Instance.MyShipsModules.Where(a => a.GroupId == (int)Group.SensorBooster && a.IsOnline).ToList();
                    if (sensorBoosters.Any())
                    {
                        if (!InsidePosForceField && (QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapSensorBoosters)
                        {
                            sensorBoosters.Where(a => !a.IsActive && !a.InLimboState).ForEach(m => m.Click());
                        }
                        if (InsidePosForceField && (QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) < Config.CapSensorBoosters)
                        {
                            sensorBoosters.Where(a => a.IsActive && !a.InLimboState).ForEach(m => m.Deactivate());
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
                    List<ModuleCache> trackingComputers = Cache.Instance.MyShipsModules.Where(a => (a.GroupId == (int)Group.TrackingComputer) && a.IsOnline).ToList(); //|| a.GroupId == (int)Group.MissileGuidanceComputer) && a.IsOnline).ToList();
                    if (trackingComputers.Any())
                    {
                        if (!InsidePosForceField && (QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapTrackingComputers)
                        {
                            trackingComputers.Where(a => !a.IsActive && !a.InLimboState).ForEach(m => m.Click());
                        }
                        if (InsidePosForceField && (QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) < Config.CapTrackingComputers)
                        {
                            trackingComputers.Where(a => a.IsActive && !a.InLimboState).ForEach(m => m.Deactivate());
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
                    List<ModuleCache> droneTrackingModules = Cache.Instance.MyShipsModules.Where(a => a.GroupId == (int)Group.DroneTrackingModules && a.IsOnline && !a.InLimboState && !a.IsDeactivating).ToList();
                    if (droneTrackingModules.Any())
                    {
                        foreach (ModuleCache droneTrackingModule in droneTrackingModules)
                        {
                            if (!InsidePosForceField && (QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapDroneTrackingModules)
                            {
                                if (!droneTrackingModule.IsActive && !droneTrackingModule.InLimboState)
                                {
                                    droneTrackingModule.Click();
                                    return false;
                                }
                            }
                            if (InsidePosForceField && (QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) < Config.CapDroneTrackingModules)
                            {
                                if (droneTrackingModule.IsActive && !droneTrackingModule.InLimboState)
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

            if (Config.ECCMs && QMCache.Instance.MyShipEntity.Mode != 3)
            {
                try
                {
                    List<ModuleCache> ECCM = Cache.Instance.MyShipsModules.Where(a => a.GroupId == (int)Group.ECCM && a.IsOnline).ToList();
                    if (ECCM.Any())
                    {
                        if (!InsidePosForceField && (QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapECCMs)
                        {
                            ECCM.Where(a => !a.IsActive && !a.InLimboState).ForEach(m => m.Click());
                        }
                        if (InsidePosForceField && (QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) < Config.CapECCMs)
                        {
                            ECCM.Where(a => a.IsActive && !a.InLimboState).ForEach(m => m.Deactivate());
                        }
                    }
                }
                catch (Exception){}
            }

            #endregion

            #region ECMBursts

            if (Config.ECMBursts && QMCache.Instance.MyShipEntity.Mode != 3)
            {
                try
                {
                    List<ModuleCache> ECMBursts = Cache.Instance.MyShipsModules.Where(a => a.GroupId == (int)Group.BurstJammer && a.IsOnline).ToList();
                    if (ECMBursts.Any())
                    {
                        if (!InsidePosForceField && (QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapECMBursts)
                        {
                            ECMBursts.Where(a => !a.IsActive && !a.InLimboState).ForEach(m => m.Click());
                        }
                        if (InsidePosForceField && (QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) < Config.CapECMBursts)
                        {
                            ECMBursts.Where(a => a.IsActive && !a.InLimboState).ForEach(m => m.Deactivate());
                        }
                    }
                }
                catch (Exception){}
            }

            #endregion

            #region Networked Sensor Array

            if (UseNetworkedSensorArray && QMCache.Instance.MyShipEntity.Mode != 3)
            {
                try
                {
                    List<ModuleCache> networkedSensorArrays = Cache.Instance.MyShipsModules.Where(a => (int)a.GroupId == 1706 && a.IsOnline).ToList();
                    if (networkedSensorArrays.Any())
                    {
                        if (!InsidePosForceField && (QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapNetworkedSensorArray + 2)
                        {
                            //if (targets.LockedAndLockingTargetList.Count >= targets.LockedTargetList.Count ||
                            //    (targets.LockedTargetList.Count < Entity.All.Where(i => i.Distance < 100000).Count(i => (i.IsNPC || i.IsPC))))
                            //{
                            foreach (var networkedScannerArray in networkedSensorArrays)
                            {
                                if (!networkedScannerArray.IsActive && !networkedScannerArray.InLimboState)
                                {
                                    Console.Log("|o[|gNetworkedSensorArray|o] activated");
                                    networkedScannerArray.Click();
                                }
                            }
                            //}
                        }

                        //if (InsidePosForceField) Console.Log("|wAutoModule: InsidePosForceField == true");
                        //if (Rats.LockedTargetList.Count == Entity.All.Where(i => i.Distance < 100000).Count(i => (i.IsNPC || i.IsPC || i.IsAttackingMe || i.IsActiveTarget || i.IsHostile || i.IsTargetingMe))) Console.Log("|wAutoModule: We have everthing locked");
                        //Console.Log("|wAutoModule: targets.LockedTargetList.Count [" + Rats.LockedTargetList.Count + "] NPCs [" + Entity.All.Where(i => i.Distance < 100000).Count(i => (i.IsNPC || i.IsPC || i.IsAttackingMe || i.IsActiveTarget || i.IsHostile || i.IsTargetingMe)) + "]");
                        if (InsidePosForceField || (QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) < Config.CapNetworkedSensorArray)
                        {
                            foreach (var networkedScannerArray in networkedSensorArrays)
                            {
                                if (networkedScannerArray.IsActive && !networkedScannerArray.InLimboState)
                                {
                                    Console.Log("|o[|gNetworkedSensorArray|o] deactivated  InsidePosForceField [|g" + InsidePosForceField + "|o] C[|g" + Math.Round(QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100, 0) + "|o] MinCap [|g" + Config.CapNetworkedSensorArray + "|o]");
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

            if (Config.AutoTargeters && QMCache.Instance.MyShipEntity.Mode != 3)
            {
                try
                {
                    List<ModuleCache> autoTargeters = Cache.Instance.MyShipsModules.Where(a => a.GroupId == (int)Group.AutomatedTargetingSystem && a.IsOnline).ToList();
                    if (autoTargeters.Any())
                    {
                        if ((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapAutoTargeters)
                        {
                            autoTargeters.Where(a => !a.IsActive && !a.InLimboState).ForEach(m => m.Click());
                        }
                        if ((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) < Config.CapAutoTargeters)
                        {
                            autoTargeters.Where(a => a.IsActive && !a.InLimboState).ForEach(m => m.Deactivate());
                        }
                    }
                }
                catch (Exception){}
            }

            #endregion

            #region Propulsion Modules
            List<ModuleCache> propulsionModules = Cache.Instance.MyShipsModules.Where(a => a.GroupId == (int)Group.PropulsionModule && a.IsOnline).ToList();
            if (Config.PropulsionModules && propulsionModules.Any())
            {
                try
                {
                    if (QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping && !Config.KeepPropulsionModuleActive)
                    {
                        propulsionModules.Where(a => a.IsActive && !a.InLimboState).ForEach(m => m.Deactivate());
                        return false;
                    }

                    if ((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) > Config.CapPropulsionModules && ((Config.PropulsionModulesApproaching && QMCache.Instance.MyShipEntity.Mode == (int)EntityMode.Approaching) || (Config.PropulsionModulesApproaching && QMCache.Instance.MyShipEntity.Mode == (int)EntityMode.Aligned) || (Config.PropulsionModulesOrbiting && QMCache.Instance.MyShipEntity.Mode == 4) || Config.PropulsionModulesAlwaysOn))
                    {
                        propulsionModules.Where(a => !a.IsActive && !a.InLimboState).ForEach(m => m.Click());
                    }

                    if (!Config.KeepPropulsionModuleActive && !Config.PropulsionModulesAlwaysOn && ((QMCache.Instance.ActiveShip.Capacitor / QMCache.Instance.ActiveShip.MaxCapacitor * 100) < Config.CapPropulsionModules) || QMCache.Instance.MyShipEntity.Mode == (int)EntityMode.Stopped || QMCache.Instance.MyShipEntity.Mode == (int)EntityMode.Aligned)
                    {
                        propulsionModules.Where(a => a.IsActive && !a.InLimboState).ForEach(m => m.Deactivate());
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
                    //DateTime timeBoostersShouldWearOff = Session.BoosterTimer;
                    //if (timeBoostersShouldWearOff == null || timeBoostersShouldWearOff < DateTime.Now)
                    //{
                        //
                        // find a booster to take in the local cargo: this needs to eventually be based on a setting to choose which booster to take
                        // as we could have multiple choices of boosters in our cargo!
                        //
                    //}
                }
                catch (Exception ex)
                {
                    Console.Log("Exception [" + ex + "]");
                }
            }

            #endregion

            return false;
        }

        //#endregion
    }

}

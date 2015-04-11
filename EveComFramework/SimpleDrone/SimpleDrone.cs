using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EveCom;
using EveComFramework.Core;
using EveComFramework.Targets;
using EveComFramework.Security;

namespace EveComFramework.SimpleDrone
{
    #region Enums

    public enum Mode
    {
        None,
        Sentry,
        Fighter,
        FighterSupport,
        PointDefense,
        FighterPointDefense,
        AgressiveScout,
        AgressiveMedium,
        AgressiveMediumGila,
        AFKHeavy,
        AgressiveHeavy,
        AgressiveSentry
    }

    #endregion

    #region Settings

    public class SimpleDroneSettings : EveComFramework.Core.Settings
    {
        public Mode Mode = Mode.None;
        public bool PrivateTargets = true;
        public bool SharedTargets = false;
        public int TargetSlots = 2;
        //public bool AutoChangeDroneModeIfWeDoNotHaveAppropriateDrones = false;
    }

    #endregion

    public class SimpleDrone : State
    {
        #region Instantiation

        static SimpleDrone _Instance;
        public static SimpleDrone Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new SimpleDrone();
                }
                return _Instance;
            }
        }

        private SimpleDrone() : base()
        {
            Rats.AddPriorityTargets();
            Rats.AddNPCs();
            Rats.AddTargetingMe();

            Rats.Ordering = new RatComparer();
        }

        #endregion

        #region Variables

        public Core.Logger Console = new Core.Logger("SimpleDrone");
        public SimpleDroneSettings Config = new SimpleDroneSettings();
        Targets.Targets Rats = new Targets.Targets();
        Security.Security SecurityCore = Security.Security.Instance;
        HashSet<Drone> DroneCooldown = new HashSet<Drone>();
        Dictionary<Drone, double> DroneHealthCache = new Dictionary<Drone, double>();
        IPC IPC = IPC.Instance;

        public List<string> PriorityTargets = new List<string>();
        public List<string> Triggers = new List<string>();
        private int _m3PerSmallestDrone = 5;
        private int _m3PerDrone = 5;
        
        #endregion

        #region Actions

        public void Enabled(bool var)
        {
            if (var)
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

        Entity ActiveTarget;
        Dictionary<Entity, DateTime> TargetCooldown = new Dictionary<Entity, DateTime>();
        bool OutOfTargets = false;
        Dictionary<Drone, DateTime> NextDroneCommand = new Dictionary<Drone, DateTime>();
        bool DroneReady(Drone drone)
        {
            if (!NextDroneCommand.ContainsKey(drone)) return true;
            if (NextDroneCommand[drone] < DateTime.Now) return true;
            return false;
        }

        private void SetSimpleDroneModeBasedOnLagestAvailableDrones()
        {
            //if (!Config.AutoChangeDroneModeIfWeDoNotHaveAppropriateDrones) return;

            string DroneTypeToLookFor = string.Empty;
            DroneTypeToLookFor = "Fighters";
            if (Drone.AllInBay.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == DroneTypeToLookFor)))
            {
                Config.Mode = Mode.Fighter;
                Console.Log("|o We have [" + DroneTypeToLookFor + "] available: Setting SimpleDrone Mode to [" + Config.Mode.ToString() + "]");
                return;
            }

            DroneTypeToLookFor = "Sentry Drones";
            if (Drone.AllInBay.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == DroneTypeToLookFor)))
            {
                Config.Mode = Mode.AgressiveSentry;
                Console.Log("|o We have [" + DroneTypeToLookFor + "] available: Setting SimpleDrone Mode to [" + Config.Mode.ToString() + "]");
                return;
            }

            DroneTypeToLookFor = "Heavy Attack Drones";
            if (Drone.AllInBay.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == DroneTypeToLookFor)))
            {
                Config.Mode = Mode.AgressiveHeavy;
                Console.Log("|o We have [" + DroneTypeToLookFor + "] available: Setting SimpleDrone Mode to [" + Config.Mode.ToString() + "]");
                return;
            }

            DroneTypeToLookFor = "Medium Scout Drones";
            if (Drone.AllInBay.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == DroneTypeToLookFor)))
            {
                Config.Mode = Mode.AgressiveMedium;
                Console.Log("|o We have [" + DroneTypeToLookFor + "] available: Setting SimpleDrone Mode to [" + Config.Mode.ToString() + "]");
                return;
            }

            DroneTypeToLookFor = "Light Scout Drones";
            if (Drone.AllInBay.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == DroneTypeToLookFor)))
            {
                Config.Mode = Mode.AgressiveMedium;
                Console.Log("|o We have [" + DroneTypeToLookFor + "] available: Setting SimpleDrone Mode to [" + Config.Mode.ToString() + "]");
                return;
            }
            
            return;
        }

        private string FindSmallestDroneGroupAvailable()
        {
            //if (!Config.AutoChangeDroneModeIfWeDoNotHaveAppropriateDrones) return;
            string SmallDroneTypeToLookFor;
            SmallDroneTypeToLookFor = "Light Scout Drones";
            if (Drone.AllInBay.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == SmallDroneTypeToLookFor)) || Drone.AllInSpace.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == SmallDroneTypeToLookFor)))
            {
                _m3PerSmallestDrone = 5;
                return SmallDroneTypeToLookFor;
            }

            SmallDroneTypeToLookFor = "Medium Scout Drones";
            if (Drone.AllInBay.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == SmallDroneTypeToLookFor)) || Drone.AllInSpace.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == SmallDroneTypeToLookFor)))
            {
                _m3PerSmallestDrone = 10;
                return SmallDroneTypeToLookFor;
            }

            SmallDroneTypeToLookFor = "Heavy Attack Drones";
            if (Drone.AllInBay.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == SmallDroneTypeToLookFor)) || Drone.AllInSpace.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == SmallDroneTypeToLookFor)))
            {
                _m3PerSmallestDrone = 25;
                return SmallDroneTypeToLookFor;
            }

            SmallDroneTypeToLookFor = "Sentry Drones";
            if (Drone.AllInBay.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == SmallDroneTypeToLookFor)) || Drone.AllInSpace.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == SmallDroneTypeToLookFor)))
            {
                _m3PerSmallestDrone = 25;
                return SmallDroneTypeToLookFor;
            }

            SmallDroneTypeToLookFor = "Fighters";
            if (Drone.AllInBay.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == SmallDroneTypeToLookFor)) || Drone.AllInSpace.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == SmallDroneTypeToLookFor)))
            {
                return SmallDroneTypeToLookFor;
            }

            return "n/a";
        }

        private bool IsThisDroneTypeAvailable(string DroneTypeToLookFor)
        {
            if (Drone.AllInBay.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == DroneTypeToLookFor)))
            {
                return true;
            }

            if (Drone.AllInSpace.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == DroneTypeToLookFor)))
            {
                return true;
            }

            return false;
        }

        private bool DoesThisEntityRequireSmallDronesToKill(Entity targetToEvaluate)
        {
            //
            // Is this a small fast NPC?
            //
            if (Data.NPCClasses.All.Any(a => a.Key == targetToEvaluate.GroupID && (a.Value == "Destroyer" || a.Value == "Frigate" )))
            {
                return true;
            }

            //
            //
            // Is this a small fast player?
            if (targetToEvaluate.IsHostile)
            {
                if (targetToEvaluate.GroupID == Group.Interceptor ||
                    targetToEvaluate.GroupID == Group.ElectronicAttackShip ||
                    targetToEvaluate.GroupID == Group.Frigate ||
                    targetToEvaluate.GroupID == Group.Destroyer ||
                    targetToEvaluate.GroupID == Group.Interdictor
                    //TargetToEvaluate.GroupID == Group.Drones)
                    )
                {
                    return true;
                }

                return false;
            }

            return false;
        }

        private bool AppropriateSizedDronesAvailable()
        {
            string DroneTypeToLookFor = string.Empty;
            switch (Config.Mode)
            {
                case Mode.AFKHeavy:
                case Mode.AgressiveHeavy:
                    DroneTypeToLookFor = "Heavy Attack Drones";
                    if (!IsThisDroneTypeAvailable(DroneTypeToLookFor))
                    {
                        Console.Log("|o SimpleDrone: [" + Config.Mode + "] Mode Configured but no [" + DroneTypeToLookFor + "] found");
                        break;
                    }

                    break;

                case Mode.AgressiveMedium:
                    DroneTypeToLookFor = "Medium Scout Drones";
                    if (!IsThisDroneTypeAvailable(DroneTypeToLookFor))
                    {
                        Console.Log("|o SimpleDrone: [" + Config.Mode + "] Mode Configured but no [" + DroneTypeToLookFor + "] found");
                        break;
                    }

                    break;

                case Mode.AgressiveScout:
                    DroneTypeToLookFor = "Light Scout Drones";
                    if (!IsThisDroneTypeAvailable(DroneTypeToLookFor))
                    {
                        Console.Log("|o SimpleDrone: [" + Config.Mode + "] Mode Configured but no [" + DroneTypeToLookFor + "] found");
                        break;
                    }

                    break;

                case Mode.AgressiveSentry:
                    DroneTypeToLookFor = "Sentry Drones";
                    if (!IsThisDroneTypeAvailable(DroneTypeToLookFor))
                    {
                        Console.Log("|o SimpleDrone: [" + Config.Mode + "] Mode Configured but no [" + DroneTypeToLookFor + "] found");
                        break;
                    }

                    break;

                case Mode.Fighter:
                case Mode.FighterPointDefense:
                case Mode.FighterSupport:
                    DroneTypeToLookFor = "Fighters";
                    if (!IsThisDroneTypeAvailable(DroneTypeToLookFor))
                    {
                        Console.Log("|o SimpleDrone: [" + Config.Mode + "] Mode Configured but no [" + DroneTypeToLookFor + "] found");
                        break;
                    }

                    break;
            }

            return false;
        }

        private int AvailableSlotsForThisShipTypeAndDroneSize(int m3PerDrone = 5)
        {
            int nonStandardShipsDroneBandwidth = 0;
            int availableSlots = Me.MaxActiveDrones - Drone.AllInSpace.Count();
            //Rattlesnake
            if (MyShip.ToEntity.TypeID == 17918) nonStandardShipsDroneBandwidth = 50;
            //Gila
            if (MyShip.ToEntity.TypeID == 17715) nonStandardShipsDroneBandwidth = 20;
            if (nonStandardShipsDroneBandwidth != 0) availableSlots = Math.Max((nonStandardShipsDroneBandwidth / m3PerDrone), Me.MaxActiveDrones) - Drone.AllInSpace.Count();
            return availableSlots;
        }

        bool Control(object[] Params)
        {
            if (!Session.InSpace || Config.Mode == Mode.None)
            {
                return false;
            }

            // If we're warping and drones are in space, recall them and stop the module
            if (MyShip.ToEntity.Mode == EntityMode.Warping && Drone.AllInSpace.Any() && Config.Mode != Mode.FighterSupport)
            {
                Drone.AllInSpace.ReturnToDroneBay();
                return true;
            }

            if (MyShip.DronesToReconnect)
            {
                MyShip.ReconnectToDrones();
                DislodgeWaitFor(2);
                return false;
            }

            if (!Rats.TargetList.Any() && !Entity.All.Any(a => PriorityTargets.Contains(a.Name)) && Config.Mode != Mode.FighterSupport)
            {
                List<Drone> Recall = Drone.AllInSpace.Where(a => DroneReady(a) && a.State != EntityState.Departing).ToList();
                // Recall drones
                if (Recall.Any())
                {
                    Console.Log("|oRecalling drones");
                    Console.Log(" |-gNo rats available");
                    Recall.ReturnToDroneBay();
                    Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    return false;
                }
            }
            if (Config.Mode == Mode.AFKHeavy && (Rats.TargetList.Any() || Entity.All.Any(a => PriorityTargets.Contains(a.Name))))
            {
                _m3PerDrone = 25;
                int availableSlots = AvailableSlotsForThisShipTypeAndDroneSize(_m3PerDrone);
                List<Drone> Deploy = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones")).Take(availableSlots).ToList();
                // Launch drones
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                }
                return false;
            }

            foreach (Drone d in Drone.AllInBay)
            {
                if (DroneHealthCache.ContainsKey(d)) DroneHealthCache.Remove(d);
            }

            foreach (Drone d in Drone.AllInSpace)
            {
                double health = d.ToEntity.ShieldPct + d.ToEntity.ArmorPct + d.ToEntity.HullPct;
                if (!DroneHealthCache.ContainsKey(d)) DroneHealthCache.Add(d, health);
                if (health < DroneHealthCache[d])
                {
                    DroneCooldown.Add(d);
                }
            }

            List<Drone> RecallDamaged = Drone.AllInSpace.Where(a => DroneCooldown.Contains(a) && DroneReady(a) && a.State != EntityState.Departing).ToList();
            if (RecallDamaged.Any() && Config.Mode != Mode.FighterSupport)
            {
                Console.Log("|oRecalling damaged drones");
                RecallDamaged.ReturnToDroneBay();
                RecallDamaged.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                return false;
            }

            Entity WarpScrambling = SecurityCore.ValidScramble;
            Entity Neuting = SecurityCore.ValidNeuter;

            #region ActiveTarget selection

			Double MaxRange = (Config.Mode == Mode.FighterSupport) ? 150000 : ((Config.Mode == Mode.PointDefense) ? 20000 : Me.DroneControlDistance);

            if (WarpScrambling != null)
            {
                if (ActiveTarget != WarpScrambling && WarpScrambling.Distance < MaxRange)
                {
                    Console.Log("|rEntity on grid is/was warp scrambling!");
                    Console.Log("|oOveriding current drone target");
                    Console.Log(" |-g{0}", WarpScrambling.Name);
                    ActiveTarget = WarpScrambling;
                    return false;
                }
            }
            else if (Neuting != null)
            {
                if (ActiveTarget != Neuting && Neuting.Distance < MaxRange)
                {
                    Console.Log("|rEntity on grid is/was neuting!");
                    Console.Log("|oOveriding current drone target");
                    Console.Log(" |-g{0}", Neuting.Name);
                    ActiveTarget = Neuting;
                    return false;
                }
            }

            if (ActiveTarget == null || !ActiveTarget.Exists || ActiveTarget.Exploded || ActiveTarget.Released)
            {
                ActiveTarget = null;
                ActiveTarget = Entity.All.FirstOrDefault(a => PriorityTargets.Contains(a.Name) && !a.Exploded && !a.Released && (a.LockedTarget || a.LockingTarget) && !Triggers.Contains(a.Name) && a.Distance < MaxRange);
                if (Rats.LockedAndLockingTargetList.Any() && ActiveTarget == null)
                {
                    if (Config.PrivateTargets)
                    {
                        if (Config.SharedTargets)
                        {
                            ActiveTarget = Rats.LockedAndLockingTargetList.FirstOrDefault(a => IPC.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                        }
                        else
                        {
                            ActiveTarget = Rats.LockedAndLockingTargetList.FirstOrDefault(a => !IPC.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                        }
                    }
                    if (ActiveTarget == null && OutOfTargets)
                    {
                        ActiveTarget = Rats.LockedAndLockingTargetList.FirstOrDefault(a =>  a.Distance < MaxRange);
                    }
                    if (ActiveTarget != null)
                    {
                        IPC.Relay(Me.CharID, ActiveTarget.ID);
                    }
                }
            }

            #endregion

            #region LockManagement

            TargetCooldown = TargetCooldown.Where(a => a.Value >= DateTime.Now).ToDictionary(a => a.Key, a => a.Value);
            Rats.LockedAndLockingTargetList.ForEach(a => { TargetCooldown.AddOrUpdate(a, DateTime.Now.AddSeconds(2)); });
            if (WarpScrambling != null)
            {
                if (!WarpScrambling.LockedTarget && !WarpScrambling.LockingTarget)
                {
                    if (Rats.LockedAndLockingTargetList.Count >= Me.TrueMaxTargetLocks)
                    {
                        if (Rats.LockedTargetList.Any())
                        {
                            Rats.LockedTargetList.FirstOrDefault().UnlockTarget();
                        }
                        return false;
                    }
                    WarpScrambling.LockTarget();
                    return false;
                }
            }
            else if (Neuting != null)
            {
                if (!Neuting.LockedTarget && !Neuting.LockingTarget)
                {
                    if (Rats.LockedAndLockingTargetList.Count >= Me.TrueMaxTargetLocks)
                    {
                        if (Rats.LockedTargetList.Any())
                        {
                            Rats.LockedTargetList.FirstOrDefault().UnlockTarget();
                        }
                        return false;
                    }
                    Neuting.LockTarget();
                    return false;
                }
            }
            else
            {
                Entity NewTarget = Entity.All.FirstOrDefault(a => !a.LockedTarget && !a.LockingTarget && PriorityTargets.Contains(a.Name) && a.Distance < MyShip.MaxTargetRange && !TargetCooldown.ContainsKey(a) && !Triggers.Contains(a.Name));
                if (NewTarget == null) NewTarget = Rats.UnlockedTargetList.FirstOrDefault(a => !TargetCooldown.ContainsKey(a) && a.Distance < MyShip.MaxTargetRange);
                if (Rats.LockedAndLockingTargetList.Count < Config.TargetSlots &&
                    NewTarget != null &&
                    Entity.All.FirstOrDefault(a => a.IsJamming && a.IsTargetingMe) == null)
                {
                    Console.Log("|oLocking");
                    Console.Log(" |-g{0}", NewTarget.Name);
                    TargetCooldown.AddOrUpdate(NewTarget, DateTime.Now.AddSeconds(2));
                    NewTarget.LockTarget();
                    OutOfTargets = false;
                    return false;
                }
            }
            OutOfTargets = true;

            #endregion

            // Make sure ActiveTarget is locked.  If so, make sure it's the active target, if not, return.
            if (ActiveTarget != null && ActiveTarget.Exists && ActiveTarget.LockedTarget)
            {
                if (!ActiveTarget.IsActiveTarget)
                {
                    ActiveTarget.MakeActive();
                    return false;
                }
            }
            else
            {
                if (ActiveTarget == null && Config.Mode != Mode.FighterSupport)
                {
                    List<Drone> Recall = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && a.State != EntityState.Departing).ToList();
                    // Recall drones if in point defense and no frig/destroyers in range
                    if (Recall.Any())
                    {
                        Console.Log("|oRecalling drones");
                        Recall.ReturnToDroneBay();
                        Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    }
                }
                return false;
            }

            string smallestDronesAvailable = FindSmallestDroneGroupAvailable();
            // Handle Attacking frigates - this should work for PointDefense AND Sentry AND FighterPointDefense modes
            if (ActiveTarget.Distance < 20000)
            {
                // Is the target a small fast ship that needs small drones to kill?
                if (DoesThisEntityRequireSmallDronesToKill(ActiveTarget))
                {
                    // Recall fighters and sentries
                    List<Drone> Recall = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group != smallestDronesAvailable) && a.State != EntityState.Departing).ToList();
                    if (Recall.Any())
                    {
                        Console.Log("|oRecalling larger drones, so that we can deploy smaller drones to kill [" + ActiveTarget.Name + "] GroupID [" + ActiveTarget.GroupID + "]");
                        Recall.ReturnToDroneBay();
                        Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                        return false;
                    }
                    // Send drones to attack
                    List<Drone> Attack = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == smallestDronesAvailable) && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                    if (Attack.Any())
                    {
                        Console.Log("|oSending [" + smallestDronesAvailable + "] drones to attack");
                        Attack.Attack();
                        Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                        return false;
                    }

                    int availableSlots = AvailableSlotsForThisShipTypeAndDroneSize(_m3PerSmallestDrone);
                    List<Drone> Deploy = Drone.AllInBay.Where(a => !DroneCooldown.Contains(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == smallestDronesAvailable)).Take(availableSlots).ToList();
                    List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == smallestDronesAvailable)).Take(availableSlots).ToList();
                    
                    // Launch drones
                    if (Deploy.Any())
                    {
                        Console.Log("|oLaunching [" + smallestDronesAvailable + "] drones");
                        Deploy.Launch();
                        Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                        return false;
                    }
                    
                    if (availableSlots > 0 && DeployIgnoreCooldown.Any())
                    {
                        DroneCooldown.Clear();
                    }
                }
                else if (Config.Mode != Mode.AgressiveScout && Drone.AllInSpace.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")))
                {
                    List<Drone> Recall = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && a.State != EntityState.Departing).ToList();
                    // Recall drones if in point defense or sentry and no frig/destroyers in range
                    if (Recall.Any())
                    {
                        Console.Log("|oRecalling [" + smallestDronesAvailable + "], no small targets within 20k");
                        Recall.ReturnToDroneBay();
                        Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                        return false;
                    }
                }
                else if (Config.Mode != Mode.AgressiveMedium && Drone.AllInSpace.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Medium Scout Drones")))
                {
                    List<Drone> Recall = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && a.State != EntityState.Departing).ToList();
                    // Recall drones if in point defense or sentry and no frig/destroyers in range
                    if (Recall.Any())
                    {
                        Console.Log("|oRecalling [" + smallestDronesAvailable + "], no small targets within 20k");
                        Recall.ReturnToDroneBay();
                        Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                        return false;
                    }
                }
            }

            // Handle Attacking anything if in AgressiveScout mode
            if (Config.Mode == Mode.AgressiveScout)
            {
                // Recall fighters and sentries
                List<Drone> Recall = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group != "Light Scout Drones") && a.State != EntityState.Departing).ToList();
                if (Recall.Any())
                {
                    Console.Log("|oRecalling non scout drones");
                    Recall.ReturnToDroneBay();
                    Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    return false;
                }
                // Send drones to attack
                List<Drone> Attack = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                if (Attack.Any())
                {
                    Console.Log("|oSending scout drones to attack");
                    Attack.Attack();
                    Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                    return false;
                }

                _m3PerDrone = 5;
                int availableSlots = AvailableSlotsForThisShipTypeAndDroneSize(_m3PerDrone);
                List<Drone> Deploy = Drone.AllInBay.Where(a => !DroneCooldown.Contains(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")).Take(availableSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")).Take(availableSlots).ToList();
                // Launch drones
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching scout drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (availableSlots > 0 && DeployIgnoreCooldown.Any())
                {
                    DroneCooldown.Clear();
                }

                //
                // If we have no drones of the appropriate size left (in dronebay or in space then ffs Change Modes based on the size of drones available
                // so that we will potentially have drones shooting things.
                //
                if (!AppropriateSizedDronesAvailable())
                {
                    SetSimpleDroneModeBasedOnLagestAvailableDrones();
                    return false;
                }
            }

            // Handle Attacking anything if in AgressiveMedium mode
            if (Config.Mode == Mode.AgressiveMedium)
            {
                // Recall fighters and sentries
                List<Drone> Recall = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group != "Medium Scout Drones") && a.State != EntityState.Departing).ToList();
                if (Recall.Any())
                {
                    Console.Log("|oRecalling non medium drones");
                    Recall.ReturnToDroneBay();
                    Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    return false;
                }
                // Send drones to attack
                List<Drone> Attack = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Medium Scout Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                if (Attack.Any())
                {
                    Console.Log("|oSending medium drones to attack");
                    Attack.Attack();
                    Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                    return false;
                }

                _m3PerDrone = 10;
                int availableSlots = AvailableSlotsForThisShipTypeAndDroneSize(_m3PerDrone);
                List<Drone> Deploy = Drone.AllInBay.Where(a => !DroneCooldown.Contains(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Medium Scout Drones")).Take(availableSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Medium Scout Drones")).Take(availableSlots).ToList();
                // Launch drones
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching medium drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (availableSlots > 0 && DeployIgnoreCooldown.Any())
                {
                    DroneCooldown.Clear();
                }

                //
                // If we have no drones of the appropriate size left (in dronebay or in space then ffs Change Modes based on the size of drones available
                // so that we will potentially have drones shooting things.
                //
                if (!AppropriateSizedDronesAvailable())
                {
                    SetSimpleDroneModeBasedOnLagestAvailableDrones();
                    return false;
                }
            }

            // Handle Attacking anything if in AgressiveHeavy mode
            if (Config.Mode == Mode.AgressiveHeavy)
            {
                // Recall non heavy
                List<Drone> Recall = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group != "Heavy Attack Drones") && a.State != EntityState.Departing).ToList();
                if (Recall.Any())
                {
                    Console.Log("|oRecalling non heavy drones");
                    Recall.ReturnToDroneBay();
                    Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    return false;
                }
                // Send drones to attack
                List<Drone> Attack = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                if (Attack.Any())
                {
                    Console.Log("|oSending heavy drones to attack");
                    Attack.Attack();
                    Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                    return false;
                }

                _m3PerDrone = 25;
                int availableSlots = AvailableSlotsForThisShipTypeAndDroneSize(_m3PerDrone);
                List<Drone> Deploy = Drone.AllInBay.Where(a => !DroneCooldown.Contains(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones")).Take(availableSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones")).Take(availableSlots).ToList();
                // Launch drones
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching heavy drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (availableSlots > 0 && DeployIgnoreCooldown.Any())
                {
                    DroneCooldown.Clear();
                }

                //
                // If we have no drones of the appropriate size left (in dronebay or in space then ffs Change Modes based on the size of drones available
                // so that we will potentially have drones shooting things.
                //
                if (!AppropriateSizedDronesAvailable())
                {
                    SetSimpleDroneModeBasedOnLagestAvailableDrones();
                    return false;
                }
            }

            // Handle Attacking anything if in AgressiveSentry mode
            if (Config.Mode == Mode.AgressiveSentry)
            {
                // Recall non heavy
                List<Drone> Recall = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group != "Sentry Drones") && a.State != EntityState.Departing).ToList();
                if (Recall.Any())
                {
                    Console.Log("|oRecalling non sentry drones");
                    Recall.ReturnToDroneBay();
                    Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    return false;
                }
                // Send drones to attack
                List<Drone> Attack = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                if (Attack.Any())
                {
                    Console.Log("|oSending sentry drones to attack");
                    Attack.Attack();
                    Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                    return false;
                }

                _m3PerDrone = 25;
                int availableSlots = AvailableSlotsForThisShipTypeAndDroneSize(_m3PerDrone);
                List<Drone> Deploy = Drone.AllInBay.Where(a => !DroneCooldown.Contains(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(availableSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(availableSlots).ToList();
                // Launch drones
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching sentry drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (availableSlots > 0 && DeployIgnoreCooldown.Any())
                {
                    DroneCooldown.Clear();
                }

                //
                // If we have no drones of the appropriate size left (in dronebay or in space then ffs Change Modes based on the size of drones available
                // so that we will potentially have drones shooting things.
                //
                if (!AppropriateSizedDronesAvailable())
                {
                    SetSimpleDroneModeBasedOnLagestAvailableDrones();
                    return false;
                }
            }

            // Handle managing sentries
            if (ActiveTarget.Distance < MaxRange && Config.Mode == Mode.Sentry)
            {
                // Is the target a frigate?
                if (!Data.NPCClasses.All.Any(a => a.Key == ActiveTarget.GroupID && (a.Value == "Destroyer" || a.Value == "Frigate")) || ActiveTarget.Distance > 20000)
                {
                    List<Drone> Recall = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group != "Sentry Drones") && a.State != EntityState.Departing).ToList();
                    // Recall non sentries
                    if (Recall.Any())
                    {
                        Console.Log("|oRecalling drones");
                        Recall.ReturnToDroneBay();
                        Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                        return false;
                    }
                    List<Drone> Attack = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                    // Send drones to attack
                    if (Attack.Any())
                    {
                        Console.Log("|oOrdering sentry drones to attack");
                        Attack.Attack();
                        Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                        return false;
                    }

                    _m3PerDrone = 25;
                    int availableSlots = AvailableSlotsForThisShipTypeAndDroneSize(_m3PerDrone);
                    List<Drone> Deploy = Drone.AllInBay.Where(a => !DroneCooldown.Contains(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(availableSlots).ToList();
                    List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(availableSlots).ToList();
                    // Launch drones
                    if (Deploy.Any())
                    {
                        Console.Log("|oLaunching sentry drones");
                        Deploy.Launch();
                        Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                        return false;
                    }
                    else if (availableSlots > 0 && DeployIgnoreCooldown.Any())
                    {
                        DroneCooldown.Clear();
                    }

                    //
                    // If we have no drones of the appropriate size left (in dronebay or in space then ffs Change Modes based on the size of drones available
                    // so that we will potentially have drones shooting things.
                    //
                    if (!AppropriateSizedDronesAvailable())
                    {
                        SetSimpleDroneModeBasedOnLagestAvailableDrones();
                        return false;
                    }
                }
            }

            // Handle managing fighters
            if (Config.Mode == Mode.Fighter || Config.Mode == Mode.FighterSupport)
            {
                List<Drone> Recall = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group != "Fighters") && a.State != EntityState.Departing).ToList();
                // Recall non fighters
                if (Recall.Any())
                {
                    Console.Log("|oRecalling non fighters");
                    Recall.ReturnToDroneBay();
                    Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    return false;
                }
                List<Drone> Attack = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Fighters") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                // Send fighters to attack
                if (Attack.Any())
                {
                    Console.Log("|oOrdering fighters to attack");
                    Attack.Attack();
                    Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                    return false;
                }

                int availableSlots = AvailableSlotsForThisShipTypeAndDroneSize();
                List<Drone> Deploy = Drone.AllInBay.Where(a => !DroneCooldown.Contains(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Fighters")).Take(availableSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Fighters")).Take(availableSlots).ToList();
                // Launch fighters
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching fighters");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (availableSlots > 0 && DeployIgnoreCooldown.Any())
                {
                    DroneCooldown.Clear();
                }

                //
                // If we have no drones of the appropriate size left (in dronebay or in space then ffs Change Modes based on the size of drones available
                // so that we will potentially have drones shooting things.
                //
                if (!AppropriateSizedDronesAvailable())
                {
                    SetSimpleDroneModeBasedOnLagestAvailableDrones();
                    return false;
                }
            }

            // Handle managing fighters
            if (Config.Mode == Mode.FighterPointDefense)
            {
                // Is the target a frigate?
                if (!Data.NPCClasses.All.Any(a => a.Key == ActiveTarget.GroupID && (a.Value == "Destroyer" || a.Value == "Frigate")) || ActiveTarget.Distance > 20000)
                {
                    List<Drone> Recall = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group != "Fighters") && a.State != EntityState.Departing).ToList();
                    // Recall non fighters
                    if (Recall.Any())
                    {
                        Console.Log("|oRecalling non fighters");
                        Recall.ReturnToDroneBay();
                        Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                        return false;
                    }
                    List<Drone> Attack = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Fighters") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                    // Send fighters to attack
                    if (Attack.Any())
                    {
                        Console.Log("|oOrdering fighters to attack");
                        Attack.Attack();
                        Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                        return false;
                    }
                    int availableSlots = Me.MaxActiveDrones - Drone.AllInSpace.Count();
                    List<Drone> Deploy = Drone.AllInBay.Where(a => !DroneCooldown.Contains(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Fighters")).Take(availableSlots).ToList();
                    List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Fighters")).Take(availableSlots).ToList();
                    // Launch fighters
                    if (Deploy.Any())
                    {
                        Console.Log("|oLaunching fighters");
                        Deploy.Launch();
                        Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                        return false;
                    }
                    else if (availableSlots > 0 && DeployIgnoreCooldown.Any())
                    {
                        DroneCooldown.Clear();
                    }

                    //
                    // If we have no drones of the appropriate size left (in dronebay or in space then ffs Change Modes based on the size of drones available
                    // so that we will potentially have drones shooting things.
                    //
                    if (!AppropriateSizedDronesAvailable())
                    {
                        SetSimpleDroneModeBasedOnLagestAvailableDrones();
                        return false;
                    }
                }
            }

            return false;
        }

        #endregion
    }
}

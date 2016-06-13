#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Linq;
using EveCom;
using EveComFramework.Core;
using EveComFramework.Targets;
using EveComFramework.KanedaToolkit;

namespace EveComFramework.SimpleDrone
{
    #region Enums

    public enum Mode
    {
        None,
        Sentry,
        PointDefense,
        AgressiveScout,
        AgressiveMedium,
        AFKHeavy,
        AgressiveHeavy,
        AgressiveSentry
    }

    #endregion

    #region Settings

    public class SimpleDroneSettings : Settings
    {
        public Mode Mode = Mode.None;
        public bool PrivateTargets = true;
        public bool SharedTargets = false;
        public bool StayDeployedWithNoTargets = false;
        public int TargetSlots = 2;

        public double FighterCriticalHealth = 0.2;
        public double FighterMaxRange = 800000;

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

        private SimpleDrone()
        {
            Rats.AddPriorityTargets();
            Rats.AddNPCs();
            Rats.AddTargetingMe();

            Rats.Ordering = new RatComparer();
        }

        #endregion

        #region Variables

        public Logger Console = new Logger("SimpleDrone");
        public SimpleDroneSettings Config = new SimpleDroneSettings();
        Targets.Targets Rats = new Targets.Targets();
        Security.Security SecurityCore = Security.Security.Instance;
        HashSet<Drone> DroneCooldown = new HashSet<Drone>();
        HashSet<Fighters.Fighter> FighterCooldown = new HashSet<Fighters.Fighter>();
        Dictionary<Drone, double> DroneHealthCache = new Dictionary<Drone, double>();
        IPC IPC = IPC.Instance;

        public List<string> PriorityTargets = new List<string>();
        public List<string> Triggers = new List<string>();


        #endregion

        #region Actions

        public void Enabled(bool var)
        {
            if (var)
            {
                if (Idle)
                {
                    TryReconnect = true;
                    QueueState(Control);
                }
            }
            else
            {
                Clear();
                QueueState(Recall);
            }
        }



        #endregion

        #region States

        bool TryReconnect = true;
        Entity ActiveTarget;
        Dictionary<Entity, DateTime> TargetCooldown = new Dictionary<Entity, DateTime>();
        bool OutOfTargets = false;
        Dictionary<Drone, DateTime> NextDroneCommand = new Dictionary<Drone, DateTime>();
        Dictionary<Fighters.Fighter, DateTime> NextFighterCommand = new Dictionary<Fighters.Fighter, DateTime>();
        bool DroneReady(Drone drone)
        {
            if (!NextDroneCommand.ContainsKey(drone)) return true;
            if (NextDroneCommand[drone] < DateTime.Now) return true;
            return false;
        }
        bool FighterReady(Fighters.Fighter fighter)
        {
            if (!NextFighterCommand.ContainsKey(fighter)) return true;
            if (NextFighterCommand[fighter] < DateTime.Now) return true;
            return false;
        }

        bool SmallTarget(Entity target)
        {
            if (Data.NPCClasses.All.Any(a => a.Key == target.GroupID && (a.Value == "Frigate" || a.Value == "Destroyer")))
            {
                return true;
            }

            if (target.IsHostile)
            {
                if (target.GroupID == Group.Shuttle || 
                    target.GroupID == Group.Frigate ||
                    target.GroupID == Group.AssaultFrigate ||
                    target.GroupID == Group.CovertOps ||
                    target.GroupID == Group.ElectronicAttackShip ||
                    target.GroupID == Group.Interceptor ||
                    target.GroupID == Group.ExpeditionFrigate ||
                    target.GroupID == Group.Destroyer ||
                    target.GroupID == Group.Interdictor ||
                    target.GroupID == Group.TacticalDestroyer ||
                    target.GroupID == Group.Capsule ||
                    target.GroupID == Group.LogisticsFrigate ||
                    target.GroupID == Group.CommandDestroyers
                    )
                {
                    return true;
                }
            }

            return false;
        }

        bool Recall(object[] Params)
        {
            if (Session.InStation) return true;

            // Recall drones
            List<Drone> Recall = Drone.AllInSpace.Where(a => DroneReady(a) && a.State != EntityState.Departing).ToList();
            if (Recall.Any())
            {
                Console.Log("|oRecalling drones");
                Recall.ReturnToDroneBay();
                Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                return false;
            }
            if (Drone.AllInSpace.Any()) return false;

            // Recall fighters
            List<Fighters.Fighter> RecallFighters = Fighters.Active.Where(a => FighterReady(a)).ToList();
            if(RecallFighters.Any())
            {
                Console.Log("|oRecalling fighters");
                RecallFighters.RecallToTube();
                RecallFighters.ForEach(a => NextFighterCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                RecallFighters.Where(a => !FighterCooldown.Contains(a)).ForEach(a => FighterCooldown.Add(a));
            }
            if (Fighters.Active.Any()) return false;

            return true;
        }

        bool Control(object[] Params)
        {
            if (!Session.InSpace)
            {
                return false;
            }

            if(Config.Mode == Mode.None && !Fighters.Tubes.Any())
            {
                return false;
            }

            // If we're warping and drones are in space, recall them and stop the module
            if (MyShip.ToEntity.Mode == EntityMode.Warping && Drone.AllInSpace.Any())
            {
                Drone.AllInSpace.ReturnToDroneBay();
                return true;
            }

            // If we're warping and fighters are in space, queue delayed stop of the module
            if (MyShip.ToEntity.Mode == EntityMode.Warping && Fighters.Active.Any())
            {
                QueueState(FighterShutdown);
                return true;
            }

            if (MyShip.DronesToReconnect && MyShip.DroneBay.UsedCapacity < MyShip.DroneBay.MaxCapacity && MyShip.ToEntity.GroupID != Group.Capsule && TryReconnect)
            {
                MyShip.ReconnectToDrones();
                DislodgeWaitFor(2);
                TryReconnect = false;
                return false;
            }

            if (!Rats.TargetList.Any() && !Entity.All.Any(a => PriorityTargets.Contains(a.Name)) && !Config.StayDeployedWithNoTargets)
            {
                // Recall drones
                List<Drone> Recall = Drone.AllInSpace.Where(a => DroneReady(a) && a.State != EntityState.Departing).ToList();
                if (Recall.Any())
                {
                    Console.Log("|oRecalling drones");
                    Console.Log(" |-gNo rats available");
                    Recall.ReturnToDroneBay();
                    Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    return false;
                }
                // Recall fighters
                List<Fighters.Fighter> RecallFighters = Fighters.Active.Where(a => FighterReady(a)).ToList();
                if(RecallFighters.Any())
                {
                    Console.Log("|oRecalling fighters");
                    Console.Log(" |-gNo rats available");
                    RecallFighters.RecallToTube();
                    RecallFighters.ForEach(a => NextFighterCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    RecallFighters.Where(a => !FighterCooldown.Contains(a)).ForEach(a => FighterCooldown.Add(a));
                    return false;
                }
            }
            if (Config.Mode == Mode.AFKHeavy && (Rats.TargetList.Any() || Entity.All.Any(a => PriorityTargets.Contains(a.Name))))
            {
                int AvailableSlots = ((MyShip.ToEntity.TypeID == 17918 /* Rattlesnake */) ? 2 : Me.MaxActiveDrones) - Drone.AllInSpace.Count();
                List<Drone> Deploy = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones")).Take(AvailableSlots).ToList();
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
            if (RecallDamaged.Any())
            {
                Console.Log("|oRecalling damaged drones");
                RecallDamaged.ReturnToDroneBay();
                RecallDamaged.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                return false;
            }

            List<Fighters.Fighter> recallFighters = Fighters.Active.Where(a => a.Health < Config.FighterCriticalHealth).ToList();
            if (recallFighters.Any())
            {
                recallFighters.RecallToTube();
                recallFighters.ForEach(f => {
                    NextFighterCommand.AddOrUpdate(f, DateTime.Now.AddSeconds(5));
                    if (!FighterCooldown.Contains(f)) FighterCooldown.Add(f);
                });
            }

            Entity WarpScrambling = SecurityCore.ValidScramble;
            Entity Neuting = SecurityCore.ValidNeuter;

            #region ActiveTarget selection

            Double MaxRange = Fighters.Tubes.Any() ? Math.Min(Config.FighterMaxRange, MyShip.MaxTargetRange) : ((Config.Mode == Mode.PointDefense) ? 20000 : Me.DroneControlDistance);

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
                        ActiveTarget = Rats.LockedAndLockingTargetList.FirstOrDefault(a => a.Distance < MaxRange);
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
                            Rats.LockedTargetList.First().UnlockTarget();
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
                            Rats.LockedTargetList.First().UnlockTarget();
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
                if (!Fighters.Tubes.Any() && !ActiveTarget.IsActiveTarget)
                {
                    ActiveTarget.MakeActive();
                    return false;
                }
            }
            else
            {
                if (ActiveTarget == null)
                {
                    // Recall drones if in point defense and no frig/destroyers in range
                    List<Drone> Recall = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && a.State != EntityState.Departing).ToList();
                    if (Recall.Any() && !Config.StayDeployedWithNoTargets)
                    {
                        Console.Log("|oRecalling drones");
                        Recall.ReturnToDroneBay();
                        Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    }

                    List<Fighters.Fighter> RecallFighters = Fighters.Active.Where(a => FighterReady(a)).ToList();
                    if(RecallFighters.Any() && !Config.StayDeployedWithNoTargets)
                    {
                        Console.Log("|oRecalling fighters");
                        RecallFighters.RecallToTube();
                        RecallFighters.ForEach(a => NextFighterCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                        RecallFighters.Where(a => !FighterCooldown.Contains(a)).ForEach(a => FighterCooldown.Add(a));
                    }
                }
                return false;
            }

            // Handle Attacking small targets - this should work for PointDefense AND Sentry
            if (ActiveTarget.Distance < 20000 && (Config.Mode == Mode.PointDefense || Config.Mode == Mode.Sentry))
            {
                // Is the target a small target?
                if (SmallTarget(ActiveTarget))
                {
                    // Recall sentries
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
                    int AvailableSlots = ((MyShip.ToEntity.TypeID == 17918 /* Rattlesnake */) ? 2 : Me.MaxActiveDrones) - Drone.AllInSpace.Count();
                    List<Drone> Deploy = Drone.AllInBay.Where(a => !DroneCooldown.Contains(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")).Take(AvailableSlots).ToList();
                    List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")).Take(AvailableSlots).ToList();
                    // Launch drones
                    if (Deploy.Any())
                    {
                        Console.Log("|oLaunching scout drones");
                        Deploy.Launch();
                        Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                        return false;
                    }
                    else if (AvailableSlots > 0 && DeployIgnoreCooldown.Any())
                    {
                        DroneCooldown.Clear();
                    }
                }
                else if (Config.Mode == Mode.PointDefense)
                {
                    List<Drone> Recall = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && a.State != EntityState.Departing).ToList();
                    // Recall drones if in point defense and no frig/destroyers in range
                    if (Recall.Any())
                    {
                        Console.Log("|oRecalling drones");
                        Recall.ReturnToDroneBay();
                        Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                        return false;
                    }
                }
            }

            // Handle Attacking anything if in AgressiveScout mode
            if (Config.Mode == Mode.AgressiveScout)
            {
                // Recall sentries
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
                int AvailableSlots = Me.MaxActiveDrones - Drone.AllInSpace.Count();
                List<Drone> Deploy = Drone.AllInBay.Where(a => !DroneCooldown.Contains(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")).Take(AvailableSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")).Take(AvailableSlots).ToList();
                // Launch drones
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching scout drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (AvailableSlots > 0 && DeployIgnoreCooldown.Any())
                {
                    DroneCooldown.Clear();
                }
            }

            // Handle Attacking anything if in AgressiveMedium mode
            if (Config.Mode == Mode.AgressiveMedium)
            {
                // Recall sentries
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
                int AvailableSlots = ((MyShip.ToEntity.TypeID == 17715 /* Gila */) ? 2 : Me.MaxActiveDrones) - Drone.AllInSpace.Count();
                List<Drone> Deploy = Drone.AllInBay.Where(a => !DroneCooldown.Contains(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Medium Scout Drones")).Take(AvailableSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Medium Scout Drones")).Take(AvailableSlots).ToList();
                // Launch drones
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching medium drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (AvailableSlots > 0 && DeployIgnoreCooldown.Any())
                {
                    DroneCooldown.Clear();
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
                int AvailableSlots = ((MyShip.ToEntity.TypeID == 17918 /* Rattlesnake */) ? 2 : Me.MaxActiveDrones) - Drone.AllInSpace.Count();
                List<Drone> Deploy = Drone.AllInBay.Where(a => !DroneCooldown.Contains(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones")).Take(AvailableSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones")).Take(AvailableSlots).ToList();
                // Launch drones
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching heavy drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (AvailableSlots > 0 && DeployIgnoreCooldown.Any())
                {
                    DroneCooldown.Clear();
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
                int AvailableSlots = ((MyShip.ToEntity.TypeID == 17918 /* Rattlesnake */) ? 2 : Me.MaxActiveDrones) - Drone.AllInSpace.Count();
                List<Drone> Deploy = Drone.AllInBay.Where(a => !DroneCooldown.Contains(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(AvailableSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(AvailableSlots).ToList();
                // Launch drones
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching sentry drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (AvailableSlots > 0 && DeployIgnoreCooldown.Any())
                {
                    DroneCooldown.Clear();
                }
            }

            // Handle managing sentries
            if (ActiveTarget.Distance < MaxRange && Config.Mode == Mode.Sentry)
            {
                // Is the target a small target?
                if (!SmallTarget(ActiveTarget) || ActiveTarget.Distance > 20000)
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
                    int AvailableSlots = ((MyShip.ToEntity.TypeID == 17918) ? 2 : Me.MaxActiveDrones) - Drone.AllInSpace.Count();
                    List<Drone> Deploy = Drone.AllInBay.Where(a => !DroneCooldown.Contains(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(AvailableSlots).ToList();
                    List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(AvailableSlots).ToList();
                    // Launch drones
                    if (Deploy.Any())
                    {
                        Console.Log("|oLaunching sentry drones");
                        Deploy.Launch();
                        Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(3)));
                        return false;
                    }
                    else if (AvailableSlots > 0 && DeployIgnoreCooldown.Any())
                    {
                        DroneCooldown.Clear();
                    }
                }
            }

            // Fighter management
            if(Fighters.Tubes.Any())
            {
                if(!Fighters.Bay.IsPrimed)
                {
                    Console.Log("Prime() call to FighterBay", LogType.DEBUG);
                    Fighters.Bay.Prime();
                    return false;
                }

                // Send fighters to attack, given they have the ability to
                List<Fighters.Fighter> Attack = Fighters.Active.Where(a => !FighterCooldown.Contains(a) && FighterReady(a) && !a.Attacking.Contains(ActiveTarget) && a.AbilitySlot(KFighter.AbilityType.Attack).HasValue).ToList();
                if (Attack.Any())
                {
                    Console.Log("|oSending fighters to attack "+Attack.Count);
                    Dictionary<int, List<Fighters.Fighter>> usableFighters = Attack.GroupBy(f => f.AbilitySlot(KFighter.AbilityType.Attack).Value).ToDictionary(slot => slot.Key, fighters => fighters.ToList());
                    foreach (KeyValuePair<int, List<Fighters.Fighter>> kvp in usableFighters)
                    {
                        kvp.Value.ActivateSlotOnTarget(kvp.Key, ActiveTarget);
                        Console.Log("Group of "+kvp.Value.Count+" "+kvp.Value.First().Type + " attacking target "+ActiveTarget.Name+" with slot " + kvp.Key, LogType.DEBUG);
                    }

                    Attack.ForEach(a => NextFighterCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    return false;
                }

                // Launch drones
                if(Fighters.Tubes.Any(a => !a.InSpace && FighterReady(a.Fighter) && !FighterCooldown.Contains(a.Fighter)))
                {
                    Fighters.Tubes.Where(a => !a.InSpace && FighterReady(a.Fighter) && !FighterCooldown.Contains(a.Fighter)).ForEach(m => NextFighterCommand.AddOrUpdate(m.Fighter, DateTime.Now.AddSeconds(2)));
                    Fighters.LaunchAllFighters();
                    return false;
                }

                // Wait for drones on cooldown
                if (Fighters.Tubes.Any(a => !a.InSpace && FighterReady(a.Fighter) && FighterCooldown.Contains(a.Fighter)))
                {
                    Fighters.Tubes.Where(a => !a.InSpace && FighterReady(a.Fighter) && FighterCooldown.Contains(a.Fighter)).ForEach(m => {
                        Item FightersToReload = Fighters.Bay.Items.FirstOrDefault(a => a.TypeID == m.Fighter.TypeID);
                        if (FightersToReload != null)
                        {
                            if (m.Fighter.SquadronSize < (int) FightersToReload["fighterSquadronMaxSize"])
                            {
                                m.LoadFightersToTube(FightersToReload);
                                NextFighterCommand.AddOrUpdate(m.Fighter, DateTime.Now.AddSeconds(5 * Math.Min(Math.Abs(FightersToReload.Quantity), ((int)FightersToReload["fighterSquadronMaxSize"]) - m.Fighter.SquadronSize)));
                                return;
                            }
                        }
                        NextFighterCommand.AddOrUpdate(m.Fighter, DateTime.Now.AddSeconds(6));
                        FighterCooldown.Remove(m.Fighter);
                    });
                    return false;
                }

            }

            return false;
        }

        bool FighterShutdown(object[] Params)
        {
            if (!Session.InSpace) return true;

            if (MyShip.ToEntity.Mode == EntityMode.Warping) return false;

            Fighters.RecallAllFightersToTubes();

            return true;
        }

        #endregion
    }
}

﻿#pragma warning disable 1591
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

        public double FighterCriticalHealthLevel = 30;
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
        public Targets.Targets Rats = new Targets.Targets();
        private readonly Security.Security SecurityCore = Security.Security.Instance;
        private readonly HashSet<Drone> DroneCooldown = new HashSet<Drone>();
        private readonly Dictionary<Drone, double> DroneHealthCache = new Dictionary<Drone, double>();
        private readonly IPC IPC = IPC.Instance;
        private readonly Dictionary<long, DateTime> missileFired = new Dictionary<long, DateTime>();
        private readonly List<long> offgridFighters = new List<long>();
        private IEnumerable<Fighters.Fighter> _availableFighters;
        public IEnumerable<Fighters.Fighter> AvailableFighters
        {
            get
            {
                try
                {
                    _availableFighters = Fighters.Active.Where(a => FighterReady(a) && a.ToEntity != null && a.State != Fighters.States.RECALLING).ToList();
                    if (_availableFighters != null)
                    {
                        return _availableFighters;
                    }

                    return new List<Fighters.Fighter>();
                }
                catch (Exception)
                {
                    return new List<Fighters.Fighter>();
                }
            }
        }

        private IEnumerable<Fighters.Fighter> _availableFightersWithReturningFromOffgrid;

        public IEnumerable<Fighters.Fighter> AvailableFightersWithReturningFromOffgrid
        {
            get
            {
                try
                {
                    _availableFightersWithReturningFromOffgrid = Fighters.Active.Where(a => FighterReady(a) && a.ToEntity != null && (a.State != Fighters.States.RECALLING || offgridFighters.Contains(a.ID))).ToList();
                    if (_availableFightersWithReturningFromOffgrid != null)
                    {
                        return _availableFightersWithReturningFromOffgrid;
                    }

                    return new List<Fighters.Fighter>();
                }
                catch (Exception)
                {
                    return new List<Fighters.Fighter>();
                }
            }
        }

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
        Entity ActiveTarget { get; set; }
        Dictionary<Entity, DateTime> TargetCooldown = new Dictionary<Entity, DateTime>();
        bool OutOfTargets = false;
        Dictionary<Drone, DateTime> NextDroneCommand = new Dictionary<Drone, DateTime>();
        Dictionary<Fighters.Fighter, DateTime> NextFighterCommand = new Dictionary<Fighters.Fighter, DateTime>();
        Dictionary<long, Int64> _fighterRocketSalvosLeft = new Dictionary<long, Int64>();

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

        bool FighterMissileTarget(Entity target)
        {
            return (Data.NPCClasses.All.Any(a => a.Key == target.GroupID && (a.Value == "Capital" || a.Value == "Destroyer" || a.Value == "BattleCruiser" || a.Value == "BattleShip")));
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
            List<Fighters.Fighter> RecallFighters = Fighters.Active.Where(a => a.State != Fighters.States.RECALLING).ToList();
            if (RecallFighters.Any() && MyShip.ToEntity.Mode != EntityMode.Warping)
            {
                Console.Log("|oRecalling fighters");
                Fighters.RecallAllFightersToTubes();
                return false;
            }

            if (!Entity.All.Any(b => b.GroupID == Group.ForceField))
            {
                // Speed up Returning Fighters
                IEnumerable<Fighters.Fighter> returningFighters = AvailableFighters.Where(a => a.State == Fighters.States.RECALLING && a.HasPropmod() && a.Slot2.AllowsActivate).ToList();
                if (returningFighters.Any())
                {
                    Console.Log("|oSpeed up Returning Fighters");
                    returningFighters.ActivateAbilitySlotOnSelf(1);
                    return false;
                }
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

            if (Config.Mode == Mode.None && !Fighters.Tubes.Any())
            {
                return false;
            }

            // If we're warping and drones are in space, recall them and stop the module
            if (MyShip.ToEntity.Mode == EntityMode.Warping && MyShip.ToEntity.Velocity.Magnitude < 2000 && Drone.AllInSpace.Any())
            {
                Drone.AllInSpace.ReturnToDroneBay();
                return true;
            }

            // If we're in a POS and fighters are in space, queue delayed stop of the module
            if (Entity.All.Any(a => a.GroupID == Group.ForceField) && Fighters.Active.Any())
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

            if (MyShip.ToEntity.Mode == EntityMode.Warping) return false;

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
                List<Fighters.Fighter> RecallFighters = Fighters.Active.Where(a => a.State != Fighters.States.RECALLING).ToList();
                if (RecallFighters.Any() && MyShip.ToEntity.Mode != EntityMode.Warping)
                {
                    Console.Log("|oRecalling fighters |-gNo rats available");
                    Fighters.RecallAllFightersToTubes();
                    RecallFighters.ForEach(a => NextFighterCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
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

            try
            {
                List<Fighters.Fighter> recallFighters = Fighters.Active.Where(a => a.ToEntity != null && a.Health != null && a.Health < Config.FighterCriticalHealthLevel && a.State != Fighters.States.RECALLING).ToList();
                if (recallFighters.Any())
                {
                    Console.Log("|oRecalling damaged fighter(s)");
                    recallFighters.ForEach(m => m.RecallToTube());
                    recallFighters.ForEach(a => NextFighterCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.Log(ex.ToString(), LogType.DEBUG);
            }

            Entity WarpScrambling = SecurityCore.ValidScramble;
            Entity Neuting = SecurityCore.ValidNeuter;
            Entity lcoToBlowUp = Entity.All.FirstOrDefault(a => (a.GroupID == Group.LargeCollidableObject || a.GroupID == Group.LargeCollidableStructure) && a.Distance <= 1000 && a.Exists && !a.Exploded && !a.Released);

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
            else if (lcoToBlowUp != null)
            {
                if (ActiveTarget != lcoToBlowUp && lcoToBlowUp.Distance < MaxRange)
                {
                    Console.Log("|rLCO on grid is/was keeping us from safely warping off!");
                    Console.Log("|oOveriding current drone target");
                    Console.Log(" |-g{0}", lcoToBlowUp.Name);
                    ActiveTarget = lcoToBlowUp;
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
                if (Rats.LockedTargetList.Any() && ActiveTarget == null)
                {
                    if (Config.PrivateTargets)
                    {
                        if (Config.SharedTargets)
                        {
                            ActiveTarget = Rats.LockedTargetList.FirstOrDefault(a => IPC.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                        }
                        else
                        {
                            ActiveTarget = Rats.LockedTargetList.FirstOrDefault(a => !IPC.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                        }
                    }
                    if (ActiveTarget == null && OutOfTargets)
                    {
                        ActiveTarget = Rats.LockedTargetList.FirstOrDefault(a => a.Distance < MaxRange);
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
                if (!WarpScrambling.LockedTarget && !WarpScrambling.LockingTarget && !WarpScrambling.Exploded && !WarpScrambling.Released)
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
                }
            }
            else if (lcoToBlowUp != null)
            {
                if (!lcoToBlowUp.LockedTarget && !lcoToBlowUp.LockingTarget && !lcoToBlowUp.Exploded && !lcoToBlowUp.Released)
                {
                    if (Rats.LockedAndLockingTargetList.Count >= Me.TrueMaxTargetLocks)
                    {
                        if (Rats.LockedTargetList.Any())
                        {
                            Rats.LockedTargetList.First().UnlockTarget();
                        }
                        return false;
                    }
                    lcoToBlowUp.LockTarget();
                }
            }
            else if (Neuting != null)
            {
                if (!Neuting.LockedTarget && !Neuting.LockingTarget && !Neuting.Exploded && !Neuting.Released)
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
                }
            }
            else
            {
                Entity NewTarget = Entity.All.FirstOrDefault(a => !a.Exploded && !a.Released && !a.LockedTarget && !a.LockingTarget && PriorityTargets.Contains(a.Name) && a.Distance < MyShip.MaxTargetRange && !TargetCooldown.ContainsKey(a) && !Triggers.Contains(a.Name));
                if (NewTarget == null) NewTarget = Rats.UnlockedTargetList.FirstOrDefault(a => !a.Exploded && !a.Released && !TargetCooldown.ContainsKey(a) && a.Distance < MyShip.MaxTargetRange);
                if (Rats.LockedAndLockingTargetList.Count < Config.TargetSlots &&
                    NewTarget != null &&
                    Entity.All.FirstOrDefault(a => a.IsJamming && a.IsTargetingMe) == null)
                {
                    Console.Log("|oLocking");
                    Console.Log(" |-g{0}", NewTarget.Name);
                    TargetCooldown.AddOrUpdate(NewTarget, DateTime.Now.AddSeconds(2));
                    NewTarget.LockTarget();
                    OutOfTargets = false;
                }
            }
            OutOfTargets = true;

            #endregion

            // Flag offgridFighters
            offgridFighters.AddRange(Fighters.Tubes.Where(a => a.InSpace && a.Fighter.ToEntity == null && !offgridFighters.Contains(a.Fighter.ID)).Select(a => a.Fighter.ID));

            // Remove offgridFighters flagging if fighters are on grid and state is != returning
            Fighters.Tubes.Where(a => a.InSpace && a.Fighter.ToEntity != null && a.Fighter.State != Fighters.States.RECALLING && offgridFighters.Contains(a.Fighter.ID)).Select(a => a.Fighter.ID).ForEach(m => offgridFighters.Remove(m));

            // If offgridFighters appeared on grid: command orbit
            Fighters.Tubes.Where(a => a.InSpace && a.Fighter.ToEntity != null && a.Fighter.State == Fighters.States.RECALLING && offgridFighters.Contains(a.Fighter.ID)).Select(a => a.Fighter).ReturnAndOrbit();

            // Make sure ActiveTarget is locked.  If so, make sure it's the active target, if not, return.
            if (ActiveTarget != null && ActiveTarget.Exists && ActiveTarget.LockedTarget)
            {
                if (!Fighters.Tubes.Any() && !ActiveTarget.IsActiveTarget && !ActiveTarget.Exploded && !ActiveTarget.Released)
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
                        return false;
                    }

                    try
                    {
                        IEnumerable<Fighters.Fighter> recallFighters = Fighters.Active.Where(a => a.State != Fighters.States.RECALLING).ToList();
                        if (recallFighters.Any() && !Config.StayDeployedWithNoTargets && MyShip.ToEntity.Mode != EntityMode.Warping)
                        {
                            Console.Log("|oRecalling fighters: No ActiveTarget");
                            Fighters.RecallAllFightersToTubes();
                            recallFighters.ForEach(a => NextFighterCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Log("Exception [" + ex + "]");
                    }

                    // Speed up Returning Fighters
                    try
                    {
                        if (Entity.All.All(b => b.GroupID != Group.ForceField))
                        {
                            IEnumerable<Fighters.Fighter> returningFighter = AvailableFighters.Where(a => a.State == Fighters.States.RECALLING && a.HasPropmod() && a.Slot2 != null && a.Slot2.AllowsActivate).ToList();
                            if (returningFighter.Any())
                            {
                                Console.Log("|oSpeed up Returning Fighters");
                                returningFighter.ActivateAbilitySlotOnSelf(1);
                                returningFighter.ForEach(a => NextFighterCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(2)));
                                return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Log("Exception [" + ex + "]");
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
            if(Fighters.Tubes != null && Fighters.Tubes.Any())
            {
                if (!Fighters.Bay.IsPrimed)
                {
                    Console.Log("Prime() call to FighterBay", LogType.DEBUG);
                    Fighters.Bay.Prime();
                    return false;
                }

                // Launch fighters
                try
                {
                    IEnumerable<Fighters.Tube> deployFighters = Fighters.Tubes.Where(a => !a.InSpace && a.Fighter.State == Fighters.States.READY).ToList();
                    if (deployFighters.Any())
                    {
                        foreach (Fighters.Tube deployfighter in deployFighters)
                        {
                            //Console.Log("Updating _fighterRocketSalvosLeft list for deployFighter ID [" + deployfighter.Fighter.ID + "] to have 12 rockets");
                            _fighterRocketSalvosLeft.AddOrUpdate(deployfighter.Fighter.ID, 12);
                            NextFighterCommand.AddOrUpdate(deployfighter.Fighter, DateTime.Now.AddSeconds(2));
                        }

                        Console.Log("Launching Fighters");
                        Fighters.LaunchAllFighters();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.Log("Exception [" + ex + "]");
                }

                // Speed up Returning Fighters
                try
                {
                    if (Entity.All.All(b => b.GroupID != Group.ForceField))
                    {
                        IEnumerable<Fighters.Fighter> returningFighters = AvailableFighters.Where(a => FighterReady(a) && a.State == Fighters.States.RECALLING && a.HasPropmod() && a.Slot2 != null && a.Slot2.AllowsActivate).ToList();
                        if (returningFighters.Any())
                        {
                            foreach (Fighters.Fighter returningFighter in returningFighters)
                            {
                                Console.Log("|oFighter [" + returningFighter.ID + "] is Returning: Activating Speed Mod");
                                returningFighter.Slot2.ActivateOnSelf();
                                NextFighterCommand.AddOrUpdate(returningFighter, DateTime.Now.AddSeconds(2));
                                return false;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Log("Exception [" + ex + "]");
                }

                if (ActiveTarget != null)
                {
                    try
                    {
                        // Activate propmod for fighters outside optimal range
                        IEnumerable<Fighters.Fighter> fightersThatNeedToActivatePropmod = AvailableFighters.Where(a => a.HasPropmod() && a.ToEntity.DistanceTo(ActiveTarget) > ((double)a["fighterAbilityAttackMissileRangeOptimal"] + (double)a["fighterAbilityAttackMissileRangeFalloff"] + 10000) && a.Slot2 != null && a.Slot2.AllowsActivate).ToList();
                        if (fightersThatNeedToActivatePropmod.Any())
                        {
                            foreach (Fighters.Fighter fighterThatNeedsToActivatPropMod in fightersThatNeedToActivatePropmod)
                            {
                                Console.Log("|oFighter [" + fighterThatNeedsToActivatPropMod.ID + "] is now Activating propmod to get in range of [" + ActiveTarget.Name + "][" + Math.Round(fighterThatNeedsToActivatPropMod.ToEntity.DistanceTo(ActiveTarget)/1000,0) +"k]");
                                fighterThatNeedsToActivatPropMod.Slot2.ActivateOnSelf();
                                NextFighterCommand.AddOrUpdate(fighterThatNeedsToActivatPropMod, DateTime.Now.AddSeconds(2));
                                return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Log("Exception [" + ex + "]");
                    }

                    try
                    {
                        // Use missile, given they have the ability to
                        IEnumerable<Fighters.Fighter> fightersReadyToMissileAttack = AvailableFighters.Where(a => a.State != Fighters.States.RECALLING && a.HasMissiles() && a.Slot3 != null && a.Slot3.AllowsActivate).ToList();
                        if (fightersReadyToMissileAttack.Any())
                        {
                            foreach (Fighters.Fighter fighterReadyToMissileAttack in fightersReadyToMissileAttack)
                            {
                                Entity MissileTarget = Entity.All.Where(a => a.LockedTarget && !a.Exploded && !a.Released && (FighterMissileTarget(a) || a.GroupID == Group.LargeCollidableStructure || a.GroupID == Group.LargeCollidableObject) && fighterReadyToMissileAttack.ToEntity.DistanceTo(a) < (double)fighterReadyToMissileAttack["fighterAbilityMissilesRange"] - 10 && (!missileFired.ContainsKey(a.ID) || missileFired[a.ID].AddSeconds((double)fighterReadyToMissileAttack["fighterAbilityAttackMissileDuration"] / 1000) < DateTime.Now)).OrderBy(a => a == ActiveTarget).ThenByDescending(a => a.Velocity).ThenBy(a => a.ShieldPct).FirstOrDefault();
                                if (MissileTarget != null)
                                {
                                    Console.Log("|oFighter [" + fighterReadyToMissileAttack.ID + "] Initiating Missile attack on [ " + MissileTarget.Name + " ][" + Math.Round(MissileTarget.Distance / 1000, 0) + "k]");
                                    fighterReadyToMissileAttack.Slot3.ActivateOnTarget(MissileTarget);
                                    _fighterRocketSalvosLeft.AddOrUpdate(fighterReadyToMissileAttack.ID, _fighterRocketSalvosLeft[fighterReadyToMissileAttack.ID] - 1);
                                    NextFighterCommand.AddOrUpdate(fighterReadyToMissileAttack, DateTime.Now.AddSeconds(2));
                                    return false;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Log("Exception [" + ex + "]");
                    }

                    try
                    {
                        if (Fighters.Active != null && Fighters.Active.Any())
                        {
                            // Rearm fighters that have run out of Rockets
                            if (AvailableFighters != null)
                            {
                                foreach (Fighters.Fighter availableFighter in AvailableFighters)
                                {
                                    if (_fighterRocketSalvosLeft.ContainsKey(availableFighter.ToEntity.ID) && (_fighterRocketSalvosLeft[availableFighter.ToEntity.ID] <= 0))
                                    {
                                        Console.Log("|oSending Recall to Tube command to [" + availableFighter.Count() + "] fighter [" + availableFighter.ID + "]");
                                        availableFighter.RecallToTube();
                                        NextFighterCommand.AddOrUpdate(availableFighter, DateTime.Now.AddSeconds(2));
                                        return false;
                                    }
                                }
                            }

                            // Send fighters to orbit if they are out of range
                            IEnumerable<Fighters.Fighter> fightersThatNeedANewOrbitTarget = AvailableFightersWithReturningFromOffgrid.Where(a => (a.State != Fighters.States.RECALLING || (a.ToEntity != null && offgridFighters.Contains(a.ID))) && a.Slot1.AllowsActivate && a.ToEntity.DistanceTo(ActiveTarget) > (double)a["maxTargetRange"] - 10).ToList();
                            if (fightersThatNeedANewOrbitTarget.Any())
                            {
                                foreach (Fighters.Fighter fighterThatNeedsANewOrbitTarget in fightersThatNeedANewOrbitTarget.Where(a => a.HasPropmod() && a.Slot2.AllowsActivate))
                                {
                                    Console.Log("|oFighter [" + fighterThatNeedsANewOrbitTarget.ID + "] Initiating Orbit of [" + ActiveTarget.Name + "]");
                                    fighterThatNeedsANewOrbitTarget.Slot2.ActivateOnSelf();
                                    NextFighterCommand.AddOrUpdate(fighterThatNeedsANewOrbitTarget, DateTime.Now.AddSeconds(2));
                                }

                                return false;
                            }

                            // Send fighters to attack, given they have the ability to
                            IEnumerable<Fighters.Fighter> fightersThatNeedAnAttackTarget = AvailableFighters.Where(a => (a.State != Fighters.States.RECALLING || (a.ToEntity != null && offgridFighters != null && offgridFighters.Contains(a.ID))) && a.Slot1 != null && a.Slot1.AllowsActivate && (double)a["maxTargetRange"] != 0 && a.ToEntity.DistanceTo(ActiveTarget) < (double)a["maxTargetRange"] - 10).ToList();
                            if (fightersThatNeedAnAttackTarget.Any())
                            {
                                foreach (Fighters.Fighter fighterThatNeedsAnAttackTarget in fightersThatNeedAnAttackTarget)
                                {
                                    Console.Log("|oFighter [" + fighterThatNeedsAnAttackTarget.ID + "] Initiating Attack of [" + ActiveTarget.Name + "]");
                                    fighterThatNeedsAnAttackTarget.Slot1.ActivateOnTarget(ActiveTarget);
                                    NextFighterCommand.AddOrUpdate(fighterThatNeedsAnAttackTarget, DateTime.Now.AddSeconds(3));
                                    return false;
                                }

                                return false;
                            }

                            // Return fighters that are not moving (not responding to commands?)
                            IEnumerable<Fighters.Fighter> fightersThatShouldReturn = AvailableFighters.Where(a => (a.ToEntity != null && a.ToEntity.Velocity.Magnitude <= 0)).ToList();
                            if (fightersThatShouldReturn.Any() && !Entity.All.Where(i => i.Distance < 100000).Any(i => i.LockingTarget))
                            {
                                foreach (var fighterThatShouldReturn in fightersThatShouldReturn)
                                {
                                    Console.Log("|oFighter [" + fighterThatShouldReturn.ID + "] should now Return and Orbit. It was not moving");
                                    fighterThatShouldReturn.ReturnAndOrbit();
                                    NextFighterCommand.AddOrUpdate(fighterThatShouldReturn, DateTime.Now.AddSeconds(5));
                                    return false;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Log("Exception [" + ex + "]");
                    }
                }

                // Rearm missing fighters
                IEnumerable<Fighters.Tube> rearmFighters = Fighters.Tubes.Where(a => !a.InSpace && a.Fighter.State == Fighters.States.READY && a.Fighter.SquadronSize < (int)a.Fighter["fighterSquadronMaxSize"]);
                if (rearmFighters != null)
                {
                    foreach (Fighters.Tube rearmFighter in rearmFighters)
                    {
                        Item fighterItemToReload = Fighters.Bay.Items.FirstOrDefault(a => a.TypeID == rearmFighter.Fighter.TypeID);
                        if (fighterItemToReload != null)
                        {
                            Console.Log("Missing Fighters in a squadron: Loading a [" + fighterItemToReload.Name + "] into the Tube");
                            rearmFighter.LoadFightersToTube(fighterItemToReload);
                            return false;
                        }
                    }
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

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
        AgressiveSentry,
        AFKSalvage
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
        public bool ReArmFighters = false;
        public bool AttackAnchoredBubble = true;
        public bool AttackHostile = true;
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

        private DateTime _evecomSessionIsReady = DateTime.MinValue;
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
                    if (!Session.InSpace) return null;

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

        private IEnumerable<Fighters.Fighter> _returningFighters;
        public IEnumerable<Fighters.Fighter> ReturningFighters
        {
            get
            {
                try
                {
                    if (!Session.InSpace) return null;

                    _returningFighters = Fighters.Active.Where(a => FighterReady(a) && a.State == Fighters.States.RECALLING).ToList();
                    if (_returningFighters != null)
                    {
                        return _returningFighters;
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
                    if (!Session.InSpace) return null;

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


        private Entity _hostilePilot = null;

        private Entity HostilePilot
        {
            get
            {
                if (!Session.InSpace) return null;

                if (Config.AttackHostile)
                {
                    _hostilePilot =
                        Entity.All.Where(i => i.Distance < 240000).Where(a => Local.Pilots.Any(pilot => pilot.ID == a.OwnerID && pilot.Hostile()))
                            .OrderByDescending(i => i.GroupID == Group.BlackOps)
                            .ThenByDescending(i => i.GroupID == Group.Battleship)
                            .ThenByDescending(i => i.GroupID == Group.AttackBattlecruiser)
                            .ThenByDescending(i => i.GroupID == Group.CombatBattlecruiser)
                            .ThenByDescending(i => i.GroupID == Group.Cruiser)
                            .FirstOrDefault();

                    return _hostilePilot ?? null;
                }

                return null;
            }
        }

        private Entity _anchoredBubble;
        private Entity AnchoredBubble
        {
            get
            {
                if (!Session.InSpace) return null;

                if (Config.AttackAnchoredBubble)
                {
                    _anchoredBubble = Entity.All.Where(i => i.Distance < 240000).FirstOrDefault(a => a.GroupID == Group.MobileWarpDisruptor && a.SurfaceDistance < MyShip.MaxTargetRange);
                    return _anchoredBubble ?? null;
                }

                return null;
            }
        }

        private Entity _warpScrambling;
        private Entity WarpScrambling
        {
            get
            {
                if (!Session.InSpace) return null;

                _warpScrambling = SecurityCore.ValidScramble;
                return _warpScrambling ?? null;
            }
        }

        private Entity _neuting;
        private Entity Neuting
        {
            get
            {
                if (!Session.InSpace) return null;

                _neuting = SecurityCore.ValidNeuter;
                return _neuting ?? null;
            }
        }

        private Entity _lcoToBlowUp;
        private Entity lcoToBlowUp
        {
            get
            {
                if (!Session.InSpace) return null;

                _lcoToBlowUp = Entity.All.FirstOrDefault(a => (a.GroupID == Group.LargeCollidableObject || a.GroupID == Group.LargeCollidableStructure) && a.Distance <= 1000 && a.Exists && !a.Exploded && !a.Released);
                return _lcoToBlowUp ?? null;
            }
        }

        private Double _maxRange = 0;
        private Double MaxRange
        {
            get
            {
                if (!Session.InSpace) return 80000;

                if (Fighters.Tubes.Any())
                {
                    _maxRange = Math.Min(Config.FighterMaxRange, MyShip.MaxTargetRange);
                    return _maxRange;
                }
                else if (Config.Mode == Mode.PointDefense)
                {
                    _maxRange = 20000;
                    return _maxRange;
                }
                else
                {
                    _maxRange = Me.DroneControlDistance;
                    return _maxRange;
                }
            }
        }

        #endregion

        #region Actions

        public void Enabled(bool var)
        {
            if (var)
            {
                if (Idle)
                {
                    LastTargetLocation = MyShip.ToEntity.Position;
                    TryReconnect = true;
                    QueueState(WaitForEve, 2000);
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
        public Entity ActiveTarget { get; set; }
        Dictionary<Entity, DateTime> TargetCooldown = new Dictionary<Entity, DateTime>();
        bool OutOfTargets = false;
        Dictionary<Drone, DateTime> NextDroneCommand = new Dictionary<Drone, DateTime>();
        public Dictionary<Fighters.Fighter, DateTime> NextFighterCommand = new Dictionary<Fighters.Fighter, DateTime>();
        Dictionary<long, Int64> _fighterRocketSalvosLeft = new Dictionary<long, Int64>();

        bool WaitForEve(object[] Params)
        {
            try
            {
                if (Login.AtLogin || CharSel.AtCharSel)
                {
                    Console.Log("Waiting for Login to complete");
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

        bool DroneReady(Drone drone)
        {
            if (!NextDroneCommand.ContainsKey(drone)) return true;
            if (NextDroneCommand[drone] < DateTime.Now) return true;
            return false;
        }

        bool FighterReady(Fighters.Fighter fighter)
        {
            if (!NextFighterCommand.ContainsKey(fighter) && fighter.ToEntity != null) return true;
            if (NextFighterCommand[fighter] < DateTime.Now && fighter.ToEntity != null) return true;
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
                Console.Log("|oRecalling drones: Recall()");
                Recall.ReturnToDroneBay();
                Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(7)));
                return false;
            }
            if (Drone.AllInSpace.Any()) return false;

            // Recall fighters
            List<Fighters.Fighter> RecallFighters = AvailableFighters.Where(a => a.State != Fighters.States.RECALLING).ToList();
            if (RecallFighters.Any() && MyShip.ToEntity.Mode != EntityMode.Warping)
            {
                Console.Log("|oRecalling fighters: Recall()");
                Fighters.RecallAllFightersToTubes();
                return false;
            }

            if (Entity.All.All(b => b.GroupID != Group.ForceField))
            {
                IEnumerable<Fighters.Fighter> returningFightersWithAPropMod = ReturningFighters.Where(a => a.HasPropmod() && a.Slot2 != null && a.Slot2.AllowsActivate).ToList();
                if (returningFightersWithAPropMod.Any())
                {
                    foreach (Fighters.Fighter returningFighterWithAPropMod in returningFightersWithAPropMod)
                    {
                        Console.Log("|oFighter [|g" + MaskedId(returningFighterWithAPropMod.ID) + "|o] is returning: turning on speed mod");
                        returningFighterWithAPropMod.Slot2.ActivateOnSelf();
                        NextFighterCommand.AddOrUpdate(returningFighterWithAPropMod, DateTime.Now.AddSeconds(3));
                    }

                    return false;
                }
            }

            if (Fighters.Active.Any()) return false;

            return true;
        }
        Vector3 LastTargetLocation = Vector3.origin;
        public bool LockManagement()
        {
            TargetCooldown = TargetCooldown.Where(a => a.Value >= DateTime.Now).ToDictionary(a => a.Key, a => a.Value);
            Rats.LockedAndLockingTargetList.ForEach(a => { TargetCooldown.AddOrUpdate(a, DateTime.Now.AddSeconds(15)); });

            if (HostilePilot != null)
            {
                //Console.Log("|Found HostilePilot");
                if (!HostilePilot.LockedTarget && !HostilePilot.LockingTarget && !HostilePilot.Released && !HostilePilot.Exploded)
                {
                    if (Entity.Targeting.Count + Entity.Targets.Count >= Me.TrueMaxTargetLocks)
                    {
                        if (Entity.Targeting.Any())
                        {
                            Console.Log("|oUnLocking [" + Rats.LockedTargetList.First().Name + "][" + Math.Round(Rats.LockedTargetList.First().Distance / 1000, 0) + "k|o] to free up a targeting slot");
                            Rats.LockedTargetList.First().UnlockTarget();
                        }
                        return false;
                    }
                    Console.Log("|oLocking HostilePilot [|-g" + HostilePilot.Name + "|o][|g" + MaskedId(HostilePilot.ID) + "|o][|g" + Math.Round(HostilePilot.Distance / 1000, 0) + "k|o]");
                    HostilePilot.LockTarget();
                    return false;
                }
            }
            else if (AnchoredBubble != null)
            {
                //Console.Log("|Found AnchoredBubble");
                if (!AnchoredBubble.LockedTarget && !AnchoredBubble.LockingTarget && !AnchoredBubble.Released && !AnchoredBubble.Exploded)
                {
                    if (Entity.Targeting.Count + Entity.Targets.Count >= Me.TrueMaxTargetLocks)
                    {
                        if (Entity.Targeting.Any())
                        {
                            Console.Log("|oUnLocking [" + Rats.LockedTargetList.First().Name + "][" + Math.Round(Rats.LockedTargetList.First().Distance / 1000, 0) + "k|o] to free up a targeting slot");
                            Rats.LockedTargetList.First().UnlockTarget();
                        }
                        return false;
                    }
                    Console.Log("|oLocking AnchoredBubble [|-g" + AnchoredBubble.Name + "|o][|g" + MaskedId(AnchoredBubble.ID) + "|o][|g" + Math.Round(AnchoredBubble.Distance / 1000, 0) + "k|o]");
                    AnchoredBubble.LockTarget();
                    return false;
                }
            }
            else if (WarpScrambling != null)
            {
                //Console.Log("|Found WarpScrambling entity");
                if (!WarpScrambling.LockedTarget && !WarpScrambling.LockingTarget && !WarpScrambling.Released && !WarpScrambling.Exploded)
                {
                    if (Rats.LockedAndLockingTargetList.Count >= Me.TrueMaxTargetLocks)
                    {
                        if (Rats.LockedTargetList.Any())
                        {
                            Console.Log("|oUnLocking [" + Rats.LockedTargetList.First().Name + "][" + Math.Round(Rats.LockedTargetList.First().Distance / 1000,0) + "k|o] to free up a targeting slot");
                            Rats.LockedTargetList.First().UnlockTarget();
                        }
                        return false;
                    }
                    Console.Log("|oLocking WarpScrambling [|-g" + WarpScrambling.Name + "|o][|g" + MaskedId(WarpScrambling.ID) + "|o][|g" + Math.Round(WarpScrambling.Distance / 1000, 0) + "k|o]");
                    WarpScrambling.LockTarget();
                    return false;
                }
            }
            else if (lcoToBlowUp != null)
            {
                //Console.Log("|Found lcoToBlowUp entity");
                if (!lcoToBlowUp.LockedTarget && !lcoToBlowUp.LockingTarget && !lcoToBlowUp.Released && !lcoToBlowUp.Exploded)
                {
                    if (Rats.LockedAndLockingTargetList.Count >= Me.TrueMaxTargetLocks)
                    {
                        if (Rats.LockedTargetList.Any())
                        {
                            Console.Log("|oUnLocking [" + Rats.LockedTargetList.First().Name + "][" + Math.Round(Rats.LockedTargetList.First().Distance / 1000, 0) + "k|o] to free up a targeting slot");
                            Rats.LockedTargetList.First().UnlockTarget();
                        }
                        return false;
                    }
                    Console.Log("|oLocking lcoToBlowUp [|-g" + lcoToBlowUp.Name + "|o][|g" + MaskedId(lcoToBlowUp.ID) + "|o][|g" + Math.Round(lcoToBlowUp.Distance / 1000, 0) + "k|o]");
                    lcoToBlowUp.LockTarget();
                    return false;
                }
            }
            else if (Neuting != null)
            {
                //Console.Log("|Found Neuting entity");
                if (!Neuting.LockedTarget && !Neuting.LockingTarget && !Neuting.Released && !Neuting.Exploded)
                {
                    if (Rats.LockedAndLockingTargetList.Count >= Me.TrueMaxTargetLocks)
                    {
                        if (Rats.LockedTargetList.Any())
                        {
                            Console.Log("|oUnLocking [" + Rats.LockedTargetList.First().Name + "][" + Math.Round(Rats.LockedTargetList.First().Distance / 1000, 0) + "k|o] to free up a targeting slot");
                            Rats.LockedTargetList.First().UnlockTarget();
                        }
                        return false;
                    }
                    Console.Log("|oLocking Neuts [|-g" + Neuting.Name + "|o][|g" + MaskedId(Neuting.ID) + "|o][|g" + Math.Round(Neuting.Distance / 1000, 0) + "k|o]");
                    Neuting.LockTarget();
                    return false;
                }
            }
            else if (ActiveTarget != null && ActiveTarget.Exists && !ActiveTarget.Exploded && !ActiveTarget.Released) //&& Config.PriorityTargets.Contains(ActiveTarget.Name)
            {
                //Console.Log("|oActiveTarget is not null");
                if (!ActiveTarget.LockedTarget && !ActiveTarget.LockingTarget)
                {
                    if (Entity.Targeting.Count + Entity.Targets.Count >= Me.TrueMaxTargetLocks)
                    {
                        if (Entity.Targeting.Any())
                        {
                            Console.Log("|oUnLocking [" + Rats.LockedTargetList.First().Name + "][" + Math.Round(Rats.LockedTargetList.First().Distance / 1000, 0) + "k|o] to free up a targeting slot");
                            Rats.LockedTargetList.First().UnlockTarget();
                        }
                        return false;
                    }

                    if (MyShip.ToEntity.Mode == EntityMode.Warping) return false;
                    Console.Log("|oLocking ActiveTarget [|-g" + ActiveTarget.Name + "|o][|g" + MaskedId(ActiveTarget.ID) + "|o][|g" + Math.Round(ActiveTarget.Distance / 1000, 0) + "k|o]");
                    ActiveTarget.LockTarget();
                    return false;
                }
            }

            if (ActiveTarget != null && (ActiveTarget.Exploded || ActiveTarget.Released || !ActiveTarget.Exists))
            {
                ActiveTarget = null;
            }

            if (Rats.LockedAndLockingTargetList.Count < Config.TargetSlots)
            {
                //Console.Log("|oActiveTarget is empty; picking a NewTarget");
                Entity NewTarget = Rats.UnlockedTargetList.FirstOrDefault(a => !a.Exploded && !a.Released && !TargetCooldown.ContainsKey(a) && a.Distance < MyShip.MaxTargetRange); // && !Config.Triggers.Contains(a.Name));
                if (NewTarget == null) NewTarget = Rats.UnlockedTargetList.FirstOrDefault(a => !a.Exploded && !a.Released && !TargetCooldown.ContainsKey(a) && a.Distance < MyShip.MaxTargetRange);
                if (NewTarget != null && Entity.All.FirstOrDefault(a => a.IsJamming && a.IsTargetingMe) == null)
                {
                    Console.Log("|oLocking [|-g" + NewTarget.Name + "|o][|g" + MaskedId(NewTarget.ID) + "|o][|g" + Math.Round(NewTarget.Distance / 1000,0) + "k|o]");
                    TargetCooldown.AddOrUpdate(NewTarget, DateTime.Now.AddSeconds(2));
                    NewTarget.LockTarget();
                    OutOfTargets = false;
                    return false;
                }
                if (ActiveTarget != null && !ActiveTarget.LockedTarget && !ActiveTarget.LockingTarget)
                {
                    //Console.Log("|oif (ActiveTarget != null && !ActiveTarget.LockedTarget && !ActiveTarget.LockingTarget)");
                    ActiveTarget = null;
                    return false;
                }
            }

            OutOfTargets = true;
            return true;
        }

        bool ChooseActiveTarget()
        {
            Entity entityToUseForClosestNpcMeasurement = null;
            if (AvailableFighters.Any())
            {
                entityToUseForClosestNpcMeasurement = AvailableFighters.FirstOrDefault().ToEntity;
            }

            if (entityToUseForClosestNpcMeasurement == null)
            {
                entityToUseForClosestNpcMeasurement = MyShip.ToEntity;
            }

            if (HostilePilot != null)
            {
                if (ActiveTarget != HostilePilot && HostilePilot.Distance < MaxRange)
                {
                    Console.Log("|rHostile Pilot on Grid! |o[|g" + HostilePilot.Name + "|o][|g" + Math.Round(HostilePilot.Distance, 0) + "k|o]");
                    Console.Log("|oOveriding current drone target");
                    ActiveTarget = HostilePilot;
                    return false;
                }
            }
            else if (AnchoredBubble != null)
            {
                if (ActiveTarget != AnchoredBubble && AnchoredBubble.Distance < MaxRange)
                {
                    Console.Log("|rAnchored bubble on grid! Attacking it so that we dont keep getting stuck. |o[|g" + AnchoredBubble.Name + "|o][|g" + Math.Round(AnchoredBubble.Distance, 0) + "k|o]");
                    Console.Log("|oOveriding current drone target");
                    ActiveTarget = AnchoredBubble;
                    return false;
                }
            }
            else if (WarpScrambling != null)
            {
                if (ActiveTarget != WarpScrambling && WarpScrambling.Distance < MaxRange)
                {
                    Console.Log("|rEntity on grid is/was warp scrambling! |o[|g" + WarpScrambling.Name + "|o][|g" + Math.Round(WarpScrambling.Distance, 0) + "k|o]");
                    Console.Log("|oOveriding current drone target");
                    ActiveTarget = WarpScrambling;
                    return false;
                }
            }
            else if (lcoToBlowUp != null)
            {
                if (ActiveTarget != lcoToBlowUp && lcoToBlowUp.Distance < MaxRange)
                {
                    Console.Log("|rLCO on grid is/was keeping us from safely warping off! |o[|g" + lcoToBlowUp.Name + "|o][|g" + Math.Round(lcoToBlowUp.Distance, 0) + "k|o]");
                    Console.Log("|oOveriding current drone target");
                    ActiveTarget = lcoToBlowUp;
                    return false;
                }
            }
            else if (Neuting != null)
            {
                if (ActiveTarget != Neuting && Neuting.Distance < MaxRange)
                {
                    Console.Log("|rEntity on grid is/was neuting! |o[|g" + Neuting.Name + "|o][|g" + Math.Round(Neuting.Distance, 0) + "k|o]");
                    Console.Log("|oOveriding current drone target");
                    ActiveTarget = Neuting;
                    return false;
                }
            }

            if (ActiveTarget == null || !ActiveTarget.Exists || ActiveTarget.Exploded || ActiveTarget.Released)
            {
                List<Entity> AvailableTargets = Entity.All.ToList();
                if (LastTargetLocation != Vector3.origin) AvailableTargets = AvailableTargets.OrderBy(a => a.DistanceTo(LastTargetLocation)).ToList();
                ActiveTarget = null;
                ActiveTarget = AvailableTargets.FirstOrDefault(a => PriorityTargets.Contains(a.Name) && !a.Exploded && !a.Released && (a.LockedTarget || a.LockingTarget) && !Triggers.Contains(a.Name) && a.Distance < MaxRange);

                AvailableTargets = Rats.LockedAndLockingTargetList.ToList();
                if (LastTargetLocation != Vector3.origin) AvailableTargets = AvailableTargets.OrderBy(a => a.DistanceTo(LastTargetLocation)).ToList();
                if (AvailableTargets.Any() && ActiveTarget == null)
                {
                    if (Config.PrivateTargets)
                    {
                        if (Config.SharedTargets)
                        {
                            if (entityToUseForClosestNpcMeasurement != null)
                            {
                               //ActiveTarget = AvailableTargets.FirstOrDefault(a => IPC.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                                ActiveTarget = Rats.LockedTargetList.OrderBy(i => i.DistanceTo(entityToUseForClosestNpcMeasurement)).FirstOrDefault(a => IPC.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                            }
                        }
                        else
                        {
                            if (entityToUseForClosestNpcMeasurement != null)
                            {
                                //ActiveTarget = AvailableTargets.FirstOrDefault(a => !IPC.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                                ActiveTarget = Rats.LockedTargetList.OrderBy(i => i.DistanceTo(entityToUseForClosestNpcMeasurement)).FirstOrDefault(a => !IPC.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                            }
                        }
                    }
                    if (ActiveTarget == null && OutOfTargets)
                    {
                        ActiveTarget = AvailableTargets.FirstOrDefault(a => a.Distance < MaxRange && !a.Exploded && !a.Released);
                    }
                    if (ActiveTarget != null)
                    {
                        IPC.Relay(Me.CharID, ActiveTarget.ID);
                    }

                    return false;
                }

            if (ActiveTarget != null && ActiveTarget.Exists) LastTargetLocation = ActiveTarget.Position;

                return false;
            }

            //Console.Log("|oActivetarget is [" + ActiveTarget.Name + "][" + Math.Round(ActiveTarget.Distance/1000, 0) + "k]");
            return true;
        }

        bool Control(object[] Params)
        {
            if (!Session.InSpace || MyShip.ToEntity.Velocity.Magnitude > 8000)
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
                Drone.AllInSpace.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(7)));
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
                    Console.Log("|oRecalling drones |-gNo rats available");
                    Recall.ReturnToDroneBay();
                    Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    return false;
                }
                // Recall fighters
                List<Fighters.Fighter> RecallFighters = AvailableFighters.ToList();
                if (RecallFighters.Any() && MyShip.ToEntity.Mode != EntityMode.Warping)
                {
                    Console.Log("|oRecalling fighters |-gNo rats available");
                    Fighters.RecallAllFightersToTubes();
                    RecallFighters.ForEach(a => NextFighterCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    return false;
                }

                if (Entity.All.All(b => b.GroupID != Group.ForceField))
                {
                    IEnumerable<Fighters.Fighter> returningFightersWithAPropMod = ReturningFighters.Where(a => a.HasPropmod() && a.Slot2 != null && a.Slot2.AllowsActivate).ToList();
                    if (returningFightersWithAPropMod.Any())
                    {
                        foreach (Fighters.Fighter returningFighterWithAPropMod in returningFightersWithAPropMod)
                        {
                            Console.Log("|oFighter [|g" + MaskedId(returningFighterWithAPropMod.ID) + "|o][|g" + Math.Round(returningFighterWithAPropMod.ToEntity.Distance / 1000,0) + "k|o] is returning: turning on speed mod!");
                            returningFighterWithAPropMod.Slot2.ActivateOnSelf();
                            NextFighterCommand.AddOrUpdate(returningFighterWithAPropMod, DateTime.Now.AddSeconds(3));
                        }

                        return false;
                    }
                }
            }
            if (Config.Mode == Mode.AFKHeavy && (Rats.TargetList.Any() || Entity.All.Any(a => PriorityTargets.Contains(a.Name))))
            {
                int AvailableSlots = ((MyShip.ToEntity.TypeID == 17918 /* Rattlesnake */) ? 2 : Me.MaxActiveDrones) - Drone.AllInSpace.Count();
                List<Drone> Deploy = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones")).Take(AvailableSlots).ToList();
                // Launch drones
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching heavy drones.");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                }
                return false;
            }

            if (Config.Mode == Mode.AFKSalvage)
            {
                int AvailableSlots = Me.MaxActiveDrones - Drone.AllInSpace.Count();
                List<Drone> Deploy = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Salvage Drone")).Take(AvailableSlots).ToList();
                // Launch drones
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching salvage drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
                    return false;
                }

                List<Drone> Salvage = Drone.AllInSpace.Where(a => !DroneCooldown.Contains(a) && DroneReady(a) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Salvage Drone") && a.State != EntityState.Salvaging).ToList();
                if (Salvage.Any())
                {
                    Console.Log("|oTelling drones to salvage");
                    Salvage.Salvage();
                    Salvage.ForEach(a => NextDroneCommand.AddOrUpdate(a, DateTime.Now.AddSeconds(5)));
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
                List<Fighters.Fighter> damagedFighters = AvailableFighters.Where(a => a.Health != null && a.Health < Config.FighterCriticalHealthLevel).ToList();
                if (damagedFighters.Any())
                {
                    foreach (Fighters.Fighter damagedFighter in damagedFighters)
                    {
                        Console.Log("|o Fighter [|g" + MaskedId(damagedFighter.ID) + "|o][|g" + Math.Round(damagedFighter.ToEntity.Distance / 1000, 0) + "k|o] is Damaged: Recalling");
                        damagedFighter.RecallToTube();
                        NextFighterCommand.AddOrUpdate(damagedFighter, DateTime.Now.AddSeconds(5));
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Log(ex.ToString(), LogType.DEBUG);
            }

            #region LockManagement
            //Console.Log("if (!LockManagement()) return false;");
            if (!LockManagement()) return false;
            #endregion

            #region ActiveTarget selection
            //Console.Log("if (!ChooseActiveTarget()) return false;");
            if (!ChooseActiveTarget()) return false;
            #endregion

            try
            {
                // Flag offgridFighters
                offgridFighters.AddRange(Fighters.Tubes.Where(a => a.InSpace && a.Fighter.ToEntity == null && !offgridFighters.Contains(a.Fighter.ID)).Select(a => a.Fighter.ID));
            }
            catch (Exception){}

            // Remove offgridFighters flagging if fighters are on grid and state is != returning
            Fighters.Tubes.Where(a => a.InSpace && a.Fighter.ToEntity != null && a.Fighter.State != Fighters.States.RECALLING && offgridFighters.Contains(a.Fighter.ID)).Select(a => a.Fighter.ID).ForEach(m => offgridFighters.Remove(m));

            // If offgridFighters appeared on grid: command orbit
            Fighters.Tubes.Where(a => a.InSpace && a.Fighter.ToEntity != null && a.Fighter.State == Fighters.States.RECALLING && offgridFighters.Contains(a.Fighter.ID)).Select(a => a.Fighter).ReturnAndOrbit();

            // Make sure ActiveTarget is locked.  If so, make sure it's the active target, if not, return.
            if (ActiveTarget != null && ActiveTarget.Exists && ActiveTarget.LockedTarget)
            {
                if (!ActiveTarget.IsActiveTarget && !ActiveTarget.Exploded && !ActiveTarget.Released)
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
                        IEnumerable<Fighters.Fighter> recallFighters = AvailableFighters.ToList();
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
                            IEnumerable<Fighters.Fighter> returningFightersWithAPropMod = AvailableFighters.Where(a => a.HasPropmod() && a.Slot2 != null && a.Slot2.AllowsActivate).ToList();
                            if (returningFightersWithAPropMod.Any())
                            {
                                foreach (Fighters.Fighter returningFighterWithAPropMod in returningFightersWithAPropMod)
                                {
                                    Console.Log("|oFighter [|g" + MaskedId(returningFighterWithAPropMod.ID) + "|o] is returning: propmod on");
                                    returningFighterWithAPropMod.Slot2.ActivateOnSelf();
                                    NextFighterCommand.AddOrUpdate(returningFighterWithAPropMod, DateTime.Now.AddSeconds(5));
                                }

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
                    if (Deploy.Any() && Rats.LockedAndLockingTargetList.Any())
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
                if (Deploy.Any() && Rats.LockedAndLockingTargetList.Any())
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
                if (Deploy.Any() && Rats.LockedAndLockingTargetList.Any())
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
                if (Deploy.Any() && Rats.LockedAndLockingTargetList.Any())
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
                if (Deploy.Any() && Rats.LockedAndLockingTargetList.Any())
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
                    if (Deploy.Any() && Rats.LockedAndLockingTargetList.Any())
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

                try
                {
                    if (Config.ReArmFighters)
                    {
                        // Rearm missing fighters
                        IEnumerable<Fighters.Tube> rearmFightersInTube = Fighters.Tubes.Where(a => a.Fighter != null && !a.InSpace && a.Fighter.State == Fighters.States.READY && a.Fighter.SquadronSize < (int)a.Fighter["fighterSquadronMaxSize"]).ToList();
                        if (rearmFightersInTube.Any())
                        {
                            foreach (Fighters.Tube rearmFighter in rearmFightersInTube)
                            {
                                Item fighterItemToReload = Fighters.Bay.Items.FirstOrDefault(a => a.TypeID == rearmFighter.Fighter.TypeID);
                                if (fighterItemToReload != null)
                                {
                                    Console.Log("|oMissing Fighters in a squadron: Loading a [|g" + fighterItemToReload.Name + "|o] into the Tube");
                                    rearmFighter.LoadFightersToTube(fighterItemToReload);
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

                // Launch fighters
                try
                {
                    IEnumerable<Fighters.Tube> deployFighters = Fighters.Tubes.Where(a => !a.InSpace && a.Fighter.State == Fighters.States.READY).ToList();
                    if (deployFighters.Any() && Rats.LockedAndLockingTargetList.Any())
                    {
                        foreach (Fighters.Tube deployfighter in deployFighters.Where(i => (NextFighterCommand.ContainsKey(i.Fighter) && DateTime.Now > NextFighterCommand[i.Fighter]) || !NextFighterCommand.ContainsKey(i.Fighter)))
                        {
                            //Console.Log("Updating _fighterRocketSalvosLeft list for deployFighter ID [" + deployfighter.Fighter.ID + "] to have 12 rockets");
                            _fighterRocketSalvosLeft.AddOrUpdate(deployfighter.Fighter.ID, 12);
                            NextFighterCommand.AddOrUpdate(deployfighter.Fighter, DateTime.Now.AddSeconds(3));
                            Console.Log("Launching Fighter [" + MaskedId(deployfighter.Fighter.ID) + "]");
                            deployfighter.Launch();
                            return false;
                        }
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
                        IEnumerable<Fighters.Fighter> returningFightersWithAPropMod = ReturningFighters.Where(a => a.HasPropmod() && a.Slot2 != null && a.Slot2.AllowsActivate).ToList();
                        if (returningFightersWithAPropMod.Any())
                        {
                            foreach (Fighters.Fighter returningFighterWithAPropMod in returningFightersWithAPropMod)
                            {
                                Console.Log("|oFighter [|g" + MaskedId(returningFighterWithAPropMod.ID) + "|o][|g" + Math.Round(returningFighterWithAPropMod.ToEntity.Distance / 1000,0) + "k|o] is Returning: burning back");
                                returningFighterWithAPropMod.Slot2.ActivateOnSelf();
                                NextFighterCommand.AddOrUpdate(returningFighterWithAPropMod, DateTime.Now.AddSeconds(10));
                                return false;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Log("Exception [" + ex + "]");
                }

                if (ActiveTarget != null && AvailableFighters.Any())
                {
                    try
                    {
                        // Activate propmod for fighters outside optimal range
                        IEnumerable<Fighters.Fighter> fightersThatNeedToActivatePropmod = AvailableFighters.Where(a => a.HasPropmod() && a.ToEntity.DistanceTo(ActiveTarget) > ((double)a["fighterAbilityAttackMissileRangeOptimal"] + (double)a["fighterAbilityAttackMissileRangeFalloff"] + 10000) && a.Slot2 != null && a.Slot2.AllowsActivate).ToList();
                        if (fightersThatNeedToActivatePropmod.Any())
                        {
                            foreach (Fighters.Fighter fighterThatNeedsToActivatPropMod in fightersThatNeedToActivatePropmod)
                            {
                                Console.Log("|oFighter [|g" + MaskedId(fighterThatNeedsToActivatPropMod.ID) + "|o] [2]MWD toward[|g" + ActiveTarget.Name + "|o][|g" + MaskedId(ActiveTarget.ID) + "|o][|g" + Math.Round(fighterThatNeedsToActivatPropMod.ToEntity.DistanceTo(ActiveTarget)/1000,0) + "k|o] FighterToTarget");
                                fighterThatNeedsToActivatPropMod.Slot2.ActivateOnSelf();
                                NextFighterCommand.AddOrUpdate(fighterThatNeedsToActivatPropMod, DateTime.Now.AddSeconds(3));
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
                        // Rearm fighters that have run out of Rockets
                        foreach (Fighters.Fighter availableFighter in AvailableFighters)
                        {
                            if (_fighterRocketSalvosLeft.ContainsKey(availableFighter.ToEntity.ID) && (_fighterRocketSalvosLeft[availableFighter.ToEntity.ID] <= 0))
                            {
                                Console.Log("|oFighter [" + MaskedId(availableFighter.ID) + "|o] Reloading Rockets");
                                availableFighter.RecallToTube();
                                NextFighterCommand.AddOrUpdate(availableFighter, DateTime.Now.AddSeconds(7));
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
                                Entity rocketTargetEntity = Entity.All.Where(a => a.LockedTarget && !a.Exploded && !a.Released && (FighterMissileTarget(a) || a.GroupID == Group.LargeCollidableStructure || a.GroupID == Group.LargeCollidableObject || a.GroupID == Group.DestructibleSentryGun) && fighterReadyToMissileAttack.ToEntity.DistanceTo(a) < (double)fighterReadyToMissileAttack["fighterAbilityMissilesRange"] - 3000 && (!missileFired.ContainsKey(a.ID) || missileFired[a.ID].AddSeconds((double)fighterReadyToMissileAttack["fighterAbilityAttackMissileDuration"] / 1000 + 1) < DateTime.Now)).OrderBy(a => a == ActiveTarget).ThenByDescending(a => a.DistanceTo(fighterReadyToMissileAttack.ToEntity)).ThenByDescending(a => a.Velocity).ThenBy(a => a.ShieldPct).FirstOrDefault();
                                if (rocketTargetEntity != null)
                                {
                                    Console.Log("|oFighter [|g" + MaskedId(fighterReadyToMissileAttack.ID) + "|o] [3]Rocket    [|g" + rocketTargetEntity.Name + "|o][|g" + MaskedId(rocketTargetEntity.ID) + "|o][|g" + Math.Round(rocketTargetEntity.DistanceTo(fighterReadyToMissileAttack.ToEntity) / 1000, 0) + "k|o] FighterToTarget");
                                    fighterReadyToMissileAttack.Slot3.ActivateOnTarget(rocketTargetEntity);
                                    _fighterRocketSalvosLeft.AddOrUpdate(fighterReadyToMissileAttack.ID, _fighterRocketSalvosLeft[fighterReadyToMissileAttack.ID] - 1);
                                    fightersReadyToMissileAttack.ForEach(a => NextFighterCommand.AddOrUpdate(a, DateTime.Now.AddMilliseconds(800)));
                                    NextFighterCommand.AddOrUpdate(fighterReadyToMissileAttack, DateTime.Now.AddSeconds(4));
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
                        // Send fighters to attack, given they have the ability to
                        IEnumerable<Fighters.Fighter> fightersThatNeedAnAttackTarget = AvailableFighters.Where(a => (a.State != Fighters.States.RECALLING || (a.ToEntity != null && offgridFighters != null && offgridFighters.Contains(a.ID))) && a.Slot1 != null && a.Slot1.AllowsActivate && (double) a["maxTargetRange"] != 0).ToList();  //&& a.ToEntity.DistanceTo(ActiveTarget) < (double)a["maxTargetRange"] - 10).ToList();
                        if (fightersThatNeedAnAttackTarget.Any() && ActiveTarget != null && ActiveTarget.Exists && !ActiveTarget.Exploded && !ActiveTarget.Released)
                        {
                            foreach (Fighters.Fighter fighterThatNeedsAnAttackTarget in fightersThatNeedAnAttackTarget)
                            {
                                Console.Log("|oFighter [|g" + MaskedId(fighterThatNeedsAnAttackTarget.ID) + "|o] [1]Attacking [|g" + ActiveTarget.Name + "|o][|g" + MaskedId(ActiveTarget.ID) + "|o][|g" + Math.Round(fighterThatNeedsAnAttackTarget.ToEntity.DistanceTo(ActiveTarget) / 1000, 0) + "k|o] FighterToTarget"); //
                                fighterThatNeedsAnAttackTarget.Slot1.ActivateOnTarget(ActiveTarget);
                                fightersThatNeedAnAttackTarget.ForEach(a => NextFighterCommand.AddOrUpdate(a, DateTime.Now.AddMilliseconds(800)));
                                NextFighterCommand.AddOrUpdate(fighterThatNeedsAnAttackTarget, DateTime.Now.AddSeconds(4));
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
                                Console.Log("|oFighter [|g" + MaskedId(fighterThatShouldReturn.ID) + "|o] should now Return and Orbit. It was not moving");
                                fighterThatShouldReturn.ReturnAndOrbit();
                                NextFighterCommand.AddOrUpdate(fighterThatShouldReturn, DateTime.Now.AddSeconds(2));
                                return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Log("Exception [" + ex + "]");
                    }
                }
            }

            return false;
        }

        bool FighterShutdown(object[] Params)
        {
            if (!Session.InSpace) return true;

            if (MyShip.ToEntity.Mode == EntityMode.Warping) return false;
            Console.Log("|oRecalling fighters: |gFighterShutdown()");
            Fighters.RecallAllFightersToTubes();
            return true;
        }

        public string MaskedId(long ID)
        {
            try
            {
                string _unmaskedID = ID.ToString();
                int numofCharacters = _unmaskedID.Length;
                if (numofCharacters >= 5)
                {
                    string maskedID = _unmaskedID.Substring(numofCharacters - 2);
                    maskedID = "|oID#|g" + maskedID;
                    return maskedID;
                }

                return ID.ToString();
            }
            catch (Exception exception)
            {
                Console.Log("EntityCache", "Exception [" + exception + "]");
                return ID.ToString();
            }
        }

        #endregion
    }
}

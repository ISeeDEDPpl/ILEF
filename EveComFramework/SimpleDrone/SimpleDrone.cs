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

    /// <summary>
    /// SimpleDrone Mode of operation: Which drones will we try to use and how do you want them to behave?
    /// </summary>
    public enum Mode
    {
        /// <summary>
        /// SimpleDrone Mode: No specific mode defined: used for Fighters
        /// </summary>
        None,
        /// <summary>
        /// SimpleDrone Mode: Use Sentry Drones
        /// </summary>
        Sentry,
        /// <summary>
        /// SimpleDrone Mode: Point Defense
        /// </summary>
        PointDefense,
        /// <summary>
        /// SimpleDrone Mode: Use Scout Drones: Agressively
        /// </summary>
        AgressiveScout,
        /// <summary>
        /// SimpleDrone Mode: Use Medium Drones: Agressively
        /// </summary>
        AgressiveMedium,
        /// <summary>
        /// SimpleDrone Mode: AFK Heavy Drones
        /// </summary>
        AfkHeavy,
        /// <summary>
        /// SimpleDrone Mode: Use Heavy Drones: Agressively
        /// </summary>
        AgressiveHeavy,
        /// <summary>
        /// SimpleDrone Mode: Use Sentry Drones: Agressively
        /// </summary>
        AgressiveSentry,
        /// <summary>
        /// SimpleDrone Mode: AFK Salvage Drones
        /// </summary>
        AfkSalvage
    }

    #endregion

    #region Settings

    /// <summary>
    /// SimpleDrone: Settings: Serialized to XML and reloaded from XML
    /// </summary>
    public class SimpleDroneSettings : Settings
    {
        /// <summary>
        /// SimpleDrone Mode of operation: which drones will we be using?
        /// </summary>
        public Mode Mode = Mode.None;

        /// <summary>
        /// Should drones /fighters stay deployed even if there are no targets?
        /// </summary>
        public bool StayDeployedWithNoTargets = false;

        /// <summary>
        /// Attack Hostile? PVP?
        /// </summary>
        public bool AttackHostile = true;

        /// <summary>
        /// Use the Fighters Missle Attack on the ActiveTarget?
        /// </summary>
        public bool UseFighterMissileAttackOnActiveTarget = false;

        internal bool PrivateTargets = true;
        internal bool SharedTargets = false;
        internal bool TargetCooldownRandomize = false;
        internal int TargetSlots = 2;

        internal int TargetCooldown
        {
            get
            {
                if (TargetCooldownRandomize)
                {
                    Random rng = new Random();
                    return rng.Next(2, 10);
                }

                return 2;
            }
        }
        internal double FighterCriticalHealthLevel = 70;
        internal double FighterMaxRange = 800000;
        internal bool ReArmFighters = false;
        internal bool RefillRockets = false;
        internal bool AttackAnchoredBubble = true;
    }

    #endregion

    /// <summary>
    /// SimpleDrone Class - Drone and Fighter Targeting and Damage Application
    /// </summary>
    public class SimpleDrone : State
    {
        #region Instantiation

        static SimpleDrone _instance;
        /// <summary>
        /// Instance of SimpleDrone - Allow Only One
        /// </summary>
        public static SimpleDrone Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SimpleDrone();
                }
                return _instance;
            }
        }

        private SimpleDrone()
        {
            _rats.AddPriorityTargets();
            _rats.AddNPCs();
            _rats.AddTargetingMe();
            _rats.Ordering = new RatComparer();
            Console.Log("SimpleDrone Instance Created");
        }

        #endregion

        #region Variables

        private DateTime _evecomSessionIsReady = DateTime.MinValue;
        /// <summary>
        /// Writes Log Entries to the GUI, log file and innerspace console
        /// </summary>
        public Logger Console = new Logger("SimpleDrone");
        /// <summary>
        /// Settings for SimpleDrone: Serialized to XML and Deserialized from XML
        /// </summary>
        public SimpleDroneSettings Config = new SimpleDroneSettings();
        /// <summary>
        /// Instance of Targets: used for tracking what is locked / locking / unlocked
        /// </summary>
        private readonly Targets.Targets _rats = new Targets.Targets();
        private readonly Security.Security _securityCore = Security.Security.Instance;
        private readonly HashSet<Drone> _droneCooldown = new HashSet<Drone>();
        private readonly Dictionary<Drone, double> _droneHealthCache = new Dictionary<Drone, double>();
        private readonly IPC _ipc = IPC.Instance;
        private readonly Dictionary<long?, int?> _missileEntityTracking = new Dictionary<long?, int?>();
        private readonly List<long> _offgridFighters = new List<long>();
        private IEnumerable<Fighters.Fighter> _availableFighters;
        /// <summary>
        /// AvailableFighters - Fighters on Grid that are ready for orders and not already being recalled
        /// </summary>
        public IEnumerable<Fighters.Fighter> AvailableFighters
        {
            get
            {
                try
                {
                    if (!Session.InSpace) return new List<Fighters.Fighter>();

                    _availableFighters = Fighters.Active.Where(a => FighterReady(a.ID) && a.ToEntity != null && a.State != Fighters.States.RECALLING).ToList();
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
        /// <summary>
        /// Fighters in space that are currently returning to the Fighter Bay
        /// </summary>
        public IEnumerable<Fighters.Fighter> ReturningFighters
        {
            get
            {
                try
                {
                    if (!Session.InSpace) return null;

                    _returningFighters = Fighters.Active.Where(a => FighterReady(a.ID) && a.State == Fighters.States.RECALLING).ToList();
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

        private IEnumerable<Drone> _dronesInSpace;
        /// <summary>
        /// Drones in space that are not Incapacitated or about to Die
        /// </summary>
        public IEnumerable<Drone> DronesInSpace
        {
            get
            {
                if (Drone.AllInSpace.Any(i => i.State != EntityState.Departing && i.State != EntityState.Incapacitated))
                {
                    _dronesInSpace = Drone.AllInSpace.Where(i => i.State != EntityState.Departing && i.State != EntityState.Incapacitated);
                    return _dronesInSpace;
                }

                return new List<Drone>();
            }
        }



        /// <summary>
        /// PriorityTargets - Targets that should be handles before others: by default there are no priority targets they are added manually using the GUI
        /// </summary>
        public List<string> PriorityTargets = new List<string>();
        /// <summary>
        /// Triggers - NPCs that cause other spawns and thus should be killed last: by default there are no triggers they are added manually using the GUI
        /// </summary>
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

                _warpScrambling = _securityCore.ValidScramble;
                return _warpScrambling ?? null;
            }
        }

        private Entity _neuting;
        private Entity Neuting
        {
            get
            {
                if (!Session.InSpace) return null;

                _neuting = _securityCore.ValidNeuter;
                return _neuting ?? null;
            }
        }

        private Entity _lcoToBlowUp;
        private Entity lcoToBlowUp
        {
            get
            {
                if (!Session.InSpace) return null;
                _lcoToBlowUp = Entity.All.FirstOrDefault(a => (a.GroupID == Group.LargeCollidableObject || a.GroupID == Group.LargeCollidableStructure) && !a.Name.ToLower().Contains("rock") && !a.Name.ToLower().Contains("stone") && !a.Name.ToLower().Contains("asteroid") && a.Distance <= 1000 && a.Exists && !a.Exploded && !a.Released);
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

        /// <summary>
        /// When 'Enabled' and Idle start
        /// </summary>
        /// <param name="var"></param>
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
        Dictionary<long, DateTime> TargetCooldown = new Dictionary<long, DateTime>();
        bool OutOfTargets = false;
        Dictionary<long, DateTime> NextDroneCommand = new Dictionary<long, DateTime>();
        public Dictionary<long, DateTime> NextFighterCommand = new Dictionary<long, DateTime>();
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

        bool DroneReady(long droneID)
        {
            if (Drone.AllInSpace.Any(droneInSpace => droneInSpace.ID == droneID && (droneInSpace.State == EntityState.Departing || droneInSpace.State == EntityState.Incapacitated)))
            {
                return false;
            }

            if (NextDroneCommand != null && NextDroneCommand.Any())
            {
                if (NextDroneCommand.ContainsKey(droneID))
                {
                    if (DateTime.Now < NextDroneCommand[droneID]) return false;
                }
            }

            return true;
        }

        bool FighterReady(long fighterID)
        {
            if (NextFighterCommand != null && NextFighterCommand.Any())
            {
                if (NextFighterCommand.ContainsKey(fighterID))
                {
                    if (DateTime.Now < NextFighterCommand[fighterID]) return false;
                }
            }

            return true;
        }

        bool FighterMissileTarget(Entity target)
        {
            if (Data.NPCClasses.All.Any(a => a.Key == target.GroupID && (a.Value == "Capital" || a.Value == "BattleShip")))
            {
                return true;
            }

            if (target.GroupID == Group.LargeCollidableStructure ||
                target.GroupID == Group.LargeCollidableObject ||
                target.GroupID == Group.DestructibleSentryGun)
            {
                if (!target.Name.Contains("Rock") && !target.Name.Contains("Stone") && !target.Name.Contains("Asteroid") && !target.Name.Contains("Stargate"))
                {
                    if (target.Distance < 1000)
                    {
                        return true;
                    }

                    return false;
                }

                return false;
            }

            return false;
        }

        bool NPCBattlecruiser(Entity target)
        {
            return (Data.NPCClasses.All.Any(a => a.Key == target.GroupID && (a.Value == "BattleCruiser")));
        }

        bool NPCFrigate(Entity target)
        {
            return (Data.NPCClasses.All.Any(a => a.Key == target.GroupID && (a.Value == "Destroyer" || a.Value == "Frigate")));
        }

        bool SmallFighterMissileTarget(Entity target)
        {
            return (Data.NPCClasses.All.Any(a => a.Key == target.GroupID && (a.Value == "Destroyer" || a.Value == "BattleCruiser")));
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

        /// <summary>
        /// Is it safe to issue Fighter Commands? i.e. Not warping
        /// </summary>
        /// <returns></returns>
        public bool SafeToIssueFighterCommands()
        {
            if (Session.InStation) return false;
            if (!Session.InSpace) return false;
            if (MyShip.ToEntity.Mode == EntityMode.Warping) return false;
            if (MyShip.ToEntity.Velocity.Magnitude > 10000) return false;
            return true;
        }

        /// <summary>
        /// RecallFighters that are on grid and are not yet returning
        /// </summary>
        /// <param name="fightersToRecall"></param>
        /// <param name="reason"></param>
        /// <returns></returns>
        public bool RecallFighters(IEnumerable<Fighters.Fighter> fightersToRecall, string reason)
        {
            try
            {
                if (fightersToRecall != null && fightersToRecall.Any())
                {
                    fightersToRecall = fightersToRecall.ToList();
                    if (fightersToRecall.Any(a => FighterReady(a.ID) && a.ToEntity != null && a.State != Fighters.States.RECALLING))
                    {
                        IEnumerable<Fighters.Fighter> fightersWaitingToBeRecalled = fightersToRecall.Where(fighter => FighterReady(fighter.ID) && fighter.ToEntity != null && fighter.State != Fighters.States.RECALLING).ToList();
                        if (SafeToIssueFighterCommands())
                        {
                            foreach (Fighters.Fighter fighterWaitingToBeRecalled in fightersWaitingToBeRecalled)
                            {
                                Console.Log("|oFighter [" + fighterWaitingToBeRecalled.Type + "][" + MaskedId(fighterWaitingToBeRecalled.ID) + "|o][|g" + Math.Round(fighterWaitingToBeRecalled.ToEntity.Distance / 1000, 0) + "k|o] Recalling [|g" + reason + "|o]");
                                fighterWaitingToBeRecalled.RecallToTube();
                                NextFighterCommand.AddOrUpdate(fighterWaitingToBeRecalled.ID, DateTime.Now.AddSeconds(7));
                                continue;
                            }

                            //Console.Log("RecallFighters: All Fighters are recalling");
                            return false;
                        }

                        //Console.Log("RecallFighters: fightersToRecall.Where...");
                        return false;
                    }

                    if (_offgridFighters != null && _offgridFighters.Any(fighterId => FighterReady(fighterId)))
                    {
                        IEnumerable<Fighters.Fighter> offgridFightersWaitingToBeRecalled = fightersToRecall.Where(fighter => FighterReady(fighter.ID)).ToList();
                        if (offgridFightersWaitingToBeRecalled.Any() && SafeToIssueFighterCommands())
                        {
                            foreach (Fighters.Fighter offgridFighterWaitingToBeRecalled in offgridFightersWaitingToBeRecalled)
                            {
                                Console.Log("|oFighter [" + offgridFighterWaitingToBeRecalled.Type + "][" + MaskedId(offgridFighterWaitingToBeRecalled.ID) + "|o][|g" + Math.Round(offgridFighterWaitingToBeRecalled.ToEntity.Distance / 1000, 0) + "k|o] Recalling from offgrid [|g" + reason + "|o]");
                                offgridFighterWaitingToBeRecalled.RecallToTube();
                                NextFighterCommand.AddOrUpdate(offgridFighterWaitingToBeRecalled.ID, DateTime.Now.AddSeconds(30));
                                continue;
                            }

                            //Console.Log("|oRecallFighters: _offgridFighters All fighters are recalling");
                            return false;
                        }

                        //Console.Log("|oRecallFighters: ... _offgridFighters.Any(fighterId => FighterReady(fighterId)))");
                        return true;
                    }

                    return true;
                }

                //Console.Log("|oRecallFighters: We have no fighters to recall at the moment.");
                return true;
            }
            catch (Exception ex)
            {
                Console.Log("Exception [" + ex + "]");
                return true;
            }
        }

        bool Recall(object[] Params)
        {
            if (Session.InStation) return true;

            // Recall drones
            List<Drone> Recall = DronesInSpace.Where(droneInSpace => DroneReady(droneInSpace.ID)).ToList();
            if (Recall.Any())
            {
                Console.Log("|oRecalling drones: Recall()");
                Recall.ReturnToDroneBay();
                Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(7)));
                return false;
            }
            if (DronesInSpace.Any()) return false;

            //
            // Recall fighters
            //
            if (AvailableFighters.Any())
            {
                if(!RecallFighters(AvailableFighters, "Recalling")) return false;
                if (!SpeedUpFighters(ReturningFighters, "Burning Back")) return false;
            }

            if (Fighters.Active.Any(i => i.ToEntity != null && i.ToEntity.Distance < 900000)) return false;
            _missileEntityTracking.Clear();
            return true;
        }

        /// <summary>
        /// Turn on the Prop Mod on a set of fighters
        /// </summary>
        /// <param name="fightersThatNeedPropModOn"></param>
        /// <param name="reason"></param>
        /// <returns></returns>
        public bool SpeedUpFighters(IEnumerable<Fighters.Fighter> fightersThatNeedPropModOn, string reason)
        {
            try
            {
                if (Entity.All.All(b => b.GroupID != Group.ForceField))
                {
                    if (fightersThatNeedPropModOn != null && fightersThatNeedPropModOn.Any(a => a.ToEntity != null && a.ToEntity.Velocity.Magnitude < 3000 && a.HasPropmod() && a.Slot2 != null && a.Slot2.AllowsActivate && a.ToEntity.Distance > 15000))
                    {
                        fightersThatNeedPropModOn = fightersThatNeedPropModOn.ToList();
                        IEnumerable<Fighters.Fighter> availableFightersThatNeedPropModOn = fightersThatNeedPropModOn.Where(a => FighterReady(a.ID) && a.HasPropmod() && a.Slot2 != null && a.Slot2.AllowsActivate && a.ToEntity.Distance > 15000).ToList();
                        if (availableFightersThatNeedPropModOn.Any(i => i.ToEntity != null))
                        {
                            foreach (Fighters.Fighter availableFighterThatNeedPropModOn in availableFightersThatNeedPropModOn)
                            {
                                Console.Log("|oFighter [|g" + MaskedId(availableFighterThatNeedPropModOn.ID) + "|o] propmod on: [|g" + reason + "|o]");
                                availableFighterThatNeedPropModOn.Slot2.ActivateOnSelf();
                                NextFighterCommand.AddOrUpdate(availableFighterThatNeedPropModOn.ID, DateTime.Now.AddSeconds(2));
                            }

                            return false;
                        }

                        return false;
                    }

                    //Console.Log("|oSpeedUpFighters: No Fighters need and have a speedmod ready");
                    return true;
                }

                //Console.Log("|oSpeedUpFighters: We are in a POS: Fighters will have to slow boat it back.");
                return true;
            }
            catch (Exception ex)
            {
                Console.Log("Exception [" + ex + "]");
                return true;
            }
        }

        Vector3 LastTargetLocation = Vector3.origin;
        public bool LockManagement()
        {
            Entity entityToUseForClosestNpcMeasurement = null;
            if (AvailableFighters != null && AvailableFighters.Any())
            {
                entityToUseForClosestNpcMeasurement = AvailableFighters.FirstOrDefault().ToEntity;
            }

            if (entityToUseForClosestNpcMeasurement == null)
            {
                entityToUseForClosestNpcMeasurement = MyShip.ToEntity;
            }

            TargetCooldown = TargetCooldown.Where(a => a.Value >= DateTime.Now).ToDictionary(a => a.Key, a => a.Value);
            _rats.LockedAndLockingTargetList.ForEach(a => { TargetCooldown.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(Config.TargetCooldown)); });

            if (HostilePilot != null)
            {
                //Console.Log("|Found HostilePilot");
                if (!HostilePilot.LockedTarget && !HostilePilot.LockingTarget && HostilePilot.Exists && !HostilePilot.Released && !HostilePilot.Exploded)
                {
                    if (Entity.Targeting.Count + Entity.Targets.Count >= Me.TrueMaxTargetLocks)
                    {
                        if (Entity.Targeting.Any())
                        {
                            Console.Log("|oUnLocking [" + _rats.LockedTargetList.First().Name + "][" + Math.Round(_rats.LockedTargetList.First().Distance / 1000, 0) + "k|o] to free up a targeting slot");
                            _rats.LockedTargetList.First().UnlockTarget();
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
                            Console.Log("|oUnLocking [" + _rats.LockedTargetList.First().Name + "][" + Math.Round(_rats.LockedTargetList.First().Distance / 1000, 0) + "k|o] to free up a targeting slot");
                            _rats.LockedTargetList.First().UnlockTarget();
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
                    if (_rats.LockedAndLockingTargetList.Count >= Me.TrueMaxTargetLocks)
                    {
                        if (_rats.LockedTargetList.Any())
                        {
                            Console.Log("|oUnLocking [" + _rats.LockedTargetList.First().Name + "][" + Math.Round(_rats.LockedTargetList.First().Distance / 1000,0) + "k|o] to free up a targeting slot");
                            _rats.LockedTargetList.First().UnlockTarget();
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
                    if (_rats.LockedAndLockingTargetList.Count >= Me.TrueMaxTargetLocks)
                    {
                        if (_rats.LockedTargetList.Any())
                        {
                            Console.Log("|oUnLocking [" + _rats.LockedTargetList.First().Name + "][" + Math.Round(_rats.LockedTargetList.First().Distance / 1000, 0) + "k|o] to free up a targeting slot");
                            _rats.LockedTargetList.First().UnlockTarget();
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
                    if (_rats.LockedAndLockingTargetList.Count >= Me.TrueMaxTargetLocks)
                    {
                        if (_rats.LockedTargetList.Any())
                        {
                            Console.Log("|oUnLocking [" + _rats.LockedTargetList.First().Name + "][" + Math.Round(_rats.LockedTargetList.First().Distance / 1000, 0) + "k|o] to free up a targeting slot");
                            _rats.LockedTargetList.First().UnlockTarget();
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
                            Console.Log("|oUnLocking [" + _rats.LockedTargetList.First().Name + "][" + Math.Round(_rats.LockedTargetList.First().Distance / 1000, 0) + "k|o] to free up a targeting slot");
                            _rats.LockedTargetList.First().UnlockTarget();
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

            if (_rats.LockedAndLockingTargetList.Count < Config.TargetSlots)
            {
                int freeTargetSlots = Config.TargetSlots - _rats.LockedAndLockingTargetList.Count;
                //Console.Log("|oActiveTarget is empty; picking a NewTarget");
                IEnumerable<Entity> newTargets = _rats.UnlockedTargetList.OrderByDescending(i => PriorityTargets.Any() && PriorityTargets.Contains(i.Name)).ThenBy(i => i.DistanceTo(entityToUseForClosestNpcMeasurement)).Where(a => a.Exists && !a.Exploded && !a.Released && !TargetCooldown.ContainsKey(a.ID) && a.Distance < MyShip.MaxTargetRange).ToList();
                if (newTargets.Any() && Entity.All.FirstOrDefault(a => a.IsJamming && a.IsTargetingMe) == null)
                {
                    foreach (Entity newTarget in newTargets)
                    {
                        freeTargetSlots--;
                        Console.Log("|oLocking [|-g" + newTarget.Name + "|o][|g" + MaskedId(newTarget.ID) + "|o][|g" + Math.Round(newTarget.Distance / 1000, 0) + "k|o]");
                        TargetCooldown.AddOrUpdate(newTarget.ID, DateTime.Now.AddSeconds(Config.TargetCooldown));
                        newTarget.LockTarget();
                        OutOfTargets = false;
                        if (freeTargetSlots == 0) return true;
                        continue;
                    }

                    return false;
                }
                else if (ActiveTarget != null && !ActiveTarget.LockedTarget && !ActiveTarget.LockingTarget)
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
                try
                {
                    List<Entity> AvailableTargets = Entity.All.ToList();
                    if (LastTargetLocation != Vector3.origin) AvailableTargets = AvailableTargets.OrderBy(a => a.DistanceTo(LastTargetLocation)).ToList();
                    ActiveTarget = null;
                    AvailableTargets = _rats.LockedAndLockingTargetList.OrderBy(i => i.DistanceTo(entityToUseForClosestNpcMeasurement)).ToList();
                    ActiveTarget = AvailableTargets.FirstOrDefault(a => PriorityTargets.Contains(a.Name) && !a.Exploded && !a.Released && (a.LockedTarget || a.LockingTarget) && !Triggers.Contains(a.Name)  && a.Distance < MaxRange);

                    if (LastTargetLocation != Vector3.origin) AvailableTargets = AvailableTargets.OrderBy(a => a.DistanceTo(LastTargetLocation)).ToList();
                    if (AvailableTargets.Any() && ActiveTarget == null)
                    {
                        if (Config.PrivateTargets)
                        {
                            if (Config.SharedTargets)
                            {
                                if (entityToUseForClosestNpcMeasurement != null)
                                {
                                    //ActiveTarget = AvailableTargets.FirstOrDefault(a => _ipc.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                                    ActiveTarget = AvailableTargets.Where(a => !Triggers.Contains(a.Name)).OrderBy(i => i.DistanceTo(entityToUseForClosestNpcMeasurement)).FirstOrDefault(a => _ipc.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                                    if (ActiveTarget == null) ActiveTarget = AvailableTargets.OrderBy(i => i.DistanceTo(entityToUseForClosestNpcMeasurement)).FirstOrDefault(a => _ipc.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                                }
                            }
                            else
                            {
                                if (entityToUseForClosestNpcMeasurement != null)
                                {
                                    //ActiveTarget = AvailableTargets.FirstOrDefault(a => !_ipc.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                                    ActiveTarget = _rats.LockedTargetList.Where(a => !Triggers.Contains(a.Name)).OrderBy(i => i.DistanceTo(entityToUseForClosestNpcMeasurement)).FirstOrDefault(a => !_ipc.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                                    if (ActiveTarget == null) ActiveTarget = _rats.LockedTargetList.OrderBy(i => i.DistanceTo(entityToUseForClosestNpcMeasurement)).FirstOrDefault(a => !_ipc.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                                }
                            }
                        }
                        if (ActiveTarget == null && OutOfTargets)
                        {
                            ActiveTarget = AvailableTargets.Where(a => !Triggers.Contains(a.Name)).FirstOrDefault(a => a.Distance < MaxRange && !a.Exploded && !a.Released);
                            if (ActiveTarget == null) ActiveTarget = AvailableTargets.FirstOrDefault(a => a.Distance < MaxRange && !a.Exploded && !a.Released);
                        }
                        if (ActiveTarget != null)
                        {
                            _ipc.Relay(Me.CharID, ActiveTarget.ID);
                        }

                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.Log("Exception [" + ex + "]");
                }

            if (ActiveTarget != null && ActiveTarget.Exists) LastTargetLocation = ActiveTarget.Position;

                return false;
            }
            int intAttribute = 0;
            //foreach (KeyValuePair<string, object> a in Entity.ActiveTarget)
            //{
            //    intAttribute++;
            //    Console.Log("ActiveTarget Attribute [" + intAttribute + "] Key[" + a.Key + "] Value [" + a.Value.ToString() + "]");
            //}
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
            if (MyShip.ToEntity.Mode == EntityMode.Warping && MyShip.ToEntity.Velocity.Magnitude < 2000 && DronesInSpace.Any(droneInSpace => DroneReady(droneInSpace.ID)))
            {
                Drone.AllInSpace.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(7)));
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

            if (!_rats.TargetList.Any() && !Entity.All.Any(a => PriorityTargets.Contains(a.Name)) && !Config.StayDeployedWithNoTargets)
            {
                // Recall drones
                List<Drone> Recall = Drone.AllInSpace.Where(droneInSpace => droneInSpace.ToEntity != null && DroneReady(droneInSpace.ID)).ToList();
                if (Recall.Any())
                {
                    Console.Log("|oRecalling drones |-gNo rats available");
                    Recall.ReturnToDroneBay();
                    Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(5)));
                    return false;
                }

                //
                // Recall fighters
                //
                if (AvailableFighters.Any()) if (!RecallFighters(AvailableFighters, "No rats available")) return false;
            }

            //
            // Speed up Fighters
            //
            if (ReturningFighters.Any()) if (!SpeedUpFighters(ReturningFighters, "Burning Back.")) return false;

            if (Config.Mode == Mode.AfkHeavy && (_rats.TargetList.Any() || Entity.All.Any(a => PriorityTargets.Contains(a.Name))))
            {
                int AvailableSlots = ((MyShip.ToEntity.TypeID == 17918 /* Rattlesnake */) ? 2 : Me.MaxActiveDrones) - Drone.AllInSpace.Count();
                List<Drone> Deploy = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones")).Take(AvailableSlots).ToList();
                // Launch drones
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching heavy drones.");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(5)));
                }
                return false;
            }

            if (Config.Mode == Mode.AfkSalvage)
            {
                int AvailableSlots = Me.MaxActiveDrones - Drone.AllInSpace.Count();
                List<Drone> Deploy = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Salvage Drone")).Take(AvailableSlots).ToList();
                // Launch drones
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching salvage drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(5)));
                    return false;
                }

                List<Drone> Salvage = Drone.AllInSpace.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Salvage Drone") && a.State != EntityState.Salvaging).ToList();
                if (Salvage.Any())
                {
                    Console.Log("|oTelling drones to salvage");
                    Salvage.Salvage();
                    Salvage.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(5)));
                }
                return false;
            }

            foreach (Drone d in Drone.AllInBay)
            {
                if (_droneHealthCache.ContainsKey(d)) _droneHealthCache.Remove(d);
            }

            foreach (Drone d in Drone.AllInSpace)
            {
                double health = d.ToEntity.ShieldPct + d.ToEntity.ArmorPct + d.ToEntity.HullPct;
                if (!_droneHealthCache.ContainsKey(d)) _droneHealthCache.Add(d, health);
                if (health < _droneHealthCache[d])
                {
                    _droneCooldown.Add(d);
                }
            }

            List<Drone> RecallDamaged = Drone.AllInSpace.Where(a => _droneCooldown.Contains(a) && DroneReady(a.ID)).ToList();
            if (RecallDamaged.Any())
            {
                Console.Log("|oRecalling damaged drones");
                RecallDamaged.ReturnToDroneBay();
                RecallDamaged.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(5)));
                return false;
            }

            try
            {
                List<Fighters.Fighter> damagedFighters = AvailableFighters.Where(a => a.Health != null && a.Health < Config.FighterCriticalHealthLevel).ToList();
                if (damagedFighters.Any())
                {
                    if (!RecallFighters(damagedFighters, "Low Health")) return false;
                    if (!SpeedUpFighters(damagedFighters.Where(i => i.State == Fighters.States.RECALLING), "Recalling")) return false;
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
                // Flag _offgridFighters
                _offgridFighters.AddRange(Fighters.Tubes.Where(a => a.InSpace && a.Fighter.ToEntity == null && !_offgridFighters.Contains(a.Fighter.ID)).Select(a => a.Fighter.ID));
            }
            catch (Exception){}

            // Remove _offgridFighters flagging if fighters are on grid and state is != returning
            Fighters.Tubes.Where(a => a.InSpace && a.Fighter.ToEntity != null && a.Fighter.State != Fighters.States.RECALLING && _offgridFighters.Contains(a.Fighter.ID)).Select(a => a.Fighter.ID).ForEach(m => _offgridFighters.Remove(m));

            // If _offgridFighters appeared on grid: command orbit
            Fighters.Tubes.Where(a => a.InSpace && a.Fighter.ToEntity != null && a.Fighter.State == Fighters.States.RECALLING && _offgridFighters.Contains(a.Fighter.ID)).Select(a => a.Fighter).ReturnAndOrbit();

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
                    List<Drone> Recall = Drone.AllInSpace.Where(a => DroneReady(a.ID)).ToList();
                    if (Recall.Any())
                    {
                        Console.Log("|oRecalling drones");
                        Recall.ReturnToDroneBay();
                        Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(5)));
                        return false;
                    }

                    //
                    // Recall Fighters if there is no ActiveTarget and Config.StayDeployedWithNoTargets is false
                    //
                    if (!Config.StayDeployedWithNoTargets)
                    {
                        if (AvailableFighters.Any())
                        {
                            if (!RecallFighters(AvailableFighters, "No ActiveTarget")) return false;
                            if (ReturningFighters.Any()) if (!SpeedUpFighters(ReturningFighters, "Burning Back.")) return false;
                        }
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
                    List<Drone> Recall = Drone.AllInSpace.Where(a => _droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group != "Light Scout Drones")).ToList();
                    if (Recall.Any())
                    {
                        Console.Log("|oRecalling non scout drones");
                        Recall.ReturnToDroneBay();
                        Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(5)));
                        return false;
                    }
                    // Send drones to attack
                    List<Drone> Attack = Drone.AllInSpace.Where(a => DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                    if (Attack.Any())
                    {
                        Console.Log("|oSending scout drones to attack");
                        Attack.Attack();
                        Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                        return false;
                    }
                    int AvailableSlots = ((MyShip.ToEntity.TypeID == 17918 /* Rattlesnake */) ? 2 : Me.MaxActiveDrones) - Drone.AllInSpace.Count();
                    List<Drone> Deploy = Drone.AllInBay.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")).Take(AvailableSlots).ToList();
                    List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")).Take(AvailableSlots).ToList();
                    // Launch drones
                    if (Deploy.Any() && _rats.LockedAndLockingTargetList.Any())
                    {
                        Console.Log("|oLaunching scout drones");
                        Deploy.Launch();
                        Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                        return false;
                    }
                    else if (AvailableSlots > 0 && DeployIgnoreCooldown.Any())
                    {
                        _droneCooldown.Clear();
                    }
                }
                else if (Config.Mode == Mode.PointDefense)
                {
                    List<Drone> Recall = Drone.AllInSpace.Where(a => DroneReady(a.ID)).ToList();
                    // Recall drones if in point defense and no frig/destroyers in range
                    if (Recall.Any())
                    {
                        Console.Log("|oRecalling drones");
                        Recall.ReturnToDroneBay();
                        Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(5)));
                        return false;
                    }
                }
            }

            // Handle Attacking anything if in AgressiveScout mode
            if (Config.Mode == Mode.AgressiveScout)
            {
                // Recall sentries
                List<Drone> Recall = Drone.AllInSpace.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group != "Light Scout Drones")).ToList();
                if (Recall.Any())
                {
                    Console.Log("|oRecalling non scout drones");
                    Recall.ReturnToDroneBay();
                    Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(5)));
                    return false;
                }
                // Send drones to attack
                List<Drone> Attack = Drone.AllInSpace.Where(a => DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                if (Attack.Any())
                {
                    Console.Log("|oSending scout drones to attack");
                    Attack.Attack();
                    Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                int AvailableSlots = Me.MaxActiveDrones - Drone.AllInSpace.Count();
                List<Drone> Deploy = Drone.AllInBay.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")).Take(AvailableSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")).Take(AvailableSlots).ToList();
                // Launch drones
                if (Deploy.Any() && _rats.LockedAndLockingTargetList.Any())
                {
                    Console.Log("|oLaunching scout drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (AvailableSlots > 0 && DeployIgnoreCooldown.Any())
                {
                    _droneCooldown.Clear();
                }
            }

            // Handle Attacking anything if in AgressiveMedium mode
            if (Config.Mode == Mode.AgressiveMedium)
            {
                // Recall sentries
                List<Drone> Recall = Drone.AllInSpace.Where(a => _droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group != "Medium Scout Drones") && a.State != EntityState.Departing).ToList();
                if (Recall.Any())
                {
                    Console.Log("|oRecalling non medium drones");
                    Recall.ReturnToDroneBay();
                    Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(5)));
                    return false;
                }
                // Send drones to attack
                List<Drone> Attack = Drone.AllInSpace.Where(a => DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Medium Scout Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                if (Attack.Any())
                {
                    Console.Log("|oSending medium drones to attack");
                    Attack.Attack();
                    Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                int AvailableSlots = ((MyShip.ToEntity.TypeID == 17715 /* Gila */) ? 2 : Me.MaxActiveDrones) - Drone.AllInSpace.Count();
                List<Drone> Deploy = Drone.AllInBay.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Medium Scout Drones")).Take(AvailableSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Medium Scout Drones")).Take(AvailableSlots).ToList();
                // Launch drones
                if (Deploy.Any() && _rats.LockedAndLockingTargetList.Any())
                {
                    Console.Log("|oLaunching medium drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (AvailableSlots > 0 && DeployIgnoreCooldown.Any())
                {
                    _droneCooldown.Clear();
                }
            }

            // Handle Attacking anything if in AgressiveHeavy mode
            if (Config.Mode == Mode.AgressiveHeavy)
            {
                // Recall non heavy
                List<Drone> Recall = Drone.AllInSpace.Where(a => _droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group != "Heavy Attack Drones")).ToList();
                if (Recall.Any())
                {
                    Console.Log("|oRecalling non heavy drones");
                    Recall.ReturnToDroneBay();
                    Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(5)));
                    return false;
                }
                // Send drones to attack
                List<Drone> Attack = Drone.AllInSpace.Where(a => DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                if (Attack.Any())
                {
                    Console.Log("|oSending heavy drones to attack");
                    Attack.Attack();
                    Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                int AvailableSlots = ((MyShip.ToEntity.TypeID == 17918 /* Rattlesnake */) ? 2 : Me.MaxActiveDrones) - Drone.AllInSpace.Count();
                List<Drone> Deploy = Drone.AllInBay.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones")).Take(AvailableSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones")).Take(AvailableSlots).ToList();
                // Launch drones
                if (Deploy.Any() && _rats.LockedAndLockingTargetList.Any())
                {
                    Console.Log("|oLaunching heavy drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (AvailableSlots > 0 && DeployIgnoreCooldown.Any())
                {
                    _droneCooldown.Clear();
                }
            }

            // Handle Attacking anything if in AgressiveSentry mode
            if (Config.Mode == Mode.AgressiveSentry)
            {
                // Recall non heavy
                List<Drone> Recall = Drone.AllInSpace.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group != "Sentry Drones") && a.State != EntityState.Departing).ToList();
                if (Recall.Any())
                {
                    Console.Log("|oRecalling non sentry drones");
                    Recall.ReturnToDroneBay();
                    Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(5)));
                    return false;
                }
                // Send drones to attack
                List<Drone> Attack = Drone.AllInSpace.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                if (Attack.Any())
                {
                    Console.Log("|oSending sentry drones to attack");
                    Attack.Attack();
                    Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                int AvailableSlots = ((MyShip.ToEntity.TypeID == 17918 /* Rattlesnake */) ? 2 : Me.MaxActiveDrones) - Drone.AllInSpace.Count();
                List<Drone> Deploy = Drone.AllInBay.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(AvailableSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(AvailableSlots).ToList();
                // Launch drones
                if (Deploy.Any() && _rats.LockedAndLockingTargetList.Any())
                {
                    Console.Log("|oLaunching sentry drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (AvailableSlots > 0 && DeployIgnoreCooldown.Any())
                {
                    _droneCooldown.Clear();
                }
            }

            // Handle managing sentries
            if (ActiveTarget.Distance < MaxRange && Config.Mode == Mode.Sentry)
            {
                // Is the target a small target?
                if (!SmallTarget(ActiveTarget) || ActiveTarget.Distance > 20000)
                {
                    List<Drone> Recall = Drone.AllInSpace.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group != "Sentry Drones") && a.State != EntityState.Departing).ToList();
                    // Recall non sentries
                    if (Recall.Any())
                    {
                        Console.Log("|oRecalling drones");
                        Recall.ReturnToDroneBay();
                        Recall.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(5)));
                        return false;
                    }
                    List<Drone> Attack = Drone.AllInSpace.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                    // Send drones to attack
                    if (Attack.Any())
                    {
                        Console.Log("|oOrdering sentry drones to attack");
                        Attack.Attack();
                        Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                        return false;
                    }
                    int AvailableSlots = ((MyShip.ToEntity.TypeID == 17918) ? 2 : Me.MaxActiveDrones) - Drone.AllInSpace.Count();
                    List<Drone> Deploy = Drone.AllInBay.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(AvailableSlots).ToList();
                    List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(AvailableSlots).ToList();
                    // Launch drones
                    if (Deploy.Any() && _rats.LockedAndLockingTargetList.Any())
                    {
                        Console.Log("|oLaunching sentry drones");
                        Deploy.Launch();
                        Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                        return false;
                    }
                    else if (AvailableSlots > 0 && DeployIgnoreCooldown.Any())
                    {
                        _droneCooldown.Clear();
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

                //
                // Rearm missing fighters
                //
                try
                {
                    if (Config.ReArmFighters)
                    {
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

                //
                // Launch fighters
                //
                try
                {
                    IEnumerable<Fighters.Tube> deployFighters = Fighters.Tubes.Where(a => a.Fighter != null && !a.InSpace && a.Fighter.State == Fighters.States.READY && FighterReady(a.Fighter.ID)).ToList();
                    if (deployFighters.Any() && _rats.LockedAndLockingTargetList.Any())
                    {
                        foreach (Fighters.Tube deployfighter in deployFighters)
                        {
                            _fighterRocketSalvosLeft.AddOrUpdate(deployfighter.Fighter.ID, 12);
                            NextFighterCommand.AddOrUpdate(deployfighter.Fighter.ID, DateTime.Now.AddSeconds(3));
                            Console.Log("Launching Fighter [" + MaskedId(deployfighter.Fighter.ID) + "]");
                            deployfighter.Launch();
                            continue;
                        }

                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.Log("Exception [" + ex + "]");
                }

                if (ActiveTarget != null && AvailableFighters.Any())
                {
                    //
                    // Activate propmod for fighters outside optimal range
                    //
                    try
                    {
                        IEnumerable<Fighters.Fighter> fightersThatNeedToActivatePropmod = AvailableFighters.Where(a => a.HasPropmod() && a.ToEntity.DistanceTo(ActiveTarget) > ((double)a["fighterAbilityAttackMissileRangeOptimal"] + (double)a["fighterAbilityAttackMissileRangeFalloff"] + 10000) && a.Slot2 != null && a.Slot2.AllowsActivate).ToList();
                        if (!SpeedUpFighters(fightersThatNeedToActivatePropmod, "Burning into range")) return false;
                    }
                    catch (Exception ex)
                    {
                        Console.Log("Exception [" + ex + "]");
                    }

                    //
                    // Rearm fighters that have run out of Rockets
                    //
                    try
                    {
                        if (Config.RefillRockets)
                        {
                            if (AvailableFighters.Any(i => _fighterRocketSalvosLeft != null && _fighterRocketSalvosLeft.ContainsKey(i.ID) && (_fighterRocketSalvosLeft[i.ID] <= 0)))
                            {
                                IEnumerable<Fighters.Fighter> fightersThatNeedToRefillRockets = Fighters.Active.Where(i => FighterReady(i.ID) && _fighterRocketSalvosLeft != null && _fighterRocketSalvosLeft.ContainsKey(i.ID) && (_fighterRocketSalvosLeft[i.ID] <= 0)).ToList();
                                if (fightersThatNeedToRefillRockets.Any())
                                {
                                    if (!RecallFighters(fightersThatNeedToRefillRockets, "Refill Rockets")) return false;
                                    if (!SpeedUpFighters(fightersThatNeedToRefillRockets.Where(i => i.State == Fighters.States.RECALLING), "returning")) return false;
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
                        // Use missile, given they have the ability to
                        IEnumerable<Fighters.Fighter> fightersReadyToMissileAttack = AvailableFighters.Where(a => a.HasMissiles() && a.Slot3 != null && a.Slot3.AllowsActivate).ToList();
                        if (fightersReadyToMissileAttack.Any())
                        {
                            bool slightPauseNeededAfterMissileAttack = false;
                            foreach (Fighters.Fighter fighterReadyToMissileAttack in fightersReadyToMissileAttack)
                            {
                                Entity rocketTargetEntity = _rats.LockedTargetList.FirstOrDefault(a => HostilePilot != null && HostilePilot.ID == a.ID && fighterReadyToMissileAttack.ToEntity.DistanceTo(a) < (double)fighterReadyToMissileAttack["fighterAbilityMissilesRange"] - 3000);
                                if (rocketTargetEntity == null) rocketTargetEntity = _rats.LockedTargetList.OrderByDescending(FighterMissileTarget).FirstOrDefault(a => !Triggers.Contains(a.Name) && a.ArmorPct > 40 &&  (FighterMissileTarget(a) || (Config.UseFighterMissileAttackOnActiveTarget && a == ActiveTarget && !NPCFrigate(a))) && fighterReadyToMissileAttack.ToEntity.DistanceTo(a) < (double)fighterReadyToMissileAttack["fighterAbilityMissilesRange"] - 3000);
                                if (rocketTargetEntity == null) rocketTargetEntity = _rats.LockedTargetList.OrderByDescending(FighterMissileTarget).FirstOrDefault(a => a.ArmorPct > 40 && (FighterMissileTarget(a) || (Config.UseFighterMissileAttackOnActiveTarget && a == ActiveTarget && !NPCFrigate(a))) && fighterReadyToMissileAttack.ToEntity.DistanceTo(a) < (double)fighterReadyToMissileAttack["fighterAbilityMissilesRange"] - 3000);
                                if (rocketTargetEntity != null)
                                {
                                    int missilesAlreadyShotAtThisEntity = 0;
                                    if (_missileEntityTracking!= null && _missileEntityTracking.Any() && _missileEntityTracking.ContainsKey(rocketTargetEntity.ID))
                                    {
                                        missilesAlreadyShotAtThisEntity = (int)_missileEntityTracking[rocketTargetEntity.ID];
                                    }
                                    if (missilesAlreadyShotAtThisEntity <= 1)
                                    {
                                        Console.Log("|oFighter [|g" + MaskedId(fighterReadyToMissileAttack.ID) + "|o] [3]Rocket    [|g" + rocketTargetEntity.Name + "|o][|g" + MaskedId(rocketTargetEntity.ID) + "|o][|g" + Math.Round(rocketTargetEntity.DistanceTo(fighterReadyToMissileAttack.ToEntity) / 1000, 0) + "k|o] FighterToTarget");
                                        fighterReadyToMissileAttack.Slot3.ActivateOnTarget(rocketTargetEntity);
                                        _fighterRocketSalvosLeft.AddOrUpdate(fighterReadyToMissileAttack.ID, _fighterRocketSalvosLeft[fighterReadyToMissileAttack.ID] - 1);
                                        missilesAlreadyShotAtThisEntity++;
                                        _missileEntityTracking.AddOrUpdate(rocketTargetEntity.ID, missilesAlreadyShotAtThisEntity);
                                        NextFighterCommand.AddOrUpdate(fighterReadyToMissileAttack.ID, DateTime.Now.AddSeconds(3));
                                        slightPauseNeededAfterMissileAttack = true;
                                    }

                                    continue;
                                }

                                if (slightPauseNeededAfterMissileAttack) return false;
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
                        IEnumerable<Fighters.Fighter> fightersThatNeedAnAttackTarget = AvailableFighters.Where(a => a.Slot1 != null && a.Slot1.AllowsActivate && (double) a["maxTargetRange"] != 0).ToList();  //&& a.ToEntity.DistanceTo(ActiveTarget) < (double)a["maxTargetRange"] - 10).ToList();
                        if (fightersThatNeedAnAttackTarget.Any() && ActiveTarget != null && ActiveTarget.Exists && !ActiveTarget.Exploded && !ActiveTarget.Released)
                        {
                            foreach (Fighters.Fighter fighterThatNeedsAnAttackTarget in fightersThatNeedAnAttackTarget)
                            {
                                Console.Log("|oFighter [|g" + MaskedId(fighterThatNeedsAnAttackTarget.ID) + "|o] [1]Attacking [|g" + ActiveTarget.Name + "|o][|g" + MaskedId(ActiveTarget.ID) + "|o][|g" + Math.Round(fighterThatNeedsAnAttackTarget.ToEntity.DistanceTo(ActiveTarget) / 1000, 0) + "k|o] FighterToTarget"); //
                                fighterThatNeedsAnAttackTarget.Slot1.ActivateOnTarget(ActiveTarget);
                                NextFighterCommand.AddOrUpdate(fighterThatNeedsAnAttackTarget.ID, DateTime.Now.AddSeconds(2));
                                continue;
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
                                NextFighterCommand.AddOrUpdate(fighterThatShouldReturn.ID, DateTime.Now.AddSeconds(2));
                                continue;
                            }

                            return false;
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
            if (!RecallFighters(Fighters.Active, "FighterShutdown")) return false;
            Console.Log("FighterShutdown completed.");
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

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms.VisualStyles;
using ILoveEVE.Framework;
using ILEF.Caching;
using ILEF.Combat;
using ILEF.Core;
using ILEF.Data;
using ILEF.Targets;
using ILEF.KanedaToolkit;
using ILEF.Lookup;

namespace ILEF.SimpleDrone
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
        public bool UseFighterMissileAttackOnActiveTarget = true;

        public bool PrivateTargets = true;
        public bool SharedTargets = false;
        public bool TargetCooldownRandomize = false;
        public int TargetSlots = 2;
        public bool LockTargetsOneAtaTime = false;

        public int TargetCooldown
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

        public double FighterCriticalHealthLevel = 80;
        public double FighterMaxRange = 800000;
        public bool ReArmFighters = false;
        public bool RefillRockets = false;
        public bool AttackAnchoredBubble = true;
        public int FighterSquadronMaxSize = 9;
        public bool FightersOrbitAtOptimal = true;
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

        //private readonly Security.Security _securityCore = Security.Security.Instance;
        private readonly HashSet<DirectEntity> _droneCooldown = new HashSet<DirectEntity>();
            //note: DirectEntity was Drone when this was EveCom

        private readonly Dictionary<DirectEntity, double> _droneHealthCache = new Dictionary<DirectEntity, double>();
            //note: DirectEntity was Drone when this was EveCom

        private readonly IPC _ipc = IPC.Instance;
        private readonly Dictionary<long?, int?> _missileEntityTracking = new Dictionary<long?, int?>();

        private int _availableDroneSlots;

        /// <summary>
        /// BroneBandwidth / M3 of the correct drone type in DroneBay based on the configured simpledrone mode
        /// </summary>
        public int AvailableDroneSlots
        {
            get
            {
                try
                {
                    int myShipsDroneBandwidth;

                    try
                    {
                        myShipsDroneBandwidth = (int)Enum.Parse(typeof(ShipsDroneBandwidth), DirectEve.ActiveShip.TypeName);
                    }
                    catch (Exception ex)
                    {
                        Console.Log("SimpleDrone.AvailableDroneSlots: Exception [" + ex + "]");
                        myShipsDroneBandwidth = 0;
                    }

                    _availableDroneSlots = 0;
                    if (Drones.DroneBay.UsedCapacity > 0)
                    {
                        double myDronesM3 = Drones.DroneBay.Items.FirstOrDefault().Volume;
                        string droneGroupName;
                        switch (Config.Mode)
                        {
                            case Mode.None:
                                myDronesM3 = Drones.DroneBay.Items.FirstOrDefault().Volume;
                                break;
                            case Mode.AfkHeavy:
                                myDronesM3 = 25;
                                droneGroupName = "Heavy Attack Drones";
                                if (Drones.DroneBay.Items.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeId && b.Group == droneGroupName)))
                                {
                                    myDronesM3 = Drones.DroneBay.Items.OrderByDescending(i => i.Volume).FirstOrDefault(a => Data.DroneType.All.Any(b => b.ID == a.TypeId && b.Group == droneGroupName)).Volume;
                                }
                                break;
                            case Mode.PointDefense:
                                myDronesM3 = Drones.DroneBay.Items.FirstOrDefault().Volume;
                                break;
                            case Mode.Sentry:
                                myDronesM3 = 25;
                                droneGroupName = "Sentry Drones";
                                if (Drones.DroneBay.Items.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeId && b.Group == droneGroupName)))
                                {
                                    myDronesM3 = Drones.DroneBay.Items.OrderByDescending(i => i.Volume).FirstOrDefault(a => Data.DroneType.All.Any(b => b.ID == a.TypeId && b.Group == droneGroupName)).Volume;
                                }
                                break;
                            case Mode.AgressiveScout:
                                myDronesM3 = 5;
                                droneGroupName = "Light Scout Drones";
                                if (Drones.DroneBay.Items.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeId && b.Group == droneGroupName)))
                                {
                                    myDronesM3 = Drones.DroneBay.Items.OrderByDescending(i => i.Volume).FirstOrDefault(a => Data.DroneType.All.Any(b => b.ID == a.TypeId && b.Group == droneGroupName)).Volume;
                                }
                                break;
                            case Mode.AgressiveMedium:
                                myDronesM3 = 10;
                                droneGroupName = "Medium Scout Drones";
                                if (Drones.DroneBay.Items.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeId && b.Group == droneGroupName)))
                                {
                                    myDronesM3 = Drones.DroneBay.Items.OrderByDescending(i => i.Volume).FirstOrDefault(a => Data.DroneType.All.Any(b => b.ID == a.TypeId && b.Group == droneGroupName)).Volume;
                                }
                                break;
                            case Mode.AgressiveHeavy:
                                myDronesM3 = 25;
                                droneGroupName = "Heavy Attack Drones";
                                if (Drones.DroneBay.Items.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeId && b.Group == droneGroupName)))
                                {
                                    myDronesM3 = Drones.DroneBay.Items.OrderByDescending(i => i.Volume).FirstOrDefault(a => Data.DroneType.All.Any(b => b.ID == a.TypeId && b.Group == droneGroupName)).Volume;
                                }
                                break;
                            case Mode.AgressiveSentry:
                                myDronesM3 = 25;
                                droneGroupName = "Sentry Drones";
                                if (Drones.DroneBay.Items.Any(a => Data.DroneType.All.Any(b => b.ID == a.TypeId && b.Group == droneGroupName)))
                                {
                                    myDronesM3 = Drones.DroneBay.Items.OrderByDescending(i => i.Volume).FirstOrDefault(a => Data.DroneType.All.Any(b => b.ID == a.TypeId && b.Group == droneGroupName)).Volume;
                                }
                                break;
                            case Mode.AfkSalvage:
                                myDronesM3 = 5;
                                break;
                        }

                        int numOfDronesBandwidthAllowsToLaunch = (int)Math.Round(myShipsDroneBandwidth / myDronesM3, 0);
                        int totalDroneSlots = 5; // Math.Min(Me.MaxActiveDrones, numOfDronesBandwidthAllowsToLaunch);
                        _availableDroneSlots = totalDroneSlots - Drone.AllInSpace.Count();
                        if (_availableDroneSlots > 0)
                        {
                            return _availableDroneSlots;
                        }

                        return 0;
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Log("Exception [" + ex + "]");
                    return 0;
                }
            }
        }

        private List<long> _offgridFighters;

        /// <summary>
        /// Fighters that are InSpace but not on Grid with us (Fighter.ToEntity == null)
        /// </summary>
        public List<long> OffgridFighters
        {
            get
            {
                try
                {
                    // Flag _offgridFighters
                    _offgridFighters = Fighters.Tubes.Where(a => a.InSpace && a.Fighter.ToEntity == null).Select(a => a.Fighter.ID).ToList();
                    return _offgridFighters;
                }
                catch (Exception) { }

                return null;
            }
        }

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
                    if (!QMCache.Instance.InSpace) return new List<Fighters.Fighter>();

                    _availableFighters = Fighters.Active.Where(a => FighterReady(a.ID) && a.ToEntity != null && a.State != Fighters.States.RECALLING && a.State != Fighters.States.LANDING && a.State != Fighters.States.LAUNCHING).ToList();
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
                    if (!QMCache.Instance.InSpace) return null;

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

        private DateTime lastPriorityTargetListUpdate = DateTime.MinValue;

        private List<string> _priorityTargets = new List<string>();

        /// <summary>
        /// PriorityTargets Added from the main behavior (Ratter / Copilot / evecombine / MissionMiner, etc)
        /// </summary>
        public List<string> PriorityTargetsFromBehavior = new List<string>();

        /// <summary>
        /// PriorityTargets - Targets that should be handles before others: by default there are no priority targets they are added manually using the GUI
        /// </summary>
        public List<string> PriorityTargets
        {
            get
            {
                try
                {
                    if (_priorityTargets != null && _priorityTargets.Any() && DateTime.Now < lastPriorityTargetListUpdate.AddSeconds(10))
                    {
                        return _priorityTargets ?? new List<string>();
                    }

                    lastPriorityTargetListUpdate = DateTime.Now;
                    _priorityTargets = new List<string>();
                    _priorityTargets.Clear();

                    if (PriorityTargetsFromBehavior != null && PriorityTargetsFromBehavior.Any())
                    {
                        _priorityTargets.AddRange(PriorityTargetsFromBehavior.ToList());
                    }

                    if (QMCache.Instance.MyShipEntity != null && QMCache.Instance.MyShipEntity.EntityMode != EntityMode.Warping)
                    {
                        if (_rats.TargetList != null && _rats.TargetList.Any())
                        {
                            IEnumerable<EntityCache> entitiesTargetingMe = QMCache.Instance.Entities.Where(i => i.IsValid && !i.HasExploded && !i.HasReleased && i.IsTargetedBy);
                            try
                            {
                                foreach (EntityCache entityTargetingMeCanWarpScramble in entitiesTargetingMe.Where(i => i.CanWarpScramble))
                                {
                                    if (!_priorityTargets.Contains(entityTargetingMeCanWarpScramble.Name))
                                    {
                                        Console.Log("|oAdding PriorityTarget [|g" + entityTargetingMeCanWarpScramble.Name + "|o][|g" + Math.Round(entityTargetingMeCanWarpScramble.Distance/1000, 0) + "|o]k as it can WarpScramble");
                                        _priorityTargets.Add(entityTargetingMeCanWarpScramble.Name);
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                if (_priorityTargets.Any())
                                {
                                    return _priorityTargets;
                                }

                                return new List<string>();
                            }

                            IEnumerable<EntityCache> movingNpcsThatExistOnGrid = QMCache.Instance.Entities.Where(i => i.IsValid && !i.HasExploded && !i.HasReleased && i.IsNpc && i.Distance < 150000 && i.Velocity > 0);
                            try
                            {
                                foreach (EntityCache warpScramblingNpcThatExistsOnGrid in movingNpcsThatExistOnGrid.Where(i => i.CanWarpScramble))
                                {
                                    if (!_priorityTargets.Contains(warpScramblingNpcThatExistsOnGrid.Name))
                                    {
                                        Console.Log("|oAdding PriorityTarget [|g" + warpScramblingNpcThatExistsOnGrid.Name + "|o][|g" + Math.Round(warpScramblingNpcThatExistsOnGrid.Distance/1000, 0) + "|o]k as it can WarpScrable");
                                        _priorityTargets.Add(warpScramblingNpcThatExistsOnGrid.Name);
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                if (_priorityTargets.Any())
                                {
                                    return _priorityTargets;
                                }

                                return new List<string>();
                            }
                        }
                    }

                    if (_priorityTargets.Any())
                    {
                        return _priorityTargets;
                    }

                    return new List<string>();
                }
                catch (Exception ex)
                {
                    Console.Log("Exception [" + ex + "]");
                    return new List<string>();
                }
            }
            //(int)a.Fighter["fighterSquadronMaxSize"]
        }

        /// <summary>
        /// Triggers - NPCs that cause other spawns and thus should be killed last: by default there are no triggers they are added manually using the GUI
        /// </summary>
        public List<string> Triggers = new List<string>();

        private bool? _insidePosForceField = null;

        public bool InsidePosForceField
        {
            get
            {
                try
                {
                    if (_insidePosForceField == null)
                    {
                        _insidePosForceField = QMCache.Instance.Entities.Where(i => i.Distance < 60000) .Any(b => b.GroupId == (int)Group.ForceField && b.Distance <= 0);
                        return _insidePosForceField ?? false;
                    }

                    return _insidePosForceField ?? false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private Double? _maxRange = null;

        private Double MaxRange
        {
            get
            {
                if (_maxRange == null)
                {
                    if (!QMCache.Instance.InSpace) return 0;

                    if (Fighters.Tubes.Any())
                    {
                        _maxRange = Math.Min(Config.FighterMaxRange, DirectEve.ActiveShip.MaxTargetRange);
                        return (double) _maxRange;
                    }
                    else if (Config.Mode == Mode.PointDefense)
                    {
                        _maxRange = 20000;
                        return (double) _maxRange;
                    }
                    else
                    {
                        _maxRange = Drones.DroneControlRange;
                        return (double) _maxRange;
                    }
                }

                return 0;
            }
        }

        private void ClearCachedDataEveryPulse()
        {
            _insidePosForceField = null;
            _maxRange = null;
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
                    Console.Log("SimpleDrone enabled", LogType.DEBUG);
                    TryReconnect = true;
                    QueueState(WaitForEve, 2000);
                    QueueState(Control);
                }
            }
            else
            {
                Console.Log("SimpleDrone disabled", LogType.DEBUG);
                Clear();
                QueueState(Recall);
                Config.Save();
            }
        }



        #endregion

        #region States

        bool TryReconnect = true;
        public EntityCache ActiveTarget { get; set; }
        Dictionary<long, DateTime> TargetCooldown = new Dictionary<long, DateTime>();
        bool OutOfTargets = false;
        Dictionary<long, DateTime> NextDroneCommand = new Dictionary<long, DateTime>();
        public Dictionary<long, DateTime> NextFighterCommand = new Dictionary<long, DateTime>();
        Dictionary<long, Int64> _fighterRocketSalvosLeft = new Dictionary<long, Int64>();

        bool WaitForEve(object[] Params)
        {
            try
            {
                ClearCachedDataEveryPulse();
                if (DirectEve.Login.AtLogin || DirectEve.Login.AtCharacterSelection)
                {
                    Console.Log("Waiting for Login to complete");
                    return false;
                }

                if (!QMCache.Instance.InSpace && !QMCache.Instance.InStation)
                {
                    Console.Log("Waiting for Session to be safe");
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

                Config.Save();
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

        bool FighterMissileTarget(EntityCache target)
        {
            if (Data.NPCClasses.All.Any(a => (int)a.Key == (int)target.GroupId && (a.Value == "Capital" || a.Value == "BattleShip")))
            {
                return true;
            }

            if (target.GroupId == (int)Group.LargeCollidableStructure ||
                target.GroupId == (int)Group.LargeCollidableObject ||
                target.GroupId == (int)Group.DestructibleSentryGun)
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

        bool NPCBattlecruiser(EntityCache target)
        {
            return (Data.NPCClasses.All.Any(a => (int)a.Key == (int)target.GroupId && (a.Value == "BattleCruiser")));
        }

        bool NPCFrigate(EntityCache target)
        {
            return (Data.NPCClasses.All.Any(a => (int)a.Key == (int)target.GroupId && (a.Value == "Destroyer" || a.Value == "Frigate")));
        }

        bool SmallFighterMissileTarget(EntityCache target)
        {
            return (Data.NPCClasses.All.Any(a => (int)a.Key == (int)target.GroupId && (a.Value == "Destroyer" || a.Value == "BattleCruiser")));
        }

        bool SmallTarget(EntityCache target)
        {
            if (Data.NPCClasses.All.Any(a => (int)a.Key == (int)target.GroupId && (a.Value == "Frigate" || a.Value == "Destroyer")))
            {
                return true;
            }

            if (target.IsPlayer)
            {
                if (target.GroupId == (int)Group.Shuttle ||
                    target.GroupId == (int)Group.Frigate ||
                    target.GroupId == (int)Group.AssaultShip ||
                    //target.GroupId == (int)Group.CovertOps ||
                    target.GroupId == (int)Group.ElectronicAttackShip ||
                    target.GroupId == (int)Group.Interceptor ||
                    //target.GroupId == (int)Group.ExpeditionFrigate ||
                    target.GroupId == (int)Group.Destroyer ||
                    target.GroupId == (int)Group.Interdictor ||
                    //target.GroupId == (int)Group.TacticalDestroyer ||
                    target.GroupId == (int)Group.Capsule //||
                    //target.GroupId == (int)Group.LogisticsFrigate ||
                    //target.GroupId == (int)Group.CommandDestroyer
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
            if (QMCache.Instance.InStation) return false;
            if (!QMCache.Instance.InSpace) return false;
            if (QMCache.Instance.MyShipEntity == null) return false;
            if (QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping) return false;
            if (QMCache.Instance.MyShipEntity.Velocity > 10000) return false;
            return true;
        }

        /// <summary>
        /// RecallFighters that are on grid and are not yet returning
        /// </summary>
        /// <param name="fightersToRecall"></param>
        /// <param name="reason"></param>
        /// <param name="spamReturning"></param>
        /// <param name="waitForFightersToReturn"></param>
        /// <returns></returns>
        public bool RecallFighters(IEnumerable<Fighters.Fighter> fightersToRecall, string reason, bool spamReturning = false, bool waitForFightersToReturn = false)
        {
            try
            {
                if (fightersToRecall != null)
                {
                    if (fightersToRecall.Any())
                    {
                        fightersToRecall = fightersToRecall.ToList();
                        if (!waitForFightersToReturn && fightersToRecall.Any(i => i.ToEntity != null && i.ToEntity.Distance > 30000))
                        {
                            IEnumerable<Fighters.Fighter> fightersTooFarAway = fightersToRecall.Where(i => i.ToEntity != null && FighterReady(i.ID) && i.ToEntity.Distance > 30000).ToList();
                            if (fightersTooFarAway.Any())
                            {
                                if (!InsidePosForceField)
                                {
                                    foreach (Fighters.Fighter fighterTooFarAway in fightersTooFarAway.Where(i => i.ToEntity != null && (i.ToEntity.Distance > i.ToEntity.Velocity.Magnitude*5)))
                                    {
                                        EntityCache closestEntityToOrbit = QMCache.Instance.Entities.OrderByDescending(i => i.DistanceFromEntity(fighterTooFarAway.ToEntity)).FirstOrDefault();
                                        if (closestEntityToOrbit != null)
                                        {
                                            Console.Log("|oFighter [|g" + MaskedId(fighterTooFarAway.ID) + "|o] is [|g" + Math.Round(fighterTooFarAway.ToEntity.Distance/1000, 0) + "|o]k away going [|g" + Math.Round(fighterTooFarAway.ToEntity.Velocity.Magnitude, 0) + "|o]m/s is not likely to make it back before we warp: telling fighter to orbit");
                                            fighterTooFarAway.Follow(closestEntityToOrbit, 60000);
                                            NextFighterCommand.AddOrUpdate(fighterTooFarAway.ID, DateTime.Now.AddSeconds(30));
                                            continue;
                                        }

                                        Console.Log("|oFighter [|g" + MaskedId(fighterTooFarAway.ID) + "|o] is [|g" + Math.Round(fighterTooFarAway.ToEntity.Distance/1000, 0) + "|o]k away going [|g" + Math.Round(fighterTooFarAway.ToEntity.Velocity.Magnitude, 0) + "|o]m/s is not likely to make it back before we warp: stopping fighter");
                                        fighterTooFarAway.Stop();
                                        NextFighterCommand.AddOrUpdate(fighterTooFarAway.ID, DateTime.Now.AddSeconds(30));
                                        continue;
                                    }
                                }
                            }
                        }

                        fightersToRecall = fightersToRecall.ToList();
                        if (fightersToRecall.Any(a => FighterReady(a.ID) && a.ToEntity != null && a.State != Fighters.States.RECALLING))
                        {
                            IEnumerable<Fighters.Fighter> fightersWaitingToBeRecalled = fightersToRecall.Where(fighter =>
                                        (spamReturning || FighterReady(fighter.ID)) &&
                                        fighter.ToEntity != null &&
                                        fighter.State != Fighters.States.RECALLING &&
                                        fighter.State != Fighters.States.LANDING &&
                                        fighter.State != Fighters.States.LAUNCHING).ToList();
                            if (SafeToIssueFighterCommands())
                            {
                                foreach (Fighters.Fighter fighterWaitingToBeRecalled in fightersWaitingToBeRecalled)
                                {
                                    Console.Log("|oFighter [" + fighterWaitingToBeRecalled.Type + "][" + MaskedId(fighterWaitingToBeRecalled.ID) + "|o][|g" + Math.Round(fighterWaitingToBeRecalled.ToEntity.Distance/1000, 0) + "k|o] Recalling [|g" + reason + "|o]");
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
                    }
                }

                if (OffgridFighters != null && OffgridFighters.Any(fighterId => (spamReturning || FighterReady(fighterId))))
                {
                    IEnumerable<Fighters.Fighter> offgridFightersWaitingToBeRecalled = Fighters.Active.Where(fighter => (spamReturning || FighterReady(fighter.ID)) && OffgridFighters.Contains(fighter.ID)).ToList();
                    if (offgridFightersWaitingToBeRecalled.Any() && SafeToIssueFighterCommands())
                    {
                        foreach (Fighters.Fighter offgridFighterWaitingToBeRecalled in offgridFightersWaitingToBeRecalled)
                        {
                            offgridFighterWaitingToBeRecalled.RecallToTube();
                            NextFighterCommand.AddOrUpdate(offgridFighterWaitingToBeRecalled.ID, DateTime.Now.AddSeconds(30));
                            Console.Log("|oFighter [" + offgridFighterWaitingToBeRecalled.Type + "][" + MaskedId(offgridFighterWaitingToBeRecalled.ID) + "|o] Recalling from offgrid [|g" + reason + "|o]");
                            continue;
                        }

                        //Console.Log("|oRecallFighters: _offgridFighters All fighters are recalling");
                        return false;
                    }

                    //Console.Log("|oRecallFighters: ... _offgridFighters.Any(fighterId => FighterReady(fighterId)))");
                    //if (!waitForFightersToReturn) return true;
                }

                if (waitForFightersToReturn && (fightersToRecall.Any() || OffgridFighters.Any()))
                {
                    return false;
                }

                return true;
                //Console.Log("|oRecallFighters: We have no fighters to recall at the moment.");
            }
            catch (Exception ex)
            {
                Console.Log("Exception [" + ex + "]");
                return true;
            }
        }

        public bool CollectSentriesInSpace(List<Drone> recallTheseSentryDrones, string reason = "|oRecalling Sentry Drones: CollectSentriesInSpace")
        {
            if (QMCache.Instance.MyShipEntity != null && QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping) return true;

            IEnumerable<Drone> sentriesInSpace = recallTheseSentryDrones.Where(a => a.State == EntityState.Incapacitated || DroneType.All.Any(b => b.ID == a.ID && b.Group == "Sentry Drones")).ToList();
            if (sentriesInSpace.Any())
            {
                Console.Log(reason);
                foreach (Drone sentryInSpace in sentriesInSpace)
                {
                    if (sentryInSpace.ToEntity.Distance < 2400)
                    {
                        Console.Log("|oScooping Sentry [" + sentryInSpace.Name + "][" + Math.Round(sentryInSpace.ToEntity.Distance, 0) + "m]");
                        sentryInSpace.ReturnToDroneBay();
                        DislodgeWaitFor(1);
                    }
                    else
                    {
                        Console.Log("|Approaching Sentry [" + sentryInSpace.Name + "][" + Math.Round(sentryInSpace.ToEntity.Distance, 0) + "m]");
                        sentryInSpace.ToEntity.Approach();
                    }

                    return false;
                }

                return false;
            }

            return true;
        }

        bool Recall(object[] Params)
        {
            ClearCachedDataEveryPulse();
            if (!RecallDronesAndFighters()) return false;
            return true;
        }

        public bool RecallDrones(List<Drone> recallTheseDrones, string reason = "|oRecalling drones: Recall()")
        {
            if (QMCache.Instance.InStation || !QMCache.Instance.InSpace) return true;

            if (recallTheseDrones == null) return true;

            // Recall sentry drones
            if (!CollectSentriesInSpace(recallTheseDrones, reason)) return false;

            try
            {
                // Recall drones
                List<Drone> recall = recallTheseDrones.Where(droneInSpace => DroneReady(droneInSpace.ID)).ToList();
                if (recall.Any())
                {
                    Console.Log(reason);
                    recall.ReturnToDroneBay();
                    recall.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(7)));
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.Log("Exception [" + ex + "]");
            }

            if (recallTheseDrones != null && recallTheseDrones.Any()) return false;

            return true;
        }

        public bool RecallDronesAndFighters(string reason = "|oRecalling drones and fighters: Recall()")
        {
            if (QMCache.Instance.InStation) return true;

            //
            // Recall drones
            //
            if (!RecallDrones(DronesInSpace.ToList())) return false;

            //
            // Recall fighters
            //
            if (AvailableFighters.Any())
            {
                bool RecallingFightersFinished = RecallFighters(AvailableFighters, reason, false, true);
                bool SpeedingUpFightersFinsihed = SpeedUpFighters(ReturningFighters, "Burning Back");
                if (!RecallingFightersFinished || !SpeedingUpFightersFinsihed)
                {
                    return false;
                }
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
                if (QMCache.Instance.Entities.All(b => b.GroupId != (int)Group.ForceField))
                {
                    if (fightersThatNeedPropModOn != null && fightersThatNeedPropModOn.Any(a =>
                                a.ToEntity != null && a.ToEntity.Velocity.Magnitude < 3000 && a.HasPropmod() &&
                                a.Slot2 != null && a.Slot2.AllowsActivate && a.ToEntity.Distance > 15000 &&
                                a.State != Fighters.States.LANDING && a.State != Fighters.States.LAUNCHING))
                    {
                        fightersThatNeedPropModOn = fightersThatNeedPropModOn.ToList();
                        IEnumerable<Fighters.Fighter> availableFightersThatNeedPropModOn = fightersThatNeedPropModOn.Where(a =>
                                    FighterReady(a.ID) && a.HasPropmod() && a.Slot2 != null &&
                                    a.Slot2.AllowsActivate && a.ToEntity.Distance > 15000 &&
                                    a.State != Fighters.States.LANDING && a.State != Fighters.States.LAUNCHING).ToList();
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

        private bool UnlockTargetsAsNeeded()
        {
            if (QMCache.Instance.TotalTargetsandTargeting.Count() >= QMCache.Instance.MaxLockedTargets)
            {
                if (QMCache.Instance.MyShipEntity != null && QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping) return false;
                if (QMCache.Instance.Targets.Any())
                {
                    EntityCache unlockthis = QMCache.Instance.Targets.OrderBy(i => i.IsNpc)
                            .ThenBy(i => i.Velocity)
                            .ThenByDescending(i => i.Distance)
                            .FirstOrDefault();
                    if (unlockthis != null)
                    {
                        Console.Log("|oUnLocking [" + unlockthis.Name + "][" + Math.Round(unlockthis.Distance / 1000, 0) + "k|o] to free up a targeting slot");
                        unlockthis.UnlockTarget("UnlockTargetsAsNeeded");
                        return true;
                    }
                }

                return true;
            }

            return true;
        }

        //Vector3 LastTargetLocation = Vector3.origin;

        private bool LockManagement()
        {
            EntityCache entityToUseForClosestNpcMeasurement = null;
            if (AvailableFighters != null && AvailableFighters.Any())
            {
                entityToUseForClosestNpcMeasurement = AvailableFighters.FirstOrDefault().ToEntity;
            }

            if (entityToUseForClosestNpcMeasurement == null && QMCache.Instance.MyShipEntity != null)
            {
                entityToUseForClosestNpcMeasurement = QMCache.Instance.MyShipEntity;
            }

            TargetCooldown = TargetCooldown.Where(a => a.Value >= DateTime.Now).ToDictionary(a => a.Key, a => a.Value);
            _rats.LockedAndLockingTargetList.ForEach(a => { TargetCooldown.AddOrUpdate(a.Id, DateTime.Now.AddSeconds(Config.TargetCooldown)); });

            if (Cache.Instance.HostilePilot != null)
            {
                //Console.Log("|Found HostilePilot");
                if (!Cache.Instance.HostilePilot.LockedTarget &&
                    !Cache.Instance.HostilePilot.LockingTarget &&
                    Cache.Instance.HostilePilot.Exists &&
                    !Cache.Instance.HostilePilot.HasReleased &&
                    !Cache.Instance.HostilePilot.HasExploded &&
                    Cache.Instance.HostilePilot.Mode != EntityMode.Warping)
                {
                    if (!UnlockTargetsAsNeeded()) return false;
                    Console.Log("|oLocking HostilePilot [|-g" + Cache.Instance.HostilePilot.Name + "|o][|g" + MaskedId(Cache.Instance.HostilePilot.ID) + "|o][|g" + Math.Round(Cache.Instance.HostilePilot.Distance/1000, 0) + "k|o]");
                    Cache.Instance.HostilePilot.LockTarget();
                    return false;
                }
            }
            else if (Cache.Instance.AnchoredBubble != null && Config.AttackAnchoredBubble)
            {
                //Console.Log("|Found AnchoredBubble");
                if (!Cache.Instance.AnchoredBubble.IsTarget && !Cache.Instance.AnchoredBubble.IsTargeting && !Cache.Instance.AnchoredBubble.HasReleased && !Cache.Instance.AnchoredBubble.HasExploded)
                {
                    if (QMCache.Instance.TotalTargetsandTargetingCount >= QMCache.Instance.MaxLockedTargets)
                    {
                        if (QMCache.Instance.Targeting.Any())
                        {
                            Console.Log("|oUnLocking [" + _rats.LockedTargetList.First().Name + "][" + Math.Round(_rats.LockedTargetList.First().Distance / 1000, 0) + "k|o] to free up a targeting slot");
                            _rats.LockedTargetList.First().UnlockTarget("LockManagement: AnchoredBubble");
                        }
                        return false;
                    }

                    if (Cache.Instance.AnchoredBubble.LockTarget("LockManagement: AnchoredBubble"))
                    {
                        Console.Log("|oLocking AnchoredBubble [|-g" + Cache.Instance.AnchoredBubble.Name + "|o][|g" + MaskedId(Cache.Instance.AnchoredBubble.Id) + "|o][|g" + Math.Round(Cache.Instance.AnchoredBubble.Distance / 1000, 0) + "k|o]");
                    }
                    return false;
                }
            }
            else if (Cache.Instance.WarpScrambling != null)
            {
                //Console.Log("|Found WarpScrambling entity");
                if (!Cache.Instance.WarpScrambling.IsTarget && !Cache.Instance.WarpScrambling.IsTargeting && !Cache.Instance.WarpScrambling.HasReleased && !Cache.Instance.WarpScrambling.HasExploded && Cache.Instance.WarpScrambling.EntityMode != EntityMode.Warping)
                {
                    if (!UnlockTargetsAsNeeded()) return false;
                    Console.Log("|oLocking WarpScrambling [|-g" + Cache.Instance.WarpScrambling.Name + "|o][|g" + MaskedId(Cache.Instance.WarpScrambling.Id) + "|o][|g" + Math.Round(Cache.Instance.WarpScrambling.Distance/1000, 0) + "k|o]");
                    Cache.Instance.WarpScrambling.LockTarget("LockManagement: WarpScrambling");
                    return false;
                }
            }
            else if (Cache.Instance.lcoToBlowUp != null)
            {
                //Console.Log("|Found lcoToBlowUp entity");
                if (!Cache.Instance.lcoToBlowUp.IsTarget && !Cache.Instance.lcoToBlowUp.IsTargeting && !Cache.Instance.lcoToBlowUp.HasReleased && !Cache.Instance.lcoToBlowUp.HasExploded)
                {
                    if (!UnlockTargetsAsNeeded()) return false;
                    Console.Log("|oLocking lcoToBlowUp [|-g" + Cache.Instance.lcoToBlowUp.Name + "|o][|g" + MaskedId(Cache.Instance.lcoToBlowUp.Id) + "|o][|g" + Math.Round(Cache.Instance.lcoToBlowUp.Distance/1000, 0) + "k|o]");
                    Cache.Instance.lcoToBlowUp.LockTarget("LockManagement: lcoToBlowUp");
                    return false;
                }
            }
            else if (Cache.Instance.Neuting != null)
            {
                //Console.Log("|Found Neuting entity");
                if (!Cache.Instance.Neuting.IsTarget && !Cache.Instance.Neuting.IsTargeting && !Cache.Instance.Neuting.HasReleased && !Cache.Instance.Neuting.HasExploded && Cache.Instance.Neuting.EntityMode != EntityMode.Warping)
                {
                    if (!UnlockTargetsAsNeeded()) return false;
                    Console.Log("|oLocking Neuts [|-g" + Cache.Instance.Neuting.Name + "|o][|g" + MaskedId(Cache.Instance.Neuting.Id) + "|o][|g" + Math.Round(Cache.Instance.Neuting.Distance/1000, 0) + "k|o]");
                    Cache.Instance.Neuting.LockTarget("LockManagement: Neuts");
                    return false;
                }
            }
            else if (ActiveTarget != null && ActiveTarget.IsValid && !ActiveTarget.HasExploded && !ActiveTarget.HasReleased && ActiveTarget.EntityMode != EntityMode.Warping) //&& Config.PriorityTargets.Contains(ActiveTarget.Name)
            {
                //Console.Log("|oActiveTarget is not null");
                if (!ActiveTarget.IsTarget && !ActiveTarget.IsTargeting)
                {
                    if (!UnlockTargetsAsNeeded()) return false;
                    Console.Log("|oLocking ActiveTarget [|-g" + ActiveTarget.Name + "|o][|g" + MaskedId(ActiveTarget.Id) + "|o][|g" + Math.Round(ActiveTarget.Distance/1000, 0) + "k|o]");
                    ActiveTarget.LockTarget("LockManagement: ActiveTarget");
                    return false;
                }
            }

            if (ActiveTarget != null && (ActiveTarget.HasExploded || ActiveTarget.HasReleased || !ActiveTarget.IsValid))
            {
                ActiveTarget = null;
            }

            if (_rats.LockedAndLockingTargetList.Count < Config.TargetSlots)
            {
                int freeTargetSlots = Config.TargetSlots - _rats.LockedAndLockingTargetList.Count;
                //Console.Log("|oActiveTarget is empty; picking a NewTarget");
                IEnumerable<EntityCache> newTargets = _rats.UnlockedTargetList.OrderByDescending(i => PriorityTargets.Any() && PriorityTargets.Contains(i.Name))
                        .ThenBy(i => i.DistanceFromEntity(entityToUseForClosestNpcMeasurement))
                        .Where(a => a.IsValid && !a.HasExploded && !a.HasReleased && !TargetCooldown.ContainsKey(a.Id) && a.Distance < QMCache.Instance.ActiveShip.MaxTargetRange && a.Mode != 3)
                        .ToList();
                if (newTargets.Any() && QMCache.Instance.Entities.FirstOrDefault(a => a.IsJammingMe && a.IsTargetedBy) == null)
                {
                    foreach (EntityCache newTarget in newTargets)
                    {
                        freeTargetSlots--;
                        Console.Log("|oLocking [|-g" + newTarget.Name + "|o][|g" + MaskedId(newTarget.Id) + "|o][|g" + Math.Round(newTarget.Distance / 1000, 0) + "k|o]");
                        TargetCooldown.AddOrUpdate(newTarget.Id, DateTime.Now.AddSeconds(Config.TargetCooldown));
                        newTarget.LockTarget("LockManagement: NewTarget");
                        OutOfTargets = false;
                        if (freeTargetSlots == 0) return true;
                        if (Config.LockTargetsOneAtaTime) return true;
                        continue;
                    }

                    return false;
                }
                //else if (ActiveTarget != null && !ActiveTarget.LockedTarget && !ActiveTarget.LockingTarget)
                //{
                //    //Console.Log("|oif (ActiveTarget != null && !ActiveTarget.LockedTarget && !ActiveTarget.LockingTarget)");
                //    ActiveTarget = null;
                //    return false;
                //}
            }

            OutOfTargets = true;
            return true;
        }

        bool ChooseActiveTarget()
        {
            EntityCache entityToUseForClosestNpcMeasurement = null;
            if (AvailableFighters.Any())
            {
                entityToUseForClosestNpcMeasurement = AvailableFighters.FirstOrDefault().ToEntity;
            }

            if (entityToUseForClosestNpcMeasurement == null)
            {
                entityToUseForClosestNpcMeasurement = QMCache.Instance.MyShipEntity;
            }

            if (Cache.Instance.HostilePilot != null)
            {
                if (ActiveTarget != Cache.Instance.HostilePilot && Cache.Instance.HostilePilot.Distance < MaxRange)
                {
                    Console.Log("|rHostile Pilot on Grid! |o[|g" + Cache.Instance.HostilePilot.Name + "|o][|g" + Math.Round(Cache.Instance.HostilePilot.Distance/1000, 0) + "k|o][" + Cache.Instance.HostilePilot.Type + "]");
                    ActiveTarget = Cache.Instance.HostilePilot;
                    return true;
                }
            }
            else if (Cache.Instance.AnchoredBubble != null)
            {
                if (ActiveTarget != Cache.Instance.AnchoredBubble && Cache.Instance.AnchoredBubble.Distance < MaxRange)
                {
                    Console.Log("|rAnchored bubble on grid! Attacking it so that we dont keep getting stuck. |o[|g" + Cache.Instance.AnchoredBubble.Name + "|o][|g" + Math.Round(Cache.Instance.AnchoredBubble.Distance, 0) + "k|o]");
                    ActiveTarget = Cache.Instance.AnchoredBubble;
                    return true;
                }
            }
            else if (Cache.Instance.WarpScrambling != null)
            {
                if (ActiveTarget != Cache.Instance.WarpScrambling && Cache.Instance.WarpScrambling.Distance < MaxRange)
                {
                    Console.Log("|rEntity on grid is/was warp scrambling! |o[|g" + Cache.Instance.WarpScrambling.Name + "|o][|g" + Math.Round(Cache.Instance.WarpScrambling.Distance/1000, 0) + "k|o]");
                    ActiveTarget = Cache.Instance.WarpScrambling;
                    return true;
                }
            }
            else if (Cache.Instance.lcoToBlowUp != null)
            {
                if (ActiveTarget != Cache.Instance.lcoToBlowUp && Cache.Instance.lcoToBlowUp.Distance < MaxRange)
                {
                    Console.Log("|rLCO on grid is/was keeping us from safely warping off! |o[|g" + Cache.Instance.lcoToBlowUp.Name + "|o][|g" + Math.Round(Cache.Instance.lcoToBlowUp.Distance/1000, 0) + "k|o]");
                    ActiveTarget = Cache.Instance.lcoToBlowUp;
                    return true;
                }
            }
            else if (Cache.Instance.Neuting != null)
            {
                if (ActiveTarget != Cache.Instance.Neuting && Cache.Instance.Neuting.Distance < MaxRange)
                {
                    Console.Log("|rEntity on grid is/was neuting! |o[|g" + Cache.Instance.Neuting.Name + "|o][|g" + Math.Round(Cache.Instance.Neuting.Distance/1000, 0) + "k|o]");
                    ActiveTarget = Cache.Instance.Neuting;
                    return true;
                }
            }

            if (ActiveTarget == null || !ActiveTarget.IsValid || ActiveTarget.HasExploded || ActiveTarget.HasReleased)
            {
                try
                {
                    List<EntityCache> availableTargets;
                    //if (LastTargetLocation != Vector3.origin) AvailableTargets = AvailableTargets.OrderBy(a => a.DistanceFromEntity(LastTargetLocation)).ToList();
                    ActiveTarget = null;
                    try
                    {
                        availableTargets = _rats.LockedAndLockingTargetList.OrderBy(i => i.DistanceFromEntity(entityToUseForClosestNpcMeasurement)).ToList();
                    }
                    catch (Exception)
                    {
                        availableTargets = new List<EntityCache>();
                    }

                    if (availableTargets.Any())
                    {
                        ActiveTarget = availableTargets.FirstOrDefault(a => PriorityTargets.Contains(a.Name) && !a.HasExploded && !a.HasReleased && (a.LockedTarget || a.LockingTarget) && !Triggers.Contains(a.Name) && a.Distance < MaxRange);
                        //if (LastTargetLocation != Vector3.origin) AvailableTargets = AvailableTargets.OrderBy(a => a.DistanceFromEntity(LastTargetLocation)).ToList();
                    }
                    else ActiveTarget = null;

                    if (availableTargets.Any() && ActiveTarget == null)
                    {
                        if (Config.PrivateTargets)
                        {
                            if (Config.SharedTargets)
                            {
                                if (entityToUseForClosestNpcMeasurement != null)
                                {
                                    //ActiveTarget = AvailableTargets.FirstOrDefault(a => _ipc.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                                    ActiveTarget = availableTargets.Where(a => !Triggers.Contains(a.Name)).OrderBy(i => i.DistanceFromEntity(entityToUseForClosestNpcMeasurement)).FirstOrDefault(a => _ipc.ActiveTargets.ContainsValue(a.Id) && a.Distance < MaxRange);
                                    if (ActiveTarget == null) ActiveTarget = availableTargets.OrderBy(i => i.DistanceFromEntity(entityToUseForClosestNpcMeasurement)).FirstOrDefault(a => _ipc.ActiveTargets.ContainsValue(a.Id) && a.Distance < MaxRange);
                                }
                            }
                            else
                            {
                                if (entityToUseForClosestNpcMeasurement != null)
                                {
                                    //ActiveTarget = AvailableTargets.FirstOrDefault(a => !_ipc.ActiveTargets.ContainsValue(a.ID) && a.Distance < MaxRange);
                                    ActiveTarget = _rats.LockedTargetList.Where(a => !Triggers.Contains(a.Name)).OrderBy(i => i.DistanceFromEntity(entityToUseForClosestNpcMeasurement)).FirstOrDefault(a => !_ipc.ActiveTargets.ContainsValue(a.Id) && a.Distance < MaxRange);
                                    if (ActiveTarget == null) ActiveTarget = _rats.LockedTargetList.OrderBy(i => i.DistanceFromEntity(entityToUseForClosestNpcMeasurement)).FirstOrDefault(a => !_ipc.ActiveTargets.ContainsValue(a.Id) && a.Distance < MaxRange);
                                }
                            }
                        }
                        if (ActiveTarget == null && OutOfTargets)
                        {
                            ActiveTarget = availableTargets.Where(a => !Triggers.Contains(a.Name)).FirstOrDefault(a => a.Distance < MaxRange && !a.HasExploded && !a.HasReleased);
                            if (ActiveTarget == null) ActiveTarget = availableTargets.FirstOrDefault(a => a.Distance < MaxRange && !a.HasExploded && !a.HasReleased);
                        }
                        if (ActiveTarget != null)
                        {
                            //_ipc.Relay(Me.CharID, ActiveTarget.Id);
                        }

                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.Log("Exception [" + ex + "]");
                }

                return false;
            }

            //if (ActiveTarget != null && ActiveTarget.Exists) LastTargetLocation = ActiveTarget.Position;
            //int intAttribute = 0;
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
            ClearCachedDataEveryPulse();

            if (!QMCache.Instance.InSpace || (QMCache.Instance.MyShipEntity != null && QMCache.Instance.MyShipEntity.Velocity > 8000))
            {
                return false;
            }

            if (Config.Mode == Mode.None && !Fighters.Tubes.Any())
            {
                return false;
            }

            // If we're warping and drones are in space, recall them and stop the module
            if (QMCache.Instance.MyShipEntity != null && QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping && QMCache.Instance.MyShipEntity.Velocity < 2000 && DronesInSpace.Any(droneInSpace => DroneReady(droneInSpace.ID)))
            {
                RecallDrones(DronesInSpace.ToList(), "We are warping: pull drones!");
                return true;
            }

            // If we're in a POS and fighters are in space, queue delayed stop of the module
            if (QMCache.Instance.Entities.Any(a => a.GroupId == (int)Group.ForceField) && Fighters.Active.Any())
            {
                QueueState(FighterShutdown, 10000);
                return true;
            }

            if (MyShip.DronesToReconnect && Drones.DroneBay.UsedCapacity < Drones.DroneBay.Capacity && QMCache.Instance.MyShipEntity != null && QMCache.Instance.MyShipEntity.GroupId != (int)Group.Capsule && TryReconnect)
            {
                MyShip.ReconnectToDrones();
                DislodgeWaitFor(2);
                TryReconnect = false;
                return false;
            }

            if (QMCache.Instance.MyShipEntity != null && QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping) return false;

            try
            {
                if (OffgridFighters.Any(i => FighterReady(i)))
                {
                    IEnumerable<Fighters.Fighter> offGridFightersToRecall = Fighters.Active.Where(a => OffgridFighters.Contains(a.ID));
                    RecallFighters(offGridFightersToRecall, "offgrid fighters");
                }
            }
            catch (Exception){}


            if (!_rats.TargetList.Any() && !QMCache.Instance.Entities.Any(a => PriorityTargets.Contains(a.Name)) && !Config.StayDeployedWithNoTargets)
            {
                // Recall drones and fighters
                if (!RecallDronesAndFighters("|oRecalling drones and fighters [|gNo rats available|o]")) return false;
            }

            //
            // Speed up Fighters
            //
            if (ReturningFighters.Any()) if (!SpeedUpFighters(ReturningFighters, "Burning Back.")) return false;

            if (Config.Mode == Mode.AfkHeavy && (_rats.TargetList.Any() || QMCache.Instance.Entities.Any(a => PriorityTargets.Contains(a.Name))))
            {
                try
                {
                    List<Drone> Deploy = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones")).Take(AvailableDroneSlots).ToList();
                    // Launch drones
                    if (Deploy.Any())
                    {
                        Console.Log("|oLaunching [|g" + Deploy.Count + "|o] heavy drones.");
                        Deploy.Launch();
                        Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(5)));
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Console.Log("Exception [" + ex + "]");
                }
            }

            if (Config.Mode == Mode.AfkSalvage)
            {
                List<Drone> Deploy = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Salvage Drone")).Take(AvailableDroneSlots).ToList();
                // Launch drones
                if (Deploy.Any())
                {
                    Console.Log("|oLaunching [|g" + Deploy.Count + "|o] salvage drones");
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

            try
            {
                List<Drone> recallDamaged = Drone.AllInSpace.Where(a => _droneCooldown.Contains(a) && DroneReady(a.ID)).ToList();
                if (recallDamaged.Any())
                {
                    RecallDrones(recallDamaged, "|oRecalling [|g" + recallDamaged.Count + "|o] Damaged Drones");
                }
            }
            catch (Exception ex)
            {
                Console.Log("Exception [" + ex + "]");
            }

            try
            {
                List<Fighters.Fighter> damagedFighters = AvailableFighters.Where(a => a.Health != null && a.Health < Config.FighterCriticalHealthLevel).ToList();
                if (damagedFighters.Any())
                {
                    bool RecallingFightersFinished = RecallFighters(damagedFighters, "Low Health", false, true);
                    bool SpeedingUpFightersFinsihed = SpeedUpFighters(damagedFighters.Where(i => i.State == Fighters.States.RECALLING), "Recalling");
                    if (!RecallingFightersFinished || !SpeedingUpFightersFinsihed)
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Log("Exception [" + ex + "]");
            }

            #region LockManagement
            //Console.Log("if (!LockManagement()) return false;");
            if (!LockManagement()) return false;
            #endregion

            #region ActiveTarget selection
            //Console.Log("if (!ChooseActiveTarget()) return false;");
            if (!ChooseActiveTarget()) return false;
            #endregion

            // Make sure ActiveTarget is locked.  If so, make sure it's the active target, if not, return.
            if (ActiveTarget != null && ActiveTarget.Exists && ActiveTarget.LockedTarget)
            {
                if (!ActiveTarget.IsActiveTarget && !ActiveTarget.HasExploded && !ActiveTarget.HasReleased)
                {
                    ActiveTarget.MakeActiveTarget();
                    return false;
                }
            }
            else
            {
                if (ActiveTarget == null)
                {
                    //
                    // Recall Drones / Fighters if there is no ActiveTarget and Config.StayDeployedWithNoTargets is false
                    //
                    if (!Config.StayDeployedWithNoTargets)
                    {
                        List<Drone> Recall = Drone.AllInSpace.Where(a => DroneReady(a.ID)).ToList();
                        if (Recall.Any())
                        {
                            if (!RecallDrones(Recall, "|oRecalling drones: No ActiveTarget")) return false;
                        }

                        if (AvailableFighters.Any())
                        {
                            bool RecallingFightersFinished = RecallFighters(AvailableFighters, "No ActiveTarget", false, true);
                            bool SpeedingUpFightersFinsihed = SpeedUpFighters(ReturningFighters, "Burning Back");
                            if (!RecallingFightersFinished || !SpeedingUpFightersFinsihed)
                            {
                                return false;
                            }
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
                        if (!RecallDrones(Recall, "|oRecalling non scout drones")) return false;
                    }
                    // Send drones to attack
                    List<Drone> Attack = Drone.AllInSpace.Where(a => DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                    if (Attack.Any())
                    {
                        Console.Log("|oSending scout drones to attack [|g" + ActiveTarget.Name + "|o][|g" + Math.Round(ActiveTarget.Distance / 1000, 0) + "|ok]");
                        Attack.Attack();
                        Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                        return false;
                    }
                    List<Drone> Deploy = Drone.AllInBay.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")).Take(AvailableDroneSlots).ToList();
                    List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")).Take(AvailableDroneSlots).ToList();
                    // Launch drones
                    if (Deploy.Any() && _rats.LockedAndLockingTargetList.Any())
                    {
                        Console.Log("|oLaunching [|g" + Deploy.Count + "|o] scout drones");
                        Deploy.Launch();
                        Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                        return false;
                    }
                    else if (AvailableDroneSlots > 0 && DeployIgnoreCooldown.Any())
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
                        if (!RecallDrones(Recall, "|oRecalling drones: no frigs/destroyers in range")) return false;
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
                    if (!RecallDrones(Recall, "|oRecalling non scout drones")) return false;
                }
                // Send drones to attack
                List<Drone> Attack = Drone.AllInSpace.Where(a => DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                if (Attack.Any())
                {
                    Console.Log("|oSending scout drones to attack [|g" + ActiveTarget.Name + "|o][|g" + Math.Round(ActiveTarget.Distance / 1000, 0) + "|ok]");
                    Attack.Attack();
                    Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                List<Drone> Deploy = Drone.AllInBay.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")).Take(AvailableDroneSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Light Scout Drones")).Take(AvailableDroneSlots).ToList();
                // Launch drones
                if (Deploy.Any() && _rats.LockedAndLockingTargetList.Any())
                {
                    Console.Log("|oLaunching [|g" + Deploy.Count + "|o] scout drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (AvailableDroneSlots > 0 && DeployIgnoreCooldown.Any())
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
                    if (!RecallDrones(Recall, "|oRecalling non medium drones")) return false;
                }
                // Send drones to attack
                List<Drone> Attack = Drone.AllInSpace.Where(a => DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Medium Scout Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                if (Attack.Any())
                {
                    Console.Log("|oSending medium drones to attack [|g" + ActiveTarget.Name + "|o][|g" + Math.Round(ActiveTarget.Distance / 1000, 0) + "|ok]");
                    Attack.Attack();
                    Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                List<Drone> Deploy = Drone.AllInBay.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Medium Scout Drones")).Take(AvailableDroneSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Medium Scout Drones")).Take(AvailableDroneSlots).ToList();
                // Launch drones
                if (Deploy.Any() && _rats.LockedAndLockingTargetList.Any())
                {
                    Console.Log("|oLaunching [|g" + Deploy.Count + "|o] medium drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (AvailableDroneSlots > 0 && DeployIgnoreCooldown.Any())
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
                    if (!RecallDrones(Recall, "|oRecalling non heavy drones")) return false;
                }
                // Send drones to attack
                List<Drone> Attack = Drone.AllInSpace.Where(a => DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                if (Attack.Any())
                {
                    Console.Log("|oSending heavy drones to attack [|g" + ActiveTarget.Name + "|o][|g" + Math.Round(ActiveTarget.Distance / 1000, 0) + "|ok]");
                    Attack.Attack();
                    Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                List<Drone> Deploy = Drone.AllInBay.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones")).Take(AvailableDroneSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Heavy Attack Drones")).Take(AvailableDroneSlots).ToList();
                // Launch drones
                if (Deploy.Any() && _rats.LockedAndLockingTargetList.Any())
                {
                    Console.Log("|oLaunching [|g" + Deploy.Count + "|o] heavy drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (AvailableDroneSlots > 0 && DeployIgnoreCooldown.Any())
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
                    if (!RecallDrones(Recall, "|oRecalling non sentry drones")) return false;
                }
                // Send drones to attack
                List<Drone> Attack = Drone.AllInSpace.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                if (Attack.Any())
                {
                    Console.Log("|oSending sentry drones to attack [|g" + ActiveTarget.Name + "|o][|g" + Math.Round(ActiveTarget.Distance / 1000, 0) + "|ok]");
                    Attack.Attack();
                    Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                List<Drone> Deploy = Drone.AllInBay.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(AvailableDroneSlots).ToList();
                List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(AvailableDroneSlots).ToList();
                // Launch drones
                if (Deploy.Any() && _rats.LockedAndLockingTargetList.Any())
                {
                    Console.Log("|oLaunching [|g" + Deploy.Count + "|o] sentry drones");
                    Deploy.Launch();
                    Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                    return false;
                }
                else if (AvailableDroneSlots > 0 && DeployIgnoreCooldown.Any())
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
                        if (!RecallDrones(Recall, "|oRecalling non-sentry drones")) return false;
                    }
                    List<Drone> Attack = Drone.AllInSpace.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones") && (a.State != EntityState.Combat || a.Target == null || a.Target != ActiveTarget)).ToList();
                    // Send drones to attack
                    if (Attack.Any())
                    {
                        Console.Log("|oOrdering sentry drones to attack [|g" + ActiveTarget.Name + "|o][|g" + Math.Round(ActiveTarget.Distance / 1000, 0) + "|ok]");
                        Attack.Attack();
                        Attack.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                        return false;
                    }
                    List<Drone> Deploy = Drone.AllInBay.Where(a => !_droneCooldown.Contains(a) && DroneReady(a.ID) && Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(AvailableDroneSlots).ToList();
                    List<Drone> DeployIgnoreCooldown = Drone.AllInBay.Where(a => Data.DroneType.All.Any(b => b.ID == a.TypeID && b.Group == "Sentry Drones")).Take(AvailableDroneSlots).ToList();
                    // Launch drones
                    if (Deploy.Any() && _rats.LockedAndLockingTargetList.Any())
                    {
                        Console.Log("|oLaunching [|g" + Deploy.Count + "|o] sentry drones");
                        Deploy.Launch();
                        Deploy.ForEach(a => NextDroneCommand.AddOrUpdate(a.ID, DateTime.Now.AddSeconds(3)));
                        return false;
                    }
                    else if (AvailableDroneSlots > 0 && DeployIgnoreCooldown.Any())
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
                        IEnumerable<Fighters.Tube> rearmFightersInTube = Fighters.Tubes.Where(a => !a.InSpace && a.Fighter.State == Fighters.States.READY && a.Fighter.SquadronSize < Config.FighterSquadronMaxSize);
                        if (rearmFightersInTube.Any())
                        {
                            Console.Log("|oMissing Fighters in a squadron");
                            if (Fighters.Bay.Items.Any(a => a.TypeID == rearmFightersInTube.FirstOrDefault().Fighter.TypeID))
                            {
                                VisualStyleElement.Header.Item fighterItemToReload = Fighters.Bay.Items.FirstOrDefault(a => a.TypeID == rearmFightersInTube.FirstOrDefault().Fighter.TypeID);
                                if (fighterItemToReload != null)
                                {
                                    Console.Log("|oLoading [|g" + fighterItemToReload.Name + "|o] into the Tube");
                                    rearmFightersInTube.FirstOrDefault().LoadFightersToTube(fighterItemToReload);
                                    return false;
                                }
                            }

                            Console.Log("|oWe are missing fighters in a squadron and have none in the drone bay to add to the squadron: panic");
                            if (!RecallFighters(AvailableFighters, "Low On Fighters: panicking", false, true)) return false;
                            Cache.Instance._securityCore.Panic();
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
                        IEnumerable<Fighters.Fighter> fightersThatNeedToActivatePropmod = AvailableFighters.Where(a => a.HasPropmod() && (a.ToEntity.DistanceFromEntity(ActiveTarget) > (a.OptimalRange() + a.FalloffRange() + 10000) || ActiveTarget.Velocity > 1500) && a.Slot2 != null && a.Slot2.AllowsActivate).ToList();
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
                        if (Config.RefillRockets || QMCache.Instance.Entities.Any(a => Data.NPCClasses.All.Any(b => (int)b.Key == (int)a.GroupId && b.Value == "Capital")))
                        {
                            if (AvailableFighters.Any(i => _fighterRocketSalvosLeft != null && _fighterRocketSalvosLeft.ContainsKey(i.ID) && (_fighterRocketSalvosLeft[i.ID] <= 0)))
                            {
                                IEnumerable<Fighters.Fighter> fightersThatNeedToRefillRockets = Fighters.Active.Where(i => FighterReady(i.ID) && _fighterRocketSalvosLeft != null && _fighterRocketSalvosLeft.ContainsKey(i.ID) && (_fighterRocketSalvosLeft[i.ID] <= 0)).ToList();
                                if (fightersThatNeedToRefillRockets.Any())
                                {
                                    bool RecallingFightersFinished = RecallFighters(AvailableFighters, "Refill Rockets", false, true);
                                    bool SpeedingUpFightersFinsihed = SpeedUpFighters(fightersThatNeedToRefillRockets.Where(i => i.State == Fighters.States.RECALLING), "returning");
                                    if (!RecallingFightersFinished || !SpeedingUpFightersFinsihed)
                                    {
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

                    try
                    {
                        // Use missile, given they have the ability to
                        IEnumerable<Fighters.Fighter> fightersReadyToMissileAttack = AvailableFighters.Where(a => a.HasMissiles() && a.Slot3 != null && a.Slot3.AllowsActivate).ToList();
                        if (fightersReadyToMissileAttack.Any())
                        {
                            bool slightPauseNeededAfterMissileAttack = false;
                            foreach (Fighters.Fighter fighterReadyToMissileAttack in fightersReadyToMissileAttack)
                            {
                                EntityCache rocketTargetEntity = _rats.LockedTargetList.FirstOrDefault(a => Cache.Instance.HostilePilot != null && Cache.Instance.HostilePilot.ID == a.Id && fighterReadyToMissileAttack.ToEntity.DistanceFromEntity(a) < fighterReadyToMissileAttack.MissileOptimalRange() - 2000);
                                if (rocketTargetEntity == null) rocketTargetEntity = _rats.LockedTargetList.FirstOrDefault(a => Cache.Instance.WarpScrambling != null && Cache.Instance.WarpScrambling.Id == a.Id && fighterReadyToMissileAttack.ToEntity.DistanceFromEntity(a) < fighterReadyToMissileAttack.MissileOptimalRange() - 2000);
                                if (rocketTargetEntity == null) rocketTargetEntity = _rats.LockedTargetList.OrderByDescending(FighterMissileTarget).FirstOrDefault(a => !Triggers.Contains(a.Name) && a.ArmorPct > 40 &&  (FighterMissileTarget(a) || (Config.UseFighterMissileAttackOnActiveTarget && a == ActiveTarget && !NPCFrigate(a))) && fighterReadyToMissileAttack.ToEntity.DistanceFromEntity(a) < fighterReadyToMissileAttack.MissileOptimalRange() - 2000);
                                if (rocketTargetEntity == null) rocketTargetEntity = _rats.LockedTargetList.OrderByDescending(FighterMissileTarget).FirstOrDefault(a => a.ArmorPct > 40 && (FighterMissileTarget(a) || (Config.UseFighterMissileAttackOnActiveTarget && a == ActiveTarget && !NPCFrigate(a))) && fighterReadyToMissileAttack.ToEntity.DistanceFromEntity(a) < fighterReadyToMissileAttack.MissileOptimalRange() - 2000);
                                if (rocketTargetEntity != null)
                                {
                                    int missilesAlreadyShotAtThisEntity = 0;
                                    if (_missileEntityTracking!= null && _missileEntityTracking.Any() && _missileEntityTracking.ContainsKey(rocketTargetEntity.ID))
                                    {
                                        missilesAlreadyShotAtThisEntity = (int)_missileEntityTracking[rocketTargetEntity.Id];
                                    }
                                    if (missilesAlreadyShotAtThisEntity <= 1)
                                    {
                                        Console.Log("|oFighter [|g" + MaskedId(fighterReadyToMissileAttack.ID) + "|o] [3]Rocket [range" + Math.Round(fighterReadyToMissileAttack.MissileOptimalRange() - 2000 / 1000, 0) + "k][|g" + rocketTargetEntity.Name + "|o][|g" + MaskedId(rocketTargetEntity.Id) + "|o][|g" + Math.Round((double)rocketTargetEntity.DistanceFromEntity(fighterReadyToMissileAttack.ToEntity) / 1000, 0) + "k|o] FighterToTarget");
                                        fighterReadyToMissileAttack.Slot3.ActivateOnTarget(rocketTargetEntity);
                                        _fighterRocketSalvosLeft.AddOrUpdate(fighterReadyToMissileAttack.ID, _fighterRocketSalvosLeft[fighterReadyToMissileAttack.ID] - 1);
                                        missilesAlreadyShotAtThisEntity++;
                                        _missileEntityTracking.AddOrUpdate(rocketTargetEntity.Id, missilesAlreadyShotAtThisEntity);
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
                        IEnumerable<Fighters.Fighter> fightersThatNeedAnAttackTarget = AvailableFighters.Where(a => a.Slot1 != null && a.Slot1.AllowsActivate && (double) a["maxTargetRange"] != 0).ToList();  //&& a.ToEntity.DistanceFromEntity(ActiveTarget) < (double)a["maxTargetRange"] - 10).ToList();
                        if (fightersThatNeedAnAttackTarget.Any() && ActiveTarget != null && ActiveTarget.Exists && !ActiveTarget.HasExploded && !ActiveTarget.HasReleased)
                        {
                            foreach (Fighters.Fighter fighterThatNeedsAnAttackTarget in fightersThatNeedAnAttackTarget)
                            {
                                Console.Log("|oFighter [|g" + MaskedId(fighterThatNeedsAnAttackTarget.ID) + "|o] [1]Attacking [|g" + ActiveTarget.Name + "|o][|g" + MaskedId(ActiveTarget.Id) + "|o][|g" + Math.Round(fighterThatNeedsAnAttackTarget.ToEntity.DistanceFromEntity(ActiveTarget) / 1000, 0) + "k|o] FighterToTarget"); //
                                fighterThatNeedsAnAttackTarget.Slot1.ActivateOnTarget(ActiveTarget);
                                if (Config.FightersOrbitAtOptimal)
                                {
                                    fighterThatNeedsAnAttackTarget.Orbit(ActiveTarget, (int)fighterThatNeedsAnAttackTarget.OptimalRange());
                                }

                                NextFighterCommand.AddOrUpdate(fighterThatNeedsAnAttackTarget.ID, DateTime.Now.AddSeconds(Math.Max(2, Math.Round(ActiveTarget.Distance/1000, 0) / 15)));
                                continue;
                            }

                            return false;
                        }

                        // Return fighters that are not moving (not responding to commands?)
                        IEnumerable<Fighters.Fighter> fightersThatShouldReturn = AvailableFighters.Where(a => (a.ToEntity != null && a.ToEntity.Velocity.Magnitude <= 0)).ToList();
                        if (fightersThatShouldReturn.Any() && !QMCache.Instance.Entities.Where(i => i.Distance < 100000).Any(i => i.LockingTarget))
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
            ClearCachedDataEveryPulse();
            bool RecallingFightersFinished = RecallFighters(Fighters.Active, "FighterShutdown", false, true);
            bool SpeedingUpFightersFinsihed = SpeedUpFighters(ReturningFighters, "returning");
            if (!RecallingFightersFinished || !SpeedingUpFightersFinsihed)
            {
                return false;
            }

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

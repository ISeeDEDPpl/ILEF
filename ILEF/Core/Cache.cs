#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using System.Threading;
using ILoveEVE.Framework;
using ILEF.Caching;
using ILEF.KanedaToolkit;
using ILEF.Lookup;
using ILEF.EVEInteration;

namespace ILEF.Core
{
    /// <summary>
    /// This class provides cached information useful for user interfaces
    /// </summary>
    public class Cache : State
    {
        #region Instantiation

        static Cache _Instance;
        public readonly Security.Security _securityCore = Security.Security.Instance;

        /// <summary>
        /// Singletoner
        /// </summary>
        public static Cache Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new Cache();
                }
                return _Instance;
            }
        }

        public ILoveEVE.Framework.DirectEve DirectEve { get; set; }

        private Cache()
        {
            //ItemVolume = new Dictionary<string, double>();
            ShipVolume = new Dictionary<string, double>();
            //CachedMissions = new Dictionary<string, CachedMission>();
            AvailableAgents = new List<string>();
            ShipNames = new HashSet<string>();
            //AllEntities = new List<EntityCache>();
            QueueState(Control, 400);
        }

        #endregion

        #region Variables

        /// <summary>
        /// Your pilot's Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Your pilot's CharID
        /// </summary>
        public long CharID { get; set; }

        /// <summary>
        /// Array of bookmark titles
        /// </summary>
        public string[] Bookmarks { get; set; }

        /// <summary>
        /// Array of bookmark titles
        /// </summary>
        public string[] CitadelBookmarks { get; set; }

        /// <summary>
        /// Array of fleet member names
        /// </summary>
        public string[] FleetMembers { get; set; }

        /// <summary>
        /// Item Volumes, keyed by Types
        /// </summary>
        public Dictionary<string, double> ItemVolume { get; set; }

        public Dictionary<string, double> ShipVolume { get; set; }
        public HashSet<string> ShipNames { get; set; }
        public List<string> Fittings { get; set; }
        public Double ArmorPercent = 1;
        public Double HullPercent = 1;
        public bool DamagedDrones = false;
        public List<string> AvailableAgents { get; set; }
        public List<ModuleCache> MyShipsModules = null;

        //public IEnumerable<EntityCache> AllEntities = new List<EntityCache>();

            /**
        private List<EntityCache> _hostilePilots = new List<EntityCache>();
        private EntityCache _hostilePilot = null;
        public EntityCache HostilePilot
        {
            get
            {
                try
                {
                    if (!DirectEve.Session.IsInSpace) return null;

                    if (_hostilePilot == null)
                    {
                        _hostilePilots = QMCache.Instance.Entities.Where(i => i.Distance < 60000 && i.CategoryId != (int)CategoryID.Charge && i.GroupId != (int)Group.CombatDrone && i.GroupId != (int)Group.FighterDrone && i.GroupId != (int)Group.FighterBomber && i.GroupId != (int)Group.Wreck).Where(a => Local.Pilots.Any(pilot => pilot.ID == a.OwnerID && pilot.Hostile())).ToList();
                        if (_hostilePilots.Any())
                        {
                                _hostilePilot = _hostilePilots.OrderByDescending(i => i.IsWarpScramblingMe)
                                    .ThenByDescending(i => i.GroupId == (int)Group.Interdictor)
                                    .ThenByDescending(i => i.GroupId == (int)Group.HeavyInterdictor)
                                    .ThenByDescending(i => i.GroupId == (int)Group.BlackOps)
                                    .ThenByDescending(i => i.GroupId == (int)Group.Battleship)
                                    .ThenByDescending(i => i.GroupId == (int)Group.Cruiser)
                                    .ThenByDescending(i => i.GroupId == (int)Group.HeavyAssaultShip)
                                    .ThenByDescending(i => i.GroupId == (int)Group.AttackBattlecruiser)
                                    .ThenByDescending(i => i.GroupId == (int)Group.CombatBattlecruiser)
                                    .FirstOrDefault();
                        }

                        if (_hostilePilot == null)
                        {
                                _hostilePilots = QMCache.Instance.Entities.Where(i => i.CategoryId != (int)CategoryID.Charge && i.GroupId != (int)Group.CombatDrone && i.GroupId != (int)Group.FighterDrone && i.GroupId != (int)Group.FighterBomber && i.GroupId != (int)Group.Wreck).Where(a => Local.Pilots.Any(pilot => pilot.ID == a.OwnerID && pilot.Hostile())).ToList();
                                if (_hostilePilots.Any())
                                {
                                    _hostilePilot = _hostilePilots.OrderByDescending(i => i.IsWarpScramblingMe)
                                        .ThenByDescending(i => i.GroupId == (int)Group.Interdictor)
                                        .ThenByDescending(i => i.GroupId == (int)Group.HeavyInterdictor)
                                        .ThenByDescending(i => i.GroupId == (int)Group.BlackOps)
                                        .ThenByDescending(i => i.GroupId == (int)Group.Battleship)
                                        .ThenByDescending(i => i.GroupId == (int)Group.Cruiser)
                                        .ThenByDescending(i => i.GroupId == (int)Group.HeavyAssaultShip)
                                        .ThenByDescending(i => i.GroupId == (int)Group.AttackBattlecruiser)
                                        .ThenByDescending(i => i.GroupId == (int)Group.CombatBattlecruiser)
                                        .FirstOrDefault();
                                }
                        }

                        return _hostilePilot ?? null;
                    }

                    return _hostilePilot ?? null;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
        **/
        private EntityCache _anchoredBubble = null;

        public EntityCache AnchoredBubble
        {
            get
            {
                try
                {
                    if (_anchoredBubble == null)
                    {
                        if (!DirectEve.Session.IsInSpace) return null;

                        _anchoredBubble = QMCache.Instance.Entities.Where(i => i.Distance < 240000).FirstOrDefault(a => a.GroupId == (int)Group.MobileWarpDisruptor && a.Distance < DirectEve.ActiveShip.MaxTargetRange);
                        return _anchoredBubble ?? null;
                    }

                    return _anchoredBubble ?? null;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        private EntityCache _warpScrambling = null;

        public EntityCache WarpScrambling
        {
            get
            {
                try
                {
                    if (_warpScrambling == null)
                    {
                        if (!DirectEve.Session.IsInSpace) return null;

                        _warpScrambling = _securityCore.ValidScramble;
                        return _warpScrambling ?? null;
                    }

                    return _warpScrambling ?? null;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        private EntityCache _neuting = null;

        public EntityCache Neuting
        {
            get
            {
                try
                {
                    if (_neuting == null)
                    {
                        if (!DirectEve.Session.IsInSpace) return null;

                        _neuting = _securityCore.ValidNeuter;
                        return _neuting ?? null;
                    }

                    return _neuting ?? null;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        private EntityCache _lcoToBlowUp = null;

        public EntityCache lcoToBlowUp
        {
            get
            {
                try
                {
                    if (_lcoToBlowUp == null)
                    {
                        if (!DirectEve.Session.IsInSpace) return null;
                        _lcoToBlowUp = QMCache.Instance.Entities.FirstOrDefault(a => (a.GroupId == (int)Group.LargeCollidableObject || a.GroupId == (int)Group.LargeCollidableStructure) && !a.Name.ToLower().Contains("rock") && !a.Name.ToLower().Contains("stone") && !a.Name.ToLower().Contains("asteroid") && a.Distance <= 1000 && !a.IsValid && !a.HasExploded && !a.HasReleased);
                        return _lcoToBlowUp ?? null;
                    }

                    return _lcoToBlowUp ?? null;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /**
        public class CachedMission
        {
            public int ContentID;
            public string Name;
            public int Level;
            public DirectAgentMission.MissionState State;
            public DirectAgentMission.MissionType Type;
            internal CachedMission(int ContentID, string Name, int Level, DirectAgentMission.MissionState State, DirectAgentMission.MissionType Type)
            {
                this.ContentID = ContentID;
                this.Name = Name;
                this.Level = Level;
                this.State = State;
                this.Type = Type;
            }
        }
        **/

        //public Dictionary<string, CachedMission> CachedMissions { get; set; }

        private void ClearCachedDataEveryPulse()
        {
            //_hostilePilot = null;
            _anchoredBubble = null;
            _warpScrambling = null;
            _neuting = null;
            _lcoToBlowUp = null;
        }

        #endregion

        public ILoveEVE.Framework.DirectActiveShip ActiveShip
        {
            get
            {
                return Cache.Instance.DirectEve.ActiveShip;
            }
        }

        public double DistanceFromMe(double x, double y, double z)
        {
            if (Cache.Instance.ActiveShip.Entity == null)
            {
                return double.MaxValue;
            }

            double curX = Cache.Instance.ActiveShip.Entity.X;
            double curY = Cache.Instance.ActiveShip.Entity.Y;
            double curZ = Cache.Instance.ActiveShip.Entity.Z;

            return Math.Round(Math.Sqrt((curX - x) * (curX - x) + (curY - y) * (curY - y) + (curZ - z) * (curZ - z)), 2);
        }

        #region States

        DateTime BookmarkUpdate = DateTime.Now;

        bool Control(object[] Params)
        {
            if ((!DirectEve.Session.IsInSpace && !DirectEve.Session.IsInStation) || !DirectEve.Session.IsReady) return false;
            ClearCachedDataEveryPulse();

            Name = DirectEve.Me.Name;
            //CharID = DirectEve.Me. //CharID;

            if (Bookmarks == null || BookmarkUpdate < DateTime.Now)
            {
                Bookmarks = DirectEve.Bookmarks.OrderBy(a => a.Title).Select(a => a.Title).ToArray();
                CitadelBookmarks = DirectEve.Bookmarks.Where(a => a.GroupId == (int)Group.Citadel).Select(a => a.Title).ToArray();
                BookmarkUpdate = DateTime.Now.AddMinutes(1);
            }
            //if (DirectEve.Session.InFleet) FleetMembers = Fleet.Members.Select(a => a.Name).ToArray();

            //try
            //{
            //    QMCache.Instance.DirectEve.AgentMissions.ForEach(a => { CachedMissions.AddOrUpdate(Agent.Get(a.AgentID).Name, new CachedMission(a.ContentID, a.Name, Agent.Get(a.AgentID).Level, a.State, a.Type)); });
            //}
            //catch (Exception){}

            //AvailableAgents = Agent.MyAgents.Select(a => a.Name).ToList();
            if (QMCache.Instance.InStation)
            {
                /**
                if (FittingManager.Ready)
                {
                    if (FittingManager.Fittings != null && FittingManager.Fittings.Any())
                    {
                        Fittings = FittingManager.Fittings.Select(fit => fit.Name).ToList();
                    }
                }
                else
                {
                    FittingManager.Prime();
                }
                **/
                //for (int i = 0; i <= 6; i++)
                //{
                //    if (DirectEve.Session.IsInStation && Station.CorpHangar(i) != null)
                //    {
                //        if (Station.CorpHangar(i).IsPrimed)
                //        {
                //            Station.CorpHangar(i).Items.ForEach(a => { ItemVolume.AddOrUpdate(a.Type, a.Volume); });
                //        }
                //        else
                //        {
                //            Station.CorpHangar(i).Prime();
                //            return false;
                //        }
                //    }
                //}
            }

            if (QMCache.Instance.InSpace)
            {
                try
                {
                    ArmorPercent = QMCache.Instance.ActiveShip.Armor / QMCache.Instance.ActiveShip.MaxArmor;
                    HullPercent = QMCache.Instance.ActiveShip.Structure / QMCache.Instance.ActiveShip.MaxStructure;
                    //if (Drone.AllInSpace.Any(a => a.ToEntity != null && (a.ToEntity.ArmorPct < 100 || a.ToEntity.HullPct < 100))) DamagedDrones = true;
                }
                catch (Exception){}
            }
            return false;
        }

        #endregion
    }

}

namespace ILEF.Caching
{
    using global::ILEF.Actions;
    using global::ILEF.BackgroundTasks;
    using global::ILEF.Combat;
    using global::ILEF.Lookup;
    using global::ILEF.States;
    using global::ILEF.Logging;
    //using DirectEve;

    public class QMCache
    {
        /// <summary>
        ///   Singleton implementation
        /// </summary>
        private static QMCache _instance = new QMCache();

        public static QMCache Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new QMCache();
                }
                return _instance;
            }
        }

        /// <summary>
        ///   _agent cache //cleared in InvalidateCache
        /// </summary>
        private DirectAgent _agent;

        /// <summary>
        ///   Current Storyline Mission Agent
        /// </summary>
        public long CurrentStorylineAgentId { get; set; }

        /// <summary>
        ///   Agent blacklist
        /// </summary>
        public List<long> AgentBlacklist;

        /// <summary>
        ///   Approaching cache //cleared in InvalidateCache
        /// </summary>
        private EntityCache _approaching;

        /// <summary>
        ///   BigObjects we are likely to bump into (mainly LCOs) //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _bigObjects;

        /// <summary>
        ///   BigObjects we are likely to bump into (mainly LCOs) //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _gates;

        /// <summary>
        ///   BigObjects we are likely to bump into (mainly LCOs) //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _bigObjectsAndGates;

        /// <summary>
        ///   objects we are likely to bump into (Anything that is not an NPC a wreck or a can) //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _objects;

        /// <summary>
        ///   Returns all non-empty wrecks and all containers //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _containers;

        /// <summary>
        ///   Safespot Bookmark cache (all bookmarks that start with the defined safespot prefix) //cleared in InvalidateCache
        /// </summary>
        private List<DirectBookmark> _safeSpotBookmarks;

        /// <summary>
        ///   Entities by Id //cleared in InvalidateCache
        /// </summary>
        private readonly Dictionary<long, EntityCache> _entitiesById;

        /// <summary>
        ///   Module cache //cleared in InvalidateCache
        /// </summary>
        private List<ModuleCache> _modules;

        public string OrbitEntityNamed;

        public DirectLocation MissionSolarSystem;

        public string DungeonId;

        /// <summary>
        ///   Star cache //cleared in InvalidateCache
        /// </summary>
        private EntityCache _star;

        /// <summary>
        ///   Station cache //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _stations;

        /// <summary>
        ///   Stargate cache //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _stargates;

        /// <summary>
        ///   Stargate by name //cleared in InvalidateCache
        /// </summary>
        private EntityCache _stargate;

        /// <summary>
        ///   JumpBridges //cleared in InvalidateCache
        /// </summary>
        private IEnumerable<EntityCache> _jumpBridges;

        /// <summary>
        ///   Targeting cache //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _targeting;

        /// <summary>
        ///   Targets cache //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _targets;


        /// <summary>
        ///   IDs in Inventory window tree (on left) //cleared in InvalidateCache
        /// </summary>
        public List<long> _IDsinInventoryTree;

        /// <summary>
        ///   Returns all unlooted wrecks & containers //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _unlootedContainers;

        /// <summary>
        ///   Returns all unlooted wrecks & containers and secure cans //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _unlootedWrecksAndSecureCans;

        /// <summary>
        ///   Returns all windows //cleared in InvalidateCache
        /// </summary>
        private List<DirectWindow> _windows;

        /// <summary>
        ///   Returns maxLockedTargets, the minimum between the character and the ship //cleared in InvalidateCache
        /// </summary>
        private int? _maxLockedTargets;

        /// <summary>
        ///  Dictionary for cached EWAR target
        /// </summary>
        public HashSet<long> ListOfWarpScramblingEntities = new HashSet<long>();
        public HashSet<long> ListOfJammingEntities = new HashSet<long>();
        public HashSet<long> ListOfTrackingDisruptingEntities = new HashSet<long>();
        public HashSet<long> ListNeutralizingEntities = new HashSet<long>();
        public HashSet<long> ListOfTargetPaintingEntities = new HashSet<long>();
        public HashSet<long> ListOfDampenuingEntities = new HashSet<long>();
        public HashSet<long> ListofWebbingEntities = new HashSet<long>();
        public HashSet<long> ListofContainersToLoot = new HashSet<long>();
        public HashSet<string> ListofMissionCompletionItemsToLoot = new HashSet<string>();
        //public List<EachWeaponsVolleyCache> ListofEachWeaponsVolleyData = new List<EachWeaponsVolleyCache>();
        //public long VolleyCount;

        /*
        public void IterateInvTypes(string module)
        {
            string path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (path != null)
            {
                string invtypesXmlFile = System.IO.Path.Combine(path, "InvTypes.xml");
                InvTypesById = new Dictionary<int, InvType>();

                if (!File.Exists(invtypesXmlFile))
                {
                    Logging.Log(module, "IterateInvTypes - unable to find [" + invtypesXmlFile + "]", Logging.White);
                    return;
                }

                try
                {
                    Logging.Log(module, "IterateInvTypes - Loading [" + invtypesXmlFile + "]", Logging.White);
                    InvTypes = XDocument.Load(invtypesXmlFile);
                    if (InvTypes.Root != null)
                    {
                        foreach (XElement element in InvTypes.Root.Elements("invtype"))
                        {
                            InvTypesById.Add((int)element.Attribute("id"), new InvType(element));
                        }
                    }
                }
                catch (Exception exception)
                {
                    Logging.Log(module, "IterateInvTypes - Exception: [" + exception + "]", Logging.Red);
                }

            }
            elsef
            {
                Logging.Log(module, "IterateInvTypes - unable to find [" + System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "]", Logging.White);
            }
        }
         * */

        public void IterateShipTargetValues(string module)
        {
            string path = Logging.PathToCurrentDirectory;

            if (path != null)
            {
                string ShipTargetValuesXmlFile = System.IO.Path.Combine(path, "ShipTargetValues.xml");
                ShipTargetValues = new List<ShipTargetValue>();

                if (!File.Exists(ShipTargetValuesXmlFile))
                {
                    Logging.Log(module, "IterateShipTargetValues - unable to find [" + ShipTargetValuesXmlFile + "]", Logging.White);
                    return;
                }

                try
                {
                    Logging.Log(module, "IterateShipTargetValues - Loading [" + ShipTargetValuesXmlFile + "]", Logging.White);
                    XDocument values = XDocument.Load(ShipTargetValuesXmlFile);
                    if (values.Root != null)
                    {
                        foreach (XElement value in values.Root.Elements("ship"))
                        {
                            ShipTargetValues.Add(new ShipTargetValue(value));
                        }
                    }
                }
                catch (Exception exception)
                {
                    Logging.Log(module, "IterateShipTargetValues - Exception: [" + exception + "]", Logging.Red);
                }
            }
        }

        public void IterateUnloadLootTheseItemsAreLootItems(string module)
        {
            string path = Logging.PathToCurrentDirectory;

            if (path != null)
            {
                string UnloadLootTheseItemsAreLootItemsXmlFile = System.IO.Path.Combine(path, "UnloadLootTheseItemsAreLootItems.xml");
                UnloadLootTheseItemsAreLootById = new Dictionary<int, string>();

                if (!File.Exists(UnloadLootTheseItemsAreLootItemsXmlFile))
                {
                    Logging.Log(module, "IterateUnloadLootTheseItemsAreLootItems - unable to find [" + UnloadLootTheseItemsAreLootItemsXmlFile + "]", Logging.White);
                    return;
                }

                try
                {
                    Logging.Log(module, "IterateUnloadLootTheseItemsAreLootItems - Loading [" + UnloadLootTheseItemsAreLootItemsXmlFile + "]", Logging.White);
                    MissionSettings.UnloadLootTheseItemsAreLootItems = XDocument.Load(UnloadLootTheseItemsAreLootItemsXmlFile);

                    if (MissionSettings.UnloadLootTheseItemsAreLootItems.Root != null)
                    {
                        foreach (XElement element in MissionSettings.UnloadLootTheseItemsAreLootItems.Root.Elements("invtype"))
                        {
                            UnloadLootTheseItemsAreLootById.Add((int)element.Attribute("id"), (string)element.Attribute("name"));
                        }
                    }
                }
                catch (Exception exception)
                {
                    Logging.Log(module, "IterateUnloadLootTheseItemsAreLootItems - Exception: [" + exception + "]", Logging.Red);
                }
            }
            else
            {
                Logging.Log(module, "IterateUnloadLootTheseItemsAreLootItems - unable to find [" + Logging.PathToCurrentDirectory + "]", Logging.White);
            }
        }

        public static int CacheInstances;

        public QMCache()
        {

            //string line = "Cache: new cache instance being instantiated";
            //InnerSpace.Echo(string.Format("{0:HH:mm:ss} {1}", DateTime.UtcNow, line));
            //line = string.Empty;

            LastModuleTargetIDs = new Dictionary<long, long>();
            TargetingIDs = new Dictionary<long, DateTime>();
            _entitiesById = new Dictionary<long, EntityCache>();

            InvTypesById = new Dictionary<int, InvType>();
            ShipTargetValues = new List<ShipTargetValue>();
            UnloadLootTheseItemsAreLootById = new Dictionary<int, string>();

            LootedContainers = new HashSet<long>();
            Time.Instance.LastKnownGoodConnectedTime = DateTime.UtcNow;

            Interlocked.Increment(ref CacheInstances);
        }

        ~QMCache()
        {
            Interlocked.Decrement(ref CacheInstances);
        }

        /// <summary>
        ///   List of containers that have been looted
        /// </summary>
        public HashSet<long> LootedContainers { get; private set; }

        public bool ExitWhenIdle;
        public bool StopBot;
        public bool LootAlreadyUnloaded;
        public bool RouteIsAllHighSecBool;

        public double Wealth { get; set; }
        public double WealthatStartofPocket { get; set; }
        public int StackHangarAttempts { get; set; }
        public bool NormalApproach = true;
        public bool CourierMission;
        public bool doneUsingRepairWindow;

        public long AmmoHangarID = -99;
        public long LootHangarID = -99;

        /// <summary>
        ///   Returns the mission for a specific agent
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="ForceUpdate"> </param>
        /// <returns>null if no mission could be found</returns>
        public DirectAgentMission GetAgentMission(long agentId, bool ForceUpdate)
        {
            if (DateTime.UtcNow < Time.Instance.NextGetAgentMissionAction)
            {
                if (MissionSettings.FirstAgentMission != null)
                {
                    return MissionSettings.FirstAgentMission;
                }

                return null;
            }

            try
            {
                if (ForceUpdate || MissionSettings.myAgentMissionList == null || !MissionSettings.myAgentMissionList.Any())
                {
                    MissionSettings.myAgentMissionList = DirectEve.AgentMissions.Where(m => m.AgentId == agentId).ToList();
                    Time.Instance.NextGetAgentMissionAction = DateTime.UtcNow.AddSeconds(5);
                }

                if (MissionSettings.myAgentMissionList.Any())
                {
                    MissionSettings.FirstAgentMission = MissionSettings.myAgentMissionList.FirstOrDefault();
                    return MissionSettings.FirstAgentMission;
                }

                return null;
            }
            catch (Exception exception)
            {
                Logging.Log("QMCache.Instance.GetAgentMission", "DirectEve.AgentMissions failed: [" + exception + "]", Logging.Teal);
                return null;
            }
        }

        public bool InMission { get; set; }

        public bool normalNav = true;  //Do we want to bypass normal navigation for some reason?
        public bool onlyKillAggro { get; set; }

        public int StackLoothangarAttempts { get; set; }
        public int StackAmmohangarAttempts { get; set; }
        public int StackItemhangarAttempts { get; set; }

        public string Path;

        public bool _isCorpInWar = false;

        public bool IsCorpInWar
        {
            get
            {
                if (DateTime.UtcNow > Time.Instance.NextCheckCorpisAtWar)
                {
                    bool war = DirectEve.Me.IsAtWar;
                    QMCache.Instance._isCorpInWar = war;

                    Time.Instance.NextCheckCorpisAtWar = DateTime.UtcNow.AddMinutes(15);
                    if (!_isCorpInWar)
                    {
                        if (Logging.DebugWatchForActiveWars) Logging.Log("IsCorpInWar", "Your corp is not involved in any wars (yet)", Logging.Green);
                    }
                    else
                    {
                        if (Logging.DebugWatchForActiveWars) Logging.Log("IsCorpInWar", "Your corp is involved in a war, be careful", Logging.Orange);
                    }

                    return _isCorpInWar;
                }

                return _isCorpInWar;
            }
        }

        public bool LocalSafe(int maxBad, double stand)
        {
            int number = 0;
            DirectChatWindow local = (DirectChatWindow)GetWindowByName("Local");

            try
            {
                foreach (DirectCharacter localMember in local.Members)
                {
                    float[] alliance = { DirectEve.Standings.GetPersonalRelationship(localMember.AllianceId), DirectEve.Standings.GetCorporationRelationship(localMember.AllianceId), DirectEve.Standings.GetAllianceRelationship(localMember.AllianceId) };
                    float[] corporation = { DirectEve.Standings.GetPersonalRelationship(localMember.CorporationId), DirectEve.Standings.GetCorporationRelationship(localMember.CorporationId), DirectEve.Standings.GetAllianceRelationship(localMember.CorporationId) };
                    float[] personal = { DirectEve.Standings.GetPersonalRelationship(localMember.CharacterId), DirectEve.Standings.GetCorporationRelationship(localMember.CharacterId), DirectEve.Standings.GetAllianceRelationship(localMember.CharacterId) };

                    if (alliance.Min() <= stand || corporation.Min() <= stand || personal.Min() <= stand)
                    {
                        Logging.Log("Cache.LocalSafe", "Bad Standing Pilot Detected: [ " + localMember.Name + "] " + " [ " + number + " ] so far... of [ " + maxBad + " ] allowed", Logging.Orange);
                        number++;
                    }

                    if (number > maxBad)
                    {
                        Logging.Log("Cache.LocalSafe", "[" + number + "] Bad Standing pilots in local, We should stay in station", Logging.Orange);
                        return false;
                    }
                }
            }
            catch (Exception exception)
            {
                Logging.Log("LocalSafe", "Exception [" + exception + "]", Logging.Debug);
            }

            return true;
        }

        public ILoveEVE.Framework.DirectEve DirectEve { get; set; }

        public Dictionary<int, InvType> InvTypesById { get; private set; }

        public Dictionary<int, string> UnloadLootTheseItemsAreLootById { get; private set; }


        /// <summary>
        ///   List of ship target values, higher target value = higher kill priority
        /// </summary>
        public List<ShipTargetValue> ShipTargetValues { get; private set; }

        /// <summary>
        ///   Best damage type for this mission
        /// </summary>
        public DamageType FrigateDamageType { get; set; }

        /// <summary>
        ///   Best damage type for Frigates for this mission / faction
        /// </summary>
        public DamageType CruiserDamageType { get; set; }

        /// <summary>
        ///   Best damage type for BattleCruisers for this mission / faction
        /// </summary>
        public DamageType BattleCruiserDamageType { get; set; }

        /// <summary>
        ///   Best damage type for BattleShips for this mission / faction
        /// </summary>
        public DamageType BattleShipDamageType { get; set; }

        /// <summary>
        ///   Best damage type for LargeColidables for this mission / faction
        /// </summary>
        public DamageType LargeColidableDamageType { get; set; }

        /// <summary>
        ///   Force Salvaging after mission
        /// </summary>
        public bool AfterMissionSalvaging { get; set; }


        //cargo =

        private DirectContainer _currentShipsCargo;

        public DirectContainer CurrentShipsCargo
        {
            get
            {
                try
                {
                    if ((QMCache.Instance.InSpace && DateTime.UtcNow > Time.Instance.LastInStation.AddSeconds(10)) || (QMCache.Instance.InStation && DateTime.UtcNow > Time.Instance.LastInSpace.AddSeconds(10)))
                    {
                        if (_currentShipsCargo == null)
                        {
                            _currentShipsCargo = QMCache.Instance.DirectEve.GetShipsCargo();
                            if (Logging.DebugCargoHold) Logging.Log("CurrentShipsCargo", "_currentShipsCargo is null", Logging.Debug);
                        }

                        return _currentShipsCargo;
                    }

                    int EntityCount = 0;
                    if (QMCache.Instance.Entities.Any())
                    {
                        EntityCount = QMCache.Instance.Entities.Count();
                    }

                    if (Logging.DebugCargoHold) Logging.Log("CurrentShipsCargo", "QMCache.Instance.MyShipEntity is null: We have a total of [" + EntityCount + "] entities available at the moment.", Logging.Debug);
                    return null;
                }
                catch (Exception exception)
                {
                    Logging.Log("CurrentShipsCargo", "Unable to complete ReadyCargoHold [" + exception + "]", Logging.Teal);
                    return null;
                }
            }
        }

        public DirectContainer _containerInSpace { get; set; }

        public DirectContainer ContainerInSpace
        {
            get
            {
                if (_containerInSpace == null)
                {
                    return null;
                }

                return _containerInSpace;
            }

            set { _containerInSpace = value; }
        }

        private DirectActiveShip _activeShip;
        public DirectActiveShip ActiveShip
        {
            get
            {
                try
                {
                    _activeShip = QMCache.Instance.DirectEve.ActiveShip;
                    return _activeShip;
                }
                catch (Exception)
                {
                    _activeShip = null;
                    return _activeShip;
                }
            }
        }

        /// <summary>
        ///   Returns the maximum weapon distance
        /// </summary>
        public int WeaponRange
        {
            get
            {
                // Get ammo based on current damage type
                IEnumerable<Ammo> ammo = Combat.Ammo.Where(a => a.DamageType == MissionSettings.CurrentDamageType).ToList();

                try
                {
                    // Is our ship's cargo available?
                    if (QMCache.Instance.CurrentShipsCargo != null)
                    {
                        ammo = ammo.Where(a => QMCache.Instance.CurrentShipsCargo.Items.Any(i => a.TypeId == i.TypeId && i.Quantity >= Combat.MinimumAmmoCharges));
                    }
                    else
                    {
                        return System.Convert.ToInt32(Combat.MaxTargetRange);
                    }

                    // Return ship range if there's no ammo left
                    if (!ammo.Any())
                    {
                        return System.Convert.ToInt32(Combat.MaxTargetRange);
                    }

                    return ammo.Max(a => a.Range);
                }
                catch (Exception ex)
                {
                    if (Logging.DebugExceptions) Logging.Log("Cache.WeaponRange", "exception was:" + ex.Message, Logging.Teal);

                    // Return max range
                    if (QMCache.Instance.ActiveShip != null)
                    {
                        return System.Convert.ToInt32(Combat.MaxTargetRange);
                    }

                    return 0;
                }
            }
        }

        private DirectItem _myCurrentAmmoInWeapon;
        public DirectItem myCurrentAmmoInWeapon
        {
            get
            {
                try
                {
                    if (_myCurrentAmmoInWeapon == null)
                    {
                        if (QMCache.Instance.Weapons != null && QMCache.Instance.Weapons.Any())
                        {
                            ModuleCache WeaponToCheckForAmmo = QMCache.Instance.Weapons.FirstOrDefault();
                            if (WeaponToCheckForAmmo != null)
                            {
                                _myCurrentAmmoInWeapon = WeaponToCheckForAmmo.Charge;
                                return _myCurrentAmmoInWeapon;
                            }

                            return null;
                        }

                        return null;
                    }

                    return _myCurrentAmmoInWeapon;
                }
                catch (Exception ex)
                {
                    if (Logging.DebugExceptions) Logging.Log("Cache.myCurrentAmmoInWeapon", "exception was:" + ex.Message, Logging.Teal);
                    return null;
                }
            }
        }


        /// <summary>
        ///   Last target for a certain module
        /// </summary>
        public Dictionary<long, long> LastModuleTargetIDs { get; private set; }

        /// <summary>
        ///   Targeting delay cache (used by LockTarget)
        /// </summary>
        public Dictionary<long, DateTime> TargetingIDs { get; private set; }

        public bool AllAgentsStillInDeclineCoolDown { get; set; }

        private string _currentAgent { get; set; }

        public bool Paused { get; set; }

        public long TotalMegaBytesOfMemoryUsed = 0;
        public double MyWalletBalance { get; set; }

        public bool UpdateMyWalletBalance()
        {
            //we know we are connected here
            Time.Instance.LastKnownGoodConnectedTime = DateTime.UtcNow;
            QMCache.Instance.MyWalletBalance = QMCache.Instance.DirectEve.Me.Wealth;
            return true;
        }

        public string CurrentPocketAction { get; set; }
        public float AgentEffectiveStandingtoMe;
        public string AgentEffectiveStandingtoMeText;
        public float AgentCorpEffectiveStandingtoMe;
        public float AgentFactionEffectiveStandingtoMe;
        public float StandingUsedToAccessAgent;
        public bool MissionBookmarkTimerSet;
        public long AgentStationID { get; set; }
        public string AgentStationName;
        public long AgentSolarSystemID;
        //public string AgentSolarSystemName;
        //public string CurrentAgentText = string.Empty;
        public string CurrentAgent
        {
            get
            {
                try
                {
                    if (QMSettings.Instance.CharacterXMLExists)
                    {
                        if (string.IsNullOrEmpty(_currentAgent))
                        {
                            try
                            {
                                if (!string.IsNullOrEmpty(SwitchAgent()))
                                {
                                    _currentAgent = SwitchAgent();
                                    Logging.Log("Cache.CurrentAgent", "[ " + _currentAgent + " ] AgentID [ " + Agent.AgentId + " ]", Logging.White);
                                }

                                return string.Empty;
                            }
                            catch (Exception ex)
                            {
                                Logging.Log("Cache.AgentId", "Exception [" + ex + "]", Logging.Debug);
                                return string.Empty;
                            }
                        }

                        return _currentAgent;
                    }

                    return string.Empty;
                }
                catch (Exception ex)
                {
                    Logging.Log("SelectNearestAgent", "Exception [" + ex + "]", Logging.Debug);
                    return "";
                }
            }
            set
            {
                try
                {
                    _currentAgent = value;
                }
                catch (Exception ex)
                {
                    Logging.Log("SelectNearestAgent", "Exception [" + ex + "]", Logging.Debug);
                }
            }
        }
        private static readonly Func<DirectAgent, DirectSession, bool> AgentInThisSolarSystemSelector = (a, s) => a.SolarSystemId == s.SolarSystemId;
        private static readonly Func<DirectAgent, DirectSession, bool> AgentInThisStationSelector = (a, s) => a.StationId == s.StationId;

        private string SelectNearestAgent(bool requireValidDeclineTimer)
        {
            string agentName = null;

            try
            {
                DirectAgentMission mission = null;

                if (!MissionSettings.ListOfAgents.Any()) return string.Empty;

                // first we try to find if we accepted a mission (not important) given by an agent in settings agents list
                foreach (AgentsList potentialAgent in MissionSettings.ListOfAgents)
                {
                    if (QMCache.Instance.DirectEve.AgentMissions.Any(m => m.State == (int)MissionState.Accepted && !m.Important && DirectEve.GetAgentById(m.AgentId).Name == potentialAgent.Name))
                    {
                        mission = QMCache.Instance.DirectEve.AgentMissions.FirstOrDefault(m => m.State == (int)MissionState.Accepted && !m.Important && DirectEve.GetAgentById(m.AgentId).Name == potentialAgent.Name);

                        // break on first accepted (not important) mission found
                        break;
                    }
                }

                if (mission != null)
                {
                    agentName = DirectEve.GetAgentById(mission.AgentId).Name;
                }
                // no accepted (not important) mission found, so we need to find the nearest agent in our settings agents list
                else if (QMCache.Instance.DirectEve.Session.IsReady)
                {
                    try
                    {
                        Func<DirectAgent, DirectSession, bool> selector = DirectEve.Session.IsInSpace ? AgentInThisSolarSystemSelector : AgentInThisStationSelector;
                        var nearestAgent = MissionSettings.ListOfAgents
                            .Where(x => !requireValidDeclineTimer || DateTime.UtcNow >= x.DeclineTimer)
                            .OrderBy(x => x.Priorit)
                            .Select(x => new { Agent = x, DirectAgent = DirectEve.GetAgentByName(x.Name) })
                            .FirstOrDefault(x => selector(x.DirectAgent, DirectEve.Session));

                        if (nearestAgent != null)
                        {
                            agentName = nearestAgent.Agent.Name;
                        }
                        else if (MissionSettings.ListOfAgents.OrderBy(j => j.Priorit).Any())
                        {
                            AgentsList __HighestPriorityAgentInList = MissionSettings.ListOfAgents
                                .Where(x => !requireValidDeclineTimer || DateTime.UtcNow >= x.DeclineTimer)
                                .OrderBy(x => x.Priorit)
                                .FirstOrDefault();
                            if (__HighestPriorityAgentInList != null)
                            {
                                agentName = __HighestPriorityAgentInList.Name;
                            }
                        }
                    }
                    catch (NullReferenceException) { }
                }
            }
            catch (Exception ex)
            {
                Logging.Log("SelectNearestAgent", "Exception [" + ex + "]", Logging.Debug);
            }

            return agentName ?? null;
        }

        private string SelectFirstAgent(bool returnFirstOneIfNoneFound = false)
        {
            try
            {
                AgentsList FirstAgent = MissionSettings.ListOfAgents.OrderBy(j => j.Priorit).FirstOrDefault();

                if (FirstAgent != null)
                {
                    return FirstAgent.Name;
                }

                Logging.Log("SelectFirstAgent", "Unable to find the first agent, are your agents configured?", Logging.Debug);
                return null;
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.SelectFirstAgent", "Exception [" + exception + "]", Logging.Debug);
                return null;
            }
        }

        public string SwitchAgent()
        {
            try
            {
                string agentNameToSwitchTo = null;

                if (_States.CurrentCombatMissionBehaviorState == CombatMissionsBehaviorState.PrepareStorylineSwitchAgents)
                {
                    //TODO: must be a better way to achieve this
                    if (!string.IsNullOrEmpty(SelectFirstAgent()))
                    {
                        return SelectFirstAgent();
                    }

                    return string.Empty;
                }

                if (string.IsNullOrEmpty(_currentAgent))
                {
                    // it means that this is first switch for Questor, so we'll check missions, then station or system for agents.
                    AllAgentsStillInDeclineCoolDown = false;
                    if (!string.IsNullOrEmpty(SelectNearestAgent(true)))
                    {
                        agentNameToSwitchTo = SelectNearestAgent(true);
                        return agentNameToSwitchTo;
                    }

                    if (!string.IsNullOrEmpty(SelectNearestAgent(false)))
                    {
                        agentNameToSwitchTo = SelectNearestAgent(false);
                        return agentNameToSwitchTo;
                    }

                    return string.Empty;
                }

                // find agent by priority and with ok declineTimer
                AgentsList agentToUseByPriority = MissionSettings.ListOfAgents.OrderBy(j => j.Priorit).FirstOrDefault(i => DateTime.UtcNow >= i.DeclineTimer);

                if (agentToUseByPriority != null)
                {
                    AllAgentsStillInDeclineCoolDown = false; //this literally means we DO have agents available (at least one agents decline timer has expired and is clear to use)
                    return agentToUseByPriority.Name;
                }

                // Why try to find an agent at this point ?
                /*
                try
                {
                    agent = QMSettings.Instance.ListOfAgents.OrderBy(j => j.Priorit).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Logging.Log("Cache.SwitchAgent", "Unable to process agent section of [" + QMSettings.Instance.CharacterSettingsPath + "] make sure you have a valid agent listed! Pausing so you can fix it. [" + ex.Message + "]", Logging.Debug);
                    QMCache.Instance.Paused = true;
                }
                */
                AllAgentsStillInDeclineCoolDown = true; //this literally means we have no agents available at the moment (decline timer likely)
                return null;
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.SwitchAgent", "Exception [" + exception + "]", Logging.Debug);
                return null;
            }
        }

        public DirectAgent Agent
        {
            get
            {
                try
                {
                    if (QMSettings.Instance.CharacterXMLExists)
                    {
                        try
                        {
                            if (_agent == null)
                            {
                                _agent = QMCache.Instance.DirectEve.GetAgentByName(CurrentAgent);
                                return null;
                            }

                            if (_agent != null)
                            {
                                //Logging.Log("Cache: CurrentAgent", "Processing Agent Info...", Logging.White);
                                QMCache.Instance.AgentStationName = QMCache.Instance.DirectEve.GetLocationName(QMCache.Instance._agent.StationId);
                                QMCache.Instance.AgentStationID = QMCache.Instance._agent.StationId;
                                //QMCache.Instance.AgentSolarSystemName = QMCache.Instance.DirectEve.GetLocationName(QMCache.Instance._agent.SolarSystemId);
                                QMCache.Instance.AgentSolarSystemID = QMCache.Instance._agent.SolarSystemId;
                                //Logging.Log("Cache: CurrentAgent", "AgentStationName [" + QMCache.Instance.AgentStationName + "]", Logging.White);
                                //Logging.Log("Cache: CurrentAgent", "AgentStationID [" + QMCache.Instance.AgentStationID + "]", Logging.White);
                                //Logging.Log("Cache: CurrentAgent", "AgentSolarSystemName [" + QMCache.Instance.AgentSolarSystemName + "]", Logging.White);
                                //Logging.Log("Cache: CurrentAgent", "AgentSolarSystemID [" + QMCache.Instance.AgentSolarSystemID + "]", Logging.White);
                                return _agent;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logging.Log("Cache.Agent", "Unable to process agent section of [" + Logging.CharacterSettingsPath + "] make sure you have a valid agent listed! Pausing so you can fix it. [" + ex.Message + "]", Logging.Debug);
                            QMCache.Instance.Paused = true;
                        }
                    }

                    Logging.Log("Cache.Agent", "if (!QMSettings.Instance.CharacterXMLExists)", Logging.Debug);
                    return null;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.Agent", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
        }

        //
        // in space only, use something like [ DirectEve.GetShipsModules ] in space to return a container of modules
        //
        public IEnumerable<ModuleCache> Modules
        {
            get
            {
                try
                {
                    if (_modules == null || !_modules.Any())
                    {
                        _modules = QMCache.Instance.DirectEve.Modules.Select(m => new ModuleCache(m)).ToList();
                    }

                    return _modules;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.Modules", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
        }

        public DirectContainer _fittedModules;
        public DirectContainer FittedModules
        {
            get
            {
                try
                {
                    if (_fittedModules == null)
                    {
                        _fittedModules = QMCache.Instance.DirectEve.GetShipsModules();
                    }

                    return _fittedModules;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.Modules", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
        }
        //
        // this CAN and should just list all possible weapon system groupIDs
        //

        private IEnumerable<ModuleCache> _weapons;
        public IEnumerable<ModuleCache> Weapons
        {
            get
            {
                if (_weapons == null)
                {
                    _weapons = Modules.Where(m => m.GroupId == Combat.WeaponGroupId).ToList(); // ||
                    //m.GroupId == (int)Group.ProjectileWeapon ||
                    //m.GroupId == (int)Group.EnergyWeapon ||
                    //m.GroupId == (int)Group.HybridWeapon ||
                    //m.GroupId == (int)Group.CruiseMissileLaunchers ||
                    //m.GroupId == (int)Group.RocketLaunchers ||
                    //m.GroupId == (int)Group.StandardMissileLaunchers ||
                    //m.GroupId == (int)Group.TorpedoLaunchers ||
                    //m.GroupId == (int)Group.AssaultMissilelaunchers ||
                    //m.GroupId == (int)Group.HeavyMissilelaunchers ||
                    //m.GroupId == (int)Group.DefenderMissilelaunchers);
                    if (MissionSettings.MissionWeaponGroupId != 0)
                    {
                        _weapons = Modules.Where(m => m.GroupId == MissionSettings.MissionWeaponGroupId).ToList();
                    }

                    if (QMCache.Instance.InSpace && DateTime.UtcNow > Time.Instance.LastInStation.AddSeconds(10))
                    {
                        if (!_weapons.Any())
                        {
                            int moduleNumber = 0;
                            Logging.Log("Cache.Weapons", "WeaponGroupID is defined as [" + Combat.WeaponGroupId + "] in your characters settings XML", Logging.Debug);
                            foreach (ModuleCache _module in QMCache.Instance.Modules)
                            {
                                moduleNumber++;
                                Logging.Log("Cache.Weapons", "[" + moduleNumber + "][] typeID [" + _module.TypeId + "] groupID [" + _module.GroupId + "]", Logging.White);
                            }
                        }
                        else
                        {
                            if (DateTime.UtcNow > Time.Instance.NextModuleDisableAutoReload)
                            {
                                //int weaponNumber = 0;
                                foreach (ModuleCache _weapon in QMCache.Instance.Weapons)
                                {
                                    //weaponNumber++;
                                    //if (_weapon.AutoReload)
                                    //{
                                    //    bool returnValueHereNotUsed = _weapon.DisableAutoReload;
                                    //    Time.Instance.NextModuleDisableAutoReload = DateTime.UtcNow.AddSeconds(2);
                                    //}
                                    //Logging.Log("Cache.Weapons", "[" + weaponNumber + "][" + _module.TypeName + "] typeID [" + _module.TypeId + "] groupID [" + _module.GroupId + "]", Logging.White);
                                }
                            }
                        }
                    }

                }

                return _weapons;
            }
        }

        public int MaxLockedTargets
        {
            get
            {
                try
                {
                    if (_maxLockedTargets == null)
                    {
                        _maxLockedTargets = Math.Min(QMCache.Instance.DirectEve.Me.MaxLockedTargets, QMCache.Instance.ActiveShip.MaxLockedTargets);
                        return (int)_maxLockedTargets;
                    }

                    return (int)_maxLockedTargets;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.MaxLockedTargets", "Exception [" + exception + "]", Logging.Debug);
                    return -1;
                }
            }
        }

        private List<EntityCache> _myAmmoInSpace;
        public IEnumerable<EntityCache> myAmmoInSpace
        {
            get
            {
                if (_myAmmoInSpace == null)
                {
                    if (myCurrentAmmoInWeapon != null)
                    {
                        _myAmmoInSpace = QMCache.Instance.Entities.Where(e => e.Distance > 3000 && e.IsOnGridWithMe && e.TypeId == myCurrentAmmoInWeapon.TypeId && e.Velocity > 50).ToList();
                        if (_myAmmoInSpace.Any())
                        {
                            return _myAmmoInSpace;
                        }

                        return null;
                    }

                    return null;
                }

                return _myAmmoInSpace;
            }
        }

        public IEnumerable<EntityCache> Containers
        {
            get
            {
                try
                {
                    return _containers ?? (_containers = QMCache.Instance.EntitiesOnGrid.Where(e =>
                           e.IsContainer &&
                           e.HaveLootRights &&
                          //(e.GroupId == (int)Group.Wreck && !e.IsWreckEmpty) &&
                          (e.Name != "Abandoned Container")).ToList());
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.Containers", "Exception [" + exception + "]", Logging.Debug);
                    return new List<EntityCache>();
                }
            }
        }

        public IEnumerable<EntityCache> ContainersIgnoringLootRights
        {
            get
            {
                return _containers ?? (_containers = QMCache.Instance.EntitiesOnGrid.Where(e =>
                           e.IsContainer &&
                          //(e.GroupId == (int)Group.Wreck && !e.IsWreckEmpty) &&
                          (e.Name != "Abandoned Container")).ToList());
            }
        }

        private IEnumerable<EntityCache> _wrecks;

        public IEnumerable<EntityCache> Wrecks
        {
            get { return _wrecks ?? (_wrecks = QMCache.Instance.EntitiesOnGrid.Where(e => (e.GroupId == (int)Group.Wreck)).ToList()); }
        }

        public IEnumerable<EntityCache> UnlootedContainers
        {
            get
            {
                return _unlootedContainers ?? (_unlootedContainers = QMCache.Instance.EntitiesOnGrid.Where(e =>
                          e.IsContainer &&
                          e.HaveLootRights &&
                          (!LootedContainers.Contains(e.Id))).OrderBy(
                              e => e.Distance).
                              ToList());
            }
        }

        //This needs to include items you can steal from (thus gain aggro)
        public IEnumerable<EntityCache> UnlootedWrecksAndSecureCans
        {
            get
            {
                return _unlootedWrecksAndSecureCans ?? (_unlootedWrecksAndSecureCans = QMCache.Instance.EntitiesOnGrid.Where(e =>
                          (e.GroupId == (int)Group.Wreck || e.GroupId == (int)Group.SecureContainer ||
                           e.GroupId == (int)Group.AuditLogSecureContainer ||
                           e.GroupId == (int)Group.FreightContainer)).OrderBy(e => e.Distance).
                          ToList());
            }
        }

        public IEnumerable<EntityCache> _TotalTargetsandTargeting;

        public IEnumerable<EntityCache> TotalTargetsandTargeting
        {
            get
            {
                if (_TotalTargetsandTargeting == null)
                {
                    _TotalTargetsandTargeting = QMCache.Instance.Targets.Concat(QMCache.Instance.Targeting.Where(i => !i.IsTarget));
                    return _TotalTargetsandTargeting;
                }

                return _TotalTargetsandTargeting;
            }
        }

        public int TotalTargetsandTargetingCount
        {
            get
            {
                if (!TotalTargetsandTargeting.Any())
                {
                    return 0;
                }

                return TotalTargetsandTargeting.Count();
            }
        }

        public int TargetingSlotsNotBeingUsedBySalvager
        {
            get
            {
                if (Salvage.MaximumWreckTargets > 0 && QMCache.Instance.MaxLockedTargets >= 5)
                {
                    return QMCache.Instance.MaxLockedTargets - Salvage.MaximumWreckTargets;
                }

                return QMCache.Instance.MaxLockedTargets;
            }
        }

        public IEnumerable<EntityCache> Targets
        {
            get
            {
                if (_targets == null)
                {
                    _targets = QMCache.Instance.EntitiesOnGrid.Where(e => e.IsTarget).ToList();
                }

                // Remove the target info from the TargetingIDs Queue (its been targeted)
                foreach (EntityCache target in _targets.Where(t => TargetingIDs.ContainsKey(t.Id)))
                {
                    TargetingIDs.Remove(target.Id);
                }

                return _targets;
            }
        }

        public IEnumerable<EntityCache> Targeting
        {
            get
            {
                if (_targeting == null)
                {
                    _targeting = QMCache.Instance.EntitiesOnGrid.Where(e => e.IsTargeting || QMCache.Instance.TargetingIDs.ContainsKey(e.Id)).ToList();
                }

                if (_targeting.Any())
                {
                    return _targeting;
                }

                return new List<EntityCache>();
            }
        }

        public List<long> IDsinInventoryTree
        {
            get
            {
                Logging.Log("Cache.IDsinInventoryTree", "Refreshing IDs from inventory tree, it has been longer than 30 seconds since the last refresh", Logging.Teal);
                return _IDsinInventoryTree ?? (_IDsinInventoryTree = QMCache.Instance.PrimaryInventoryWindow.GetIdsFromTree(false));
            }
        }

        /// <summary>
        ///   Entities cache (all entities within 256km) //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _entitiesOnGrid;

        public IEnumerable<EntityCache> EntitiesOnGrid
        {
            get
            {
                try
                {
                    if (_entitiesOnGrid == null)
                    {
                        _entitiesOnGrid = QMCache.Instance.Entities.Where(e => e.IsOnGridWithMe).ToList();
                        return _entitiesOnGrid;
                    }

                    return _entitiesOnGrid;
                }
                catch (NullReferenceException) { }  // this can happen during session changes

                return new List<EntityCache>();
            }
        }

        /// <summary>
        ///   Entities cache (all entities within 256km) //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _entities;

        public IEnumerable<EntityCache> Entities
        {
            get
            {
                try
                {
                    if (_entities == null)
                    {
                        _entities = QMCache.Instance.DirectEve.Entities.Where(e => e.IsValid && !e.HasExploded && !e.HasReleased && e.CategoryId != (int)CategoryID.Charge).Select(i => new EntityCache(i)).ToList();
                        return _entities;
                    }

                    return _entities;
                }
                catch (NullReferenceException) { }  // this can happen during session changes

                return new List<EntityCache>();
            }
        }

        /// <summary>
        ///   Entities cache (all entities within 256km) //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _chargeEntities;

        public IEnumerable<EntityCache> ChargeEntities
        {
            get
            {
                try
                {
                    if (_chargeEntities == null)
                    {
                        _chargeEntities = QMCache.Instance.DirectEve.Entities.Where(e => e.IsValid && !e.HasExploded && !e.HasReleased && e.CategoryId == (int)CategoryID.Charge).Select(i => new EntityCache(i)).ToList();
                        return _chargeEntities;
                    }

                    return _chargeEntities;
                }
                catch (NullReferenceException) { }  // this can happen during session changes

                return new List<EntityCache>();
            }
        }

        public Dictionary<long, string> EntityNames = new Dictionary<long, string>();
        public Dictionary<long, int> EntityTypeID = new Dictionary<long, int>();
        public Dictionary<long, int> EntityGroupID = new Dictionary<long, int>();
        public Dictionary<long, long> EntityBounty = new Dictionary<long, long>();
        public Dictionary<long, bool> EntityIsFrigate = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsNPCFrigate = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsCruiser = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsNPCCruiser = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsBattleCruiser = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsNPCBattleCruiser = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsBattleShip = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsNPCBattleShip = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsHighValueTarget = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsLowValueTarget = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsLargeCollidable = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsMiscJunk = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsBadIdea = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsFactionWarfareNPC = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsNPCByGroupID = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsEntutyIShouldLeaveAlone = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsSentry = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityHaveLootRights = new Dictionary<long, bool>();
        public Dictionary<long, bool> EntityIsStargate = new Dictionary<long, bool>();

        private IEnumerable<EntityCache> _entitiesActivelyBeingLocked;
        public IEnumerable<EntityCache> EntitiesActivelyBeingLocked
        {
            get
            {
                if (!InSpace)
                {
                    return new List<EntityCache>();
                }

                if (QMCache.Instance.EntitiesOnGrid.Any())
                {
                    if (_entitiesActivelyBeingLocked == null)
                    {
                        _entitiesActivelyBeingLocked = QMCache.Instance.EntitiesOnGrid.Where(i => i.IsTargeting).ToList();
                        if (_entitiesActivelyBeingLocked.Any())
                        {
                            return _entitiesActivelyBeingLocked;
                        }

                        return new List<EntityCache>();
                    }

                    return _entitiesActivelyBeingLocked;
                }

                return new List<EntityCache>();
            }
        }

        /// <summary>
        ///   Entities cache (all entities within 256km) //cleared in InvalidateCache
        /// </summary>
        private List<EntityCache> _entitiesNotSelf;

        public IEnumerable<EntityCache> EntitiesNotSelf
        {
            get
            {
                if (_entitiesNotSelf == null)
                {
                    _entitiesNotSelf = QMCache.Instance.EntitiesOnGrid.Where(i => i.CategoryId != (int)CategoryID.Asteroid && i.Id != QMCache.Instance.ActiveShip.ItemId).ToList();
                    if (_entitiesNotSelf.Any())
                    {
                        return _entitiesNotSelf;
                    }

                    return new List<EntityCache>();
                }

                return _entitiesNotSelf;
            }
        }

        private EntityCache _myShipEntity;
        public EntityCache MyShipEntity
        {
            get
            {
                if (_myShipEntity == null)
                {
                    if (!QMCache.Instance.InSpace)
                    {
                        return null;
                    }

                    _myShipEntity = QMCache.Instance.EntitiesOnGrid.FirstOrDefault(e => e.Id == QMCache.Instance.ActiveShip.ItemId);
                    return _myShipEntity;
                }

                return _myShipEntity;
            }
        }

        public bool InSpace
        {
            get
            {
                try
                {
                    if (DateTime.UtcNow < Time.Instance.LastSessionChange.AddSeconds(10))
                    {
                        return false;
                    }

                    if (DateTime.UtcNow < Time.Instance.LastInSpace.AddMilliseconds(800))
                    {
                        //if We already set the LastInStation timestamp this iteration we do not need to check if we are in station
                        return true;
                    }

                    if (DirectEve.Session.IsInSpace)
                    {
                        if (!QMCache.Instance.InStation)
                        {
                            if (QMCache.Instance.DirectEve.ActiveShip.Entity != null)
                            {
                                if (DirectEve.Session.IsReady)
                                {
                                    if (QMCache.Instance.Entities.Any())
                                    {
                                        Time.Instance.LastInSpace = DateTime.UtcNow;
                                        return true;
                                    }
                                }

                                if (Logging.DebugInSpace) Logging.Log("InSpace", "Session is Not Ready", Logging.Debug);
                                return false;
                            }

                            if (Logging.DebugInSpace) Logging.Log("InSpace", "QMCache.Instance.DirectEve.ActiveShip.Entity is null", Logging.Debug);
                            return false;
                        }

                        if (Logging.DebugInSpace) Logging.Log("InSpace", "NOT InStation is False", Logging.Debug);
                        return false;
                    }

                    if (Logging.DebugInSpace) Logging.Log("InSpace", "InSpace is False", Logging.Debug);
                    return false;
                }
                catch (Exception ex)
                {
                    if (Logging.DebugExceptions) Logging.Log("Cache.InSpace", "if (DirectEve.Session.IsInSpace && !DirectEve.Session.IsInStation && DirectEve.Session.IsReady && QMCache.Instance.ActiveShip.Entity != null) <---must have failed exception was [" + ex.Message + "]", Logging.Teal);
                    return false;
                }
            }
        }

        public bool InStation
        {
            get
            {
                try
                {
                    if (DateTime.UtcNow < Time.Instance.LastSessionChange.AddSeconds(10))
                    {
                        return false;
                    }

                    if (DateTime.UtcNow < Time.Instance.LastInStation.AddMilliseconds(800))
                    {
                        //if We already set the LastInStation timestamp this iteration we do not need to check if we are in station
                        return true;
                    }

                    if (DirectEve.Session.IsInStation && !DirectEve.Session.IsInSpace && DirectEve.Session.IsReady)
                    {
                        if (!QMCache.Instance.Entities.Any())
                        {
                            Time.Instance.LastInStation = DateTime.UtcNow;
                            return true;
                        }
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    if (Logging.DebugExceptions) Logging.Log("Cache.InStation", "if (DirectEve.Session.IsInStation && !DirectEve.Session.IsInSpace && DirectEve.Session.IsReady) <---must have failed exception was [" + ex.Message + "]", Logging.Teal);
                    return false;
                }
            }
        }

        public bool InWarp
        {
            get
            {
                try
                {
                    if (QMCache.Instance.InSpace && !QMCache.Instance.InStation)
                    {
                        if (QMCache.Instance.ActiveShip != null)
                        {
                            if (QMCache.Instance.ActiveShip.Entity != null)
                            {
                                if (QMCache.Instance.ActiveShip.Entity.Mode == 3)
                                {
                                    if (QMCache.Instance.Modules != null && QMCache.Instance.Modules.Any())
                                    {
                                        Combat.ReloadAll(QMCache.Instance.MyShipEntity, true);
                                    }

                                    Time.Instance.LastInWarp = DateTime.UtcNow;
                                    return true;
                                }

                                if (Logging.DebugInWarp && !QMCache.Instance.Paused) Logging.Log("Cache.InWarp", "We are not in warp.QMCache.Instance.ActiveShip.Entity.Mode  is [" + (int)QMCache.Instance.MyShipEntity.Mode + "]", Logging.Teal);
                                return false;
                            }

                            if (Logging.DebugInWarp && !QMCache.Instance.Paused) Logging.Log("Cache.InWarp", "Why are we checking for InWarp if QMCache.Instance.ActiveShip.Entity is Null? (session change?)", Logging.Teal);
                            return false;
                        }

                        if (Logging.DebugInWarp && !QMCache.Instance.Paused) Logging.Log("Cache.InWarp", "Why are we checking for InWarp if QMCache.Instance.ActiveShip is Null? (session change?)", Logging.Teal);
                        return false;
                    }

                    if (Logging.DebugInWarp && !QMCache.Instance.Paused) Logging.Log("Cache.InWarp", "Why are we checking for InWarp while docked or between session changes?", Logging.Teal);
                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.InWarp", "InWarp check failed, exception [" + exception + "]", Logging.Teal);
                }

                return false;
            }
        }

        public bool IsOrbiting(long EntityWeWantToBeOrbiting = 0)
        {
            try
            {
                if (QMCache.Instance.Approaching != null)
                {
                    bool _followIDIsOnGrid = false;

                    if (EntityWeWantToBeOrbiting != 0)
                    {
                        _followIDIsOnGrid = (EntityWeWantToBeOrbiting == QMCache.Instance.ActiveShip.Entity.FollowId);
                    }
                    else
                    {
                        _followIDIsOnGrid = QMCache.Instance.EntitiesOnGrid.Any(i => i.Id == QMCache.Instance.ActiveShip.Entity.FollowId);
                    }

                    if (QMCache.Instance.ActiveShip.Entity != null && QMCache.Instance.ActiveShip.Entity.Mode == 4 && _followIDIsOnGrid)
                    {
                        return true;
                    }

                    return false;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.IsApproaching", "Exception [" + exception + "]", Logging.Debug);
                return false;
            }
        }

        public bool IsApproaching(long EntityWeWantToBeApproaching = 0)
        {
            try
            {
                if (QMCache.Instance.Approaching != null)
                {
                    bool _followIDIsOnGrid = false;

                    if (EntityWeWantToBeApproaching != 0)
                    {
                        _followIDIsOnGrid = (EntityWeWantToBeApproaching == QMCache.Instance.ActiveShip.Entity.FollowId);
                    }
                    else
                    {
                        _followIDIsOnGrid = QMCache.Instance.EntitiesOnGrid.Any(i => i.Id == QMCache.Instance.ActiveShip.Entity.FollowId);
                    }

                    if (QMCache.Instance.ActiveShip.Entity != null && QMCache.Instance.ActiveShip.Entity.Mode == 1 && _followIDIsOnGrid)
                    {
                        return true;
                    }

                    return false;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.IsApproaching", "Exception [" + exception + "]", Logging.Debug);
                return false;
            }
        }

        public bool IsApproachingOrOrbiting(long EntityWeWantToBeApproachingOrOrbiting = 0)
        {
            try
            {
                if (IsApproaching(EntityWeWantToBeApproachingOrOrbiting))
                {
                    return true;
                }

                if (IsOrbiting(EntityWeWantToBeApproachingOrOrbiting))
                {
                    return true;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.IsApproachingOrOrbiting", "Exception [" + exception + "]", Logging.Debug);
                return false;
            }
        }

        public List<EntityCache> Stations
        {
            get
            {
                try
                {
                    if (_stations == null)
                    {
                        if (QMCache.Instance.Entities.Any())
                        {
                            _stations = QMCache.Instance.Entities.Where(e => e.CategoryId == (int)CategoryID.Station).OrderBy(i => i.Distance).ToList();
                            if (_stations.Any())
                            {
                                return _stations;
                            }

                            return new List<EntityCache>();
                        }

                        return null;
                    }

                    return _stations;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.SolarSystems", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
        }

        public EntityCache ClosestStation
        {
            get
            {
                try
                {
                    if (Stations != null && Stations.Any())
                    {
                        return Stations.OrderBy(s => s.Distance).FirstOrDefault() ?? QMCache.Instance.Entities.OrderByDescending(s => s.Distance).FirstOrDefault();
                    }

                    return null;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.IsApproaching", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
        }

        public EntityCache StationByName(string stationName)
        {
            EntityCache station = Stations.First(x => x.Name.ToLower() == stationName.ToLower());
            return station;
        }


        public IEnumerable<DirectSolarSystem> _solarSystems;
        public IEnumerable<DirectSolarSystem> SolarSystems
        {
            get
            {
                try
                {
                    //High sec: 1090
                    //Low sec: 817
                    //0.0: 3524 (of which 230 are not connected)
                    //W-space: 2499

                    //High sec + Low sec = Empire: 1907
                    //Empire + 0.0 = K-space: 5431
                    //K-space + W-space = Total: 7930
                    if (Time.Instance.LastSessionChange.AddSeconds(30) > DateTime.UtcNow && (QMCache.Instance.InSpace || QMCache.Instance.InStation))
                    {
                        if (_solarSystems == null || !_solarSystems.Any() || _solarSystems.Count() < 5400)
                        {
                            if (QMCache.Instance.DirectEve.SolarSystems.Any())
                            {
                                if (QMCache.Instance.DirectEve.SolarSystems.Values.Any())
                                {
                                    _solarSystems = QMCache.Instance.DirectEve.SolarSystems.Values.OrderBy(s => s.Name).ToList();
                                }

                                return null;
                            }

                            return null;
                        }

                        return _solarSystems;
                    }

                    return null;
                }
                catch (NullReferenceException) // Not sure why this happens, but seems to be no problem
                {
                    return null;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.SolarSystems", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
        }

        public IEnumerable<EntityCache> JumpBridges
        {
            get { return _jumpBridges ?? (_jumpBridges = QMCache.Instance.Entities.Where(e => e.GroupId == (int)Group.JumpBridge).ToList()); }
        }

        public List<EntityCache> Stargates
        {
            get
            {
                try
                {
                    if (_stargates == null)
                    {
                        if (QMCache.Instance.Entities != null && QMCache.Instance.Entities.Any())
                        {
                            //if (QMCache.Instance.EntityIsStargate.Any())
                            //{
                            //    if (_stargates != null && _stargates.Any()) _stargates.Clear();
                            //    if (_stargates == null) _stargates = new List<EntityCache>();
                            //    foreach (KeyValuePair<long, bool> __stargate in QMCache.Instance.EntityIsStargate)
                            //    {
                            //        _stargates.Add(QMCache.Instance.Entities.FirstOrDefault(i => i.Id == __stargate.Key));
                            //    }
                            //
                            //    if (_stargates.Any()) return _stargates;
                            //}

                            _stargates = QMCache.Instance.Entities.Where(e => e.GroupId == (int)Group.Stargate).ToList();
                            //foreach (EntityCache __stargate in _stargates)
                            //{
                            //    if (QMCache.Instance.EntityIsStargate.Any())
                            //    {
                            //        if (!QMCache.Instance.EntityIsStargate.ContainsKey(__stargate.Id))
                            //        {
                            //            QMCache.Instance.EntityIsStargate.Add(__stargate.Id, true);
                            //            continue;
                            //        }
                            //
                            //        continue;
                            //    }
                            //
                            //    QMCache.Instance.EntityIsStargate.Add(__stargate.Id, true);
                            //    continue;
                            //}

                            return _stargates;
                        }

                        return null;
                    }

                    return _stargates;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.Stargates", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
        }

        public EntityCache ClosestStargate
        {
            get
            {
                try
                {
                    if (QMCache.Instance.InSpace)
                    {
                        if (QMCache.Instance.Entities != null && QMCache.Instance.Entities.Any())
                        {
                            if (QMCache.Instance.Stargates != null && QMCache.Instance.Stargates.Any())
                            {
                                return QMCache.Instance.Stargates.OrderBy(s => s.Distance).FirstOrDefault() ?? null;
                            }

                            return null;
                        }

                        return null;
                    }

                    return null;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.ClosestStargate", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
        }

        public EntityCache StargateByName(string locationName)
        {
            {
                return _stargate ?? (_stargate = QMCache.Instance.EntitiesByName(locationName, QMCache.Instance.Entities.Where(i => i.GroupId == (int)Group.Stargate)).FirstOrDefault(e => e.GroupId == (int)Group.Stargate));
            }
        }

        public IEnumerable<EntityCache> BigObjects
        {
            get
            {
                try
                {
                    return _bigObjects ?? (_bigObjects = QMCache.Instance.EntitiesOnGrid.Where(e =>
                       e.Distance < (double)Distances.OnGridWithMe &&
                       (e.IsLargeCollidable || e.CategoryId == (int)CategoryID.Asteroid || e.GroupId == (int)Group.SpawnContainer)
                       ).OrderBy(t => t.Distance).ToList());
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.BigObjects", "Exception [" + exception + "]", Logging.Debug);
                    return new List<EntityCache>();
                }
            }
        }

        public IEnumerable<EntityCache> AccelerationGates
        {
            get
            {
                return _gates ?? (_gates = QMCache.Instance.EntitiesOnGrid.Where(e =>
                       e.Distance < (double)Distances.OnGridWithMe &&
                       e.GroupId == (int)Group.AccelerationGate &&
                       e.Distance < (double)Distances.OnGridWithMe).OrderBy(t => t.Distance).ToList());
            }
        }

        public IEnumerable<EntityCache> BigObjectsandGates
        {
            get
            {
                return _bigObjectsAndGates ?? (_bigObjectsAndGates = QMCache.Instance.EntitiesOnGrid.Where(e =>
                       (e.IsLargeCollidable || e.CategoryId == (int)CategoryID.Asteroid || e.GroupId == (int)Group.AccelerationGate || e.GroupId == (int)Group.SpawnContainer)
                       && e.Distance < (double)Distances.DirectionalScannerCloseRange).OrderBy(t => t.Distance).ToList());
            }
        }

        public IEnumerable<EntityCache> Objects
        {
            get
            {
                return _objects ?? (_objects = QMCache.Instance.EntitiesOnGrid.Where(e =>
                       !e.IsPlayer &&
                       e.GroupId != (int)Group.SpawnContainer &&
                       e.GroupId != (int)Group.Wreck &&
                       e.Distance < 200000).OrderBy(t => t.Distance).ToList());
            }
        }

        public EntityCache Star
        {
            get { return _star ?? (_star = Entities.FirstOrDefault(e => e.CategoryId == (int)CategoryID.Celestial && e.GroupId == (int)Group.Star)); }
        }

        public EntityCache Approaching
        {
            get
            {
                try
                {
                    if (_approaching == null)
                    {
                        if (QMCache.Instance.MyShipEntity != null && QMCache.Instance.MyShipEntity.IsValid && !QMCache.Instance.MyShipEntity.HasExploded && !QMCache.Instance.MyShipEntity.HasReleased)
                        {
                            if (QMCache.Instance.MyShipEntity.FollowId != 0)
                            {
                                _approaching = EntityById(QMCache.Instance.MyShipEntity.FollowId);
                                if (_approaching != null && _approaching.IsValid)
                                {
                                    return _approaching;
                                }

                                return null;
                            }

                            return null;
                        }

                        return null;
                    }

                    return _approaching;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.Approaching", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
            set
            {
                _approaching = value;
            }
        }

        public List<DirectWindow> Windows
        {
            get
            {
                try
                {
                    if (QMCache.Instance.InSpace && DateTime.UtcNow > Time.Instance.LastInStation.AddSeconds(20) || (QMCache.Instance.InStation && DateTime.UtcNow > Time.Instance.LastInSpace.AddSeconds(20)))
                    {
                        return _windows ?? (_windows = DirectEve.Windows);
                    }

                    return new List<DirectWindow>();
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.Windows", "Exception [" + exception + "]", Logging.Debug);
                }

                return null;
            }
        }

        public bool CloseQuestorCMDLogoff; //false;

        public bool CloseQuestorCMDExitGame = true;

        public bool CloseQuestorEndProcess;

        public bool GotoBaseNow; //false;

        public bool QuestorJustStarted = true;

        //public bool DropMode;

        public DirectWindow GetWindowByCaption(string caption)
        {
            return Windows.FirstOrDefault(w => w.Caption.Contains(caption));
        }

        public DirectWindow GetWindowByName(string name)
        {
            DirectWindow WindowToFind = null;
            try
            {
                if (!QMCache.Instance.Windows.Any())
                {
                    return null;
                }

                // Special cases
                if (name == "Local")
                {
                    WindowToFind = Windows.FirstOrDefault(w => w.Name.StartsWith("chatchannel_solarsystemid"));
                }

                if (WindowToFind == null)
                {
                    WindowToFind = Windows.FirstOrDefault(w => w.Name == name);
                }

                if (WindowToFind != null)
                {
                    return WindowToFind;
                }
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.GetWindowByName", "Exception [" + exception + "]", Logging.Debug);
            }

            return null;
        }

        /// <summary>
        ///   Return entities by name
        /// </summary>
        /// <param name = "nameToSearchFor"></param>
        /// <param name = "EntitiesToLookThrough"></param>
        /// <returns></returns>
        public IEnumerable<EntityCache> EntitiesByName(string nameToSearchFor, IEnumerable<EntityCache> EntitiesToLookThrough)
        {
            return EntitiesToLookThrough.Where(e => e.Name.ToLower() == nameToSearchFor.ToLower()).ToList();
        }

        /// <summary>
        ///   Return entity by name
        /// </summary>
        /// <param name = "name"></param>
        /// <returns></returns>
        public EntityCache EntityByName(string name)
        {
            return QMCache.Instance.Entities.FirstOrDefault(e => System.String.Compare(e.Name, name, System.StringComparison.OrdinalIgnoreCase) == 0);
        }

        public IEnumerable<EntityCache> EntitiesByPartialName(string nameToSearchFor)
        {
            try
            {
                if (QMCache.Instance.Entities != null && QMCache.Instance.Entities.Any())
                {
                    IEnumerable<EntityCache> _entitiesByPartialName = QMCache.Instance.Entities.Where(e => e.Name.Contains(nameToSearchFor)).ToList();
                    if (!_entitiesByPartialName.Any())
                    {
                        _entitiesByPartialName = QMCache.Instance.Entities.Where(e => e.Name == nameToSearchFor).ToList();
                    }

                    //if we have no entities by that name return null;
                    if (!_entitiesByPartialName.Any())
                    {
                        _entitiesByPartialName = null;
                    }

                    return _entitiesByPartialName;
                }

                return null;
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.allBookmarks", "Exception [" + exception + "]", Logging.Debug);
                return null;
            }
        }

        /// <summary>
        ///   Return entities that contain the name
        /// </summary>
        /// <returns></returns>
        public IEnumerable<EntityCache> EntitiesThatContainTheName(string label)
        {
            try
            {
                return QMCache.Instance.Entities.Where(e => !string.IsNullOrEmpty(e.Name) && e.Name.ToLower().Contains(label.ToLower())).ToList();
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.EntitiesThatContainTheName", "Exception [" + exception + "]", Logging.Debug);
                return null;
            }
        }

        /// <summary>
        ///   Return a cached entity by Id
        /// </summary>
        /// <param name = "id"></param>
        /// <returns></returns>
        public EntityCache EntityById(long id)
        {
            try
            {
                if (_entitiesById.ContainsKey(id))
                {
                    return _entitiesById[id];
                }

                EntityCache entity = QMCache.Instance.EntitiesOnGrid.FirstOrDefault(e => e.Id == id);
                _entitiesById[id] = entity;
                return entity;
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.EntityById", "Exception [" + exception + "]", Logging.Debug);
                return null;
            }
        }

        public List<DirectBookmark> _allBookmarks;

        public List<DirectBookmark> AllBookmarks
        {
            get
            {
                try
                {
                    if (QMCache.Instance._allBookmarks == null || !QMCache.Instance._allBookmarks.Any())
                    {
                        if (DateTime.UtcNow > Time.Instance.NextBookmarkAction)
                        {
                            Time.Instance.NextBookmarkAction = DateTime.UtcNow.AddMilliseconds(200);
                            if (DirectEve.Bookmarks.Any())
                            {
                                _allBookmarks = QMCache.Instance.DirectEve.Bookmarks;
                                return _allBookmarks;
                            }

                            return null; //there are no bookmarks to list...
                        }

                        return null; //new List<DirectBookmark>(); //there are no bookmarks to list...
                    }

                    return QMCache.Instance._allBookmarks;
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.allBookmarks", "Exception [" + exception + "]", Logging.Debug);
                    return new List<DirectBookmark>(); ;
                }
            }
            set
            {
                _allBookmarks = value;
            }
        }

        /// <summary>
        ///   Return a bookmark by id
        /// </summary>
        /// <param name = "bookmarkId"></param>
        /// <returns></returns>
        public DirectBookmark BookmarkById(long bookmarkId)
        {
            try
            {
                if (QMCache.Instance.AllBookmarks != null && QMCache.Instance.AllBookmarks.Any())
                {
                    return QMCache.Instance.AllBookmarks.FirstOrDefault(b => b.BookmarkId == bookmarkId);
                }

                return null;
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.BookmarkById", "Exception [" + exception + "]", Logging.Debug);
                return null;
            }
        }

        /// <summary>
        ///   Returns bookmarks that start with the supplied label
        /// </summary>
        /// <param name = "label"></param>
        /// <returns></returns>
        public List<DirectBookmark> BookmarksByLabel(string label)
        {
            try
            {
                // Does not seems to refresh the Corporate Bookmark list so it's having troubles to find Corporate Bookmarks
                if (QMCache.Instance.AllBookmarks != null && QMCache.Instance.AllBookmarks.Any())
                {
                    return QMCache.Instance.AllBookmarks.Where(b => !string.IsNullOrEmpty(b.Title) && b.Title.ToLower().StartsWith(label.ToLower())).OrderBy(f => f.LocationId).ThenBy(i => QMCache.Instance.DistanceFromMe(i.X ?? 0, i.Y ?? 0, i.Z ?? 0)).ToList();
                }

                return null;
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.BookmarkById", "Exception [" + exception + "]", Logging.Debug);
                return null;
            }
        }

        /// <summary>
        ///   Returns bookmarks that contain the supplied label anywhere in the title
        /// </summary>
        /// <param name = "label"></param>
        /// <returns></returns>
        public List<DirectBookmark> BookmarksThatContain(string label)
        {
            try
            {
                if (QMCache.Instance.AllBookmarks != null && QMCache.Instance.AllBookmarks.Any())
                {
                    return QMCache.Instance.AllBookmarks.Where(b => !string.IsNullOrEmpty(b.Title) && b.Title.ToLower().Contains(label.ToLower())).OrderBy(f => f.LocationId).ToList();
                }

                return null;
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.BookmarksThatContain", "Exception [" + exception + "]", Logging.Debug);
                return null;
            }
        }

        /// <summary>
        ///   Invalidate the cached items
        /// </summary>
        public void InvalidateCache()
        {
            try
            {
                Logging.InvalidateCache();
                Arm.InvalidateCache();
                Drones.InvalidateCache();
                Combat.InvalidateCache();
                Salvage.InvalidateCache();

                _ammoHangar = null;
                _lootHangar = null;
                _lootContainer = null;
                _fittedModules = null;
                _oreHold = null;

                //
                // this list of variables is cleared every pulse.
                //
                _agent = null;
                _allBookmarks = null;
                _activeShip = null;
                _approaching = null;
                _bigObjects = null;
                _bigObjectsAndGates = null;
                _chargeEntities = null;
                _currentShipsCargo = null;
                _containerInSpace = null;
                _containers = null;
                _entities = null;
                _entitiesNotSelf = null;
                _entitiesOnGrid = null;
                _entitiesById.Clear();
                _fittingManagerWindow = null;
                _gates = null;
                _IDsinInventoryTree = null;
                _itemHangar = null;
                _jumpBridges = null;
                _lpStore = null;
                _maxLockedTargets = null;
                _modules = null;
                _myAmmoInSpace = null;
                _myCurrentAmmoInWeapon = null;
                _myShipEntity = null;
                _objects = null;
                _safeSpotBookmarks = null;
                _shipHangar = null;
                _star = null;
                _stations = null;
                _stargate = null;
                _stargates = null;
                _targets = null;
                _targeting = null;
                _TotalTargetsandTargeting = null;
                _unlootedContainers = null;
                _unlootedWrecksAndSecureCans = null;
                _weapons = null;
                _windows = null;
                _wrecks = null;
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.InvalidateCache", "Exception [" + exception + "]", Logging.Debug);
            }
        }

        /// <summary>
        ///   Calculate distance from me
        /// </summary>
        /// <param name = "x"></param>
        /// <param name = "y"></param>
        /// <param name = "z"></param>
        /// <returns></returns>
        public double DistanceFromMe(double x, double y, double z)
        {
            try
            {

                if (QMCache.Instance.ActiveShip.Entity == null)
                {
                    return double.MaxValue;
                }

                double curX = QMCache.Instance.ActiveShip.Entity.X;
                double curY = QMCache.Instance.ActiveShip.Entity.Y;
                double curZ = QMCache.Instance.ActiveShip.Entity.Z;

                return Math.Round(Math.Sqrt((curX - x) * (curX - x) + (curY - y) * (curY - y) + (curZ - z) * (curZ - z)), 2);
            }
            catch (Exception ex)
            {
                Logging.Log("DistanceFromMe", "Exception [" + ex + "]", Logging.Debug);
                return 0;
            }
        }

        /// <summary>
        ///   Create a bookmark
        /// </summary>
        /// <param name = "label"></param>
        public void CreateBookmark(string label)
        {
            try
            {
                if (QMCache.Instance.AfterMissionSalvageBookmarks.Count() < 100)
                {
                    if (Salvage.CreateSalvageBookmarksIn.ToLower() == "corp".ToLower())
                    {
                        DirectBookmarkFolder folder = QMCache.Instance.DirectEve.BookmarkFolders.FirstOrDefault(i => i.Name == QMSettings.Instance.BookmarkFolder);
                        if (folder != null)
                        {
                            QMCache.Instance.DirectEve.CorpBookmarkCurrentLocation(label, "", folder.Id);
                        }
                        else
                        {
                            QMCache.Instance.DirectEve.CorpBookmarkCurrentLocation(label, "", null);
                        }
                    }
                    else
                    {
                        DirectBookmarkFolder folder = QMCache.Instance.DirectEve.BookmarkFolders.FirstOrDefault(i => i.Name == QMSettings.Instance.BookmarkFolder);
                        if (folder != null)
                        {
                            QMCache.Instance.DirectEve.BookmarkCurrentLocation(label, "", folder.Id);
                        }
                        else
                        {
                            QMCache.Instance.DirectEve.BookmarkCurrentLocation(label, "", null);
                        }
                    }
                }
                else
                {
                    Logging.Log("CreateBookmark", "We already have over 100 AfterMissionSalvage bookmarks: their must be a issue processing or deleting bookmarks. No additional bookmarks will be created until the number of salvage bookmarks drops below 100.", Logging.Orange);
                }

                return;
            }
            catch (Exception ex)
            {
                Logging.Log("CreateBookmark", "Exception [" + ex + "]", Logging.Debug);
                return;
            }
        }

        //public void CreateBookmarkofWreck(IEnumerable<EntityCache> containers, string label)
        //{
        //    DirectEve.BookmarkEntity(QMCache.Instance.Containers.FirstOrDefault, "a", "a", null);
        //}

        public Func<EntityCache, int> OrderByLowestHealth()
        {
            try
            {
                return t => (int)(t.ShieldPct + t.ArmorPct + t.StructurePct);
            }
            catch (Exception ex)
            {
                Logging.Log("OrderByLowestHealth", "Exception [" + ex + "]", Logging.Debug);
                return null;
            }
        }

        //public List <long> BookMarkToDestination(DirectBookmark bookmark)
        //{
        //    Directdestination = new MissionBookmarkDestination(QMCache.Instance.GetMissionBookmark(QMCache.Instance.AgentId, "Encounter"));
        //    return List<long> destination;
        //}

        public DirectItem CheckCargoForItem(int typeIdToFind, int quantityToFind)
        {
            try
            {
                if (QMCache.Instance.CurrentShipsCargo != null && QMCache.Instance.CurrentShipsCargo.Items.Any())
                {
                    DirectItem item = QMCache.Instance.CurrentShipsCargo.Items.FirstOrDefault(i => i.TypeId == typeIdToFind && i.Quantity >= quantityToFind);
                    return item;
                }

                return null; // no items found
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.CheckCargoForItem", "Exception [" + exception + "]", Logging.Debug);
            }

            return null;
        }

        public bool CheckifRouteIsAllHighSec()
        {
            QMCache.Instance.RouteIsAllHighSecBool = false;

            try
            {
                // Find the first waypoint
                if (DirectEve.Navigation.GetDestinationPath() != null && DirectEve.Navigation.GetDestinationPath().Count > 0)
                {
                    List<int> currentPath = DirectEve.Navigation.GetDestinationPath();
                    if (currentPath == null || !currentPath.Any()) return false;
                    if (currentPath[0] == 0) return false; //No destination set - prevents exception if somehow we have got an invalid destination

                    foreach (int _system in currentPath)
                    {
                        if (_system < 60000000) // not a station
                        {
                            DirectSolarSystem solarSystemInRoute = QMCache.Instance.DirectEve.SolarSystems[_system];
                            if (solarSystemInRoute != null)
                            {
                                if (solarSystemInRoute.Security < 0.45)
                                {
                                    //Bad bad bad
                                    QMCache.Instance.RouteIsAllHighSecBool = false;
                                    return true;
                                }
                            }

                            Logging.Log("CheckifRouteIsAllHighSec", "Jump number [" + _system + "of" + currentPath.Count() + "] in the route came back as null, we could not get the system name or sec level", Logging.Debug);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Logging.Log("Cache.CheckifRouteIsAllHighSec", "Exception [" + exception + "]", Logging.Debug);
            }


            //
            // if DirectEve.Navigation.GetDestinationPath() is null or 0 jumps then it must be safe (can we assume we are not in lowsec or 0.0 already?!)
            //
            QMCache.Instance.RouteIsAllHighSecBool = true;
            return true;
        }

        //public int MyMissileProjectionSkillLevel;

        public void ClearPerPocketCache(string callingroutine)
        {
            try
            {
                if (DateTime.Now > Time.NextClearPocketCache)
                {
                    MissionSettings.ClearPocketSpecificSettings();
                    Combat._doWeCurrentlyHaveTurretsMounted = null;
                    Combat.LastTargetPrimaryWeaponsWereShooting = null;
                    Drones.LastTargetIDDronesEngaged = null;

                    _ammoHangar = null;
                    _lootHangar = null;
                    _lootContainer = null;

                    ListOfWarpScramblingEntities.Clear();
                    ListOfJammingEntities.Clear();
                    ListOfTrackingDisruptingEntities.Clear();
                    ListNeutralizingEntities.Clear();
                    ListOfTargetPaintingEntities.Clear();
                    ListOfDampenuingEntities.Clear();
                    ListofWebbingEntities.Clear();
                    ListofContainersToLoot.Clear();
                    ListofMissionCompletionItemsToLoot.Clear();
                    //Statistics.IndividualVolleyDataStatistics(QMCache.Instance.ListofEachWeaponsVolleyData);
                    //ListofEachWeaponsVolleyData.Clear();
                    ListOfUndockBookmarks = null;

                    //MyMissileProjectionSkillLevel = SkillPlan.MissileProjectionSkillLevel();

                    EntityNames.Clear();
                    EntityTypeID.Clear();
                    EntityGroupID.Clear();
                    EntityBounty.Clear();
                    EntityIsFrigate.Clear();
                    EntityIsNPCFrigate.Clear();
                    EntityIsCruiser.Clear();
                    EntityIsNPCCruiser.Clear();
                    EntityIsBattleCruiser.Clear();
                    EntityIsNPCBattleCruiser.Clear();
                    EntityIsBattleShip.Clear();
                    EntityIsNPCBattleShip.Clear();
                    EntityIsHighValueTarget.Clear();
                    EntityIsLowValueTarget.Clear();
                    EntityIsLargeCollidable.Clear();
                    EntityIsSentry.Clear();
                    EntityIsMiscJunk.Clear();
                    EntityIsBadIdea.Clear();
                    EntityIsFactionWarfareNPC.Clear();
                    EntityIsNPCByGroupID.Clear();
                    EntityIsEntutyIShouldLeaveAlone.Clear();
                    EntityHaveLootRights.Clear();
                    EntityIsStargate.Clear();

                    QMCache.Instance.LootedContainers.Clear();
                    return;
                }

                //Logging.Log("ClearPerPocketCache", "[ " + callingroutine + " ] Attempted to ClearPocketCache within 5 seconds of a previous ClearPocketCache, aborting attempt", Logging.Debug);
            }
            catch (Exception ex)
            {
                Logging.Log("ClearPerPocketCache", "Exception [" + ex + "]", Logging.Debug);
                return;
            }
            finally
            {
                Time.NextClearPocketCache = DateTime.UtcNow.AddSeconds(5);
            }
        }

        public int RandomNumber(int min, int max)
        {
            Random random = new Random();
            return random.Next(min, max);
        }

        internal List<DirectAnomalies> getAnomalies
        {
            get
            {
                List<DirectAnomalies> anomalies = new List<DirectAnomalies>();

                var anomalies_list = DirectEve.GetLocalSvc("sensorSuite").Call("GetAllSites").ToList();
                foreach (var anomaly in anomalies_list)
                {
                    try
                    {
                        string id = (string)anomaly.Attribute("id");
                        string name = (string)anomaly.Attribute("dungeonName");
                        float strength = (float)anomaly.Attribute("certainty");
                        int attributeid = (int)anomaly.Attribute("strengthAttributeID");

                        //if(strength == 1)
                        anomalies.Add(new DirectAnomalies(id, name, attributeid, strength));
                    }
                    catch (Exception ex)
                    {
                        Logging.Log("Cache.getAnomalies", "Exception [" + ex + "]", Logging.Debug);
                    }
                }

                return anomalies;
            }
        }

        public bool DebugInventoryWindows(string module)
        {
            List<DirectWindow> windows = QMCache.Instance.Windows;

            Logging.Log(module, "DebugInventoryWindows: *** Start Listing Inventory Windows ***", Logging.White);
            int windownumber = 0;
            foreach (DirectWindow window in windows)
            {
                if (window.Type.ToLower().Contains("inventory"))
                {
                    windownumber++;
                    Logging.Log(module, "----------------------------  #[" + windownumber + "]", Logging.White);
                    Logging.Log(module, "DebugInventoryWindows.Name:    [" + window.Name + "]", Logging.White);
                    Logging.Log(module, "DebugInventoryWindows.Type:    [" + window.Type + "]", Logging.White);
                    Logging.Log(module, "DebugInventoryWindows.Caption: [" + window.Caption + "]", Logging.White);
                }
            }
            Logging.Log(module, "DebugInventoryWindows: ***  End Listing Inventory Windows  ***", Logging.White);
            return true;
        }

        public DirectContainer _itemHangar { get; set; }

        public DirectContainer ItemHangar
        {
            get
            {
                try
                {
                    if (!SafeToUseStationHangars())
                    {
                        //Logging.Log("ItemHangar", "if (!SafeToUseStationHangars())", Logging.Debug);
                        return null;
                    }

                    if (!QMCache.Instance.InSpace && QMCache.Instance.InStation)
                    {
                        if (QMCache.Instance._itemHangar == null)
                        {
                            QMCache.Instance._itemHangar = QMCache.Instance.DirectEve.GetItemHangar();
                        }

                        if (QMCache.Instance.Windows.All(i => i.Type != "form.StationItems")) // look for windows via the window (via caption of form type) ffs, not what is attached to this DirectCotnainer
                        {
                            if (DateTime.UtcNow > Time.Instance.LastOpenHangar.AddSeconds(10))
                            {
                                Logging.Log("Cache.ItemHangar", "Opening ItemHangar", Logging.Debug);
                                Statistics.LogWindowActionToWindowLog("Itemhangar", "Opening ItemHangar");
                                QMCache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenHangarFloor);
                                Time.Instance.LastOpenHangar = DateTime.UtcNow;
                                return null;
                            }

                            if (Logging.DebugArm || Logging.DebugUnloadLoot || Logging.DebugHangars) Logging.Log("Cache.ItemHangar", "ItemHangar recently opened, waiting for the window to actually appear", Logging.Debug);
                            return null;
                        }

                        if (QMCache.Instance.Windows.Any(i => i.Type == "form.StationItems"))
                        {
                            if (Logging.DebugArm || Logging.DebugUnloadLoot || Logging.DebugHangars) Logging.Log("Cache.ItemHangar", "if (QMCache.Instance.Windows.Any(i => i.Type == form.StationItems))", Logging.Debug);
                            return QMCache.Instance._itemHangar;
                        }

                        if (Logging.DebugArm || Logging.DebugUnloadLoot || Logging.DebugHangars) Logging.Log("Cache.ItemHangar", "Not sure how we got here... ", Logging.Debug);
                        return null;
                    }

                    if (Logging.DebugArm || Logging.DebugUnloadLoot || Logging.DebugHangars) Logging.Log("Cache.ItemHangar", "InSpace [" + QMCache.Instance.InSpace + "] InStation [" + QMCache.Instance.InStation + "] waiting...", Logging.Debug);
                    return null;
                }
                catch (Exception ex)
                {
                    Logging.Log("ItemHangar", "Exception [" + ex + "]", Logging.Debug);
                    return null;
                }
            }

            set { _itemHangar = value; }
        }

        public bool SafeToUseStationHangars()
        {
            if (DateTime.UtcNow < Time.Instance.NextDockAction.AddSeconds(10)) //yes we are adding 10 more seconds...
            {
                if (Logging.DebugArm || Logging.DebugUnloadLoot || Logging.DebugHangars) Logging.Log("ItemHangar", "if (DateTime.UtcNow < Time.Instance.NextDockAction.AddSeconds(10))", Logging.Debug);
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(15))
            {
                if (Logging.DebugArm || Logging.DebugUnloadLoot || Logging.DebugHangars) Logging.Log("ItemHangar", "if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(15))", Logging.Debug);
                return false;
            }

            return true;
        }

        public bool ReadyItemsHangarSingleInstance(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
            {
                return false;
            }

            if (QMCache.Instance.InStation)
            {
                DirectContainerWindow lootHangarWindow = (DirectContainerWindow)QMCache.Instance.Windows.FirstOrDefault(w => w.Type.Contains("form.StationItems") && w.Caption.Contains("Item hangar"));

                // Is the items hangar open?
                if (lootHangarWindow == null)
                {
                    // No, command it to open
                    QMCache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenHangarFloor);
                    Statistics.LogWindowActionToWindowLog("Itemhangar", "Opening ItemHangar");
                    Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(QMCache.Instance.RandomNumber(3, 5));
                    Logging.Log(module, "Opening Item Hangar: waiting [" + Math.Round(Time.Instance.NextOpenHangarAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + "sec]", Logging.White);
                    return false;
                }

                QMCache.Instance.ItemHangar = QMCache.Instance.DirectEve.GetContainer(lootHangarWindow.CurrInvIdItem);
                return true;
            }

            return false;
        }

        public bool CloseItemsHangar(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
            {
                return false;
            }

            try
            {
                if (QMCache.Instance.InStation)
                {
                    if (Logging.DebugHangars) Logging.Log("OpenItemsHangar", "We are in Station", Logging.Teal);
                    QMCache.Instance.ItemHangar = QMCache.Instance.DirectEve.GetItemHangar();

                    if (QMCache.Instance.ItemHangar == null)
                    {
                        if (Logging.DebugHangars) Logging.Log("OpenItemsHangar", "ItemsHangar was null", Logging.Teal);
                        return false;
                    }

                    if (Logging.DebugHangars) Logging.Log("OpenItemsHangar", "ItemsHangar exists", Logging.Teal);

                    // Is the items hangar open?
                    if (QMCache.Instance.ItemHangar.Window == null)
                    {
                        Logging.Log(module, "Item Hangar: is closed", Logging.White);
                        return true;
                    }

                    if (!QMCache.Instance.ItemHangar.Window.IsReady)
                    {
                        if (Logging.DebugHangars) Logging.Log("OpenItemsHangar", "ItemsHangar.window is not yet ready", Logging.Teal);
                        return false;
                    }

                    if (QMCache.Instance.ItemHangar.Window.IsReady)
                    {
                        QMCache.Instance.ItemHangar.Window.Close();
                        Statistics.LogWindowActionToWindowLog("Itemhangar", "Closing ItemHangar");
                        return false;
                    }
                }
                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("CloseItemsHangar", "Unable to complete CloseItemsHangar [" + exception + "]", Logging.Teal);
                return false;
            }
        }

        public bool ReadyItemsHangarAsLootHangar(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
            {
                return false;
            }

            try
            {
                if (QMCache.Instance.InStation)
                {
                    if (Logging.DebugItemHangar) Logging.Log("ReadyItemsHangarAsLootHangar", "We are in Station", Logging.Teal);
                    QMCache.Instance.LootHangar = QMCache.Instance.ItemHangar;
                    return true;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("ReadyItemsHangarAsLootHangar", "Unable to complete ReadyItemsHangarAsLootHangar [" + exception + "]", Logging.Teal);
                return false;
            }
        }

        public bool ReadyItemsHangarAsAmmoHangar(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                if (Logging.DebugHangars) Logging.Log("ReadyItemsHangarAsAmmoHangar", "if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace)", Logging.Teal);
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
            {
                if (Logging.DebugHangars) Logging.Log("ReadyItemsHangarAsAmmoHangar", "if (DateTime.UtcNow < QMCache.Instance.NextOpenHangarAction)", Logging.Teal);
                return false;
            }

            try
            {
                if (QMCache.Instance.InStation)
                {
                    if (Logging.DebugHangars) Logging.Log("ReadyItemsHangarAsAmmoHangar", "We are in Station", Logging.Teal);
                    QMCache.Instance.AmmoHangar = QMCache.Instance.ItemHangar;
                    return true;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("ReadyItemsHangarAsAmmoHangar", "unable to complete ReadyItemsHangarAsAmmoHangar [" + exception + "]", Logging.Teal);
                return false;
            }
        }

        public bool StackItemsHangarAsLootHangar(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
            {
                return false;
            }

            try
            {
                if (Logging.DebugItemHangar) Logging.Log("StackItemsHangarAsLootHangar", "public bool StackItemsHangarAsLootHangar(String module)", Logging.Teal);

                if (QMCache.Instance.InStation)
                {
                    if (Logging.DebugHangars) Logging.Log("StackItemsHangarAsLootHangar", "if (QMCache.Instance.InStation)", Logging.Teal);
                    if (QMCache.Instance.LootHangar != null)
                    {
                        try
                        {
                            if (QMCache.Instance.StackHangarAttempts > 0)
                            {
                                if (!WaitForLockedItems(Time.Instance.LastStackLootHangar)) return false;
                                return true;
                            }

                            if (QMCache.Instance.StackHangarAttempts <= 0)
                            {
                                if (LootHangar.Items.Any() && LootHangar.Items.Count() > RandomNumber(600, 800))
                                {
                                    Logging.Log(module, "Stacking Item Hangar (as LootHangar)", Logging.White);
                                    Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(5);
                                    QMCache.Instance.LootHangar.StackAll();
                                    QMCache.Instance.StackHangarAttempts++;
                                    Time.Instance.LastStackLootHangar = DateTime.UtcNow;
                                    Time.Instance.LastStackItemHangar = DateTime.UtcNow;
                                    return false;
                                }

                                return true;
                            }

                            Logging.Log(module, "Not Stacking LootHangar", Logging.White);
                            return true;
                        }
                        catch (Exception exception)
                        {
                            Logging.Log(module, "Stacking Item Hangar failed [" + exception + "]", Logging.Teal);
                            return true;
                        }
                    }

                    if (Logging.DebugHangars) Logging.Log("StackItemsHangarAsLootHangar", "if (!QMCache.Instance.ReadyItemsHangarAsLootHangar(Cache.StackItemsHangar)) return false;", Logging.Teal);
                    if (!QMCache.Instance.ReadyItemsHangarAsLootHangar("Cache.StackItemsHangar")) return false;
                    return false;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("StackItemsHangarAsLootHangar", "Unable to complete StackItemsHangarAsLootHangar [" + exception + "]", Logging.Teal);
                return true;
            }
        }

        private static bool WaitForLockedItems(DateTime __lastAction)
        {
            if (QMCache.Instance.DirectEve.GetLockedItems().Count != 0)
            {
                if (Math.Abs(DateTime.UtcNow.Subtract(__lastAction).TotalSeconds) > 15)
                {
                    Logging.Log(_States.CurrentArmState.ToString(), "Moving Ammo timed out, clearing item locks", Logging.Orange);
                    QMCache.Instance.DirectEve.UnlockItems();
                    return false;
                }

                if (Logging.DebugUnloadLoot) Logging.Log(_States.CurrentArmState.ToString(), "Waiting for Locks to clear. GetLockedItems().Count [" + QMCache.Instance.DirectEve.GetLockedItems().Count + "]", Logging.Teal);
                return false;
            }

            QMCache.Instance.StackHangarAttempts = 0;
            return true;
        }

        public bool StackItemsHangarAsAmmoHangar(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                if (Logging.DebugHangars) Logging.Log("StackItemsHangarAsAmmoHangar", "if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace)", Logging.Teal);
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
            {
                if (Logging.DebugHangars) Logging.Log("StackItemsHangarAsAmmoHangar", "if (DateTime.UtcNow < QMCache.Instance.NextOpenHangarAction)", Logging.Teal);
                return false;
            }

            try
            {
                if (Logging.DebugItemHangar) Logging.Log("StackItemsHangarAsAmmoHangar", "public bool StackItemsHangarAsAmmoHangar(String module)", Logging.Teal);

                if (QMCache.Instance.InStation)
                {
                    if (Logging.DebugHangars) Logging.Log("StackItemsHangarAsAmmoHangar", "if (QMCache.Instance.InStation)", Logging.Teal);
                    if (QMCache.Instance.AmmoHangar != null)
                    {
                        try
                        {
                            if (QMCache.Instance.StackHangarAttempts > 0)
                            {
                                if (!WaitForLockedItems(Time.Instance.LastStackAmmoHangar)) return false;
                                return true;
                            }

                            if (QMCache.Instance.StackHangarAttempts <= 0)
                            {
                                if (AmmoHangar.Items.Any() && AmmoHangar.Items.Count() > RandomNumber(600, 800))
                                {
                                    Logging.Log(module, "Stacking Item Hangar (as AmmoHangar)", Logging.White);
                                    Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(5);
                                    QMCache.Instance.AmmoHangar.StackAll();
                                    QMCache.Instance.StackHangarAttempts++;
                                    Time.Instance.LastStackAmmoHangar = DateTime.UtcNow;
                                    Time.Instance.LastStackItemHangar = DateTime.UtcNow;
                                    return true;
                                }

                                return true;
                            }

                            Logging.Log(module, "Not Stacking AmmoHangar[" + "ItemHangar" + "]", Logging.White);
                            return true;
                        }
                        catch (Exception exception)
                        {
                            Logging.Log(module, "Stacking Item Hangar failed [" + exception + "]", Logging.Teal);
                            return true;
                        }
                    }

                    if (Logging.DebugHangars) Logging.Log("StackItemsHangarAsAmmoHangar", "if (!QMCache.Instance.ReadyItemsHangarAsAmmoHangar(Cache.StackItemsHangar)) return false;", Logging.Teal);
                    if (!QMCache.Instance.ReadyItemsHangarAsAmmoHangar("Cache.StackItemsHangar")) return false;
                    return false;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("StackItemsHangarAsAmmoHangar", "Unable to complete StackItemsHangarAsAmmoHangar [" + exception + "]", Logging.Teal);
                return true;
            }
        }

        public bool StackCargoHold(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.UtcNow < Time.Instance.LastStackCargohold.AddSeconds(90))
                return true;

            try
            {
                Logging.Log(module, "Stacking CargoHold: waiting [" + Math.Round(Time.Instance.NextOpenCargoAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + "sec]", Logging.White);
                if (QMCache.Instance.CurrentShipsCargo != null)
                {
                    try
                    {
                        if (QMCache.Instance.StackHangarAttempts > 0)
                        {
                            if (!WaitForLockedItems(Time.Instance.LastStackAmmoHangar)) return false;
                            return true;
                        }

                        if (QMCache.Instance.StackHangarAttempts <= 0)
                        {
                            if (QMCache.Instance.CurrentShipsCargo.Items.Any())
                            {
                                Time.Instance.LastStackCargohold = DateTime.UtcNow;
                                QMCache.Instance.CurrentShipsCargo.StackAll();
                                QMCache.Instance.StackHangarAttempts++;
                                return false;
                            }

                            return true;
                        }
                    }
                    catch (Exception exception)
                    {
                        Logging.Log(module, "Stacking Item Hangar failed [" + exception + "]", Logging.Teal);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("StackCargoHold", "Unable to complete StackCargoHold [" + exception + "]", Logging.Teal);
                return true;
            }
        }

        public bool CloseCargoHold(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            try
            {
                if (DateTime.UtcNow < Time.Instance.NextOpenCargoAction)
                {
                    if ((DateTime.UtcNow.Subtract(Time.Instance.NextOpenCargoAction).TotalSeconds) > 0)
                    {
                        Logging.Log("CloseCargoHold", "waiting [" + Math.Round(Time.Instance.NextOpenCargoAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + "sec]", Logging.White);
                    }

                    return false;
                }

                if (QMCache.Instance.CurrentShipsCargo == null || QMCache.Instance.CurrentShipsCargo.Window == null)
                {
                    QMCache.Instance._currentShipsCargo = null;
                    Logging.Log("CloseCargoHold", "Cargohold was not open, no need to close", Logging.White);
                    return true;
                }

                if (QMCache.Instance.InStation || QMCache.Instance.InSpace) //do we need to special case pods here?
                {
                    if (QMCache.Instance.CurrentShipsCargo.Window == null)
                    {
                        QMCache.Instance._currentShipsCargo = null;
                        Logging.Log("CloseCargoHold", "Cargohold is closed", Logging.White);
                        return true;
                    }

                    if (!QMCache.Instance.CurrentShipsCargo.Window.IsReady)
                    {
                        //Logging.Log(module, "cargo window is not ready", Logging.White);
                        return false;
                    }

                    if (QMCache.Instance.CurrentShipsCargo.Window.IsReady)
                    {
                        QMCache.Instance.CurrentShipsCargo.Window.Close();
                        Statistics.LogWindowActionToWindowLog("CargoHold", "Closing CargoHold");
                        Time.Instance.NextOpenCargoAction = DateTime.UtcNow.AddSeconds(QMCache.Instance.RandomNumber(1, 2));
                        return false;
                    }

                    QMCache.Instance._currentShipsCargo = null;
                    Logging.Log("CloseCargoHold", "Cargohold is probably closed", Logging.White);
                    return true;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("CloseCargoHold", "Unable to complete CloseCargoHold [" + exception + "]", Logging.Teal);
                return true;
            }
        }

        private DirectContainer _shipHangar;
        public DirectContainer ShipHangar
        {
            get
            {
                try
                {
                    if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                    {
                        if (Logging.DebugHangars) Logging.Log("OpenShipsHangar", "if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace)", Logging.Teal);
                        return null;
                    }

                    if (SafeToUseStationHangars() && !QMCache.Instance.InSpace && QMCache.Instance.InStation)
                    {
                        if (QMCache.Instance._shipHangar == null)
                        {
                            QMCache.Instance._shipHangar = QMCache.Instance.DirectEve.GetShipHangar();
                        }

                        if (Instance.Windows.All(i => i.Type != "form.StationShips")) // look for windows via the window (via caption of form type) ffs, not what is attached to this DirectCotnainer
                        {
                            if (DateTime.UtcNow > Time.Instance.LastOpenHangar.AddSeconds(15))
                            {
                                Statistics.LogWindowActionToWindowLog("ShipHangar", "Opening ShipHangar");
                                QMCache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenShipHangar);
                                Time.Instance.LastOpenHangar = DateTime.UtcNow;
                                return null;
                            }

                            return null;
                        }

                        if (Instance.Windows.Any(i => i.Type == "form.StationShips") && DateTime.UtcNow > Time.Instance.LastOpenHangar.AddSeconds(15))
                        {
                            return QMCache.Instance._shipHangar;
                        }

                        return null;
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    Logging.Log("OpenShipsHangar", "Exception [" + ex + "]", Logging.Debug);
                    return null;
                }
            }

            set { _shipHangar = value; }
        }

        public bool StackShipsHangar(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
                return false;

            try
            {
                if (QMCache.Instance.InStation)
                {
                    if (QMCache.Instance.ShipHangar != null && QMCache.Instance.ShipHangar.IsValid)
                    {
                        if (QMCache.Instance.StackHangarAttempts > 0)
                        {
                            if (!WaitForLockedItems(Time.Instance.LastStackShipsHangar)) return false;
                            return true;
                        }

                        if (QMCache.Instance.StackHangarAttempts <= 0)
                        {
                            if (QMCache.Instance.ShipHangar.Items.Any())
                            {
                                Logging.Log(module, "Stacking Ship Hangar", Logging.White);
                                Time.Instance.LastStackShipsHangar = DateTime.UtcNow;
                                Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(QMCache.Instance.RandomNumber(3, 5));
                                QMCache.Instance.ShipHangar.StackAll();
                                return false;
                            }

                            return true;
                        }

                    }
                    Logging.Log(module, "Stacking Ship Hangar: not yet ready: waiting [" + Math.Round(Time.Instance.NextOpenHangarAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + "sec]", Logging.White);
                    return false;
                }
                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("StackShipsHangar", "Unable to complete StackShipsHangar [" + exception + "]", Logging.Teal);
                return true;
            }
        }

        public bool CloseShipsHangar(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
                return false;

            try
            {
                if (QMCache.Instance.InStation)
                {
                    if (Logging.DebugHangars) Logging.Log("OpenShipsHangar", "We are in Station", Logging.Teal);
                    QMCache.Instance.ShipHangar = QMCache.Instance.DirectEve.GetShipHangar();

                    if (QMCache.Instance.ShipHangar == null)
                    {
                        if (Logging.DebugHangars) Logging.Log("OpenShipsHangar", "ShipsHangar was null", Logging.Teal);
                        return false;
                    }
                    if (Logging.DebugHangars) Logging.Log("OpenShipsHangar", "ShipsHangar exists", Logging.Teal);

                    // Is the items hangar open?
                    if (QMCache.Instance.ShipHangar.Window == null)
                    {
                        Logging.Log(module, "Ship Hangar: is closed", Logging.White);
                        return true;
                    }

                    if (!QMCache.Instance.ShipHangar.Window.IsReady)
                    {
                        if (Logging.DebugHangars) Logging.Log("OpenShipsHangar", "ShipsHangar.window is not yet ready", Logging.Teal);
                        return false;
                    }

                    if (QMCache.Instance.ShipHangar.Window.IsReady)
                    {
                        QMCache.Instance.ShipHangar.Window.Close();
                        Statistics.LogWindowActionToWindowLog("ShipHangar", "Close ShipHangar");
                        return false;
                    }
                }
                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("CloseShipsHangar", "Unable to complete CloseShipsHangar [" + exception + "]", Logging.Teal);
                return false;
            }
        }

        //public DirectContainer CorpAmmoHangar { get; set; }

        public bool GetCorpAmmoHangarID()
        {
            try
            {
                if (QMCache.Instance.InStation && DateTime.UtcNow > Time.Instance.LastSessionChange.AddSeconds(10))
                {
                    string CorpHangarName;
                    if (QMSettings.Instance.AmmoHangarTabName != null)
                    {
                        CorpHangarName = QMSettings.Instance.AmmoHangarTabName;
                        if (Logging.DebugHangars) Logging.Log("GetCorpAmmoHangarID", "CorpHangarName we are looking for is [" + CorpHangarName + "][ AmmoHangarID was: " + QMCache.Instance.AmmoHangarID + "]", Logging.White);
                    }
                    else
                    {
                        if (Logging.DebugHangars) Logging.Log("GetCorpAmmoHangarID", "AmmoHangar not configured: Questor will default to item hangar", Logging.White);
                        return true;
                    }

                    if (CorpHangarName != string.Empty) //&& QMCache.Instance.AmmoHangarID == -99)
                    {
                        QMCache.Instance.AmmoHangarID = -99;
                        QMCache.Instance.AmmoHangarID = QMCache.Instance.DirectEve.GetCorpHangarId(QMSettings.Instance.AmmoHangarTabName); //- 1;
                        if (Logging.DebugHangars) Logging.Log("GetCorpAmmoHangarID", "AmmoHangarID is [" + QMCache.Instance.AmmoHangarID + "]", Logging.Teal);

                        QMCache.Instance.AmmoHangar = null;
                        QMCache.Instance.AmmoHangar = QMCache.Instance.DirectEve.GetCorporationHangar((int)QMCache.Instance.AmmoHangarID);
                        if (QMCache.Instance.AmmoHangar.IsValid)
                        {
                            if (Logging.DebugHangars) Logging.Log("GetCorpAmmoHangarID", "AmmoHangar contains [" + QMCache.Instance.AmmoHangar.Items.Count() + "] Items", Logging.White);

                            //if (Logging.DebugHangars) Logging.Log("GetCorpAmmoHangarID", "AmmoHangar Description [" + QMCache.Instance.AmmoHangar.Description + "]", Logging.White);
                            //if (Logging.DebugHangars) Logging.Log("GetCorpAmmoHangarID", "AmmoHangar UsedCapacity [" + QMCache.Instance.AmmoHangar.UsedCapacity + "]", Logging.White);
                            //if (Logging.DebugHangars) Logging.Log("GetCorpAmmoHangarID", "AmmoHangar Volume [" + QMCache.Instance.AmmoHangar.Volume + "]", Logging.White);
                        }

                        return true;
                    }
                    return true;
                }
                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("GetCorpAmmoHangarID", "Unable to complete GetCorpAmmoHangarID [" + exception + "]", Logging.Teal);
                return false;
            }
        }

        public bool GetCorpLootHangarID()
        {
            try
            {
                if (QMCache.Instance.InStation && DateTime.UtcNow > Time.Instance.LastSessionChange.AddSeconds(10))
                {
                    string CorpHangarName;
                    if (QMSettings.Instance.LootHangarTabName != null)
                    {
                        CorpHangarName = QMSettings.Instance.LootHangarTabName;
                        if (Logging.DebugHangars) Logging.Log("GetCorpLootHangarID", "CorpHangarName we are looking for is [" + CorpHangarName + "][ LootHangarID was: " + QMCache.Instance.LootHangarID + "]", Logging.White);
                    }
                    else
                    {
                        if (Logging.DebugHangars) Logging.Log("GetCorpLootHangarID", "LootHangar not configured: Questor will default to item hangar", Logging.White);
                        return true;
                    }

                    if (CorpHangarName != string.Empty) //&& QMCache.Instance.LootHangarID == -99)
                    {
                        QMCache.Instance.LootHangarID = -99;
                        QMCache.Instance.LootHangarID = QMCache.Instance.DirectEve.GetCorpHangarId(QMSettings.Instance.LootHangarTabName);  //- 1;
                        if (Logging.DebugHangars) Logging.Log("GetCorpLootHangarID", "LootHangarID is [" + QMCache.Instance.LootHangarID + "]", Logging.Teal);

                        QMCache.Instance.LootHangar = null;
                        QMCache.Instance.LootHangar = QMCache.Instance.DirectEve.GetCorporationHangar((int)QMCache.Instance.LootHangarID);
                        if (QMCache.Instance.LootHangar.IsValid)
                        {
                            if (Logging.DebugHangars) Logging.Log("GetCorpLootHangarID", "LootHangar contains [" + QMCache.Instance.LootHangar.Items.Count() + "] Items", Logging.White);

                            //if (Logging.DebugHangars) Logging.Log("GetCorpLootHangarID", "LootHangar Description [" + QMCache.Instance.LootHangar.Description + "]", Logging.White);
                            //if (Logging.DebugHangars) Logging.Log("GetCorpLootHangarID", "LootHangar UsedCapacity [" + QMCache.Instance.LootHangar.UsedCapacity + "]", Logging.White);
                            //if (Logging.DebugHangars) Logging.Log("GetCorpLootHangarID", "LootHangar Volume [" + QMCache.Instance.LootHangar.Volume + "]", Logging.White);
                        }

                        return true;
                    }
                    return true;
                }
                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("GetCorpLootHangarID", "Unable to complete GetCorpLootHangarID [" + exception + "]", Logging.Teal);
                return false;
            }
        }

        public bool StackCorpAmmoHangar(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
            {
                return false;
            }

            try
            {
                if (Logging.DebugHangars) Logging.Log("StackCorpAmmoHangar", "LastStackAmmoHangar: [" + Time.Instance.LastStackAmmoHangar.AddSeconds(60) + "] DateTime.UtcNow: [" + DateTime.UtcNow + "]", Logging.Teal);

                if (QMCache.Instance.InStation)
                {
                    if (!string.IsNullOrEmpty(QMSettings.Instance.AmmoHangarTabName))
                    {
                        if (AmmoHangar != null && AmmoHangar.IsValid)
                        {
                            try
                            {
                                if (QMCache.Instance.StackHangarAttempts > 0)
                                {
                                    if (!WaitForLockedItems(Time.Instance.LastStackAmmoHangar)) return false;
                                    return true;
                                }

                                if (QMCache.Instance.StackHangarAttempts <= 0)
                                {
                                    if (AmmoHangar.Items.Any() && AmmoHangar.Items.Count() > RandomNumber(600, 800))
                                    {
                                        Logging.Log(module, "Stacking Item Hangar (as AmmoHangar)", Logging.White);
                                        Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(5);
                                        QMCache.Instance.AmmoHangar.StackAll();
                                        QMCache.Instance.StackHangarAttempts++;
                                        Time.Instance.LastStackAmmoHangar = DateTime.UtcNow;
                                        Time.Instance.LastStackItemHangar = DateTime.UtcNow;
                                        return true;
                                    }

                                    return true;
                                }

                                Logging.Log(module, "Not Stacking AmmoHangar [" + QMSettings.Instance.AmmoHangarTabName + "]", Logging.White);
                                return true;
                            }
                            catch (Exception exception)
                            {
                                Logging.Log(module, "Stacking AmmoHangar failed [" + exception + "]", Logging.Teal);
                                return true;
                            }
                        }

                        return false;
                    }

                    QMCache.Instance.AmmoHangar = null;
                    return true;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("StackCorpAmmoHangar", "Unable to complete StackCorpAmmoHangar [" + exception + "]", Logging.Teal);
                return true;
            }
        }

        //public DirectContainer CorpLootHangar { get; set; }
        public DirectContainerWindow PrimaryInventoryWindow { get; set; }

        public DirectContainerWindow corpAmmoHangarSecondaryWindow { get; set; }

        public DirectContainerWindow corpLootHangarSecondaryWindow { get; set; }

        public bool OpenInventoryWindow(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                return false;
            }

            QMCache.Instance.PrimaryInventoryWindow = (DirectContainerWindow)QMCache.Instance.Windows.FirstOrDefault(w => w.Type.Contains("form.Inventory") && w.Name.Contains("Inventory"));

            if (QMCache.Instance.PrimaryInventoryWindow == null)
            {
                if (Logging.DebugHangars) Logging.Log("debug", "QMCache.Instance.InventoryWindow is null, opening InventoryWindow", Logging.Teal);

                // No, command it to open
                QMCache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenInventory);
                Statistics.LogWindowActionToWindowLog("Inventory (main)", "Open Inventory");
                Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(QMCache.Instance.RandomNumber(2, 3));
                Logging.Log(module, "Opening Inventory Window: waiting [" + Math.Round(Time.Instance.NextOpenHangarAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + "sec]", Logging.White);
                return false;
            }

            if (QMCache.Instance.PrimaryInventoryWindow != null)
            {
                if (Logging.DebugHangars) Logging.Log("debug", "QMCache.Instance.InventoryWindow exists", Logging.Teal);
                if (QMCache.Instance.PrimaryInventoryWindow.IsReady)
                {
                    if (Logging.DebugHangars) Logging.Log("debug", "QMCache.Instance.InventoryWindow exists and is ready", Logging.Teal);
                    return true;
                }

                //
                // if the InventoryWindow "hangs" and is never ready we will hang... it would be better if we set a timer
                // and closed the inventorywindow that is not ready after 10-20seconds. (can we close a window that is in a state if !window.isready?)
                //
                return false;
            }

            return false;
        }

        public bool StackCorpLootHangar(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                if (Logging.DebugHangars) Logging.Log("StackCorpLootHangar", "if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace)", Logging.Debug);
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
            {
                if (Logging.DebugHangars) Logging.Log("StackCorpLootHangar", "if (DateTime.UtcNow < QMCache.Instance.NextOpenHangarAction)", Logging.Debug);
                return false;
            }

            try
            {
                if (QMCache.Instance.InStation)
                {
                    if (!string.IsNullOrEmpty(QMSettings.Instance.LootHangarTabName))
                    {
                        if (LootHangar != null && LootHangar.IsValid)
                        {
                            try
                            {
                                if (QMCache.Instance.StackHangarAttempts > 0)
                                {
                                    if (!WaitForLockedItems(Time.Instance.LastStackAmmoHangar)) return false;
                                    return true;
                                }

                                if (QMCache.Instance.StackHangarAttempts <= 0)
                                {
                                    if (LootHangar.Items.Any() && LootHangar.Items.Count() > RandomNumber(600, 800))
                                    {
                                        Logging.Log(module, "Stacking Item Hangar (as LootHangar)", Logging.White);
                                        Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(5);
                                        QMCache.Instance.LootHangar.StackAll();
                                        QMCache.Instance.StackHangarAttempts++;
                                        Time.Instance.LastStackLootHangar = DateTime.UtcNow;
                                        Time.Instance.LastStackItemHangar = DateTime.UtcNow;
                                        return false;
                                    }

                                    return true;
                                }

                                Logging.Log(module, "Done Stacking AmmoHangar [" + QMSettings.Instance.AmmoHangarTabName + "]", Logging.White);
                                return true;
                            }
                            catch (Exception exception)
                            {
                                Logging.Log(module, "Stacking LootHangar failed [" + exception + "]", Logging.Teal);
                                return true;
                            }
                        }

                        return false;
                    }

                    QMCache.Instance.LootHangar = null;
                    return true;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("StackCorpLootHangar", "Unable to complete StackCorpLootHangar [" + exception + "]", Logging.Teal);
                return true;
            }
        }

        public DirectContainer CorpBookmarkHangar { get; set; }

        //
        // why do we still have this in here? depreciated in favor of using the corporate bookmark system
        //
        public bool OpenCorpBookmarkHangar(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextOpenCorpBookmarkHangarAction)
            {
                return false;
            }

            if (QMCache.Instance.InStation)
            {
                QMCache.Instance.CorpBookmarkHangar = !string.IsNullOrEmpty(QMSettings.Instance.BookmarkHangar)
                                      ? QMCache.Instance.DirectEve.GetCorporationHangar(QMSettings.Instance.BookmarkHangar)
                                      : null;

                // Is the corpHangar open?
                if (QMCache.Instance.CorpBookmarkHangar != null)
                {
                    if (QMCache.Instance.CorpBookmarkHangar.Window == null)
                    {
                        // No, command it to open
                        //QMCache.Instance.DirectEve.OpenCorporationHangar();
                        Time.Instance.NextOpenCorpBookmarkHangarAction = DateTime.UtcNow.AddSeconds(2 + QMCache.Instance.RandomNumber(1, 3));
                        Logging.Log(module, "Opening Corporate Bookmark Hangar: waiting [" + Math.Round(Time.Instance.NextOpenCorpBookmarkHangarAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + "sec]", Logging.White);
                        return false;
                    }

                    if (!QMCache.Instance.CorpBookmarkHangar.Window.IsReady)
                    {
                        return false;
                    }

                    if (QMCache.Instance.CorpBookmarkHangar.Window.IsReady)
                    {
                        if (QMCache.Instance.CorpBookmarkHangar.Window.IsPrimary())
                        {
                            QMCache.Instance.CorpBookmarkHangar.Window.OpenAsSecondary();
                            return false;
                        }

                        return true;
                    }
                }
                if (QMCache.Instance.CorpBookmarkHangar == null)
                {
                    if (!string.IsNullOrEmpty(QMSettings.Instance.BookmarkHangar))
                    {
                        Logging.Log(module, "Opening Corporate Bookmark Hangar: failed! No Corporate Hangar in this station! lag?", Logging.Orange);
                    }

                    return false;
                }
            }

            return false;
        }

        public bool CloseCorpHangar(string module, string window)
        {
            try
            {
                if (QMCache.Instance.InStation && !string.IsNullOrEmpty(window))
                {
                    DirectContainerWindow corpHangarWindow = (DirectContainerWindow)QMCache.Instance.Windows.FirstOrDefault(w => w.Type.Contains("form.InventorySecondary") && w.Caption == window);

                    if (corpHangarWindow != null)
                    {
                        Logging.Log(module, "Closing Corp Window: " + window, Logging.Teal);
                        corpHangarWindow.Close();
                        Statistics.LogWindowActionToWindowLog("Corporate Hangar", "Close Corporate Hangar");
                        return false;
                    }

                    return true;
                }

                return true;
            }
            catch (Exception exception)
            {
                Logging.Log("CloseCorpHangar", "Unable to complete CloseCorpHangar [" + exception + "]", Logging.Teal);
                return false;
            }
        }

        public bool ClosePrimaryInventoryWindow(string module)
        {
            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
                return false;

            //
            // go through *every* window
            //
            try
            {
                foreach (DirectWindow window in QMCache.Instance.Windows)
                {
                    if (window.Type.Contains("form.Inventory"))
                    {
                        if (Logging.DebugHangars) Logging.Log(module, "ClosePrimaryInventoryWindow: Closing Primary Inventory Window Named [" + window.Name + "]", Logging.White);
                        window.Close();
                        Statistics.LogWindowActionToWindowLog("Inventory (main)", "Close Inventory");
                        Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddMilliseconds(500);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Logging.Log("ClosePrimaryInventoryWindow", "Unable to complete ClosePrimaryInventoryWindow [" + exception + "]", Logging.Teal);
                return false;
            }
        }

        private DirectContainer _lootContainer;

        public DirectContainer LootContainer
        {
            get
            {
                try
                {
                    if (QMCache.Instance.InStation)
                    {
                        if (_lootContainer == null)
                        {
                            if (!string.IsNullOrEmpty(QMSettings.Instance.LootContainerName))
                            {
                                //if (Logging.DebugHangars) Logging.Log("LootContainer", "Debug: if (!string.IsNullOrEmpty(QMSettings.Instance.LootContainer))", Logging.Teal);

                                if (Instance.Windows.All(i => i.Type != "form.Inventory"))
                                // look for windows via the window (via caption of form type) ffs, not what is attached to this DirectCotnainer
                                {
                                    if (DateTime.UtcNow > Time.Instance.LastOpenHangar.AddSeconds(10))
                                    {
                                        Statistics.LogWindowActionToWindowLog("Inventory", "Opening Inventory");
                                        QMCache.Instance.DirectEve.OpenInventory();
                                        Time.Instance.LastOpenHangar = DateTime.UtcNow;
                                        return null;
                                    }
                                }

                                DirectItem firstLootContainer = QMCache.Instance.LootHangar.Items.FirstOrDefault(i => i.GivenName != null && i.IsSingleton && (i.GroupId == (int)Group.FreightContainer || i.GroupId == (int)Group.AuditLogSecureContainer) && i.GivenName.ToLower() == QMSettings.Instance.LootContainerName.ToLower());
                                if (firstLootContainer == null && QMCache.Instance.LootHangar.Items.Any(i => i.IsSingleton && (i.GroupId == (int)Group.FreightContainer || i.GroupId == (int)Group.AuditLogSecureContainer)))
                                {
                                    Logging.Log("LootContainer", "Unable to find a container named [" + QMSettings.Instance.LootContainerName + "], using the available unnamed container", Logging.Teal);
                                    firstLootContainer = QMCache.Instance.LootHangar.Items.FirstOrDefault(i => i.IsSingleton && (i.GroupId == (int)Group.FreightContainer || i.GroupId == (int)Group.AuditLogSecureContainer));
                                }

                                if (firstLootContainer != null)
                                {
                                    _lootContainer = QMCache.Instance.DirectEve.GetContainer(firstLootContainer.ItemId);
                                    if (_lootContainer != null && _lootContainer.IsValid)
                                    {
                                        Logging.Log("LootContainer", "LootContainer is defined", Logging.Debug);
                                        return _lootContainer;
                                    }

                                    Logging.Log("LootContainer", "LootContainer is still null", Logging.Debug);
                                    return null;
                                }

                                Logging.Log("LootContainer", "unable to find LootContainer named [ " + QMSettings.Instance.LootContainerName.ToLower() + " ]", Logging.Orange);
                                DirectItem firstOtherContainer = QMCache.Instance.ItemHangar.Items.FirstOrDefault(i => i.GivenName != null && i.IsSingleton && i.GroupId == (int)Group.FreightContainer);

                                if (firstOtherContainer != null)
                                {
                                    Logging.Log("LootContainer", "we did however find a container named [ " + firstOtherContainer.GivenName + " ]", Logging.Orange);
                                    return null;
                                }

                                return null;
                            }

                            return null;
                        }

                        return _lootContainer;
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    Logging.Log("LootContainer", "Exception [" + ex + "]", Logging.Debug);
                    return null;
                }
            }
            set
            {
                _lootContainer = value;
            }
        }

        public bool ReadyHighTierLootContainer(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextOpenLootContainerAction)
            {
                return false;
            }

            if (QMCache.Instance.InStation)
            {
                if (!string.IsNullOrEmpty(QMSettings.Instance.LootContainerName))
                {
                    if (Logging.DebugHangars) Logging.Log("OpenLootContainer", "Debug: if (!string.IsNullOrEmpty(QMSettings.Instance.HighTierLootContainer))", Logging.Teal);

                    DirectItem firstLootContainer = QMCache.Instance.LootHangar.Items.FirstOrDefault(i => i.GivenName != null && i.IsSingleton && i.GroupId == (int)Group.FreightContainer && i.GivenName.ToLower() == QMSettings.Instance.HighTierLootContainer.ToLower());
                    if (firstLootContainer != null)
                    {
                        long highTierLootContainerID = firstLootContainer.ItemId;
                        QMCache.Instance.HighTierLootContainer = QMCache.Instance.DirectEve.GetContainer(highTierLootContainerID);

                        if (QMCache.Instance.HighTierLootContainer != null && QMCache.Instance.HighTierLootContainer.IsValid)
                        {
                            if (Logging.DebugHangars) Logging.Log(module, "HighTierLootContainer is defined (no window needed)", Logging.Debug);
                            return true;
                        }

                        if (QMCache.Instance.HighTierLootContainer == null)
                        {
                            if (!string.IsNullOrEmpty(QMSettings.Instance.LootHangarTabName))
                                Logging.Log(module, "Opening HighTierLootContainer: failed! lag?", Logging.Orange);
                            return false;
                        }

                        if (Logging.DebugHangars) Logging.Log(module, "HighTierLootContainer is not yet ready. waiting...", Logging.Debug);
                        return false;
                    }

                    Logging.Log(module, "unable to find HighTierLootContainer named [ " + QMSettings.Instance.HighTierLootContainer.ToLower() + " ]", Logging.Orange);
                    DirectItem firstOtherContainer = QMCache.Instance.ItemHangar.Items.FirstOrDefault(i => i.GivenName != null && i.IsSingleton && i.GroupId == (int)Group.FreightContainer);

                    if (firstOtherContainer != null)
                    {
                        Logging.Log(module, "we did however find a container named [ " + firstOtherContainer.GivenName + " ]", Logging.Orange);
                        return false;
                    }

                    return false;
                }

                return true;
            }

            return false;
        }

        public bool OpenAndSelectInvItem(string module, long id)
        {
            try
            {
                if (DateTime.UtcNow < Time.Instance.LastSessionChange.AddSeconds(10))
                {
                    if (Logging.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace)", Logging.Teal);
                    return false;
                }

                if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
                {
                    if (Logging.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: if (DateTime.UtcNow < NextOpenHangarAction)", Logging.Teal);
                    return false;
                }

                if (Logging.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: about to: if (!QMCache.Instance.OpenInventoryWindow", Logging.Teal);

                if (!QMCache.Instance.OpenInventoryWindow(module)) return false;

                QMCache.Instance.PrimaryInventoryWindow = (DirectContainerWindow)QMCache.Instance.Windows.FirstOrDefault(w => w.Type.Contains("form.Inventory") && w.Name.Contains("Inventory"));

                if (QMCache.Instance.PrimaryInventoryWindow != null && QMCache.Instance.PrimaryInventoryWindow.IsReady)
                {
                    if (id < 0)
                    {
                        //
                        // this also kicks in if we have no corp hangar at all in station... can we detect that some other way?
                        //
                        Logging.Log("OpenAndSelectInvItem", "Inventory item ID from tree cannot be less than 0, retrying", Logging.White);
                        return false;
                    }

                    List<long> idsInInvTreeView = QMCache.Instance.PrimaryInventoryWindow.GetIdsFromTree(false);
                    if (Logging.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: IDs Found in the Inv Tree [" + idsInInvTreeView.Count() + "]", Logging.Teal);

                    foreach (Int64 itemInTree in idsInInvTreeView)
                    {
                        if (Logging.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: itemInTree [" + itemInTree + "][looking for: " + id, Logging.Teal);
                        if (itemInTree == id)
                        {
                            if (Logging.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: Found a match! itemInTree [" + itemInTree + "] = id [" + id + "]", Logging.Teal);
                            if (QMCache.Instance.PrimaryInventoryWindow.CurrInvIdItem != id)
                            {
                                if (Logging.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: We do not have the right ID selected yet, select it now.", Logging.Teal);
                                QMCache.Instance.PrimaryInventoryWindow.SelectTreeEntryByID(id);
                                Statistics.LogWindowActionToWindowLog("Select Tree Entry", "Selected Entry on Left of Primary Inventory Window");
                                Time.Instance.NextOpenCargoAction = DateTime.UtcNow.AddMilliseconds(QMCache.Instance.RandomNumber(2000, 4400));
                                return false;
                            }

                            if (Logging.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: We already have the right ID selected.", Logging.Teal);
                            return true;
                        }

                        continue;
                    }

                    if (!idsInInvTreeView.Contains(id))
                    {
                        if (Logging.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: if (!QMCache.Instance.InventoryWindow.GetIdsFromTree(false).Contains(ID))", Logging.Teal);

                        if (id >= 0 && id <= 6 && QMCache.Instance.PrimaryInventoryWindow.ExpandCorpHangarView())
                        {
                            Logging.Log(module, "ExpandCorpHangar executed", Logging.Teal);
                            Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(4);
                            return false;
                        }

                        foreach (Int64 itemInTree in idsInInvTreeView)
                        {
                            Logging.Log(module, "ID: " + itemInTree, Logging.Red);
                        }

                        Logging.Log(module, "Was looking for: " + id, Logging.Red);
                        return false;
                    }

                    return false;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logging.Log("OpenAndSelectInvItem", "Exception [" + ex + "]", Logging.Debug);
                return false;
            }
        }

        public bool ListInvTree(string module)
        {
            try
            {
                if (DateTime.UtcNow < Time.Instance.LastSessionChange.AddSeconds(10))
                {
                    if (Logging.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace)", Logging.Teal);
                    return false;
                }

                if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
                {
                    if (Logging.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: if (DateTime.UtcNow < NextOpenHangarAction)", Logging.Teal);
                    return false;
                }

                if (Logging.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: about to: if (!QMCache.Instance.OpenInventoryWindow", Logging.Teal);

                if (!QMCache.Instance.OpenInventoryWindow(module)) return false;

                QMCache.Instance.PrimaryInventoryWindow = (DirectContainerWindow)QMCache.Instance.Windows.FirstOrDefault(w => w.Type.Contains("form.Inventory") && w.Name.Contains("Inventory"));

                if (QMCache.Instance.PrimaryInventoryWindow != null && QMCache.Instance.PrimaryInventoryWindow.IsReady)
                {
                    List<long> idsInInvTreeView = QMCache.Instance.PrimaryInventoryWindow.GetIdsFromTree(false);
                    if (Logging.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: IDs Found in the Inv Tree [" + idsInInvTreeView.Count() + "]", Logging.Teal);

                    if (QMCache.Instance.PrimaryInventoryWindow.ExpandCorpHangarView())
                    {
                        Statistics.LogWindowActionToWindowLog("Corporate Hangar", "ExpandCorpHangar executed");
                        Logging.Log(module, "ExpandCorpHangar executed", Logging.Teal);
                        Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(4);
                        return false;
                    }

                    foreach (Int64 itemInTree in idsInInvTreeView)
                    {
                        Logging.Log(module, "ID: " + itemInTree, Logging.Red);
                    }
                    return false;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logging.Log("ListInvTree", "Exception [" + ex + "]", Logging.Debug);
                return false;
            }
        }

        public bool StackLootContainer(string module)
        {
            try
            {
                if (DateTime.UtcNow.AddMinutes(10) < Time.Instance.LastStackLootContainer)
                {
                    return true;
                }

                if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                {
                    return false;
                }

                if (DateTime.UtcNow < Time.Instance.NextOpenLootContainerAction)
                {
                    return false;
                }

                if (QMCache.Instance.InStation)
                {
                    if (LootContainer.Window == null)
                    {
                        DirectItem firstLootContainer = QMCache.Instance.LootHangar.Items.FirstOrDefault(i => i.GivenName != null && i.IsSingleton && i.GroupId == (int)Group.FreightContainer && i.GivenName.ToLower() == QMSettings.Instance.LootContainerName.ToLower());
                        if (firstLootContainer != null)
                        {
                            long lootContainerID = firstLootContainer.ItemId;
                            if (!OpenAndSelectInvItem(module, lootContainerID))
                                return false;
                        }
                        else
                        {
                            return false;
                        }
                    }

                    if (LootContainer.Window == null || !LootContainer.Window.IsReady) return false;

                    if (QMCache.Instance.StackHangarAttempts > 0)
                    {
                        if (!WaitForLockedItems(Time.Instance.LastStackLootContainer)) return false;
                        return true;
                    }

                    if (QMCache.Instance.StackHangarAttempts <= 0)
                    {
                        if (QMCache.Instance.LootContainer.Items.Any())
                        {
                            Logging.Log(module, "Loot Container window named: [ " + LootContainer.Window.Name + " ] was found and its contents are being stacked", Logging.White);
                            LootContainer.StackAll();
                            Time.Instance.LastStackLootContainer = DateTime.UtcNow;
                            Time.Instance.LastStackLootHangar = DateTime.UtcNow;
                            Time.Instance.NextOpenLootContainerAction = DateTime.UtcNow.AddSeconds(2 + QMCache.Instance.RandomNumber(1, 3));
                            return false;
                        }

                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logging.Log("StackLootContainer", "Exception [" + ex + "]", Logging.Debug);
                return false;
            }
        }

        public bool CloseLootContainer(string module)
        {
            try
            {
                if (!string.IsNullOrEmpty(QMSettings.Instance.LootContainerName))
                {
                    if (Logging.DebugHangars) Logging.Log("CloseCorpLootHangar", "Debug: else if (!string.IsNullOrEmpty(QMSettings.Instance.LootContainer))", Logging.Teal);
                    DirectContainerWindow lootHangarWindow = (DirectContainerWindow)QMCache.Instance.Windows.FirstOrDefault(w => w.Type.Contains("form.Inventory") && w.Caption == QMSettings.Instance.LootContainerName);

                    if (lootHangarWindow != null)
                    {
                        lootHangarWindow.Close();
                        Statistics.LogWindowActionToWindowLog("LootHangar", "Closing LootHangar [" + QMSettings.Instance.LootHangarTabName + "]");
                        return false;
                    }

                    return true;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logging.Log("CloseLootContainer", "Exception [" + ex + "]", Logging.Debug);
                return false;
            }
        }

        private DirectContainer _oreHold;
        public DirectContainer OreHold
        {
            get
            {
                try
                {
                    if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                    {
                        if (Logging.DebugHangars) Logging.Log("OpenShipsHangar", "if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace)", Logging.Teal);
                        return null;
                    }

                    if (!QMCache.Instance.InSpace && QMCache.Instance.InStation)
                    {
                        if (QMCache.Instance._oreHold == null)
                        {
                            QMCache.Instance._oreHold = QMCache.Instance.DirectEve.GetShipsOreHold();
                        }

                        if (Instance.Windows.All(i => i.Type != "form.ActiveShipOreHold"))
                        // look for windows via the window (via caption of form type) ffs, not what is attached to this DirectCotnainer
                        {
                            if (DateTime.UtcNow > Time.Instance.LastOpenHangar.AddSeconds(10))
                            {
                                Statistics.LogWindowActionToWindowLog("OreHold", "Opening OreHold");
                                QMCache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenOreHoldOfActiveShip);
                                Time.Instance.LastOpenHangar = DateTime.UtcNow;
                            }
                        }

                        return QMCache.Instance._oreHold;
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    Logging.Log("ItemHangar", "Exception [" + ex + "]", Logging.Debug);
                    return null;
                }
            }

            set { _shipHangar = value; }
        }

        public DirectContainer _lootHangar;

        public DirectContainer LootHangar
        {
            get
            {
                try
                {
                    if (QMCache.Instance.InStation)
                    {
                        if (_lootHangar == null && DateTime.UtcNow > Time.Instance.NextOpenHangarAction)
                        {
                            if (QMSettings.Instance.LootHangarTabName != string.Empty)
                            {
                                //
                                // todo: we should check for the Inventory window w CorpHangar selected and open it if necessary!
                                //

                                QMCache.Instance.LootHangarID = -99;
                                QMCache.Instance.LootHangarID = QMCache.Instance.DirectEve.GetCorpHangarId(QMSettings.Instance.LootHangarTabName); //- 1;
                                if (Logging.DebugHangars) Logging.Log("LootHangar: GetCorpLootHangarID", "LootHangarID is [" + QMCache.Instance.LootHangarID + "]", Logging.Teal);

                                _lootHangar = null;
                                Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(2);
                                _lootHangar = QMCache.Instance.DirectEve.GetCorporationHangar((int)QMCache.Instance.LootHangarID);

                                if (_lootHangar != null && _lootHangar.IsValid) //do we have a corp hangar tab setup with that name?
                                {
                                    if (Logging.DebugHangars)
                                    {
                                        Logging.Log("LootHangar", "LootHangar is defined (no window needed)", Logging.Debug);
                                        try
                                        {
                                            if (_lootHangar.Items.Any())
                                            {
                                                int LootHangarItemCount = _lootHangar.Items.Count();
                                                if (Logging.DebugHangars) Logging.Log("LootHangar", "LootHangar [" + QMSettings.Instance.LootHangarTabName + "] has [" + LootHangarItemCount + "] items", Logging.Debug);
                                            }
                                        }
                                        catch (Exception exception)
                                        {
                                            Logging.Log("ReadyCorpLootHangar", "Exception [" + exception + "]", Logging.Debug);
                                        }
                                    }

                                    return _lootHangar;
                                }

                                Logging.Log("LootHangar", "Opening Corporate LootHangar: failed! No Corporate Hangar in this station! lag?", Logging.Orange);
                                return QMCache.Instance.ItemHangar;

                            }
                            //else if (QMSettings.Instance.AmmoHangarTabName == string.Empty && QMCache.Instance._ammoHangar != null)
                            //{
                            //    Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(2);
                            //    _lootHangar = _ammoHangar;
                            //}
                            else
                            {
                                if (Logging.DebugHangars) Logging.Log("Cache.LootHangar", "Using ItemHangar as the LootHangar", Logging.Debug);
                                Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(2);
                                _lootHangar = QMCache.Instance.ItemHangar;
                            }

                            return _lootHangar;
                        }

                        return _lootHangar;
                    }

                    return null;
                }
                catch (Exception exception)
                {
                    Logging.Log("LootHangar", "Unable to define LootHangar [" + exception + "]", Logging.Teal);
                    return null;
                }
            }
            set
            {
                _lootHangar = value;
            }
        }

        public DirectContainer HighTierLootContainer { get; set; }

        public bool CloseLootHangar(string module)
        {
            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
            {
                return false;
            }

            try
            {
                if (QMCache.Instance.InStation)
                {
                    if (!string.IsNullOrEmpty(QMSettings.Instance.LootHangarTabName))
                    {
                        QMCache.Instance.LootHangar = QMCache.Instance.DirectEve.GetCorporationHangar(QMSettings.Instance.LootHangarTabName);

                        // Is the corp loot Hangar open?
                        if (QMCache.Instance.LootHangar != null)
                        {
                            QMCache.Instance.corpLootHangarSecondaryWindow = (DirectContainerWindow)QMCache.Instance.Windows.FirstOrDefault(w => w.Type.Contains("form.InventorySecondary") && w.Caption.Contains(QMSettings.Instance.LootHangarTabName));
                            if (Logging.DebugHangars) Logging.Log("CloseCorpLootHangar", "Debug: if (QMCache.Instance.LootHangar != null)", Logging.Teal);

                            if (QMCache.Instance.corpLootHangarSecondaryWindow != null)
                            {
                                // if open command it to close
                                QMCache.Instance.corpLootHangarSecondaryWindow.Close();
                                Statistics.LogWindowActionToWindowLog("LootHangar", "Closing LootHangar [" + QMSettings.Instance.LootHangarTabName + "]");
                                Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(2 + QMCache.Instance.RandomNumber(1, 3));
                                Logging.Log(module, "Closing Corporate Loot Hangar: waiting [" + Math.Round(Time.Instance.NextOpenHangarAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + "sec]", Logging.White);
                                return false;
                            }

                            return true;
                        }

                        if (QMCache.Instance.LootHangar == null)
                        {
                            if (!string.IsNullOrEmpty(QMSettings.Instance.LootHangarTabName))
                            {
                                Logging.Log(module, "Closing Corporate Hangar: failed! No Corporate Hangar in this station! lag or setting misconfiguration?", Logging.Orange);
                                return true;
                            }
                            return false;
                        }
                    }
                    else if (!string.IsNullOrEmpty(QMSettings.Instance.LootContainerName))
                    {
                        if (Logging.DebugHangars) Logging.Log("CloseCorpLootHangar", "Debug: else if (!string.IsNullOrEmpty(QMSettings.Instance.LootContainer))", Logging.Teal);
                        DirectContainerWindow lootHangarWindow = (DirectContainerWindow)QMCache.Instance.Windows.FirstOrDefault(w => w.Type.Contains("form.InventorySecondary") && w.Caption.Contains(QMSettings.Instance.LootContainerName));

                        if (lootHangarWindow != null)
                        {
                            lootHangarWindow.Close();
                            Statistics.LogWindowActionToWindowLog("LootHangar", "Closing LootHangar [" + QMSettings.Instance.LootHangarTabName + "]");
                            return false;
                        }
                        return true;
                    }
                    else //use local items hangar
                    {
                        QMCache.Instance.LootHangar = QMCache.Instance.DirectEve.GetItemHangar();
                        if (QMCache.Instance.LootHangar == null)
                            return false;

                        // Is the items hangar open?
                        if (QMCache.Instance.LootHangar.Window != null)
                        {
                            // if open command it to close
                            QMCache.Instance.LootHangar.Window.Close();
                            Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(2 + QMCache.Instance.RandomNumber(1, 4));
                            Logging.Log(module, "Closing Item Hangar: waiting [" + Math.Round(Time.Instance.NextOpenHangarAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + "sec]", Logging.White);
                            return false;
                        }

                        return true;
                    }
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("CloseLootHangar", "Unable to complete CloseLootHangar [" + exception + "]", Logging.Teal);
                return false;
            }
        }

        public bool StackLootHangar(string module)
        {
            if (Math.Abs(DateTime.UtcNow.Subtract(Time.Instance.LastStackLootHangar).TotalMinutes) < 10)
            {
                return true;
            }

            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                if (Logging.DebugHangars) Logging.Log("StackLootHangar", "if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace)", Logging.Teal);
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
            {
                if (Logging.DebugHangars) Logging.Log("StackLootHangar", "if (DateTime.UtcNow [" + DateTime.UtcNow + "] < QMCache.Instance.NextOpenHangarAction [" + Time.Instance.NextOpenHangarAction + "])", Logging.Teal);
                return false;
            }

            try
            {
                if (QMCache.Instance.InStation)
                {
                    if (!string.IsNullOrEmpty(QMSettings.Instance.LootHangarTabName))
                    {
                        if (Logging.DebugHangars) Logging.Log("StackLootHangar", "Starting [QMCache.Instance.StackCorpLootHangar]", Logging.Teal);
                        if (!QMCache.Instance.StackCorpLootHangar("Cache.StackCorpLootHangar")) return false;
                        if (Logging.DebugHangars) Logging.Log("StackLootHangar", "Finished [QMCache.Instance.StackCorpLootHangar]", Logging.Teal);
                        return true;
                    }

                    if (!string.IsNullOrEmpty(QMSettings.Instance.LootContainerName))
                    {
                        if (Logging.DebugHangars) Logging.Log("StackLootHangar", "if (!string.IsNullOrEmpty(QMSettings.Instance.LootContainer))", Logging.Teal);
                        //if (!QMCache.Instance.StackLootContainer("Cache.StackLootContainer")) return false;
                        Logging.Log("StackLootHangar", "We do not stack containers, you will need to do so manually. StackAll does not seem to work with Primary Inventory windows.", Logging.Teal);
                        return true;
                    }

                    if (Logging.DebugHangars) Logging.Log("StackLootHangar", "!QMCache.Instance.StackItemsHangarAsLootHangar(Cache.StackLootHangar))", Logging.Teal);
                    if (!QMCache.Instance.StackItemsHangarAsLootHangar("Cache.StackItemsHangarAsLootHangar")) return false;
                    return true;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("StackLootHangar", "Unable to complete StackLootHangar [" + exception + "]", Logging.Teal);
                return true;
            }
        }

        public bool SortLootHangar(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
            {
                return false;
            }

            if (QMCache.Instance.InStation)
            {
                if (LootHangar != null && LootHangar.IsValid)
                {
                    List<DirectItem> items = QMCache.Instance.LootHangar.Items;
                    foreach (DirectItem item in items)
                    {
                        //if (item.FlagId)
                        Logging.Log(module, "Items: " + item.TypeName, Logging.White);

                        //
                        // add items with a high tier or faction to transferlist
                        //
                    }

                    //
                    // transfer items in transferlist to HighTierLootContainer
                    //
                    return true;
                }
            }

            return false;
        }

        //public DirectContainer _ammoHangar { get; set; }

        public DirectContainer _ammoHangar;

        public DirectContainer AmmoHangar
        {
            get
            {
                try
                {
                    if (QMCache.Instance.InStation)
                    {
                        if (_ammoHangar == null && DateTime.UtcNow > Time.Instance.NextOpenHangarAction)
                        {
                            if (QMSettings.Instance.AmmoHangarTabName != string.Empty)
                            {
                                QMCache.Instance.AmmoHangarID = -99;
                                QMCache.Instance.AmmoHangarID = QMCache.Instance.DirectEve.GetCorpHangarId(QMSettings.Instance.AmmoHangarTabName); //- 1;
                                if (Logging.DebugHangars) Logging.Log("AmmoHangar: GetCorpAmmoHangarID", "AmmoHangarID is [" + QMCache.Instance.AmmoHangarID + "]", Logging.Teal);

                                _ammoHangar = null;
                                Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(2);
                                _ammoHangar = QMCache.Instance.DirectEve.GetCorporationHangar((int)QMCache.Instance.AmmoHangarID);
                                Statistics.LogWindowActionToWindowLog("AmmoHangar", "AmmoHangar Defined (not opened?)");

                                if (_ammoHangar != null && _ammoHangar.IsValid) //do we have a corp hangar tab setup with that name?
                                {
                                    if (Logging.DebugHangars)
                                    {
                                        Logging.Log("AmmoHangar", "AmmoHangar is defined (no window needed)", Logging.Debug);
                                        try
                                        {
                                            if (AmmoHangar.Items.Any())
                                            {
                                                int AmmoHangarItemCount = AmmoHangar.Items.Count();
                                                if (Logging.DebugHangars) Logging.Log("AmmoHangar", "AmmoHangar [" + QMSettings.Instance.AmmoHangarTabName + "] has [" + AmmoHangarItemCount + "] items", Logging.Debug);
                                            }
                                        }
                                        catch (Exception exception)
                                        {
                                            Logging.Log("ReadyCorpAmmoHangar", "Exception [" + exception + "]", Logging.Debug);
                                        }
                                    }

                                    return _ammoHangar;
                                }

                                Logging.Log("AmmoHangar", "Opening Corporate Ammo Hangar: failed! No Corporate Hangar in this station! lag?", Logging.Orange);
                                return _ammoHangar;

                            }

                            if (QMSettings.Instance.LootHangarTabName == string.Empty && QMCache.Instance._lootHangar != null)
                            {
                                Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(2);
                                _ammoHangar = QMCache.Instance._lootHangar;
                            }
                            else
                            {
                                Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(2);
                                _ammoHangar = QMCache.Instance.ItemHangar;
                            }

                            return _ammoHangar;
                        }

                        return _ammoHangar;
                    }

                    return null;
                }
                catch (Exception exception)
                {
                    Logging.Log("AmmoHangar", "Unable to define AmmoHangar [" + exception + "]", Logging.Teal);
                    return null;
                }
            }
            set
            {
                _ammoHangar = value;
            }
        }

        public bool StackAmmoHangar(string module)
        {
            StackAmmohangarAttempts++;
            if (StackAmmohangarAttempts > 10)
            {
                Logging.Log("StackAmmoHangar", "Stacking the ammoHangar has failed: attempts [" + StackAmmohangarAttempts + "]", Logging.Teal);
                return true;
            }

            if (Math.Abs(DateTime.UtcNow.Subtract(Time.Instance.LastStackAmmoHangar).TotalMinutes) < 10)
            {
                return true;
            }

            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                if (Logging.DebugHangars) Logging.Log("StackAmmoHangar", "if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace)", Logging.Teal);
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
            {
                if (Logging.DebugHangars) Logging.Log("StackAmmoHangar", "if (DateTime.UtcNow [" + DateTime.UtcNow + "] < QMCache.Instance.NextOpenHangarAction [" + Time.Instance.NextOpenHangarAction + "])", Logging.Teal);
                return false;
            }

            try
            {
                if (QMCache.Instance.InStation)
                {
                    if (!string.IsNullOrEmpty(QMSettings.Instance.AmmoHangarTabName))
                    {
                        if (Logging.DebugHangars) Logging.Log("StackAmmoHangar", "Starting [QMCache.Instance.StackCorpAmmoHangar]", Logging.Teal);
                        if (!QMCache.Instance.StackCorpAmmoHangar(module)) return false;
                        if (Logging.DebugHangars) Logging.Log("StackAmmoHangar", "Finished [QMCache.Instance.StackCorpAmmoHangar]", Logging.Teal);
                        return true;
                    }

                    //if (!string.IsNullOrEmpty(QMSettings.Instance.LootContainer))
                    //{
                    //    if (Logging.DebugHangars) Logging.Log("StackLootHangar", "if (!string.IsNullOrEmpty(QMSettings.Instance.LootContainer))", Logging.Teal);
                    //    if (!QMCache.Instance.StackLootContainer("Cache.StackLootHangar")) return false;
                    //    StackLoothangarAttempts = 0;
                    //    return true;
                    //}

                    if (Logging.DebugHangars) Logging.Log("StackAmmoHangar", "Starting [QMCache.Instance.StackItemsHangarAsAmmoHangar]", Logging.Teal);
                    if (!QMCache.Instance.StackItemsHangarAsAmmoHangar(module)) return false;
                    if (Logging.DebugHangars) Logging.Log("StackAmmoHangar", "Finished [QMCache.Instance.StackItemsHangarAsAmmoHangar]", Logging.Teal);
                    StackAmmohangarAttempts = 0;
                    return true;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("StackAmmoHangar", "Unable to complete StackAmmoHangar [" + exception + "]", Logging.Teal);
                return true;
            }
        }

        public bool CloseAmmoHangar(string module)
        {
            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
            {
                return false;
            }

            try
            {
                if (QMCache.Instance.InStation)
                {
                    if (!string.IsNullOrEmpty(QMSettings.Instance.AmmoHangarTabName))
                    {
                        if (Logging.DebugHangars) Logging.Log("CloseCorpAmmoHangar", "Debug: if (!string.IsNullOrEmpty(QMSettings.Instance.AmmoHangar))", Logging.Teal);

                        if (QMCache.Instance.AmmoHangar == null)
                        {
                            QMCache.Instance.AmmoHangar = QMCache.Instance.DirectEve.GetCorporationHangar(QMSettings.Instance.AmmoHangarTabName);
                        }

                        // Is the corp Ammo Hangar open?
                        if (QMCache.Instance.AmmoHangar != null)
                        {
                            QMCache.Instance.corpAmmoHangarSecondaryWindow = (DirectContainerWindow)QMCache.Instance.Windows.FirstOrDefault(w => w.Type.Contains("form.InventorySecondary") && w.Caption.Contains(QMSettings.Instance.AmmoHangarTabName));
                            if (Logging.DebugHangars) Logging.Log("CloseCorpAmmoHangar", "Debug: if (QMCache.Instance.AmmoHangar != null)", Logging.Teal);

                            if (QMCache.Instance.corpAmmoHangarSecondaryWindow != null)
                            {
                                if (Logging.DebugHangars) Logging.Log("CloseCorpAmmoHangar", "Debug: if (ammoHangarWindow != null)", Logging.Teal);

                                // if open command it to close
                                QMCache.Instance.corpAmmoHangarSecondaryWindow.Close();
                                Statistics.LogWindowActionToWindowLog("Ammohangar", "Closing AmmoHangar");
                                Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(2 + QMCache.Instance.RandomNumber(1, 3));
                                Logging.Log(module, "Closing Corporate Ammo Hangar: waiting [" + Math.Round(Time.Instance.NextOpenHangarAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + "sec]", Logging.White);
                                return false;
                            }

                            return true;
                        }

                        if (QMCache.Instance.AmmoHangar == null)
                        {
                            if (!string.IsNullOrEmpty(QMSettings.Instance.AmmoHangarTabName))
                            {
                                Logging.Log(module, "Closing Corporate Hangar: failed! No Corporate Hangar in this station! lag or setting misconfiguration?", Logging.Orange);
                            }

                            return false;
                        }
                    }
                    else //use local items hangar
                    {
                        if (QMCache.Instance.AmmoHangar == null)
                        {
                            QMCache.Instance.AmmoHangar = QMCache.Instance.DirectEve.GetItemHangar();
                            return false;
                        }

                        // Is the items hangar open?
                        if (QMCache.Instance.AmmoHangar.Window != null)
                        {
                            // if open command it to close
                            if (!QMCache.Instance.CloseItemsHangar(module)) return false;
                            Logging.Log(module, "Closing AmmoHangar Hangar", Logging.White);
                            return true;
                        }

                        return true;
                    }
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("CloseAmmoHangar", "Unable to complete CloseAmmoHangar [" + exception + "]", Logging.Teal);
                return false;
            }
        }

        public DirectLoyaltyPointStoreWindow _lpStore;
        public DirectLoyaltyPointStoreWindow LPStore
        {
            get
            {
                try
                {
                    if (QMCache.Instance.InStation)
                    {
                        if (_lpStore == null)
                        {
                            if (!QMCache.Instance.InStation)
                            {
                                Logging.Log("LPStore", "Opening LP Store: We are not in station?! There is no LP Store in space, waiting...", Logging.Orange);
                                return null;
                            }

                            if (QMCache.Instance.InStation)
                            {
                                _lpStore = QMCache.Instance.Windows.OfType<DirectLoyaltyPointStoreWindow>().FirstOrDefault();

                                if (_lpStore == null)
                                {
                                    if (DateTime.UtcNow > Time.Instance.NextLPStoreAction)
                                    {
                                        Logging.Log("LPStore", "Opening loyalty point store", Logging.White);
                                        Time.Instance.NextLPStoreAction = DateTime.UtcNow.AddSeconds(QMCache.Instance.RandomNumber(30, 240));
                                        QMCache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenLpstore);
                                        Statistics.LogWindowActionToWindowLog("LPStore", "Opening LPStore");
                                        return null;
                                    }

                                    return null;
                                }

                                return _lpStore;
                            }

                            return null;
                        }

                        return _lpStore;
                    }

                    return null;
                }
                catch (Exception exception)
                {
                    Logging.Log("LPStore", "Unable to define LPStore [" + exception + "]", Logging.Teal);
                    return null;
                }
            }
            private set
            {
                _lpStore = value;
            }
        }

        public bool CloseLPStore(string module)
        {
            if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
            {
                return false;
            }

            if (!QMCache.Instance.InStation)
            {
                Logging.Log(module, "Closing LP Store: We are not in station?!", Logging.Orange);
                return false;
            }

            if (QMCache.Instance.InStation)
            {
                QMCache.Instance.LPStore = QMCache.Instance.Windows.OfType<DirectLoyaltyPointStoreWindow>().FirstOrDefault();
                if (QMCache.Instance.LPStore != null)
                {
                    Logging.Log(module, "Closing loyalty point store", Logging.White);
                    QMCache.Instance.LPStore.Close();
                    Statistics.LogWindowActionToWindowLog("LPStore", "Closing LPStore");
                    return false;
                }

                return true;
            }

            return true; //if we are not in station then the LP Store should have auto closed already.
        }

        private DirectFittingManagerWindow _fittingManagerWindow; //cleared in invalidatecache()
        public DirectFittingManagerWindow FittingManagerWindow
        {
            get
            {
                try
                {
                    if (QMCache.Instance.InStation)
                    {
                        if (_fittingManagerWindow == null)
                        {
                            if (!QMCache.Instance.InStation || QMCache.Instance.InSpace)
                            {
                                Logging.Log("FittingManager", "Opening Fitting Manager: We are not in station?! There is no Fitting Manager in space, waiting...", Logging.Debug);
                                return null;
                            }

                            if (QMCache.Instance.InStation)
                            {
                                if (QMCache.Instance.Windows.OfType<DirectFittingManagerWindow>().Any())
                                {
                                    DirectFittingManagerWindow __fittingManagerWindow = QMCache.Instance.Windows.OfType<DirectFittingManagerWindow>().FirstOrDefault();
                                    if (__fittingManagerWindow != null && __fittingManagerWindow.IsReady)
                                    {
                                        _fittingManagerWindow = __fittingManagerWindow;
                                        return _fittingManagerWindow;
                                    }
                                }

                                if (DateTime.UtcNow > Time.Instance.NextWindowAction)
                                {
                                    Logging.Log("FittingManager", "Opening Fitting Manager Window", Logging.White);
                                    Time.Instance.NextWindowAction = DateTime.UtcNow.AddSeconds(QMCache.Instance.RandomNumber(10, 24));
                                    QMCache.Instance.DirectEve.OpenFitingManager();
                                    Statistics.LogWindowActionToWindowLog("FittingManager", "Opening FittingManager");
                                    return null;
                                }

                                if (Logging.DebugFittingMgr) Logging.Log("FittingManager", "NextWindowAction is still in the future [" + Time.Instance.NextWindowAction.Subtract(DateTime.UtcNow).TotalSeconds + "] sec", Logging.Debug);
                                return null;
                            }

                            return null;
                        }

                        return _fittingManagerWindow;
                    }

                    Logging.Log("FittingManager", "Opening Fitting Manager: We are not in station?! There is no Fitting Manager in space, waiting...", Logging.Debug);
                    return null;
                }
                catch (Exception exception)
                {
                    Logging.Log("FittingManager", "Unable to define FittingManagerWindow [" + exception + "]", Logging.Teal);
                    return null;
                }
            }
            private set
            {
                _fittingManagerWindow = value;
            }
        }

        public bool CloseFittingManager(string module)
        {
            if (QMSettings.Instance.UseFittingManager)
            {
                if (DateTime.UtcNow < Time.Instance.NextOpenHangarAction)
                {
                    return false;
                }

                if (QMCache.Instance.Windows.OfType<DirectFittingManagerWindow>().FirstOrDefault() != null)
                {
                    Logging.Log(module, "Closing Fitting Manager Window", Logging.White);
                    QMCache.Instance.FittingManagerWindow.Close();
                    Statistics.LogWindowActionToWindowLog("FittingManager", "Closing FittingManager");
                    QMCache.Instance.FittingManagerWindow = null;
                    Time.Instance.NextOpenHangarAction = DateTime.UtcNow.AddSeconds(2);
                    return true;
                }

                return true;
            }

            return true;
        }

        public DirectMarketWindow MarketWindow { get; set; }

        public bool OpenMarket(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextWindowAction)
            {
                return false;
            }

            if (QMCache.Instance.InStation)
            {
                QMCache.Instance.MarketWindow = QMCache.Instance.Windows.OfType<DirectMarketWindow>().FirstOrDefault();

                // Is the Market window open?
                if (QMCache.Instance.MarketWindow == null)
                {
                    // No, command it to open
                    QMCache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenMarket);
                    Statistics.LogWindowActionToWindowLog("MarketWindow", "Opening MarketWindow");
                    Time.Instance.NextWindowAction = DateTime.UtcNow.AddSeconds(QMCache.Instance.RandomNumber(2, 4));
                    Logging.Log(module, "Opening Market Window: waiting [" + Math.Round(Time.Instance.NextWindowAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + "sec]", Logging.White);
                    return false;
                }

                return true; //if MarketWindow is not null then the window must be open.
            }

            return false;
        }

        public bool CloseMarket(string module)
        {
            if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(20) && !QMCache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                return false;
            }

            if (DateTime.UtcNow < Time.Instance.NextWindowAction)
            {
                return false;
            }

            if (QMCache.Instance.InStation)
            {
                QMCache.Instance.MarketWindow = QMCache.Instance.Windows.OfType<DirectMarketWindow>().FirstOrDefault();

                // Is the Market window open?
                if (QMCache.Instance.MarketWindow == null)
                {
                    //already closed
                    return true;
                }

                //if MarketWindow is not null then the window must be open, so close it.
                QMCache.Instance.MarketWindow.Close();
                Statistics.LogWindowActionToWindowLog("MarketWindow", "Closing MarketWindow");
                return true;
            }

            return true;
        }

        public bool OpenContainerInSpace(string module, EntityCache containerToOpen)
        {
            if (DateTime.UtcNow < Time.Instance.NextLootAction)
            {
                return false;
            }

            if (QMCache.Instance.InSpace && containerToOpen.Distance <= (int)Distances.ScoopRange)
            {
                QMCache.Instance.ContainerInSpace = QMCache.Instance.DirectEve.GetContainer(containerToOpen.Id);

                if (QMCache.Instance.ContainerInSpace != null)
                {
                    if (QMCache.Instance.ContainerInSpace.Window == null)
                    {
                        if (containerToOpen.OpenCargo())
                        {
                            Time.Instance.NextLootAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.LootingDelay_milliseconds);
                            Logging.Log(module, "Opening Container: waiting [" + Math.Round(Time.Instance.NextLootAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + " sec]", Logging.White);
                            return false;
                        }

                        return false;
                    }

                    if (!QMCache.Instance.ContainerInSpace.Window.IsReady)
                    {
                        Logging.Log(module, "Container window is not ready", Logging.White);
                        return false;
                    }

                    if (QMCache.Instance.ContainerInSpace.Window.IsPrimary())
                    {
                        Logging.Log(module, "Opening Container window as secondary", Logging.White);
                        QMCache.Instance.ContainerInSpace.Window.OpenAsSecondary();
                        Statistics.LogWindowActionToWindowLog("ContainerInSpace", "Opening ContainerInSpace");
                        Time.Instance.NextLootAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.LootingDelay_milliseconds);
                        return true;
                    }
                }

                return true;
            }
            Logging.Log(module, "Not in space or not in scoop range", Logging.Orange);
            return true;
        }

        public bool RepairItems(string module)
        {
            try
            {

                if (DateTime.UtcNow < Time.Instance.LastInSpace.AddSeconds(5) && !QMCache.Instance.InSpace || DateTime.UtcNow < Time.Instance.NextRepairItemsAction) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                {
                    //Logging.Log(module, "Waiting...", Logging.Orange);
                    return false;
                }

                if (!QMCache.Instance.Windows.Any())
                {
                    return false;
                }

                Time.Instance.NextRepairItemsAction = DateTime.UtcNow.AddSeconds(QMSettings.Instance.RandomNumber(2, 4));

                if (QMCache.Instance.InStation && !QMCache.Instance.DirectEve.HasRepairFacility())
                {
                    Logging.Log(module, "This station does not have repair facilities to use! aborting attempt to use non-existent repair facility.", Logging.Orange);
                    return true;
                }

                if (QMCache.Instance.InStation)
                {
                    DirectRepairShopWindow repairWindow = QMCache.Instance.Windows.OfType<DirectRepairShopWindow>().FirstOrDefault();

                    DirectWindow repairQuote = QMCache.Instance.GetWindowByName("Set Quantity");

                    if (doneUsingRepairWindow)
                    {
                        doneUsingRepairWindow = false;
                        if (repairWindow != null) repairWindow.Close();
                        return true;
                    }

                    foreach (DirectWindow window in QMCache.Instance.Windows)
                    {
                        if (window.Name == "modal")
                        {
                            if (!string.IsNullOrEmpty(window.Html))
                            {
                                if (window.Html.Contains("Repairing these items will cost"))
                                {
                                    if (window.Html != null) Logging.Log("RepairItems", "Content of modal window (HTML): [" + (window.Html).Replace("\n", "").Replace("\r", "") + "]", Logging.White);
                                    Logging.Log(module, "Closing Quote for Repairing All with YES", Logging.White);
                                    window.AnswerModal("Yes");
                                    doneUsingRepairWindow = true;
                                    return false;
                                }

                                if (window.Html.Contains("How much would you like to repair?"))
                                {
                                    if (window.Html != null) Logging.Log("RepairItems", "Content of modal window (HTML): [" + (window.Html).Replace("\n", "").Replace("\r", "") + "]", Logging.White);
                                    Logging.Log(module, "Closing Quote for Repairing All with OK", Logging.White);
                                    window.AnswerModal("OK");
                                    doneUsingRepairWindow = true;
                                    return false;
                                }
                            }
                        }
                    }

                    if (repairQuote != null && repairQuote.IsModal && repairQuote.IsKillable)
                    {
                        if (repairQuote.Html != null) Logging.Log("RepairItems", "Content of modal window (HTML): [" + (repairQuote.Html).Replace("\n", "").Replace("\r", "") + "]", Logging.White);
                        Logging.Log(module, "Closing Quote for Repairing All with OK", Logging.White);
                        repairQuote.AnswerModal("OK");
                        doneUsingRepairWindow = true;
                        return false;
                    }

                    if (repairWindow == null)
                    {
                        Logging.Log(module, "Opening repairshop window", Logging.White);
                        QMCache.Instance.DirectEve.OpenRepairShop();
                        Statistics.LogWindowActionToWindowLog("RepairWindow", "Opening RepairWindow");
                        Time.Instance.NextRepairItemsAction = DateTime.UtcNow.AddSeconds(QMSettings.Instance.RandomNumber(1, 3));
                        return false;
                    }

                    if (QMCache.Instance.ShipHangar == null) return false;
                    if (QMCache.Instance.ItemHangar == null) return false;
                    if (Drones.UseDrones)
                    {
                        if (Drones.DroneBay == null) return false;
                    }

                    //repair ships in ships hangar
                    List<DirectItem> repairAllItems = QMCache.Instance.ShipHangar.Items;

                    //repair items in items hangar and drone bay of active ship also
                    repairAllItems.AddRange(QMCache.Instance.ItemHangar.Items);
                    if (Drones.UseDrones)
                    {
                        repairAllItems.AddRange(Drones.DroneBay.Items);
                    }

                    if (repairAllItems.Any())
                    {
                        if (String.IsNullOrEmpty(repairWindow.AvgDamage()))
                        {
                            Logging.Log(module, "Add items to repair list", Logging.White);
                            repairWindow.RepairItems(repairAllItems);
                            Time.Instance.NextRepairItemsAction = DateTime.UtcNow.AddSeconds(QMSettings.Instance.RandomNumber(2, 4));
                            return false;
                        }

                        Logging.Log(module, "Repairing Items: repairWindow.AvgDamage: " + repairWindow.AvgDamage(), Logging.White);
                        if (repairWindow.AvgDamage() == "Avg: 0.0 % Damaged")
                        {
                            Logging.Log(module, "Repairing Items: Zero Damage: skipping repair.", Logging.White);
                            repairWindow.Close();
                            Statistics.LogWindowActionToWindowLog("RepairWindow", "Closing RepairWindow");
                            Arm.NeedRepair = false;
                            return true;
                        }

                        repairWindow.RepairAll();
                        Arm.NeedRepair = false;
                        Time.Instance.NextRepairItemsAction = DateTime.UtcNow.AddSeconds(QMSettings.Instance.RandomNumber(2, 4));
                        return false;
                    }

                    Logging.Log(module, "No items available, nothing to repair.", Logging.Orange);
                    return true;
                }
                Logging.Log(module, "Not in station.", Logging.Orange);
                return false;
            }
            catch (Exception ex)
            {
                Logging.Log("Cache.RepairItems", "Exception:" + ex.Message, Logging.White);
                return false;
            }
        }

        private IEnumerable<DirectBookmark> ListOfUndockBookmarks;

        internal static DirectBookmark _undockBookmarkInLocal;
        public DirectBookmark UndockBookmark
        {
            get
            {
                try
                {
                    if (_undockBookmarkInLocal == null)
                    {
                        if (ListOfUndockBookmarks == null)
                        {
                            if (QMSettings.Instance.UndockBookmarkPrefix != "")
                            {
                                ListOfUndockBookmarks = QMCache.Instance.BookmarksByLabel(QMSettings.Instance.UndockBookmarkPrefix);
                            }
                        }
                        if (ListOfUndockBookmarks != null && ListOfUndockBookmarks.Any())
                        {
                            ListOfUndockBookmarks = ListOfUndockBookmarks.Where(i => i.LocationId == QMCache.Instance.DirectEve.Session.LocationId).ToList();
                            _undockBookmarkInLocal = ListOfUndockBookmarks.OrderBy(i => QMCache.Instance.DistanceFromMe(i.X ?? 0, i.Y ?? 0, i.Z ?? 0)).FirstOrDefault(b => QMCache.Instance.DistanceFromMe(b.X ?? 0, b.Y ?? 0, b.Z ?? 0) < (int)Distances.NextPocketDistance);
                            if (_undockBookmarkInLocal != null)
                            {
                                return _undockBookmarkInLocal;
                            }

                            return null;
                        }

                        return null;
                    }

                    return _undockBookmarkInLocal;
                }
                catch (Exception exception)
                {
                    Logging.Log("UndockBookmark", "[" + exception + "]", Logging.Teal);
                    return null;
                }
            }
            internal set
            {
                _undockBookmarkInLocal = value;
            }

        }

        public IEnumerable<DirectBookmark> SafeSpotBookmarks
        {
            get
            {
                try
                {

                    if (_safeSpotBookmarks == null)
                    {
                        _safeSpotBookmarks = QMCache.Instance.BookmarksByLabel(QMSettings.Instance.SafeSpotBookmarkPrefix).ToList();
                    }

                    if (_safeSpotBookmarks != null && _safeSpotBookmarks.Any())
                    {
                        return _safeSpotBookmarks;
                    }

                    return new List<DirectBookmark>();
                }
                catch (Exception exception)
                {
                    Logging.Log("Cache.SafeSpotBookmarks", "Exception [" + exception + "]", Logging.Debug);
                }

                return new List<DirectBookmark>();
            }
        }

        public IEnumerable<DirectBookmark> AfterMissionSalvageBookmarks
        {
            get
            {
                try
                {
                    string _bookmarkprefix = QMSettings.Instance.BookmarkPrefix;

                    if (_States.CurrentQuestorState == QuestorState.DedicatedBookmarkSalvagerBehavior)
                    {
                        return QMCache.Instance.BookmarksByLabel(_bookmarkprefix + " ").Where(e => e.CreatedOn != null && e.CreatedOn.Value.CompareTo(AgedDate) < 0).ToList();
                    }

                    if (QMCache.Instance.BookmarksByLabel(_bookmarkprefix + " ") != null)
                    {
                        return QMCache.Instance.BookmarksByLabel(_bookmarkprefix + " ").ToList();
                    }

                    return new List<DirectBookmark>();
                }
                catch (Exception ex)
                {
                    Logging.Log("AfterMissionSalvageBookmarks", "Exception [" + ex + "]", Logging.Debug);
                    return new List<DirectBookmark>();
                }
            }
        }

        //Represents date when bookmarks are eligible for salvage. This should not be confused with when the bookmarks are too old to salvage.
        public DateTime AgedDate
        {
            get
            {
                try
                {
                    return DateTime.UtcNow.AddMinutes(-Salvage.AgeofBookmarksForSalvageBehavior);
                }
                catch (Exception ex)
                {
                    Logging.Log("AgedDate", "Exception [" + ex + "]", Logging.Debug);
                    return DateTime.UtcNow.AddMinutes(-45);
                }
            }
        }

        public DirectBookmark GetSalvagingBookmark
        {
            get
            {
                try
                {
                    if (QMCache.Instance.AllBookmarks != null && QMCache.Instance.AllBookmarks.Any())
                    {
                        List<DirectBookmark> _SalvagingBookmarks;
                        DirectBookmark _SalvagingBookmark;
                        if (Salvage.FirstSalvageBookmarksInSystem)
                        {
                            Logging.Log("CombatMissionsBehavior.BeginAftermissionSalvaging", "Salvaging at first bookmark from system", Logging.White);
                            _SalvagingBookmarks = QMCache.Instance.BookmarksByLabel(QMSettings.Instance.BookmarkPrefix + " ");
                            if (_SalvagingBookmarks != null && _SalvagingBookmarks.Any())
                            {
                                _SalvagingBookmark = _SalvagingBookmarks.OrderBy(b => b.CreatedOn).FirstOrDefault(c => c.LocationId == QMCache.Instance.DirectEve.Session.SolarSystemId);
                                return _SalvagingBookmark;
                            }

                            return null;
                        }

                        Logging.Log("CombatMissionsBehavior.BeginAftermissionSalvaging", "Salvaging at first oldest bookmarks", Logging.White);
                        _SalvagingBookmarks = QMCache.Instance.BookmarksByLabel(QMSettings.Instance.BookmarkPrefix + " ");
                        if (_SalvagingBookmarks != null && _SalvagingBookmarks.Any())
                        {
                            _SalvagingBookmark = _SalvagingBookmarks.OrderBy(b => b.CreatedOn).FirstOrDefault();
                            return _SalvagingBookmark;
                        }

                        return null;
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    Logging.Log("GetSalvagingBookmark", "Exception [" + ex + "]", Logging.Debug);
                    return null;
                }
            }
        }

        public DirectBookmark GetTravelBookmark
        {
            get
            {
                try
                {
                    DirectBookmark bm = QMCache.Instance.BookmarksByLabel(QMSettings.Instance.TravelToBookmarkPrefix).OrderByDescending(b => b.CreatedOn).FirstOrDefault(c => c.LocationId == QMCache.Instance.DirectEve.Session.SolarSystemId) ??
                                    QMCache.Instance.BookmarksByLabel(QMSettings.Instance.TravelToBookmarkPrefix).OrderByDescending(b => b.CreatedOn).FirstOrDefault() ??
                                    QMCache.Instance.BookmarksByLabel("Jita").OrderByDescending(b => b.CreatedOn).FirstOrDefault() ??
                                    QMCache.Instance.BookmarksByLabel("Rens").OrderByDescending(b => b.CreatedOn).FirstOrDefault() ??
                                    QMCache.Instance.BookmarksByLabel("Amarr").OrderByDescending(b => b.CreatedOn).FirstOrDefault() ??
                                    QMCache.Instance.BookmarksByLabel("Dodixie").OrderByDescending(b => b.CreatedOn).FirstOrDefault();

                    if (bm != null)
                    {
                        Logging.Log("CombatMissionsBehavior.BeginAftermissionSalvaging", "GetTravelBookmark [" + bm.Title + "][" + bm.LocationId + "]", Logging.White);
                    }
                    return bm;
                }
                catch (Exception ex)
                {
                    Logging.Log("GetTravelBookmark", "Exception [" + ex + "]", Logging.Debug);
                    return null;
                }
            }
        }

        public bool GateInGrid()
        {
            try
            {
                if (QMCache.Instance.AccelerationGates.FirstOrDefault() == null || !QMCache.Instance.AccelerationGates.Any())
                {
                    return false;
                }

                Time.Instance.LastAccelerationGateDetected = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                Logging.Log("GateInGrid", "Exception [" + ex + "]", Logging.Debug);
                return true;
            }
        }

        private int _bookmarkDeletionAttempt;
        public DateTime NextBookmarkDeletionAttempt = DateTime.UtcNow;

        public bool DeleteBookmarksOnGrid(string module)
        {
            try
            {
                if (DateTime.UtcNow < NextBookmarkDeletionAttempt)
                {
                    return false;
                }

                NextBookmarkDeletionAttempt = DateTime.UtcNow.AddSeconds(5 + QMSettings.Instance.RandomNumber(1, 5));

                //
                // remove all salvage bookmarks over 48hrs old - they have long since been rendered useless
                //
                DeleteUselessSalvageBookmarks(module);

                List<DirectBookmark> bookmarksInLocal = new List<DirectBookmark>(AfterMissionSalvageBookmarks.Where(b => b.LocationId == QMCache.Instance.DirectEve.Session.SolarSystemId).OrderBy(b => b.CreatedOn));
                DirectBookmark onGridBookmark = bookmarksInLocal.FirstOrDefault(b => QMCache.Instance.DistanceFromMe(b.X ?? 0, b.Y ?? 0, b.Z ?? 0) < (int)Distances.OnGridWithMe);
                if (onGridBookmark != null)
                {
                    _bookmarkDeletionAttempt++;
                    if (_bookmarkDeletionAttempt <= bookmarksInLocal.Count() + 60)
                    {
                        Logging.Log(module, "removing salvage bookmark:" + onGridBookmark.Title, Logging.White);
                        onGridBookmark.Delete();
                        Logging.Log(module, "after: removing salvage bookmark:" + onGridBookmark.Title, Logging.White);
                        NextBookmarkDeletionAttempt = DateTime.UtcNow.AddSeconds(QMCache.Instance.RandomNumber(2, 6));
                        return false;
                    }

                    if (_bookmarkDeletionAttempt > bookmarksInLocal.Count() + 60)
                    {
                        Logging.Log(module, "error removing bookmark!" + onGridBookmark.Title, Logging.White);
                        _States.CurrentQuestorState = QuestorState.Error;
                        return false;
                    }

                    return false;
                }

                _bookmarkDeletionAttempt = 0;
                Time.Instance.NextSalvageTrip = DateTime.UtcNow;
                Statistics.FinishedSalvaging = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                Logging.Log("DeleteBookmarksOnGrid", "Exception [" + ex + "]", Logging.Debug);
                return true;
            }
        }

        public bool DeleteUselessSalvageBookmarks(string module)
        {
            if (DateTime.UtcNow < NextBookmarkDeletionAttempt)
            {
                if (Logging.DebugSalvage) Logging.Log("DeleteUselessSalvageBookmarks", "NextBookmarkDeletionAttempt is still [" + NextBookmarkDeletionAttempt.Subtract(DateTime.UtcNow).TotalSeconds + "] sec in the future... waiting", Logging.Debug);
                return false;
            }

            try
            {
                //Delete bookmarks older than 2 hours.
                DateTime bmExpirationDate = DateTime.UtcNow.AddMinutes(-Salvage.AgeofSalvageBookmarksToExpire);
                List<DirectBookmark> uselessSalvageBookmarks = new List<DirectBookmark>(AfterMissionSalvageBookmarks.Where(e => e.CreatedOn != null && e.CreatedOn.Value.CompareTo(bmExpirationDate) < 0).ToList());

                DirectBookmark uselessSalvageBookmark = uselessSalvageBookmarks.FirstOrDefault();
                if (uselessSalvageBookmark != null)
                {
                    _bookmarkDeletionAttempt++;
                    if (_bookmarkDeletionAttempt <= uselessSalvageBookmarks.Count(e => e.CreatedOn != null && e.CreatedOn.Value.CompareTo(bmExpirationDate) < 0) + 60)
                    {
                        Logging.Log(module, "removing a salvage bookmark that aged more than [" + Salvage.AgeofSalvageBookmarksToExpire + "]" + uselessSalvageBookmark.Title, Logging.White);
                        NextBookmarkDeletionAttempt = DateTime.UtcNow.AddSeconds(5 + QMSettings.Instance.RandomNumber(1, 5));
                        uselessSalvageBookmark.Delete();
                        return false;
                    }

                    if (_bookmarkDeletionAttempt > uselessSalvageBookmarks.Count(e => e.CreatedOn != null && e.CreatedOn.Value.CompareTo(bmExpirationDate) < 0) + 60)
                    {
                        Logging.Log(module, "error removing bookmark!" + uselessSalvageBookmark.Title, Logging.White);
                        _States.CurrentQuestorState = QuestorState.Error;
                        return false;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Logging.Log("Cache.DeleteUselessSalvageBookmarks", "Exception:" + ex.Message, Logging.White);
            }

            return true;
        }

    }
}


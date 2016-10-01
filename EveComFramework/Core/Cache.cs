﻿#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using EveCom;
using EveComFramework.KanedaToolkit;

namespace EveComFramework.Core
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

        private Cache()
        {
            ItemVolume = new Dictionary<string, double>();
            ShipVolume = new Dictionary<string, double>();
            CachedMissions = new Dictionary<string, CachedMission>();
            AvailableAgents = new List<string>();
            ShipNames = new HashSet<string>();
            AllEntities = new List<Entity>();
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
        public Entity MyShipAsEntity = null;
        public List<Module> MyShipsModules = null;

        public IEnumerable<EveCom.Entity> AllEntities = new List<Entity>();

        private List<Entity> _hostilePilots = new List<Entity>();
        private Entity _hostilePilot = null;
        public Entity HostilePilot
        {
            get
            {
                try
                {
                    if (!Session.InSpace) return null;

                    if (_hostilePilot == null)
                    {
                        _hostilePilots = AllEntities.Where(i => i.Distance < 60000 && i.CategoryID != Category.Charge && i.GroupID != Group.Drones && i.GroupID != Group.FighterDrone && i.GroupID != Group.FighterBomber && i.GroupID != Group.Wreck).Where(a => Local.Pilots.Any(pilot => pilot.ID == a.OwnerID && pilot.Hostile())).ToList();
                        if (_hostilePilots.Any())
                        {
                                _hostilePilot = _hostilePilots.OrderByDescending(i => i.IsWarpScrambling)
                                    .ThenByDescending(i => i.GroupID == Group.Interdictor)
                                    .ThenByDescending(i => i.GroupID == Group.HeavyInterdictionCruiser)
                                    .ThenByDescending(i => i.GroupID == Group.BlackOps)
                                    .ThenByDescending(i => i.GroupID == Group.Battleship)
                                    .ThenByDescending(i => i.GroupID == Group.Cruiser)
                                    .ThenByDescending(i => i.GroupID == Group.HeavyAssaultCruiser)
                                    .ThenByDescending(i => i.GroupID == Group.AttackBattlecruiser)
                                    .ThenByDescending(i => i.GroupID == Group.CombatBattlecruiser)
                                    .FirstOrDefault();
                        }

                        if (_hostilePilot == null)
                        {
                                _hostilePilots = AllEntities.Where(i => i.CategoryID != Category.Charge && i.GroupID != Group.Drones && i.GroupID != Group.FighterDrone && i.GroupID != Group.FighterBomber && i.GroupID != Group.Wreck).Where(a => Local.Pilots.Any(pilot => pilot.ID == a.OwnerID && pilot.Hostile())).ToList();
                                if (_hostilePilots.Any())
                                {
                                    _hostilePilot = _hostilePilots.OrderByDescending(i => i.IsWarpScrambling)
                                        .ThenByDescending(i => i.GroupID == Group.Interdictor)
                                        .ThenByDescending(i => i.GroupID == Group.HeavyInterdictionCruiser)
                                        .ThenByDescending(i => i.GroupID == Group.BlackOps)
                                        .ThenByDescending(i => i.GroupID == Group.Battleship)
                                        .ThenByDescending(i => i.GroupID == Group.Cruiser)
                                        .ThenByDescending(i => i.GroupID == Group.HeavyAssaultCruiser)
                                        .ThenByDescending(i => i.GroupID == Group.AttackBattlecruiser)
                                        .ThenByDescending(i => i.GroupID == Group.CombatBattlecruiser)
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

        private Entity _anchoredBubble = null;

        public Entity AnchoredBubble
        {
            get
            {
                try
                {
                    if (_anchoredBubble == null)
                    {
                        if (!Session.InSpace) return null;

                        _anchoredBubble = AllEntities.Where(i => i.Distance < 240000).FirstOrDefault(a => a.GroupID == Group.MobileWarpDisruptor && a.SurfaceDistance < MyShip.MaxTargetRange);
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

        private Entity _warpScrambling = null;

        public Entity WarpScrambling
        {
            get
            {
                try
                {
                    if (_warpScrambling == null)
                    {
                        if (!Session.InSpace) return null;

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

        private Entity _neuting = null;

        public Entity Neuting
        {
            get
            {
                try
                {
                    if (_neuting == null)
                    {
                        if (!Session.InSpace) return null;

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

        private Entity _lcoToBlowUp = null;

        public Entity lcoToBlowUp
        {
            get
            {
                try
                {
                    if (_lcoToBlowUp == null)
                    {
                        if (!Session.InSpace) return null;
                        _lcoToBlowUp = AllEntities.FirstOrDefault(a => (a.GroupID == Group.LargeCollidableObject || a.GroupID == Group.LargeCollidableStructure) && !a.Name.ToLower().Contains("rock") && !a.Name.ToLower().Contains("stone") && !a.Name.ToLower().Contains("asteroid") && a.Distance <= 1000 && a.Exists && !a.Exploded && !a.Released);
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


        public class CachedMission
        {
            public int ContentID;
            public string Name;
            public int Level;
            public AgentMission.MissionState State;
            public AgentMission.MissionType Type;
            internal CachedMission(int ContentID, string Name, int Level, AgentMission.MissionState State, AgentMission.MissionType Type)
            {
                this.ContentID = ContentID;
                this.Name = Name;
                this.Level = Level;
                this.State = State;
                this.Type = Type;
            }
        }

        public Dictionary<string, CachedMission> CachedMissions { get; set; }

        private void ClearCachedDataEveryPulse()
        {
            _hostilePilot = null;
            _anchoredBubble = null;
            _warpScrambling = null;
            _neuting = null;
            _lcoToBlowUp = null;
        }

        #endregion

        #region States

        DateTime BookmarkUpdate = DateTime.Now;

        bool Control(object[] Params)
        {
            if ((!Session.InSpace && !Session.InStation) || !Session.Safe) return false;
            ClearCachedDataEveryPulse();

            Name = Me.Name;
            CharID = Me.CharID;

            try
            {
                AllEntities = Entity.All;
            }
            catch (Exception)
            {
                AllEntities = new List<Entity>();
            }

            try
            {
                MyShipAsEntity = MyShip.ToEntity;
            }
            catch (Exception)
            {
                MyShipAsEntity = null;
            }

            try
            {
                MyShipsModules = MyShip.Modules;
            }
            catch (Exception)
            {
                MyShipsModules = new List<Module>();
            }

            if (Bookmarks == null || BookmarkUpdate < DateTime.Now)
            {
                Bookmarks = Bookmark.All.OrderBy(a => a.Title).Select(a => a.Title).ToArray();
                CitadelBookmarks = Bookmark.All.Where(a => a.GroupID == Group.Citadel).Select(a => a.Title).ToArray();
                BookmarkUpdate = DateTime.Now.AddMinutes(1);
            }
            if (Session.InFleet) FleetMembers = Fleet.Members.Select(a => a.Name).ToArray();
            if (MyShip.CargoBay != null)
            {
                if (MyShip.CargoBay.IsPrimed)
                {
                    if (MyShip.CargoBay.Items != null && MyShip.CargoBay.Items.Any())
                    {
                        MyShip.CargoBay.Items.ForEach(a => { ItemVolume.AddOrUpdate(a.Type, a.Volume); });
                    }
                }
                else
                {
                    MyShip.CargoBay.Prime();
                    return false;
                }
            }
            if (MyShip.DroneBay != null)
            {
                if (MyShip.DroneBay.IsPrimed)
                {
                    if (MyShip.DroneBay.Items != null && MyShip.DroneBay.Items.Any())
                    {
                        MyShip.DroneBay.Items.ForEach(a => { ItemVolume.AddOrUpdate(a.Type, a.Volume); });
                    }
                }
                else
                {
                    MyShip.DroneBay.Prime();
                    return false;
                }
            }

            try
            {
                AgentMission.All.ForEach(a => { CachedMissions.AddOrUpdate(Agent.Get(a.AgentID).Name, new CachedMission(a.ContentID, a.Name, Agent.Get(a.AgentID).Level, a.State, a.Type)); });
            }
            catch (Exception){}

            AvailableAgents = Agent.MyAgents.Select(a => a.Name).ToList();
            if (Session.InStation)
            {
                if (Station.ItemHangar != null)
                {
                    if (Station.ItemHangar.IsPrimed)
                    {
                        if (Station.ItemHangar.Items != null && Station.ItemHangar.Items.Any())
                        {
                            Station.ItemHangar.Items.ForEach(a => { ItemVolume.AddOrUpdate(a.Type, a.Volume); });
                        }
                    }
                    else
                    {
                        Station.ItemHangar.Prime();
                        return false;
                    }
                }
                if (Station.ShipHangar != null)
                {
                    if (Station.ShipHangar.IsPrimed)
                    {
                        if (Station.ShipHangar.Items != null && Station.ShipHangar.Items.Any())
                        {
                            foreach (Item ship in Station.ShipHangar.Items.Where(ship => ship != null && ship.isUnpacked))
                            {
                                ShipVolume.AddOrUpdate(ship.Type, ship.Volume);
                                if (ship.Name != null) ShipNames.Add(ship.Name);
                            }
                        }
                    }
                    else
                    {
                        Station.ShipHangar.Prime();
                        return false;
                    }
                }

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
                //for (int i = 0; i <= 6; i++)
                //{
                //    if (Session.InStation && Station.CorpHangar(i) != null)
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

            if (Session.InSpace)
            {
                try
                {
                    ArmorPercent = MyShip.Armor / MyShip.MaxArmor;
                    HullPercent = MyShip.Hull / MyShip.MaxHull;
                    if (Drone.AllInSpace.Any(a => a.ToEntity != null && (a.ToEntity.ArmorPct < 100 || a.ToEntity.HullPct < 100))) DamagedDrones = true;
                }
                catch (Exception){}
            }
            return false;
        }

        #endregion
    }

}

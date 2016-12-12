#pragma warning disable 1591

namespace ILEF.Move
{
    using System.Collections.Generic;
    using System.Linq;
    using ILoveEVE.Framework;
    using ILEF.AutoModule;
    using ILEF.Caching;
    using ILEF.Core;
    using ILEF.Lookup;
    using ILEF.KanedaToolkit;
    using System;
    class Location
    {
        internal enum LocationType
        {
            SolarSystem,
            Station,
            POSStructure
        }
        internal LocationType Type { get; set; }
        internal DirectBookmark Bookmark { get; set; }
        internal int StationID { get; set; }
        internal int SolarSystem { get; set; }
        internal string ContainerName { get; set; }

        internal Location(LocationType Type, DirectBookmark Bookmark = null, int StationID = 0, int SolarSystem = 0, string ContainerName = null)
        {
            this.Type = Type;
            this.Bookmark = Bookmark;
            this.StationID = StationID;
            this.SolarSystem = SolarSystem;
            this.ContainerName = ContainerName;
        }

        public Location Clone()
        {
            return new Location(Type, Bookmark, StationID, SolarSystem, ContainerName);
        }

    }

    [Serializable]
    public class JumpbridgeData
    {
        public int SolarSystemFrom;
        public int SolarSystemTo;
        public string BookmarkName;

        public JumpbridgeData(int SolarSystemFrom, int SolarSystemTo, string BookmarkName)
        {
            this.SolarSystemFrom = SolarSystemFrom;
            this.SolarSystemTo = SolarSystemTo;
            this.BookmarkName = BookmarkName;
        }
    }

    /// <summary>
    /// Settings for the Move class
    /// </summary>
    public class MoveSettings : Settings
    {
        public bool WarpCollisionPrevention = true;
        public decimal WarpCollisionTrigger = 1;
        public decimal WarpCollisionOrbit = 5;
        public bool ApproachCollisionPrevention = true;
        public decimal ApproachCollisionTrigger = .5m;
        public decimal ApproachCollisionOrbit = .6m;
        public bool OrbitCollisionPrevention = true;
        public decimal OrbitCollisionTrigger = 5;
        public decimal OrbitCollisionOrbit = 10;
        public bool InstaWarp = false;
        public List<JumpbridgeData> Jumpbridges = new List<JumpbridgeData>();
        public bool WaitForCloakReactivationTimer = false;
    }

    /// <summary>
    /// This class handles navigation
    /// </summary>
    public class Move : State
    {

        #region Instantiation
        static Move _Instance;
        /// <summary>
        /// Singletoner
        /// </summary>
        public static Move Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new Move();
                }
                return _Instance;
            }
        }

        private Move()
        {

        }

        #endregion

        #region Variables

        /// <summary>
        /// The logger for this class
        /// </summary>
        public DirectEve DirectEve { get; set; }
        public Logger Log = new Logger("Move");
        public MoveSettings Config = new MoveSettings();
        InstaWarp InstaWarpModule = InstaWarp.Instance;
        public AutoModule AutoModule = AutoModule.Instance;
        #endregion

        #region Actions

        /// <summary>
        /// Toggle on/off the autopilot
        /// </summary>
        /// <param name="Activate">Enable = true</param>
        public void ToggleAutopilot(bool Activate = true)
        {
            Clear();
            if (Activate)
            {
                QueueState(AutoPilotPrep);
            }
        }

        /// <summary>
        /// Warp to a bookmark
        /// </summary>
        /// <param name="Bookmark">The bookmark to warp to</param>
        /// <param name="Distance">The distance to warp at.  Default: 0</param>
        public void Bookmark(DirectBookmark Bookmark, int Distance = 0)
        {
            Clear();
            QueueState(BookmarkPrep, -1, Bookmark, Distance);
        }

        /// <summary>
        /// Warp to an entity
        /// </summary>
        /// <param name="Entity">The entity to which to warp</param>
        /// <param name="Distance">The distance to warp at.  Default: 0</param>
        public void Object(EntityCache Entity, int Distance = 0)
        {
            Clear();
            QueueState(ObjectPrep, -1, Entity, Distance);
        }

        /// <summary>
        /// Activate an entity (ex: Jump gate)
        /// </summary>
        /// <param name="Entity"></param>
        public void Activate(EntityCache Entity)
        {
            Clear();
            QueueState(ActivateEntity, -1, Entity);
        }

        /// <summary>
        /// Jump through an entity (ex: Jump portal array)
        /// </summary>
        //public void Jump()
        //{
        //    if (Idle)
        //    {
        //        QueueState(JumpThroughArray);
        //    }
        //}

        #endregion

        #region States

        bool BookmarkPrep(object[] Params)
        {

            DirectBookmark Bookmark = (DirectBookmark)Params[0];
            int Distance = (int)Params[1];

            if (Bookmark == null) return true;

            if (DirectEve.Session.IsInStation)
            {
                if (DirectEve.Session.StationId == Bookmark.ItemId)
                {
                    return true;
                }
                QueueState(Undock);
                QueueState(BookmarkPrep, -1, Bookmark, Distance);
                return true;
            }
            if (Bookmark.LocationId != DirectEve.Session.SolarSystemId)
            {
                if (Route.Path.Last() != Bookmark.LocationId)
                {
                    Log.Log("|oSetting course");
                    Log.Log(" |-g{0}", Bookmark.Title);
                    Bookmark.SetDestination();
                }
                QueueState(AutoPilot, 2000);
            }
            if (Bookmark.Dockable() && Bookmark.LocationId == DirectEve.Session.SolarSystemId)
            {
                AutoModule.PrepareToDock();
                QueueState(Dock, -1, QMCache.Instance.Entities.FirstOrDefault(a => a.Id == Bookmark.ItemId));
            }
            else
            {
                QueueState(BookmarkWarp, -1, Bookmark, Distance);
            }
            return true;
        }

        bool BookmarkWarp(object[] Params)
        {
            DirectBookmark BMDestination = (DirectBookmark)Params[0];
            int Distance = (int)Params[1];
            EntityCache Collision = null;
            if (Params.Count() > 2) Collision = (EntityCache)Params[2];

            if (QMCache.Instance.InStation)
            {
                if (BMDestination.ItemId != DirectEve.Session.StationId)
                {
                    InsertState(BookmarkWarp, -1, BMDestination, Distance);
                    InsertState(Undock);
                }
                return true;
            }
            if (!QMCache.Instance.InSpace || QMCache.Instance.MyShipEntity == null)
            {
                return false;
            }
            if (QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping)
            {
                return false;
            }
            if (BMDestination.LocationId != DirectEve.Session.StationId)
            {
                if (Route.Path.Last() != BMDestination.LocationId)
                {
                    Log.Log("|oSetting course");
                    Log.Log(" |-g{0}", BMDestination.Title);
                    BMDestination.SetDestination();
                }
                DislodgeCurState(AutoPilot, 2000);
                return false;
            }

            try
            {
                double BMDistanceFromMe = QMCache.Instance.DistanceFromMe((double)BMDestination.X, (double)BMDestination.Y, (double)BMDestination.Z);
                if (BMDistanceFromMe < Constants.WarpMinDistance && BMDistanceFromMe > 0)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Log("Exception [" + ex + "]");
            }

            if (!Config.WarpCollisionPrevention)
            {
                DoInstaWarp();
                if (BMDestination.Dockable() && BMDestination.LocationId == DirectEve.Session.SolarSystemId)
                {
                    AutoModule.PrepareToDock();
                    QueueState(Dock, -1, QMCache.Instance.Entities.FirstOrDefault(a => a.Id == BMDestination.ItemId));
                    return true;
                }
                else
                {
                    Log.Log("|oWarping");
                    Log.Log(" |-g{0} (|w{1} km|-g)", BMDestination.Title, Distance);
                    BMDestination.WarpTo(Distance);
                    InsertState(BookmarkWarp, -1, BMDestination, Distance);
                    WaitFor(10, () => QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping);
                    return true;
                }
            }

            EntityCache LCO = QMCache.Instance.Entities.FirstOrDefault(a => a.Collidable() && a.Distance <= (double)(Config.WarpCollisionTrigger * 900));
            EntityCache LCO2 = QMCache.Instance.Entities.FirstOrDefault(a => a.Collidable() && a.Distance <= (double)(Config.WarpCollisionTrigger * 500));
            if (LCO != null && Collision == null)
            {
                Collision = LCO;
                Log.Log("|oToo close for warp, orbiting");
                Log.Log(" |-g{0}(|w{1} km|-g)", Collision.Name, Config.WarpCollisionOrbit);
                Collision.Orbit((int)(Config.WarpCollisionOrbit * 1000));
                InsertState(BookmarkWarp, -1, BMDestination, Distance, Collision);
            }
            // Else, if we're in half trigger of a structure that isn't our current collision target, change orbit and collision target to it
            else if (LCO2 != null && Collision != null && Collision != LCO2)
            {
                Collision = LCO2;
                Log.Log("|oOrbiting");
                Log.Log(" |-g{0}(|w{1} km|-g)", Collision.Name, Config.WarpCollisionOrbit);
                Collision.Orbit((int)(Config.WarpCollisionOrbit * 1000));
                InsertState(BookmarkWarp, -1, BMDestination, Distance, Collision);
            }
            else if (LCO == null)
            {
                if (BMDestination.Exists && BMDestination.CanWarpTo)
                {
                    DoInstaWarp();
                    if (BMDestination.Dockable() && BMDestination.LocationId == DirectEve.Session.StationId)
                    {
                        AutoModule.PrepareToDock();
                        QueueState(Dock, -1, QMCache.Instance.Entities.FirstOrDefault(a => a.Id == BMDestination.ItemId));
                        return true;
                    }
                    else
                    {
                        Log.Log("|oWarping");
                        Log.Log(" |-g{0} (|w{1} km|-g)", BMDestination.Title, Distance);
                        BMDestination.WarpTo(Distance);
                        InsertState(BookmarkWarp, -1, BMDestination, Distance);
                        WaitFor(10, () => QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping);
                        return true;
                    }
                }
            }
            else if (Collision == LCO)
            {
                InsertState(BookmarkWarp, -1, BMDestination, Distance, Collision);
            }

            return true;
        }

        bool ObjectPrep(object[] Params)
        {
            EntityCache Entity = (EntityCache)Params[0];
            int Distance = (int)Params[1];

            if (Entity.Dockable())
            {
                AutoModule.PrepareToDock();
                QueueState(Dock, -1, Entity);
            }
            else
            {
                QueueState(ObjectWarp, -1, Entity, Distance);
            }
            return true;
        }

        bool ObjectWarp(object[] Params)
        {
            EntityCache Entity = (EntityCache)Params[0];
            int Distance = (int)Params[1];
            EntityCache Collision = null;
            if (Params.Count() > 2) Collision = (EntityCache)Params[2];

            if (!QMCache.Instance.InSpace)
            {
                return true;
            }
            if (QMCache.Instance.MyShipEntity != null && QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping)
            {
                return false;
            }
            if (Entity.Distance < Constants.WarpMinDistance && Entity.Distance > 0)
            {
                return true;
            }
            if (!Config.WarpCollisionPrevention)
            {
                DoInstaWarp();
                Log.Log("|oWarping");
                Log.Log(" |-g{0} (|w{1} km|-g)", Entity.Name, Distance);
                Entity.WarpTo(Distance);
                InsertState(ObjectWarp, -1, Entity, Distance);
                WaitFor(10, () => QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping);
                return true;
            }

            EntityCache LCO = QMCache.Instance.Entities.FirstOrDefault(a => a.Collidable() && a.Distance <= (double)(Config.WarpCollisionTrigger * 900));
            EntityCache LCO2 = QMCache.Instance.Entities.FirstOrDefault(a => a.Collidable() && a.Distance <= (double)(Config.WarpCollisionTrigger * 500));
            if (LCO != null && Collision == null)
            {
                Collision = LCO;
                Log.Log("|oToo close for warp, orbiting");
                Log.Log(" |-g{0}(|w{1} km|-g)", Collision.Name, Config.WarpCollisionOrbit);
                Collision.Orbit((int)(Config.WarpCollisionOrbit * 1000));
                InsertState(ObjectWarp, -1, Entity, Distance, Collision);
            }
            // Else, if we're in half trigger of a structure that isn't our current collision target, change orbit and collision target to it
            else if (LCO2 != null && Collision != null && Collision != LCO2)
            {
                Collision = LCO2;
                Log.Log("|oOrbiting");
                Log.Log(" |-g{0}(|w{1} km|-g)", Collision.Name, Config.WarpCollisionOrbit);
                Collision.Orbit((int)(Config.WarpCollisionOrbit * 1000));
                InsertState(ObjectWarp, -1, Entity, Distance, Collision);
            }
            else if (LCO == null)
            {
                if (Entity.IsValid && Entity.Distance > Constants.WarpMinDistance)
                {
                    DoInstaWarp();
                    Log.Log("|oWarping");
                    Log.Log(" |-g{0} (|w{1} km|-g)", Entity.Name, Distance);
                    Entity.WarpTo(Distance);
                    InsertState(ObjectWarp, -1, Entity, Distance);
                    WaitFor(10, () => QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping);
                }
            }
            else if (Collision == LCO)
            {
                InsertState(ObjectWarp, -1, Entity, Distance, Collision);
            }

            return true;
        }

        public bool Undock(object[] Params)
        {
            if (QMCache.Instance.InSpace)
            {
                Log.Log("|oUndock complete");
                return true;
            }

            Log.Log("|oUndocking");
            Log.Log(" |-g{0}", DirectEve.Session.StationName);
            AutoModule.PrepareToUnDock();
            DirectEve.ExecuteCommand(DirectCmd.CmdExitStation);
            InsertState(Undock);
            WaitFor(20, () => QMCache.Instance.InSpace);
            return true;
        }

        /**
        bool JumpThroughArray(object[] Params)
        {
            EntityCache JumpPortalArray = QMCache.Instance.Entities.FirstOrDefault(a => a.GroupId == (int)Group.JumpPortalArray);
            if (JumpPortalArray == null)
            {
                Log.Log("|yNo Jump Portal Array on grid");
                return true;
            }
            if (JumpPortalArray.Distance > 2500)
            {
                ApproachTarget = JumpPortalArray;
                ApproachDistance = 2500;
                InsertState(JumpThroughArray);
                InsertState(ApproachState);
                return true;
            }
            Log.Log("|oJumping through");
            Log.Log(" |-g{0}", JumpPortalArray.Name);
            JumpPortalArray.JumpThroughPortal();
            InsertState(JumpThroughArray);
            long CurSystem = (long)DirectEve.Session.SolarSystemId;
            WaitFor(10, () => DirectEve.Session.SolarSystemId != CurSystem, () => QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Approaching);
            return true;
        }
        **/
        bool ActivateEntity(object[] Params)
        {
            EntityCache Target = (EntityCache)Params[0];
            if (Target == null || !Target.IsValid) return true;
            if (Target.Distance > 2500)
            {
                Clear();
                ApproachTarget = Target;
                ApproachDistance = 2500;
                Approaching = false;
                QueueState(ApproachState);
                QueueState(ActivateEntity, -1, Target);
                return false;
            }
            Log.Log("|oActivating");
            Log.Log(" |-g{0}", Target.Name);

            Target.Activate();

            WaitFor(30, () => QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping);
            return true;
        }

        #region Approach

        /// <summary>
        /// Approach an entity
        /// </summary>
        /// <param name="Target">The entity to approach</param>
        /// <param name="Distance">What distance from the entity to stop at</param>
        public void Approach(EntityCache Target, int Distance = 1000)
        {
            // If we're not doing anything, just start ApproachState
            if (Idle)
            {
                ApproachTarget = Target;
                ApproachDistance = Distance;
                Approaching = false;
                ApproachCollision = null;
                QueueState(ApproachState);
                return;
            }
            // If we're approaching something else or orbiting something, change to approaching the new target - retain collision information!
            if ((CurState.State == ApproachState && ApproachTarget != Target) || (CurState.State == OrbitState && ApproachCollision == null))
            {
                Approaching = false;
                ApproachTarget = Target;
                ApproachDistance = Distance;
                Clear();
                QueueState(ApproachState, -1, Target, Distance, false);
            }
        }

        EntityCache ApproachTarget;
        int ApproachDistance;
        bool Approaching;
        EntityCache ApproachCollision;

        bool ApproachState(object[] Params)
        {
            if (!QMCache.Instance.InSpace || QMCache.Instance.MyShipEntity == null)
            {
                return false;
            }

            if (ApproachTarget == null || !ApproachTarget.IsValid || ApproachTarget.HasExploded || ApproachTarget.HasReleased)
            {
                return true;
            }

            if (QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping)
            {
                return false;
            }

            if ((ApproachTarget.CategoryId == (int)CategoryID.Asteroid ? ApproachTarget.Distance : ApproachTarget.Distance) > ApproachDistance)
            {

                // Start approaching our approach target if we're not currently approaching anything
                if (!Approaching && ApproachCollision == null)
                {
                    if (ApproachTarget.Distance > Constants.WarpMinDistance && ApproachTarget.Warpable())
                    {
                        DoInstaWarp();
                        Log.Log("|oWarping");
                        Log.Log(" |-g{0}(|w{1} km|-g)", ApproachTarget.Name, ApproachDistance / 1000);
                        ApproachTarget.WarpTo(ApproachDistance);
                        DislodgeWaitFor(10, () => QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping);
                        return false;
                    }

                    Approaching = true;
                    Log.Log("|oApproaching");
                    Log.Log(" |-g{0}(|w{1} km|-g)", ApproachTarget.Name, ApproachDistance / 1000);
                    ApproachTarget.Approach();
                    DislodgeWaitFor(10, () => QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Approaching);
                    return false;
                }

                if (Config.ApproachCollisionPrevention)
                {
                    // Else, if we're in trigger of a structure and aren't already orbiting a structure, orbit it and set it as our collision target
                    EntityCache CollisionCheck = QMCache.Instance.Entities.FirstOrDefault(a => (a.GroupId == (int)Group.LargeCollidableObject || a.GroupId == (int)Group.LargeCollidableShip || a.GroupId == (int)Group.LargeCollidableStructure) && a.TypeName != "Beacon" && a.Distance <= (double)(Config.ApproachCollisionTrigger * 900));
                    if (CollisionCheck != null && ApproachCollision == null)
                    {
                        ApproachCollision = CollisionCheck;
                        Log.Log("|oOrbiting");
                        Log.Log(" |-g{0}(|w{1} km|-g)", ApproachCollision.Name, Config.ApproachCollisionOrbit);
                        ApproachCollision.Orbit((int)(Config.ApproachCollisionOrbit * 1000));
                        return false;
                    }
                    // Else, if we're in half trigger of a structure that isn't our current collision target, change orbit and collision target to it
                    CollisionCheck = QMCache.Instance.Entities.FirstOrDefault(a => (a.GroupId == (int)Group.LargeCollidableObject || a.GroupId == (int)Group.LargeCollidableShip || a.GroupId == (int)Group.LargeCollidableStructure) && a.TypeName != "Beacon" && a.Distance <= (double)(Config.ApproachCollisionTrigger * 500));
                    if (CollisionCheck != null && CollisionCheck != ApproachCollision)
                    {
                        ApproachCollision = CollisionCheck;
                        Log.Log("|oOrbiting");
                        Log.Log(" |-g{0}(|w{1} km|-g)", ApproachCollision.Name, Config.ApproachCollisionOrbit);
                        ApproachCollision.Orbit((int)(Config.ApproachCollisionOrbit * 1000));
                        return false;
                    }
                    // Else, if we're not within trigger of a structure and we have a collision target (orbiting a structure) change approach back to our approach target
                    CollisionCheck = QMCache.Instance.Entities.FirstOrDefault(a => (a.GroupId == (int)Group.LargeCollidableObject || a.GroupId == (int)Group.LargeCollidableShip || a.GroupId == (int)Group.LargeCollidableStructure) && a.TypeName != "Beacon" && a.Distance <= (double)(Config.ApproachCollisionTrigger * 900));
                    if (CollisionCheck == null && ApproachCollision != null)
                    {
                        ApproachCollision = null;
                        Log.Log("|oApproaching");
                        Log.Log(" |-g{0}(|w{1} km|-g)", ApproachTarget.Name, ApproachDistance / 1000);
                        ApproachTarget.Approach();
                        return false;
                    }
                }
            }
            else
            {
                if (QMCache.Instance.MyShipEntity.Velocity > 0)
                {
                    DirectEve.ExecuteCommand(DirectCmd.CmdStopShip);
                }

                return true;
            }

            return false;
        }

        #endregion

        int LastOrbitDistance;
        /// <summary>
        /// Orbit an entity
        /// </summary>
        /// <param name="Target">The entity to orbit</param>
        /// <param name="Distance">The distance from the entity to orbit</param>
        public void Orbit(EntityCache Target, int Distance = 1000)
        {
            // If we're not doing anything, just start OrbitState
            if (Idle)
            {
                LastOrbitDistance = Distance;
                QueueState(OrbitState, -1, Target, Distance, false);
                return;
            }
            // If we're orbiting something else or approaching something, change to orbiting the new target - retain collision information!
            if ((CurState.State == OrbitState && (EntityCache)CurState.Params[0] != Target) || CurState.State == ApproachState)
            {
                Clear();
                LastOrbitDistance = Distance;
                QueueState(OrbitState, -1, Target, Distance, false);
            }

            if (Distance != LastOrbitDistance && LastOrbitDistance != 0 && Distance != 0)
            {
                Clear();
                LastOrbitDistance = Distance;
                QueueState(OrbitState, -1, Target, Distance, false);
            }
        }

        bool OrbitState(object[] Params)
        {
            EntityCache Target = ((EntityCache)Params[0]);
            int Distance = (int)Params[1];
            bool Orbiting = (bool)Params[2];
            EntityCache Collision = null;
            if (Params.Count() > 3) { Collision = (EntityCache)Params[3]; }

            if (!QMCache.Instance.InSpace || QMCache.Instance.MyShipEntity == null)
            {
                return false;
            }

            if (Target == null || !Target.IsValid || Target.HasExploded || Target.HasReleased)
            {
                return true;
            }

            // Start orbiting our orbit target if we're not currently orbiting anything
            if (!Orbiting || QMCache.Instance.MyShipEntity.EntityMode != EntityMode.Orbiting)
            {
                Log.Log("|oOrbiting");
                Log.Log(" |-g{0}(|w{1} km|-g)", Target.Name, Distance / 1000);
                Target.Orbit(Distance);
                InsertState(OrbitState, -1, Target, Distance, true);
                WaitFor(10, () => QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Orbiting);
            }
            else
            {
                EntityCache LCO = QMCache.Instance.Entities.FirstOrDefault(a => a.Collidable() && a.Distance <= (double)(Config.OrbitCollisionTrigger * 900));
                EntityCache LCO2 = QMCache.Instance.Entities.FirstOrDefault(a => a.Collidable() && a.Distance <= (double)(Config.OrbitCollisionTrigger * 500));
                // Else, if we're in trigger of a structure and aren't already orbiting a structure, orbit it and set it as our collision target
                if (Config.OrbitCollisionPrevention)
                {
                    if (LCO != null && Collision == null)
                    {
                        Collision = LCO;
                        Log.Log("|oOrbiting");
                        Log.Log(" |-g{0}(|w{1} km|-g)", Collision.Name, Config.OrbitCollisionOrbit);
                        Collision.Orbit((int)(Config.OrbitCollisionOrbit * 1000));
                        InsertState(OrbitState, -1, Target, Distance, true, Collision);
                    }
                    // Else, if we're in half trigger of a structure that isn't our current collision target, change orbit and collision target to it
                    else if (LCO2 != null && Collision != null && Collision != LCO2)
                    {
                        Collision = LCO2;
                        Log.Log("|oOrbiting");
                        Log.Log(" |-g{0}(|w{1} km|-g)", Collision.Name, Config.OrbitCollisionOrbit);
                        Collision.Orbit((int)(Config.OrbitCollisionOrbit * 1000));
                        InsertState(OrbitState, -1, Target, Distance, true, Collision);
                    }
                    // Else, if we're not within 1km of a structure and we have a collision target (orbiting a structure) change orbit back to our orbit target
                    else if (LCO == null && Collision != null)
                    {
                        Log.Log("|oOrbiting");
                        Log.Log(" |-g{0}(|w{1} km|-g)", Target.Name, Distance / 1000);
                        Target.Orbit(Distance);
                        InsertState(OrbitState, -1, Target, Distance, true);
                        WaitFor(10, () => QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Orbiting);
                    }
                    else
                    {
                        InsertState(OrbitState, -1, Target, Distance, true);
                    }
                }
                else
                {
                    InsertState(OrbitState, -1, Target, Distance, true);
                }
            }
            return true;

        }

        readonly Dictionary<int, int> Bubbles = new Dictionary<int, int> {
                        {12200, 26500},
                        {26888, 40000},
                        {12199, 11500},
                        {26890, 17500},
                        {12198, 5000},
                        {26892, 7500},
                        {22778, 20000}
                        };

        readonly List<int> NullificationSubsystems = new List<int>()
        {
            30082,  // Legion
            30092,  // Tengu
            30102,  // Proteus
            30112   // Loki
        };

        bool Bubbled()
        {
            if (QMCache.Instance.MyShipEntity.GroupId == (int)Group.Interceptor) return false;
            if (QMCache.Instance.MyShipEntity.TypeId == 34590) return false; // Victorieux Luxury Yacht
            // @TODO: T3 Nullification Subsystem
            if (!QMCache.Instance.Entities.Any(a => Bubbles.Keys.Contains(a.TypeId))) return false;
            if (!QMCache.Instance.Entities.Any(a => a.Distance < Bubbles.Values.Max() && Bubbles.Keys.Contains(a.TypeId))) return false;
            return Bubbles.Any(bubble => QMCache.Instance.Entities.Any(a => a.TypeId == bubble.Key && a.Distance < bubble.Value));
        }

        bool AutoPilotPrep(object[] Params)
        {
            QueueAutoPilotDeactivation = false;
            if (Route.Path == null || Route.Path[0] == -1)
            {
                return true;
            }
            if (QMCache.Instance.InStation)
            {
                QueueState(Undock);
            }
            QueueState(AutoPilot);
            return true;
        }

        bool QueueAutoPilotDeactivation;
        public bool SunMidpoint = false;
        public bool MoonPatrol = false;
        List<long> checkedMoons = new List<long>();

        bool AutoPilot(object[] Params)
        {
            if (!QMCache.Instance.InSpace || QMCache.Instance.MyShipEntity == null)
            {
                return false;
            }

            if (Route.Path == null || Route.Path[0] == -1 || QueueAutoPilotDeactivation)
            {
                QueueAutoPilotDeactivation = false;
                Log.Log("|oAutopilot deactivated");
                //Comms.Comms.Instance.ChatQueue.Enqueue("<Move> Autopilot deactivated");
                return true;
            }

            if (QMCache.Instance.InSpace)
            {
                if (UndockWarp.Instance != null && !UndockWarp.Instance.Idle && UndockWarp.Instance.CurState.ToString() != "WaitStation") return false;
                if (QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping) return false;

                if (Config.WaitForCloakReactivationTimer &&
                    QMCache.Instance.Modules.Any(m => (m.TypeId == 11578 || m.TypeId == 20563) && m.OnCooldown) && Session.JumpCloakTimer > Session.Now) return false;

                EntityCache Sun = QMCache.Instance.Entities.FirstOrDefault(a => a.GroupId == (int)Group.Sun);
                if (MoonPatrol)
                {
                    EntityCache moon = QMCache.Instance.Entities.OrderBy(a => a.Distance).FirstOrDefault(a => a.GroupId == (int)Group.Moon && !checkedMoons.Contains(a.Id));
                    if (moon != null)
                    {
                        if (Bubbled())
                        {
                            if (QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Stopped)
                            {
                                Log.Log("|rBubble detected!");
                                Log.Log("|oAligning to |-g{0}", moon.Name);
                                moon.AlignTo();
                                InsertState(AutoPilot);
                                WaitFor(10, () => QMCache.Instance.MyShipEntity.EntityMode != EntityMode.Stopped);
                                return true;
                            }
                            return false;
                        }
                        Log.Log("|oWarping to |-g{0} |w(|y40 km|w)", moon.Name);
                        moon.WarpTo(40000);
                        checkedMoons.Add(moon.Id);
                        InsertState(AutoPilot);
                        WaitFor(10, () => QMCache.Instance.MyShipEntity.EntityMode != EntityMode.Stopped);
                        return true;
                    }
                }

                if (SunMidpoint && Sun != null && Sun.Distance < 1000000000)
                {
                    if (Bubbled())
                    {
                        if (QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Stopped)
                        {
                            Log.Log("|rBubble detected!");
                            Log.Log("|oAligning to |-g{0}", Sun.Name);
                            Sun.AlignTo();
                            InsertState(AutoPilot);
                            WaitFor(10, () => QMCache.Instance.MyShipEntity.EntityMode != EntityMode.Stopped);
                            return true;
                        }
                        else
                        {
                            DoInstaWarp();
                            Log.Log("|oWarping");
                            Log.Log(" |-g{0}", Route.NextWaypoint.Name);
                            Route.NextWaypoint.WarpTo();
                            return false;
                        }
                    }
                    DoInstaWarp();
                    Log.Log("|oWarping to |-g{0} |w(|y100 km|w)", Sun.Name);
                    Sun.WarpTo(100000);
                    InsertState(AutoPilot);
                    WaitFor(10, () => QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping);
                    return true;
                }
                if (Route.NextWaypoint.GroupID == Group.Stargate)
                {
                    if (Bubbled() && Route.NextWaypoint.Distance > 2000)
                    {
                        if (QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Stopped)
                        {
                            Log.Log("|rBubble detected!");
                            Log.Log("|oAligning to |-g{0}", Route.NextWaypoint.Name);
                            //Comms.Comms.Instance.ChatQueue.Enqueue("<Move> Bubble Detected! Aligning to " + Route.NextWaypoint.Name);
                            Route.NextWaypoint.AlignTo();
                            InsertState(AutoPilot);
                            WaitFor(10, () => QMCache.Instance.MyShipEntity.EntityMode != EntityMode.Stopped);
                            return true;
                        }
                        else
                        {
                            DoInstaWarp();
                            Log.Log("|oWarping");
                            Log.Log(" |-g{0}", Route.NextWaypoint.Name);
                            //Comms.Comms.Instance.ChatQueue.Enqueue("<Move> Warping " + Route.NextWaypoint.Name);
                            Route.NextWaypoint.WarpTo();
                            return false;
                        }
                    }
                    if (!QMCache.Instance.Entities.Any(a => a.Dockable() && a.Distance < Constants.WarpMinDistance)) DoInstaWarp();
                    if (Route.NextWaypoint.Distance < 2000 || Route.NextWaypoint.Distance > Constants.WarpMinDistance)
                    {
                        Log.Log("|oJumping through to |-g{0}", Route.NextWaypoint.Name);
                        //Comms.Comms.Instance.ChatQueue.Enqueue("<Move> Jumping through to " + Route.NextWaypoint.Name);
                        Route.NextWaypoint.Jump();
                    }
                    else
                    {
                        if (QMCache.Instance.MyShipEntity.EntityMode != EntityMode.Approaching)
                        {
                            Log.Log("|oApproaching |-g{0}", Route.NextWaypoint.Name);
                            //Comms.Comms.Instance.ChatQueue.Enqueue("<Move> Approaching " + Route.NextWaypoint.Name);
                            Route.NextWaypoint.Approach();
                        }
                        return false;
                    }
                    if (Route.Path != null && Route.Waypoints != null)
                    {
                        if (Route.Path.FirstOrDefault() == Route.Waypoints.FirstOrDefault()) QueueAutoPilotDeactivation = true;
                    }
                    long curSystem = (long)DirectEve.Session.SolarSystemId;
                    InsertState(AutoPilot);
                    WaitFor(10, () => DirectEve.Session.SolarSystemId != curSystem, () => QMCache.Instance.MyShipEntity.EntityMode != EntityMode.Stopped);
                    return true;
                }
                if (Route.NextWaypoint.GroupID == Group.Station || Route.NextWaypoint.GroupID == Group.Citadel)
                {
                    if (Bubbled() && Route.NextWaypoint.Distance > 2000)
                    {
                        if (QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Stopped)
                        {
                            Log.Log("|rBubble detected!");
                            Log.Log("|oAligning to |-g{0}", Route.NextWaypoint.Name);
                            //Comms.Comms.Instance.ChatQueue.Enqueue("<Move> Bubble Detected! Aligning to " + Route.NextWaypoint.Name);
                            Route.NextWaypoint.AlignTo();
                            InsertState(AutoPilot);
                            WaitFor(10, () => QMCache.Instance.MyShipEntity.EntityMode != EntityMode.Stopped);
                            return true;
                        }
                        else
                        {
                            DoInstaWarp();
                            Log.Log("|oWarping");
                            Log.Log(" |-g{0}", Route.NextWaypoint.Name);
                            //Comms.Comms.Instance.ChatQueue.Enqueue("<Move> Warping " + Route.NextWaypoint.Name);
                            Route.NextWaypoint.WarpTo();
                            return false;
                        }
                    }
                    AutoModule.PrepareToDock();
                    InsertState(Dock, 500, Route.NextWaypoint);
                    return true;
                }
            }
            return false;
        }


        bool Dock(object[] Params)
        {
            if (!QMCache.Instance.InSpace || QMCache.Instance.MyShipEntity == null)
            {
                return true;
            }

            EntityCache Target = (EntityCache)Params[0];
            EntityCache Collision = null;
            if (Params.Count() > 1) Collision = (EntityCache)Params[1];

            if (Params.Length == 0)
            {
                Log.Log("|yDock call incomplete");
                return true;
            }
            if (QMCache.Instance.InStation)
            {
                Log.Log("|oDock complete");
                return true;
            }
            if (!Config.WarpCollisionPrevention)
            {
                if (!QMCache.Instance.Entities.Any(a => a.Dockable() && a.Distance < Constants.WarpMinDistance)) DoInstaWarp();
                Log.Log("|oDocking");
                try
                {
                    Log.Log(" |-g{0}", Target.Name);
                }
                catch (Exception ex)
                {
                    Log.Log("FIXME! Entity name not ready yet? " + ex.ToString(), LogType.DEBUG);
                }
                Target.Dock();
                InsertState(Dock, -1, Target);
                WaitFor(10, () => QMCache.Instance.InStation, () => QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping);
                return true;
            }

            EntityCache LCO = QMCache.Instance.Entities.FirstOrDefault(a => a.Collidable() && a.Distance <= (double)(Config.WarpCollisionTrigger * 900));
            EntityCache LCO2 = QMCache.Instance.Entities.FirstOrDefault(a => a.Collidable() && a.Distance <= (double)(Config.WarpCollisionTrigger * 500));

            if (LCO != null && Collision == null)
            {
                Collision = LCO;
                Log.Log("|oToo close for warp, orbiting");
                Log.Log(" |-g{0}(|w{1} km|-g)", Collision.Name, Config.WarpCollisionOrbit);
                Collision.Orbit((int)(Config.WarpCollisionOrbit * 1000));
                InsertState(Dock, -1, Target, Collision);
            }
            // Else, if we're in .2km of a structure that isn't our current collision target, change orbit and collision target to it
            else if (LCO2 != null)
            {
                Collision = LCO2;
                Log.Log("|oOrbiting");
                Log.Log(" |-g{0}(|w{1} km|-g)", Collision.Name, Config.WarpCollisionOrbit);
                Collision.Orbit((int)(Config.WarpCollisionOrbit * 1000));
                InsertState(Dock, -1, Target, Collision);
            }
            else if (LCO == null)
            {
                if (!QMCache.Instance.Entities.Any(a => a.Dockable() && a.Distance < Constants.WarpMinDistance)) DoInstaWarp();
                Log.Log("|oDocking");
                Log.Log(" |-g{0}", Target.Name);
                Target.Dock();
                InsertState(Dock, -1, Target);
                WaitFor(10, () => QMCache.Instance.InStation, () => QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping);
            }
            else
            {
                InsertState(Dock, -1, Target, Collision);
            }

            return true;
        }

        #endregion

        #region Helper Methods

        void DoInstaWarp()
        {
            if (Config.InstaWarp && QMCache.Instance.MyShipEntity.EntityMode != EntityMode.Warping)
            {
                InstaWarpModule.Enabled(true);
            }
        }
        #endregion

    }

    class InstaWarp : State
    {
        public Logger Log = new Logger("MoveInstaWarp");
        #region Instantiation
        static InstaWarp _Instance;
        /// <summary>
        /// Singletoner
        /// </summary>
        public static InstaWarp Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new InstaWarp();
                }
                return _Instance;
            }
        }

        public ILoveEVE.Framework.DirectEve DirectEve { get; set; }

        private InstaWarp()
        {
            DefaultFrequency = 400;
        }
        #endregion
        #region Actions

        public void Enabled(bool val)
        {
            if (val)
            {
                if (Idle)
                {
                    Log.Log("|yDoing InstaWarp");
                    QueueState(Prepare);
                    QueueState(EnablePropmod);
                }
            }
            else
            {
                Clear();
            }
        }
        #endregion
        #region States
        bool Prepare(object[] Params)
        {
            if (!DirectEve.Session.IsInSpace) return false;
            return !QMCache.Instance.MyShipEntity.IsCloaked;
        }

        bool EnablePropmod(object[] Params)
        {
            try
            {
                List<ModuleCache> propulsionModules = Cache.Instance.MyShipsModules.Where(a => a.GroupId == (int)Group.PropulsionModule && a.IsOnline).ToList();
                if (propulsionModules.Any())
                {
                    if (propulsionModules.Any(a => a.IsActivatable && !a.IsActive && !a.IsDeactivating && a.IsOnline))
                    {
                        Log.Log("|g  InstaWarp turned on the propmod.");
                        propulsionModules.Where(a => a.IsActivatable && !a.IsActive && !a.IsDeactivating && a.IsOnline).ForEach(m => m.Click());
                        return true;
                    }

                    return true;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Log("Exception [" + ex + "]");
                return false;
            }
        }

        #endregion
    }

    /// <summary>
    /// Settings for the UndockWarp class
    /// </summary>
    public class UndockWarpSettings : Settings
    {
        public long MaxDistance = 30000000;
        public string Substring = "Undock";
        public bool Enabled = false;
    }

    /// <summary>
    /// This class automatically performs a warp to a bookmark which contains the configured substring which is in-system and within 200km
    /// </summary>
    public class UndockWarp : State
    {
        #region Instantiation
        static UndockWarp _Instance;
        /// <summary>
        /// Singletoner
        /// </summary>
        public static UndockWarp Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new UndockWarp();
                }
                return _Instance;
            }
        }

        public ILoveEVE.Framework.DirectEve DirectEve { get; set; }

        private UndockWarp()
        {
            DefaultFrequency = 400;
            if (Config.Enabled) QueueState(WaitStation);
        }

        #endregion

        #region Actions

        /// <summary>
        /// Toggle on/off this class
        /// </summary>
        /// <param name="val">Enabled = true</param>
        public void Enabled(bool val)
        {
            if (val)
            {
                if (Idle)
                {
                    QueueState(WaitStation);
                }
            }
            else
            {
                Clear();
            }
        }

        #endregion

        #region Variables

        /// <summary>
        /// The config for this class
        /// </summary>
        public UndockWarpSettings Config = new UndockWarpSettings();

        #endregion

        #region States

        bool Space(object[] Params)
        {
            if (DirectEve.Session.IsInStation)
            {
                QueueState(Station);
                return true;
            }
            if (DirectEve.Session.IsInSpace)
            {
                ILoveEVE.Framework.DirectBookmark undock = DirectEve.Bookmarks.FirstOrDefault(a => a.Title.Contains(Config.Substring) && a.LocationId == DirectEve.Session.SolarSystemId && Cache.Instance.DistanceFromMe((double)a.X, (double)a.Y, (double)a.Z) < Config.MaxDistance);
                if (undock != null) undock.WarpTo(0);
                QueueState(WaitStation);
                return true;
            }
            return false;
        }

        bool WaitStation(object[] Params)
        {
            if (DirectEve.Session.IsInStation)
            {
                QueueState(Station);
                return true;
            }
            return false;
        }

        bool Station(object[] Params)
        {
            if (DirectEve.Session.IsInSpace)
            {
                QueueState(Space);
                return true;
            }
            return false;
        }

        #endregion
    }
}

﻿#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EveCom;
using LavishScriptAPI;
using EveComFramework.Core;
using EveComFramework.Data;
using EveComFramework.KanedaToolkit;

namespace EveComFramework.Targets
{
    public class Targets : State
    {

        #region Variables

        public Targets()
        {
            DefaultFrequency = 50;
            InsertState(Update);
        }

        private List<Entity> _TargetList;
        private List<Entity> _UnlockedTargetList;
        private List<Entity> _LockedTargetList;
        private List<Entity> _LockedAndLockingTargetList;
        private Dictionary<Entity, DateTime> Delays = new Dictionary<Entity, DateTime>();
        public Comparer<Entity> Ordering;

        public List<Entity> TargetList
        {
            get
            {
                try
                {
                    if (_TargetList == null)
                    {
                        if (Ordering != null)
                        {
                            _TargetList = Entity.All.Where(QueriesCompiled).Where(ent => ent.Exists && !ent.Exploded && !ent.Released).OrderBy(ent => ent, Ordering).ThenBy(ent => ent.Distance).ToList();
                        }
                        else
                        {
                            _TargetList = Entity.All.Where(QueriesCompiled).Where(ent => ent.Exists && !ent.Exploded && !ent.Released).OrderBy(ent => ent.Distance).ToList();
                        }
                    }
                    return _TargetList;
                }
                catch (Exception)
                {
                    return new List<Entity>();
                }
            }
        }
        public List<Entity> UnlockedTargetList
        {
            get
            {
                try
                {
                    if (_UnlockedTargetList == null)
                    {
                        _UnlockedTargetList = TargetList.Where(a => !a.LockedTarget && !a.LockingTarget).ToList();
                    }
                    return _UnlockedTargetList;
                }
                catch (Exception)
                {
                    return new List<Entity>();
                }
            }
        }
        public List<Entity> LockedTargetList
        {
            get
            {
                try
                {
                    if (_LockedTargetList == null)
                    {
                        _LockedTargetList =
                            LockedAndLockingTargetList.Where(
                                a => a.Exists && !a.Exploded && !a.Released && a.LockedTarget).ToList();
                    }

                    return _LockedTargetList;
                }
                catch (Exception)
                {
                    return new List<Entity>();
                }
            }
        }
        public List<Entity> LockedAndLockingTargetList
        {
            get
            {
                try
                {
                    if (_LockedAndLockingTargetList == null)
                    {
                        _LockedAndLockingTargetList = TargetList.Where(a => a.LockedTarget || a.LockingTarget).ToList();
                    }
                    return _LockedAndLockingTargetList;
                }
                catch (Exception)
                {
                    return new List<Entity>();
                }
            }
        }

        private Expression<Func<Entity, bool>> Queries = Utility.False<Entity>();
        private Func<Entity, bool> QueriesCompiled = Utility.False<Entity>().Compile();

        #endregion

        #region Actions

        public void AddPriorityTargets()
        {
            AddQuery(a => PriorityTarget.All.Contains(a.Name));
        }

        public void AddNPCs()
        {
            AddQuery(a => NPCTypes.All.Contains((long)a.GroupID));
            //Queries = Queries.Or(a => a.IsNPC);
        }

        public void AddTargetingMe()
        {
            AddQuery(a => a.IsTargetingMe && a.IsNPC);
        }

        public void AddNonFleetPlayers()
        {
            AddQuery(a => a.CategoryID == Category.Ship && a.IsPC && a.OwnerID != Session.CharID && !a.IsFleetMember);
        }

        public void AddQuery(Expression<Func<Entity, bool>> Query)
        {
            Queries = Queries.Or(Query);
            QueriesCompiled = Queries.Compile();
        }

        public bool GetLocks(int Count = 2)
        {
            if (MyShip.ToEntity.Mode == EntityMode.Warping) return false;

            if (Delays.Keys.Union(LockedAndLockingTargetList).Count() < Count)
            {
                Entity TryLock = UnlockedTargetList.FirstOrDefault(ent => !Delays.ContainsKey(ent));
                if (TryLock != null && TryLock.Distance < MyShip.MaxTargetRange && Entity.Targets.Count + Entity.Targeting.Count < MyShip.MaxTargetLocks)
                {
                    Delays.Add(TryLock, DateTime.Now.AddSeconds(2));
                    TryLock.LockTarget();
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region States

        bool Update(object[] Params)
        {
            if (!Session.InSpace)
            {
                return false;
            }
            _TargetList = null;
            _LockedAndLockingTargetList = null;
            _UnlockedTargetList = null;
            _LockedTargetList = null;
            if (Delays.Count > 0)
            {
                DateTime newTime = DateTime.Now.AddSeconds(2);
                Delays.Keys.Where(ent => LockedAndLockingTargetList.Contains(ent)).ToList().ForEach(ent => Delays[ent] = newTime);
                Delays.Keys.Where(ent => !LockedAndLockingTargetList.Contains(ent) && Delays[ent] < DateTime.Now).ToList().ForEach(ent => Delays.Remove(ent));
            }
            return false;
        }

        #endregion

    }

    public class IPCPilot
    {
        public double Hull;
        public double Armor;
        public double Shield;
        public double Capacitor;
    }

    public class IPC : State
    {
        #region Instantiation

        public Logger Console = new Logger("IPCTargets");

        static IPC _Instance;
        public static IPC Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new IPC();
                }
                return _Instance;
            }
        }

        private IPC()
        {
            LavishScript.Events.RegisterEvent("UpdateActiveTargets");
            LavishScript.Events.AttachEventTarget("UpdateActiveTargets", UpdateActiveTargets);
            LavishScript.Events.RegisterEvent("UpdateIPCPilots");
            LavishScript.Events.AttachEventTarget("UpdateIPCPilots", UpdateIPCPilots);
            QueueState(Control);
        }

        #endregion

        public Dictionary<long, long> ActiveTargets = new Dictionary<long, long>();
        public Dictionary<long, IPCPilot> IPCPilots = new Dictionary<long, IPCPilot>();

        bool Control(object[] Params)
        {
            try
            {
                if (!Session.InSpace || !Session.Safe || MyShip.ToEntity == null || MyShip.Capacitor == 0) return false;
                LavishScript.ExecuteCommand(string.Format("relay \"all other\" Event[UpdateIPCPilots]:Execute[{0},{1},{2},{3},{4}]", Me.CharID, MyShip.ToEntity.HullPct, MyShip.ToEntity.ArmorPct, MyShip.ToEntity.ShieldPct, MyShip.Capacitor / MyShip.MaxCapacitor));
                return false;
            }
            catch (Exception)
            {
                Console.Log("|oOveriding current drone target");
                return false;
            }
        }

        void UpdateActiveTargets(object sender, LSEventArgs args)
        {
            try
            {
                ActiveTargets.AddOrUpdate(long.Parse(args.Args[0]), long.Parse(args.Args[1]));
            }
            catch { }
        }

        void UpdateIPCPilots(object sender, LSEventArgs args)
        {
            try
            {
                IPCPilot newpilot = new IPCPilot();
                newpilot.Hull = double.Parse(args.Args[1]);
                newpilot.Armor = double.Parse(args.Args[2]);
                newpilot.Shield = double.Parse(args.Args[3]);
                newpilot.Capacitor = double.Parse(args.Args[4]);
                IPCPilots.AddOrUpdate(long.Parse(args.Args[0]), newpilot);
            }
            catch { }
        }

        public void Relay(long Pilot, long ID)
        {
            LavishScript.ExecuteCommand("relay \"all other\" Event[UpdateActiveTargets]:Execute[" + Pilot + "," + ID.ToString() + "]");
        }
    }

    public class ParameterRebinder : ExpressionVisitor
    {
        private readonly Dictionary<ParameterExpression, ParameterExpression> map;

        public ParameterRebinder(Dictionary<ParameterExpression, ParameterExpression> map)
        {
            this.map = map ?? new Dictionary<ParameterExpression, ParameterExpression>();
        }

        public static Expression ReplaceParameters(Dictionary<ParameterExpression, ParameterExpression> map, Expression exp)
        {
            return new ParameterRebinder(map).Visit(exp);
        }

        protected override Expression VisitParameter(ParameterExpression p)
        {
            ParameterExpression replacement;
            if (map.TryGetValue(p, out replacement))
            {
                p = replacement;
            }
            return base.VisitParameter(p);
        }
    }

    public static class Utility
    {
        public static Expression<T> Compose<T>(this Expression<T> first, Expression<T> second, Func<Expression, Expression, Expression> merge)
        {
            // build parameter map (from parameters of second to parameters of first)
            var map = first.Parameters.Select((f, i) => new { f, s = second.Parameters[i] }).ToDictionary(p => p.s, p => p.f);

            // replace parameters in the second lambda expression with parameters from the first
            var secondBody = ParameterRebinder.ReplaceParameters(map, second.Body);

            // apply composition of lambda expression bodies to parameters from the first expression
            return Expression.Lambda<T>(merge(first.Body, secondBody), first.Parameters);
        }

        public static Expression<Func<T, bool>> False<T>()
        {
            return p => false;
        }

        public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
        {
            return first.Compose(second, Expression.And);
        }

        public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
        {
            return first.Compose(second, Expression.Or);
        }
    }
    public class RatComparer : Comparer<Entity>
    {
        public override int Compare(Entity x, Entity y)
        {
            if (x == null && y == null)
                return 0;
            if (x == null)
                return -1;
            if (y == null)
                return 1;
            if (x == y)
                return 0;

            int orderx = 0;
            if (PriorityTarget.All.Contains(x.Name))
            {
                orderx = PriorityTarget.All.IndexOf(x.Name);
            }
            else if (NPCTypes.All.Contains((long)x.GroupID))
            {
                orderx = NPCTypes.All.IndexOf((long)x.GroupID) + PriorityTarget.All.Count;
            }

            int ordery = 0;
            if (PriorityTarget.All.Contains(y.Name))
            {
                ordery = PriorityTarget.All.IndexOf(y.Name);
            }
            else if (NPCTypes.All.Contains((long)y.GroupID))
            {
                ordery = NPCTypes.All.IndexOf((long)y.GroupID) + PriorityTarget.All.Count;
            }

            if (orderx > ordery)
                return 1;
            if (orderx < ordery)
                return -1;
            return 0;
        }
    }

}

#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ILoveEVE.Framework;
using ILEF.Caching;
using ILEF.Core;
using ILEF.Data;
using ILEF.Lookup;
using ILEF.KanedaToolkit;

namespace ILEF.Targets
{
    public class Targets : State
    {

        #region Variables

        public Targets()
        {
            DefaultFrequency = 400;
            InsertState(Update);
        }

        public ILoveEVE.Framework.DirectEve DirectEve { get; set; }

        private List<EntityCache> _TargetList;
        private List<EntityCache> _UnlockedTargetList;
        private List<EntityCache> _LockedTargetList;
        private List<EntityCache> _LockedAndLockingTargetList;
        private Dictionary<EntityCache, DateTime> Delays = new Dictionary<EntityCache, DateTime>();
        public Comparer<EntityCache> Ordering;

        public List<EntityCache> TargetList
        {
            get
            {
                try
                {
                    if (_TargetList == null)
                    {
                        if (Ordering != null)
                        {
                            _TargetList = QMCache.Instance.Entities.Where(QueriesCompiled).Where(ent => ent.IsValid && !ent.HasExploded && !ent.HasReleased).OrderBy(ent => ent, Ordering).ThenBy(ent => ent.Distance).ToList();
                        }
                        else
                        {
                            _TargetList = QMCache.Instance.Entities.Where(QueriesCompiled).Where(ent => ent.IsValid && !ent.HasExploded && !ent.HasReleased).OrderBy(ent => ent.Distance).ToList();
                        }
                    }
                    return _TargetList;
                }
                catch (Exception)
                {
                    return new List<EntityCache>();
                }
            }
        }
        public List<EntityCache> UnlockedTargetList
        {
            get
            {
                try
                {
                    if (_UnlockedTargetList == null)
                    {
                        _UnlockedTargetList = TargetList.Where(a => !a.IsTarget && !a.IsTargeting).ToList();
                    }
                    return _UnlockedTargetList;
                }
                catch (Exception)
                {
                    return new List<EntityCache>();
                }
            }
        }
        public List<EntityCache> LockedTargetList
        {
            get
            {
                try
                {
                    if (_LockedTargetList == null)
                    {
                        _LockedTargetList =
                            LockedAndLockingTargetList.Where(
                                a => a.IsValid && !a.HasExploded && !a.HasReleased && a.IsTarget).ToList();
                    }

                    return _LockedTargetList;
                }
                catch (Exception)
                {
                    return new List<EntityCache>();
                }
            }
        }
        public List<EntityCache> LockedAndLockingTargetList
        {
            get
            {
                try
                {
                    if (_LockedAndLockingTargetList == null)
                    {
                        _LockedAndLockingTargetList = TargetList.Where(a => a.IsTarget || a.IsTargeting).ToList();
                    }
                    return _LockedAndLockingTargetList;
                }
                catch (Exception)
                {
                    return new List<EntityCache>();
                }
            }
        }

        private Expression<Func<EntityCache, bool>> Queries = Utility.False<EntityCache>();
        private Func<EntityCache, bool> QueriesCompiled = Utility.False<EntityCache>().Compile();

        #endregion

        #region Actions

        public void AddPriorityTargets()
        {
            AddQuery(a => PriorityTargetData.All.Contains(a.Name));
        }

        public void AddNPCs()
        {
            AddQuery(a => NPCTypes.All.Contains((long)a.GroupId));
            //Queries = Queries.Or(a => a.IsNPC);
        }

        public void AddTargetingMe()
        {
            AddQuery(a => a.IsTargetedBy && a.IsNpc);
        }

        //public void AddNonFleetPlayers()
        //{
        //    AddQuery(a => a.CategoryId == (int)CategoryID.Ship && a.IsPlayer && a.Id != DirectEve.Session.CharacterId && !a.IsFleetMember);
        //}

        public void AddQuery(Expression<Func<EntityCache, bool>> Query)
        {
            Queries = Queries.Or(Query);
            QueriesCompiled = Queries.Compile();
        }

        public bool GetLocks(int Count = 2)
        {
            if (QMCache.Instance.MyShipEntity != null && QMCache.Instance.MyShipEntity.EntityMode == EntityMode.Warping) return false;

            if (Delays.Keys.Union(LockedAndLockingTargetList).Count() < Count)
            {
                EntityCache TryLock = UnlockedTargetList.FirstOrDefault(ent => !Delays.ContainsKey(ent));
                if (TryLock != null && TryLock.Distance < QMCache.Instance.WeaponRange && QMCache.Instance.TotalTargetsandTargetingCount < QMCache.Instance.MaxLockedTargets)
                {
                    Delays.Add(TryLock, DateTime.Now.AddSeconds(2));
                    if(TryLock.LockTarget("Locking [" + TryLock.Name + "] at [" + Math.Round(TryLock.Distance / 1000) + "k]"));
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region States

        bool Update(object[] Params)
        {
            if (!QMCache.Instance.InSpace)
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
            //LavishScript.Events.RegisterEvent("UpdateActiveTargets");
            //LavishScript.Events.AttachEventTarget("UpdateActiveTargets", UpdateActiveTargets);
            //LavishScript.Events.RegisterEvent("UpdateIPCPilots");
            //LavishScript.Events.AttachEventTarget("UpdateIPCPilots", UpdateIPCPilots);
            QueueState(Control);
        }

        #endregion

        public Dictionary<long, long> ActiveTargets = new Dictionary<long, long>();
        public Dictionary<long, IPCPilot> IPCPilots = new Dictionary<long, IPCPilot>();

        bool Control(object[] Params)
        {
            try
            {
                if (!QMCache.Instance.InSpace || Cache.Instance.ActiveShip == null || Cache.Instance.ActiveShip.Capacitor == 0) return false;
                //LavishScript.ExecuteCommand(string.Format("relay \"all other\" Event[UpdateIPCPilots]:Execute[{0},{1},{2},{3},{4}]", Me.CharID, QMCache.Instance.MyShipEntity.HullPct, QMCache.Instance.MyShipEntity.ArmorPct, QMCache.Instance.MyShipEntity.ShieldPct, MyShip.Capacitor / MyShip.MaxCapacitor));
                return false;
            }
            catch (Exception)
            {
                Console.Log("|oOveriding current drone target");
                return false;
            }
        }
        /**
        void UpdateActiveTargets(object sender, LSEventArgs args)
        {
            try
            {
                ActiveTargets.AddOrUpdate(long.Parse(args.Args[0]), long.Parse(args.Args[1]));
            }
            catch { }
        }
        **/
        /**
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
        **/
        public void Relay(long Pilot, long ID)
        {
            //LavishScript.ExecuteCommand("relay \"all other\" Event[UpdateActiveTargets]:Execute[" + Pilot + "," + ID.ToString() + "]");
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
    public class RatComparer : Comparer<EntityCache>
    {
        public override int Compare(EntityCache x, EntityCache y)
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
            if (PriorityTargetData.All.Contains(x.Name))
            {
                orderx = PriorityTargetData.All.IndexOf(x.Name);
            }
            else if (NPCTypes.All.Contains((long)x.GroupId))
            {
                orderx = NPCTypes.All.IndexOf((long)x.GroupId) + PriorityTargetData.All.Count;
            }

            int ordery = 0;
            if (PriorityTargetData.All.Contains(y.Name))
            {
                ordery = PriorityTargetData.All.IndexOf(y.Name);
            }
            else if (NPCTypes.All.Contains((long)y.GroupId))
            {
                ordery = NPCTypes.All.IndexOf((long)y.GroupId) + PriorityTargetData.All.Count;
            }

            if (orderx > ordery)
                return 1;
            if (orderx < ordery)
                return -1;
            return 0;
        }
    }

}

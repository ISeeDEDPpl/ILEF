

namespace ILEF.Caching
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ILoveEVE.Framework;
    using global::ILEF.Activities;
    using global::ILEF.BackgroundTasks;
    using global::ILEF.Combat;
    using global::ILEF.Lookup;
    using global::ILEF.Logging;

    public class EntityCache
    {
        private readonly DirectEntity _directEntity;
        //public static int EntityCacheInstances = 0;
        private readonly DateTime ThisEntityCacheCreated = DateTime.UtcNow;
        private const int DictionaryCountThreshhold = 250;

        public EntityCache(DirectEntity entity)
        {
            //
            // reminder: this class and all the info within it is created (and destroyed!) each frame for each entity!
            //
            _directEntity = entity;
            //Interlocked.Increment(ref EntityCacheInstances);
            ThisEntityCacheCreated = DateTime.UtcNow;
        }

        //~EntityCache()
        //{
        //    Interlocked.Decrement(ref EntityCacheInstances);
        //}

        public double? DistanceFromEntity(EntityCache OtherEntityToMeasureFrom)
        {
            try
            {
                if (OtherEntityToMeasureFrom == null)
                {
                    return null;
                }

                double deltaX = XCoordinate - OtherEntityToMeasureFrom.XCoordinate;
                double deltaY = YCoordinate - OtherEntityToMeasureFrom.YCoordinate;
                double deltaZ = ZCoordinate - OtherEntityToMeasureFrom.ZCoordinate;

                return Math.Sqrt((deltaX*deltaX) + (deltaY*deltaY) + (deltaZ*deltaZ));
            }
            catch (Exception ex)
            {
                Logging.Log("DistanceFromEntity", "Exception [" + ex + "]", Logging.Debug);
                return 0;
            }
        }

        public bool BookmarkThis(string NameOfBookmark = "bookmark", string Comment = "")
        {
            try
            {
                if (
                    QMCache.Instance.BookmarksByLabel(NameOfBookmark)
                        .Any(i => i.LocationId == QMCache.Instance.DirectEve.Session.LocationId))
                {
                    List<DirectBookmark> PreExistingBookmarks = QMCache.Instance.BookmarksByLabel(NameOfBookmark);
                    if (PreExistingBookmarks.Any())
                    {
                        foreach (DirectBookmark _PreExistingBookmark in PreExistingBookmarks)
                        {

                            if (_PreExistingBookmark.X == _directEntity.X
                                && _PreExistingBookmark.Y == _directEntity.Y
                                && _PreExistingBookmark.Z == _directEntity.Z)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("EntityCache.BookmarkThis", "We already have a bookmark for [" + Name + "] and do not need another.", Logging.Debug);
                                return true;
                            }
                            continue;
                        }
                    }
                }

                if (IsLargeCollidable || IsStation || IsAsteroid || IsAsteroidBelt)
                {
                    QMCache.Instance.DirectEve.BookmarkEntity(_directEntity, NameOfBookmark, Comment, 0);
                    return true;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
            }

            return false;
        }

        private int? _groupID;

        public int GroupId
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_groupID == null)
                        {
                            if (QMCache.Instance.EntityGroupID.Any() && QMCache.Instance.EntityGroupID.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.GroupID", "We have [" + QMCache.Instance.EntityGroupID.Count() + "] Entities in QMCache.Instance.EntityGroupID", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityGroupID.Any())
                            {
                                int value;
                                if (QMCache.Instance.EntityGroupID.TryGetValue(Id, out value))
                                {
                                    _groupID = value;
                                    return (int) _groupID;
                                }
                            }

                            _groupID = _directEntity.GroupId;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.GroupID", "Adding [" + Name + "] to EntityGroupID as [" + _groupID + "]", Logging.Debug);
                            QMCache.Instance.EntityGroupID.Add(Id, (int) _groupID);
                            return (int) _groupID;
                        }

                        return (int) _groupID;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private int? _categoryId;

        public int CategoryId
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_categoryId == null)
                        {
                            _categoryId = _directEntity.CategoryId;
                        }

                        return (int) _categoryId;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private long? _id;

        public long Id
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_id == null)
                        {
                            _id = _directEntity.Id;
                        }

                        return (long) _id;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        public string MaskedId
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        int numofCharacters = _directEntity.Id.ToString().Length;
                        if (numofCharacters >= 5)
                        {
                            string maskedID = _directEntity.Id.ToString().Substring(numofCharacters - 4);
                            maskedID = "[MaskedID]" + maskedID;
                            return maskedID;
                        }

                        return "!0!";
                    }

                    return "!0!";
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return "!0!";
                }
            }
        }

        private int? _TypeId;

        public int TypeId
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_TypeId == null)
                        {
                            if (QMCache.Instance.EntityTypeID.Any() && QMCache.Instance.EntityTypeID.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.TypeID", "We have [" + QMCache.Instance.EntityTypeID.Count() + "] Entities in QMCache.Instance.EntityTypeID", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityTypeID.Any())
                            {
                                int value;
                                if (QMCache.Instance.EntityTypeID.TryGetValue(Id, out value))
                                {
                                    _TypeId = value;
                                    return (int) _TypeId;
                                }
                            }

                            _TypeId = _directEntity.TypeId;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.TypeId", "Adding [" + Name + "] to EntityTypeId as [" + _TypeId + "]", Logging.Debug);
                            QMCache.Instance.EntityTypeID.Add(Id, (int) _TypeId);
                            return (int) _TypeId;
                        }

                        return (int) _TypeId;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private long? _followId;

        public long FollowId
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_followId == null)
                        {
                            _followId = _directEntity.FollowId;
                        }

                        return (long) _followId;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private List<string> _attacks;

        public List<string> Attacks
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_attacks == null)
                        {
                            _attacks = _directEntity.Attacks;
                        }

                        return _attacks;
                    }

                    return null;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
        }

        private int? _mode;

        public int Mode
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_mode == null)
                        {
                            try
                            {
                                _mode = _directEntity.Mode;
                            }
                            catch (Exception ex)
                            {
                                Logging.Log("EntityCache", "Mode: Exception [" + ex + "]", Logging.Debug);
                            }

                            return (int)_mode;
                        }
                    }

                    _mode = null;
                    return 0;
                }
                catch (Exception ex)
                {
                    Logging.Log("EntityCache", "Exception [" + ex + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private EntityMode? _entityMode;

        public EntityMode EntityMode
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_entityMode == null)
                        {
                            switch (_directEntity.Mode)
                            {
                                // 1 = Approaching or entityCombat
                                // 2 =
                                // 3 = Warping
                                // 4 = Orbiting
                                // 5 =
                                // 6 = entityPursuit
                                // 7 =
                                // 8 =
                                // 9 =
                                // 10 = entityEngage
                                //
                                case 0:
                                    break;
                                case 1:
                                    _entityMode = EntityMode.Approaching;
                                    break;
                                case 2:
                                    break;
                                case 3:
                                    _entityMode = EntityMode.Warping;
                                    break;
                                case 4:
                                    break;
                                case 5:
                                    break;
                            }

                            return (EntityMode)_entityMode;
                        }
                    }

                    _entityMode = null;
                    return EntityMode.Stopped;
                }
                catch (Exception ex)
                {
                    Logging.Log("EntityCache", "Exception [" + ex + "]", Logging.Debug);
                    return EntityMode.Stopped;
                }
            }
        }

        private string _name;

        public string Name
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (DateTime.UtcNow.AddSeconds(-5) > ThisEntityCacheCreated)
                        {
                            Logging.Log("EntityCache.Name", "The EntityCache instance that represents [" + _directEntity.Name + "][" + Math.Round(_directEntity.Distance / 1000, 0) + "k][" + MaskedId + "] was created more than 5 seconds ago (ugh!)", Logging.Debug);
                        }

                        if (_name == null)
                        {
                            if (QMCache.Instance.EntityNames.Any() && QMCache.Instance.EntityNames.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.Name", "We have [" + QMCache.Instance.EntityNames.Count() + "] Entities in QMCache.Instance.EntityNames", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityNames.Any())
                            {
                                string value;
                                if (QMCache.Instance.EntityNames.TryGetValue(Id, out value))
                                {
                                    _name = value;
                                    return _name;
                                }
                            }

                            _name = _directEntity.Name;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.Name", "Adding [" + MaskedId + "] to EntityName as [" + _name + "]", Logging.Debug);
                            QMCache.Instance.EntityNames.Add(Id, _name);
                            return _name ?? string.Empty;
                        }

                        return _name;
                    }

                    return string.Empty;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return string.Empty;
                }

            }
        }

        private string _typeName;

        public string TypeName
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (DateTime.UtcNow.AddSeconds(-5) > ThisEntityCacheCreated)
                        {
                            Logging.Log("EntityCache.Name", "The EntityCache instance that represents [" + _directEntity.Name + "][" + Math.Round(_directEntity.Distance / 1000, 0) + "k][" + MaskedId + "] was created more than 5 seconds ago (ugh!)", Logging.Debug);
                        }

                        _typeName = _directEntity.TypeName;
                        return _typeName ?? "";
                    }

                    return "";
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return "";
                }
            }
        }

        private string _givenName;

        public string GivenName
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (DateTime.UtcNow.AddSeconds(-5) > ThisEntityCacheCreated)
                        {
                            Logging.Log("EntityCache.Name", "The EntityCache instance that represents [" + _directEntity.Name + "][" + Math.Round(_directEntity.Distance / 1000, 0) + "k][" + MaskedId + "] was created more than 5 seconds ago (ugh!)", Logging.Debug);
                        }
                        if (String.IsNullOrEmpty(_givenName))
                        {
                            _givenName = _directEntity.GivenName;
                        }

                        return _givenName ?? "";
                    }

                    return "";
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return "";
                }
            }
        }

        private double? _distance;

        public double Distance
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (DateTime.UtcNow.AddSeconds(-5) > ThisEntityCacheCreated)
                        {
                            Logging.Log("EntityCache.Name", "The EntityCache instance that represents [" + _directEntity.Name + "][" + Math.Round(_directEntity.Distance / 1000, 0) + "k][" + MaskedId + "] was created more than 5 seconds ago (ugh!)", Logging.Debug);
                        }
                        if (_distance == null)
                        {
                            _distance = _directEntity.Distance;
                        }

                        return (double)_distance;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _nearest5kDistance;

        public double Nearest5kDistance
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_nearest5kDistance == null)
                        {
                            if (Distance > 0 && Distance < 900000000)
                            {
                                //_nearest5kDistance = Math.Round((Distance / 1000) * 2, MidpointRounding.AwayFromZero) / 2;
                                _nearest5kDistance = Math.Ceiling(Math.Round((Distance / 1000)) / 5.0) * 5;
                            }
                        }

                        return _nearest5kDistance ?? Distance;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _shieldPct;

        public double ShieldPct
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_shieldPct == null)
                        {
                            _shieldPct = _directEntity.ShieldPct;
                            return (double)_shieldPct;
                        }

                        return (double)_shieldPct;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _shieldHitPoints;

        public double ShieldHitPoints
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_shieldHitPoints == null)
                        {
                            _shieldHitPoints = _directEntity.Shield;
                            return _shieldHitPoints ?? 0;
                        }

                        return (double)_shieldHitPoints;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _shieldResistanceEM;
        public double ShieldResistanceEM
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_shieldResistanceEM == null)
                        {
                            _shieldResistanceEM = _directEntity.ShieldResistanceEm;
                            return _shieldResistanceEM ?? 0;
                        }

                        return (double)_shieldResistanceEM;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _shieldResistanceExplosive;
        public double ShieldResistanceExplosive
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_shieldResistanceExplosive == null)
                        {
                            if (_directEntity.ShieldResistanceExplosion != null)
                            {
                                _shieldResistanceExplosive = _directEntity.ShieldResistanceExplosion;
                                return (double)_shieldResistanceExplosive;
                            }

                            _shieldResistanceExplosive = 0;
                            return (double)_shieldResistanceExplosive;
                        }

                        return (double)_shieldResistanceExplosive;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _shieldResistanceKinetic;
        public double ShieldResistanceKinetic
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_shieldResistanceKinetic == null)
                        {
                            if (_directEntity.ShieldResistanceKinetic != null)
                            {
                                _shieldResistanceKinetic = _directEntity.ShieldResistanceKinetic;
                                return (double)_shieldResistanceKinetic;
                            }

                            _shieldResistanceKinetic = 0;
                            return (double)_shieldResistanceKinetic;
                        }

                        return (double)_shieldResistanceKinetic;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _shieldResistanceThermal;
        public double ShieldResistanceThermal
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_shieldResistanceThermal == null)
                        {
                            if (_directEntity.ShieldResistanceThermal != null)
                            {
                                _shieldResistanceThermal = _directEntity.ShieldResistanceThermal;
                                return (double)_shieldResistanceThermal;
                            }

                            _shieldResistanceThermal = 0;
                            return (double)_shieldResistanceThermal;
                        }

                        return (double)_shieldResistanceThermal;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _armorPct;

        public double ArmorPct
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_armorPct == null)
                        {
                            _armorPct = _directEntity.ArmorPct;
                            return (double)_armorPct;
                        }

                        return (double)_armorPct;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _armorHitPoints;

        public double ArmorHitPoints
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_armorHitPoints == null)
                        {
                            _armorHitPoints = _directEntity.Armor;
                            return _armorHitPoints ?? 0;
                        }

                        return (double)_armorHitPoints;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }


        private double? _armorResistanceEM;
        public double ArmorResistanceEM
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_armorResistanceEM == null)
                        {
                            _armorResistanceEM = _directEntity.ArmorResistanceEm;
                            return _armorResistanceEM ?? 0;
                        }

                        return (double)_armorResistanceEM;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _armorResistanceExplosive;
        public double ArmorResistanceExplosive
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_armorResistanceExplosive == null)
                        {
                            _armorResistanceExplosive = _directEntity.ArmorResistanceExplosion;
                            return _armorResistanceExplosive ?? 0;
                        }

                        return (double)_armorResistanceExplosive;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _armorResistanceKinetic;
        public double ArmorResistanceKinetic
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_armorResistanceKinetic == null)
                        {
                            _armorResistanceKinetic = _directEntity.ArmorResistanceKinetic;
                            return _armorResistanceKinetic ?? 0;
                        }

                        return (double)_armorResistanceKinetic;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _armorResistanceThermal;
        public double ArmorResistanceThermal
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_armorResistanceThermal == null)
                        {
                            if (_directEntity.ArmorResistanceThermal != null)
                            {
                                _armorResistanceThermal = _directEntity.ArmorResistanceThermal;
                                return (double)_armorResistanceThermal;
                            }

                            _armorResistanceThermal = 0;
                            return (double)_armorResistanceThermal;
                        }

                        return (double)_armorResistanceThermal;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _structurePct;

        public double StructurePct
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_structurePct == null)
                        {
                            _structurePct = _directEntity.StructurePct;
                            return (double)_structurePct;
                        }

                        return (double)_structurePct;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _structureHitPoints;

        public double StructureHitPoints
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_structureHitPoints == null)
                        {
                            _structureHitPoints = _directEntity.Structure;
                            return _structureHitPoints ?? 0;
                        }

                        return (double)_structureHitPoints;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        public double? _effectiveHitpointsViaEM;
        public double EffectiveHitpointsViaEM
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_effectiveHitpointsViaEM == null)
                        {
                            //
                            // this does not take into account hull, but most things have so very little hull (LCOs might be a problem!)
                            //
                            _effectiveHitpointsViaEM = ((ShieldHitPoints * ShieldResistanceEM) + (ArmorHitPoints * ArmorResistanceEM));
                            return (double)_effectiveHitpointsViaEM;
                        }

                        return (double)_effectiveHitpointsViaEM;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        public double? _effectiveHitpointsViaExplosive;
        public double EffectiveHitpointsViaExplosive
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_effectiveHitpointsViaExplosive == null)
                        {
                            //
                            // this does not take into account hull, but most things have so very little hull (LCOs might be a problem!)
                            //
                            _effectiveHitpointsViaExplosive = ((ShieldHitPoints * ShieldResistanceExplosive) + (ArmorHitPoints * ArmorResistanceExplosive));
                            return (double)_effectiveHitpointsViaExplosive;
                        }

                        return (double)_effectiveHitpointsViaExplosive;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        public double? _effectiveHitpointsViaKinetic;
        public double EffectiveHitpointsViaKinetic
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_effectiveHitpointsViaKinetic == null)
                        {
                            //
                            // this does not take into account hull, but most things have so very little hull (LCOs might be a problem!)
                            //
                            _effectiveHitpointsViaKinetic = ((ShieldHitPoints * ShieldResistanceKinetic) + (ArmorHitPoints * ArmorResistanceKinetic));
                            return (double)_effectiveHitpointsViaKinetic;
                        }

                        return (double)_effectiveHitpointsViaKinetic;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        public double? _effectiveHitpointsViaThermal;
        public double EffectiveHitpointsViaThermal
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_effectiveHitpointsViaThermal == null)
                        {
                            //
                            // this does not take into account hull, but most things have so very little hull (LCOs might be a problem!)
                            //
                            _effectiveHitpointsViaThermal = ((ShieldHitPoints * ShieldResistanceThermal) + (ArmorHitPoints * ArmorResistanceThermal));
                            return (double)_effectiveHitpointsViaThermal;
                        }

                        return (double)_effectiveHitpointsViaThermal;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double _shieldRadius;
        public double ShieldRadius
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_shieldRadius == 0 && _directEntity.GroupId == (int)Group.ControlTower)
                        {
                            _shieldRadius = _directEntity.ShieldRadius;
                            return (double)_shieldRadius;
                        }

                        return (double)_shieldRadius;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private bool? _canWarpScramble = null;
        public bool CanWarpScramble
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_canWarpScramble == null)
                        {
                            _canWarpScramble = _directEntity.CanWarpScramble;
                            return (bool)_canWarpScramble;
                        }

                        return (bool)_canWarpScramble;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isNpc;

        public bool IsNpc
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isNpc == null)
                        {
                            _isNpc = _directEntity.IsNpc;
                            return (bool)_isNpc;
                        }

                        return (bool)_isNpc;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private double? _velocity;

        public double Velocity
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_velocity == null)
                        {
                            _velocity = _directEntity.Velocity;
                            return (double)_velocity;
                        }

                        return (double)_velocity;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _transversalVelocity;
        public double TransversalVelocity
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_transversalVelocity == null)
                        {
                            _transversalVelocity = _directEntity.TransversalVelocity;
                            return (double)_transversalVelocity;
                        }

                        return (double)_transversalVelocity;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _angularVelocity;
        public double AngularVelocity
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_angularVelocity == null)
                        {
                            _angularVelocity = _directEntity.AngularVelocity;
                            return (double)_angularVelocity;
                        }

                        return (double)_angularVelocity;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _xCoordinate;
        public double XCoordinate
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_xCoordinate == null)
                        {
                            _xCoordinate = _directEntity.X;
                            return (double)_xCoordinate;
                        }

                        return (double)_xCoordinate;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _yCoordinate;
        public double YCoordinate
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_yCoordinate == null)
                        {
                            _yCoordinate = _directEntity.Y;
                            return (double)_yCoordinate;
                        }

                        return (double)_yCoordinate;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private double? _zCoordinate;
        public double ZCoordinate
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_zCoordinate == null)
                        {
                            _zCoordinate = _directEntity.Z;
                            return (double)_zCoordinate;
                        }

                        return (double)_zCoordinate;
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private bool? _isTarget;

        public bool IsTarget
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isTarget == null)
                        {
                            _isTarget = _directEntity.IsTarget;
                            return (bool)_isTarget;
                        }

                        return (bool)_isTarget;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isCorrectSizeForMyWeapons;

        public bool IsCorrectSizeForMyWeapons
        {
            get
            {
                try
                {
                    if (_isCorrectSizeForMyWeapons == null)
                    {
                        if (QMCache.Instance.MyShipEntity.IsFrigate)
                        {
                            if (IsFrigate)
                            {
                                _isCorrectSizeForMyWeapons = true;
                                return (bool)_isCorrectSizeForMyWeapons;
                            }
                        }

                        if (QMCache.Instance.MyShipEntity.IsCruiser)
                        {
                            if (IsCruiser)
                            {
                                _isCorrectSizeForMyWeapons = true;
                                return (bool)_isCorrectSizeForMyWeapons;
                            }
                        }

                        if (QMCache.Instance.MyShipEntity.IsBattlecruiser || QMCache.Instance.MyShipEntity.IsBattleship)
                        {
                            if (IsBattleship || IsBattlecruiser)
                            {
                                _isCorrectSizeForMyWeapons = true;
                                return (bool)_isCorrectSizeForMyWeapons;
                            }
                        }

                        return false;
                    }

                    return (bool)_isCorrectSizeForMyWeapons;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                }

                return false;
            }

        }

        private bool? _isPreferredPrimaryWeaponTarget;

        public bool isPreferredPrimaryWeaponTarget
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isPreferredPrimaryWeaponTarget == null)
                        {
                            if (Combat.PreferredPrimaryWeaponTarget != null && Combat.PreferredPrimaryWeaponTarget.Id == Id)
                            {
                                _isPreferredPrimaryWeaponTarget = true;
                                return (bool)_isPreferredPrimaryWeaponTarget;
                            }

                            _isPreferredPrimaryWeaponTarget = false;
                            return (bool)_isPreferredPrimaryWeaponTarget;
                        }

                        return (bool)_isPreferredPrimaryWeaponTarget;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isPrimaryWeaponKillPriority;

        public bool IsPrimaryWeaponKillPriority
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isPrimaryWeaponKillPriority == null)
                        {
                            if (Combat.PrimaryWeaponPriorityTargets.Any(e => e.Entity.Id == Id))
                            {
                                _isPrimaryWeaponKillPriority = true;
                                return (bool)_isPrimaryWeaponKillPriority;
                            }

                            _isPrimaryWeaponKillPriority = false;
                            return (bool)_isPrimaryWeaponKillPriority;
                        }

                        return (bool)_isPrimaryWeaponKillPriority;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isPreferredDroneTarget;

        public bool isPreferredDroneTarget
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isPreferredDroneTarget == null)
                        {
                            if (Drones.PreferredDroneTarget != null && Drones.PreferredDroneTarget.Id == _directEntity.Id)
                            {
                                _isPreferredDroneTarget = true;
                                return (bool)_isPreferredDroneTarget;
                            }

                            _isPreferredDroneTarget = false;
                            return (bool)_isPreferredDroneTarget;
                        }

                        return (bool)_isPreferredDroneTarget;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _IsDroneKillPriority;

        public bool IsDroneKillPriority
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_IsDroneKillPriority == null)
                        {
                            if (Drones.DronePriorityTargets.Any(e => e.Entity.Id == _directEntity.Id))
                            {
                                _IsDroneKillPriority = true;
                                return (bool)_IsDroneKillPriority;
                            }

                            _IsDroneKillPriority = false;
                            return (bool)_IsDroneKillPriority;
                        }

                        return (bool)_IsDroneKillPriority;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _IsTooCloseTooFastTooSmallToHit;

        public bool IsTooCloseTooFastTooSmallToHit
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_IsTooCloseTooFastTooSmallToHit == null)
                        {
                            if (IsNPCFrigate || IsFrigate)
                            {
                                if (Combat.DoWeCurrentlyHaveTurretsMounted() && Drones.UseDrones)
                                {
                                    if (Distance < Combat.DistanceNPCFrigatesShouldBeIgnoredByPrimaryWeapons
                                     && Velocity > Combat.SpeedNPCFrigatesShouldBeIgnoredByPrimaryWeapons)
                                    {
                                        _IsTooCloseTooFastTooSmallToHit = true;
                                        return (bool)_IsTooCloseTooFastTooSmallToHit;
                                    }

                                    _IsTooCloseTooFastTooSmallToHit = false;
                                    return (bool)_IsTooCloseTooFastTooSmallToHit;
                                }

                                _IsTooCloseTooFastTooSmallToHit = false;
                                return (bool)_IsTooCloseTooFastTooSmallToHit;
                            }
                        }

                        return _IsTooCloseTooFastTooSmallToHit ?? false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _IsReadyToShoot;

        public bool IsReadyToShoot
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_IsReadyToShoot == null)
                        {
                            if (!HasExploded && IsTarget && !IsIgnored && Distance < Combat.MaxRange)
                            {
                                //if (_directEntity.Distance < Combat.MaxRange)
                                //{
                                //if (QMCache.Instance.Entities.Any(t => t.Id == Id))
                                //{
                                _IsReadyToShoot = true;
                                return (bool)_IsReadyToShoot;
                                //}

                                //_IsReadyToShoot = false;
                                //return _IsReadyToShoot ?? false;
                                //}

                                //_IsReadyToShoot = false;
                                //return _IsReadyToShoot ?? false;
                            }
                        }

                        return _IsReadyToShoot ?? false;
                    }

                    if (Logging.DebugIsReadyToShoot) Logging.Log("IsReadyToShoot", "_directEntity is null or invalid", Logging.Debug);
                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _IsReadyToTarget;

        public bool IsReadyToTarget
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_IsReadyToTarget == null)
                        {
                            if (!HasExploded && !IsTarget && !IsTargeting && Distance < Combat.MaxTargetRange)
                            {
                                _IsReadyToTarget = true;
                                return (bool)_IsReadyToTarget;
                            }

                            _IsReadyToTarget = false;
                            return (bool)_IsReadyToTarget;
                        }

                        return (bool)_IsReadyToTarget;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isHigherPriorityPresent;

        public bool IsHigherPriorityPresent
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isHigherPriorityPresent == null)
                        {
                            if (Combat.PrimaryWeaponPriorityTargets.Any() || Drones.DronePriorityTargets.Any())
                            {
                                if (Combat.PrimaryWeaponPriorityTargets.Any())
                                {
                                    if (Combat.PrimaryWeaponPriorityTargets.Any(pt => pt.EntityID == Id))
                                    {
                                        PrimaryWeaponPriority _currentPrimaryWeaponPriority = Combat.PrimaryWeaponPriorityEntities.Where(t => t.Id == _directEntity.Id).Select(pt => pt.PrimaryWeaponPriorityLevel).FirstOrDefault();

                                        if (!Combat.PrimaryWeaponPriorityEntities.All(pt => pt.PrimaryWeaponPriorityLevel < _currentPrimaryWeaponPriority && pt.Distance < Combat.MaxRange))
                                        {
                                            _isHigherPriorityPresent = true;
                                            return (bool)_isHigherPriorityPresent;
                                        }

                                        _isHigherPriorityPresent = false;
                                        return (bool)_isHigherPriorityPresent;
                                    }

                                    if (Combat.PrimaryWeaponPriorityEntities.Any(e => e.Distance < Combat.MaxRange))
                                    {
                                        _isHigherPriorityPresent = true;
                                        return (bool)_isHigherPriorityPresent;
                                    }

                                    _isHigherPriorityPresent = false;
                                    return (bool)_isHigherPriorityPresent;
                                }

                                if (Drones.DronePriorityTargets.Any())
                                {
                                    if (Drones.DronePriorityTargets.Any(pt => pt.EntityID == _directEntity.Id))
                                    {
                                        DronePriority _currentEntityDronePriority = Drones.DronePriorityEntities.Where(t => t.Id == _directEntity.Id).Select(pt => pt.DronePriorityLevel).FirstOrDefault();

                                        if (!Drones.DronePriorityEntities.All(pt => pt.DronePriorityLevel < _currentEntityDronePriority && pt.Distance < Drones.MaxDroneRange))
                                        {
                                            return true;
                                        }

                                        return false;
                                    }

                                    if (Drones.DronePriorityEntities.Any(e => e.Distance < Drones.MaxDroneRange))
                                    {
                                        _isHigherPriorityPresent = true;
                                        return (bool)_isHigherPriorityPresent;
                                    }

                                    _isHigherPriorityPresent = false;
                                    return (bool)_isHigherPriorityPresent;
                                }

                                _isHigherPriorityPresent = false;
                                return (bool)_isHigherPriorityPresent;
                            }
                        }

                        return _isHigherPriorityPresent ?? false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isActiveTarget;

        public bool IsActiveTarget
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isActiveTarget == null)
                        {
                            if (_directEntity.IsActiveTarget)
                            {
                                _isActiveTarget = true;
                                return (bool)_isActiveTarget;
                            }

                            _isActiveTarget = false;
                            return (bool)_isActiveTarget;
                        }

                        return (bool)_isActiveTarget;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isLootTarget;

        public bool IsLootTarget
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        //if (QMSettings.Instance.FleetSupportSlave)
                        //{
                        //    return false;
                        //}

                        if (_isLootTarget == null)
                        {
                            if (QMCache.Instance.ListofContainersToLoot.Contains(Id))
                            {
                                return true;
                            }

                            _isLootTarget = false;
                            return (bool)_isLootTarget;
                        }

                        return (bool)_isLootTarget;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }


        private bool? _isCurrentTarget;

        public bool IsCurrentTarget
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isCurrentTarget == null)
                        {
                            if (Combat.CurrentWeaponTarget() != null)
                            {
                                _isCurrentTarget = true;
                                return (bool)_isCurrentTarget;
                            }

                            _isCurrentTarget = false;
                            return (bool)_isCurrentTarget;
                        }

                        return (bool)_isCurrentTarget;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isLastTargetPrimaryWeaponsWereShooting;

        public bool IsLastTargetPrimaryWeaponsWereShooting
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isLastTargetPrimaryWeaponsWereShooting == null)
                        {
                            if (Combat.LastTargetPrimaryWeaponsWereShooting != null && Id == Combat.LastTargetPrimaryWeaponsWereShooting.Id)
                            {
                                _isLastTargetPrimaryWeaponsWereShooting = true;
                                return (bool)_isLastTargetPrimaryWeaponsWereShooting;
                            }

                            _isLastTargetPrimaryWeaponsWereShooting = false;
                            return (bool)_isLastTargetPrimaryWeaponsWereShooting;
                        }

                        return (bool)_isLastTargetPrimaryWeaponsWereShooting;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isLastTargetDronesWereShooting;

        public bool IsLastTargetDronesWereShooting
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isLastTargetDronesWereShooting == null)
                        {
                            if (Drones.LastTargetIDDronesEngaged != null && Id == Drones.LastTargetIDDronesEngaged)
                            {
                                _isLastTargetDronesWereShooting = true;
                                return (bool)_isLastTargetDronesWereShooting;
                            }

                            _isLastTargetDronesWereShooting = false;
                            return (bool)_isLastTargetDronesWereShooting;
                        }

                        return (bool)_isLastTargetDronesWereShooting;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isInOptimalRange;

        public bool IsInOptimalRange
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isInOptimalRange == null)
                        {
                            if (NavigateOnGrid.SpeedTank && NavigateOnGrid.OrbitDistance != 0)
                            {
                                if (NavigateOnGrid.OptimalRange == 0)
                                {
                                    MissionSettings.MissionOptimalRange = NavigateOnGrid.OrbitDistance + 5000;
                                }
                            }

                            if (MissionSettings.MissionOptimalRange != 0 || NavigateOnGrid.OptimalRange != 0)
                            {
                                double optimal = 0;

                                if (MissionSettings.MissionOptimalRange != null && MissionSettings.MissionOptimalRange != 0)
                                {
                                    optimal = (double)MissionSettings.MissionOptimalRange;
                                }
                                else if (NavigateOnGrid.OptimalRange != 0) //do we really need this condition? we cant even get in here if one of them is not != 0, that is the idea, if its 0 we sure as hell do not want to use it as the optimal
                                {
                                    optimal = NavigateOnGrid.OptimalRange;
                                }

                                if (optimal > QMCache.Instance.ActiveShip.MaxTargetRange)
                                {
                                    optimal = QMCache.Instance.ActiveShip.MaxTargetRange - 500;
                                }

                                if (Combat.DoWeCurrentlyHaveTurretsMounted()) //Lasers, Projectile, and Hybrids
                                {
                                    if (Distance > Combat.InsideThisRangeIsHardToTrack)
                                    {
                                        if (Distance < (optimal * 1.5) && Distance < QMCache.Instance.ActiveShip.MaxTargetRange)
                                        {
                                            _isInOptimalRange = true;
                                            return (bool)_isInOptimalRange;
                                        }
                                    }
                                }
                                else //missile boats - use max range
                                {
                                    optimal = Combat.MaxRange;
                                    if (Distance < optimal)
                                    {
                                        _isInOptimalRange = true;
                                        return (bool)_isInOptimalRange;
                                    }

                                    _isInOptimalRange = false;
                                    return (bool)_isInOptimalRange;
                                }

                                _isInOptimalRange = false;
                                return (bool)_isInOptimalRange;
                            }

                            // If you have no optimal you have to assume the entity is within Optimal... (like missiles)
                            _isInOptimalRange = true;
                            return (bool)_isInOptimalRange;
                        }

                        return (bool)_isInOptimalRange;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsInOptimalRangeOrNothingElseAvail
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        //if it is in optimal, return true, we want to shoot things that are in optimal!
                        if (IsInOptimalRange)
                        {
                            return true;
                        }

                        //Any targets which are not the current target and is not a wreck or container
                        if (!QMCache.Instance.Targets.Any(i => i.Id != Id && !i.IsContainer))
                        {
                            return true;
                        }

                        //something else must be available to shoot, and this entity is not in optimal, return false;
                        return false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsInDroneRange
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (Drones.MaxDroneRange > 0) //&& QMCache.Instance.UseDrones)
                        {
                            if (Distance < Drones.MaxDroneRange)
                            {
                                return true;
                            }

                            return false;
                        }

                        return false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsDronePriorityTarget
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {

                        if (Drones.DronePriorityTargets.All(i => i.EntityID != Id))
                        {
                            return false;
                        }

                        return true;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsPriorityWarpScrambler
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (Combat.PrimaryWeaponPriorityTargets.Any(pt => pt.EntityID == Id))
                        {
                            if (PrimaryWeaponPriorityLevel == PrimaryWeaponPriority.WarpScrambler)
                            {
                                return true;
                            }

                            //return false; //check for drone priority targets too!
                        }

                        if (Drones.DronePriorityTargets.Any(pt => pt.EntityID == Id))
                        {
                            if (DronePriorityLevel == DronePriority.WarpScrambler)
                            {
                                return true;
                            }

                            return false;
                        }

                        return false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsPrimaryWeaponPriorityTarget
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (Combat.PrimaryWeaponPriorityTargets.Any(i => i.EntityID == Id))
                        {
                            return true;
                        }

                        return false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private PrimaryWeaponPriority? _primaryWeaponPriorityLevel;
        public PrimaryWeaponPriority PrimaryWeaponPriorityLevel
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_primaryWeaponPriorityLevel == null)
                        {
                            if (Combat.PrimaryWeaponPriorityTargets.Any(pt => pt.EntityID == Id))
                            {
                                _primaryWeaponPriorityLevel = Combat.PrimaryWeaponPriorityTargets.Where(t => t.Entity.IsTarget && t.EntityID == Id)
                                                                                                                            .Select(pt => pt.PrimaryWeaponPriority)
                                                                                                                            .FirstOrDefault();
                                return (PrimaryWeaponPriority)_primaryWeaponPriorityLevel;
                            }

                            return PrimaryWeaponPriority.NotUsed;
                        }

                        return (PrimaryWeaponPriority)_primaryWeaponPriorityLevel;
                    }

                    return PrimaryWeaponPriority.NotUsed;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return PrimaryWeaponPriority.NotUsed;
                }
            }
        }

        public DronePriority DronePriorityLevel
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {

                        if (Drones.DronePriorityTargets.Any(pt => pt.EntityID == _directEntity.Id))
                        {
                            DronePriority currentTargetPriority = Drones.DronePriorityTargets.Where(t => t.Entity.IsTarget
                                                                                                                        && t.EntityID == Id)
                                                                                                                        .Select(pt => pt.DronePriority)
                                                                                                                        .FirstOrDefault();

                            return currentTargetPriority;
                        }

                        return DronePriority.NotUsed;
                    }

                    return DronePriority.NotUsed;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return DronePriority.NotUsed;
                }
            }
        }

        private bool? _isTargeting;

        public bool IsTargeting
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isTargeting == null)
                        {
                            _isTargeting = _directEntity.IsTargeting;
                            return (bool)_isTargeting;
                        }

                        return (bool)_isTargeting;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isTargetedBy;

        public bool IsTargetedBy
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isTargetedBy == null)
                        {
                            _isTargetedBy = _directEntity.IsTargetedBy;
                            return (bool)_isTargetedBy;
                        }

                        return (bool)_isTargetedBy;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isAttacking;
        public bool IsAttacking
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isAttacking == null)
                        {
                            _isAttacking = _directEntity.IsAttacking;
                            return (bool)_isAttacking;
                        }

                        return (bool)_isAttacking;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsWreckEmpty
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (GroupId == (int)Group.Wreck)
                        {
                            return _directEntity.IsEmpty;
                        }

                        return false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool HasReleased
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        return _directEntity.HasReleased;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool HasExploded
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        return _directEntity.HasExploded;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isEwarTarget;

        public bool IsEwarTarget
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isEwarTarget == null)
                        {
                            bool result = false;
                            result |= IsWarpScramblingMe;
                            result |= IsWebbingMe;
                            result |= IsNeutralizingMe;
                            result |= IsJammingMe;
                            result |= IsSensorDampeningMe;
                            result |= IsTargetPaintingMe;
                            result |= IsTrackingDisruptingMe;
                            _isEwarTarget = result;
                            return (bool)_isEwarTarget;
                        }

                        return (bool)_isEwarTarget;
                    }

                    return _isEwarTarget ?? false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public DronePriority IsActiveDroneEwarType
        {
            get
            {
                try
                {
                    if (IsWarpScramblingMe)
                    {
                        return DronePriority.WarpScrambler;
                    }

                    if (IsWebbingMe)
                    {
                        return DronePriority.Webbing;
                    }

                    if (IsNeutralizingMe)
                    {
                        return DronePriority.PriorityKillTarget;
                    }

                    if (IsJammingMe)
                    {
                        return DronePriority.PriorityKillTarget;
                    }

                    if (IsSensorDampeningMe)
                    {
                        return DronePriority.PriorityKillTarget;
                    }

                    if (IsTargetPaintingMe)
                    {
                        return DronePriority.PriorityKillTarget;
                    }

                    if (IsTrackingDisruptingMe)
                    {
                        return DronePriority.PriorityKillTarget;
                    }

                    return DronePriority.NotUsed;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return DronePriority.NotUsed;
                }
            }
        }

        public PrimaryWeaponPriority IsActivePrimaryWeaponEwarType
        {
            get
            {
                try
                {
                    if (IsWarpScramblingMe)
                    {
                        return PrimaryWeaponPriority.WarpScrambler;
                    }

                    if (IsWebbingMe)
                    {
                        return PrimaryWeaponPriority.Webbing;
                    }

                    if (IsNeutralizingMe)
                    {
                        return PrimaryWeaponPriority.Neutralizing;
                    }

                    if (IsJammingMe)
                    {
                        return PrimaryWeaponPriority.Jamming;
                    }

                    if (IsSensorDampeningMe)
                    {
                        return PrimaryWeaponPriority.Dampening;
                    }

                    if (IsTargetPaintingMe)
                    {
                        return PrimaryWeaponPriority.TargetPainting;
                    }

                    if (IsTrackingDisruptingMe)
                    {
                        return PrimaryWeaponPriority.TrackingDisrupting;
                    }

                    return PrimaryWeaponPriority.NotUsed;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return PrimaryWeaponPriority.NotUsed;
                }
            }
        }

        public bool IsWarpScramblingMe
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (QMCache.Instance.ListOfWarpScramblingEntities.Contains(Id))
                        {
                            return true;
                        }

                        if (_directEntity.Attacks.Contains("effects.WarpScramble"))
                        {
                            if (!QMCache.Instance.ListOfWarpScramblingEntities.Contains(Id))
                            {
                                QMCache.Instance.ListOfWarpScramblingEntities.Add(Id);
                            }

                            return true;
                        }

                        return false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsWebbingMe
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {

                        if (_directEntity.Attacks.Contains("effects.ModifyTargetSpeed"))
                        {
                            if (!QMCache.Instance.ListofWebbingEntities.Contains(Id)) QMCache.Instance.ListofWebbingEntities.Add(Id);
                            return true;
                        }

                        if (QMCache.Instance.ListofWebbingEntities.Contains(Id))
                        {
                            return true;
                        }

                        return false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsNeutralizingMe
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_directEntity.ElectronicWarfare.Contains("ewEnergyNeut"))
                        {
                            if (!QMCache.Instance.ListNeutralizingEntities.Contains(Id)) QMCache.Instance.ListNeutralizingEntities.Add(Id);
                            return true;
                        }

                        if (QMCache.Instance.ListNeutralizingEntities.Contains(Id))
                        {
                            return true;
                        }

                        return false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsJammingMe
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_directEntity.ElectronicWarfare.Contains("electronic"))
                        {
                            if (!QMCache.Instance.ListOfJammingEntities.Contains(Id)) QMCache.Instance.ListOfJammingEntities.Add(Id);
                            return true;
                        }

                        if (QMCache.Instance.ListOfJammingEntities.Contains(Id))
                        {
                            return true;
                        }

                        return false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsSensorDampeningMe
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_directEntity.ElectronicWarfare.Contains("ewRemoteSensorDamp"))
                        {
                            if (!QMCache.Instance.ListOfDampenuingEntities.Contains(Id)) QMCache.Instance.ListOfDampenuingEntities.Add(Id);
                            return true;
                        }

                        if (QMCache.Instance.ListOfDampenuingEntities.Contains(Id))
                        {
                            return true;
                        }

                        return false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsTargetPaintingMe
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_directEntity.ElectronicWarfare.Contains("ewTargetPaint"))
                        {
                            if (!QMCache.Instance.ListOfTargetPaintingEntities.Contains(Id)) QMCache.Instance.ListOfTargetPaintingEntities.Add(Id);
                            return true;
                        }

                        if (QMCache.Instance.ListOfTargetPaintingEntities.Contains(Id))
                        {
                            return true;
                        }

                        return false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsTrackingDisruptingMe
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {

                        if (_directEntity.ElectronicWarfare.Contains("ewTrackingDisrupt"))
                        {
                            if (!QMCache.Instance.ListOfTrackingDisruptingEntities.Contains(Id)) QMCache.Instance.ListOfTrackingDisruptingEntities.Add(Id);
                            return true;
                        }

                        if (QMCache.Instance.ListOfTrackingDisruptingEntities.Contains(Id))
                        {
                            return true;
                        }

                        return false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public int Health
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        return (int)((ShieldPct + ArmorPct + StructurePct) * 100);
                    }

                    return 0;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return 0;
                }
            }
        }

        private bool? _isEntityIShouldKeepShooting;

        public bool IsEntityIShouldKeepShooting
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isEntityIShouldKeepShooting == null)
                        {
                            //
                            // Is our current target already in armor? keep shooting the same target if so...
                            //
                            if (IsReadyToShoot
                                && IsInOptimalRange && !IsLargeCollidable
                                && (((!IsFrigate && !IsNPCFrigate) || !IsTooCloseTooFastTooSmallToHit))
                                    && ArmorPct * 100 < Combat.DoNotSwitchTargetsIfTargetHasMoreThanThisArmorDamagePercentage)
                            {
                                if (Logging.DebugGetBestTarget) Logging.Log("EntityCache.IsEntityIShouldKeepShooting", "[" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + MaskedId + " GroupID [" + GroupId + "]] has less than 60% armor, keep killing this target", Logging.Debug);
                                _isEntityIShouldKeepShooting = true;
                                return (bool)_isEntityIShouldKeepShooting;
                            }

                            _isEntityIShouldKeepShooting = false;
                            return (bool)_isEntityIShouldKeepShooting;
                        }

                        return (bool)_isEntityIShouldKeepShooting;
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    Logging.Log("EntityCache.IsEntityIShouldKeepShooting", "Exception: [" + ex + "]", Logging.Debug);
                }

                return false;
            }
        }

        private bool? _isEntityIShouldKeepShootingWithDrones;

        public bool IsEntityIShouldKeepShootingWithDrones
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isEntityIShouldKeepShootingWithDrones == null)
                        {
                            //
                            // Is our current target already in armor? keep shooting the same target if so...
                            //
                            if (IsReadyToShoot
                                && IsInDroneRange
                                && !IsLargeCollidable
                                && ((IsFrigate || IsNPCFrigate) || Drones.DronesKillHighValueTargets)
                                && ShieldPct * 100 < 80)
                            {
                                if (Logging.DebugGetBestTarget) Logging.Log("EntityCache.IsEntityIShouldKeepShootingWithDrones", "[" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + MaskedId + " GroupID [" + GroupId + "]] has less than 60% armor, keep killing this target", Logging.Debug);
                                _isEntityIShouldKeepShootingWithDrones = true;
                                return (bool)_isEntityIShouldKeepShootingWithDrones;
                            }

                            _isEntityIShouldKeepShootingWithDrones = false;
                            return (bool)_isEntityIShouldKeepShootingWithDrones;
                        }

                        return (bool)_isEntityIShouldKeepShootingWithDrones;
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    Logging.Log("EntityCache.IsEntityIShouldKeepShooting", "Exception: [" + ex + "]", Logging.Debug);
                }

                return false;
            }
        }

        private bool? _isSentry;

        public bool IsSentry
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isSentry == null)
                        {
                            if (QMCache.Instance.EntityIsSentry.Any() && QMCache.Instance.EntityIsSentry.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsSentry", "We have [" + QMCache.Instance.EntityIsSentry.Count() + "] Entities in QMCache.Instance.EntityIsSentry", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsSentry.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsSentry.TryGetValue(Id, out value))
                                {
                                    _isSentry = value;
                                    return (bool)_isSentry;
                                }
                            }

                            bool result = false;
                            //if (GroupId == (int)Group.SentryGun) return true;
                            result |= (GroupId == (int)Group.ProtectiveSentryGun);
                            result |= (GroupId == (int)Group.MobileSentryGun);
                            result |= (GroupId == (int)Group.DestructibleSentryGun);
                            result |= (GroupId == (int)Group.MobileMissileSentry);
                            result |= (GroupId == (int)Group.MobileProjectileSentry);
                            result |= (GroupId == (int)Group.MobileLaserSentry);
                            result |= (GroupId == (int)Group.MobileHybridSentry);
                            result |= (GroupId == (int)Group.DeadspaceOverseersSentry);
                            result |= (GroupId == (int)Group.StasisWebificationBattery);
                            result |= (GroupId == (int)Group.EnergyNeutralizingBattery);
                            _isSentry = result;
                            QMCache.Instance.EntityIsSentry.Add(Id, result);
                            return (bool)_isSentry;
                        }

                        return (bool)_isSentry;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isIgnored;

        public bool IsIgnored
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isIgnored == null)
                        {
                            //IsIgnoredRefreshes++;
                            //if (QMCache.Instance.Entities.All(t => t.Id != _directEntity.Id))
                            //{
                            //    IsIgnoredRefreshes = IsIgnoredRefreshes + 1000;
                            //    _isIgnored = true;
                            //    return _isIgnored ?? true;
                            //}
                            if (CombatMissionCtrl.IgnoreTargets != null && CombatMissionCtrl.IgnoreTargets.Any())
                            {
                                _isIgnored = CombatMissionCtrl.IgnoreTargets.Contains(Name.Trim());
                                if ((bool)_isIgnored)
                                {
                                    if (Combat.PreferredPrimaryWeaponTarget != null && Combat.PreferredPrimaryWeaponTarget.Id != Id)
                                    {
                                        Combat.PreferredPrimaryWeaponTarget = null;
                                    }

                                    if (QMCache.Instance.EntityIsLowValueTarget.ContainsKey(Id))
                                    {
                                        QMCache.Instance.EntityIsLowValueTarget.Remove(Id);
                                    }

                                    if (QMCache.Instance.EntityIsHighValueTarget.ContainsKey(Id))
                                    {
                                        QMCache.Instance.EntityIsHighValueTarget.Remove(Id);
                                    }

                                    if (Logging.DebugEntityCache) Logging.Log("EntityCache.IsIgnored", "[" + Name + "][" + Math.Round(Distance / 1000, 0) + "k][" + MaskedId + "] isIgnored [" + _isIgnored + "]", Logging.Debug);
                                    return (bool)_isIgnored;
                                }

                                _isIgnored = false;
                                if (Logging.DebugEntityCache) Logging.Log("EntityCache.IsIgnored", "[" + Name + "][" + Math.Round(Distance / 1000, 0) + "k][" + MaskedId + "] isIgnored [" + _isIgnored + "]", Logging.Debug);
                                return (bool)_isIgnored;
                            }

                            _isIgnored = false;
                            if (Logging.DebugEntityCache) Logging.Log("EntityCache.IsIgnored", "[" + Name + "][" + Math.Round(Distance / 1000, 0) + "k][" + MaskedId + "] isIgnored [" + _isIgnored + "]", Logging.Debug);
                            return (bool)_isIgnored;
                        }

                        if (Logging.DebugEntityCache) Logging.Log("EntityCache.IsIgnored", "[" + Name + "][" + Math.Round(Distance / 1000, 0) + "k][" + MaskedId + "] isIgnored [" + _isIgnored + "]", Logging.Debug);
                        return (bool)_isIgnored;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool HaveLootRights
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {

                        if (GroupId == (int)Group.SpawnContainer)
                        {
                            return true;
                        }

                        bool result = false;
                        if (QMCache.Instance.ActiveShip.Entity != null)
                        {
                            result |= _directEntity.CorpId == QMCache.Instance.ActiveShip.Entity.CorpId;
                            result |= _directEntity.OwnerId == QMCache.Instance.ActiveShip.Entity.CharId;
                            //
                            // It would be nice if this were eventually extended to detect and include 'abandoned' wrecks (blue ones).
                            // I do not yet know what attributed actually change when that happens. We should collect some data.
                            //
                            return result;
                        }

                        return false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private int? _targetValue;

        public int? TargetValue
        {
            get
            {
                try
                {
                    int result = -1;

                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_targetValue == null)
                        {
                            ShipTargetValue value = null;

                            try
                            {
                                value = QMCache.Instance.ShipTargetValues.FirstOrDefault(v => v.GroupId == GroupId);
                            }
                            catch (Exception exception)
                            {
                                if (Logging.DebugShipTargetValues) Logging.Log("TargetValue", "exception [" + exception + "]", Logging.Debug);
                            }

                            if (value == null)
                            {

                                if (IsNPCBattleship)
                                {
                                    _targetValue = 4;
                                }
                                else if (IsNPCBattlecruiser)
                                {
                                    _targetValue = 3;
                                }
                                else if (IsNPCCruiser)
                                {
                                    _targetValue = 2;
                                }
                                else if (IsNPCFrigate)
                                {
                                    _targetValue = 0;
                                }

                                return _targetValue ?? -1;
                            }

                            _targetValue = value.TargetValue;
                            return _targetValue;
                        }

                        return _targetValue;
                    }

                    return result;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return -1;
                }
            }
        }

        private bool? _isHighValueTarget;

        public bool IsHighValueTarget
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isHighValueTarget == null)
                        {
                            if (QMCache.Instance.EntityIsHighValueTarget.Any() && QMCache.Instance.EntityIsHighValueTarget.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsHighValueTarget", "We have [" + QMCache.Instance.EntityIsHighValueTarget.Count() + "] Entities in QMCache.Instance.EntityIsHighValueTarget", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsHighValueTarget.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsHighValueTarget.TryGetValue(Id, out value))
                                {
                                    _isHighValueTarget = value;
                                    return (bool)_isHighValueTarget;
                                }
                            }

                            if (TargetValue != null)
                            {
                                if (!IsIgnored || !IsContainer || !IsBadIdea || !IsCustomsOffice || !IsFactionWarfareNPC || !IsPlayer)
                                {
                                    if (TargetValue >= Combat.MinimumTargetValueToConsiderTargetAHighValueTarget)
                                    {
                                        if (IsSentry && !Combat.KillSentries && !IsEwarTarget)
                                        {
                                            _isHighValueTarget = false;
                                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsHighValueTarget", "Adding [" + Name + "] to EntityIsHighValueTarget as [" + _isHighValueTarget + "]", Logging.Debug);
                                            QMCache.Instance.EntityIsHighValueTarget.Add(Id, (bool)_isHighValueTarget);
                                            return (bool)_isHighValueTarget;
                                        }

                                        _isHighValueTarget = true;
                                        if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsHighValueTarget", "Adding [" + Name + "] to EntityIsHighValueTarget as [" + _isHighValueTarget + "]", Logging.Debug);
                                        QMCache.Instance.EntityIsHighValueTarget.Add(Id, (bool)_isHighValueTarget);
                                        return (bool)_isHighValueTarget;
                                    }

                                    //if (IsLargeCollidable)
                                    //{
                                    //    return true;
                                    //}
                                }

                                _isHighValueTarget = false;
                                //do not cache things that may be ignored temporarily...
                                return (bool)_isHighValueTarget;
                            }

                            _isHighValueTarget = false;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsHighValueTarget", "Adding [" + Name + "] to EntityIsHighValueTarget as [" + _isHighValueTarget + "]", Logging.Debug);
                            QMCache.Instance.EntityIsHighValueTarget.Add(Id, (bool)_isHighValueTarget);
                            return (bool)_isHighValueTarget;
                        }

                        return (bool)_isHighValueTarget;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isLowValueTarget;

        public bool IsLowValueTarget
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isLowValueTarget == null)
                        {
                            if (QMCache.Instance.EntityIsLowValueTarget.Any() && QMCache.Instance.EntityIsLowValueTarget.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsLowValueTarget", "We have [" + QMCache.Instance.EntityIsLowValueTarget.Count() + "] Entities in QMCache.Instance.EntityIsLowValueTarget", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsLowValueTarget.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsLowValueTarget.TryGetValue(Id, out value))
                                {
                                    _isLowValueTarget = value;
                                    return (bool)_isLowValueTarget;
                                }
                            }

                            if (!IsIgnored || !IsContainer || !IsBadIdea || !IsCustomsOffice || !IsFactionWarfareNPC || !IsPlayer)
                            {
                                if (TargetValue != null && TargetValue <= Combat.MaximumTargetValueToConsiderTargetALowValueTarget)
                                {
                                    if (IsSentry && !Combat.KillSentries && !IsEwarTarget)
                                    {
                                        _isLowValueTarget = false;
                                        if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsLowValueTarget", "Adding [" + Name + "] to EntityIsLowValueTarget as [" + _isLowValueTarget + "]", Logging.Debug);
                                        QMCache.Instance.EntityIsLowValueTarget.Add(Id, (bool)_isLowValueTarget);
                                        return (bool)_isLowValueTarget;
                                    }

                                    if (TargetValue < 0 && Velocity == 0)
                                    {
                                        _isLowValueTarget = false;
                                        if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsLowValueTarget", "Adding [" + Name + "] to EntityIsLowValueTarget as [" + _isLowValueTarget + "]", Logging.Debug);
                                        QMCache.Instance.EntityIsLowValueTarget.Add(Id, (bool)_isLowValueTarget);
                                        return (bool)_isLowValueTarget;
                                    }

                                    _isLowValueTarget = true;
                                    if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsLowValueTarget", "Adding [" + Name + "] to EntityIsLowValueTarget as [" + _isLowValueTarget + "]", Logging.Debug);
                                    QMCache.Instance.EntityIsLowValueTarget.Add(Id, (bool)_isLowValueTarget);
                                    return (bool)_isLowValueTarget;
                                }

                                _isLowValueTarget = false;
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsLowValueTarget", "Adding [" + Name + "] to EntityIsLowValueTarget as [" + _isLowValueTarget + "]", Logging.Debug);
                                QMCache.Instance.EntityIsLowValueTarget.Add(Id, (bool)_isLowValueTarget);
                                return (bool)_isLowValueTarget;
                            }

                            _isLowValueTarget = false;
                            //do not cache things that may be ignored temporarily
                            return (bool)_isLowValueTarget;
                        }

                        return (bool)_isLowValueTarget;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public DirectContainerWindow CargoWindow
        {
            get
            {
                try
                {
                    if (!QMCache.Instance.Windows.Any())
                    {
                        return null;
                    }

                    return QMCache.Instance.Windows.OfType<DirectContainerWindow>().FirstOrDefault(w => w.ItemId == Id);
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return null;
                }
            }
        }

        private bool? _isValid;
        public bool IsValid
        {
            get
            {
                try
                {
                    if (_directEntity != null)
                    {
                        if (_isValid == null)
                        {
                            _isValid = _directEntity.IsValid;
                            return (bool)_isValid;
                        }

                        return (bool)_isValid;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isContainer;

        public bool IsContainer
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isContainer == null)
                        {
                            bool result = false;
                            result |= (GroupId == (int)Group.Wreck);
                            result |= (GroupId == (int)Group.CargoContainer);
                            result |= (GroupId == (int)Group.SpawnContainer);
                            result |= (GroupId == (int)Group.MissionContainer);
                            result |= (GroupId == (int)Group.DeadSpaceOverseersBelongings);
                            _isContainer = result;
                            return (bool)_isContainer;
                        }

                        return (bool)_isContainer;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isPlayer;

        public bool IsPlayer
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isPlayer == null)
                        {
                            _isPlayer = _directEntity.IsPc;
                            return (bool)_isPlayer;
                        }

                        return (bool)_isPlayer;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsTargetingMeAndNotYetTargeted
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= (((IsNpc || IsNpcByGroupID) || IsAttacking)
                                    && CategoryId == (int)CategoryID.Entity
                                    && Distance < Combat.MaxTargetRange
                                    && !IsLargeCollidable
                                    && (!IsTargeting && !IsTarget && IsTargetedBy)
                                    && !IsContainer
                                    && !IsIgnored
                                    && (!IsBadIdea || IsAttacking)
                                    && !IsEntityIShouldLeaveAlone
                                    && !IsFactionWarfareNPC
                                    && !IsStation);

                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsNotYetTargetingMeAndNotYetTargeted
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= (((IsNpc || IsNpcByGroupID) || IsAttacking || QMCache.Instance.InMission)
                                    && (!IsTargeting && !IsTarget)
                                    && !IsContainer
                                    && CategoryId == (int)CategoryID.Entity
                                    && Distance < Combat.MaxTargetRange
                                    && !IsIgnored
                                    && (!IsBadIdea || IsAttacking)
                                    && !IsEntityIShouldLeaveAlone
                                    && !IsFactionWarfareNPC
                                    && !IsLargeCollidable
                                    && !IsStation);

                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsTargetWeCanShootButHaveNotYetTargeted
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= (CategoryId == (int)CategoryID.Entity
                                    && !IsTarget
                                    && !IsTargeting
                                    && Distance < Combat.MaxTargetRange
                                    && !IsIgnored
                                    && !IsStation);

                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isFrigate;
        /// <summary>
        /// Frigate includes all elite-variants - this does NOT need to be limited to players, as we check for players specifically everywhere this is used
        /// </summary>
        ///

        public bool IsFrigate
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isFrigate == null)
                        {
                            if (QMCache.Instance.EntityIsFrigate.Any() && QMCache.Instance.EntityIsFrigate.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsFrigate", "We have [" + QMCache.Instance.EntityIsFrigate.Count() + "] Entities in QMCache.Instance.EntityIsFrigate", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsFrigate.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsFrigate.TryGetValue(Id, out value))
                                {
                                    _isFrigate = value;
                                    return (bool)_isFrigate;
                                }
                            }

                            bool result = false;
                            result |= GroupId == (int)Group.Frigate;
                            result |= GroupId == (int)Group.AssaultShip;
                            result |= GroupId == (int)Group.StealthBomber;
                            result |= GroupId == (int)Group.ElectronicAttackShip;
                            result |= GroupId == (int)Group.PrototypeExplorationShip;

                            // Technically not frigs, but for our purposes they are
                            result |= GroupId == (int)Group.Destroyer;
                            result |= GroupId == (int)Group.Interdictor;
                            result |= GroupId == (int)Group.Interceptor;

                            _isFrigate = result;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsFrigate", "Adding [" + Name + "] to EntityIsFrigate as [" + _isFrigate + "]", Logging.Debug);
                            QMCache.Instance.EntityIsFrigate.Add(Id, (bool)_isFrigate);
                            return (bool)_isFrigate;
                        }

                        return (bool)_isFrigate;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isNPCFrigate;
        /// <summary>
        /// Frigate includes all elite-variants - this does NOT need to be limited to players, as we check for players specifically everywhere this is used
        /// </summary>
        public bool IsNPCFrigate
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isNPCFrigate == null)
                        {
                            if (QMCache.Instance.EntityIsNPCFrigate.Any() && QMCache.Instance.EntityIsNPCFrigate.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsNPCFrigate", "We have [" + QMCache.Instance.EntityIsNPCFrigate.Count() + "] Entities in QMCache.Instance.EntityIsNPCFrigate", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsNPCFrigate.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsNPCFrigate.TryGetValue(Id, out value))
                                {
                                    _isNPCFrigate = value;
                                    return (bool)_isNPCFrigate;
                                }
                            }

                            bool result = false;
                            if (IsPlayer)
                            {
                                //
                                // if it is a player it is by definition not an NPC
                                //
                                return false;
                            }
                            result |= GroupId == (int)Group.Frigate;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Guristas_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_Destroyer;
                            result |= GroupId == (int)Group.Deadspace_Angel_Cartel_Destroyer;
                            result |= GroupId == (int)Group.Deadspace_Blood_Raiders_Destroyer;
                            result |= GroupId == (int)Group.Deadspace_Guristas_Destroyer;
                            result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_Destroyer;
                            result |= GroupId == (int)Group.Deadspace_Serpentis_Destroyer;
                            result |= GroupId == (int)Group.Mission_Amarr_Empire_Destroyer;
                            result |= GroupId == (int)Group.Mission_Caldari_State_Destroyer;
                            result |= GroupId == (int)Group.Mission_Gallente_Federation_Destroyer;
                            result |= GroupId == (int)Group.Mission_Minmatar_Republic_Destroyer;
                            result |= GroupId == (int)Group.Mission_Khanid_Destroyer;
                            result |= GroupId == (int)Group.Mission_CONCORD_Destroyer;
                            result |= GroupId == (int)Group.Mission_Mordu_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Guristas_Commander_Destroyer;
                            result |= GroupId == (int)Group.Deadspace_Rogue_Drone_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Commander_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_Destroyer;
                            result |= GroupId == (int)Group.Mission_Thukker_Destroyer;
                            result |= GroupId == (int)Group.Mission_Generic_Destroyers;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_Destroyer;
                            result |= GroupId == (int)Group.asteroid_angel_cartel_frigate;
                            result |= GroupId == (int)Group.asteroid_blood_raiders_frigate;
                            result |= GroupId == (int)Group.asteroid_guristas_frigate;
                            result |= GroupId == (int)Group.asteroid_sanshas_nation_frigate;
                            result |= GroupId == (int)Group.asteroid_serpentis_frigate;
                            result |= GroupId == (int)Group.deadspace_angel_cartel_frigate;
                            result |= GroupId == (int)Group.deadspace_blood_raiders_frigate;
                            result |= GroupId == (int)Group.deadspace_guristas_frigate;
                            result |= GroupId == (int)Group.deadspace_sanshas_nation_frigate;
                            result |= GroupId == (int)Group.deadspace_serpentis_frigate;
                            result |= GroupId == (int)Group.mission_amarr_empire_frigate;
                            result |= GroupId == (int)Group.mission_caldari_state_frigate;
                            result |= GroupId == (int)Group.mission_gallente_federation_frigate;
                            result |= GroupId == (int)Group.mission_minmatar_republic_frigate;
                            result |= GroupId == (int)Group.mission_khanid_frigate;
                            result |= GroupId == (int)Group.mission_concord_frigate;
                            result |= GroupId == (int)Group.mission_mordu_frigate;
                            result |= GroupId == (int)Group.asteroid_rouge_drone_frigate;
                            result |= GroupId == (int)Group.deadspace_rogue_drone_frigate;
                            result |= GroupId == (int)Group.asteroid_angel_cartel_commander_frigate;
                            result |= GroupId == (int)Group.asteroid_blood_raiders_commander_frigate;
                            result |= GroupId == (int)Group.asteroid_guristas_commander_frigate;
                            result |= GroupId == (int)Group.asteroid_sanshas_nation_commander_frigate;
                            result |= GroupId == (int)Group.asteroid_serpentis_commander_frigate;
                            result |= GroupId == (int)Group.mission_generic_frigates;
                            result |= GroupId == (int)Group.mission_thukker_frigate;
                            result |= GroupId == (int)Group.asteroid_rouge_drone_commander_frigate;
                            result |= GroupId == (int)Group.TutorialDrone;
                            //result |= Name.Contains("Spider Drone"); //we *really* need to find out the GroupID of this one.
                            _isNPCFrigate = result;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsNPCFrigate", "Adding [" + Name + "] to EntityIsNPCFrigate as [" + _isNPCFrigate + "]", Logging.Debug);
                            QMCache.Instance.EntityIsNPCFrigate.Add(Id, (bool)_isNPCFrigate);
                            return (bool)_isNPCFrigate;
                        }

                        return (bool)_isNPCFrigate;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isCruiser;
        /// <summary>
        /// Cruiser includes all elite-variants
        /// </summary>
        public bool IsCruiser
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isCruiser == null)
                        {
                            if (QMCache.Instance.EntityIsCruiser.Any() && QMCache.Instance.EntityIsCruiser.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsCruiser", "We have [" + QMCache.Instance.EntityIsCruiser.Count() + "] Entities in QMCache.Instance.EntityIsCruiser", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsCruiser.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsCruiser.TryGetValue(Id, out value))
                                {
                                    _isCruiser = value;
                                    return (bool)_isCruiser;
                                }
                            }

                            bool result = false;
                            result |= GroupId == (int)Group.Cruiser;
                            result |= GroupId == (int)Group.HeavyAssaultShip;
                            result |= GroupId == (int)Group.Logistics;
                            result |= GroupId == (int)Group.ForceReconShip;
                            result |= GroupId == (int)Group.CombatReconShip;
                            result |= GroupId == (int)Group.HeavyInterdictor;

                            _isCruiser = result;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsCruiser", "Adding [" + Name + "] to EntityIsCruiser as [" + _isCruiser + "]", Logging.Debug);
                            QMCache.Instance.EntityIsCruiser.Add(Id, (bool)_isCruiser);
                            return (bool)_isCruiser;
                        }

                        return (bool)_isCruiser;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isNPCCruiser;
        /// <summary>
        /// Cruiser includes all elite-variants
        /// </summary>
        public bool IsNPCCruiser
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isNPCCruiser == null)
                        {
                            if (QMCache.Instance.EntityIsNPCCruiser.Any() && QMCache.Instance.EntityIsNPCCruiser.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsNPCCruiser", "We have [" + QMCache.Instance.EntityIsNPCCruiser.Count() + "] Entities in QMCache.Instance.EntityIsNPCCruiser", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsNPCCruiser.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsNPCCruiser.TryGetValue(Id, out value))
                                {
                                    _isNPCCruiser = value;
                                    return (bool)_isNPCCruiser;
                                }
                            }

                            bool result = false;
                            result |= GroupId == (int)Group.Storyline_Cruiser;
                            result |= GroupId == (int)Group.Storyline_Mission_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Guristas_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_Cruiser;
                            result |= GroupId == (int)Group.Deadspace_Angel_Cartel_Cruiser;
                            result |= GroupId == (int)Group.Deadspace_Blood_Raiders_Cruiser;
                            result |= GroupId == (int)Group.Deadspace_Guristas_Cruiser;
                            result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_Cruiser;
                            result |= GroupId == (int)Group.Deadspace_Serpentis_Cruiser;
                            result |= GroupId == (int)Group.Mission_Amarr_Empire_Cruiser;
                            result |= GroupId == (int)Group.Mission_Caldari_State_Cruiser;
                            result |= GroupId == (int)Group.Mission_Gallente_Federation_Cruiser;
                            result |= GroupId == (int)Group.Mission_Khanid_Cruiser;
                            result |= GroupId == (int)Group.Mission_CONCORD_Cruiser;
                            result |= GroupId == (int)Group.Mission_Mordu_Cruiser;
                            result |= GroupId == (int)Group.Mission_Minmatar_Republic_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Guristas_Commander_Cruiser;
                            result |= GroupId == (int)Group.Deadspace_Rogue_Drone_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Commander_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_Cruiser;
                            result |= GroupId == (int)Group.Mission_Generic_Cruisers;
                            result |= GroupId == (int)Group.Deadspace_Overseer_Cruiser;
                            result |= GroupId == (int)Group.Mission_Thukker_Cruiser;
                            result |= GroupId == (int)Group.Mission_Generic_Battle_Cruisers;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_Cruiser;
                            result |= GroupId == (int)Group.Mission_Faction_Cruiser;
                            result |= GroupId == (int)Group.Mission_Faction_Industrials;
                            _isNPCCruiser = result;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsNPCCruiser", "Adding [" + Name + "] to EntityIsNPCCruiser as [" + _isNPCCruiser + "]", Logging.Debug);
                            QMCache.Instance.EntityIsNPCCruiser.Add(Id, (bool)_isNPCCruiser);
                            return (bool)_isNPCCruiser;
                        }

                        return (bool)_isNPCCruiser;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isBattleCruiser;
        /// <summary>
        /// BattleCruiser includes all elite-variants
        /// </summary>
        public bool IsBattlecruiser
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isBattleCruiser == null)
                        {
                            if (QMCache.Instance.EntityIsBattleCruiser.Any() && QMCache.Instance.EntityIsBattleCruiser.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsBattleCruiser", "We have [" + QMCache.Instance.EntityIsBattleCruiser.Count() + "] Entities in QMCache.Instance.EntityIsBattleCruiser", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsBattleCruiser.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsBattleCruiser.TryGetValue(Id, out value))
                                {
                                    _isBattleCruiser = value;
                                    return (bool)_isBattleCruiser;
                                }
                            }

                            bool result = false;
                            result |= GroupId == (int)Group.AttackBattlecruiser;
                            result |= GroupId == (int)Group.CombatBattlecruiser;
                            result |= GroupId == (int)Group.CommandShip;
                            result |= GroupId == (int)Group.StrategicCruiser; // Technically a cruiser, but hits hard enough to be a BC :)
                            _isBattleCruiser = result;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsBattleCruiser", "Adding [" + Name + "] to EntityIsBattleCruiser as [" + _isBattleCruiser + "]", Logging.Debug);
                            QMCache.Instance.EntityIsBattleCruiser.Add(Id, (bool)_isBattleCruiser);
                            return (bool)_isBattleCruiser;
                        }

                        return (bool)_isBattleCruiser;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isNPCBattleCruiser;
        /// <summary>
        /// BattleCruiser includes all elite-variants
        /// </summary>
        public bool IsNPCBattlecruiser
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isNPCBattleCruiser == null)
                        {
                            if (QMCache.Instance.EntityIsNPCBattleCruiser.Any() && QMCache.Instance.EntityIsNPCBattleCruiser.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsNPCBattleCruiser", "We have [" + QMCache.Instance.EntityIsNPCBattleCruiser.Count() + "] Entities in QMCache.Instance.EntityIsNPCBattleCruiser", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsNPCBattleCruiser.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsNPCBattleCruiser.TryGetValue(Id, out value))
                                {
                                    _isNPCBattleCruiser = value;
                                    return (bool)_isNPCBattleCruiser;
                                }
                            }

                            bool result = false;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Guristas_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_BattleCruiser;
                            result |= GroupId == (int)Group.Deadspace_Angel_Cartel_BattleCruiser;
                            result |= GroupId == (int)Group.Deadspace_Blood_Raiders_BattleCruiser;
                            result |= GroupId == (int)Group.Deadspace_Guristas_BattleCruiser;
                            result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_BattleCruiser;
                            result |= GroupId == (int)Group.Deadspace_Serpentis_BattleCruiser;
                            result |= GroupId == (int)Group.Mission_Amarr_Empire_Battlecruiser;
                            result |= GroupId == (int)Group.Mission_Caldari_State_Battlecruiser;
                            result |= GroupId == (int)Group.Mission_Gallente_Federation_Battlecruiser;
                            result |= GroupId == (int)Group.Mission_Minmatar_Republic_Battlecruiser;
                            result |= GroupId == (int)Group.Mission_Khanid_Battlecruiser;
                            result |= GroupId == (int)Group.Mission_CONCORD_Battlecruiser;
                            result |= GroupId == (int)Group.Mission_Mordu_Battlecruiser;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Guristas_Commander_BattleCruiser;
                            result |= GroupId == (int)Group.Deadspace_Rogue_Drone_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Commander_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_BattleCruiser;
                            result |= GroupId == (int)Group.Mission_Thukker_Battlecruiser;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_BattleCruiser;
                            _isNPCBattleCruiser = result;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsNPCBattleCruiser", "Adding [" + Name + "] to EntityIsNPCBattleCruiser as [" + _isNPCBattleCruiser + "]", Logging.Debug);
                            QMCache.Instance.EntityIsNPCBattleCruiser.Add(Id, (bool)_isNPCBattleCruiser);
                            return (bool)_isNPCBattleCruiser;
                        }

                        return (bool)_isNPCBattleCruiser;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }


        private bool? _isBattleship;
        /// <summary>
        /// Battleship includes all elite-variants
        /// </summary>
        public bool IsBattleship
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isBattleship == null)
                        {
                            if (QMCache.Instance.EntityIsBattleShip.Any() && QMCache.Instance.EntityIsBattleShip.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsBattleShip", "We have [" + QMCache.Instance.EntityIsBattleShip.Count() + "] Entities in QMCache.Instance.EntityIsBattleShip", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsBattleShip.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsBattleShip.TryGetValue(Id, out value))
                                {
                                    _isBattleship = value;
                                    return (bool)_isBattleship;
                                }
                            }

                            bool result = false;
                            result |= GroupId == (int)Group.Battleship;
                            result |= GroupId == (int)Group.EliteBattleship;
                            result |= GroupId == (int)Group.BlackOps;
                            result |= GroupId == (int)Group.Marauder;
                            _isBattleship = result;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsBattleShip", "Adding [" + Name + "] to EntityIsBattleShip as [" + _isBattleship + "]", Logging.Debug);
                            QMCache.Instance.EntityIsBattleShip.Add(Id, (bool)_isBattleship);
                            return (bool)_isBattleship;
                        }

                        return (bool)_isBattleship;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isNPCBattleship;
        /// <summary>
        /// Battleship includes all elite-variants
        /// </summary>
        public bool IsNPCBattleship
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isNPCBattleship == null)
                        {
                            if (QMCache.Instance.EntityIsNPCBattleShip.Any() && QMCache.Instance.EntityIsNPCBattleShip.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsNPCBattleShip", "We have [" + QMCache.Instance.EntityIsNPCBattleShip.Count() + "] Entities in QMCache.Instance.EntityIsNPCBattleShip", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsNPCBattleShip.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsNPCBattleShip.TryGetValue(Id, out value))
                                {
                                    _isNPCBattleship = value;
                                    return (bool)_isNPCBattleship;
                                }
                            }

                            bool result = false;
                            result |= GroupId == (int)Group.Storyline_Battleship;
                            result |= GroupId == (int)Group.Storyline_Mission_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Guristas_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_Battleship;
                            result |= GroupId == (int)Group.Deadspace_Angel_Cartel_Battleship;
                            result |= GroupId == (int)Group.Deadspace_Blood_Raiders_Battleship;
                            result |= GroupId == (int)Group.Deadspace_Guristas_Battleship;
                            result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_Battleship;
                            result |= GroupId == (int)Group.Deadspace_Serpentis_Battleship;
                            result |= GroupId == (int)Group.Mission_Amarr_Empire_Battleship;
                            result |= GroupId == (int)Group.Mission_Caldari_State_Battleship;
                            result |= GroupId == (int)Group.Mission_Gallente_Federation_Battleship;
                            result |= GroupId == (int)Group.Mission_Khanid_Battleship;
                            result |= GroupId == (int)Group.Mission_CONCORD_Battleship;
                            result |= GroupId == (int)Group.Mission_Mordu_Battleship;
                            result |= GroupId == (int)Group.Mission_Minmatar_Republic_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Battleship;
                            result |= GroupId == (int)Group.Deadspace_Rogue_Drone_Battleship;
                            result |= GroupId == (int)Group.Mission_Generic_Battleships;
                            result |= GroupId == (int)Group.Deadspace_Overseer_Battleship;
                            result |= GroupId == (int)Group.Mission_Thukker_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Guristas_Commander_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Commander_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_Battleship;
                            result |= GroupId == (int)Group.Mission_Faction_Battleship;
                            _isNPCBattleship = result;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsNPCBattleShip", "Adding [" + Name + "] to EntityIsNPCBattleShip as [" + _isNPCBattleship + "]", Logging.Debug);
                            QMCache.Instance.EntityIsNPCBattleShip.Add(Id, (bool)_isNPCBattleship);
                            return (bool)_isNPCBattleship;
                        }

                        return (bool)_isNPCBattleship;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isLargeCollidable;

        public bool IsLargeCollidable
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isLargeCollidable == null)
                        {
                            if (QMCache.Instance.EntityIsLargeCollidable.Any() && QMCache.Instance.EntityIsLargeCollidable.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsLargeCollidable", "We have [" + QMCache.Instance.EntityIsLargeCollidable.Count() + "] Entities in QMCache.Instance.EntityIsLargeCollidable", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsLargeCollidable.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsLargeCollidable.TryGetValue(Id, out value))
                                {
                                    _isLargeCollidable = value;
                                    return (bool)_isLargeCollidable;
                                }
                            }

                            bool result = false;
                            result |= GroupId == (int)Group.LargeColidableObject;
                            result |= GroupId == (int)Group.LargeColidableShip;
                            result |= GroupId == (int)Group.LargeColidableStructure;
                            result |= GroupId == (int)Group.DeadSpaceOverseersStructure;
                            result |= GroupId == (int)Group.DeadSpaceOverseersBelongings;
                            _isLargeCollidable = result;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsLargeCollidableObject", "Adding [" + Name + "] to EntityIsLargeCollidableObject as [" + _isLargeCollidable + "]", Logging.Debug);
                            QMCache.Instance.EntityIsLargeCollidable.Add(Id, (bool)_isLargeCollidable);
                            return (bool)_isLargeCollidable;
                        }

                        return (bool)_isLargeCollidable;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isMiscJunk;
        public bool IsMiscJunk
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isMiscJunk == null)
                        {
                            if (QMCache.Instance.EntityIsMiscJunk.Any() && QMCache.Instance.EntityIsMiscJunk.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsMiscJunk", "We have [" + QMCache.Instance.EntityIsMiscJunk.Count() + "] Entities in QMCache.Instance.EntityIsMiscJunk", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsMiscJunk.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsMiscJunk.TryGetValue(Id, out value))
                                {
                                    _isMiscJunk = value;
                                    return (bool)_isMiscJunk;
                                }
                            }

                            bool result = false;
                            result |= GroupId == (int)Group.PlayerDrone;
                            result |= GroupId == (int)Group.Wreck;
                            result |= GroupId == (int)Group.AccelerationGate;
                            result |= GroupId == (int)Group.GasCloud;
                            _isMiscJunk = result;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsMiscJunk", "Adding [" + Name + "] to EntityIsMiscJunk as [" + _isMiscJunk + "]", Logging.Debug);
                            QMCache.Instance.EntityIsMiscJunk.Add(Id, (bool)_isMiscJunk);
                            return (bool)_isMiscJunk;
                        }

                        return (bool)_isMiscJunk;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _IsBadIdea;

        public bool IsBadIdea
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_IsBadIdea == null)
                        {
                            if (QMCache.Instance.EntityIsBadIdea.Any() && QMCache.Instance.EntityIsBadIdea.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsBadIdea", "We have [" + QMCache.Instance.EntityIsBadIdea.Count() + "] Entities in QMCache.Instance.EntityIsBadIdea", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsBadIdea.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsBadIdea.TryGetValue(Id, out value))
                                {
                                    _IsBadIdea = value;
                                    return (bool)_IsBadIdea;
                                }
                            }

                            bool result = false;
                            result |= GroupId == (int)Group.ConcordDrone;
                            result |= GroupId == (int)Group.PoliceDrone;
                            result |= GroupId == (int)Group.CustomsOfficial;
                            result |= GroupId == (int)Group.Billboard;
                            result |= GroupId == (int)Group.Stargate;
                            result |= GroupId == (int)Group.Station;
                            result |= GroupId == (int)Group.SentryGun;
                            result |= GroupId == (int)Group.Capsule;
                            result |= GroupId == (int)Group.MissionContainer;
                            result |= GroupId == (int)Group.CustomsOffice;
                            result |= GroupId == (int)Group.GasCloud;
                            result |= GroupId == (int)Group.ConcordBillboard;
                            result |= IsFrigate;
                            result |= IsCruiser;
                            result |= IsBattlecruiser;
                            result |= IsBattleship;
                            result |= IsPlayer;
                            _IsBadIdea = result;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsBadIdea", "Adding [" + Name + "] to EntityIsBadIdea as [" + _IsBadIdea + "]", Logging.Debug);
                            QMCache.Instance.EntityIsBadIdea.Add(Id, (bool)_IsBadIdea);
                            return (bool)_IsBadIdea;
                        }

                        return (bool)_IsBadIdea;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsFactionWarfareNPC
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= GroupId == (int)Group.FactionWarfareNPC;
                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isNpcByGroupID;

        public bool IsNpcByGroupID
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isNpcByGroupID == null)
                        {
                            if (QMCache.Instance.EntityIsNPCByGroupID.Any() && QMCache.Instance.EntityIsNPCByGroupID.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsNPCByGroupID", "We have [" + QMCache.Instance.EntityIsNPCByGroupID.Count() + "] Entities in QMCache.Instance.EntityIsNPCByGroupID", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsNPCByGroupID.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsNPCByGroupID.TryGetValue(Id, out value))
                                {
                                    _isNpcByGroupID = value;
                                    return (bool)_isNpcByGroupID;
                                }
                            }

                            bool result = false;
                            result |= IsLargeCollidable;
                            result |= IsSentry;
                            result |= GroupId == (int)Group.DeadSpaceOverseersStructure;
                            //result |= GroupId == (int)Group.DeadSpaceOverseersBelongings;
                            result |= GroupId == (int)Group.Storyline_Battleship;
                            result |= GroupId == (int)Group.Storyline_Mission_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Guristas_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_Battleship;
                            result |= GroupId == (int)Group.Deadspace_Angel_Cartel_Battleship;
                            result |= GroupId == (int)Group.Deadspace_Blood_Raiders_Battleship;
                            result |= GroupId == (int)Group.Deadspace_Guristas_Battleship;
                            result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_Battleship;
                            result |= GroupId == (int)Group.Deadspace_Serpentis_Battleship;
                            result |= GroupId == (int)Group.Mission_Amarr_Empire_Battleship;
                            result |= GroupId == (int)Group.Mission_Caldari_State_Battleship;
                            result |= GroupId == (int)Group.Mission_Gallente_Federation_Battleship;
                            result |= GroupId == (int)Group.Mission_Khanid_Battleship;
                            result |= GroupId == (int)Group.Mission_CONCORD_Battleship;
                            result |= GroupId == (int)Group.Mission_Mordu_Battleship;
                            result |= GroupId == (int)Group.Mission_Minmatar_Republic_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Battleship;
                            result |= GroupId == (int)Group.Deadspace_Rogue_Drone_Battleship;
                            result |= GroupId == (int)Group.Mission_Generic_Battleships;
                            result |= GroupId == (int)Group.Deadspace_Overseer_Battleship;
                            result |= GroupId == (int)Group.Mission_Thukker_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Guristas_Commander_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_Battleship;
                            result |= GroupId == (int)Group.Mission_Faction_Battleship;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Guristas_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_BattleCruiser;
                            result |= GroupId == (int)Group.Deadspace_Angel_Cartel_BattleCruiser;
                            result |= GroupId == (int)Group.Deadspace_Blood_Raiders_BattleCruiser;
                            result |= GroupId == (int)Group.Deadspace_Guristas_BattleCruiser;
                            result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_BattleCruiser;
                            result |= GroupId == (int)Group.Deadspace_Serpentis_BattleCruiser;
                            result |= GroupId == (int)Group.Mission_Amarr_Empire_Battlecruiser;
                            result |= GroupId == (int)Group.Mission_Caldari_State_Battlecruiser;
                            result |= GroupId == (int)Group.Mission_Gallente_Federation_Battlecruiser;
                            result |= GroupId == (int)Group.Mission_Minmatar_Republic_Battlecruiser;
                            result |= GroupId == (int)Group.Mission_Khanid_Battlecruiser;
                            result |= GroupId == (int)Group.Mission_CONCORD_Battlecruiser;
                            result |= GroupId == (int)Group.Mission_Mordu_Battlecruiser;
                            result |= GroupId == (int)Group.Mission_Faction_Industrials;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Guristas_Commander_BattleCruiser;
                            result |= GroupId == (int)Group.Deadspace_Rogue_Drone_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Commander_BattleCruiser;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_BattleCruiser;
                            result |= GroupId == (int)Group.Mission_Thukker_Battlecruiser;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_BattleCruiser;
                            result |= GroupId == (int)Group.Storyline_Cruiser;
                            result |= GroupId == (int)Group.Storyline_Mission_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Guristas_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_Cruiser;
                            result |= GroupId == (int)Group.Deadspace_Angel_Cartel_Cruiser;
                            result |= GroupId == (int)Group.Deadspace_Blood_Raiders_Cruiser;
                            result |= GroupId == (int)Group.Deadspace_Guristas_Cruiser;
                            result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_Cruiser;
                            result |= GroupId == (int)Group.Deadspace_Serpentis_Cruiser;
                            result |= GroupId == (int)Group.Mission_Amarr_Empire_Cruiser;
                            result |= GroupId == (int)Group.Mission_Caldari_State_Cruiser;
                            result |= GroupId == (int)Group.Mission_Gallente_Federation_Cruiser;
                            result |= GroupId == (int)Group.Mission_Khanid_Cruiser;
                            result |= GroupId == (int)Group.Mission_CONCORD_Cruiser;
                            result |= GroupId == (int)Group.Mission_Mordu_Cruiser;
                            result |= GroupId == (int)Group.Mission_Minmatar_Republic_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Guristas_Commander_Cruiser;
                            result |= GroupId == (int)Group.Deadspace_Rogue_Drone_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Commander_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_Cruiser;
                            result |= GroupId == (int)Group.Mission_Generic_Cruisers;
                            result |= GroupId == (int)Group.Deadspace_Overseer_Cruiser;
                            result |= GroupId == (int)Group.Mission_Thukker_Cruiser;
                            result |= GroupId == (int)Group.Mission_Generic_Battle_Cruisers;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_Cruiser;
                            result |= GroupId == (int)Group.Mission_Faction_Cruiser;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Guristas_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_Destroyer;
                            result |= GroupId == (int)Group.Deadspace_Angel_Cartel_Destroyer;
                            result |= GroupId == (int)Group.Deadspace_Blood_Raiders_Destroyer;
                            result |= GroupId == (int)Group.Deadspace_Guristas_Destroyer;
                            result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_Destroyer;
                            result |= GroupId == (int)Group.Deadspace_Serpentis_Destroyer;
                            result |= GroupId == (int)Group.Mission_Amarr_Empire_Destroyer;
                            result |= GroupId == (int)Group.Mission_Caldari_State_Destroyer;
                            result |= GroupId == (int)Group.Mission_Gallente_Federation_Destroyer;
                            result |= GroupId == (int)Group.Mission_Minmatar_Republic_Destroyer;
                            result |= GroupId == (int)Group.Mission_Khanid_Destroyer;
                            result |= GroupId == (int)Group.Mission_CONCORD_Destroyer;
                            result |= GroupId == (int)Group.Mission_Mordu_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Guristas_Commander_Destroyer;
                            result |= GroupId == (int)Group.Deadspace_Rogue_Drone_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Commander_Destroyer;
                            result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_Destroyer;
                            result |= GroupId == (int)Group.Mission_Thukker_Destroyer;
                            result |= GroupId == (int)Group.Mission_Generic_Destroyers;
                            result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_Destroyer;
                            result |= GroupId == (int)Group.TutorialDrone;
                            result |= GroupId == (int)Group.asteroid_angel_cartel_frigate;
                            result |= GroupId == (int)Group.asteroid_blood_raiders_frigate;
                            result |= GroupId == (int)Group.asteroid_guristas_frigate;
                            result |= GroupId == (int)Group.asteroid_sanshas_nation_frigate;
                            result |= GroupId == (int)Group.asteroid_serpentis_frigate;
                            result |= GroupId == (int)Group.deadspace_angel_cartel_frigate;
                            result |= GroupId == (int)Group.deadspace_blood_raiders_frigate;
                            result |= GroupId == (int)Group.deadspace_guristas_frigate;
                            result |= GroupId == (int)Group.Deadspace_Overseer_Frigate;
                            result |= GroupId == (int)Group.Deadspace_Rogue_Drone_Swarm;
                            result |= GroupId == (int)Group.deadspace_sanshas_nation_frigate;
                            result |= GroupId == (int)Group.deadspace_serpentis_frigate;
                            result |= GroupId == (int)Group.mission_amarr_empire_frigate;
                            result |= GroupId == (int)Group.mission_caldari_state_frigate;
                            result |= GroupId == (int)Group.mission_gallente_federation_frigate;
                            result |= GroupId == (int)Group.mission_minmatar_republic_frigate;
                            result |= GroupId == (int)Group.mission_khanid_frigate;
                            result |= GroupId == (int)Group.mission_concord_frigate;
                            result |= GroupId == (int)Group.mission_mordu_frigate;
                            result |= GroupId == (int)Group.asteroid_rouge_drone_frigate;
                            result |= GroupId == (int)Group.deadspace_rogue_drone_frigate;
                            result |= GroupId == (int)Group.asteroid_angel_cartel_commander_frigate;
                            result |= GroupId == (int)Group.asteroid_blood_raiders_commander_frigate;
                            result |= GroupId == (int)Group.asteroid_guristas_commander_frigate;
                            result |= GroupId == (int)Group.asteroid_sanshas_nation_commander_frigate;
                            result |= GroupId == (int)Group.asteroid_serpentis_commander_frigate;
                            result |= GroupId == (int)Group.mission_generic_frigates;
                            result |= GroupId == (int)Group.mission_thukker_frigate;
                            result |= GroupId == (int)Group.asteroid_rouge_drone_commander_frigate;
                            _isNpcByGroupID = result;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsNPCByGroupID", "Adding [" + Name + "] to EntityIsNPCByGroupID as [" + _isNpcByGroupID + "]", Logging.Debug);
                            QMCache.Instance.EntityIsNPCByGroupID.Add(Id, (bool)_isNpcByGroupID);
                            return (bool)_isNpcByGroupID;
                        }

                        return (bool)_isNpcByGroupID;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isEntityIShouldLeaveAlone;
        public bool IsEntityIShouldLeaveAlone
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isEntityIShouldLeaveAlone == null)
                        {
                            if (QMCache.Instance.EntityIsEntutyIShouldLeaveAlone.Any() && QMCache.Instance.EntityIsEntutyIShouldLeaveAlone.Count() > DictionaryCountThreshhold)
                            {
                                if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsEntutyIShouldLeaveAlone", "We have [" + QMCache.Instance.EntityIsEntutyIShouldLeaveAlone.Count() + "] Entities in QMCache.Instance.EntityIsEntutyIShouldLeaveAlone", Logging.Debug);
                            }

                            if (QMCache.Instance.EntityIsEntutyIShouldLeaveAlone.Any())
                            {
                                bool value;
                                if (QMCache.Instance.EntityIsEntutyIShouldLeaveAlone.TryGetValue(Id, out value))
                                {
                                    _isEntityIShouldLeaveAlone = value;
                                    return (bool)_isEntityIShouldLeaveAlone;
                                }
                            }

                            bool result = false;
                            result |= GroupId == (int)Group.Merchant;            // Merchant, Convoy?
                            result |= GroupId == (int)Group.Mission_Merchant;    // Merchant, Convoy? - Dread Pirate Scarlet
                            result |= GroupId == (int)Group.FactionWarfareNPC;
                            result |= IsOreOrIce;
                            _isEntityIShouldLeaveAlone = result;
                            if (Logging.DebugEntityCache) Logging.Log("Entitycache.IsEntutyIShouldLeaveAlone", "Adding [" + Name + "] to EntityIsEntutyIShouldLeaveAlone as [" + _isEntityIShouldLeaveAlone + "]", Logging.Debug);
                            QMCache.Instance.EntityIsEntutyIShouldLeaveAlone.Add(Id, (bool)_isEntityIShouldLeaveAlone);
                            return (bool)_isEntityIShouldLeaveAlone;
                        }

                        return (bool)_isEntityIShouldLeaveAlone;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isOnGridWithMe;

        public bool IsOnGridWithMe
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isOnGridWithMe == null)
                        {
                            if (Distance < (double)Distances.OnGridWithMe)
                            {
                                _isOnGridWithMe = true;
                                return (bool)_isOnGridWithMe;
                            }

                            _isOnGridWithMe = false;
                            return (bool)_isOnGridWithMe;
                        }

                        return (bool)_isOnGridWithMe;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsStation
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= GroupId == (int)Group.Station;
                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsCustomsOffice
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= GroupId == (int)Group.CustomsOffice;
                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsCelestial
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= CategoryId == (int)CategoryID.Celestial;
                        result |= CategoryId == (int)CategoryID.Station;
                        result |= GroupId == (int)Group.Moon;
                        result |= GroupId == (int)Group.AsteroidBelt;
                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsAsteroidBelt
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= GroupId == (int)Group.AsteroidBelt;
                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsPlanet
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= GroupId == (int)Group.Planet;
                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsMoon
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= GroupId == (int)Group.Moon;
                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsAsteroid
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= CategoryId == (int)CategoryID.Asteroid;
                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsShipWithOreHold
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= TypeId == (int)TypeID.Venture;
                        result |= GroupId == (int)Group.MiningBarge;
                        result |= GroupId == (int)Group.Exhumer;
                        result |= GroupId == (int)Group.IndustrialCommandShip; // Orca
                        result |= GroupId == (int)Group.CapitalIndustrialShip; // Rorqual
                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }
        public bool IsShipWithNoDroneBay
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= TypeId == (int)TypeID.Tengu;
                        result |= GroupId == (int)Group.Shuttle;
                        if (QMCache.Instance.InSpace && QMCache.Instance.InMission)
                        {
                            if (Drones.DroneBay != null && Drones.DroneBay.IsReady)
                            {
                                //
                                // can or should we just check for drone bandwidth?
                                //
                                if (Drones.DroneBay.Volume == 0)
                                {
                                    if (Logging.DebugDrones) Logging.Log("IsShipWithNoDroneBay", "Dronebay Volume = 0", Logging.Debug);
                                    //result = true; // no drone bay available
                                }
                            }
                        }

                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool IsShipWithNoCargoBay
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= GroupId == (int)Group.Capsule;
                        //result |= GroupId == (int)Group.Shuttle;
                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool SalvagersAvailable
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= QMCache.Instance.Modules.Any(m => m.GroupId == (int)Group.Salvager && m.IsOnline);
                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }

        }

        public bool IsOreOrIce
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= GroupId == (int)Group.Plagioclase;
                        result |= GroupId == (int)Group.Spodumain;
                        result |= GroupId == (int)Group.Kernite;
                        result |= GroupId == (int)Group.Hedbergite;
                        result |= GroupId == (int)Group.Arkonor;
                        result |= GroupId == (int)Group.Bistot;
                        result |= GroupId == (int)Group.Pyroxeres;
                        result |= GroupId == (int)Group.Crokite;
                        result |= GroupId == (int)Group.Jaspet;
                        result |= GroupId == (int)Group.Omber;
                        result |= GroupId == (int)Group.Scordite;
                        result |= GroupId == (int)Group.Gneiss;
                        result |= GroupId == (int)Group.Veldspar;
                        result |= GroupId == (int)Group.Hemorphite;
                        result |= GroupId == (int)Group.DarkOchre;
                        result |= GroupId == (int)Group.Ice;
                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        public bool LockTarget(string module)
        {
            try
            {
                if (DateTime.UtcNow < Time.Instance.LastInWarp.AddSeconds(5))
                {
                    return false;
                }

                if (DateTime.UtcNow < Time.Instance.NextTargetAction)
                {
                    return false;
                }

                if (_directEntity != null && _directEntity.IsValid)
                {
                    if (!IsTarget)
                    {
                        if (!HasExploded)
                        {
                            if (Distance < Combat.MaxTargetRange)
                            {
                                if (QMCache.Instance.Targets.Count() < QMCache.Instance.MaxLockedTargets)
                                {
                                    if (!IsTargeting)
                                    {
                                        if (QMCache.Instance.EntitiesOnGrid.Any(i => i.Id == Id))
                                        {
                                            // If the bad idea is attacking, attack back
                                            if (IsBadIdea && !IsAttacking)
                                            {
                                                Logging.Log("EntityCache.LockTarget", "[" + module + "] Attempted to target a player or concord entity! [" + Name + "] - aborting", Logging.White);
                                                return false;
                                            }

                                            if (Distance >= 250001 || Distance > Combat.MaxTargetRange) //250k is the MAX targeting range in eve.
                                            {
                                                Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "] which is [" + Math.Round(Distance / 1000, 2) + "k] away. Do not try to lock things that you cant possibly target", Logging.Debug);
                                                return false;
                                            }

                                            // Remove the target info (its been targeted)
                                            foreach (EntityCache target in QMCache.Instance.EntitiesOnGrid.Where(e => e.IsTarget && QMCache.Instance.TargetingIDs.ContainsKey(e.Id)))
                                            {
                                                QMCache.Instance.TargetingIDs.Remove(target.Id);
                                            }

                                            if (QMCache.Instance.TargetingIDs.ContainsKey(Id))
                                            {
                                                DateTime lastTargeted = QMCache.Instance.TargetingIDs[Id];

                                                // Ignore targeting request
                                                double seconds = DateTime.UtcNow.Subtract(lastTargeted).TotalSeconds;
                                                if (seconds < 20)
                                                {
                                                    Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + MaskedId + "][" + QMCache.Instance.Targets.Count() + "] targets already, can reTarget in [" + Math.Round(20 - seconds, 0) + "]", Logging.White);
                                                    return false;
                                                }
                                            }
                                            // Only add targeting id's when its actually being targeted

                                            if (_directEntity.LockTarget())
                                            {
                                                //QMCache.Instance.NextTargetAction = DateTime.UtcNow.AddMilliseconds(Time.Instance.TargetDelay_milliseconds);
                                                QMCache.Instance.TargetingIDs[Id] = DateTime.UtcNow;
                                                return true;
                                            }

                                            Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + MaskedId + "][" + QMCache.Instance.Targets.Count() + "] targets already, LockTarget failed (unknown reason)", Logging.White);
                                            return false;
                                        }

                                        Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + MaskedId + "][" + QMCache.Instance.Targets.Count() + "] targets already, LockTarget failed: target was not in Entities List", Logging.White);
                                        return false;
                                    }

                                    Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + MaskedId + "][" + QMCache.Instance.Targets.Count() + "] targets already, LockTarget aborted: target is already being targeted", Logging.White);
                                    return false;
                                }

                                Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + MaskedId + "][" + QMCache.Instance.Targets.Count() + "] targets already, we only have [" + QMCache.Instance.MaxLockedTargets + "] slots!", Logging.White);
                                return false;
                            }

                            Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + MaskedId + "][" + QMCache.Instance.Targets.Count() + "] targets already, my targeting range is only [" + Combat.MaxTargetRange + "]!", Logging.White);
                            return false;
                        }

                        Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "][" + QMCache.Instance.Targets.Count() + "] targets already, target is already dead!", Logging.White);
                        return false;
                    }

                    Logging.Log("EntityCache.LockTarget", "[" + module + "] LockTarget request has been ignored for [" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + MaskedId + "][" + QMCache.Instance.Targets.Count() + "] targets already, target is already locked!", Logging.White);
                    return false;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                return false;
            }
        }

        public bool UnlockTarget(string module)
        {
            try
            {
                if (_directEntity != null && _directEntity.IsValid)
                {
                    //if (Distance > 250001)
                    //{
                    //    return false;
                    //}

                    QMCache.Instance.TargetingIDs.Remove(Id);

                    if (IsTarget)
                    {
                        _directEntity.UnlockTarget();
                        return true;
                    }

                    return false;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                return false;
            }
        }

        public bool Jump()
        {
            try
            {
                if (DateTime.UtcNow < Time.Instance.LastInWarp.AddSeconds(5))
                {
                    return false;
                }

                if (DateTime.UtcNow > Time.Instance.NextJumpAction)
                {
                    if (Time.Instance.LastInSpace.AddSeconds(2) > DateTime.UtcNow && QMCache.Instance.InSpace)
                    {
                        if (_directEntity != null && _directEntity.IsValid)
                        {
                            if (DateTime.UtcNow.AddSeconds(-5) > ThisEntityCacheCreated)
                            {
                                Logging.Log("EntityCache.Name", "The EntityCache instance that represents [" + _directEntity.Name + "][" + Math.Round(_directEntity.Distance / 1000, 0) + "k][" + MaskedId + "] was created more than 5 seconds ago (ugh!)", Logging.Debug);
                            }

                            if (Distance < 2500)
                            {
                                _directEntity.Jump();
                                QMCache.Instance.ClearPerPocketCache("Jump()");
                                Time.Instance.LastSessionChange = DateTime.UtcNow;
                                Time.Instance.NextInSpaceorInStation = DateTime.UtcNow;
                                Time.Instance.WehaveMoved = DateTime.UtcNow.AddDays(-7);
                                Time.Instance.NextJumpAction = DateTime.UtcNow.AddSeconds(QMCache.Instance.RandomNumber(8, 12));
                                Time.Instance.NextTravelerAction = DateTime.UtcNow.AddSeconds(Time.Instance.TravelerJumpedGateNextCommandDelay_seconds);
                                Time.Instance.NextActivateModules = DateTime.UtcNow.AddSeconds(Time.Instance.TravelerJumpedGateNextCommandDelay_seconds);
                                return true;
                            }

                            Logging.Log("EntityCache.Jump", "we tried to jump through [" + Name + "] but it is [" + Math.Round(Distance / 1000, 2) + "k away][" + MaskedId + "]", Logging.White);
                            return false;
                        }

                        Logging.Log("EntityCache.Jump", "[" + Name + "] DirecEntity is null or is not valid", Logging.Debug);
                        return false;
                    }

                    Logging.Log("EntityCache.Jump", "We have not yet been in space for 2 seconds, waiting", Logging.White);
                    return false;
                }

                Logging.Log("EntityCache.Jump", "We still have [" + DateTime.UtcNow.Subtract(Time.Instance.NextJumpAction) + "] seconds until we should jump again.", Logging.White);
                return false;

            }
            catch (Exception exception)
            {
                Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                return false;
            }
        }

        public bool IsEwarImmune
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        bool result = false;
                        result |= TypeId == (int)TypeID.Zor;
                        return result;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }

        private bool? _isCloaked = null;
        public bool IsCloaked
        {
            get
            {
                try
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (_isCloaked == null)
                        {
                            _isCloaked = _directEntity.IsCloaked;
                            return _isCloaked ?? false;
                        }

                        return _isCloaked ?? false;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                    return false;
                }
            }
        }


        public bool Activate()
        {
            try
            {
                if (DateTime.UtcNow < Time.Instance.LastInWarp.AddSeconds(5))
                {
                    return false;
                }

                if (DateTime.UtcNow > Time.Instance.NextActivateAction)
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (DateTime.UtcNow.AddSeconds(-5) > ThisEntityCacheCreated)
                        {
                            Logging.Log("EntityCache.Name", "The EntityCache instance that represents [" + _directEntity.Name + "][" + Math.Round(_directEntity.Distance / 1000, 0) + "k][" + MaskedId + "] was created more than 5 seconds ago (ugh!)", Logging.Debug);
                        }

                        //we cant move in bastion mode, do not try
                        List<ModuleCache> bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
                        if (bastionModules.Any(i => i.IsActive))
                        {
                            Logging.Log("EntityCache.Activate", "BastionMode is active, we cannot move, aborting attempt to Activate Gate", Logging.Debug);
                            return false;
                        }

                        _directEntity.Activate();
                        QMCache.Instance.ClearPerPocketCache("Activate");
                        Time.Instance.LastInWarp = DateTime.UtcNow;
                        Time.Instance.NextActivateAction = DateTime.UtcNow.AddSeconds(15);
                        return true;
                    }

                    Logging.Log("EntityCache.Activate", "[" + Name + "] DirecEntity is null or is not valid", Logging.Debug);
                    return false;
                }

                Logging.Log("EntityCache.Activate", "You have another [" + Time.Instance.NextActivateAction.Subtract(DateTime.UtcNow).TotalSeconds + "] sec before we should attempt to activate [" + Name + "], waiting.", Logging.Debug);
                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                return false;
            }
        }

        public bool Approach()
        {
            try
            {
                if (DateTime.UtcNow < Time.Instance.LastInWarp.AddSeconds(5))
                {
                    return false;
                }

                if (DateTime.UtcNow > Time.Instance.NextApproachAction)
                {
                    if (_directEntity != null && _directEntity.IsValid && DateTime.UtcNow > Time.Instance.NextApproachAction)
                    {
                        if (DateTime.UtcNow.AddSeconds(-5) > ThisEntityCacheCreated)
                        {
                            Logging.Log("EntityCache.Name", "The EntityCache instance that represents [" + _directEntity.Name + "][" + Math.Round(_directEntity.Distance / 1000, 0) + "k][" + MaskedId + "] was created more than 5 seconds ago (ugh!)", Logging.Debug);
                        }

                        //we cant move in bastion mode, do not try
                        List<ModuleCache> bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
                        if (bastionModules.Any(i => i.IsActive))
                        {
                            Logging.Log("EntityCache.Approach", "BastionMode is active, we cannot move, aborting attempt to Approach", Logging.Debug);
                            return false;
                        }

                        _directEntity.Approach();
                        Time.Instance.NextApproachAction = DateTime.UtcNow.AddSeconds(Time.Instance.ApproachDelay_seconds);
                        Time.Instance.NextTravelerAction = DateTime.UtcNow.AddSeconds(Time.Instance.ApproachDelay_seconds);
                        QMCache.Instance.Approaching = this;
                        return true;
                    }

                    return false;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                QMCache.Instance.Approaching = null;
                return false;
            }
        }

        public bool KeepAtRange(int range)
        {
            try
            {
                if (DateTime.UtcNow > Time.Instance.NextApproachAction)
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (DateTime.UtcNow.AddSeconds(-5) > ThisEntityCacheCreated)
                        {
                            Logging.Log("EntityCache.Name", "The EntityCache instance that represents [" + _directEntity.Name + "][" + Math.Round(_directEntity.Distance / 1000, 0) + "k][" + MaskedId + "] was created more than 5 seconds ago (ugh!)", Logging.Debug);
                        }

                        //we cant move in bastion mode, do not try
                        List<ModuleCache> bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
                        if (bastionModules.Any(i => i.IsActive))
                        {
                            Logging.Log("EntityCache.Approach", "BastionMode is active, we cannot move, aborting attempt to Approach", Logging.Debug);
                            return false;
                        }

                        Time.Instance.NextApproachAction = DateTime.UtcNow.AddSeconds(Time.Instance.ApproachDelay_seconds);
                        _directEntity.KeepAtRange(range);
                        QMCache.Instance.Approaching = this;
                        return true;
                    }

                    return false;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                QMCache.Instance.Approaching = null;
                return false;
            }
        }

        public bool Orbit(int _orbitRange)
        {
            try
            {
                if (DateTime.UtcNow < Time.Instance.LastInWarp.AddSeconds(5))
                {
                    return false;
                }

                if (DateTime.UtcNow > Time.Instance.NextOrbit)
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (DateTime.UtcNow.AddSeconds(-5) > ThisEntityCacheCreated)
                        {
                            Logging.Log("EntityCache.Name", "The EntityCache instance that represents [" + _directEntity.Name + "][" + Math.Round(_directEntity.Distance / 1000, 0) + "k][" + MaskedId + "] was created more than 5 seconds ago (ugh!)", Logging.Debug);
                        }

                        //we cant move in bastion mode, do not try
                        List<ModuleCache> bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
                        if (bastionModules.Any(i => i.IsActive))
                        {
                            Logging.Log("EntityCache.Orbit", "BastionMode is active, we cannot move, aborting attempt to Orbit", Logging.Debug);
                            return false;
                        }

                        _directEntity.Orbit(_orbitRange);
                        Logging.Log("EntityCache", "Initiating Orbit [" + Name + "][at " + Math.Round(((double)_orbitRange / 1000), 2) + "k][" + MaskedId + "]", Logging.Teal);
                        Time.Instance.NextOrbit = DateTime.UtcNow.AddSeconds(10 + QMCache.Instance.RandomNumber(1, 15));
                        QMCache.Instance.Approaching = this;
                        return true;
                    }

                    return false;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                QMCache.Instance.Approaching = null;
                return false;
            }
        }

        public bool WarpTo(int distance = 0)
        {
            try
            {
                if (DateTime.UtcNow < Time.Instance.LastInWarp.AddSeconds(5))
                {
                    return false;
                }

                if (DateTime.UtcNow > Time.Instance.NextWarpAction)
                {
                    if (Time.Instance.LastInSpace.AddSeconds(2) > DateTime.UtcNow && QMCache.Instance.InSpace)
                    {
                        if (_directEntity != null && _directEntity.IsValid)
                        {
                            if (DateTime.UtcNow.AddSeconds(-5) > ThisEntityCacheCreated)
                            {
                                Logging.Log("EntityCache.Name", "The EntityCache instance that represents [" + _directEntity.Name + "][" + Math.Round(_directEntity.Distance / 1000, 0) + "k][" + MaskedId + "] was created more than 5 seconds ago (ugh!)", Logging.Debug);
                            }

                            //
                            // If the position we are trying to warp to is more than 1/2 a light year away it MUST be in a different solar system (31500+ AU)
                            //
                            if (Distance < (long)Distances.HalfOfALightYearInAU)
                            {
                                if (Distance > (int)Distances.WarptoDistance)
                                {
                                    //we cant move in bastion mode, do not try
                                    List<ModuleCache> bastionModules = QMCache.Instance.Modules.Where(m => m.GroupId == (int)Group.Bastion && m.IsOnline).ToList();
                                    if (bastionModules.Any(i => i.IsActive))
                                    {
                                        Logging.Log("EntityCache.WarpTo", "BastionMode is active, we cannot warp, aborting attempt to warp", Logging.Debug);
                                        return false;
                                    }

                                    if (_directEntity.WarpTo(distance))
                                    {
                                        QMCache.Instance.ClearPerPocketCache("WarpTo");
                                        Time.Instance.WehaveMoved = DateTime.UtcNow;
                                        Time.Instance.LastInWarp = DateTime.UtcNow;
                                        Time.Instance.NextWarpAction = DateTime.UtcNow.AddSeconds(Time.Instance.WarptoDelay_seconds);
                                        return true;
                                    }

                                    Logging.Log("EntityCache.WarpTo", "[" + Name + "] Distance [" + Math.Round(Distance / 1000, 0) + "k] returned false", Logging.Debug);
                                    return false;
                                }

                                Logging.Log("EntityCache.WarpTo", "[" + Name + "] Distance [" + Math.Round(Distance / 1000, 0) + "k] is not greater then 150k away, WarpTo aborted!", Logging.Debug);
                                return false;
                            }

                            Logging.Log("EntityCache.WarpTo", "[" + Name + "] Distance [" + Math.Round(Distance / 1000, 0) + "k] was greater than 5000AU away, we assume this an error!, WarpTo aborted!", Logging.Debug);
                            return false;
                        }

                        Logging.Log("EntityCache.WarpTo", "[" + Name + "] DirecEntity is null or is not valid", Logging.Debug);
                        return false;
                    }

                    Logging.Log("EntityCache.WarpTo", "We have not yet been in space at least 2 seconds, waiting", Logging.Debug);
                    return false;
                }

                //Logging.Log("EntityCache.WarpTo", "Waiting [" + Math.Round(Time.Instance.NextWarpAction.Subtract(DateTime.UtcNow).TotalSeconds,0) + "sec] before next attempted warp.", Logging.Debug);
                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                return false;
            }
        }

        public bool AlignTo()
        {
            try
            {
                if (DateTime.UtcNow < Time.Instance.LastInWarp.AddSeconds(5))
                {
                    return false;
                }

                if (DateTime.UtcNow > Time.Instance.NextAlign)
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        if (DateTime.UtcNow.AddSeconds(-5) > ThisEntityCacheCreated)
                        {
                            Logging.Log("EntityCache.Name", "The EntityCache instance that represents [" + _directEntity.Name + "][" + Math.Round(_directEntity.Distance / 1000, 0) + "k][" + MaskedId + "] was created more than 5 seconds ago (ugh!)", Logging.Debug);
                        }

                        _directEntity.AlignTo();
                        Time.Instance.WehaveMoved = DateTime.UtcNow;
                        Time.Instance.NextAlign = DateTime.UtcNow.AddMinutes(Time.Instance.AlignDelay_minutes);
                        return true;
                    }

                    return false;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                return false;
            }
        }

        public void WarpToAndDock()
        {
            try
            {
                if (DateTime.UtcNow < Time.Instance.LastInWarp.AddSeconds(5))
                {
                    return;
                }

                if (DateTime.UtcNow > Time.Instance.NextDockAction && DateTime.UtcNow > Time.Instance.NextWarpAction)
                {
                    if (Time.Instance.LastInSpace.AddSeconds(2) > DateTime.UtcNow && QMCache.Instance.InSpace && DateTime.UtcNow > Time.Instance.LastInStation.AddSeconds(20))
                    {
                        if (_directEntity != null && _directEntity.IsValid)
                        {
                            if (DateTime.UtcNow.AddSeconds(-5) > ThisEntityCacheCreated)
                            {
                                Logging.Log("EntityCache.Name", "The EntityCache instance that represents [" + _directEntity.Name + "][" + Math.Round(_directEntity.Distance / 1000, 0) + "k][" + MaskedId + "] was created more than 5 seconds ago (ugh!)", Logging.Debug);
                            }

                            Time.Instance.WehaveMoved = DateTime.UtcNow;
                            Time.Instance.LastInWarp = DateTime.UtcNow;
                            Time.Instance.NextWarpAction = DateTime.UtcNow.AddSeconds(Time.Instance.WarptoDelay_seconds);
                            Time.Instance.NextDockAction = DateTime.UtcNow.AddSeconds(Time.Instance.DockingDelay_seconds);
                            _directEntity.WarpToAndDock();
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
            }
        }

        public bool Dock()
        {
            try
            {
                if (DateTime.UtcNow < Time.Instance.LastInWarp.AddSeconds(5))
                {
                    return false;
                }

                if (DateTime.UtcNow > Time.Instance.NextDockAction)
                {
                    if (Time.Instance.LastInSpace.AddSeconds(2) > DateTime.UtcNow && QMCache.Instance.InSpace && DateTime.UtcNow > Time.Instance.LastInStation.AddSeconds(20))
                    {
                        if (_directEntity != null && _directEntity.IsValid)
                        {
                            //if (Distance < (int) Distances.DockingRange)
                            //{
                            _directEntity.Dock();
                            Time.Instance.WehaveMoved = DateTime.UtcNow;
                            Time.Instance.NextDockAction = DateTime.UtcNow.AddSeconds(Time.Instance.DockingDelay_seconds);
                            Time.Instance.NextApproachAction = DateTime.UtcNow.AddSeconds(Time.Instance.DockingDelay_seconds);
                            Time.Instance.LastSessionChange = DateTime.UtcNow;
                            Time.Instance.NextActivateModules = DateTime.UtcNow.AddSeconds(Time.Instance.TravelerJumpedGateNextCommandDelay_seconds);
                            //}

                            //Logging.Log("Dock", "[" + Name + "][" + Distance +"] is not in docking range, aborting docking request", Logging.Debug);
                            //return false;
                        }

                        //Logging.Log("Dock", "[" + Name + "]: directEntity is null or is not valid", Logging.Debug);
                        return false;
                    }

                    Logging.Log("Dock", "We were last detected in space [" + DateTime.UtcNow.Subtract(Time.Instance.LastInSpace).TotalSeconds + "] seconds ago. We have been unDocked for [ " + DateTime.UtcNow.Subtract(Time.Instance.LastInStation).TotalSeconds + " ] seconds. we should not dock yet, waiting", Logging.Debug);
                    return false;
                }

                //Logging.Log("Dock", "Dock command will not be allowed again until after another [" + Math.Round(Time.Instance.NextDockAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + "sec]", Logging.Red);
                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                return false;
            }
        }

        public bool OpenCargo()
        {
            try
            {
                if (DateTime.UtcNow > Time.Instance.NextOpenCargoAction)
                {
                    if (_directEntity != null && _directEntity.IsValid)
                    {
                        _directEntity.OpenCargo();
                        Time.Instance.NextOpenCargoAction = DateTime.UtcNow.AddSeconds(2 + QMCache.Instance.RandomNumber(1, 3));
                        return true;
                    }

                    return false;
                }

                return false;
            }
            catch (Exception exception)
            {
                Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
                return false;
            }
        }

        public void MakeActiveTarget()
        {
            try
            {
                if (_directEntity != null && _directEntity.IsValid)
                {
                    if (IsTarget)
                    {
                        _directEntity.MakeActiveTarget();
                        Time.Instance.NextMakeActiveTargetAction = DateTime.UtcNow.AddSeconds(1 + QMCache.Instance.RandomNumber(2, 3));
                    }

                    return;
                }

                return;
            }
            catch (Exception exception)
            {
                Logging.Log("EntityCache", "Exception [" + exception + "]", Logging.Debug);
            }
        }
    }
}
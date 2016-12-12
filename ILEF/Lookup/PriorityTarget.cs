// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------


namespace ILEF.Lookup
{
    using System;
    using global::ILEF.Caching;
    using global::ILEF.Logging;

    public class PriorityTarget
    {
        private EntityCache _entity;

        public long EntityID { get; set; }

        private string _maskedID;
        public string MaskedID
        {
            get
            {
                try
                {
                    int numofCharacters = EntityID.ToString().Length;
                    if (numofCharacters >= 5)
                    {
                        _maskedID = EntityID.ToString().Substring(numofCharacters - 4);
                        _maskedID = "[MaskedID]" + _maskedID;
                        return _maskedID;
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

        public string Name { get; set; }

        public PrimaryWeaponPriority PrimaryWeaponPriority { get; set; }

        public DronePriority DronePriority { get; set; }

        public EntityCache Entity
        {
            get { return _entity ?? (_entity = QMCache.Instance.EntityById(EntityID)); }
        }

        public void ClearCache()
        {
            _entity = null;
        }
    }
}
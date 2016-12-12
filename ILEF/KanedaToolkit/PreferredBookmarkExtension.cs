#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Linq;
using ILoveEVE.Framework;
using ILEF.Caching;

namespace ILEF.KanedaToolkit
{

    public static class PreferredBookmarkExtension
    {
        public static DirectBookmark PreferredBookmark(this IEnumerable<DirectBookmark> items, Func<DirectBookmark, bool> pred)
        {
            List<int> currentPath = QMCache.Instance.DirectEve.Navigation.GetDestinationPath();
            return QMCache.Instance.AllBookmarks.Where(pred)
                .Where(a => currentPath.Contains((int)a.LocationId)) // ignore unreachable bookmarks (w-space)
                .OrderBy(a => !currentPath.Contains((int)a.LocationId)) // nearest bookmark, prefer in system
                .First();
        }
    }

}

using ILoveEVE.Framework;
using ILEF.Lookup;

namespace ILEF.KanedaToolkit
{
    static class KBookmark
    {

        public static bool Dockable (this DirectBookmark bookmark)
        {
            if (bookmark.GroupId == (int)Group.Station) return true;
            if (bookmark.GroupId == (int)Group.Citadel || bookmark.GroupId == (int)Group.Citadel) return true;
            return false;
        }

    }
}

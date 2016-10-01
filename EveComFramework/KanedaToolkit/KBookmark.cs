using EveCom;

namespace EveComFramework.KanedaToolkit
{
    static class KBookmark
    {

        public static bool Dockable (this Bookmark bookmark)
        {
            if (bookmark.GroupID == Group.Station) return true;
            if (bookmark.GroupID == Group.Citadel || bookmark.GroupID == Group.Citadel) return true;
            return false;
        }

    }
}

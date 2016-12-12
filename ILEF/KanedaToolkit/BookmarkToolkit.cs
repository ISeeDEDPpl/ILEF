using System;
using ILoveEVE.Framework;
using ILEF.Caching;
using System.Linq;

namespace ILEF.KanedaToolkit
{
    /// <summary>
    /// Bookmark Toolkit
    /// </summary>
    public class BookmarkToolkit
    {
        #region Instantiation
        static BookmarkToolkit _Instance;
        /// <summary>
        /// Singletoner
        /// </summary>
        public static BookmarkToolkit Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new BookmarkToolkit();
                }
                return _Instance;
            }
        }

        private BookmarkToolkit()
        {

        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Delete a Bookmark with a given name
        /// </summary>
        /// <param name="bookmarkName">Bookmark name</param>
        /// <returns></returns>
        public bool DeleteBookmark(String bookmarkName)
        {
            return DeleteBookmark(QMCache.Instance.AllBookmarks.FirstOrDefault(a => a.Title == bookmarkName));
        }

        /// <summary>
        /// Safely delete a bookmark
        /// </summary>
        /// <param name="bookmark">bookmark</param>
        /// <returns></returns>
        public bool DeleteBookmark(DirectBookmark bookmark)
        {
            if (bookmark != null)
            {
                bookmark.Delete();
            }
            return false;
        }

        #endregion
    }
}

namespace EveComFramework.KanedaToolkit
{
    /// <summary>
    /// This class provides constants for static values.
    ///  Naming is not final, so please don't use it outside kaneda projects for now.
    /// </summary>
    public class Constants
    {
        /// <summary>
        /// This should be used to determine if we are on grid with other things like anomalies, structures, etc.
        /// </summary>
        public static readonly int GridsizeMaxDistance = 8000000;

        /// <summary>
        /// This defines the minimum distance required from the target to be able to warp to it.
        /// </summary>
        public static readonly int WarpMinDistance = 150000;
    }
}

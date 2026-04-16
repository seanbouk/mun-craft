namespace MunCraft.Core
{
    /// <summary>
    /// Lightweight static flags that let independent systems (player,
    /// camera, miner, menus) coordinate without hard references.
    /// </summary>
    public static class GameState
    {
        /// <summary>
        /// True while any side menu is open. Player/camera/miner skip
        /// input when this is set; the menu manager owns it.
        /// </summary>
        public static bool MenuOpen;
    }
}

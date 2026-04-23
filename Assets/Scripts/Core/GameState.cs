namespace MunCraft.Core
{
    public enum FlowState
    {
        Title,
        LevelSelect,
        Playing
    }

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

        /// <summary>
        /// Current game flow state — Title screen, Level select, or Playing.
        /// UI systems hide when not Playing; menus suppress input.
        /// </summary>
        public static FlowState CurrentFlow = FlowState.Title;
    }
}

namespace Editor.LevelEditor
{
    // USS class name constants. A mismatch between these and LevelEditor.uss is a
    // silent failure at runtime; centralising the strings here makes USS-refactoring safe.
    internal static class Css
    {
        public const string Root          = "rl-root";
        public const string AppContainer  = "rl-app-container";
        public const string Topbar        = "rl-topbar";
        public const string TopbarSep     = "rl-topbar__sep";

        public const string Btn        = "rl-btn";
        public const string BtnPrimary = "rl-btn--primary";
        public const string BtnDanger  = "rl-btn--danger";
        public const string Num        = "rl-num";

        public const string Tool       = "rl-tool";
        public const string ToolActive = "rl-tool--active";

        public const string CanvasHost = "rl-canvas-host";
        public const string Canvas     = "rl-canvas";

        public const string StatusLabel = "rl-status-label";

        public const string Section       = "rl-section";
        public const string SectionHeader = "rl-section__header";
        public const string Row           = "rl-row";
        public const string RowWrap       = "rl-row--wrap";

        // ── Color/entity palette ─────────────────────────────────────────────
        public const string PaletteSwatch         = "rl-palette-swatch";
        public const string PaletteSwatchSelected = "rl-palette-swatch--selected";

        // ── Table order queue (side panel) ───────────────────────────────────
        public const string Lane       = "rl-lane";
        public const string LaneHeader = "rl-lane__header";
        public const string LaneList   = "rl-lane__list";
        public const string Chip       = "rl-chip";
        public const string ChipFront  = "rl-chip--front";
        public const string ChipIcon   = "rl-chip__icon";
        public const string ChipLabel  = "rl-chip__label";
        public const string ChipRemove = "rl-chip__remove";
    }
}

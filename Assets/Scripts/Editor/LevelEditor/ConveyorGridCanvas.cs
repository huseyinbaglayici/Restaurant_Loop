using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor.LevelEditor
{
    public enum CanvasMode { Path, Entity, Table }

    /// <summary>
    /// Custom-drawn grid canvas for painting the conveyor plus the food entities that ride it.
    /// The source of truth is <see cref="Data"/> (a <see cref="ConveyorLevelDataSO"/>) — every
    /// mutation goes through <c>Undo.RecordObject</c> on it first, so Ctrl+Z/Y work the same way
    /// they do for any other Unity object. <see cref="Cells"/>/<see cref="Entities"/> are just a
    /// fast lookup cache rebuilt from <see cref="Data"/> after every change (including undo/redo).
    ///
    /// Path mode: pick a brush shape (Straight or Corner) and left-drag to stamp it onto cells
    /// (new/changed cells start at rotation 0). Rotation is set explicitly, standard tile-editor
    /// style: Shift+Click a cell to rotate it 90° CW, or hover it and press R. Right-drag erases.
    ///
    /// Entity mode: left-drag across existing conveyor cells to stamp the selected dish+amount
    /// onto them; right-drag clears entities. Only valid on cells that are conveyor.
    ///
    /// Table mode: click an empty non-conveyor cell to place a table there (its order queue
    /// starts empty — edit it in the side panel); click an existing table to select it instead
    /// of re-placing it. Right-click erases a table and its whole queue.
    /// </summary>
    public class ConveyorGridCanvas : VisualElement
    {
        public int GridWidth = 8;
        public int GridHeight = 8;
        public float CellSize = 40f;

        /// <summary>Backing data — must be assigned before any interaction. Every edit records Undo on this.</summary>
        public ConveyorLevelDataSO Data;

        /// <summary>Read-only lookup cache mirroring Data.ConveyorCells, rebuilt after every change.</summary>
        public readonly Dictionary<Vector2Int, ConveyorCellPaint> Cells = new();

        /// <summary>Read-only lookup cache mirroring Data.ConveyorEntities.</summary>
        public readonly Dictionary<Vector2Int, FoodStackData> Entities = new();

        /// <summary>Read-only lookup cache mirroring Data.Tables.</summary>
        public readonly Dictionary<Vector2Int, TableData> Tables = new();

        public CanvasMode Mode = CanvasMode.Path;
        public ConveyorTileShape BrushShape = ConveyorTileShape.Straight;
        public FoodType SelectedFoodType = FoodType.Hamburger;
        public int SelectedAmount = 1;
        public Vector2Int? SelectedTable { get; private set; }

        public System.Action OnCellsChanged;
        public System.Action OnEntitiesChanged;
        public System.Action OnTablesChanged;

        /// <summary>Fires whenever a table is selected (placed or clicked) — argument is its coord.</summary>
        public System.Action<Vector2Int> OnTableSelected;

        private static readonly Color GridLineColor = new(1f, 1f, 1f, 0.08f);
        private static readonly Color CellBgColor = new(1f, 1f, 1f, 0.03f);
        private static readonly Color BeltColor = new(0.55f, 0.55f, 0.58f);
        private static readonly Color BeltOutline = new(0.25f, 0.25f, 0.27f);
        private static readonly Color TreadColor = new(0.38f, 0.38f, 0.40f);
        private static readonly Color ArrowColor = new(0.95f, 0.75f, 0.15f);
        private static readonly Color EntityOutline = new(0f, 0f, 0f, 0.55f);
        private static readonly Color TableColor = new(0.18f, 0.42f, 0.38f);
        private static readonly Color TableOutline = new(0.10f, 0.24f, 0.22f);
        private static readonly Color TableSelectedOutline = new(0.95f, 0.75f, 0.15f);
        private static readonly Color GateColor = new(0.2f, 0.9f, 0.95f);

        private bool _isDragging;
        private bool _eraseMode;
        private Vector2Int? _hoverCell;
        private readonly VisualElement _entityLabelLayer;

        public ConveyorGridCanvas()
        {
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerLeaveEvent>(_ => _hoverCell = null);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            focusable = true;
            style.width = 0; // set explicitly by owner via Resize()
            style.height = 0;

            _entityLabelLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _entityLabelLayer.style.position = Position.Absolute;
            _entityLabelLayer.style.left = 0;
            _entityLabelLayer.style.top = 0;
            Add(_entityLabelLayer);
        }

        public void Resize()
        {
            style.width = GridWidth * CellSize;
            style.height = GridHeight * CellSize;
            MarkDirtyRepaint();
        }

        /// <summary>Rebuilds the lookup cache from Data and repaints. Call after Data changes externally (Load, undo/redo).</summary>
        public void RebuildCacheAndRepaint()
        {
            Cells.Clear();
            foreach (var c in Data.ConveyorCells) Cells[c.Coord] = new ConveyorCellPaint { Shape = c.Shape, Rotation = c.Rotation, IsGate = c.IsGate };

            Entities.Clear();
            foreach (var e in Data.ConveyorEntities) Entities[e.Coord] = e.Stack;

            Tables.Clear();
            foreach (var t in Data.Tables) Tables[t.Coord] = t;

            RebuildEntityLabels();
            MarkDirtyRepaint();
        }

        /// <summary>Icon + amount badge overlaid on entity/table cells — color alone can't tell 9 dishes apart.</summary>
        private void RebuildEntityLabels()
        {
            _entityLabelLayer.Clear();
            foreach (var kv in Entities)
            {
                Vector2 center = CellCenterLocal(kv.Key);
                _entityLabelLayer.Add(EntityLabelFactory.CreateIcon(kv.Value.Type, center, CellSize));
                if (kv.Value.Amount > 1)
                    _entityLabelLayer.Add(EntityLabelFactory.CreateAmountBadge(kv.Value.Amount, center, CellSize));
            }

            // A table shows the icon for whatever it's currently requesting (front of its queue).
            foreach (var kv in Tables)
            {
                if (kv.Value.Orders.Count == 0) continue;
                var front = kv.Value.Orders[0];
                Vector2 center = CellCenterLocal(kv.Key);
                _entityLabelLayer.Add(EntityLabelFactory.CreateIcon(front.Type, center, CellSize));
                if (front.Amount > 1)
                    _entityLabelLayer.Add(EntityLabelFactory.CreateAmountBadge(front.Amount, center, CellSize));
            }
        }

        public void ClearAll()
        {
            Undo.RecordObject(Data, "Clear Conveyor");
            Data.ConveyorCells.Clear();
            Data.ConveyorEntities.Clear();
            Data.Tables.Clear();
            SelectedTable = null;
            RebuildCacheAndRepaint();
            OnCellsChanged?.Invoke();
            OnEntitiesChanged?.Invoke();
            OnTablesChanged?.Invoke();
        }

        private Vector2Int CellAt(Vector2 localPos)
        {
            int cx = Mathf.FloorToInt(localPos.x / CellSize);
            int cy = GridHeight - 1 - Mathf.FloorToInt(localPos.y / CellSize); // Y flipped: row 0 at bottom
            return new Vector2Int(cx, cy);
        }

        private bool InBounds(Vector2Int c) =>
            c.x >= 0 && c.x < GridWidth && c.y >= 0 && c.y < GridHeight;

        private void OnPointerDown(PointerDownEvent evt)
        {
            Focus(); // so KeyDownEvent (R = rotate) routes here after a click

            var cell = CellAt(evt.localPosition);
            if (!InBounds(cell)) return;

            // Shift+left-click rotates an existing cell instead of painting/dragging.
            if (evt.button == 0 && evt.shiftKey && Mode == CanvasMode.Path)
            {
                RotateAt(cell);
                evt.StopPropagation();
                return;
            }

            // Ctrl+left-click toggles the Gate (the belt's spawn/despawn seam) — at most one per level.
            if (evt.button == 0 && evt.ctrlKey && Mode == CanvasMode.Path)
            {
                ToggleGateAt(cell);
                evt.StopPropagation();
                return;
            }

            // Tables are placed/selected one click at a time — no drag-painting.
            if (Mode == CanvasMode.Table)
            {
                HandleTableClick(cell, evt.button == 1);
                evt.StopPropagation();
                return;
            }

            this.CapturePointer(evt.pointerId);
            _isDragging = true;
            _eraseMode = evt.button == 1; // right-click drags erase

            PaintAt(cell);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            var cell = CellAt(evt.localPosition);
            _hoverCell = InBounds(cell) ? cell : null;
            MarkDirtyRepaint(); // hover outline needs to track the cursor even while idle

            if (!_isDragging || !InBounds(cell)) return;

            PaintAt(cell);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging) return;
            _isDragging = false;
            this.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.R || Mode != CanvasMode.Path || !_hoverCell.HasValue) return;
            RotateAt(_hoverCell.Value);
            evt.StopPropagation();
        }

        private int FindCellIndex(Vector2Int coord) => Data.ConveyorCells.FindIndex(c => c.Coord == coord);
        private int FindEntityIndex(Vector2Int coord) => Data.ConveyorEntities.FindIndex(e => e.Coord == coord);
        private int FindTableIndex(Vector2Int coord) => Data.Tables.FindIndex(t => t.Coord == coord);

        private void HandleTableClick(Vector2Int cell, bool erase)
        {
            if (erase)
            {
                if (FindTableIndex(cell) < 0) return;
                Undo.RecordObject(Data, "Erase Table");
                Data.Tables.RemoveAll(t => t.Coord == cell);
                if (SelectedTable == cell) SelectedTable = null;
                RebuildCacheAndRepaint();
                OnTablesChanged?.Invoke();
                return;
            }

            if (Cells.ContainsKey(cell)) return; // tables only sit on floor cells, not the belt itself

            if (!Tables.ContainsKey(cell))
            {
                Undo.RecordObject(Data, "Place Table");
                Data.Tables.Add(new TableData { Coord = cell });
                RebuildCacheAndRepaint();
                OnTablesChanged?.Invoke();
            }

            SelectedTable = cell;
            MarkDirtyRepaint();
            OnTableSelected?.Invoke(cell);
        }

        private void RotateAt(Vector2Int cell)
        {
            int idx = FindCellIndex(cell);
            if (idx < 0) return;

            Undo.RecordObject(Data, "Rotate Conveyor Cell");
            var entry = Data.ConveyorCells[idx];
            entry.Rotation = (entry.Rotation + 90f) % 360f;
            Data.ConveyorCells[idx] = entry;
            RebuildCacheAndRepaint();
            OnCellsChanged?.Invoke();
        }

        /// <summary>At most one Gate per level — setting a new one clears whichever cell held it before.</summary>
        private void ToggleGateAt(Vector2Int cell)
        {
            int idx = FindCellIndex(cell);
            if (idx < 0)
            {
                Debug.LogWarning($"[ConveyorGridCanvas] Ctrl+Click at {cell} isn't a conveyor cell yet — paint Straight/Corner there first, then Ctrl+Click to mark it as Gate.");
                return;
            }

            Undo.RecordObject(Data, "Set Gate");
            bool turningOn = !Data.ConveyorCells[idx].IsGate;
            Debug.Log($"[ConveyorGridCanvas] Gate at {cell}: {(turningOn ? "ON" : "OFF")}");
            for (int i = 0; i < Data.ConveyorCells.Count; i++)
            {
                var c = Data.ConveyorCells[i];
                bool shouldBeGate = turningOn && i == idx;
                if (c.IsGate != shouldBeGate)
                {
                    c.IsGate = shouldBeGate;
                    Data.ConveyorCells[i] = c;
                }
            }
            RebuildCacheAndRepaint();
            OnCellsChanged?.Invoke();
        }

        private void PaintAt(Vector2Int cell)
        {
            if (Mode == CanvasMode.Path)
            {
                if (_eraseMode)
                {
                    if (!Cells.ContainsKey(cell)) return;
                    Undo.RecordObject(Data, "Erase Conveyor Cell");
                    Data.ConveyorCells.RemoveAll(c => c.Coord == cell);
                    Data.ConveyorEntities.RemoveAll(e => e.Coord == cell);
                    RebuildCacheAndRepaint();
                    OnCellsChanged?.Invoke();
                    OnEntitiesChanged?.Invoke();
                }
                else
                {
                    // Only (re)stamp when the shape actually changes, so dragging back over
                    // already-painted cells doesn't reset a rotation you already set.
                    if (Cells.TryGetValue(cell, out var existing) && existing.Shape == BrushShape) return;

                    Undo.RecordObject(Data, "Paint Conveyor Cell");
                    int idx = FindCellIndex(cell);
                    var entry = new ConveyorCellData { Coord = cell, Shape = BrushShape, Rotation = 0f };
                    if (idx >= 0) Data.ConveyorCells[idx] = entry;
                    else Data.ConveyorCells.Add(entry);
                    RebuildCacheAndRepaint();
                    OnCellsChanged?.Invoke();
                }
            }
            else // Entity mode — only valid on existing conveyor cells
            {
                if (!Cells.ContainsKey(cell)) return;

                if (_eraseMode)
                {
                    if (FindEntityIndex(cell) < 0) return;
                    Undo.RecordObject(Data, "Erase Entity");
                    Data.ConveyorEntities.RemoveAll(e => e.Coord == cell);
                    RebuildCacheAndRepaint();
                    OnEntitiesChanged?.Invoke();
                }
                else
                {
                    Undo.RecordObject(Data, "Paint Entity");
                    int idx = FindEntityIndex(cell);
                    var entry = new ConveyorEntityPlacement
                    {
                        Coord = cell,
                        Stack = new FoodStackData { Type = SelectedFoodType, Amount = SelectedAmount }
                    };
                    if (idx >= 0) Data.ConveyorEntities[idx] = entry;
                    else Data.ConveyorEntities.Add(entry);
                    RebuildCacheAndRepaint();
                    OnEntitiesChanged?.Invoke();
                }
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;

            // grid lines + cell backgrounds
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    var r = new Rect(x * CellSize, y * CellSize, CellSize, CellSize);
                    painter.fillColor = CellBgColor;
                    painter.BeginPath();
                    painter.MoveTo(r.position);
                    painter.LineTo(new Vector2(r.xMax, r.yMin));
                    painter.LineTo(new Vector2(r.xMax, r.yMax));
                    painter.LineTo(new Vector2(r.xMin, r.yMax));
                    painter.ClosePath();
                    painter.Fill();
                }
            }

            painter.strokeColor = GridLineColor;
            painter.lineWidth = 1f;
            for (int x = 0; x <= GridWidth; x++)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x * CellSize, 0));
                painter.LineTo(new Vector2(x * CellSize, GridHeight * CellSize));
                painter.Stroke();
            }
            for (int y = 0; y <= GridHeight; y++)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, y * CellSize));
                painter.LineTo(new Vector2(GridWidth * CellSize, y * CellSize));
                painter.Stroke();
            }

            // conveyor cells
            foreach (var kv in Cells)
            {
                Vector2 center = CellCenterLocal(kv.Key);
                if (kv.Value.Shape == ConveyorTileShape.Straight)
                {
                    DrawStraightTile(painter, center, kv.Value.Rotation);
                    DrawArrow(painter, center, new Vector2(1f, 0f), kv.Value.Rotation, CellSize * 0.30f);
                }
                else
                {
                    DrawCornerTile(painter, center, kv.Value.Rotation);
                    // A corner connects two edges, not one flow direction — one arrow was
                    // ambiguous (which edge is it even pointing from/to?). Two arrows, one per
                    // connected edge, remove that ambiguity outright.
                    DrawArrow(painter, center, new Vector2(-1f, 0f), kv.Value.Rotation, CellSize * 0.22f, CellSize * 0.30f);
                    DrawArrow(painter, center, new Vector2(0f, -1f), kv.Value.Rotation, CellSize * 0.22f, CellSize * 0.30f);
                }

                if (Entities.TryGetValue(kv.Key, out var stack))
                    DrawEntity(painter, center, stack);

                if (kv.Value.IsGate)
                    DrawGateMarker(painter, center);
            }

            // tables
            foreach (var kv in Tables)
            {
                Vector2 center = CellCenterLocal(kv.Key);
                DrawTable(painter, center, SelectedTable.HasValue && SelectedTable.Value == kv.Key);
            }

            // hover outline — helps line up the R-to-rotate gesture with a specific cell
            if (_hoverCell.HasValue)
            {
                var r = new Rect(_hoverCell.Value.x * CellSize, (GridHeight - 1 - _hoverCell.Value.y) * CellSize, CellSize, CellSize);
                painter.strokeColor = new Color(1f, 1f, 1f, 0.35f);
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                painter.MoveTo(r.position);
                painter.LineTo(new Vector2(r.xMax, r.yMin));
                painter.LineTo(new Vector2(r.xMax, r.yMax));
                painter.LineTo(new Vector2(r.xMin, r.yMax));
                painter.ClosePath();
                painter.Stroke();
            }
        }

        private Vector2 CellCenterLocal(Vector2Int cell)
        {
            float px = cell.x * CellSize + CellSize * 0.5f;
            float py = (GridHeight - 1 - cell.y) * CellSize + CellSize * 0.5f;
            return new Vector2(px, py);
        }

        private static Vector2 RotatePoint(Vector2 center, Vector2 local, float rotationDeg)
        {
            // UI space Y grows downward, so invert to keep "clockwise" reading as clockwise on screen.
            float rad = -rotationDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(
                center.x + local.x * cos - local.y * sin,
                center.y + local.x * sin + local.y * cos);
        }

        /// <summary>A straight belt segment: a plank running fully across the cell, with a cross-tread in the middle.</summary>
        private void DrawStraightTile(Painter2D painter, Vector2 center, float rotation)
        {
            float h = CellSize * 0.46f;
            float bandHalf = CellSize * 0.16f;

            Vector2 p0 = RotatePoint(center, new Vector2(-h, -bandHalf), rotation);
            Vector2 p1 = RotatePoint(center, new Vector2(h, -bandHalf), rotation);
            Vector2 p2 = RotatePoint(center, new Vector2(h, bandHalf), rotation);
            Vector2 p3 = RotatePoint(center, new Vector2(-h, bandHalf), rotation);

            painter.fillColor = BeltColor;
            painter.strokeColor = BeltOutline;
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(p0);
            painter.LineTo(p1);
            painter.LineTo(p2);
            painter.LineTo(p3);
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();

            // tread ridges, echoing the real belt mesh's cross bars
            painter.strokeColor = TreadColor;
            painter.lineWidth = 2f;
            foreach (float lx in new[] { -h * 0.45f, h * 0.45f })
            {
                Vector2 t0 = RotatePoint(center, new Vector2(lx, -bandHalf), rotation);
                Vector2 t1 = RotatePoint(center, new Vector2(lx, bandHalf), rotation);
                painter.BeginPath();
                painter.MoveTo(t0);
                painter.LineTo(t1);
                painter.Stroke();
            }
        }

        /// <summary>
        /// A corner belt segment: an actual quarter-pipe elbow (not a square + dot), so the shape
        /// reads as "this turns" at a glance. Base orientation (rotation 0) hugs the cell's
        /// top-left corner, connecting the Left edge to the Top edge.
        /// </summary>
        private void DrawCornerTile(Painter2D painter, Vector2 center, float rotation)
        {
            float h = CellSize * 0.46f;
            float outerR = h;
            float innerR = outerR - CellSize * 0.32f; // same band thickness as the straight tile
            Vector2 pivotLocal = new(-h, -h);
            const int segments = 10;

            painter.fillColor = BeltColor;
            painter.strokeColor = BeltOutline;
            painter.lineWidth = 2f;
            painter.BeginPath();

            for (int i = 0; i <= segments; i++)
            {
                float t = Mathf.Lerp(90f, 0f, i / (float)segments) * Mathf.Deg2Rad;
                var local = pivotLocal + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * outerR;
                var p = RotatePoint(center, local, rotation);
                if (i == 0) painter.MoveTo(p); else painter.LineTo(p);
            }
            for (int i = 0; i <= segments; i++)
            {
                float t = Mathf.Lerp(0f, 90f, i / (float)segments) * Mathf.Deg2Rad;
                var local = pivotLocal + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * innerR;
                var p = RotatePoint(center, local, rotation);
                painter.LineTo(p);
            }

            painter.ClosePath();
            painter.Fill();
            painter.Stroke();

            // tread ridge following the curve's midline, same purpose as the straight tile's bars
            painter.strokeColor = TreadColor;
            painter.lineWidth = 1.5f;
            float midR = (outerR + innerR) * 0.5f;
            painter.BeginPath();
            for (int i = 0; i <= segments; i++)
            {
                float t = Mathf.Lerp(90f, 0f, i / (float)segments) * Mathf.Deg2Rad;
                var local = pivotLocal + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * midR;
                var p = RotatePoint(center, local, rotation);
                if (i == 0) painter.MoveTo(p); else painter.LineTo(p);
            }
            painter.Stroke();
        }

        /// <summary>
        /// Draws one arrow, built from a base direction (unrotated, at cell rotation 0) that gets
        /// rotated by the cell's actual rotation — so the arrow always tracks a real edge of the
        /// tile rather than an assumed "always points right" direction.
        /// </summary>
        private void DrawArrow(Painter2D painter, Vector2 center, Vector2 baseDir, float rotationDeg, float size, float centerOffset = 0f)
        {
            Vector2 dir = RotatePoint(Vector2.zero, baseDir, rotationDeg);
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 arrowCenter = center + dir * centerOffset;

            painter.fillColor = ArrowColor;
            painter.BeginPath();
            painter.MoveTo(arrowCenter + dir * (size * 0.5f));
            painter.LineTo(arrowCenter - dir * (size * 0.15f) + perp * (size * 0.35f));
            painter.LineTo(arrowCenter - dir * (size * 0.15f) - perp * (size * 0.35f));
            painter.ClosePath();
            painter.Fill();
        }

        private void DrawEntity(Painter2D painter, Vector2 center, FoodStackData stack)
        {
            float radius = CellSize * 0.22f;

            painter.fillColor = FoodTypeUtil.ToColor(stack.Type);
            painter.strokeColor = EntityOutline;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Fill();
            painter.Stroke();
        }

        private void DrawTable(Painter2D painter, Vector2 center, bool selected)
        {
            float half = CellSize * 0.38f;
            var r = new Rect(center.x - half, center.y - half, half * 2, half * 2);

            painter.fillColor = TableColor;
            painter.strokeColor = selected ? TableSelectedOutline : TableOutline;
            painter.lineWidth = selected ? 3f : 2f;
            painter.BeginPath();
            painter.MoveTo(r.position);
            painter.LineTo(new Vector2(r.xMax, r.yMin));
            painter.LineTo(new Vector2(r.xMax, r.yMax));
            painter.LineTo(new Vector2(r.xMin, r.yMax));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }

        /// <summary>
        /// Diamond ring marking the belt's spawn/despawn seam — drawn as a vector shape (not a
        /// text glyph) so it always renders regardless of what the editor's font supports.
        /// </summary>
        private void DrawGateMarker(Painter2D painter, Vector2 center)
        {
            float r = CellSize * 0.16f;
            painter.strokeColor = GateColor;
            painter.lineWidth = 2.5f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(center.x, center.y - r));
            painter.LineTo(new Vector2(center.x + r, center.y));
            painter.LineTo(new Vector2(center.x, center.y + r));
            painter.LineTo(new Vector2(center.x - r, center.y));
            painter.ClosePath();
            painter.Stroke();
        }
    }
}

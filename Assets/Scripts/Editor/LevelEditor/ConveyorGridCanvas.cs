using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor.LevelEditor
{
    public enum CanvasMode { Path, Entity, Customer }

    public enum CustomerSubMode { Order, SecondOrder, Hidden }

    public class ConveyorGridCanvas : VisualElement
    {
        public int GridWidth = 8;
        public int GridHeight = 8;
        public float CellSize = 40f;

        public ConveyorLevelDataSO Data;

        public readonly Dictionary<Vector2Int, ConveyorCellPaint> Cells = new();
        public readonly Dictionary<Vector2Int, FoodStackData> Entities = new();
        public readonly Dictionary<Vector2Int, CustomerEntity> Customers = new();

        public CanvasMode Mode = CanvasMode.Path;
        public CustomerSubMode CustomerMode = CustomerSubMode.Order;
        public ConveyorTileShape BrushShape = ConveyorTileShape.Straight;
        public FoodType SelectedFoodType = FoodType.Hamburger;
        public int SelectedAmount = 1;

        public System.Action OnCellsChanged;
        public System.Action OnEntitiesChanged;
        public System.Action OnCustomersChanged;

        private static readonly Color GridLineColor = new(1f, 1f, 1f, 0.08f);
        private static readonly Color CellBgColor = new(1f, 1f, 1f, 0.03f);
        private static readonly Color BeltColor = new(0.55f, 0.55f, 0.58f);
        private static readonly Color BeltOutline = new(0.25f, 0.25f, 0.27f);
        private static readonly Color TreadColor = new(0.38f, 0.38f, 0.40f);
        private static readonly Color ArrowColor = new(0.95f, 0.75f, 0.15f);
        private static readonly Color EntityOutline = new(0f, 0f, 0f, 0.55f);
        private static readonly Color CustomerColor = new(0.18f, 0.42f, 0.38f);
        private static readonly Color CustomerOutline = new(0.10f, 0.24f, 0.22f);
        private static readonly Color GateColor = new(0.2f, 0.9f, 0.95f);

        private bool _isDragging;
        private bool _eraseMode;
        private Vector2Int? _hoverCell;
        private readonly VisualElement _entityLabelLayer;
        private Vector2Int? _pickedCell;
        private System.Action<Vector2Int> _pickCallback;

        public void BeginPick(System.Action<Vector2Int> onPicked) => _pickCallback = onPicked;

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

        public void RebuildCacheAndRepaint()
        {
            Cells.Clear();
            foreach (var c in Data.ConveyorCells) Cells[c.Coord] = new ConveyorCellPaint { Shape = c.Shape, Rotation = c.Rotation, IsGate = c.IsGate };

            Entities.Clear();
            foreach (var e in Data.ConveyorEntities) Entities[e.Coord] = e.Stack;

            Customers.Clear();
            foreach (var c in Data.Customers) Customers[c.Coord] = c;

            RebuildEntityLabels();
            MarkDirtyRepaint();
        }

        private void RebuildEntityLabels()
        {
            _entityLabelLayer.Clear();
            foreach (var kv in Entities)
            {
                Vector2 center = CellCenterLocal(kv.Key);
                _entityLabelLayer.Add(EntityLabelFactory.CreateIcon(kv.Value.Type, center, CellSize));
                if (kv.Value.Amount > 0)
                    _entityLabelLayer.Add(EntityLabelFactory.CreateAmountBadge(kv.Value.Amount, center, CellSize));
            }

            foreach (var kv in Customers)
            {
                Vector2 center = CellCenterLocal(kv.Key);
                var order = kv.Value.Order;

                if (kv.Value.HasSecondOrder)
                {
                    // split cell left/right
                    Vector2 leftCenter = center - new Vector2(CellSize * 0.24f, 0f);
                    Vector2 rightCenter = center + new Vector2(CellSize * 0.24f, 0f);
                    float halfSize = CellSize * 0.6f;

                    _entityLabelLayer.Add(EntityLabelFactory.CreateIcon(order.Type, leftCenter, halfSize));
                    if (order.Amount > 0)
                        _entityLabelLayer.Add(EntityLabelFactory.CreateAmountBadge(order.Amount, leftCenter, halfSize));

                    var second = kv.Value.SecondOrder;
                    _entityLabelLayer.Add(EntityLabelFactory.CreateIcon(second.Type, rightCenter, halfSize));
                    if (second.Amount > 0)
                        _entityLabelLayer.Add(EntityLabelFactory.CreateAmountBadge(second.Amount, rightCenter, halfSize));
                }
                else
                {
                    _entityLabelLayer.Add(EntityLabelFactory.CreateIcon(order.Type, center, CellSize));
                    if (order.Amount > 0)
                        _entityLabelLayer.Add(EntityLabelFactory.CreateAmountBadge(order.Amount, center, CellSize));
                }

                if (kv.Value.IsHidden)
                    _entityLabelLayer.Add(EntityLabelFactory.CreateHiddenBadge(center, CellSize));
            }
        }

        public void ClearAll()
        {
            Undo.RecordObject(Data, "Clear Conveyor");
            Data.ConveyorCells.Clear();
            Data.ConveyorEntities.Clear();
            Data.Customers.Clear();
            RebuildCacheAndRepaint();
            OnCellsChanged?.Invoke();
            OnEntitiesChanged?.Invoke();
            OnCustomersChanged?.Invoke();
        }

        public void ClearCustomers()
        {
            Undo.RecordObject(Data, "Clear Customers");
            Data.Customers.Clear();
            RebuildCacheAndRepaint();
            OnCustomersChanged?.Invoke();
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

            if (_pickCallback != null)
            {
                var callback = _pickCallback;
                _pickCallback = null;
                _pickedCell = cell;
                callback(cell);
                MarkDirtyRepaint();
                evt.StopPropagation();
                return;
            }

            if (evt.button == 0 && evt.shiftKey && Mode == CanvasMode.Path)
            {
                RotateAt(cell);
                evt.StopPropagation();
                return;
            }

            if (evt.button == 0 && evt.ctrlKey && Mode == CanvasMode.Path)
            {
                ToggleGateAt(cell);
                evt.StopPropagation();
                return;
            }

            // one-shot, not drag-painted
            if (Mode == CanvasMode.Customer && CustomerMode == CustomerSubMode.SecondOrder)
            {
                if (evt.button == 1) ClearCustomerSecondOrderAt(cell);
                else SetCustomerSecondOrderAt(cell);
                evt.StopPropagation();
                return;
            }

            if (Mode == CanvasMode.Customer && CustomerMode == CustomerSubMode.Hidden)
            {
                if (evt.button == 0) ToggleCustomerHiddenAt(cell);
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
            MarkDirtyRepaint();

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
            if (evt.keyCode != KeyCode.R || !_hoverCell.HasValue) return;

            if (Mode == CanvasMode.Path) RotateAt(_hoverCell.Value);
            else return;

            evt.StopPropagation();
        }

        private int FindCellIndex(Vector2Int coord) => Data.ConveyorCells.FindIndex(c => c.Coord == coord);
        private int FindEntityIndex(Vector2Int coord) => Data.ConveyorEntities.FindIndex(e => e.Coord == coord);
        private int FindCustomerIndex(Vector2Int coord) => Data.Customers.FindIndex(c => c.Coord == coord);

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

        // at most one gate per level
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

        private void ToggleCustomerHiddenAt(Vector2Int cell)
        {
            if (Cells.ContainsKey(cell)) return;

            int idx = FindCustomerIndex(cell);
            Undo.RecordObject(Data, "Toggle Customer Hidden");

            if (idx < 0)
            {
                Data.Customers.Add(new CustomerEntity
                {
                    Coord = cell,
                    Order = new FoodStackData { Type = SelectedFoodType, Amount = SelectedAmount },
                    IsHidden = true
                });
            }
            else
            {
                var entry = Data.Customers[idx];
                entry.IsHidden = !entry.IsHidden;
                Data.Customers[idx] = entry;
            }

            RebuildCacheAndRepaint();
            OnCustomersChanged?.Invoke();
        }

        private void SetCustomerSecondOrderAt(Vector2Int cell)
        {
            int idx = FindCustomerIndex(cell);
            if (idx < 0) return;

            Undo.RecordObject(Data, "Set Customer 2nd Order");
            var entry = Data.Customers[idx];
            entry.HasSecondOrder = true;
            entry.SecondOrder = new FoodStackData { Type = SelectedFoodType, Amount = SelectedAmount };
            Data.Customers[idx] = entry;
            RebuildCacheAndRepaint();
            OnCustomersChanged?.Invoke();
        }

        private void ClearCustomerSecondOrderAt(Vector2Int cell)
        {
            int idx = FindCustomerIndex(cell);
            if (idx < 0) return;

            Undo.RecordObject(Data, "Clear Customer 2nd Order");
            var entry = Data.Customers[idx];
            entry.HasSecondOrder = false;
            entry.SecondOrder = default;
            Data.Customers[idx] = entry;
            RebuildCacheAndRepaint();
            OnCustomersChanged?.Invoke();
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
            else if (Mode == CanvasMode.Entity) // only valid on existing conveyor cells
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
            else // customer mode
            {
                if (Cells.ContainsKey(cell)) return;

                if (_eraseMode)
                {
                    if (FindCustomerIndex(cell) < 0) return;
                    Undo.RecordObject(Data, "Erase Customer");
                    Data.Customers.RemoveAll(c => c.Coord == cell);
                    RebuildCacheAndRepaint();
                    OnCustomersChanged?.Invoke();
                }
                else
                {
                    Undo.RecordObject(Data, "Paint Customer");
                    int idx = FindCustomerIndex(cell);
                    var newOrder = new FoodStackData { Type = SelectedFoodType, Amount = SelectedAmount };

                    if (idx >= 0)
                    {
                        var entry = Data.Customers[idx];
                        entry.Order = newOrder;
                        Data.Customers[idx] = entry;
                    }
                    else
                    {
                        Data.Customers.Add(new CustomerEntity { Coord = cell, Order = newOrder });
                    }
                    RebuildCacheAndRepaint();
                    OnCustomersChanged?.Invoke();
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
                    // corner: two edges, two arrows
                    DrawArrow(painter, center, new Vector2(-1f, 0f), kv.Value.Rotation, CellSize * 0.22f, CellSize * 0.30f);
                    DrawArrow(painter, center, new Vector2(0f, -1f), kv.Value.Rotation, CellSize * 0.22f, CellSize * 0.30f);
                }

                if (Entities.TryGetValue(kv.Key, out var stack))
                    DrawEntity(painter, center, stack);

                if (kv.Value.IsGate)
                    DrawGateMarker(painter, center);
            }

            // customers
            foreach (var kv in Customers)
                DrawCustomer(painter, CellCenterLocal(kv.Key), kv.Value.IsHidden, kv.Value.HasSecondOrder);

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

            if (_pickedCell.HasValue)
            {
                var r = new Rect(_pickedCell.Value.x * CellSize, (GridHeight - 1 - _pickedCell.Value.y) * CellSize, CellSize, CellSize);
                painter.strokeColor = new Color(0.95f, 0.75f, 0.15f, 0.9f);
                painter.lineWidth = 2.5f;
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
            // Y grows downward, invert for CW
            float rad = -rotationDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(
                center.x + local.x * cos - local.y * sin,
                center.y + local.x * sin + local.y * cos);
        }

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

        private void DrawCornerTile(Painter2D painter, Vector2 center, float rotation)
        {
            float h = CellSize * 0.46f;
            float outerR = h;
            float innerR = outerR - CellSize * 0.32f;
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

        private void DrawArrow(Painter2D painter, Vector2 center, Vector2 baseDir, float rotationDeg, float size, float centerOffset = 0f, Color? color = null)
        {
            Vector2 dir = RotatePoint(Vector2.zero, baseDir, rotationDeg);
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 arrowCenter = center + dir * centerOffset;

            painter.fillColor = color ?? ArrowColor;
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

        private void DrawCustomer(Painter2D painter, Vector2 center, bool isHidden, bool hasSecondOrder)
        {
            float half = CellSize * 0.38f;
            var r = new Rect(center.x - half, center.y - half, half * 2, half * 2);

            // hidden: dimmed
            painter.fillColor = isHidden ? new Color(CustomerColor.r, CustomerColor.g, CustomerColor.b, 0.4f) : CustomerColor;
            painter.strokeColor = CustomerOutline;
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(r.position);
            painter.LineTo(new Vector2(r.xMax, r.yMin));
            painter.LineTo(new Vector2(r.xMax, r.yMax));
            painter.LineTo(new Vector2(r.xMin, r.yMax));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();

            if (hasSecondOrder)
            {
                painter.strokeColor = CustomerOutline;
                painter.lineWidth = 2f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(center.x, r.yMin));
                painter.LineTo(new Vector2(center.x, r.yMax));
                painter.Stroke();
            }
        }

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

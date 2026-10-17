using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace RestaurantLoop.Editor.LevelEditor
{
    public enum CanvasMode { Path, Entity, Customer }

    public enum CustomerSubMode { Order, SecondOrder, Hidden }

    // Conveyor grid view: turns pointer/keyboard input into ConveyorLevelEdits calls and
    // renders a cached copy of the draft (rebuilt after every edit).
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
        private static readonly Color HoverOutline = new(1f, 1f, 1f, 0.35f);
        private static readonly Color PickedOutline = new(0.95f, 0.75f, 0.15f, 0.9f);

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
            style.width = 0; // sized by the owner via Resize()
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

        public void ClearAll()
        {
            ConveyorLevelEdits.ClearAll(Data);
            ApplyEdit(cells: true, entities: true, customers: true);
        }

        public void ClearCustomers()
        {
            ConveyorLevelEdits.ClearCustomers(Data);
            ApplyEdit(customers: true);
        }

        private void ApplyEdit(bool cells = false, bool entities = false, bool customers = false)
        {
            RebuildCacheAndRepaint();
            if (cells) OnCellsChanged?.Invoke();
            if (entities) OnEntitiesChanged?.Invoke();
            if (customers) OnCustomersChanged?.Invoke();
        }

        private FoodStackData SelectedStack() => new() { Type = SelectedFoodType, Amount = SelectedAmount };

        #region Input

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

            // customer sub-modes below are single clicks, not drag-painted
            if (Mode == CanvasMode.Customer && CustomerMode == CustomerSubMode.SecondOrder)
            {
                bool changed = evt.button == 1
                    ? ConveyorLevelEdits.ClearCustomerSecondOrder(Data, cell)
                    : ConveyorLevelEdits.SetCustomerSecondOrder(Data, cell, SelectedStack());
                if (changed) ApplyEdit(customers: true);
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
            if (evt.keyCode != KeyCode.R || !_hoverCell.HasValue || Mode != CanvasMode.Path) return;

            RotateAt(_hoverCell.Value);
            evt.StopPropagation();
        }

        #endregion

        #region Tools

        private void RotateAt(Vector2Int cell)
        {
            if (ConveyorLevelEdits.RotateCell(Data, cell)) ApplyEdit(cells: true);
        }

        private void ToggleGateAt(Vector2Int cell)
        {
            if (!ConveyorLevelEdits.ToggleGate(Data, cell, out bool isGate))
            {
                Debug.LogWarning($"[ConveyorGridCanvas] Ctrl+Click at {cell} isn't a conveyor cell yet — paint Straight/Corner there first, then Ctrl+Click to mark it as Gate.");
                return;
            }

            Debug.Log($"[ConveyorGridCanvas] Gate at {cell}: {(isGate ? "ON" : "OFF")}");
            ApplyEdit(cells: true);
        }

        private void ToggleCustomerHiddenAt(Vector2Int cell)
        {
            if (Cells.ContainsKey(cell)) return;

            ConveyorLevelEdits.ToggleCustomerHidden(Data, cell, SelectedStack());
            ApplyEdit(customers: true);
        }

        private void PaintAt(Vector2Int cell)
        {
            switch (Mode)
            {
                case CanvasMode.Path: PaintPathAt(cell); break;
                case CanvasMode.Entity: PaintEntityAt(cell); break;
                default: PaintCustomerAt(cell); break;
            }
        }

        private void PaintPathAt(Vector2Int cell)
        {
            if (_eraseMode)
            {
                if (!Cells.ContainsKey(cell)) return;
                ConveyorLevelEdits.EraseCell(Data, cell);
                ApplyEdit(cells: true, entities: true);
                return;
            }

            if (Cells.TryGetValue(cell, out var existing) && existing.Shape == BrushShape) return;

            ConveyorLevelEdits.PaintCell(Data, cell, BrushShape);
            ApplyEdit(cells: true);
        }

        // food stacks only sit on belt cells
        private void PaintEntityAt(Vector2Int cell)
        {
            if (!Cells.ContainsKey(cell)) return;

            if (_eraseMode)
            {
                if (ConveyorLevelEdits.EraseEntity(Data, cell)) ApplyEdit(entities: true);
                return;
            }

            ConveyorLevelEdits.PaintEntity(Data, cell, SelectedStack());
            ApplyEdit(entities: true);
        }

        // customers only sit off the belt
        private void PaintCustomerAt(Vector2Int cell)
        {
            if (Cells.ContainsKey(cell)) return;

            if (_eraseMode)
            {
                if (ConveyorLevelEdits.EraseCustomer(Data, cell)) ApplyEdit(customers: true);
                return;
            }

            ConveyorLevelEdits.PaintCustomer(Data, cell, SelectedStack());
            ApplyEdit(customers: true);
        }

        #endregion

        #region Drawing

        private void RebuildEntityLabels()
        {
            _entityLabelLayer.Clear();
            foreach (var kv in Entities)
                AddStackLabels(kv.Value, CellCenterLocal(kv.Key), CellSize);

            foreach (var kv in Customers)
            {
                Vector2 center = CellCenterLocal(kv.Key);

                if (kv.Value.HasSecondOrder)
                {
                    // two orders share the cell: left = first, right = second
                    Vector2 offset = new(CellSize * 0.24f, 0f);
                    float halfSize = CellSize * 0.6f;
                    AddStackLabels(kv.Value.Order, center - offset, halfSize);
                    AddStackLabels(kv.Value.SecondOrder, center + offset, halfSize);
                }
                else
                {
                    AddStackLabels(kv.Value.Order, center, CellSize);
                }

                if (kv.Value.IsHidden)
                    _entityLabelLayer.Add(EntityLabelFactory.CreateHiddenBadge(center, CellSize));
            }
        }

        private void AddStackLabels(FoodStackData stack, Vector2 center, float size)
        {
            _entityLabelLayer.Add(EntityLabelFactory.CreateIcon(stack.Type, center, size));
            if (stack.Amount > 0)
                _entityLabelLayer.Add(EntityLabelFactory.CreateAmountBadge(stack.Amount, center, size));
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;

            painter.fillColor = CellBgColor;
            for (int x = 0; x < GridWidth; x++)
                for (int y = 0; y < GridHeight; y++)
                    GridPainter.FillRect(painter, new Rect(x * CellSize, y * CellSize, CellSize, CellSize));

            GridPainter.DrawGridLines(painter, GridWidth, GridHeight, CellSize, GridLineColor);

            foreach (var kv in Cells)
            {
                Vector2 center = CellCenterLocal(kv.Key);
                ConveyorTilePainter.DrawConveyorCell(painter, center, kv.Value, CellSize);

                if (Entities.TryGetValue(kv.Key, out var stack))
                    GridPainter.DrawFoodDisc(painter, center, CellSize * 0.22f, stack.Type);

                if (kv.Value.IsGate)
                    ConveyorTilePainter.DrawGateMarker(painter, center, CellSize);
            }

            foreach (var kv in Customers)
                ConveyorTilePainter.DrawCustomer(painter, CellCenterLocal(kv.Key), kv.Value, CellSize);

            if (_hoverCell.HasValue)
                GridPainter.StrokeRect(painter, CellRect(_hoverCell.Value), HoverOutline, 1.5f);

            if (_pickedCell.HasValue)
                GridPainter.StrokeRect(painter, CellRect(_pickedCell.Value), PickedOutline, 2.5f);
        }

        private Rect CellRect(Vector2Int cell) =>
            new(cell.x * CellSize, (GridHeight - 1 - cell.y) * CellSize, CellSize, CellSize);

        private Vector2 CellCenterLocal(Vector2Int cell) => CellRect(cell).center;

        #endregion
    }
}

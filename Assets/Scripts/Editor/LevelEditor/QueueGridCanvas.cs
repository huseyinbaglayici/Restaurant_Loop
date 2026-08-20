using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor.LevelEditor
{
    /// <summary>
    /// Grid view of the fixed supply lanes that feed food stacks onto the belt — always present,
    /// separate from table demand. Each column is a lane, each row a position within that
    /// lane — row 0 (top, highlighted) is the front, the only slot that's servable/clickable
    /// in-game; lower rows are waiting behind it.
    ///
    /// Left-click/drag stamps the selected dish+amount onto a lane: it appends to the
    /// next open slot, or edits an already-filled slot in place. Right-click/drag erases from
    /// that slot to the back of the lane, so a lane can never end up with a hole in the middle.
    /// Reads/writes <see cref="Data"/> directly (like <see cref="ConveyorGridCanvas"/>) so it
    /// stays correct across Undo/Redo.
    /// </summary>
    public class QueueGridCanvas : VisualElement
    {
        public ConveyorLevelDataSO Data;
        public int Depth = 6;
        public float CellSize = 40f;

        public FoodType SelectedFoodType = FoodType.Hamburger;
        public int SelectedAmount = 1;

        public System.Action OnChanged;

        private static readonly Color GridLineColor = new(1f, 1f, 1f, 0.08f);
        private static readonly Color CellBgColor = new(1f, 1f, 1f, 0.03f);
        private static readonly Color FrontHighlight = new(0.26f, 0.58f, 0.97f, 0.16f);
        private static readonly Color EntityOutline = new(0f, 0f, 0f, 0.55f);

        private bool _isDragging;
        private bool _eraseMode;
        private readonly VisualElement _entityLabelLayer;

        public QueueGridCanvas()
        {
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            style.width = 0;
            style.height = 0;

            _entityLabelLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _entityLabelLayer.style.position = Position.Absolute;
            _entityLabelLayer.style.left = 0;
            _entityLabelLayer.style.top = 0;
            Add(_entityLabelLayer);
        }

        public void Resize()
        {
            style.width = Data.Queues.Count * CellSize;
            style.height = Depth * CellSize;
            RebuildEntityLabels();
            MarkDirtyRepaint();
        }

        /// <summary>Icon + amount badge overlaid on entity circles — color alone can't tell 9 dishes apart.</summary>
        private void RebuildEntityLabels()
        {
            _entityLabelLayer.Clear();
            for (int c = 0; c < Data.Queues.Count; c++)
            {
                var entries = Data.Queues[c].Entries;
                for (int r = 0; r < entries.Count && r < Depth; r++)
                {
                    Vector2 center = new(c * CellSize + CellSize * 0.5f, r * CellSize + CellSize * 0.5f);
                    _entityLabelLayer.Add(EntityLabelFactory.CreateIcon(entries[r].Type, center, CellSize));
                    if (entries[r].Amount > 1)
                        _entityLabelLayer.Add(EntityLabelFactory.CreateAmountBadge(entries[r].Amount, center, CellSize));
                }
            }
        }

        private (int lane, int row) CellAt(Vector2 localPos)
        {
            int lane = Mathf.FloorToInt(localPos.x / CellSize);
            int row = Mathf.FloorToInt(localPos.y / CellSize); // row 0 at top = front, closest to the belt
            return (lane, row);
        }

        private bool InBounds(int lane, int row) => lane >= 0 && lane < Data.Queues.Count && row >= 0 && row < Depth;

        private void OnPointerDown(PointerDownEvent evt)
        {
            var (lane, row) = CellAt(evt.localPosition);
            if (!InBounds(lane, row)) return;

            this.CapturePointer(evt.pointerId);
            _isDragging = true;
            _eraseMode = evt.button == 1;

            PaintAt(lane, row);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging) return;
            var (lane, row) = CellAt(evt.localPosition);
            if (!InBounds(lane, row)) return;

            PaintAt(lane, row);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging) return;
            _isDragging = false;
            this.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void PaintAt(int lane, int row)
        {
            var entries = Data.Queues[lane].Entries;

            if (_eraseMode)
            {
                if (row >= entries.Count) return;
                Undo.RecordObject(Data, "Erase Queue Entries");
                entries.RemoveRange(row, entries.Count - row);
                RebuildEntityLabels();
                OnChanged?.Invoke();
                MarkDirtyRepaint();
                return;
            }

            if (row > entries.Count) return; // can't skip ahead and leave a gap

            Undo.RecordObject(Data, "Paint Queue Entry");
            var stack = new FoodStackData { Type = SelectedFoodType, Amount = SelectedAmount };
            if (row == entries.Count) entries.Add(stack);
            else entries[row] = stack;
            RebuildEntityLabels();
            OnChanged?.Invoke();
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            int lanes = Data?.Queues.Count ?? 0;

            for (int c = 0; c < lanes; c++)
            {
                for (int r = 0; r < Depth; r++)
                {
                    painter.fillColor = r == 0 ? FrontHighlight : CellBgColor;
                    FillRect(painter, new Rect(c * CellSize, r * CellSize, CellSize, CellSize));
                }
            }

            painter.strokeColor = GridLineColor;
            painter.lineWidth = 1f;
            for (int c = 0; c <= lanes; c++)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(c * CellSize, 0));
                painter.LineTo(new Vector2(c * CellSize, Depth * CellSize));
                painter.Stroke();
            }
            for (int r = 0; r <= Depth; r++)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, r * CellSize));
                painter.LineTo(new Vector2(lanes * CellSize, r * CellSize));
                painter.Stroke();
            }

            for (int c = 0; c < lanes; c++)
            {
                var entries = Data.Queues[c].Entries;
                for (int r = 0; r < entries.Count && r < Depth; r++)
                {
                    Vector2 center = new(c * CellSize + CellSize * 0.5f, r * CellSize + CellSize * 0.5f);
                    DrawEntity(painter, center, entries[r]);
                }
            }
        }

        private static void FillRect(Painter2D painter, Rect r)
        {
            painter.BeginPath();
            painter.MoveTo(r.position);
            painter.LineTo(new Vector2(r.xMax, r.yMin));
            painter.LineTo(new Vector2(r.xMax, r.yMax));
            painter.LineTo(new Vector2(r.xMin, r.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private void DrawEntity(Painter2D painter, Vector2 center, FoodStackData stack)
        {
            float radius = CellSize * 0.32f;
            painter.fillColor = FoodTypeUtil.ToColor(stack.Type);
            painter.strokeColor = EntityOutline;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Fill();
            painter.Stroke();
        }
    }
}

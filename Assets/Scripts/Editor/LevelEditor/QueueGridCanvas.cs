using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RestaurantLoop.Editor.LevelEditor
{
    public class QueueGridCanvas : VisualElement
    {
        public ConveyorLevelDataSO Data;
        public int Depth = 6;
        public float CellSize = 40f;

        public FoodType SelectedFoodType = FoodType.Hamburger;
        public int SelectedAmount = 1;
        public bool LinkMode;

        public System.Action OnChanged;

        private static readonly Color GridLineColor = new(1f, 1f, 1f, 0.08f);
        private static readonly Color CellBgColor = new(1f, 1f, 1f, 0.03f);
        private static readonly Color FrontHighlight = new(0.26f, 0.58f, 0.97f, 0.16f);
        private static readonly Color EntityOutline = new(0f, 0f, 0f, 0.55f);
        private static readonly Color LinkPendingColor = new(1f, 0.85f, 0.2f);

        private bool _isDragging;
        private bool _eraseMode;
        private (int lane, int row)? _linkPending;
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
            _linkPending = null;
            RebuildEntityLabels();
            MarkDirtyRepaint();
        }

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
                    if (entries[r].Amount > 0)
                        _entityLabelLayer.Add(EntityLabelFactory.CreateAmountBadge(entries[r].Amount, center, CellSize));
                }
            }
        }

        private (int lane, int row) CellAt(Vector2 localPos)
        {
            int lane = Mathf.FloorToInt(localPos.x / CellSize);
            int row = Mathf.FloorToInt(localPos.y / CellSize); // row 0 = front
            return (lane, row);
        }

        private bool InBounds(int lane, int row) => lane >= 0 && lane < Data.Queues.Count && row >= 0 && row < Depth;

        private void OnPointerDown(PointerDownEvent evt)
        {
            var (lane, row) = CellAt(evt.localPosition);
            if (!InBounds(lane, row)) return;

            if (LinkMode)
            {
                if (evt.button == 1) UnlinkAt(lane, row);
                else LinkClickAt(lane, row);
                evt.StopPropagation();
                return;
            }

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

        // click one entry, then another — same LinkId assigned to both; clicking the same cell twice cancels
        private void LinkClickAt(int lane, int row)
        {
            var entries = Data.Queues[lane].Entries;
            if (row >= entries.Count) return;

            if (!_linkPending.HasValue)
            {
                _linkPending = (lane, row);
                MarkDirtyRepaint();
                return;
            }

            var (srcLane, srcRow) = _linkPending.Value;
            if (srcLane == lane && srcRow == row)
            {
                _linkPending = null;
                MarkDirtyRepaint();
                return;
            }

            // pending source may no longer exist if the queue changed underneath (undo/load/clear)
            if (srcLane < 0 || srcLane >= Data.Queues.Count || srcRow >= Data.Queues[srcLane].Entries.Count)
            {
                _linkPending = (lane, row);
                MarkDirtyRepaint();
                return;
            }

            Undo.RecordObject(Data, "Link Queue Entries");
            var srcEntry = Data.Queues[srcLane].Entries[srcRow];
            var dstEntry = entries[row];
            int id = srcEntry.LinkId != 0 ? srcEntry.LinkId : (dstEntry.LinkId != 0 ? dstEntry.LinkId : NextLinkId());
            srcEntry.LinkId = id;
            dstEntry.LinkId = id;
            Data.Queues[srcLane].Entries[srcRow] = srcEntry;
            entries[row] = dstEntry;

            _linkPending = null;
            RebuildEntityLabels();
            OnChanged?.Invoke();
            MarkDirtyRepaint();
        }

        private void UnlinkAt(int lane, int row)
        {
            var entries = Data.Queues[lane].Entries;
            if (row >= entries.Count || entries[row].LinkId == 0) return;

            Undo.RecordObject(Data, "Unlink Queue Entry");
            var entry = entries[row];
            entry.LinkId = 0;
            entries[row] = entry;

            RebuildEntityLabels();
            OnChanged?.Invoke();
            MarkDirtyRepaint();
        }

        private int NextLinkId()
        {
            int max = 0;
            foreach (var lane in Data.Queues)
                foreach (var entry in lane.Entries)
                    if (entry.LinkId > max) max = entry.LinkId;
            return max + 1;
        }

        public void ClearQueues()
        {
            Undo.RecordObject(Data, "Clear Queues");
            foreach (var lane in Data.Queues) lane.Entries.Clear();
            _linkPending = null;
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

            DrawLinkLines(painter, lanes);

            if (_linkPending.HasValue)
            {
                var (pLane, pRow) = _linkPending.Value;
                Vector2 center = new(pLane * CellSize + CellSize * 0.5f, pRow * CellSize + CellSize * 0.5f);
                painter.strokeColor = LinkPendingColor;
                painter.lineWidth = 2.5f;
                painter.BeginPath();
                painter.Arc(center, CellSize * 0.4f, Angle.Degrees(0f), Angle.Degrees(360f));
                painter.Stroke();
            }
        }

        // one line per linked pair, colored by LinkId so multiple pairs stay visually distinct
        private void DrawLinkLines(Painter2D painter, int lanes)
        {
            var firstSeen = new Dictionary<int, Vector2>();
            for (int c = 0; c < lanes; c++)
            {
                var entries = Data.Queues[c].Entries;
                for (int r = 0; r < entries.Count && r < Depth; r++)
                {
                    int linkId = entries[r].LinkId;
                    if (linkId == 0) continue;

                    Vector2 center = new(c * CellSize + CellSize * 0.5f, r * CellSize + CellSize * 0.5f);
                    if (firstSeen.TryGetValue(linkId, out var start))
                    {
                        painter.strokeColor = LinkColorFor(linkId);
                        painter.lineWidth = 2f;
                        painter.BeginPath();
                        painter.MoveTo(start);
                        painter.LineTo(center);
                        painter.Stroke();
                    }
                    else
                    {
                        firstSeen[linkId] = center;
                    }
                }
            }
        }

        private static Color LinkColorFor(int linkId)
        {
            float hue = (linkId * 0.618034f) % 1f; // golden ratio spread, distinct hues per id
            return Color.HSVToRGB(hue, 0.65f, 1f);
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
            painter.fillColor = ConveyorGridCanvas.GetFoodColor(stack.Type);
            painter.strokeColor = EntityOutline;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Fill();
            painter.Stroke();
        }
    }
}

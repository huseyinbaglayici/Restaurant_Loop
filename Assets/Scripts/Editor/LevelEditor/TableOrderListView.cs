using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor.LevelEditor
{
    public class TableOrderListView : VisualElement
    {
        public readonly List<FoodStackData> Orders;
        public System.Action OnAddRequested;

        private readonly ConveyorLevelDataSO _undoTarget;
        private readonly VisualElement _list;

        public TableOrderListView(Vector2Int coord, List<FoodStackData> orders, ConveyorLevelDataSO undoTarget)
        {
            Orders = orders;
            _undoTarget = undoTarget;
            AddToClassList(Css.Lane);

            var header = new VisualElement();
            header.AddToClassList(Css.LaneHeader);
            header.Add(new Label($"Table ({coord.x}, {coord.y})"));

            var addBtn = new Button(() => OnAddRequested?.Invoke()) { text = "+" };
            addBtn.AddToClassList(Css.Btn);
            addBtn.tooltip = "Add the currently selected dish+amount to the back of this table's queue";
            header.Add(addBtn);

            Add(header);

            _list = new VisualElement();
            _list.AddToClassList(Css.LaneList);
            Add(_list);

            Refresh();
        }

        public void Refresh()
        {
            _list.Clear();
            for (int i = 0; i < Orders.Count; i++)
            {
                int idx = i; // capture
                var entry = Orders[i];

                var chip = new VisualElement();
                chip.AddToClassList(Css.Chip);
                if (idx == 0) chip.AddToClassList(Css.ChipFront);

                var icon = new Label(FoodTypeUtil.ToIcon(entry.Type));
                icon.AddToClassList(Css.ChipIcon);
                chip.Add(icon);

                var label = new Label(entry.Amount > 1 ? $"x{entry.Amount}" : "");
                label.AddToClassList(Css.ChipLabel);
                chip.Add(label);

                var removeBtn = new Button(() =>
                {
                    Undo.RecordObject(_undoTarget, "Remove Table Order");
                    Orders.RemoveAt(idx);
                    Refresh();
                }) { text = "x" };
                removeBtn.AddToClassList(Css.ChipRemove);
                chip.Add(removeBtn);

                _list.Add(chip);
            }

            if (Orders.Count == 0)
            {
                var empty = new Label("No orders yet.");
                empty.AddToClassList(Css.StatusLabel);
                _list.Add(empty);
            }
        }
    }
}
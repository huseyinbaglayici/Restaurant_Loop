using System.Collections.Generic;
using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor.LevelEditor
{
    public class ConveyorGridWindow : EditorWindow
    {
        private const string UssPath = "Assets/Scripts/Editor/LevelEditor/LevelEditor.uss";
        private const string LevelFolder = "Assets/Data/Levels";
        private const int QueueLaneCount = 3;

        private static readonly FoodType[] Palette =
        {
            FoodType.Hamburger, FoodType.Fries, FoodType.Meat, FoodType.Drink, FoodType.Donut,
            FoodType.Sushi, FoodType.Pizza, FoodType.Croissant, FoodType.Kebab
        };

        private ConveyorLevelDataSO _draft;
        private ConveyorGridCanvas _canvas;
        private QueueGridCanvas _queueCanvas;
        private IntegerField _levelIdField, _widthField, _heightField, _queueDepthField, _amountField;
        private Label _cellCountLabel;
        private VisualElement _tablePanel;
        private readonly List<Button> _paletteButtons = new();
        private readonly Dictionary<CanvasMode, Button> _modeButtons = new();
        private readonly Dictionary<ConveyorTileShape, Button> _brushButtons = new();
        private VisualElement _brushRow;
        private VisualElement _paletteRow;

        [MenuItem("RestaurantLoop/Conveyor Grid Tool")]
        public static void ShowWindow()
        {
            var w = GetWindow<ConveyorGridWindow>();
            w.titleContent = new GUIContent("Conveyor Grid");
            w.minSize = new Vector2(460, 620);
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.AddToClassList(Css.Root);

            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uss != null) root.styleSheets.Add(uss);
            else Debug.LogWarning($"[ConveyorGridWindow] Stylesheet not found at {UssPath}");

            _draft = ScriptableObject.CreateInstance<ConveyorLevelDataSO>();
            _draft.LevelId = 1;
            for (int i = 0; i < QueueLaneCount; i++) _draft.Queues.Add(new QueueLaneData());

            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;

            var scrollRoot = new ScrollView(ScrollViewMode.Vertical);
            scrollRoot.style.flexGrow = 1;
            root.Add(scrollRoot);

            var app = new VisualElement();
            app.AddToClassList(Css.AppContainer);
            scrollRoot.Add(app);

            app.Add(BuildLevelBar());
            app.Add(BuildGridSizeBar());
            app.Add(BuildModeBar());

            app.Add(BuildConveyorSection());
            app.Add(BuildTablePanel());
            app.Add(BuildQueueSection());
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo() => RefreshUiFromDraft();

        /// <summary>Syncs every field/canvas to <see cref="_draft"/>'s current state. Call after replacing its contents (New/Load/undo-redo/Resize).</summary>
        private void RefreshUiFromDraft()
        {
            _levelIdField.SetValueWithoutNotify(_draft.LevelId);
            _widthField.SetValueWithoutNotify(_draft.GridWidth);
            _heightField.SetValueWithoutNotify(_draft.GridHeight);

            _canvas.GridWidth = _draft.GridWidth;
            _canvas.GridHeight = _draft.GridHeight;
            _canvas.Resize();
            _canvas.RebuildCacheAndRepaint();
            UpdateCellCountLabel();

            RebuildTablePanel();

            _queueCanvas.Resize();
            _queueCanvas.MarkDirtyRepaint();
        }

        #region Level bar (Save/Load)

        private VisualElement BuildLevelBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList(Css.Topbar);

            _levelIdField = CompactIntField("Level Id", 1);
            bar.Add(_levelIdField);

            var sep = new VisualElement();
            sep.AddToClassList(Css.TopbarSep);
            bar.Add(sep);

            bar.Add(MakeButton("New", NewLevel, Css.Btn));
            bar.Add(MakeButton("Save", SaveLevel, Css.BtnPrimary));
            bar.Add(MakeButton("Load", LoadLevel, Css.Btn));

            var sep2 = new VisualElement();
            sep2.AddToClassList(Css.TopbarSep);
            bar.Add(sep2);

            bar.Add(MakeButton("Build In Scene", () => ConveyorSceneBuilder.Build(_draft), Css.BtnPrimary));
            bar.Add(MakeButton("Settings", LevelEditorSettingsWindow.ShowWindow, Css.Btn));

            return bar;
        }

        private void NewLevel()
        {
            Undo.RecordObject(_draft, "New Level");
            _draft.LevelId = _levelIdField.value;
            _draft.GridWidth = 8;
            _draft.GridHeight = 8;
            _draft.ConveyorCells.Clear();
            _draft.ConveyorEntities.Clear();
            _draft.Tables.Clear();
            _draft.Queues = Enumerable.Range(0, QueueLaneCount).Select(_ => new QueueLaneData()).ToList();

            RefreshUiFromDraft();
        }

        private void SaveLevel()
        {
            AssetFolderUtil.EnsureFolder(LevelFolder);
            _draft.LevelId = _levelIdField.value;
            string path = $"{LevelFolder}/ConveyorLevel_{_draft.LevelId}.asset";

            var asset = AssetDatabase.LoadAssetAtPath<ConveyorLevelDataSO>(path);
            bool isNew = asset == null;
            if (isNew) asset = ScriptableObject.CreateInstance<ConveyorLevelDataSO>();

            asset.LevelId = _draft.LevelId;
            asset.GridWidth = _draft.GridWidth;
            asset.GridHeight = _draft.GridHeight;
            asset.ConveyorCells = new List<ConveyorCellData>(_draft.ConveyorCells);
            asset.ConveyorEntities = new List<ConveyorEntityPlacement>(_draft.ConveyorEntities);
            asset.Tables = _draft.Tables
                .Select(t => new TableData { Coord = t.Coord, Orders = new List<FoodStackData>(t.Orders) })
                .ToList();
            asset.Queues = _draft.Queues.Select(q => new QueueLaneData { Entries = new List<FoodStackData>(q.Entries) })
                .ToList();

            if (ConveyorPathBaker.TryBake(_draft, out var orderedPath, out var bakeError))
            {
                asset.BakedPath.OrderedPath = orderedPath;
                Debug.Log($"[ConveyorGridWindow] Baked path: {orderedPath.Count} cells — {string.Join(" -> ", orderedPath)}");
            }
            else
            {
                asset.BakedPath.OrderedPath.Clear();
                Debug.LogWarning($"[ConveyorGridWindow] Bake failed, saving without a path: {bakeError}");
            }

            if (isNew) AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ConveyorGridWindow] Saved level {asset.LevelId} to {path}");
        }

        private void LoadLevel()
        {
            string path = $"{LevelFolder}/ConveyorLevel_{_levelIdField.value}.asset";
            var loaded = AssetDatabase.LoadAssetAtPath<ConveyorLevelDataSO>(path);
            if (loaded == null)
            {
                Debug.LogWarning($"[ConveyorGridWindow] No level asset found at {path}");
                return;
            }

            Undo.RecordObject(_draft, "Load Level");
            _draft.LevelId = loaded.LevelId;
            _draft.GridWidth = loaded.GridWidth;
            _draft.GridHeight = loaded.GridHeight;
            _draft.ConveyorCells = new List<ConveyorCellData>(loaded.ConveyorCells);
            _draft.ConveyorEntities = new List<ConveyorEntityPlacement>(loaded.ConveyorEntities);
            _draft.Tables = loaded.Tables
                .Select(t => new TableData { Coord = t.Coord, Orders = new List<FoodStackData>(t.Orders) })
                .ToList();
            _draft.Queues = Enumerable.Range(0, QueueLaneCount)
                .Select(i => i < loaded.Queues.Count
                    ? new QueueLaneData { Entries = new List<FoodStackData>(loaded.Queues[i].Entries) }
                    : new QueueLaneData())
                .ToList();

            RefreshUiFromDraft();

            Debug.Log($"[ConveyorGridWindow] Loaded level {_draft.LevelId} from {path}");
        }

        #endregion

        #region Grid size + mode + brush/palette

        private VisualElement BuildGridSizeBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList(Css.Topbar);

            _widthField = CompactIntField("Width", 8);
            _heightField = CompactIntField("Height", 8);
            bar.Add(_widthField);
            bar.Add(_heightField);

            var sep = new VisualElement();
            sep.AddToClassList(Css.TopbarSep);
            bar.Add(sep);

            bar.Add(MakeButton("Resize", ApplySize, Css.BtnPrimary));
            bar.Add(MakeButton("Clear Conveyor", () => _canvas.ClearAll(), Css.BtnDanger));

            return bar;
        }

        private VisualElement BuildModeBar()
        {
            var container = new VisualElement();

            var modeRow = new VisualElement();
            modeRow.AddToClassList(Css.Topbar);
            AddModeButton(modeRow, CanvasMode.Path, "Path");
            AddModeButton(modeRow, CanvasMode.Entity, "Entity");
            AddModeButton(modeRow, CanvasMode.Table, "Table");
            container.Add(modeRow);

            _brushRow = new VisualElement();
            _brushRow.AddToClassList(Css.Topbar);
            AddBrushButton(_brushRow, ConveyorTileShape.Straight, "Straight");
            AddBrushButton(_brushRow, ConveyorTileShape.Corner, "Corner");
            container.Add(_brushRow);

            _paletteRow = new VisualElement();
            _paletteRow.AddToClassList(Css.Topbar);
            _paletteRow.AddToClassList(Css.RowWrap);
            foreach (var food in Palette)
            {
                var captured = food;
                var swatch = new Button(() => SelectFoodType(captured)) { text = FoodTypeUtil.ToIcon(food) };
                swatch.AddToClassList(Css.PaletteSwatch);
                swatch.style.backgroundColor = FoodTypeUtil.ToColor(food);
                swatch.tooltip = food.ToString();
                _paletteButtons.Add(swatch);
                _paletteRow.Add(swatch);
            }

            _amountField = CompactIntField("Amount", 1);
            _amountField.style.marginLeft = 8;
            _amountField.RegisterValueChangedCallback(evt =>
            {
                int clamped = Mathf.Max(1, evt.newValue);
                if (clamped != evt.newValue) _amountField.SetValueWithoutNotify(clamped);
                if (_canvas != null) _canvas.SelectedAmount = clamped;
                if (_queueCanvas != null) _queueCanvas.SelectedAmount = clamped;
            });
            _paletteRow.Add(_amountField);
            container.Add(_paletteRow);

            return container;
        }

        private void AddModeButton(VisualElement parent, CanvasMode mode, string label)
        {
            var btn = new Button(() => SetMode(mode)) { text = label };
            btn.AddToClassList(Css.Tool);
            _modeButtons[mode] = btn;
            parent.Add(btn);
        }

        private void AddBrushButton(VisualElement parent, ConveyorTileShape shape, string label)
        {
            var btn = new Button(() => SelectBrush(shape)) { text = label };
            btn.AddToClassList(Css.Tool);
            _brushButtons[shape] = btn;
            parent.Add(btn);
        }

        private void SetMode(CanvasMode mode)
        {
            _canvas.Mode = mode;
            foreach (var kv in _modeButtons) kv.Value.EnableInClassList(Css.ToolActive, kv.Key == mode);

            // Brush shape only matters in Path mode. The color palette stays visible either way —
            // Entity mode needs it for the conveyor, and Table mode needs it to add orders.
            _brushRow.style.display = mode == CanvasMode.Path ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SelectBrush(ConveyorTileShape shape)
        {
            _canvas.BrushShape = shape;
            foreach (var kv in _brushButtons) kv.Value.EnableInClassList(Css.ToolActive, kv.Key == shape);
        }

        private void SelectFoodType(FoodType food)
        {
            _canvas.SelectedFoodType = food;
            if (_queueCanvas != null) _queueCanvas.SelectedFoodType = food;
            for (int i = 0; i < Palette.Length; i++)
                _paletteButtons[i].EnableInClassList(Css.PaletteSwatchSelected, Palette[i] == food);
        }

        private void ApplySize()
        {
            Undo.RecordObject(_draft, "Resize Grid");
            _draft.GridWidth = Mathf.Max(1, _widthField.value);
            _draft.GridHeight = Mathf.Max(1, _heightField.value);
            _draft.ConveyorCells.RemoveAll(c =>
                c.Coord.x < 0 || c.Coord.x >= _draft.GridWidth || c.Coord.y < 0 || c.Coord.y >= _draft.GridHeight);
            _draft.ConveyorEntities.RemoveAll(e =>
                e.Coord.x < 0 || e.Coord.x >= _draft.GridWidth || e.Coord.y < 0 || e.Coord.y >= _draft.GridHeight);
            _draft.Tables.RemoveAll(t =>
                t.Coord.x < 0 || t.Coord.x >= _draft.GridWidth || t.Coord.y < 0 || t.Coord.y >= _draft.GridHeight);

            RefreshUiFromDraft();
        }

        #endregion

        #region Conveyor section

        private VisualElement BuildConveyorSection()
        {
            var section = MakeSection("Conveyor");

            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.height = 340;

            var canvasHost = new VisualElement();
            canvasHost.AddToClassList(Css.CanvasHost);

            _canvas = new ConveyorGridCanvas
                { GridWidth = _draft.GridWidth, GridHeight = _draft.GridHeight, Data = _draft };
            _canvas.AddToClassList(Css.Canvas);
            _canvas.OnCellsChanged += UpdateCellCountLabel;
            _canvas.OnTableSelected += _ => RebuildTablePanel();
            _canvas.OnTablesChanged += RebuildTablePanel;
            _canvas.Resize();
            _canvas.RebuildCacheAndRepaint();

            canvasHost.Add(_canvas);
            scroll.Add(canvasHost);
            section.Add(scroll);

            _cellCountLabel = new Label();
            _cellCountLabel.AddToClassList(Css.StatusLabel);
            section.Add(_cellCountLabel);

            SetMode(CanvasMode.Path);
            SelectBrush(ConveyorTileShape.Straight);
            SelectFoodType(FoodType.Hamburger);
            UpdateCellCountLabel();

            return section;
        }

        private void UpdateCellCountLabel()
        {
            int straight = _canvas.Cells.Values.Count(c => c.Shape == ConveyorTileShape.Straight);
            int corner = _canvas.Cells.Count - straight;
            _cellCountLabel.text = $"Conveyor: {_canvas.Cells.Count} cells  ({straight} straight, {corner} corner)";
        }

        #endregion

        #region Table order panel

        private VisualElement BuildTablePanel()
        {
            _tablePanel = MakeSection("Table");
            RebuildTablePanel();
            return _tablePanel;
        }

        private void RebuildTablePanel()
        {
            if (_tablePanel == null) return;

            // section header is the first child (added by MakeSection); keep it, rebuild the rest
            while (_tablePanel.childCount > 1) _tablePanel.RemoveAt(1);

            var coord = _canvas.SelectedTable;
            if (!coord.HasValue || !_canvas.Tables.TryGetValue(coord.Value, out var table))
            {
                var hint = new Label("Table mode — click inside the loop to place or select a table.");
                hint.AddToClassList(Css.StatusLabel);
                _tablePanel.Add(hint);
                return;
            }

            var listView = new TableOrderListView(coord.Value, table.Orders, _draft);
            listView.OnAddRequested += () =>
            {
                Undo.RecordObject(_draft, "Add Table Order");
                listView.Orders.Add(new FoodStackData
                    { Type = _canvas.SelectedFoodType, Amount = _canvas.SelectedAmount });
                listView.Refresh();
                _canvas.RebuildCacheAndRepaint(); // front-order icon on the table cell may have changed
            };
            _tablePanel.Add(listView);
        }

        #endregion

        #region Queue section

        private VisualElement BuildQueueSection()
        {
            var section = MakeSection("Queue");

            var sizeBar = new VisualElement();
            sizeBar.AddToClassList(Css.Row);
            _queueDepthField = CompactIntField("Depth", 6);
            sizeBar.Add(_queueDepthField);
            sizeBar.Add(MakeButton("Resize", () =>
            {
                _queueCanvas.Depth = Mathf.Max(1, _queueDepthField.value);
                _queueCanvas.Resize();
            }, Css.BtnPrimary));
            section.Add(sizeBar);

            var canvasHost = new VisualElement();
            canvasHost.AddToClassList(Css.CanvasHost);

            _queueCanvas = new QueueGridCanvas
            {
                Data = _draft,
                Depth = _queueDepthField.value,
                SelectedFoodType = _canvas.SelectedFoodType,
                SelectedAmount = _canvas.SelectedAmount
            };
            _queueCanvas.AddToClassList(Css.Canvas);
            _queueCanvas.Resize();

            canvasHost.Add(_queueCanvas);
            section.Add(canvasHost);

            return section;
        }

        #endregion

        #region Small UI helpers

        private static VisualElement MakeSection(string header)
        {
            var section = new VisualElement();
            section.AddToClassList(Css.Section);
            var h = new Label(header);
            h.AddToClassList(Css.SectionHeader);
            section.Add(h);
            return section;
        }

        private static Button MakeButton(string text, System.Action onClick, string ussClass)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList(Css.Btn);
            if (!string.IsNullOrEmpty(ussClass)) b.AddToClassList(ussClass);
            return b;
        }

        private static IntegerField CompactIntField(string label, int value)
        {
            var f = new IntegerField(label) { value = value };
            f.AddToClassList(Css.Num);
            return f;
        }

        #endregion
    }
}
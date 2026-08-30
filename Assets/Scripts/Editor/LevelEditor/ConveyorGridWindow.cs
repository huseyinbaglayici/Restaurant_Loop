using System.Collections.Generic;
using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor.LevelEditor
{
    public class ConveyorGridWindow : EditorWindow
    {
        private const string UssPath = "Assets/Scripts/Editor/LevelEditor/LevelEditor.uss";
        private const string LevelFolder = "Assets/Data/Levels";
        private const string LevelDatabasePath = "Assets/Resources/LevelDatabase.asset";
        private const int QueueLaneCount = 3;

        private static readonly FoodType[] Palette =
        {
            FoodType.Hamburger, FoodType.Fries, FoodType.Meat, FoodType.Drink, FoodType.Donut,
            FoodType.Sushi, FoodType.Pizza, FoodType.Croissant, FoodType.Kebab
        };

        private ConveyorLevelDataSO _draft;
        private ConveyorGridCanvas _canvas;
        private QueueGridCanvas _queueCanvas;
        [SerializeField] private int _queueDepthValue = 6;
        private IntegerField _levelIdField, _widthField, _heightField, _queueDepthField, _amountField;
        private Toggle _isTutorialField;
        private Button _tutorialStepsButton;
        private EnumField _difficultyField;
        private PopupField<GameObject> _environmentField;
        private Label _cellCountLabel;
        private readonly List<Button> _paletteButtons = new();
        private readonly Dictionary<CanvasMode, Button> _modeButtons = new();
        private readonly Dictionary<ConveyorTileShape, Button> _brushButtons = new();
        private readonly Dictionary<CustomerSubMode, Button> _customerSubModeButtons = new();
        private VisualElement _brushRow;
        private VisualElement _customerSubModeRow;
        private Label _customerSubModeHint;
        private VisualElement _paletteRow;
        private Button _linkModeButton;
        private Label _linkModeHint;

        [MenuItem("RestaurantLoop/Conveyor Grid Tool")]
        public static void ShowWindow()
        {
            var w = GetWindow<ConveyorGridWindow>();
            w.titleContent = new GUIContent("Conveyor Grid");
            w.minSize = new Vector2(460, 620);
        }

        public static bool TryBeginCellPick(ConveyorLevelDataSO forDraft, System.Action<Vector2Int> onPicked)
        {
            var w = GetWindow<ConveyorGridWindow>();
            w.Focus();

            if (w._draft != forDraft)
            {
                Debug.LogWarning("[ConveyorGridWindow] Open level does not match the tutorial step's level — pick cancelled.");
                return false;
            }

            w._canvas.BeginPick(onPicked);
            return true;
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
            app.Add(BuildQueueSection());
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            RefreshUiFromDraft();
            ApplyQueueDepth();
        }

        private void ApplyQueueDepth()
        {
            _queueDepthField.SetValueWithoutNotify(_queueDepthValue);
            _queueCanvas.Depth = _queueDepthValue;
            _queueCanvas.Resize();
        }

        private void RefreshUiFromDraft()
        {
            _levelIdField.SetValueWithoutNotify(_draft.LevelId);
            _difficultyField.SetValueWithoutNotify(_draft.Difficulty);
            _environmentField.SetValueWithoutNotify(
                _environmentField.choices.Contains(_draft.Environment) ? _draft.Environment : null);
            _widthField.SetValueWithoutNotify(_draft.GridWidth);
            _heightField.SetValueWithoutNotify(_draft.GridHeight);
            _isTutorialField.SetValueWithoutNotify(_draft.IsTutorial);
            _tutorialStepsButton.SetEnabled(_draft.IsTutorial);

            _canvas.GridWidth = _draft.GridWidth;
            _canvas.GridHeight = _draft.GridHeight;
            _canvas.Resize();
            _canvas.RebuildCacheAndRepaint();
            UpdateCellCountLabel();

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

            _difficultyField = new EnumField("Difficulty", LevelDifficulty.None);
            _difficultyField.AddToClassList(Css.Num);
            bar.Add(_difficultyField);

            var environmentChoices = (ConveyorSettingsProvider.GetOrCreate().EnvironmentPresets
                ?.Where(p => p != null) ?? Enumerable.Empty<GameObject>()).ToList();
            environmentChoices.Add(null);
            _environmentField = new PopupField<GameObject>("Environment", environmentChoices, 0,
                p => p == null ? "None" : p.name, p => p == null ? "None" : p.name);
            _environmentField.AddToClassList(Css.Num);
            _environmentField.RegisterValueChangedCallback(evt => _draft.Environment = evt.newValue);
            bar.Add(_environmentField);
            _draft.Environment = _environmentField.value;

            _isTutorialField = new Toggle("Tutorial");
            _isTutorialField.AddToClassList(Css.Toggle);
            _isTutorialField.RegisterValueChangedCallback(evt =>
            {
                _draft.IsTutorial = evt.newValue;
                _tutorialStepsButton.SetEnabled(evt.newValue);
            });
            bar.Add(_isTutorialField);

            _tutorialStepsButton = MakeButton("Edit Tutorial Steps...", () => TutorialStepsWindow.Open(_draft), Css.Btn);
            _tutorialStepsButton.SetEnabled(false);
            bar.Add(_tutorialStepsButton);

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
            _draft.Difficulty = LevelDifficulty.None;
            _draft.Environment = ConveyorSettingsProvider.GetOrCreate().EnvironmentPresets?.FirstOrDefault(p => p != null);
            _draft.GridWidth = 8;
            _draft.GridHeight = 8;
            _draft.ConveyorCells.Clear();
            _draft.ConveyorEntities.Clear();
            _draft.Customers.Clear();
            _draft.Queues = Enumerable.Range(0, QueueLaneCount).Select(_ => new QueueLaneData()).ToList();
            _draft.IsTutorial = false;
            _draft.TutorialSteps.Clear();

            RefreshUiFromDraft();
        }

        private void SaveLevel()
        {
            AssetFolderUtil.EnsureFolder(LevelFolder);
            _draft.LevelId = _levelIdField.value;
            _draft.Difficulty = (LevelDifficulty)_difficultyField.value;
            _draft.Environment = _environmentField.value;
            string path = $"{LevelFolder}/ConveyorLevel_{_draft.LevelId}.asset";

            var asset = AssetDatabase.LoadAssetAtPath<ConveyorLevelDataSO>(path);
            bool isNew = asset == null;
            if (isNew) asset = ScriptableObject.CreateInstance<ConveyorLevelDataSO>();

            asset.LevelId = _draft.LevelId;
            asset.Difficulty = _draft.Difficulty;
            asset.Environment = _draft.Environment;
            asset.GridWidth = _draft.GridWidth;
            asset.GridHeight = _draft.GridHeight;
            asset.ConveyorCells = new List<ConveyorCellData>(_draft.ConveyorCells);
            asset.ConveyorEntities = new List<ConveyorEntityPlacement>(_draft.ConveyorEntities);
            asset.Customers = new List<CustomerEntity>(_draft.Customers);
            asset.Queues = _draft.Queues.Select(q => new QueueLaneData { Entries = new List<FoodStackData>(q.Entries) })
                .ToList();
            asset.IsTutorial = _draft.IsTutorial;
            asset.TutorialSteps = _draft.TutorialSteps
                .Select(s => new TutorialStepData
                {
                    Text = s.Text, Trigger = s.Trigger, HoverKind = s.HoverKind,
                    PowerUpButton = s.PowerUpButton, QueueLaneIndex = s.QueueLaneIndex,
                    QueueRowIndex = s.QueueRowIndex, ConveyorCell = s.ConveyorCell
                })
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

            RegisterInDatabase(asset);

            Debug.Log($"[ConveyorGridWindow] Saved level {asset.LevelId} to {path}");
        }

        private static void RegisterInDatabase(ConveyorLevelDataSO asset)
        {
            var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(LevelDatabasePath);
            if (database == null)
            {
                Debug.LogWarning($"[ConveyorGridWindow] No LevelDatabase found at {LevelDatabasePath} — level not registered.");
                return;
            }

            var so = new SerializedObject(database);
            var levels = so.FindProperty("_levels");

            for (int i = 0; i < levels.arraySize; i++)
            {
                var element = levels.GetArrayElementAtIndex(i);
                var existing = element.objectReferenceValue as ConveyorLevelDataSO;

                if (existing == asset) return;

                if (existing != null && existing.LevelId == asset.LevelId)
                {
                    element.objectReferenceValue = asset;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(database);
                    AssetDatabase.SaveAssets();
                    return;
                }
            }

            levels.InsertArrayElementAtIndex(levels.arraySize);
            levels.GetArrayElementAtIndex(levels.arraySize - 1).objectReferenceValue = asset;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
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
            _draft.Difficulty = loaded.Difficulty;
            _draft.Environment = loaded.Environment;
            _draft.GridWidth = loaded.GridWidth;
            _draft.GridHeight = loaded.GridHeight;
            _draft.ConveyorCells = new List<ConveyorCellData>(loaded.ConveyorCells);
            _draft.ConveyorEntities = new List<ConveyorEntityPlacement>(loaded.ConveyorEntities);
            _draft.Customers = new List<CustomerEntity>(loaded.Customers);
            _draft.Queues = Enumerable.Range(0, QueueLaneCount)
                .Select(i => i < loaded.Queues.Count
                    ? new QueueLaneData { Entries = new List<FoodStackData>(loaded.Queues[i].Entries) }
                    : new QueueLaneData())
                .ToList();
            _draft.IsTutorial = loaded.IsTutorial;
            _draft.TutorialSteps = loaded.TutorialSteps
                .Select(s => new TutorialStepData
                {
                    Text = s.Text, Trigger = s.Trigger, HoverKind = s.HoverKind,
                    PowerUpButton = s.PowerUpButton, QueueLaneIndex = s.QueueLaneIndex,
                    QueueRowIndex = s.QueueRowIndex, ConveyorCell = s.ConveyorCell
                })
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
            bar.Add(MakeButton("Clear Customers", () => _canvas.ClearCustomers(), Css.BtnDanger));

            return bar;
        }

        private VisualElement BuildModeBar()
        {
            var container = new VisualElement();

            var modeRow = new VisualElement();
            modeRow.AddToClassList(Css.Topbar);
            AddModeButton(modeRow, CanvasMode.Path, "Path");
            AddModeButton(modeRow, CanvasMode.Entity, "Entity");
            AddModeButton(modeRow, CanvasMode.Customer, "Customer");
            container.Add(modeRow);

            _brushRow = new VisualElement();
            _brushRow.AddToClassList(Css.Topbar);
            AddBrushButton(_brushRow, ConveyorTileShape.Straight, "Straight");
            AddBrushButton(_brushRow, ConveyorTileShape.Corner, "Corner");
            container.Add(_brushRow);

            _customerSubModeRow = new VisualElement();
            _customerSubModeRow.AddToClassList(Css.Topbar);
            AddCustomerSubModeButton(_customerSubModeRow, CustomerSubMode.Order, "Order",
                "Click paints the primary order from the palette below. Right-click erases the whole customer.");
            AddCustomerSubModeButton(_customerSubModeRow, CustomerSubMode.SecondOrder, "2nd Order",
                "Click an existing customer to give it a second dish (from the palette below), servable after the first. Right-click clears it.");
            AddCustomerSubModeButton(_customerSubModeRow, CustomerSubMode.Hidden, "Hidden",
                "Click an existing customer to toggle it Hidden (dimmed) — unservable until a neighboring customer is served.");
            container.Add(_customerSubModeRow);

            _customerSubModeHint = new Label();
            _customerSubModeHint.AddToClassList(Css.StatusLabel);
            container.Add(_customerSubModeHint);

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

        private void AddCustomerSubModeButton(VisualElement parent, CustomerSubMode subMode, string label, string tooltip)
        {
            var btn = new Button(() => SelectCustomerSubMode(subMode)) { text = label, tooltip = tooltip };
            btn.AddToClassList(Css.Tool);
            _customerSubModeButtons[subMode] = btn;
            parent.Add(btn);
        }

        private void SetMode(CanvasMode mode)
        {
            _canvas.Mode = mode;
            foreach (var kv in _modeButtons) kv.Value.EnableInClassList(Css.ToolActive, kv.Key == mode);

            _brushRow.style.display = mode == CanvasMode.Path ? DisplayStyle.Flex : DisplayStyle.None;

            bool isCustomer = mode == CanvasMode.Customer;
            _customerSubModeRow.style.display = isCustomer ? DisplayStyle.Flex : DisplayStyle.None;
            _customerSubModeHint.style.display = isCustomer ? DisplayStyle.Flex : DisplayStyle.None;
            if (isCustomer) SelectCustomerSubMode(_canvas.CustomerMode);
        }

        private void SelectBrush(ConveyorTileShape shape)
        {
            _canvas.BrushShape = shape;
            foreach (var kv in _brushButtons) kv.Value.EnableInClassList(Css.ToolActive, kv.Key == shape);
        }

        private void SelectCustomerSubMode(CustomerSubMode subMode)
        {
            _canvas.CustomerMode = subMode;
            foreach (var kv in _customerSubModeButtons) kv.Value.EnableInClassList(Css.ToolActive, kv.Key == subMode);
            _customerSubModeHint.text = _customerSubModeButtons[subMode].tooltip;
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
            _draft.Customers.RemoveAll(c =>
                c.Coord.x < 0 || c.Coord.x >= _draft.GridWidth || c.Coord.y < 0 || c.Coord.y >= _draft.GridHeight);

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
            _canvas.Resize();
            _canvas.RebuildCacheAndRepaint();

            canvasHost.Add(_canvas);
            scroll.Add(canvasHost);
            section.Add(scroll);

            _cellCountLabel = new Label();
            _cellCountLabel.AddToClassList(Css.StatusLabel);
            section.Add(_cellCountLabel);

            var gateHint = new Label("Ctrl+Left Click: mark Gate cell (belt enter/exit, no piece placed)");
            gateHint.AddToClassList(Css.StatusLabel);
            section.Add(gateHint);

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

        #region Queue section

        private VisualElement BuildQueueSection()
        {
            var section = MakeSection("Queue");

            var sizeBar = new VisualElement();
            sizeBar.AddToClassList(Css.Row);
            _queueDepthField = CompactIntField("Depth", _queueDepthValue);
            sizeBar.Add(_queueDepthField);
            sizeBar.Add(MakeButton("Resize", () =>
            {
                Undo.RecordObject(this, "Resize Queue Depth");
                _queueDepthValue = Mathf.Max(1, _queueDepthField.value);
                ApplyQueueDepth();
            }, Css.BtnPrimary));
            sizeBar.Add(MakeButton("Clear Queue", () => _queueCanvas.ClearQueues(), Css.BtnDanger));
            sizeBar.Add(MakeButton("Generate From Customers", GenerateQueueFromCustomers, Css.BtnPrimary));
            sizeBar.Add(MakeButton("Shuffle Queue", ShuffleQueue, Css.Btn));

            _linkModeButton = new Button(ToggleLinkMode) { text = "Link" };
            _linkModeButton.AddToClassList(Css.Tool);
            _linkModeButton.tooltip = "Click two queue entries to link them. Right-click an entry to unlink it.";
            sizeBar.Add(_linkModeButton);
            section.Add(sizeBar);

            _linkModeHint = new Label();
            _linkModeHint.AddToClassList(Css.StatusLabel);
            section.Add(_linkModeHint);

            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.height = 340;

            var canvasHost = new VisualElement();
            canvasHost.AddToClassList(Css.CanvasHost);

            _queueCanvas = new QueueGridCanvas
            {
                Data = _draft,
                Depth = _queueDepthValue,
                SelectedFoodType = _canvas.SelectedFoodType,
                SelectedAmount = _canvas.SelectedAmount
            };
            _queueCanvas.AddToClassList(Css.Canvas);
            _queueCanvas.Resize();

            canvasHost.Add(_queueCanvas);
            scroll.Add(canvasHost);
            section.Add(scroll);

            return section;
        }

        private void ToggleLinkMode()
        {
            _queueCanvas.LinkMode = !_queueCanvas.LinkMode;
            _linkModeButton.EnableInClassList(Css.ToolActive, _queueCanvas.LinkMode);
            _linkModeHint.text = _queueCanvas.LinkMode
                ? "Click two queue entries to link them. Right-click an entry to unlink it."
                : "";
        }

        // color-total -> 3-5 random chunks -> round-robin across lanes
        private void GenerateQueueFromCustomers()
        {
            Undo.RecordObject(_draft, "Generate Queue From Customers");

            var totals = new Dictionary<FoodType, int>();
            foreach (var customer in _draft.Customers)
            {
                totals[customer.Order.Type] = totals.GetValueOrDefault(customer.Order.Type) + customer.Order.Amount;
                if (customer.HasSecondOrder)
                    totals[customer.SecondOrder.Type] = totals.GetValueOrDefault(customer.SecondOrder.Type) + customer.SecondOrder.Amount;
            }

            foreach (var lane in _draft.Queues) lane.Entries.Clear();

            int laneIndex = 0;
            foreach (var kv in totals)
            {
                int remaining = kv.Value;
                while (remaining > 0)
                {
                    int chunkSize = Random.Range(3, 6);
                    if (remaining <= 5) chunkSize = remaining;
                    else if (remaining - chunkSize < 3) chunkSize = remaining - 3;
                    if (chunkSize < 3) chunkSize = remaining;

                    remaining -= chunkSize;
                    _draft.Queues[laneIndex % _draft.Queues.Count].Entries.Add(new FoodStackData { Type = kv.Key, Amount = chunkSize });
                    laneIndex++;
                }
            }

            RefreshUiFromDraft();
        }

        // flattens every lane (preserving each entry's LinkId), shuffles, then redeals round-robin
        private void ShuffleQueue()
        {
            Undo.RecordObject(_draft, "Shuffle Queue");

            var all = new List<FoodStackData>();
            foreach (var lane in _draft.Queues)
            {
                all.AddRange(lane.Entries);
                lane.Entries.Clear();
            }

            for (int i = 0; i < all.Count; i++)
            {
                int j = Random.Range(i, all.Count);
                (all[i], all[j]) = (all[j], all[i]);
            }

            for (int i = 0; i < all.Count; i++)
                _draft.Queues[i % _draft.Queues.Count].Entries.Add(all[i]);

            RefreshUiFromDraft();
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
using System.Collections.Generic;
using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace RestaurantLoop.Editor.LevelEditor
{
    // Level editor window: builds the UI Toolkit layout, keeps the toolbars in sync with the canvases
    // and forwards actions to ConveyorLevelEdits / QueueLaneEdits / ConveyorLevelAssetIO.
    public class ConveyorGridWindow : EditorWindow
    {
        private const string UssPath = "Assets/Scripts/Editor/LevelEditor/LevelEditor.uss";
        private const float CanvasScrollHeight = 340f;
        private const string LinkModeHelp = "Click two queue entries to link them. Right-click an entry to unlink it.";

        private static readonly FoodType[] Palette =
        {
            FoodType.Hamburger, FoodType.Fries, FoodType.Meat, FoodType.Drink, FoodType.Donut,
            FoodType.Sushi, FoodType.Pizza, FoodType.Croissant, FoodType.Kebab, FoodType.Steak
        };

        private ConveyorLevelDataSO _draft;
        private ConveyorGridCanvas _canvas;
        private QueueGridCanvas _queueCanvas;
        [SerializeField] private int _queueDepthValue = 6;
        private IntegerField _levelIdField, _widthField, _heightField, _queueDepthField, _amountField;
        private Toggle _isTutorialField;
        private Button _tutorialStepsButton;
        private EnumField _difficultyField;
        private EnumField _themeField;
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

            _draft = ConveyorLevelEdits.CreateDraft();

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
            _themeField.SetValueWithoutNotify(_draft.Theme);
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

        #region Level bar (New/Save/Load)

        private VisualElement BuildLevelBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList(Css.Topbar);

            _levelIdField = CompactIntField("Level Id", 1);
            bar.Add(_levelIdField);

            _difficultyField = new EnumField("Difficulty", LevelDifficulty.None);
            _difficultyField.AddToClassList(Css.Num);
            bar.Add(_difficultyField);

            _themeField = new EnumField("Theme", ThemeType.Asian);
            _themeField.AddToClassList(Css.Num);
            _themeField.RegisterValueChangedCallback(evt => _draft.Theme = (ThemeType)evt.newValue);
            bar.Add(_themeField);
            _draft.Theme = (ThemeType)_themeField.value;

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

            bar.Add(MakeTopbarSeparator());

            bar.Add(MakeButton("New", NewLevel, Css.Btn));
            bar.Add(MakeButton("Save", SaveLevel, Css.BtnPrimary));
            bar.Add(MakeButton("Load", LoadLevel, Css.Btn));

            bar.Add(MakeTopbarSeparator());

            bar.Add(MakeButton("Build In Scene", () => ConveyorSceneBuilder.Build(_draft), Css.BtnPrimary));
            bar.Add(MakeButton("Settings", LevelEditorSettingsWindow.ShowWindow, Css.Btn));

            return bar;
        }

        private void NewLevel()
        {
            ConveyorLevelEdits.ResetDraft(_draft, _levelIdField.value);
            RefreshUiFromDraft();
        }

        private void SaveLevel()
        {
            _draft.LevelId = _levelIdField.value;
            _draft.Difficulty = (LevelDifficulty)_difficultyField.value;
            _draft.Theme = (ThemeType)_themeField.value;
            ConveyorLevelAssetIO.Save(_draft);
        }

        private void LoadLevel()
        {
            if (ConveyorLevelAssetIO.TryLoadInto(_draft, _levelIdField.value))
                RefreshUiFromDraft();
        }

        #endregion

        #region Grid size + mode + brush/palette

        private VisualElement BuildGridSizeBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList(Css.Topbar);

            _widthField = CompactIntField("Width", ConveyorLevelEdits.DefaultGridSize);
            _heightField = CompactIntField("Height", ConveyorLevelEdits.DefaultGridSize);
            bar.Add(_widthField);
            bar.Add(_heightField);

            bar.Add(MakeTopbarSeparator());

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
            AddToolButton(modeRow, _modeButtons, CanvasMode.Path, "Path", SetMode);
            AddToolButton(modeRow, _modeButtons, CanvasMode.Entity, "Entity", SetMode);
            AddToolButton(modeRow, _modeButtons, CanvasMode.Customer, "Customer", SetMode);
            container.Add(modeRow);

            _brushRow = new VisualElement();
            _brushRow.AddToClassList(Css.Topbar);
            AddToolButton(_brushRow, _brushButtons, ConveyorTileShape.Straight, "Straight", SelectBrush);
            AddToolButton(_brushRow, _brushButtons, ConveyorTileShape.Corner, "Corner", SelectBrush);
            container.Add(_brushRow);

            _customerSubModeRow = new VisualElement();
            _customerSubModeRow.AddToClassList(Css.Topbar);
            AddToolButton(_customerSubModeRow, _customerSubModeButtons, CustomerSubMode.Order, "Order", SelectCustomerSubMode,
                "Click paints the primary order from the palette below. Right-click erases the whole customer.");
            AddToolButton(_customerSubModeRow, _customerSubModeButtons, CustomerSubMode.SecondOrder, "2nd Order", SelectCustomerSubMode,
                "Click an existing customer to give it a second dish (from the palette below), servable after the first. Right-click clears it.");
            AddToolButton(_customerSubModeRow, _customerSubModeButtons, CustomerSubMode.Hidden, "Hidden", SelectCustomerSubMode,
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
                swatch.style.backgroundColor = GridPainter.GetFoodColor(food);
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

        private static void AddToolButton<T>(VisualElement parent, Dictionary<T, Button> group, T value, string label,
            System.Action<T> onSelect, string tooltip = null)
        {
            var btn = new Button(() => onSelect(value)) { text = label };
            if (tooltip != null) btn.tooltip = tooltip;
            btn.AddToClassList(Css.Tool);
            group[value] = btn;
            parent.Add(btn);
        }

        private static void HighlightActive<T>(Dictionary<T, Button> group, T active)
        {
            foreach (var kv in group)
                kv.Value.EnableInClassList(Css.ToolActive, EqualityComparer<T>.Default.Equals(kv.Key, active));
        }

        private void SetMode(CanvasMode mode)
        {
            _canvas.Mode = mode;
            HighlightActive(_modeButtons, mode);

            _brushRow.style.display = mode == CanvasMode.Path ? DisplayStyle.Flex : DisplayStyle.None;

            bool isCustomer = mode == CanvasMode.Customer;
            _customerSubModeRow.style.display = isCustomer ? DisplayStyle.Flex : DisplayStyle.None;
            _customerSubModeHint.style.display = isCustomer ? DisplayStyle.Flex : DisplayStyle.None;
            if (isCustomer) SelectCustomerSubMode(_canvas.CustomerMode);
        }

        private void SelectBrush(ConveyorTileShape shape)
        {
            _canvas.BrushShape = shape;
            HighlightActive(_brushButtons, shape);
        }

        private void SelectCustomerSubMode(CustomerSubMode subMode)
        {
            _canvas.CustomerMode = subMode;
            HighlightActive(_customerSubModeButtons, subMode);
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
            ConveyorLevelEdits.ResizeGrid(_draft, _widthField.value, _heightField.value);
            RefreshUiFromDraft();
        }

        #endregion

        #region Conveyor section

        private VisualElement BuildConveyorSection()
        {
            var section = MakeSection("Conveyor");

            _canvas = new ConveyorGridCanvas
                { GridWidth = _draft.GridWidth, GridHeight = _draft.GridHeight, Data = _draft };
            _canvas.AddToClassList(Css.Canvas);
            _canvas.OnCellsChanged += UpdateCellCountLabel;
            _canvas.Resize();
            _canvas.RebuildCacheAndRepaint();
            section.Add(MakeCanvasScroll(_canvas));

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
            sizeBar.Add(MakeButton("Resize", ResizeQueueDepth, Css.BtnPrimary));
            sizeBar.Add(MakeButton("Clear Queue", () => _queueCanvas.ClearQueues(), Css.BtnDanger));
            sizeBar.Add(MakeButton("Generate From Customers", GenerateQueueFromCustomers, Css.BtnPrimary));
            sizeBar.Add(MakeButton("Shuffle Queue", ShuffleQueue, Css.Btn));

            _linkModeButton = new Button(ToggleLinkMode) { text = "Link" };
            _linkModeButton.AddToClassList(Css.Tool);
            _linkModeButton.tooltip = LinkModeHelp;
            sizeBar.Add(_linkModeButton);
            section.Add(sizeBar);

            _linkModeHint = new Label();
            _linkModeHint.AddToClassList(Css.StatusLabel);
            section.Add(_linkModeHint);

            _queueCanvas = new QueueGridCanvas
            {
                Data = _draft,
                Depth = _queueDepthValue,
                SelectedFoodType = _canvas.SelectedFoodType,
                SelectedAmount = _canvas.SelectedAmount
            };
            _queueCanvas.AddToClassList(Css.Canvas);
            _queueCanvas.Resize();
            section.Add(MakeCanvasScroll(_queueCanvas));

            return section;
        }

        // queue depth is an editor view setting stored on the window, not on the level
        private void ResizeQueueDepth()
        {
            Undo.RecordObject(this, "Resize Queue Depth");
            _queueDepthValue = Mathf.Max(1, _queueDepthField.value);
            ApplyQueueDepth();
        }

        private void ToggleLinkMode()
        {
            _queueCanvas.LinkMode = !_queueCanvas.LinkMode;
            _linkModeButton.EnableInClassList(Css.ToolActive, _queueCanvas.LinkMode);
            _linkModeHint.text = _queueCanvas.LinkMode ? LinkModeHelp : "";
        }

        private void GenerateQueueFromCustomers()
        {
            QueueLaneEdits.GenerateFromCustomers(_draft);
            RefreshUiFromDraft();
        }

        private void ShuffleQueue()
        {
            QueueLaneEdits.Shuffle(_draft);
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

        private static ScrollView MakeCanvasScroll(VisualElement canvas)
        {
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.height = CanvasScrollHeight;

            var canvasHost = new VisualElement();
            canvasHost.AddToClassList(Css.CanvasHost);
            canvasHost.Add(canvas);
            scroll.Add(canvasHost);
            return scroll;
        }

        private static VisualElement MakeTopbarSeparator()
        {
            var sep = new VisualElement();
            sep.AddToClassList(Css.TopbarSep);
            return sep;
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
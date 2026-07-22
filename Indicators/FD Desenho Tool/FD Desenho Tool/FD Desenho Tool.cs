using cAlgo.API;
using System;
using System.Collections.Generic;

namespace cAlgo
{
    public enum PanelPosition
    {
        SuperiorEsquerda,
        SuperiorDireita,
        InferiorEsquerda,
        InferiorDireita
    }

    public enum InsertMode
    {
        None,
        RectGreen,
        RectRed,
        ArrowUp,
        ArrowDown,
        TrendLine1,
        TrendLine2,
        TrendLine3,
        TrendLine4
    }

    public enum TextVerticalPosition
    {
        Above,
        Below
    }

    public enum TextHorizontalPosition
    {
        Left,
        Center,
        Right
    }

    public enum TrendTextPosition
    {
        LeftAbove,
        LeftBelow,
        RightAbove,
        RightBelow
    }

    [Indicator(IsOverlay = true, AccessRights = AccessRights.None)]
    public class ChartObjectsButtons : Indicator
    {
        private const string PLUGIN_PREFIX = "COB_";
        private const string STATE_HIDE_LTF = PLUGIN_PREFIX + "[STATE=HIDE_LTF]";

        [Parameter("Posição do Painel", DefaultValue = PanelPosition.SuperiorDireita)]
        public PanelPosition Position { get; set; }

        [Parameter("Cor Fundo Botões", DefaultValue = "#FF001B30")]
        public Color ButtonsBackgroundColor { get; set; }

        [Parameter("Cor Padrão Texto Botões", DefaultValue = "#FFFFFFFF")]
        public Color ButtonsForegroundColor { get; set; }

        [Parameter("Largura Botões", DefaultValue = 90)]
        public int ButtonWidth { get; set; }

        [Parameter("Altura Botões", DefaultValue = 22)]
        public int ButtonHeight { get; set; }

        [Parameter("Fonte Textos do Gráfico", DefaultValue = 10)]
        public int TextFontSize { get; set; }

        [Parameter("Lista de Textos (;)", DefaultValue = "1m;5m;15m;h1;h4;D;S;Int.;BOS;liq.;ChCH")]
        public string TextList { get; set; }

        [Parameter("Cor Retângulo Verde", DefaultValue = "#7746AD46")]
        public Color RectGreenColor { get; set; }

        [Parameter("Cor Retângulo Vermelho", DefaultValue = "#86FF6666")]
        public Color RectRedColor { get; set; }

        [Parameter("Cor Seta Cima", DefaultValue = "#FF005727")]
        public Color ArrowUpColor { get; set; }

        [Parameter("Cor Seta Baixo", DefaultValue = "#FFFE0000")]
        public Color ArrowDownColor { get; set; }

        // Linha 1
        [Parameter("Nome Linha 1", DefaultValue = "Principal")]
        public string TL1Name { get; set; }

        [Parameter("Cor Linha 1", DefaultValue = "#FFFFFFFF")]
        public Color TL1Color { get; set; }

        [Parameter("Estilo Linha 1", DefaultValue = LineStyle.Solid)]
        public LineStyle TL1Style { get; set; }

        [Parameter("Texto TL1", DefaultValue = TrendTextPosition.RightAbove)]
        public TrendTextPosition TL1TextPosition { get; set; }

        // Linha 2
        [Parameter("Nome Linha 2", DefaultValue = "Fundo")]
        public string TL2Name { get; set; }

        [Parameter("Cor Linha 2", DefaultValue = "#FFFFFFFF")]
        public Color TL2Color { get; set; }

        [Parameter("Estilo Linha 2", DefaultValue = LineStyle.Lines)]
        public LineStyle TL2Style { get; set; }

        [Parameter("Texto TL2", DefaultValue = TrendTextPosition.RightAbove)]
        public TrendTextPosition TL2TextPosition { get; set; }

        // Linha 3
        [Parameter("Nome Linha 3", DefaultValue = "Interna")]
        public string TL3Name { get; set; }

        [Parameter("Cor Linha 3", DefaultValue = "#FF00FFFF")]
        public Color TL3Color { get; set; }

        [Parameter("Estilo Linha 3", DefaultValue = LineStyle.Solid)]
        public LineStyle TL3Style { get; set; }

        [Parameter("Texto TL3", DefaultValue = TrendTextPosition.RightAbove)]
        public TrendTextPosition TL3TextPosition { get; set; }


        // Linha 4
        [Parameter("Nome Linha 4", DefaultValue = "Liquidez")]
        public string TL4Name { get; set; }

        [Parameter("Cor Linha 4", DefaultValue = "#FFFF00FF")]
        public Color TL4Color { get; set; }

        [Parameter("Estilo Linha 4", DefaultValue = LineStyle.Dots)]
        public LineStyle TL4Style { get; set; }
        
        [Parameter("Espessura Linha", DefaultValue = 1)]
        public int TrendLineThickness { get; set; }

        [Parameter("Texto TL4", DefaultValue = TrendTextPosition.RightAbove)]
        public TrendTextPosition TL4TextPosition { get; set; }

        private InsertMode _currentMode = InsertMode.None;

        private bool _waitingSecondRectClick;
        private int _rectStartBar;
        private double _rectStartPrice;
        private Color _rectColor;
        private ChartRectangle _previewRect;

        private bool _waitingSecondTrendClick;
        private int _trendStartBar;
        private double _trendStartPrice;
        private ChartTrendLine _previewTrend;

         // ✅ NOVA VARIÁVEL
        private Color _activeTrendLineColor;
        private List<string> _textItems;
        //private Button _activeTextButton;

        private StackPanel _rootPanel;
        private StackPanel _buttonsPanel;
        private bool _collapsed;

        private List<string> _selectedTexts = new List<string>();
        private List<Button> _activeTextButtons = new List<Button>();
        
        private LineStyle _activeTrendLineStyle;
        private string _activeTrendLineName;
        private TrendTextPosition _activeTrendTextPosition;

        private bool _waitingClearConfirmation;
        private Button _clearButton;

        //private bool _hideLowerTimeframes;
        private Button _hideLtfButton;
        //private string _lastTimeFrameCode;
        
        protected override void Initialize()
        {
            
            //Print("Initialize");
            Chart.MouseDown += OnChartMouseDown;
            Chart.MouseMove += OnChartMouseMove;
            _textItems = new List<string>(TextList.Split(';'));
            _lastTimeFrameCode = GetCurrentTimeFrameCode();
            CreatePanel();
            foreach (var obj in Chart.Objects)
            {
                if (!obj.Name.StartsWith(PLUGIN_PREFIX))
                    continue;

                obj.IsHidden = false;
            }
            UpdateHideLtfButton();
            ApplyTimeFrameFilter();
        }
        
        private void OnChartMouseDown(ChartMouseEventArgs e)
        {
            int bar = Math.Max(0, (int)e.BarIndex);
            double price = e.YValue;

            switch (_currentMode)
            {
                case InsertMode.RectGreen: HandleRectangleClick(RectGreenColor, bar, price); break;
                case InsertMode.RectRed: HandleRectangleClick(RectRedColor, bar, price); break;

                
                case InsertMode.ArrowUp: 
                    DrawArrow(true, ArrowUpColor, bar, price);
                    _currentMode = InsertMode.None;
                    break;

                case InsertMode.ArrowDown: 
                    DrawArrow(false, ArrowDownColor, bar, price);
                    _currentMode = InsertMode.None;
                    break;

                case InsertMode.TrendLine1:
                case InsertMode.TrendLine2:
                case InsertMode.TrendLine3:
                case InsertMode.TrendLine4:
                    HandleTrendLineClick(bar, price);
                    break;
            }

            
        }

        private void OnChartMouseMove(ChartMouseEventArgs e)
        {
            int bar = Math.Max(0, (int)e.BarIndex);
            double price = e.YValue;

            if (_waitingSecondRectClick)
            {
                if (_previewRect != null)
                    Chart.RemoveObject(_previewRect.Name);

                _previewRect = Chart.DrawRectangle(
                    PLUGIN_PREFIX + "RectPreview",
                    _rectStartBar,
                    _rectStartPrice,
                    bar,
                    price,
                    Color.FromArgb(40, _rectColor.R, _rectColor.G, _rectColor.B));

                _previewRect.IsFilled = true;
                _previewRect.IsInteractive = false;
            }

            if (_waitingSecondTrendClick)
            {
                if (_previewTrend != null)
                    Chart.RemoveObject(_previewTrend.Name);

                _previewTrend = Chart.DrawTrendLine(
                    PLUGIN_PREFIX + "TrendPreview",
                    _trendStartBar,
                    _trendStartPrice,
                    bar,
                    price,
                    _activeTrendLineColor);

                _previewTrend.Thickness = TrendLineThickness;
                _previewTrend.LineStyle = _activeTrendLineStyle;
                _previewTrend.IsInteractive = false;
            }
        }

        private void HandleRectangleClick(Color color, int bar, double price)
        {
            if (!_waitingSecondRectClick)
            {
                _rectStartBar = bar;
                _rectStartPrice = price;
                _rectColor = color;
                _waitingSecondRectClick = true;
            }
            else
            {
                var r = Chart.DrawRectangle(
                    PLUGIN_PREFIX + "Rect_" + Guid.NewGuid(),
                    _rectStartBar,
                    _rectStartPrice,
                    bar,
                    price,
                    Color.FromArgb(40, _rectColor.R, _rectColor.G, _rectColor.B));

                r.IsFilled = true;
                r.IsInteractive = true;

                ResetRectangleState();
            }
        }

        // Método para converter o TF atual em texto para gravar no label do desenho
        private string GetCurrentTimeFrameCode()
        {
            if (TimeFrame == TimeFrame.Minute) return "M1";
            if (TimeFrame == TimeFrame.Minute2) return "M2";
            if (TimeFrame == TimeFrame.Minute3) return "M3";
            if (TimeFrame == TimeFrame.Minute4) return "M4";
            if (TimeFrame == TimeFrame.Minute5) return "M5";
            if (TimeFrame == TimeFrame.Minute10) return "M10";
            if (TimeFrame == TimeFrame.Minute15) return "M15";
            if (TimeFrame == TimeFrame.Minute30) return "M30";
            if (TimeFrame == TimeFrame.Minute45) return "M45";

            if (TimeFrame == TimeFrame.Hour) return "H1";
            if (TimeFrame == TimeFrame.Hour2) return "H2";
            if (TimeFrame == TimeFrame.Hour3) return "H3";
            if (TimeFrame == TimeFrame.Hour4) return "H4";
            if (TimeFrame == TimeFrame.Hour6) return "H6";
            if (TimeFrame == TimeFrame.Hour8) return "H8";
            if (TimeFrame == TimeFrame.Hour12) return "H12";

            if (TimeFrame == TimeFrame.Daily) return "D1";
            if (TimeFrame == TimeFrame.Weekly) return "W1";
            if (TimeFrame == TimeFrame.Monthly) return "MN1";

            return "UNK";
        }

        // Método para converter o TF do label no TF do sistema para facilitar o filtro
        private int GetTimeFrameRank(string tf)
        {
            switch (tf)
            {
                case "M1": return 1;
                case "M2": return 2;
                case "M3": return 3;
                case "M4": return 4;
                case "M5": return 5;
                case "M10": return 10;
                case "M15": return 15;
                case "M30": return 30;
                case "M45": return 45;

                case "H1": return 60;
                case "H2": return 120;
                case "H3": return 180;
                case "H4": return 240;
                case "H6": return 360;
                case "H8": return 480;
                case "H12": return 720;

                case "D1": return 1440;
                case "W1": return 10080;
                case "MN1": return 43200;
            }

            return 0;
        }

        private string GetObjectTimeFrame(string objectName)
        {
            const string key = "[TF=";

            int start = objectName.IndexOf(key);

            if (start < 0)
                return null;

            start += key.Length;

            int end = objectName.IndexOf("]", start);

            if (end < 0)
                return null;

            return objectName.Substring(start, end - start);
        }

        private bool ObjectContainsTag(string objectName, string tag)
        {
            return objectName.Contains($"[TAG={tag}]");
        }

        private string _lastTimeFrameCode;

        
        // Criar um método para verificar mudança de timeframe
        private void CheckTimeFrameChange()
        {
            string current = GetCurrentTimeFrameCode();

            if (current == _lastTimeFrameCode)
                return;

            _lastTimeFrameCode = current;

            ApplyTimeFrameFilter();
        }

        // Verificar se o estado existe
        private bool IsHideLtfEnabled()
        {
            foreach (var obj in Chart.Objects)
            {
                if (obj.Name == STATE_HIDE_LTF)
                    return true;
            }

            return false;
        }
        // Salvar estado
        private void SetHideLtfEnabled(bool enabled)
        {
            bool current = IsHideLtfEnabled();
            if (enabled == current)
                return;
            //Print(enabled);
            if (enabled)
            {
                var state = Chart.DrawText(
                STATE_HIDE_LTF,
                ".",
                Chart.FirstVisibleBarIndex,
                Bars.HighPrices[Chart.FirstVisibleBarIndex],
                Color.Transparent);

            state.IsInteractive = true;
            state.IsHidden = true;
            }
            else
            {
                Chart.RemoveObject(STATE_HIDE_LTF);
            }
        }

        // Atualizar o botão
        private void UpdateHideLtfButton()
        {
            bool enabled = IsHideLtfEnabled();

            if (enabled)
            {
                _hideLtfButton.Text = "Mostrar LTF";
                _hideLtfButton.BackgroundColor = Color.DarkGoldenrod;   // ou DarkRed
            }
            else
            {
                _hideLtfButton.Text = "Ocultar LTF";
                _hideLtfButton.BackgroundColor = ButtonsBackgroundColor;
            }
        }
       // =============================
// 2. ALTERAR HANDLETRENDLINECLICK
// =============================

private void HandleTrendLineClick(int bar, double price)
{
    if (!_waitingSecondTrendClick)
    {
        _trendStartBar = bar;
        _trendStartPrice = price;
        _waitingSecondTrendClick = true;
    }
    else
    {
        double endPrice = _trendStartPrice;

        string labelTag = GetSelectedTextsTag();

        var t = Chart.DrawTrendLine(
            BuildObjectName("TREND"),
            _trendStartBar,
            _trendStartPrice,
            bar,
            endPrice,
            _activeTrendLineColor);

        t.Thickness = TrendLineThickness;
        t.LineStyle = _activeTrendLineStyle;
        t.IsInteractive = true;

        // =============================
        // TEXTO CONCATENADO
        // =============================

        if (_selectedTexts.Count > 0)
        {
            string combinedText = string.Join(" ", _selectedTexts);

            double visibleRange = Chart.TopY - Chart.BottomY;
            double yOffset = visibleRange * 0.02;

            int textBar = bar;
            double textPrice = endPrice;

            switch (_activeTrendTextPosition)
            {
                case TrendTextPosition.LeftAbove:
                    textBar = _trendStartBar;
                    textPrice = endPrice + yOffset;
                    break;

                case TrendTextPosition.LeftBelow:
                    textBar = _trendStartBar;
                    textPrice = endPrice - yOffset;
                    break;

                case TrendTextPosition.RightAbove:
                    textBar = bar;
                    textPrice = endPrice + yOffset;
                    break;

                case TrendTextPosition.RightBelow:
                    textBar = bar;
                    textPrice = endPrice - yOffset;
                    break;
            }

            var txt = Chart.DrawText(
                BuildObjectName("TEXT"),
                combinedText,
                textBar,
                textPrice,
                _activeTrendLineColor);

            txt.FontSize = TextFontSize;
            txt.IsInteractive = true;
        }

        ResetTrendState();

        // =============================
        // RESET VISUAL DOS BOTÕES
        // =============================

        _selectedTexts.Clear();

        foreach (var btn in _activeTextButtons)
            ResetTextButtonVisual(btn);

        _activeTextButtons.Clear();
    }
}

        private void ResetRectangleState()
        {
            if (_previewRect != null)
                Chart.RemoveObject(_previewRect.Name);

            _previewRect = null;
            _waitingSecondRectClick = false;
            _currentMode = InsertMode.None;
        }

        private void ResetTrendState()
        {
            if (_previewTrend != null)
                Chart.RemoveObject(_previewTrend.Name);

            _previewTrend = null;
            _waitingSecondTrendClick = false;
            _currentMode = InsertMode.None;
        }

        private void DrawText(string text, Color color, int bar, double price)
        {
            var t = Chart.DrawText(
                PLUGIN_PREFIX + "Text_" + Guid.NewGuid(),
                text,
                bar,
                price,
                color);

            t.FontSize = TextFontSize;
            t.IsInteractive = true;
        }

        private void DrawArrow(bool up, Color color, int bar, double price)
        {
            string labelTag = GetSelectedTextsTag();
            var a = Chart.DrawIcon(
                BuildObjectName("ARROW"),
                up ? ChartIconType.UpArrow : ChartIconType.DownArrow,
                bar,
                price,
                color);

            a.IsInteractive = true;
        }

        private void ClearAllDrawings()
        {
            var toRemove = new List<string>();

            foreach (var obj in Chart.Objects)
                if (obj.Name.StartsWith(PLUGIN_PREFIX))
                    toRemove.Add(obj.Name);

            foreach (var name in toRemove)
                Chart.RemoveObject(name);

            ResetRectangleState();
            ResetTrendState();
        }

        private void CreatePanel()
        {
            _rootPanel = new StackPanel { Orientation = Orientation.Vertical };
            SetPanelAlignment(_rootPanel);

            var toggle = new Button
            {
                Text = "≡",
                Width = ButtonWidth,
                Height = ButtonHeight,
                BackgroundColor = ButtonsBackgroundColor,
                ForegroundColor = ButtonsForegroundColor,
                Margin = 2
            };
            toggle.Click += _ => ToggleButtons();
            _rootPanel.AddChild(toggle);

            _buttonsPanel = new StackPanel { Orientation = Orientation.Vertical };

            AddDynamicTextButtons();

            AddArrowRow();

            _buttonsPanel.AddChild(CreateButton("RG", Color.Green, () => 
            {
                ResetClearConfirmation();
                _currentMode = InsertMode.RectGreen;
            }));
            
            _buttonsPanel.AddChild(CreateButton("RR", Color.Red, () => 
            {
                ResetClearConfirmation();
                _currentMode = InsertMode.RectRed;
        }));
                        

           _buttonsPanel.AddChild(CreateButton(TL1Name, TL1Color, () =>
            {
                ResetClearConfirmation();
                _activeTrendLineColor = TL1Color;
                _activeTrendLineStyle = TL1Style;
                _activeTrendLineName = TL1Name;
                _activeTrendTextPosition = TL1TextPosition;
                _currentMode = InsertMode.TrendLine1;
            }));

            _buttonsPanel.AddChild(CreateButton(TL2Name, TL2Color, () =>
            {
                ResetClearConfirmation();
                _activeTrendLineColor = TL2Color;
                _activeTrendLineStyle = TL2Style;
                _activeTrendLineName = TL2Name;
                _activeTrendTextPosition = TL2TextPosition;
                _currentMode = InsertMode.TrendLine2;
            }));

            _buttonsPanel.AddChild(CreateButton(TL3Name, TL3Color, () =>
            {
                ResetClearConfirmation();
                _activeTrendLineColor = TL3Color;
                _activeTrendLineStyle = TL3Style;
                _activeTrendLineName = TL3Name;
                _activeTrendTextPosition = TL3TextPosition;
                _currentMode = InsertMode.TrendLine3;
            }));

            _buttonsPanel.AddChild(CreateButton(TL4Name, TL4Color, () =>
            {
                ResetClearConfirmation();
                _activeTrendLineColor = TL4Color;
                _activeTrendLineStyle = TL4Style;
                _activeTrendLineName = TL4Name;
                _activeTrendTextPosition = TL4TextPosition;
                _currentMode = InsertMode.TrendLine4;
            }));

            _clearButton = CreateButton("Limpar", ButtonsForegroundColor, HandleClearButton);

            _buttonsPanel.AddChild(_clearButton);

            _buttonsPanel.AddChild(
                    CreateButton("Limpar Parcial", Color.Orange, ClearPartialDrawings)
             );

            _hideLtfButton = CreateButton("Ocultar LTF", ButtonsForegroundColor, ToggleLowerTimeframes);

            _buttonsPanel.AddChild(_hideLtfButton); 

            _rootPanel.AddChild(_buttonsPanel);
            Chart.AddControl(_rootPanel);

            _collapsed = true;
            _rootPanel.RemoveChild(_buttonsPanel);
        }

        private void HandleClearButton()
        {
            // PRIMEIRO CLIQUE
        if (!_waitingClearConfirmation)
            {
                _waitingClearConfirmation = true;

                _clearButton.Text = "⚠ Confirmar";
                _clearButton.BackgroundColor = Color.DarkRed;

                return;
            }

            // SEGUNDO CLIQUE
            ClearAllDrawings();

            _waitingClearConfirmation = false;

            _clearButton.Text = "Limpar";
            _clearButton.BackgroundColor = ButtonsBackgroundColor;
        }

        private void ToggleButtons()
        {
            _collapsed = !_collapsed;

            if (_collapsed)
                _rootPanel.RemoveChild(_buttonsPanel);
            else
                _rootPanel.AddChild(_buttonsPanel);
        }

        private void AddTextRow(string t1, Color c1, InsertMode m1,
                                string t2, Color c2, InsertMode m2)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };

            int half = ButtonWidth / 2 - 2;

           

            _buttonsPanel.AddChild(row);
        }

        private void AddArrowRow()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };

            int half = ButtonWidth / 2 - 2;

            row.AddChild(CreateSmallButton("↑", ArrowUpColor, () => _currentMode = InsertMode.ArrowUp, half));
            row.AddChild(CreateSmallButton("↓", ArrowDownColor, () => _currentMode = InsertMode.ArrowDown, half));

            _buttonsPanel.AddChild(row);
        }

        private Button CreateButton(string text, Color color, Action action)
        {
            var b = new Button
            {
                Text = text,
                Width = ButtonWidth,
                Height = ButtonHeight,
                BackgroundColor = ButtonsBackgroundColor,
                ForegroundColor = color,
                Margin = 2
            };
            b.Click += _ => action();
            return b;
        }

        private Button CreateSmallButton(string text, Color color, Action action, int width)
        {
            var b = new Button
            {
                Text = text,
                Width = width,
                Height = ButtonHeight,
                FontSize = 9,
                BackgroundColor = ButtonsBackgroundColor,
                ForegroundColor = color,
                Margin = 2
            };
            b.Click += _ => action();
            return b;
        }

        private void SetPanelAlignment(StackPanel panel)
        {
            panel.HorizontalAlignment =
                Position == PanelPosition.SuperiorDireita || Position == PanelPosition.InferiorDireita
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left;

            panel.VerticalAlignment =
                Position == PanelPosition.InferiorDireita || Position == PanelPosition.InferiorEsquerda
                    ? VerticalAlignment.Bottom
                    : VerticalAlignment.Top;
        }

        private void AddDynamicTextButtons()
        {
            int half = ButtonWidth / 2 - 2;

            for (int i = 0; i < _textItems.Count; i += 2)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };

                var text1 = _textItems[i];
                row.AddChild(CreateDynamicTextButton(text1, half));

                if (i + 1 < _textItems.Count)
                {
                    var text2 = _textItems[i + 1];
                    row.AddChild(CreateDynamicTextButton(text2, half));
                }

                _buttonsPanel.AddChild(row);
            }
        }

        private void ResetClearConfirmation()
        {
            if (!_waitingClearConfirmation)
                return;

            _waitingClearConfirmation = false;

            _clearButton.Text = "Limpar";
            _clearButton.BackgroundColor = ButtonsBackgroundColor;
        }

        private Button CreateDynamicTextButton(string text, int width)
    {
        var b = new Button
        {
            Text = text,
            Width = width,
            Height = ButtonHeight,
            FontSize = 9,
            BackgroundColor = ButtonsBackgroundColor,
            ForegroundColor = ButtonsForegroundColor,
            Margin = 2
        };

        b.Click += _ =>
        {
            ResetClearConfirmation();
            // Se já está selecionado → REMOVE (toggle off)
            if (_selectedTexts.Contains(text))
            {
                _selectedTexts.Remove(text);
                _activeTextButtons.Remove(b);
                ResetTextButtonVisual(b);
                return;
            }

            // Adiciona mantendo ORDEM
            _selectedTexts.Add(text);
            _activeTextButtons.Add(b);

            SetActiveTextButtonVisual(b);
        };

        return b;
    }

    // =============================
// 3. NOVO MÉTODO
// =============================

private void ClearPartialDrawings()
{
    if (_selectedTexts.Count == 0)
        return;

    var toRemove = new List<string>();

    foreach (var obj in Chart.Objects)
    {
        if (!obj.Name.StartsWith(PLUGIN_PREFIX))
            continue;

        bool match = true;

        foreach (var tag in _selectedTexts)
        {
            if (!ObjectContainsTag(obj.Name, tag))
            {
                match = false;
                break;
            }
        }

        if (match)
            toRemove.Add(obj.Name);
    }

    foreach (var name in toRemove)
        Chart.RemoveObject(name);

    _selectedTexts.Clear();

    foreach (var btn in _activeTextButtons)
        ResetTextButtonVisual(btn);

    _activeTextButtons.Clear();
}

// Novo ToggleLowerTimeframes()
private void ToggleLowerTimeframes()
{
    bool enabled = !IsHideLtfEnabled();

    SetHideLtfEnabled(enabled);

    ApplyTimeFrameFilter();

    UpdateHideLtfButton();
}


private void ApplyTimeFrameFilter()
{
    int currentRank = GetTimeFrameRank(GetCurrentTimeFrameCode());
    bool hideLower = IsHideLtfEnabled();

    foreach (var obj in Chart.Objects)
    {
        if (!obj.Name.StartsWith(PLUGIN_PREFIX))
            continue;

        string tf = GetObjectTimeFrame(obj.Name);

        if (tf == null)
            continue;

        int objectRank = GetTimeFrameRank(tf);

        obj.IsHidden = IsHideLtfEnabled() && objectRank < currentRank;
    }
}
    // =============================
// 1. NOVO MÉTODO AUXILIAR
// =============================

private string BuildTags()
{
    if (_selectedTexts.Count == 0)
        return string.Empty;

    string tags = string.Empty;

    foreach (var tag in _selectedTexts)
    {
        string clean = tag.Trim()
                          .Replace("[", "")
                          .Replace("]", "");

        tags += $"[TAG={clean}]";
    }

    return tags;
}

private string BuildObjectName(string type)
{
    return $"{PLUGIN_PREFIX}[TF={GetCurrentTimeFrameCode()}][TYPE={type}]{BuildTags()}_{Guid.NewGuid()}";
}


private string GetSelectedTextsTag()
{
    if (_selectedTexts.Count == 0)
        return "NO_LABEL";

    var sanitized = new List<string>();

    foreach (var txt in _selectedTexts)
        sanitized.Add(txt.Replace(" ", ""));

    return "[" + string.Join("][", sanitized) + "]";
}

private void SetActiveTextButtonVisual(Button b)
{
    b.BackgroundColor = Color.FromArgb(150, 255, 255, 255);
}

private void ResetTextButtonVisual(Button b)
{
    b.BackgroundColor = ButtonsBackgroundColor;
}

public override void Calculate(int index) 
    { 
        CheckTimeFrameChange();
        String current = GetCurrentTimeFrameCode();

        if (current != _lastTimeFrameCode)
        {
            _lastTimeFrameCode = current;

            ApplyTimeFrameFilter();
        }
    }
}
}
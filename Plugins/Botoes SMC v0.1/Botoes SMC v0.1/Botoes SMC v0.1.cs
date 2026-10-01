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
        Text1,
        Text2,
        Text3,
        Text4,
        Text5,
        Text6,
        Text7,
        Text8,
        ArrowUp,
        ArrowDown,
        TrendLine,
        TrendLine2,
        TrendLine3
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

    [Indicator(IsOverlay = true, AccessRights = AccessRights.None)]
    public class ChartObjectsButtons : Indicator
    {
        private const string PLUGIN_PREFIX = "COB_";

        [Parameter("Posição do Painel", DefaultValue = PanelPosition.SuperiorDireita)]
        public PanelPosition Position { get; set; }

        [Parameter("Cor Fundo Botões", DefaultValue = "#FF001B30")]
        public Color ButtonsBackgroundColor { get; set; }

        [Parameter("Largura Botões", DefaultValue = 90)]
        public int ButtonWidth { get; set; }

        [Parameter("Altura Botões", DefaultValue = 22)]
        public int ButtonHeight { get; set; }

        [Parameter("Fonte Textos do Gráfico", DefaultValue = 10)]
        public int TextFontSize { get; set; }

        [Parameter("Texto 1", DefaultValue = "1m")] public string Text1 { get; set; }
        [Parameter("Cor Texto 1", DefaultValue = "White")] public Color Text1Color { get; set; }

        [Parameter("Texto 2", DefaultValue = "5m")] public string Text2 { get; set; }
        [Parameter("Cor Texto 2", DefaultValue = "Yellow")] public Color Text2Color { get; set; }

        [Parameter("Texto 3", DefaultValue = "15m")] public string Text3 { get; set; }
        [Parameter("Cor Texto 3", DefaultValue = "Cyan")] public Color Text3Color { get; set; }

        [Parameter("Texto 4", DefaultValue = "h1")] public string Text4 { get; set; }
        [Parameter("Cor Texto 4", DefaultValue = "Orange")] public Color Text4Color { get; set; }

        [Parameter("Texto 5", DefaultValue = "h4")] public string Text5 { get; set; }
        [Parameter("Cor Texto 5", DefaultValue = "Lime")] public Color Text5Color { get; set; }

        [Parameter("Texto 6", DefaultValue = "BOS")] public string Text6 { get; set; }
        [Parameter("Cor Texto 6", DefaultValue = "Aqua")] public Color Text6Color { get; set; }

        [Parameter("Texto 7", DefaultValue = "liq")] public string Text7 { get; set; }
        [Parameter("Cor Texto 7", DefaultValue = "Pink")] public Color Text7Color { get; set; }

        [Parameter("Texto 8", DefaultValue = "ChC")] public string Text8 { get; set; }
        [Parameter("Cor Texto 8", DefaultValue = "Gold")] public Color Text8Color { get; set; }

        [Parameter("Cor Retângulo Verde", DefaultValue = "#7746AD46")]
        public Color RectGreenColor { get; set; }

        [Parameter("Cor Retângulo Vermelho", DefaultValue = "#86FF6666")]
        public Color RectRedColor { get; set; }

        [Parameter("Cor Seta Cima", DefaultValue = "#7746AD46")]
        public Color ArrowUpColor { get; set; }

        [Parameter("Cor Seta Baixo", DefaultValue = "#75FF0000")]
        public Color ArrowDownColor { get; set; }

        [Parameter("Nome TL 1", DefaultValue = "TL1")]
        public string TrendLineName { get; set; }
        
        [Parameter("Cor TL 1", DefaultValue = "#ABCCCCCC")]
        public Color TrendLineColor { get; set; }

        [Parameter("Estilo TL 1", DefaultValue = LineStyle.Solid)]
        public LineStyle   TrendLineStyle { get; set;}

        [Parameter("Nome TL 2", DefaultValue = "TL2")]
        public string TrendLine2Name { get; set; }

        [Parameter("Cor TL 2", DefaultValue = "#FFFFAA00")]
        public Color TrendLine2Color { get; set; }

        [Parameter("Estilo TL 2", DefaultValue = LineStyle.Solid)]
        public LineStyle   TrendLine2Style { get; set;}

        [Parameter("Nome TL 3", DefaultValue = "TL3")]
        public string TrendLine3Name { get; set; }

        [Parameter("Cor TL 3", DefaultValue = "#FFCDEEFD")]
        public Color TrendLine3Color { get; set; }

        [Parameter("Estilo TL 3", DefaultValue = LineStyle.Lines)]
        public LineStyle   TrendLine3Style { get; set;}
        
        [Parameter("Espessura Linha", DefaultValue = 1)]
        public int TrendLineThickness { get; set; }


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
        private LineStyle  _activeTrendLineStyle;
        


        private StackPanel _rootPanel;
        private StackPanel _buttonsPanel;
        private bool _collapsed;

        protected override void Initialize()
        {
            Chart.MouseDown += OnChartMouseDown;
            Chart.MouseMove += OnChartMouseMove;
            CreatePanel();
        }

        private void OnChartMouseDown(ChartMouseEventArgs e)
        {
            int bar = Math.Max(0, (int)e.BarIndex);
            double price = e.YValue;

            switch (_currentMode)
            {
                case InsertMode.RectGreen: HandleRectangleClick(RectGreenColor, bar, price); break;
                case InsertMode.RectRed: HandleRectangleClick(RectRedColor, bar, price); break;

                case InsertMode.Text1: DrawText(Text1, Text1Color, bar, price); break;
                case InsertMode.Text2: DrawText(Text2, Text2Color, bar, price); break;
                case InsertMode.Text3: DrawText(Text3, Text3Color, bar, price); break;
                case InsertMode.Text4: DrawText(Text4, Text4Color, bar, price); break;
                case InsertMode.Text5: DrawText(Text5, Text5Color, bar, price); break;
                case InsertMode.Text6: DrawText(Text6, Text6Color, bar, price); break;
                case InsertMode.Text7: DrawText(Text7, Text7Color, bar, price); break;
                case InsertMode.Text8: DrawText(Text8, Text8Color, bar, price); break;

                case InsertMode.ArrowUp: DrawArrow(true, ArrowUpColor, bar, price); break;
                case InsertMode.ArrowDown: DrawArrow(false, ArrowDownColor, bar, price); break;

                case InsertMode.TrendLine: HandleTrendLineClick(bar, price); break;

                case InsertMode.TrendLine2:
                    HandleTrendLineClick(bar, price);
                    break;
                
                case InsertMode.TrendLine3:
                    HandleTrendLineClick(bar, price);
                    break;
            }

            if (_currentMode != InsertMode.RectGreen &&
                _currentMode != InsertMode.RectRed &&
                _currentMode != InsertMode.TrendLine &&
                _currentMode != InsertMode.TrendLine2 &&
                _currentMode != InsertMode.TrendLine3)
                _currentMode = InsertMode.None;
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
                    TrendLineColor);

                _previewTrend.Thickness = TrendLineThickness;
                _previewTrend.LineStyle = LineStyle.Dots;
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
                var t = Chart.DrawTrendLine(
                    PLUGIN_PREFIX + "Trend_" + Guid.NewGuid(),
                    _trendStartBar,
                    _trendStartPrice,
                    bar,
                    _trendStartPrice,
                    _activeTrendLineColor);

                t.Thickness = TrendLineThickness;
                t.LineStyle = _activeTrendLineStyle;
                t.IsInteractive = true;

                ResetTrendState();
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
            var a = Chart.DrawIcon(
                PLUGIN_PREFIX + "Arrow_" + Guid.NewGuid(),
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
                ForegroundColor = Color.White,
                Margin = 5
            };
            toggle.Click += _ => ToggleButtons();
            _rootPanel.AddChild(toggle);

            _buttonsPanel = new StackPanel { Orientation = Orientation.Vertical };

            AddTextRow(Text1, Text1Color, InsertMode.Text1, Text2, Text2Color, InsertMode.Text2);
            AddTextRow(Text3, Text3Color, InsertMode.Text3, Text4, Text4Color, InsertMode.Text4);
            AddTextRow(Text5, Text5Color, InsertMode.Text5, Text6, Text6Color, InsertMode.Text6);
            AddTextRow(Text7, Text7Color, InsertMode.Text7, Text8, Text8Color, InsertMode.Text8);

            AddArrowRow();

            _buttonsPanel.AddChild(CreateButton("RG", Color.Green, () => _currentMode = InsertMode.RectGreen));
            _buttonsPanel.AddChild(CreateButton("RR", Color.Red, () => _currentMode = InsertMode.RectRed));
                        

            _buttonsPanel.AddChild(CreateButton(TrendLineName, TrendLineColor, () =>
            {
                _currentMode = InsertMode.TrendLine;
                _activeTrendLineColor = TrendLineColor;
                _activeTrendLineStyle = TrendLineStyle;
            }));

            // ✅ NOVO BOTÃO — APENAS AQUI
            _buttonsPanel.AddChild(CreateButton(TrendLine2Name, TrendLine2Color, () =>
            {
                _activeTrendLineColor = TrendLine2Color;
                _currentMode = InsertMode.TrendLine2;
                _activeTrendLineStyle = TrendLine2Style;
            }));

            _buttonsPanel.AddChild(CreateButton(TrendLine3Name, TrendLine3Color, () =>
            {
                _activeTrendLineColor = TrendLine3Color;
                _currentMode = InsertMode.TrendLine3;
                _activeTrendLineStyle = TrendLine3Style;
            }));

            _buttonsPanel.AddChild(CreateButton("Limpar", Color.White, ClearAllDrawings));

            _rootPanel.AddChild(_buttonsPanel);
            Chart.AddControl(_rootPanel);

            _collapsed = true;
            _rootPanel.RemoveChild(_buttonsPanel);
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

            row.AddChild(CreateSmallButton(t1, c1, () => _currentMode = m1, half));
            row.AddChild(CreateSmallButton(t2, c2, () => _currentMode = m2, half));

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
                Margin = 3
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

        public override void Calculate(int index) { }
    }
}
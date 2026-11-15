using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CerGucuSimulasyonu.ViewModels
{
    public class LiveChartsControl : FrameworkElement
    {
        private readonly List<double> _primaryValues = new();
        private readonly List<double> _secondaryValues = new();
        private readonly List<string> _labels = new();

        #region Dependency Properties / Public Properties

        public string Title { get; set; } = "Grafik";
        public string YAxisTitle { get; set; } = "Değer";
        public string XAxisTitle { get; set; } = "Zaman";

        public Brush LineColor { get; set; } = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // Cyan
        public Brush SecondaryLineColor { get; set; } = new SolidColorBrush(Color.FromRgb(250, 204, 21)); // Gold/Yellow
        public bool HasSecondarySeries { get; set; } = false;
        public string PrimaryLegend { get; set; } = "Seri 1";
        public string SecondaryLegend { get; set; } = "Seri 2";

        public Brush BackgroundColor { get; set; } = new SolidColorBrush(Color.FromArgb(240, 16, 18, 30));
        public Brush GridLineColor { get; set; } = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255));
        public Brush TextColor { get; set; } = new SolidColorBrush(Color.FromRgb(180, 185, 210));
        public Brush TitleColor { get; set; } = new SolidColorBrush(Color.FromRgb(240, 240, 250));

        public double MinY { get; set; } = 0;
        public double MaxY { get; set; } = 100;
        public bool AutoScale { get; set; } = true;
        public int MaxPoints { get; set; } = 50;

        #endregion

        #region Data Methods

        public void AddPoint(double primaryVal, double? secondaryVal = null, string? timeLabel = null)
        {
            _primaryValues.Add(primaryVal);

            if (secondaryVal.HasValue)
            {
                HasSecondarySeries = true;
                _secondaryValues.Add(secondaryVal.Value);
            }
            else if (HasSecondarySeries)
            {
                _secondaryValues.Add(0);
            }

            _labels.Add(timeLabel ?? DateTime.Now.ToString("HH:mm:ss"));

            while (_primaryValues.Count > MaxPoints)
            {
                _primaryValues.RemoveAt(0);
                if (HasSecondarySeries && _secondaryValues.Count > 0)
                    _secondaryValues.RemoveAt(0);
                if (_labels.Count > 0)
                    _labels.RemoveAt(0);
            }

            InvalidateVisual();
        }

        public void Clear()
        {
            _primaryValues.Clear();
            _secondaryValues.Clear();
            _labels.Clear();
            InvalidateVisual();
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (ActualWidth <= 60 || ActualHeight <= 50)
                return;

            DrawBackground(dc);
            DrawHeader(dc, out double headerHeight);
            DrawGridAndAxes(dc, headerHeight, out double minY, out double maxY, out double plotLeft, out double plotTop, out double plotWidth, out double plotHeight);
            DrawDataSeries(dc, minY, maxY, plotLeft, plotTop, plotWidth, plotHeight);
        }

        private void DrawBackground(DrawingContext dc)
        {
            var bgRect = new Rect(0, 0, ActualWidth, ActualHeight);
            dc.DrawRoundedRectangle(BackgroundColor, new Pen(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 1), bgRect, 6, 6);
        }

        private void DrawHeader(DrawingContext dc, out double headerHeight)
        {
            headerHeight = 44; // Başlık ve lejant için yeterli alan

            // Başlık - 1. Satır Sol
            DrawText(dc, Title, 11.5, FontWeights.Bold, TitleColor, new Point(12, 6));

            // Lejantlar - 2. Satır Sol (Başlıkla asla çakışmaz)
            double curX = 14;

            // Primary Legend
            dc.DrawRoundedRectangle(LineColor, null, new Rect(curX, 26, 9, 9), 2, 2);
            DrawText(dc, PrimaryLegend, 9, FontWeights.SemiBold, TextColor, new Point(curX + 13, 23));
            var ftPri = MeasureText(PrimaryLegend, 9, FontWeights.SemiBold);
            curX += ftPri.Width + 28;

            // Secondary Legend
            if (HasSecondarySeries)
            {
                dc.DrawRoundedRectangle(SecondaryLineColor, null, new Rect(curX, 26, 9, 9), 2, 2);
                DrawText(dc, SecondaryLegend, 9, FontWeights.SemiBold, TextColor, new Point(curX + 13, 23));
            }
        }

        private void DrawGridAndAxes(DrawingContext dc, double headerHeight, out double minY, out double maxY, out double plotLeft, out double plotTop, out double plotWidth, out double plotHeight)
        {
            plotLeft = 58; // Y ekseni sayıları için yeterli genişlik
            plotTop = headerHeight + 4;
            plotWidth = Math.Max(20, ActualWidth - plotLeft - 16);
            plotHeight = Math.Max(20, ActualHeight - plotTop - 26);

            minY = MinY;
            maxY = MaxY;

            if (AutoScale && _primaryValues.Count > 0)
            {
                double minData = double.MaxValue;
                double maxData = double.MinValue;

                foreach (var v in _primaryValues)
                {
                    if (v < minData) minData = v;
                    if (v > maxData) maxData = v;
                }
                if (HasSecondarySeries)
                {
                    foreach (var v in _secondaryValues)
                    {
                        if (v < minData) minData = v;
                        if (v > maxData) maxData = v;
                    }
                }

                if (minData < maxData)
                {
                    double margin = (maxData - minData) * 0.15;
                    minY = Math.Floor(minData - margin);
                    maxY = Math.Ceiling(maxData + margin);
                }
            }

            if (Math.Abs(maxY - minY) < 0.001)
            {
                maxY = minY + 10;
            }

            double range = maxY - minY;
            var gridPen = new Pen(GridLineColor, 1) { DashStyle = new DashStyle(new double[] { 2, 4 }, 0) };
            var axisPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1);

            // Y Axis Grid & Labels (4 grid lines)
            for (int i = 0; i <= 3; i++)
            {
                double val = minY + (range * i / 3.0);
                double y = (plotTop + plotHeight) - ((val - minY) / range * plotHeight);

                dc.DrawLine(gridPen, new Point(plotLeft, y), new Point(plotLeft + plotWidth, y));
                
                // Y eksen etiketi formatı (negatif ve pozitif sayılar için temiz format)
                string label;
                if (Math.Abs(val) >= 1000)
                {
                    label = $"{(val / 1000.0):0.#}k";
                }
                else
                {
                    label = $"{val:0}";
                }

                DrawText(dc, label, 8.5, FontWeights.Normal, TextColor, new Point(6, y - 6));
            }

            // Çerçeve çizgileri
            dc.DrawLine(axisPen, new Point(plotLeft, plotTop), new Point(plotLeft, plotTop + plotHeight));
            dc.DrawLine(axisPen, new Point(plotLeft, plotTop + plotHeight), new Point(plotLeft + plotWidth, plotTop + plotHeight));

            // X Axis Labels (Time) - Sadece Başlangıç ve Bitiş
            if (_labels.Count > 1)
            {
                int count = _labels.Count;
                int[] sampleIndices = { 0, count - 1 };

                foreach (int idx in sampleIndices)
                {
                    if (idx < 0 || idx >= count) continue;
                    double x = plotLeft + ((double)idx / (count - 1) * plotWidth);
                    double textX = idx == 0 ? plotLeft : Math.Max(plotLeft, plotLeft + plotWidth - 45);
                    DrawText(dc, _labels[idx], 8, FontWeights.Normal, TextColor, new Point(textX, plotTop + plotHeight + 6));
                }
            }
        }

        private void DrawDataSeries(DrawingContext dc, double minY, double maxY, double plotLeft, double plotTop, double plotWidth, double plotHeight)
        {
            if (_primaryValues.Count < 2) return;

            double range = maxY - minY;
            double stepX = plotWidth / (_primaryValues.Count - 1);

            // 1. Primary Series
            var primaryGeo = new StreamGeometry();
            using (var ctx = primaryGeo.Open())
            {
                for (int i = 0; i < _primaryValues.Count; i++)
                {
                    double x = plotLeft + (i * stepX);
                    double y = (plotTop + plotHeight) - ((_primaryValues[i] - minY) / range * plotHeight);
                    y = Math.Max(plotTop, Math.Min(plotTop + plotHeight, y));

                    if (i == 0) ctx.BeginFigure(new Point(x, y), false, false);
                    else ctx.LineTo(new Point(x, y), true, true);
                }
            }
            primaryGeo.Freeze();
            dc.DrawGeometry(null, new Pen(LineColor, 2), primaryGeo);

            // 2. Secondary Series
            if (HasSecondarySeries && _secondaryValues.Count >= 2)
            {
                var secGeo = new StreamGeometry();
                using (var ctx = secGeo.Open())
                {
                    for (int i = 0; i < _secondaryValues.Count && i < _primaryValues.Count; i++)
                    {
                        double x = plotLeft + (i * stepX);
                        double y = (plotTop + plotHeight) - ((_secondaryValues[i] - minY) / range * plotHeight);
                        y = Math.Max(plotTop, Math.Min(plotTop + plotHeight, y));

                        if (i == 0) ctx.BeginFigure(new Point(x, y), false, false);
                        else ctx.LineTo(new Point(x, y), true, true);
                    }
                }
                secGeo.Freeze();
                dc.DrawGeometry(null, new Pen(SecondaryLineColor, 2), secGeo);
            }
        }

        private FormattedText MeasureText(string text, double size, FontWeight weight)
        {
            return new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
                size,
                TextColor,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        private void DrawText(DrawingContext dc, string text, double size, FontWeight weight, Brush color, Point pos, double rotate = 0)
        {
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
                size,
                color,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            if (Math.Abs(rotate) > 0.01)
            {
                dc.PushTransform(new RotateTransform(rotate, pos.X, pos.Y));
                dc.DrawText(ft, pos);
                dc.Pop();
            }
            else
            {
                dc.DrawText(ft, pos);
            }
        }

        #endregion
    }
}

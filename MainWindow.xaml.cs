using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CerGucuSimulasyonu.Models;
using CerGucuSimulasyonu.ViewModels;

namespace CerGucuSimulasyonu
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext;

        private const double H1_Y = 190;
        private const double H2_Y = 320;
        private const double TRAFO_Y = 95; // Trafoları rayların üstüne bağımsız alarak istasyonlarla çakışmasını önle
        private const double MARGIN_L = 100;
        private const double MARGIN_R = 100;
        private const double BASE_LINE_PX = 5400; // 18 km hat için ferah, rahat okunabilir piksel genişliği

        // Interaction state
        private bool _isDragging = false;
        private object? _draggedItem = null;
        private double _dragStartMouseX = 0;
        private double _dragItemOriginalKm = 0;
        private Point? _regionStartPoint = null;
        private Rectangle? _previewRect = null;

        private DispatcherTimer? _uiAnimationTimer;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            Loaded += (s, e) =>
            {
                // Grafikleri ViewModel'e bağla
                ViewModel.PowerChart = ChartSistemGucu;
                ViewModel.VoltageChart = ChartGerilim;
                ViewModel.SelectedTrainSpeedChart = ChartSeciliTren;

                HookEvents();
                Redraw();
                StartUiRenderLoop();
            };
        }

        private void StartUiRenderLoop()
        {
            _uiAnimationTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(100) // 10 FPS canvas tazeleme
            };
            _uiAnimationTimer.Tick += (s, e) =>
            {
                if (ViewModel.IsSimulationRunning)
                {
                    Redraw();
                }
            };
            _uiAnimationTimer.Start();
        }

        private void HookEvents()
        {
            if (ViewModel == null) return;

            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName is nameof(MainViewModel.ZoomLevel) or nameof(MainViewModel.Data) or nameof(MainViewModel.SelectedItem))
                {
                    Redraw();
                }
            };

            ViewModel.Data.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SimulationData.HatUzunlugu))
                {
                    Redraw();
                }
            };

            void HookCollection(INotifyCollectionChanged col)
            {
                col.CollectionChanged += (s, e) => Redraw();
            }

            HookCollection(ViewModel.Data.HatEgimleri);
            HookCollection(ViewModel.Data.HatKurplari);
            HookCollection(ViewModel.Data.TrafoMerkezleri);
            HookCollection(ViewModel.Data.RayParalellemeleri);
            HookCollection(ViewModel.Data.Istasyonlar);
            HookCollection(ViewModel.Data.HizLimitleri);
            HookCollection(ViewModel.Data.KatenerSeksiyonlari);
            HookCollection(ViewModel.Data.KatenerEtaplari);
            HookCollection(ViewModel.Data.SeksiyonAyiricilar);
            HookCollection(ViewModel.Data.Trenler);
        }

        #region Coordinate Helpers

        private double GetTrackPixelLength()
        {
            return BASE_LINE_PX * ViewModel.ZoomLevel;
        }

        private double KmToX(double km)
        {
            double hat = ViewModel.Data.HatUzunlugu > 0 ? ViewModel.Data.HatUzunlugu : 17850;
            return MARGIN_L + (km / hat * GetTrackPixelLength());
        }

        private double XToKm(double x)
        {
            double hat = ViewModel.Data.HatUzunlugu > 0 ? ViewModel.Data.HatUzunlugu : 17850;
            double len = GetTrackPixelLength();
            if (len <= 0) return 0;
            double km = (x - MARGIN_L) / len * hat;
            return Math.Max(0, Math.Min(hat, km));
        }

        private string GetNearestTrack(double y)
        {
            return Math.Abs(y - H1_Y) <= Math.Abs(y - H2_Y) ? "H1" : "H2";
        }

        #endregion

        #region Canvas Rendering

        public void Redraw()
        {
            TrackCanvas.Children.Clear();

            double hat = ViewModel.Data.HatUzunlugu > 0 ? ViewModel.Data.HatUzunlugu : 17850;
            double trackPx = GetTrackPixelLength();
            double totalWidth = MARGIN_L + trackPx + MARGIN_R;

            TrackCanvas.Width = totalWidth;

            double xs = MARGIN_L;
            double xe = MARGIN_L + trackPx;

            DrawRulerAndGrid(xs, xe, hat);
            DrawKatenerSeksiyonlari();
            DrawRegions();
            DrawTrackLines(xs, xe);
            DrawRayParalellemeleri();
            DrawSeksiyonAyiricilar();
            DrawIstasyonlar();
            DrawTrafoMerkezleri();
            DrawTrenler();
        }

        private void DrawRulerAndGrid(double xs, double xe, double hat)
        {
            double step = hat switch
            {
                <= 3000 => 250,
                <= 10000 => 500,
                <= 25000 => 1000,
                _ => 2000
            };

            for (double km = 0; km <= hat; km += step)
            {
                double x = KmToX(km);

                // Grid line
                var gridLine = new Line
                {
                    X1 = x,
                    Y1 = 40,
                    X2 = x,
                    Y2 = 440,
                    Stroke = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 6 }
                };
                TrackCanvas.Children.Add(gridLine);

                // Ruler tick top
                var tick = new Line
                {
                    X1 = x,
                    Y1 = 60,
                    X2 = x,
                    Y2 = 72,
                    Stroke = new SolidColorBrush(Color.FromArgb(140, 200, 220, 255)),
                    StrokeThickness = 1.5
                };
                TrackCanvas.Children.Add(tick);

                // Label
                string label = km >= 1000 ? $"{km / 1000:0.#} km" : $"{km:0} m";
                var txt = new TextBlock
                {
                    Text = label,
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 145, 170)),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                };
                Canvas.SetLeft(txt, x - 18);
                Canvas.SetTop(txt, 42);
                TrackCanvas.Children.Add(txt);
            }
        }

        private void DrawKatenerSeksiyonlari()
        {
            Color[] sectionColors = new[]
            {
                Color.FromRgb(99, 102, 241),  // S1: Indigo
                Color.FromRgb(14, 165, 233),  // S2: Sky
                Color.FromRgb(16, 185, 129),  // S3: Emerald
                Color.FromRgb(245, 158, 11),  // S4: Amber
                Color.FromRgb(168, 85, 247),  // S5: Purple
                Color.FromRgb(244, 63, 94),   // S6: Rose
                Color.FromRgb(6, 182, 212)    // S7: Cyan
            };

            // 1. ÜST ŞERİT: 7 ADET ANA BESLEME SEKSİYONU (Seksiyon 1 .. Seksiyon 7)
            for (int i = 0; i < ViewModel.Data.KatenerSeksiyonlari.Count; i++)
            {
                var sek = ViewModel.Data.KatenerSeksiyonlari[i];
                double x1 = KmToX(sek.BaslangicKm);
                double x2 = KmToX(sek.BitisKm);
                double w = Math.Max(12, x2 - x1);
                bool isSelected = ViewModel.SelectedItem == sek;
                Color col = sectionColors[i % sectionColors.Length];

                var banner = new Border
                {
                    Width = w,
                    Height = 20,
                    Background = new SolidColorBrush(Color.FromArgb(isSelected ? (byte)160 : (byte)45, col.R, col.G, col.B)),
                    BorderBrush = new SolidColorBrush(isSelected ? Colors.White : Color.FromArgb(180, col.R, col.G, col.B)),
                    BorderThickness = new Thickness(isSelected ? 2 : 1),
                    CornerRadius = new CornerRadius(3),
                    Tag = sek,
                    ToolTip = $"⚡ Katener Seksiyonu: {sek.Ad}\nKonum: {sek.BaslangicKm:0}m – {sek.BitisKm:0}m ({sek.Uzunluk:0} m)\nBesleyen Trafolar: {sek.BesleyenTrafolar}\nAnlık Güç: {sek.AnlikToplamGucKw:0.#} kW\nAktif Tren: {sek.AktifTrenSayisi}"
                };

                var sp = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                };

                sp.Children.Add(new TextBlock
                {
                    Text = $"⚡ {sek.Ad} ({sek.BesleyenTrafolar})",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(isSelected ? Colors.White : col),
                    VerticalAlignment = VerticalAlignment.Center
                });

                if (sek.AnlikToplamGucKw > 0)
                {
                    sp.Children.Add(new TextBlock
                    {
                        Text = $" • {sek.AnlikToplamGucKw:0} kW",
                        FontSize = 8.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(250, 204, 21)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 0, 0)
                    });
                }

                banner.Child = sp;
                AttachItemEvents(banner, sek);
                Canvas.SetLeft(banner, x1);
                Canvas.SetTop(banner, 140);
                TrackCanvas.Children.Add(banner);
            }

            // 2. ALT ŞERİT: MONTAJ ETAPLARI (Etap 01, Etap 07, Etap 09 ... Etap 99 - EK-1 Montaj Karnesi)
            for (int i = 0; i < ViewModel.Data.KatenerEtaplari.Count; i++)
            {
                var etap = ViewModel.Data.KatenerEtaplari[i];
                double x1 = KmToX(etap.BaslangicKm);
                double x2 = KmToX(etap.BitisKm);
                double w = Math.Max(10, x2 - x1);
                bool isSelected = ViewModel.SelectedItem == etap;
                Color col = (i % 2 == 0) ? Color.FromRgb(6, 182, 212) : Color.FromRgb(59, 130, 246);

                var etapBox = new Border
                {
                    Width = w,
                    Height = 16,
                    Background = new SolidColorBrush(Color.FromArgb(isSelected ? (byte)180 : (byte)35, col.R, col.G, col.B)),
                    BorderBrush = new SolidColorBrush(isSelected ? Colors.White : Color.FromArgb(120, col.R, col.G, col.B)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Tag = etap,
                    ToolTip = $"🔩 {etap.Ad} ({etap.TunelTipi})\nKonum: {etap.BaslangicKm:0.#}m – {etap.BitisKm:0.#}m (Boy: {etap.Uzunluk:0.#} m)\nOrta Nokta (Ankraj): {etap.OrtaNoktaKm:0.#} m\nGeçiş: {etap.OverlapTipi}\nBesleyen: {etap.BesleyenTrafo}\nAnlık Güç: {etap.AnlikToplamGucKw:0} kW"
                };

                var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(new TextBlock
                {
                    Text = $"{etap.Ad} ({etap.Uzunluk:0}m)",
                    FontSize = 8,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = isSelected ? Brushes.White : new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                etapBox.Child = sp;

                AttachItemEvents(etapBox, etap);
                Canvas.SetLeft(etapBox, x1);
                Canvas.SetTop(etapBox, 164);
                TrackCanvas.Children.Add(etapBox);

                // Orta Nokta (Midpoint Anchor ⚓) İşareti
                if (etap.OrtaNoktaKm > 0)
                {
                    double xOrta = KmToX(etap.OrtaNoktaKm);
                    var pin = new Ellipse
                    {
                        Width = 4,
                        Height = 4,
                        Fill = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
                        Tag = etap,
                        ToolTip = $"⚓ Orta Nokta Ankraj: {etap.Ad} ({etap.OrtaNoktaKm:0.#} m)"
                    };
                    AttachItemEvents(pin, etap);
                    Canvas.SetLeft(pin, xOrta - 2);
                    Canvas.SetTop(pin, 178);
                    TrackCanvas.Children.Add(pin);
                }
            }
        }

        private void DrawSeksiyonAyiricilar()
        {
            for (int i = 0; i < ViewModel.Data.SeksiyonAyiricilar.Count; i++)
            {
                var ayirici = ViewModel.Data.SeksiyonAyiricilar[i];
                double x = KmToX(ayirici.Konum);
                bool isSelected = ViewModel.SelectedItem == ayirici;

                // Hatları kesen izole overlap ayırıcı çizgisi
                var isolatorLine = new Line
                {
                    X1 = x,
                    Y1 = 140,
                    X2 = x,
                    Y2 = 360,
                    Stroke = new SolidColorBrush(isSelected ? Colors.White : Color.FromRgb(167, 139, 250)),
                    StrokeThickness = isSelected ? 2.5 : 1.5,
                    StrokeDashArray = new DoubleCollection { 3, 2 },
                    Tag = ayirici,
                    ToolTip = $"⫽ {ayirici.Ad}\nKonum: {ayirici.Konum:0} m (KM {ayirici.Konum / 1000.0:0.000})\nTip: {ayirici.Tip}\n{ayirici.Aciklama}"
                };
                AttachItemEvents(isolatorLine, ayirici);
                TrackCanvas.Children.Add(isolatorLine);

                // Ayırıcı simge rozeti (⫽)
                var badge = new Border
                {
                    Width = 26,
                    Height = 18,
                    Background = new SolidColorBrush(isSelected ? Color.FromRgb(139, 92, 246) : Color.FromArgb(230, 46, 16, 101)),
                    BorderBrush = new SolidColorBrush(isSelected ? Colors.White : Color.FromRgb(167, 139, 250)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Tag = ayirici,
                    ToolTip = $"⫽ {ayirici.Ad} ({ayirici.Tip}) | KM {ayirici.Konum / 1000.0:0.000}"
                };

                badge.Child = new TextBlock
                {
                    Text = "⫽",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = isSelected ? Brushes.White : new SolidColorBrush(Color.FromRgb(221, 214, 254)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                AttachItemEvents(badge, ayirici);
                Canvas.SetLeft(badge, x - 13);
                Canvas.SetTop(badge, (H1_Y + H2_Y) / 2 - 9);
                TrackCanvas.Children.Add(badge);
            }
        }

        private void DrawRegions()
        {
            // Hız Limitleri (En üst katman)
            for (int i = 0; i < ViewModel.Data.HizLimitleri.Count; i++)
            {
                var hiz = ViewModel.Data.HizLimitleri[i];
                double x1 = KmToX(hiz.Baslangic);
                double x2 = KmToX(hiz.Bitis);
                double w = Math.Max(6, x2 - x1);
                double y = hiz.HatTipi == "H1" ? H1_Y - 50 : H2_Y + 44;

                var line = new Line
                {
                    X1 = x1,
                    Y1 = y + 7,
                    X2 = x2,
                    Y2 = y + 7,
                    Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                    StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    Tag = hiz,
                    ToolTip = $"Hız Limiti: {hiz.Limit:0} km/h ({hiz.Baslangic:0}m - {hiz.Bitis:0}m) [{hiz.HatTipi}]"
                };
                AttachItemEvents(line, hiz);
                TrackCanvas.Children.Add(line);

                var badge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(220, 14, 116, 144)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 1, 4, 1),
                    Child = new TextBlock
                    {
                        Text = $"{hiz.Limit:0} km/h",
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White
                    },
                    Tag = hiz
                };
                AttachItemEvents(badge, hiz);
                Canvas.SetLeft(badge, x1 + (w / 2) - 18);
                Canvas.SetTop(badge, y - 1);
                TrackCanvas.Children.Add(badge);
            }

            // Eğimler (Gradients)
            for (int i = 0; i < ViewModel.Data.HatEgimleri.Count; i++)
            {
                var egim = ViewModel.Data.HatEgimleri[i];
                double x1 = KmToX(egim.Baslangic);
                double x2 = KmToX(egim.Bitis);
                double w = Math.Max(6, x2 - x1);
                double y = egim.HatTipi == "H1" ? H1_Y - 30 : H2_Y + 24;
                Color col = egim.EgimYuzdesi >= 0 ? Color.FromArgb(70, 239, 83, 80) : Color.FromArgb(70, 76, 175, 80);

                var rect = new Rectangle
                {
                    Width = w,
                    Height = 14,
                    Fill = new SolidColorBrush(col),
                    Stroke = new SolidColorBrush(Color.FromArgb(160, col.R, col.G, col.B)),
                    StrokeThickness = 1,
                    RadiusX = 3,
                    RadiusY = 3,
                    Tag = egim,
                    ToolTip = $"Hat Eğimi: %{egim.EgimYuzdesi:0.#} ({egim.Baslangic:0}m - {egim.Bitis:0}m) [{egim.HatTipi}]"
                };
                AttachItemEvents(rect, egim);
                Canvas.SetLeft(rect, x1);
                Canvas.SetTop(rect, y);
                TrackCanvas.Children.Add(rect);

                var txt = new TextBlock
                {
                    Text = $"%{egim.EgimYuzdesi:0.#}",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                    FontWeight = FontWeights.Bold,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(txt, x1 + (w / 2) - 10);
                Canvas.SetTop(txt, y - 1);
                TrackCanvas.Children.Add(txt);
            }

            // Kurplar (Curves)
            for (int i = 0; i < ViewModel.Data.HatKurplari.Count; i++)
            {
                var kurp = ViewModel.Data.HatKurplari[i];
                double x1 = KmToX(kurp.Baslangic);
                double x2 = KmToX(kurp.Bitis);
                double w = Math.Max(6, x2 - x1);
                double y = kurp.HatTipi == "H1" ? H1_Y + 16 : H2_Y - 30;

                var rect = new Rectangle
                {
                    Width = w,
                    Height = 14,
                    Fill = new SolidColorBrush(Color.FromArgb(70, 255, 183, 77)),
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 183, 77)),
                    StrokeThickness = 1,
                    RadiusX = 3,
                    RadiusY = 3,
                    Tag = kurp,
                    ToolTip = $"Kurp R={kurp.Yaricap:0}m ({kurp.Baslangic:0}m - {kurp.Bitis:0}m) [{kurp.HatTipi}]"
                };
                AttachItemEvents(rect, kurp);
                Canvas.SetLeft(rect, x1);
                Canvas.SetTop(rect, y);
                TrackCanvas.Children.Add(rect);

                var txt = new TextBlock
                {
                    Text = $"R={kurp.Yaricap:0}",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 120)),
                    FontWeight = FontWeights.Bold,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(txt, x1 + (w / 2) - 14);
                Canvas.SetTop(txt, y - 1);
                TrackCanvas.Children.Add(txt);
            }
        }

        private void DrawTrackLines(double xs, double xe)
        {
            // H1 Rayı (Üst Hat - Darıca -> Depo)
            var h1Line = new Line
            {
                X1 = xs,
                Y1 = H1_Y,
                X2 = xe,
                Y2 = H1_Y,
                Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                StrokeThickness = 6,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            TrackCanvas.Children.Add(h1Line);

            var lblH1 = new TextBlock
            {
                Text = "H1 (Darıca → Depo)",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
            };
            Canvas.SetLeft(lblH1, xs);
            Canvas.SetTop(lblH1, H1_Y - 24);
            TrackCanvas.Children.Add(lblH1);

            // H2 Rayı (Alt Hat - Depo -> Darıca)
            var h2Line = new Line
            {
                X1 = xs,
                Y1 = H2_Y,
                X2 = xe,
                Y2 = H2_Y,
                Stroke = new SolidColorBrush(Color.FromRgb(251, 146, 60)),
                StrokeThickness = 6,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            TrackCanvas.Children.Add(h2Line);

            var lblH2 = new TextBlock
            {
                Text = "H2 (Depo → Darıca)",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(251, 146, 60))
            };
            Canvas.SetLeft(lblH2, xs);
            Canvas.SetTop(lblH2, H2_Y + 14);
            TrackCanvas.Children.Add(lblH2);
        }

        private void DrawRayParalellemeleri()
        {
            for (int i = 0; i < ViewModel.Data.RayParalellemeleri.Count; i++)
            {
                var rp = ViewModel.Data.RayParalellemeleri[i];
                double x1 = KmToX(rp.H1BaglantiKm);
                double x2 = KmToX(rp.H2BaglantiKm);

                var line = new Line
                {
                    X1 = x1,
                    Y1 = H1_Y,
                    X2 = x2,
                    Y2 = H2_Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(120, 144, 156)),
                    StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    Tag = rp,
                    ToolTip = $"⊥ Ray Paralellemesi P{rp.No}\n" +
                             $"─────────────────────\n" +
                             $"📍 H1 Bağlantı: {rp.H1BaglantiKm:0} m ({rp.H1BaglantiKm / 1000.0:0.00} km)\n" +
                             $"📍 H2 Bağlantı: {rp.H2BaglantiKm:0} m ({rp.H2BaglantiKm / 1000.0:0.00} km)"
                };
                AttachItemEvents(line, rp);
                TrackCanvas.Children.Add(line);

                var pin1 = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromRgb(120, 144, 156)), Tag = rp };
                Canvas.SetLeft(pin1, x1 - 4); Canvas.SetTop(pin1, H1_Y - 4);
                AttachItemEvents(pin1, rp);
                TrackCanvas.Children.Add(pin1);

                var pin2 = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromRgb(120, 144, 156)), Tag = rp };
                Canvas.SetLeft(pin2, x2 - 4); Canvas.SetTop(pin2, H2_Y - 4);
                AttachItemEvents(pin2, rp);
                TrackCanvas.Children.Add(pin2);
            }
        }

        private void DrawIstasyonlar()
        {
            double hat = ViewModel.Data.HatUzunlugu > 0 ? ViewModel.Data.HatUzunlugu : 15350;

            for (int i = 0; i < ViewModel.Data.Istasyonlar.Count; i++)
            {
                var ist = ViewModel.Data.Istasyonlar[i];
                double x = KmToX(ist.H1OrtaNokta);
                double peronW = Math.Max(28, (ist.Uzunluk / hat) * GetTrackPixelLength());
                bool isSelected = ViewModel.SelectedItem == ist;

                // Karşılıklı Peron 1 (H1 Rayı Üzerinde / Yanında)
                var peron1 = new Border
                {
                    Width = peronW,
                    Height = 6,
                    Background = new SolidColorBrush(isSelected ? Color.FromRgb(52, 211, 153) : Color.FromArgb(200, 16, 185, 129)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Tag = ist,
                    ToolTip = $"🚉 {ist.Ad} - H1 Peronu ({ist.Uzunluk:0}m)"
                };
                AttachItemEvents(peron1, ist);
                Canvas.SetLeft(peron1, x - (peronW / 2));
                Canvas.SetTop(peron1, H1_Y - 7);
                TrackCanvas.Children.Add(peron1);

                // Karşılıklı Peron 2 (H2 Rayı Üzerinde / Yanında - Birebir Aynı Metraj ve Genişlik)
                var peron2 = new Border
                {
                    Width = peronW,
                    Height = 6,
                    Background = new SolidColorBrush(isSelected ? Color.FromRgb(52, 211, 153) : Color.FromArgb(200, 16, 185, 129)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Tag = ist,
                    ToolTip = $"🚉 {ist.Ad} - H2 Peronu ({ist.Uzunluk:0}m)"
                };
                AttachItemEvents(peron2, ist);
                Canvas.SetLeft(peron2, x - (peronW / 2));
                Canvas.SetTop(peron2, H2_Y + 7);
                TrackCanvas.Children.Add(peron2);

                // İki Hat Arasındaki Şık ve Kompakt İstasyon Etiket Kapsülü
                var badgeW = 72;
                var badgeH = 28;
                var stationBadge = new Border
                {
                    Width = badgeW,
                    Height = badgeH,
                    Background = new SolidColorBrush(isSelected ? Color.FromArgb(240, 16, 185, 129) : Color.FromArgb(230, 20, 35, 30)),
                    BorderBrush = new SolidColorBrush(isSelected ? Colors.White : Color.FromRgb(52, 211, 153)),
                    BorderThickness = new Thickness(isSelected ? 2 : 1),
                    CornerRadius = new CornerRadius(5),
                    Tag = ist,
                    ToolTip = $"🚉 İstasyon: {ist.Ad} ({ist.KisaAd})\n" +
                             $"─────────────────────\n" +
                             $"📍 Konum: {ist.H1OrtaNokta:0.#} m (KM {ist.H1OrtaNokta / 1000.0:0.000})\n" +
                             $"📏 Peron: {ist.Uzunluk:0} m (H1 & H2 Karşılıklı Eşit)\n" +
                             $"🚃 Yaklaşan Trenler: {ist.YaklasanTrenler}"
                };

                var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                sp.Children.Add(new TextBlock
                {
                    Text = ist.KisaAd,
                    Foreground = new SolidColorBrush(isSelected ? Colors.White : Color.FromRgb(52, 211, 153)),
                    FontWeight = FontWeights.Bold,
                    FontSize = 9.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = badgeW - 6
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"{ist.H1OrtaNokta:0} m",
                    Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                    FontSize = 8,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                stationBadge.Child = sp;

                AttachItemEvents(stationBadge, ist);
                Canvas.SetLeft(stationBadge, x - (badgeW / 2));
                Canvas.SetTop(stationBadge, (H1_Y + H2_Y) / 2 - (badgeH / 2));
                TrackCanvas.Children.Add(stationBadge);
            }
        }

        private void DrawTrafoMerkezleri()
        {
            for (int i = 0; i < ViewModel.Data.TrafoMerkezleri.Count; i++)
            {
                var tm = ViewModel.Data.TrafoMerkezleri[i];
                double x = KmToX(tm.DilasKonumuH1);
                bool isSelected = ViewModel.SelectedItem == tm;

                Color borderColor = tm.YuklenmeYuzdesi > 80 ? Color.FromRgb(239, 68, 68) : Color.FromRgb(192, 132, 252);

                // Trafoyu ray hattına bağlayan besleme çizgisi
                var feederLine = new Line
                {
                    X1 = x,
                    Y1 = TRAFO_Y + 38,
                    X2 = x,
                    Y2 = H1_Y,
                    Stroke = new SolidColorBrush(Color.FromArgb(140, 192, 132, 252)),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };
                TrackCanvas.Children.Add(feederLine);

                var container = new Border
                {
                    Width = 64,
                    Height = 42,
                    Background = new SolidColorBrush(isSelected ? Color.FromArgb(240, 88, 28, 135) : Color.FromArgb(230, 28, 20, 45)),
                    BorderBrush = new SolidColorBrush(isSelected ? Colors.White : borderColor),
                    BorderThickness = new Thickness(isSelected ? 2.5 : 1.5),
                    CornerRadius = new CornerRadius(6),
                    Tag = tm,
                    ToolTip = $"⚡ Trafo Merkezi: {tm.Ad}\n" +
                             $"─────────────────────\n" +
                             $"📍 Konum: {tm.DilasKonumuH1:0} m ({tm.DilasKonumuH1 / 1000.0:0.00} km)\n" +
                             $"🏗 Bağlı İstasyon: {tm.Istasyon}\n" +
                             $"⚡ Anlık Güç: {tm.AnlikGucKw:0.0} kW\n" +
                             $"🔌 Anlık Akım: {tm.AnlikAkimA:0.0} A\n" +
                             $"🔋 Bara Gerilimi: {tm.AnlikGerilimV:0.0} V\n" +
                             $"📊 Yüklenme: %{tm.YuklenmeYuzdesi:0.0}\n" +
                             $"🚃 Beslenen Tren: {tm.BeslenenTrenSayisi}\n" +
                             $"Ω İç Direnç: {tm.Direnc:0.0} mΩ\n" +
                             $"🔗 Fider Kablo: {tm.FiderKabloDirenci:0.0} mΩ\n" +
                             $"⚙ Durum: {tm.Durum}"
                };

                var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                sp.Children.Add(new TextBlock { Text = $"⚡ {tm.Ad}", FontSize = 8.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(192, 132, 252)), HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 60 });
                sp.Children.Add(new TextBlock { Text = $"{tm.AnlikGucKw:0} kW", FontSize = 8, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
                container.Child = sp;

                AttachItemEvents(container, tm);
                Canvas.SetLeft(container, x - 32);
                Canvas.SetTop(container, TRAFO_Y);
                TrackCanvas.Children.Add(container);
            }
        }

        private void DrawTrenler()
        {
            for (int i = 0; i < ViewModel.Data.Trenler.Count; i++)
            {
                var tren = ViewModel.Data.Trenler[i];
                double x = KmToX(tren.Konum);
                double y = tren.HatTipi == "H1" ? H1_Y : H2_Y;
                bool isH1 = tren.HatTipi == "H1";
                bool isSelected = ViewModel.SelectedItem == tren;

                Color bodyColor = isH1 ? Color.FromRgb(250, 204, 21) : Color.FromRgb(251, 146, 60);

                var trainBox = new Border
                {
                    Width = 68,
                    Height = 26,
                    Background = new SolidColorBrush(bodyColor),
                    BorderBrush = isSelected ? Brushes.White : Brushes.Black,
                    BorderThickness = new Thickness(isSelected ? 2.5 : 1),
                    CornerRadius = new CornerRadius(4),
                    Tag = tren,
                    ToolTip = $"🚃 {tren.TrenAdi} (#{tren.Id})\n" +
                             $"─────────────────────\n" +
                             $"📍 Konum: {tren.Konum:0} m ({tren.Konum / 1000.0:0.00} km)\n" +
                             $"🛤 Hat: {tren.HatTipi} | Yön: {(tren.Yon == "ileri" ? "➔ İleri" : "⬅ Geri")}\n" +
                             $"⏱ Hız: {tren.AnlikHiz:0.0} km/h (Hedef: {tren.HedefHiz:0} km/h)\n" +
                             $"⚡ Çekilen Güç: {tren.CekilenGucKw:0.0} kW\n" +
                             $"🔌 Çekilen Akım: {tren.CekilenAkimA:0.0} A\n" +
                             $"🔋 Katener Gerilimi: {tren.KatenerGerilimiV:0.0} V\n" +
                             $"📊 İvme: {tren.Ivme:0.00} m/s²\n" +
                             $"👥 Yolcu: {tren.YolcuSayisi}\n" +
                             $"🏁 Sonraki İstasyon: {tren.SonrakiIstasyon} ({tren.SonrakiIstasyonMesafe:0} m)\n" +
                             $"🔗 En Yakın Trafo: {tren.EnYakinTrafo}\n" +
                             $"⚙ Durum: {tren.Durum}"
                };

                string arrow = tren.Yon == "ileri" ? "➔" : "⬅";
                var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(new TextBlock { Text = $"{tren.TrenAdi} ", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = Brushes.Black });
                sp.Children.Add(new TextBlock { Text = arrow, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.Black });
                trainBox.Child = sp;

                AttachItemEvents(trainBox, tren);
                Canvas.SetLeft(trainBox, x - 34);
                Canvas.SetTop(trainBox, isH1 ? y - 30 : y + 4);
                TrackCanvas.Children.Add(trainBox);
            }
        }

        #endregion

        #region Mouse & Interaction Events

        private void AttachItemEvents(FrameworkElement element, object itemData)
        {
            element.Cursor = Cursors.Hand;

            element.MouseLeftButtonDown += (s, e) =>
            {
                ViewModel.SelectedItem = itemData;

                if (ViewModel.ActiveToolMode == "SELECT")
                {
                    _isDragging = true;
                    _draggedItem = itemData;
                    _dragStartMouseX = e.GetPosition(TrackCanvas).X;
                    _dragItemOriginalKm = GetItemKm(itemData);

                    // TrackCanvas üzerinde Capture yap (Element üzerinde değil)
                    TrackCanvas.CaptureMouse();

                    string info = itemData switch
                    {
                        TrenKonumu tr => $"🚃 {tr.TrenAdi} seçildi | Hız: {tr.AnlikHiz:0} km/h, Güç: {tr.CekilenGucKw:0} kW, Gerilim: {tr.KatenerGerilimiV:0} V",
                        TrafoMerkezi tm => $"⚡ Trafo {tm.Ad} seçildi | Çekilen Güç: {tm.AnlikGucKw:0} kW (%{tm.YuklenmeYuzdesi:0} Yük)",
                        Istasyon ist => $"🚉 İstasyon {ist.Ad} seçildi | Konum: {ist.H1OrtaNokta:0} m",
                        KatenerSeksiyonu sek => $"⚡ {sek.Ad} seçildi | Aralık: {sek.KonumAraligi}, Güç: {sek.AnlikToplamGucKw:0} kW, Aktif Tren: {sek.AktifTrenSayisi}",
                        KatenerEtap et => $"🔩 {et.Ad} seçildi | Aralık: {et.KonumAraligi}, Boy: {et.Uzunluk:0.#} m, Orta Nokta: {et.OrtaNoktaKm:0} m, Güç: {et.AnlikToplamGucKw:0} kW",
                        SeksiyonAyirici ay => $"⫽ {ay.Ad} seçildi | Konum: {ay.Konum:0} m ({ay.Tip})",
                        RayParalellemesi rp => $"⊥ Ray Paralellemesi P{rp.No} seçildi | Konum: {rp.H1BaglantiKm:0} m",
                        HatEgimi eg => $"📐 Eğim Bölgesi seçildi | %{eg.EgimYuzdesi:0.#} ({eg.Baslangic:0}m - {eg.Bitis:0}m)",
                        HatKurbu kr => $"↩ Kurp Bölgesi seçildi | R={kr.Yaricap:0}m ({kr.Baslangic:0}m - {kr.Bitis:0}m)",
                        _ => $"Seçili: {itemData.GetType().Name}"
                    };
                    ViewModel.StatusText = info;
                    Redraw();
                    e.Handled = true;
                }
            };

            // Öğe üzerinde mouse bırakıldığında sürüklemeyi kesinlikle sonlandır
            element.MouseLeftButtonUp += (s, e) =>
            {
                if (_isDragging)
                {
                    _isDragging = false;
                    _draggedItem = null;
                    if (TrackCanvas.IsMouseCaptured)
                    {
                        TrackCanvas.ReleaseMouseCapture();
                    }
                    Mouse.Capture(null);
                    Redraw();
                    e.Handled = true;
                }
            };

            element.MouseRightButtonUp += (s, e) =>
            {
                ShowContextMenu(itemData, e.GetPosition(this));
                e.Handled = true;
            };

            element.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    OpenItemEditDialog(itemData);
                    e.Handled = true;
                }
            };
        }

        private double GetItemKm(object item)
        {
            return item switch
            {
                TrenKonumu t => t.Konum,
                Istasyon ist => ist.H1OrtaNokta,
                TrafoMerkezi tm => tm.DilasKonumuH1,
                RayParalellemesi rp => rp.H1BaglantiKm,
                SeksiyonAyirici ay => ay.Konum,
                KatenerSeksiyonu sek => sek.BaslangicKm,
                KatenerEtap et => et.BaslangicKm,
                HizLimiti hz => hz.Baslangic,
                HatEgimi eg => eg.Baslangic,
                HatKurbu kr => kr.Baslangic,
                _ => 0
            };
        }

        private void SetItemKm(object item, double newKm)
        {
            double hat = ViewModel.Data.HatUzunlugu > 0 ? ViewModel.Data.HatUzunlugu : 17850;
            double clamped = Math.Max(0, Math.Min(hat, Math.Round(newKm, 1)));

            switch (item)
            {
                case TrenKonumu t:
                    t.Konum = clamped;
                    break;
                case Istasyon ist:
                    ist.H1OrtaNokta = clamped;
                    ist.H2OrtaNokta = clamped;
                    break;
                case TrafoMerkezi tm:
                    tm.DilasKonumuH1 = clamped;
                    tm.DilasKonumuH2 = clamped;
                    break;
                case RayParalellemesi rp:
                    rp.H1BaglantiKm = clamped;
                    rp.H2BaglantiKm = clamped;
                    break;
                case SeksiyonAyirici ay:
                    ay.Konum = clamped;
                    break;
                case KatenerSeksiyonu sek:
                    double lenSek = sek.BitisKm - sek.BaslangicKm;
                    sek.BaslangicKm = clamped;
                    sek.BitisKm = Math.Min(hat, clamped + lenSek);
                    break;
                case KatenerEtap et:
                    double lenEt = et.BitisKm - et.BaslangicKm;
                    et.BaslangicKm = clamped;
                    et.BitisKm = Math.Min(hat, clamped + lenEt);
                    break;
                case HizLimiti hz:
                    double lenHz = hz.Bitis - hz.Baslangic;
                    hz.Baslangic = clamped;
                    hz.Bitis = Math.Min(hat, clamped + lenHz);
                    break;
                case HatEgimi eg:
                    double lenEg = eg.Bitis - eg.Baslangic;
                    eg.Baslangic = clamped;
                    eg.Bitis = Math.Min(hat, clamped + lenEg);
                    break;
                case HatKurbu kr:
                    double lenKr = kr.Bitis - kr.Baslangic;
                    kr.Baslangic = clamped;
                    kr.Bitis = Math.Min(hat, clamped + lenKr);
                    break;
            }
        }

        private void TrackCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(TrackCanvas);
            double km = Math.Round(XToKm(p.X), 1);
            string track = GetNearestTrack(p.Y);

            if (ViewModel.ActiveToolMode == "STATION")
            {
                int n = ViewModel.Data.Istasyonlar.Count + 1;
                var ist = new Istasyon
                {
                    IstasyonId = n,
                    Ad = $"İstasyon {n}",
                    KisaAd = $"IST{n}",
                    H1OrtaNokta = km,
                    H2OrtaNokta = km,
                    Uzunluk = 120
                };
                ViewModel.Data.Istasyonlar.Add(ist);
                ViewModel.SelectedItem = ist;
                ViewModel.StatusText = $"🚉 Yeni istasyon ({km} m) eklendi.";
            }
            else if (ViewModel.ActiveToolMode == "TRAFO")
            {
                int n = ViewModel.Data.TrafoMerkezleri.Count + 1;
                var tm = new TrafoMerkezi
                {
                    Ad = $"TM-{n}",
                    Istasyon = "",
                    Direnc = 15,
                    DilasKonumuH1 = km,
                    DilasKonumuH2 = km,
                    FiderKabloDirenci = 2,
                    GeriDonusKabloDirenci = 1.5
                };
                ViewModel.Data.TrafoMerkezleri.Add(tm);
                ViewModel.SelectedItem = tm;
                ViewModel.StatusText = $"⚡ Yeni trafo merkezi TM-{n} ({km} m) eklendi.";
            }
            else if (ViewModel.ActiveToolMode == "PARALLEL")
            {
                int n = ViewModel.Data.RayParalellemeleri.Count + 1;
                var rp = new RayParalellemesi
                {
                    No = n,
                    H1BaglantiKm = km,
                    H2BaglantiKm = km
                };
                ViewModel.Data.RayParalellemeleri.Add(rp);
                ViewModel.SelectedItem = rp;
                ViewModel.StatusText = $"⊥ Ray Paralellemesi P{n} ({km} m) eklendi.";
            }
            else if (ViewModel.ActiveToolMode is "SPEED" or "EGIM" or "KURP")
            {
                _regionStartPoint = p;
                _previewRect = new Rectangle
                {
                    Stroke = Brushes.Cyan,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    Fill = new SolidColorBrush(Color.FromArgb(40, 0, 255, 255))
                };
                TrackCanvas.Children.Add(_previewRect);
                TrackCanvas.CaptureMouse();
            }
        }

        private void TrackCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(TrackCanvas);
            double km = Math.Round(XToKm(p.X), 1);
            string track = GetNearestTrack(p.Y);

            TxtCursorInfo.Text = $"📍 Konum: {km:0} m ({km / 1000:0.00} km) | Hat: {track}";

            // Dragging an existing object
            if (_isDragging && _draggedItem != null)
            {
                double deltaX = p.X - _dragStartMouseX;
                double deltaKm = (deltaX / GetTrackPixelLength()) * ViewModel.Data.HatUzunlugu;
                SetItemKm(_draggedItem, _dragItemOriginalKm + deltaKm);
                Redraw();
            }
            // Region creation preview
            else if (_regionStartPoint.HasValue && _previewRect != null)
            {
                double x1 = Math.Min(_regionStartPoint.Value.X, p.X);
                double x2 = Math.Max(_regionStartPoint.Value.X, p.X);
                double y = GetNearestTrack(p.Y) == "H1" ? H1_Y - 30 : H2_Y - 10;

                Canvas.SetLeft(_previewRect, x1);
                Canvas.SetTop(_previewRect, y);
                _previewRect.Width = Math.Max(1, x2 - x1);
                _previewRect.Height = 40;
            }
        }

        private void TrackCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                _draggedItem = null;
                if (TrackCanvas.IsMouseCaptured)
                {
                    TrackCanvas.ReleaseMouseCapture();
                }
                Mouse.Capture(null);
                Redraw();
            }

            if (_regionStartPoint.HasValue)
            {
                Point p = e.GetPosition(TrackCanvas);
                double km1 = Math.Round(XToKm(_regionStartPoint.Value.X), 1);
                double km2 = Math.Round(XToKm(p.X), 1);
                double startKm = Math.Min(km1, km2);
                double endKm = Math.Max(km1, km2);
                string track = GetNearestTrack(p.Y);

                if (endKm - startKm > 10)
                {
                    if (ViewModel.ActiveToolMode == "SPEED")
                    {
                        ViewModel.Data.HizLimitleri.Add(new HizLimiti { Baslangic = startKm, Bitis = endKm, Limit = 80, HatTipi = track });
                        ViewModel.StatusText = $"🚦 Hız Limiti (80 km/h, {startKm:0}m - {endKm:0}m) eklendi.";
                    }
                    else if (ViewModel.ActiveToolMode == "EGIM")
                    {
                        ViewModel.Data.HatEgimleri.Add(new HatEgimi { Baslangic = startKm, Bitis = endKm, EgimYuzdesi = 1.0, HatTipi = track });
                        ViewModel.StatusText = $"📐 Eğim (%1.0, {startKm:0}m - {endKm:0}m) eklendi.";
                    }
                    else if (ViewModel.ActiveToolMode == "KURP")
                    {
                        ViewModel.Data.HatKurplari.Add(new HatKurbu { Baslangic = startKm, Bitis = endKm, Yaricap = 350, HatTipi = track });
                        ViewModel.StatusText = $"↩ Kurp (R=350m, {startKm:0}m - {endKm:0}m) eklendi.";
                    }
                }

                _regionStartPoint = null;
                if (_previewRect != null)
                {
                    TrackCanvas.Children.Remove(_previewRect);
                    _previewRect = null;
                }
                TrackCanvas.ReleaseMouseCapture();
                Redraw();
            }
        }

        private void TrackCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                ViewModel.ZoomInCommand.Execute(null);
            }
            else
            {
                ViewModel.ZoomOutCommand.Execute(null);
            }
            e.Handled = true;
        }

        private void OnModeChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string mode)
            {
                ViewModel.SetToolModeCommand.Execute(mode);
            }
        }

        #endregion

        #region Context Menu & Edit Dialog

        private void ShowContextMenu(object item, Point screenPos)
        {
            var menu = new ContextMenu { Background = new SolidColorBrush(Color.FromRgb(28, 29, 45)), Foreground = Brushes.White };

            var miEdit = new MenuItem { Header = "✏️  Özellikleri Düzenle" };
            miEdit.Click += (s, e) => OpenItemEditDialog(item);
            menu.Items.Add(miEdit);

            if (item is TrenKonumu tren)
            {
                var miToggleDir = new MenuItem { Header = "↔️  Yönü Değiştir (İleri / Geri)" };
                miToggleDir.Click += (s, e) =>
                {
                    tren.Yon = tren.Yon == "ileri" ? "geri" : "ileri";
                    Redraw();
                };
                menu.Items.Add(miToggleDir);

                var miToggleTrack = new MenuItem { Header = "↕️  Hattı Değiştir (H1 / H2)" };
                miToggleTrack.Click += (s, e) =>
                {
                    tren.HatTipi = tren.HatTipi == "H1" ? "H2" : "H1";
                    Redraw();
                };
                menu.Items.Add(miToggleTrack);
            }

            menu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(50, 52, 75)) });

            var miDelete = new MenuItem { Header = "🗑️  Sil" };
            miDelete.Click += (s, e) => DeleteItem(item);
            menu.Items.Add(miDelete);

            menu.IsOpen = true;
        }

        private void DeleteItem(object item)
        {
            switch (item)
            {
                case TrenKonumu t: ViewModel.Data.Trenler.Remove(t); break;
                case Istasyon ist: ViewModel.Data.Istasyonlar.Remove(ist); break;
                case TrafoMerkezi tm: ViewModel.Data.TrafoMerkezleri.Remove(tm); break;
                case RayParalellemesi rp: ViewModel.Data.RayParalellemeleri.Remove(rp); break;
                case KatenerSeksiyonu sek: ViewModel.Data.KatenerSeksiyonlari.Remove(sek); break;
                case KatenerEtap et: ViewModel.Data.KatenerEtaplari.Remove(et); break;
                case SeksiyonAyirici ay: ViewModel.Data.SeksiyonAyiricilar.Remove(ay); break;
                case HizLimiti hz: ViewModel.Data.HizLimitleri.Remove(hz); break;
                case HatEgimi eg: ViewModel.Data.HatEgimleri.Remove(eg); break;
                case HatKurbu kr: ViewModel.Data.HatKurplari.Remove(kr); break;
            }
            Redraw();
            ViewModel.StatusText = "Öğe silindi.";
        }

        private void OpenItemEditDialog(object item)
        {
            var win = new Window
            {
                Title = "Öğe Düzenle",
                Width = 360,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(24, 25, 40)),
                Foreground = Brushes.White,
                ResizeMode = ResizeMode.NoResize
            };

            var sp = new StackPanel { Margin = new Thickness(16) };

            void AddField(string label, string initialValue, Action<string> onSave)
            {
                var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                row.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(180, 185, 210)), VerticalAlignment = VerticalAlignment.Center });
                var tb = new TextBox { Text = initialValue, Background = new SolidColorBrush(Color.FromRgb(38, 40, 60)), Foreground = Brushes.White, Padding = new Thickness(6, 4, 6, 4) };
                Grid.SetColumn(tb, 1);
                row.Children.Add(tb);
                sp.Children.Add(row);

                win.Closing += (s, e) => onSave(tb.Text);
            }

            switch (item)
            {
                case KatenerEtap et:
                    AddField("Etap Adı", et.Ad, v => et.Ad = v);
                    AddField("Başlangıç (m)", et.BaslangicKm.ToString(), v => { if (double.TryParse(v, out var d)) et.BaslangicKm = d; });
                    AddField("Bitiş (m)", et.BitisKm.ToString(), v => { if (double.TryParse(v, out var d)) et.BitisKm = d; });
                    AddField("Tünel Tipi", et.TunelTipi, v => et.TunelTipi = v);
                    AddField("Orta Nokta (m)", et.OrtaNoktaKm.ToString(), v => { if (double.TryParse(v, out var d)) et.OrtaNoktaKm = d; });
                    AddField("Besleyen Trafo", et.BesleyenTrafo, v => et.BesleyenTrafo = v);
                    break;
                case KatenerSeksiyonu sek:
                    AddField("Seksiyon Adı", sek.Ad, v => sek.Ad = v);
                    AddField("Başlangıç (m)", sek.BaslangicKm.ToString(), v => { if (double.TryParse(v, out var d)) sek.BaslangicKm = d; });
                    AddField("Bitiş (m)", sek.BitisKm.ToString(), v => { if (double.TryParse(v, out var d)) sek.BitisKm = d; });
                    AddField("Besleyen Trafolar", sek.BesleyenTrafolar, v => sek.BesleyenTrafolar = v);
                    break;
                case SeksiyonAyirici ay:
                    AddField("Ayırıcı Adı", ay.Ad, v => ay.Ad = v);
                    AddField("Konum (m)", ay.Konum.ToString(), v => { if (double.TryParse(v, out var d)) ay.Konum = d; });
                    AddField("Tip", ay.Tip, v => ay.Tip = v);
                    break;
                case Istasyon ist:
                    AddField("İstasyon Adı", ist.Ad, v => ist.Ad = v);
                    AddField("Kısa Ad", ist.KisaAd, v => ist.KisaAd = v);
                    AddField("Orta Nokta (m)", ist.H1OrtaNokta.ToString(), v => { if (double.TryParse(v, out var d)) { ist.H1OrtaNokta = d; ist.H2OrtaNokta = d; } });
                    AddField("Uzunluk (m)", ist.Uzunluk.ToString(), v => { if (double.TryParse(v, out var d)) ist.Uzunluk = d; });
                    break;
                case TrafoMerkezi tm:
                    AddField("Trafo Adı", tm.Ad, v => tm.Ad = v);
                    AddField("Direnç (mΩ)", tm.Direnc.ToString(), v => { if (double.TryParse(v, out var d)) tm.Direnc = d; });
                    AddField("Konum H1 (m)", tm.DilasKonumuH1.ToString(), v => { if (double.TryParse(v, out var d)) { tm.DilasKonumuH1 = d; tm.DilasKonumuH2 = d; } });
                    break;
                case TrenKonumu tr:
                    AddField("Tren Adı", tr.TrenAdi, v => tr.TrenAdi = v);
                    AddField("Konum (m)", tr.Konum.ToString(), v => { if (double.TryParse(v, out var d)) tr.Konum = d; });
                    AddField("Hat (H1 / H2)", tr.HatTipi, v => tr.HatTipi = v.Trim().ToUpper() == "H2" ? "H2" : "H1");
                    AddField("Yön (ileri / geri)", tr.Yon, v => tr.Yon = v.Trim().ToLower() == "geri" ? "geri" : "ileri");
                    break;
                case HizLimiti hz:
                    AddField("Başlangıç (m)", hz.Baslangic.ToString(), v => { if (double.TryParse(v, out var d)) hz.Baslangic = d; });
                    AddField("Bitiş (m)", hz.Bitis.ToString(), v => { if (double.TryParse(v, out var d)) hz.Bitis = d; });
                    AddField("Hız Limiti (km/h)", hz.Limit.ToString(), v => { if (double.TryParse(v, out var d)) hz.Limit = d; });
                    break;
                case HatEgimi eg:
                    AddField("Başlangıç (m)", eg.Baslangic.ToString(), v => { if (double.TryParse(v, out var d)) eg.Baslangic = d; });
                    AddField("Bitiş (m)", eg.Bitis.ToString(), v => { if (double.TryParse(v, out var d)) eg.Bitis = d; });
                    AddField("Eğim (%)", eg.EgimYuzdesi.ToString(), v => { if (double.TryParse(v, out var d)) eg.EgimYuzdesi = d; });
                    break;
                case HatKurbu kr:
                    AddField("Başlangıç (m)", kr.Baslangic.ToString(), v => { if (double.TryParse(v, out var d)) kr.Baslangic = d; });
                    AddField("Bitiş (m)", kr.Bitis.ToString(), v => { if (double.TryParse(v, out var d)) kr.Bitis = d; });
                    AddField("Yarıçap R (m)", kr.Yaricap.ToString(), v => { if (double.TryParse(v, out var d)) kr.Yaricap = d; });
                    break;
            }

            var btnOk = new Button { Content = "Kaydet", Margin = new Thickness(0, 16, 0, 0), Background = new SolidColorBrush(Color.FromRgb(124, 77, 255)), Foreground = Brushes.White, FontWeight = FontWeights.Bold, Padding = new Thickness(12, 6, 12, 6) };
            btnOk.Click += (s, e) => win.Close();
            sp.Children.Add(btnOk);

            win.Content = sp;
            win.ShowDialog();
            Redraw();
        }

        #endregion
    }
}
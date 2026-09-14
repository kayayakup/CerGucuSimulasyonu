using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using CerGucuSimulasyonu.Models;

namespace CerGucuSimulasyonu.Views
{
    public partial class StartupWizardWindow : Window
    {
        private int _currentStep = 1;
        private readonly SimulationData _data;

        public StartupWizardWindow(SimulationData data)
        {
            InitializeComponent();
            _data = data ?? new SimulationData();
            LoadDataToFields();
            UpdateStepUI();
        }

        private void LoadDataToFields()
        {
            // Tren Parametreleri
            TxtMaksTasarimHizi.Text = _data.Tren.MaksTasarimHizi.ToString("0.#");
            TxtMaksIsletmeHizi.Text = _data.Tren.MaksIsletmeHizi.ToString("0.#");
            TxtMaksIvme.Text = _data.Tren.MaksIvmelenme.ToString("0.##");
            TxtMaksFrenIvme.Text = _data.Tren.MaksFrenlemeIvmesi.ToString("0.##");
            TxtJerkLimiti.Text = _data.Tren.JerkLimiti.ToString("0.#");
            TxtTrenUzunlugu.Text = _data.Tren.TrenUzunlugu.ToString("0.#");
            TxtAw0.Text = _data.Tren.Aw0BosAgirlik.ToString("0.#");
            TxtAw3.Text = _data.Tren.Aw3DoluAgirlik.ToString("0.#");
            TxtDonerKutle.Text = _data.Tren.DonerKutle.ToString("0.##");
            TxtMaksCekisKuvveti.Text = _data.Tren.MaksCekisKuvvetiKn.ToString("0.#");
            TxtMotorGucu.Text = _data.Tren.MotorGucuKw.ToString("0.#");
            TxtYardimciGuc.Text = _data.Tren.YardimciGuc.ToString("0.#");
            TxtTrenVerimi.Text = _data.Tren.TrenVerimi.ToString("0.#");
            TxtNominalGerilim.Text = _data.CerKatener.YuksuzDcBaraGerilimi.ToString("0.#");
            TxtDavisA.Text = _data.Tren.DavisA.ToString("0.###");
            TxtDavisB.Text = _data.Tren.DavisB.ToString("0.###");
            TxtDavisC.Text = _data.Tren.DavisC.ToString("0.####");

            // İşletme & Sefer Parametreleri
            TxtToplamTrenSayisi.Text = _data.Isletme.ToplamTrenSayisi.ToString();
            TxtHeadwaySaniye.Text = _data.Isletme.SeferAraligiHeadwaySaniye.ToString("0.#");
            TxtMinMesafe.Text = _data.Isletme.MinimumTrenMesafesiMetre.ToString("0.#");
            TxtIlkTrenKonum.Text = _data.Isletme.IlkTrenBaslangicKonumuMetre.ToString("0.#");
            TxtHatUzunlugu.Text = _data.HatUzunlugu.ToString("0.#");
            TxtKatenerDirenci.Text = _data.CerKatener.RijitKatenerKmDirenci.ToString("0.#");
            TxtRayDirenci.Text = _data.CerKatener.NormalRayKmDirenci.ToString("0.#");
            TxtTrafoGucu.Text = _data.CerKatener.TrafoGucu.ToString("0.#");
            TxtDogrultucuGucu.Text = _data.CerKatener.DogrultucuGucu.ToString("0.#");
        }

        private void SaveFieldsToData()
        {
            // Parse & Save Train Parameters
            if (double.TryParse(TxtMaksTasarimHizi.Text, out var vTas)) _data.Tren.MaksTasarimHizi = vTas;
            if (double.TryParse(TxtMaksIsletmeHizi.Text, out var vIsl)) _data.Tren.MaksIsletmeHizi = vIsl;
            if (double.TryParse(TxtMaksIvme.Text, out var aMax)) _data.Tren.MaksIvmelenme = aMax;
            if (double.TryParse(TxtMaksFrenIvme.Text, out var aBrk)) _data.Tren.MaksFrenlemeIvmesi = aBrk;
            if (double.TryParse(TxtJerkLimiti.Text, out var jerk)) _data.Tren.JerkLimiti = jerk;
            if (double.TryParse(TxtTrenUzunlugu.Text, out var len)) _data.Tren.TrenUzunlugu = len;
            if (double.TryParse(TxtAw0.Text, out var aw0)) _data.Tren.Aw0BosAgirlik = aw0;
            if (double.TryParse(TxtAw3.Text, out var aw3)) _data.Tren.Aw3DoluAgirlik = aw3;
            if (double.TryParse(TxtDonerKutle.Text, out var rot)) _data.Tren.DonerKutle = rot;
            if (double.TryParse(TxtMaksCekisKuvveti.Text, out var fMax)) _data.Tren.MaksCekisKuvvetiKn = fMax;
            if (double.TryParse(TxtMotorGucu.Text, out var pMot)) _data.Tren.MotorGucuKw = pMot;
            if (double.TryParse(TxtYardimciGuc.Text, out var pAux)) _data.Tren.YardimciGuc = pAux;
            if (double.TryParse(TxtTrenVerimi.Text, out var eta)) _data.Tren.TrenVerimi = eta;
            if (double.TryParse(TxtDavisA.Text, out var dA)) _data.Tren.DavisA = dA;
            if (double.TryParse(TxtDavisB.Text, out var dB)) _data.Tren.DavisB = dB;
            if (double.TryParse(TxtDavisC.Text, out var dC)) _data.Tren.DavisC = dC;

            // Parse & Save Operation Parameters
            if (int.TryParse(TxtToplamTrenSayisi.Text, out var trnCount)) _data.Isletme.ToplamTrenSayisi = Math.Max(1, trnCount);
            if (double.TryParse(TxtHeadwaySaniye.Text, out var headway)) _data.Isletme.SeferAraligiHeadwaySaniye = Math.Max(10, headway);
            if (double.TryParse(TxtMinMesafe.Text, out var minM)) _data.Isletme.MinimumTrenMesafesiMetre = Math.Max(50, minM);
            if (double.TryParse(TxtIlkTrenKonum.Text, out var initPos)) _data.Isletme.IlkTrenBaslangicKonumuMetre = initPos;
            if (double.TryParse(TxtHatUzunlugu.Text, out var lineLen)) _data.HatUzunlugu = lineLen;
            if (double.TryParse(TxtKatenerDirenci.Text, out var rKat)) _data.CerKatener.RijitKatenerKmDirenci = rKat;
            if (double.TryParse(TxtRayDirenci.Text, out var rRay)) _data.CerKatener.NormalRayKmDirenci = rRay;
            if (double.TryParse(TxtTrafoGucu.Text, out var pTr)) _data.CerKatener.TrafoGucu = pTr;
            if (double.TryParse(TxtDogrultucuGucu.Text, out var pRec)) _data.CerKatener.DogrultucuGucu = pRec;
            if (double.TryParse(TxtNominalGerilim.Text, out var vNom)) _data.CerKatener.YuksuzDcBaraGerilimi = vNom;
        }

        private void UpdateStepUI()
        {
            Step1Panel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;

            // Badges
            SetBadgeStyle(Step1Badge, _currentStep == 1, _currentStep > 1);
            SetBadgeStyle(Step2Badge, _currentStep == 2, _currentStep > 2);
            SetBadgeStyle(Step3Badge, _currentStep == 3, false);

            BtnBack.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;

            if (_currentStep == 1)
            {
                BtnNext.Content = "⚡ Çekiş Gücü Grafiğini Çiz & İncele ▶";
                BtnNext.Visibility = Visibility.Visible;
                BtnFinish.Visibility = Visibility.Collapsed;
            }
            else if (_currentStep == 2)
            {
                BtnNext.Content = "▶ Devam: Sefer & Hat Parametreleri";
                BtnNext.Visibility = Visibility.Visible;
                BtnFinish.Visibility = Visibility.Collapsed;
                DrawTractionEffortGraph();
            }
            else
            {
                BtnNext.Visibility = Visibility.Collapsed;
                BtnFinish.Visibility = Visibility.Visible;
            }
        }

        private void SetBadgeStyle(Border badge, bool isCurrent, bool isCompleted)
        {
            if (isCurrent)
            {
                badge.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                if (badge.Child is TextBlock tb) tb.Foreground = Brushes.White;
            }
            else if (isCompleted)
            {
                badge.Background = new SolidColorBrush(Color.FromRgb(5, 150, 105));
                if (badge.Child is TextBlock tb) tb.Foreground = Brushes.White;
            }
            else
            {
                badge.Background = new SolidColorBrush(Color.FromRgb(30, 32, 52));
                if (badge.Child is TextBlock tb) tb.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            }
        }

        private void OnNextClicked(object sender, RoutedEventArgs e)
        {
            SaveFieldsToData();
            if (_currentStep < 3)
            {
                _currentStep++;
                UpdateStepUI();
            }
        }

        private void OnBackClicked(object sender, RoutedEventArgs e)
        {
            SaveFieldsToData();
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateStepUI();
            }
        }

        private void OnFinishClicked(object sender, RoutedEventArgs e)
        {
            SaveFieldsToData();

            // Initialize Multi-Train Fleet with Headway Schedule
            _data.Trenler.Clear();
            int trainCount = _data.Isletme.ToplamTrenSayisi;
            double headwaySec = _data.Isletme.SeferAraligiHeadwaySaniye;
            double initialPos = _data.Isletme.IlkTrenBaslangicKonumuMetre;

            for (int i = 0; i < trainCount; i++)
            {
                bool isFirst = i == 0;
                var tren = new TrenKonumu
                {
                    Id = i + 1,
                    TrenAdi = $"Tren {i + 1:00}",
                    Konum = initialPos,
                    HatTipi = "H1",
                    Yon = "ileri",
                    AnlikHiz = 0,
                    IsDispatched = isFirst,
                    DispatchTimeSeconds = i * headwaySec,
                    Durum = isFirst ? "SEFERE HAZIR (İSTASYONDA)" : $"SEFER BEKLİYOR ({i * headwaySec:0} sn)",
                    YolcuSayisi = isFirst ? 140 : 0
                };
                _data.Trenler.Add(tren);
            }

            DialogResult = true;
            Close();
        }

        private void TractionEffortCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_currentStep == 2)
            {
                DrawTractionEffortGraph();
            }
        }

        private void DrawTractionEffortGraph()
        {
            TractionEffortCanvas.Children.Clear();

            double width = TractionEffortCanvas.ActualWidth > 50 ? TractionEffortCanvas.ActualWidth : 800;
            double height = TractionEffortCanvas.ActualHeight > 50 ? TractionEffortCanvas.ActualHeight : 420;

            double pLeft = 65, pRight = 50, pTop = 30, pBottom = 45;
            double plotW = Math.Max(100, width - pLeft - pRight);
            double plotH = Math.Max(100, height - pTop - pBottom);

            double maxV = 100.0; // km/h
            double maxF = Math.Max(280.0, _data.Tren.MaksCekisKuvvetiKn * 1.25); // kN
            double maxP = Math.Max(2200.0, _data.Tren.MotorGucuKw * 1.2); // kW

            // Calculate Base Speed (Temel Hız)
            double eta = _data.Tren.TrenVerimi > 0 ? _data.Tren.TrenVerimi / 100.0 : 0.88;
            double fMax = _data.Tren.MaksCekisKuvvetiKn > 0 ? _data.Tren.MaksCekisKuvvetiKn : 240.0;
            double pMaxMech = _data.Tren.MotorGucuKw > 0 ? _data.Tren.MotorGucuKw * eta : 1800.0 * 0.88;
            double vBaseKmh = Math.Min(80.0, Math.Max(15.0, (pMaxMech / fMax) * 3.6));

            // Summary Card Text updates
            LblVBase.Text = $"{vBaseKmh:0.1} km/h";
            LblFMax.Text = $"{fMax:0.1} kN";
            LblPMax.Text = $"{_data.Tren.MotorGucuKw:0.1} kW";

            double weightAw3 = _data.Tren.Aw3DoluAgirlik > 0 ? _data.Tren.Aw3DoluAgirlik : 236.0;
            double dA = _data.Tren.DavisA;
            double dB = _data.Tren.DavisB;
            double dC = _data.Tren.DavisC;
            double r80Kn = (dA * weightAw3 + dB * weightAw3 * 80.0 + dC * Math.Pow(80.0, 2)) / 1000.0;
            LblDavis80.Text = $"{r80Kn:0.1} kN";

            // Functions for coordinate mapping
            double MapX(double v) => pLeft + (v / maxV) * plotW;
            double MapYForce(double f) => pTop + plotH - (f / maxF) * plotH;
            double MapYPower(double p) => pTop + plotH - (p / maxP) * plotH;

            // 1. Grid Lines & Axis
            // Horizontal Force Grids
            for (double f = 0; f <= maxF; f += 50)
            {
                double y = MapYForce(f);
                var gridLine = new Line
                {
                    X1 = pLeft, Y1 = y, X2 = pLeft + plotW, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };
                TractionEffortCanvas.Children.Add(gridLine);

                var txtF = new TextBlock
                {
                    Text = $"{f:0} kN",
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                };
                Canvas.SetLeft(txtF, pLeft - 48);
                Canvas.SetTop(txtF, y - 7);
                TractionEffortCanvas.Children.Add(txtF);
            }

            // Vertical Speed Grids
            for (double v = 0; v <= maxV; v += 10)
            {
                double x = MapX(v);
                var gridLine = new Line
                {
                    X1 = x, Y1 = pTop, X2 = x, Y2 = pTop + plotH,
                    Stroke = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };
                TractionEffortCanvas.Children.Add(gridLine);

                var txtV = new TextBlock
                {
                    Text = $"{v:0}",
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                };
                Canvas.SetLeft(txtV, x - 8);
                Canvas.SetTop(txtV, pTop + plotH + 6);
                TractionEffortCanvas.Children.Add(txtV);
            }

            // Base Speed (V_base) Vertical Marker Line
            double xBase = MapX(vBaseKmh);
            var vBaseLine = new Line
            {
                X1 = xBase, Y1 = pTop, X2 = xBase, Y2 = pTop + plotH,
                Stroke = new SolidColorBrush(Color.FromArgb(180, 56, 189, 248)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };
            TractionEffortCanvas.Children.Add(vBaseLine);

            var txtBaseLabel = new TextBlock
            {
                Text = $"V_base\n{vBaseKmh:0.1} km/h",
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(txtBaseLabel, xBase - 25);
            Canvas.SetTop(txtBaseLabel, pTop - 25);
            TractionEffortCanvas.Children.Add(txtBaseLabel);

            // 80 km/h Operating Speed Marker
            double xOper = MapX(_data.Tren.MaksIsletmeHizi);
            var vOperLine = new Line
            {
                X1 = xOper, Y1 = pTop, X2 = xOper, Y2 = pTop + plotH,
                Stroke = new SolidColorBrush(Color.FromArgb(160, 245, 158, 11)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 3, 3 }
            };
            TractionEffortCanvas.Children.Add(vOperLine);

            // Axis Title Labels
            var xTitle = new TextBlock
            {
                Text = "Tren Hızı v [km/h] ➔",
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(xTitle, pLeft + (plotW / 2) - 45);
            Canvas.SetTop(xTitle, pTop + plotH + 24);
            TractionEffortCanvas.Children.Add(xTitle);

            var yTitle = new TextBlock
            {
                Text = "▲ Çekiş Kuvveti F [kN] / Güç P [kW/10]",
                Foreground = Brushes.White,
                FontSize = 10.5,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(yTitle, pLeft);
            Canvas.SetTop(yTitle, pTop - 22);
            TractionEffortCanvas.Children.Add(yTitle);

            // 2. Draw Curves
            var forcePoly = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                StrokeThickness = 3
            };

            var powerPoly = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(192, 132, 252)),
                StrokeThickness = 2.5
            };

            var davisPoly = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                StrokeThickness = 2
            };

            for (double v = 0; v <= maxV; v += 0.5)
            {
                double px = MapX(v);

                // Tractive Force Calculation F(v)
                double fKn;
                if (v <= vBaseKmh)
                {
                    fKn = fMax;
                }
                else
                {
                    double vMs = Math.Max(0.1, v / 3.6);
                    fKn = pMaxMech / vMs; // Hyperbolic drop
                }
                forcePoly.Points.Add(new Point(px, MapYForce(fKn)));

                // Power Calculation P(v)
                double pKw;
                if (v <= vBaseKmh)
                {
                    double vMs = v / 3.6;
                    pKw = (fMax * vMs) / eta;
                }
                else
                {
                    pKw = _data.Tren.MotorGucuKw;
                }
                powerPoly.Points.Add(new Point(px, MapYPower(pKw)));

                // Davis Resistance R(v)
                double rKn = (dA * weightAw3 + dB * weightAw3 * v + dC * Math.Pow(v, 2)) / 1000.0;
                davisPoly.Points.Add(new Point(px, MapYForce(rKn)));
            }

            TractionEffortCanvas.Children.Add(davisPoly);
            TractionEffortCanvas.Children.Add(powerPoly);
            TractionEffortCanvas.Children.Add(forcePoly);
        }

        private void OnSaveChartAsPng(object sender, RoutedEventArgs e)
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "PNG Görseli (*.png)|*.png|Tüm Dosyalar (*.*)|*.*",
                    FileName = "CerGucu_TractionEffort_Grafigi.png",
                    Title = "Çekiş Gücü (Traction Effort) Grafiğini Kaydet"
                };

                if (sfd.ShowDialog() == true)
                {
                    // Render Visual to Bitmap
                    var target = ChartExportContainer;
                    int w = (int)Math.Max(600, target.ActualWidth);
                    int h = (int)Math.Max(350, target.ActualHeight);

                    var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(target);

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));

                    using (var fs = File.OpenWrite(sfd.FileName))
                    {
                        encoder.Save(fs);
                    }

                    MessageBox.Show("✅ Çekiş gücü (Traction Effort) grafiği başarıyla kaydedildi:\n" + sfd.FileName,
                        "Grafik Kaydedildi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Grafik kaydedilirken hata oluştu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

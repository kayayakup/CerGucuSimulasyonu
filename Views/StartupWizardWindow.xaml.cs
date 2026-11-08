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
            const double maxV = 80.0;
            const double maxForceKn = 300.0;
            double fMax = _data.Tren.TractionEffortAtSpeedKn(0);
            double maxBrakeForce = _data.Tren.RegenerativeBrakeEffortAtSpeedKn(0);
            double forceAt80Kmh = _data.Tren.TractionEffortAtSpeedKn(maxV);
            double forceDropSpeedKmh = maxV;
            if (_data.Tren.TractionEffortCurve.Count > 1)
            {
                double initialForce = _data.Tren.TractionEffortAtSpeedKn(0);
                for (int i = 1; i < _data.Tren.TractionEffortCurve.Count; i++)
                {
                    if (_data.Tren.TractionEffortAtSpeedKn(_data.Tren.TractionEffortCurve[i].SpeedKmh) < initialForce)
                    {
                        forceDropSpeedKmh = _data.Tren.TractionEffortCurve[i].SpeedKmh;
                        break;
                    }
                }
            }

            LblVBase.Text = $"{forceDropSpeedKmh:0.0} km/h";
            LblFMax.Text = $"{fMax:0.0} kN";
            LblPMax.Text = $"{maxBrakeForce:0.0} kN";
            LblDavis80.Text = $"{forceAt80Kmh:0.0} kN";

            double MapX(double v) => pLeft + (v / maxV) * plotW;
            double MapYForce(double f) => pTop + ((maxForceKn - f) / (2 * maxForceKn)) * plotH;

            for (double force = -maxForceKn; force <= maxForceKn; force += 50)
            {
                double y = MapYForce(force);
                var gridLine = new Line
                {
                    X1 = pLeft, Y1 = y, X2 = pLeft + plotW, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)),
                    StrokeThickness = force == 0 ? 1.5 : 1
                };
                TractionEffortCanvas.Children.Add(gridLine);

                var txtF = new TextBlock
                {
                    Text = $"{force:0}",
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                };
                Canvas.SetLeft(txtF, pLeft - 48);
                Canvas.SetTop(txtF, y - 7);
                TractionEffortCanvas.Children.Add(txtF);
            }

            for (double v = 0; v <= maxV; v += 10)
            {
                double x = MapX(v);
                var gridLine = new Line
                {
                    X1 = x, Y1 = pTop, X2 = x, Y2 = pTop + plotH,
                    Stroke = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)),
                    StrokeThickness = 1
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
                Text = "Çekiş / Elektrikli Fren Kuvveti [kN]",
                Foreground = Brushes.White,
                FontSize = 10.5,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(yTitle, pLeft);
            Canvas.SetTop(yTitle, pTop - 22);
            TractionEffortCanvas.Children.Add(yTitle);

            var forcePoly = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                StrokeThickness = 3
            };

            var brakePoly = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                StrokeThickness = 3
            };

            for (double v = 0; v <= maxV; v += 0.25)
            {
                double px = MapX(v);
                double fKn = _data.Tren.TractionEffortAtSpeedKn(v);
                forcePoly.Points.Add(new Point(px, MapYForce(fKn)));

                double brakeKn = _data.Tren.RegenerativeBrakeEffortAtSpeedKn(v);
                brakePoly.Points.Add(new Point(px, MapYForce(-brakeKn)));
            }

            TractionEffortCanvas.Children.Add(forcePoly);
            TractionEffortCanvas.Children.Add(brakePoly);
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

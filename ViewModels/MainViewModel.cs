using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CerGucuSimulasyonu.Models;
using System.Text.Json;
using System.IO;

namespace CerGucuSimulasyonu.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private SimulationData _data = new();

        public MainViewModel()
        {
            // Başlangıçta örnek veriler eklenebilir
        }

        [RelayCommand]
        private void SaveData()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(Data, options);
            File.WriteAllText("SimulasyonVerileri.json", jsonString);
        }

        [RelayCommand]
        private void LoadData()
        {
            if (File.Exists("SimulasyonVerileri.json"))
            {
                string jsonString = File.ReadAllText("SimulasyonVerileri.json");
                var loadedData = JsonSerializer.Deserialize<SimulationData>(jsonString);
                if (loadedData != null)
                {
                    Data = loadedData;
                }
            }
        }
        
        [RelayCommand]
        private void AddHatEgimi()
        {
            Data.HatEgimleri.Add(new HatEgimi());
        }

        [RelayCommand]
        private void RemoveHatEgimi(HatEgimi item)
        {
            if (item != null)
            {
                Data.HatEgimleri.Remove(item);
            }
        }

        [RelayCommand]
        private void AddHatKurbu()
        {
            Data.HatKurplari.Add(new HatKurbu());
        }

        [RelayCommand]
        private void RemoveHatKurbu(HatKurbu item)
        {
            if (item != null)
            {
                Data.HatKurplari.Remove(item);
            }
        }
        
        [RelayCommand]
        private void AddTrafoMerkezi()
        {
            Data.TrafoMerkezleri.Add(new TrafoMerkezi());
        }

        [RelayCommand]
        private void RemoveTrafoMerkezi(TrafoMerkezi item)
        {
            if (item != null)
            {
                Data.TrafoMerkezleri.Remove(item);
            }
        }

        [RelayCommand]
        private void AddRayParalellemesi()
        {
            Data.RayParalellemeleri.Add(new RayParalellemesi());
        }

        [RelayCommand]
        private void RemoveRayParalellemesi(RayParalellemesi item)
        {
            if (item != null)
            {
                Data.RayParalellemeleri.Remove(item);
            }
        }

        [RelayCommand]
        private void AddIstasyon()
        {
            Data.Istasyonlar.Add(new Istasyon());
        }

        [RelayCommand]
        private void RemoveIstasyon(Istasyon item)
        {
            if (item != null)
            {
                Data.Istasyonlar.Remove(item);
            }
        }

        [RelayCommand]
        private void AddHizLimiti()
        {
            Data.HizLimitleri.Add(new HizLimiti());
        }

        [RelayCommand]
        private void RemoveHizLimiti(HizLimiti item)
        {
            if (item != null)
            {
                Data.HizLimitleri.Remove(item);
            }
        }
    }
}

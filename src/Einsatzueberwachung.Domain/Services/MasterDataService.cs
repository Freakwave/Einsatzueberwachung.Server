// Implementierung des Master-Data-Service mit JSON-basierter Persistierung
// Quelle: Abgeleitet von WPF Services/DataService.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Services
{
    public class MasterDataService : IMasterDataService
    {
        private readonly string _dataPath;
        private SessionData? _sessionData;
        private Timer? _debounceTimer;
        private static readonly JsonSerializerOptions _writeOptions = new JsonSerializerOptions { WriteIndented = true };
        private const int SaveDelayMs = 500;

        public MasterDataService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _dataPath = Path.Combine(appDataPath, "Einsatzueberwachung.Web");
            
            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
            }
        }

        public async Task<SessionData> LoadSessionDataAsync()
        {
            if (_sessionData != null)
                return _sessionData;

            var filePath = Path.Combine(_dataPath, "SessionData.json");
            
            if (File.Exists(filePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    _sessionData = JsonSerializer.Deserialize<SessionData>(json) ?? new SessionData();
                }
                catch
                {
                    _sessionData = new SessionData();
                }
            }
            else
            {
                _sessionData = new SessionData();
            }

            return _sessionData;
        }

        public Task SaveSessionDataAsync(SessionData sessionData)
        {
            _sessionData = sessionData;
            ScheduleSave();
            return Task.CompletedTask;
        }

        // Schreibt sofort auf Disk (z.B. beim App-Shutdown oder explizitem Flush)
        public async Task FlushAsync()
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
            if (_sessionData != null)
                await WriteToDiskAsync(_sessionData);
        }

        private void ScheduleSave()
        {
            // Timer-Debounce: bei jedem Aufruf wird der Timer zurückgesetzt.
            // Nach 500ms Inaktivität wird tatsächlich auf Disk geschrieben.
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                async _ => await WriteToDiskAsync(_sessionData!),
                null,
                SaveDelayMs,
                Timeout.Infinite);
        }

        private async Task WriteToDiskAsync(SessionData sessionData)
        {
            var filePath = Path.Combine(_dataPath, "SessionData.json");
            var json = JsonSerializer.Serialize(sessionData, _writeOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task<List<PersonalEntry>> GetPersonalListAsync()
        {
            var data = await LoadSessionDataAsync();
            return data.PersonalList;
        }

        public async Task<PersonalEntry?> GetPersonalByIdAsync(string id)
        {
            var list = await GetPersonalListAsync();
            return list.FirstOrDefault(p => p.Id == id);
        }

        public async Task AddPersonalAsync(PersonalEntry personal)
        {
            var data = await LoadSessionDataAsync();
            data.PersonalList.Add(personal);
            await SaveSessionDataAsync(data);
        }

        public async Task UpdatePersonalAsync(PersonalEntry personal)
        {
            var data = await LoadSessionDataAsync();
            var existing = data.PersonalList.FirstOrDefault(p => p.Id == personal.Id);
            if (existing != null)
            {
                var index = data.PersonalList.IndexOf(existing);
                data.PersonalList[index] = personal;
                await SaveSessionDataAsync(data);
            }
        }

        public async Task DeletePersonalAsync(string id)
        {
            var data = await LoadSessionDataAsync();
            var existing = data.PersonalList.FirstOrDefault(p => p.Id == id);
            if (existing != null)
            {
                data.PersonalList.Remove(existing);
                await SaveSessionDataAsync(data);
            }
        }

        public async Task<List<DogEntry>> GetDogListAsync()
        {
            var data = await LoadSessionDataAsync();
            return data.DogList;
        }

        public async Task<DogEntry?> GetDogByIdAsync(string id)
        {
            var list = await GetDogListAsync();
            return list.FirstOrDefault(d => d.Id == id);
        }

        public async Task AddDogAsync(DogEntry dog)
        {
            var data = await LoadSessionDataAsync();
            data.DogList.Add(dog);
            await SaveSessionDataAsync(data);
        }

        public async Task UpdateDogAsync(DogEntry dog)
        {
            var data = await LoadSessionDataAsync();
            var existing = data.DogList.FirstOrDefault(d => d.Id == dog.Id);
            if (existing != null)
            {
                var index = data.DogList.IndexOf(existing);
                data.DogList[index] = dog;
                await SaveSessionDataAsync(data);
            }
        }

        public async Task DeleteDogAsync(string id)
        {
            var data = await LoadSessionDataAsync();
            var existing = data.DogList.FirstOrDefault(d => d.Id == id);
            if (existing != null)
            {
                data.DogList.Remove(existing);
                await SaveSessionDataAsync(data);
            }
        }

        public async Task<List<DroneEntry>> GetDroneListAsync()
        {
            var data = await LoadSessionDataAsync();
            return data.DroneList;
        }

        public async Task<DroneEntry?> GetDroneByIdAsync(string id)
        {
            var list = await GetDroneListAsync();
            return list.FirstOrDefault(d => d.Id == id);
        }

        public async Task AddDroneAsync(DroneEntry drone)
        {
            var data = await LoadSessionDataAsync();
            data.DroneList.Add(drone);
            await SaveSessionDataAsync(data);
        }

        public async Task UpdateDroneAsync(DroneEntry drone)
        {
            var data = await LoadSessionDataAsync();
            var existing = data.DroneList.FirstOrDefault(d => d.Id == drone.Id);
            if (existing != null)
            {
                var index = data.DroneList.IndexOf(existing);
                data.DroneList[index] = drone;
                await SaveSessionDataAsync(data);
            }
        }

        public async Task DeleteDroneAsync(string id)
        {
            var data = await LoadSessionDataAsync();
            var existing = data.DroneList.FirstOrDefault(d => d.Id == id);
            if (existing != null)
            {
                data.DroneList.Remove(existing);
                await SaveSessionDataAsync(data);
            }
        }
    }
}

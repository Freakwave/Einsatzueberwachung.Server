// Service-Interface für Stammdaten-Verwaltung (Personal, Hunde, Drohnen)
// Quelle: Abgeleitet von WPF Services/DataService.cs und ViewModels/MasterDataViewModel.cs

using System.Collections.Generic;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Interfaces
{
    public interface IMasterDataService
    {
        Task<List<PersonalEntry>> GetPersonalListAsync();
        Task<PersonalEntry?> GetPersonalByIdAsync(string id);
        Task AddPersonalAsync(PersonalEntry personal);
        Task UpdatePersonalAsync(PersonalEntry personal);
        Task DeletePersonalAsync(string id);

        Task<List<DogEntry>> GetDogListAsync();
        Task<DogEntry?> GetDogByIdAsync(string id);
        Task AddDogAsync(DogEntry dog);
        Task UpdateDogAsync(DogEntry dog);
        Task DeleteDogAsync(string id);

        Task<List<DroneEntry>> GetDroneListAsync();
        Task<DroneEntry?> GetDroneByIdAsync(string id);
        Task AddDroneAsync(DroneEntry drone);
        Task UpdateDroneAsync(DroneEntry drone);
        Task DeleteDroneAsync(string id);

        Task<SessionData> LoadSessionDataAsync();
        Task SaveSessionDataAsync(SessionData sessionData);
    }
}

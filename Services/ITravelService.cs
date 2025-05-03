using WebApp.Models;

namespace WebApp.Services;

public interface ITravelService
{
    Task<IEnumerable<Trip>> GetTripsAsync();
    Task<IEnumerable<Trip>> GetClientTripsAsync(int clientId);
    Task<int> AddClientAsync(Client client);
    Task<bool> RegisterClientToTripAsync(int clientId, int tripId);
    Task<bool> UnregisterClientFromTripAsync(int clientId, int tripId);
}
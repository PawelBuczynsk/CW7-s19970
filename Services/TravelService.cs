using Microsoft.Data.SqlClient;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Services;

public class TravelService : ITravelService
{
    private readonly string _connectionString;

    public TravelService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public async Task<IEnumerable<Trip>> GetTripsAsync()
    {
        var trips = new List<Trip>();
        var tripDict = new Dictionary<int, Trip>();

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            @"SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                     c.IdCountry, c.Name AS CountryName
              FROM Trip t
              JOIN Country_Trip ct ON t.IdTrip = ct.IdTrip
              JOIN Country c ON ct.IdCountry = c.IdCountry", connection);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int id = (int)reader["IdTrip"];
            if (!tripDict.TryGetValue(id, out var trip))
            {
                trip = new Trip
                {
                    IdTrip = id,
                    Name = reader["Name"].ToString()!,
                    Description = reader["Description"].ToString()!,
                    DateFrom = (DateTime)reader["DateFrom"],
                    DateTo = (DateTime)reader["DateTo"],
                    MaxPeople = (int)reader["MaxPeople"],
                    Countries = new List<Country>()
                };
                tripDict[id] = trip;
            }
            trip.Countries.Add(new Country
            {
                IdCountry = (int)reader["IdCountry"],
                Name = reader["CountryName"].ToString()!
            });
        }
        trips.AddRange(tripDict.Values);
        return trips;
    }

    public async Task<IEnumerable<Trip>> GetClientTripsAsync(int clientId)
    {
        var trips = new List<Trip>();
        var tripDict = new Dictionary<int, Trip>();

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            @"SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                     c.IdCountry, c.Name AS CountryName
              FROM Client_Trip ct
              JOIN Trip t ON ct.IdTrip = t.IdTrip
              JOIN Country_Trip ctr ON t.IdTrip = ctr.IdTrip
              JOIN Country c ON ctr.IdCountry = c.IdCountry
              WHERE ct.IdClient = @IdClient", connection);
        command.Parameters.AddWithValue("@IdClient", clientId);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int idTrip = (int)reader["IdTrip"];
            if (!tripDict.TryGetValue(idTrip, out var trip))
            {
                trip = new Trip
                {
                    IdTrip = idTrip,
                    Name = reader["Name"].ToString()!,
                    Description = reader["Description"].ToString()!,
                    DateFrom = (DateTime)reader["DateFrom"],
                    DateTo = (DateTime)reader["DateTo"],
                    MaxPeople = (int)reader["MaxPeople"],
                    Countries = new List<Country>()
                };
                tripDict[idTrip] = trip;
            }
            trip.Countries.Add(new Country
            {
                IdCountry = (int)reader["IdCountry"],
                Name = reader["CountryName"].ToString()!
            });
        }
        trips.AddRange(tripDict.Values);
        return trips;
    }

    public async Task<int> AddClientAsync(Client client)
    {
        using var connection = new SqlConnection(_connectionString);

        // Sprawdź unikalność PESEL
        using (var checkPesel = new SqlCommand("SELECT COUNT(1) FROM Client WHERE Pesel = @Pesel", connection))
        {
            checkPesel.Parameters.AddWithValue("@Pesel", client.Pesel);
            await connection.OpenAsync();
            if ((int)await checkPesel.ExecuteScalarAsync() > 0)
                throw new Exception("Klient z tym PESEL już istnieje.");
            connection.Close();
        }

        using var command = new SqlCommand(
            @"INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
              VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel);
              SELECT SCOPE_IDENTITY();", connection);

        command.Parameters.AddWithValue("@FirstName", client.FirstName);
        command.Parameters.AddWithValue("@LastName", client.LastName);
        command.Parameters.AddWithValue("@Email", client.Email);
        command.Parameters.AddWithValue("@Telephone", client.Telephone);
        command.Parameters.AddWithValue("@Pesel", client.Pesel);

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        if (result == null)
            throw new Exception("Błąd podczas dodawania klienta.");
        return Convert.ToInt32(result);
    }

    public async Task<bool> RegisterClientToTripAsync(int clientId, int tripId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Sprawdź, czy klient istnieje
        using (var checkClient = new SqlCommand("SELECT COUNT(1) FROM Client WHERE IdClient = @IdClient", connection))
        {
            checkClient.Parameters.AddWithValue("@IdClient", clientId);
            if ((int)await checkClient.ExecuteScalarAsync() == 0)
                return false;
        }

        // Sprawdź, czy wycieczka istnieje i pobierz MaxPeople
        int maxPeople;
        using (var checkTrip = new SqlCommand("SELECT MaxPeople FROM Trip WHERE IdTrip = @IdTrip", connection))
        {
            checkTrip.Parameters.AddWithValue("@IdTrip", tripId);
            var maxPeopleObj = await checkTrip.ExecuteScalarAsync();
            if (maxPeopleObj == null)
                return false;
            maxPeople = (int)maxPeopleObj;
        }

        // Sprawdź, czy klient już jest zapisany
        using (var check = new SqlCommand("SELECT COUNT(1) FROM Client_Trip WHERE IdClient=@cid AND IdTrip=@tid", connection))
        {
            check.Parameters.AddWithValue("@cid", clientId);
            check.Parameters.AddWithValue("@tid", tripId);
            if ((int)await check.ExecuteScalarAsync() > 0)
                return false;
        }

        // Sprawdź, czy nie przekroczono limitu
        using (var countCmd = new SqlCommand("SELECT COUNT(1) FROM Client_Trip WHERE IdTrip=@id", connection))
        {
            countCmd.Parameters.AddWithValue("@id", tripId);
            int current = (int)await countCmd.ExecuteScalarAsync();
            if (current >= maxPeople)
                return false;
        }

        // Wstaw rejestrację
        using (var insert = new SqlCommand(
            "INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt) VALUES (@cid, @tid, @date)", connection))
        {
            insert.Parameters.AddWithValue("@cid", clientId);
            insert.Parameters.AddWithValue("@tid", tripId);
            insert.Parameters.AddWithValue("@date", DateTime.UtcNow);
            int rows = await insert.ExecuteNonQueryAsync();
            return rows > 0;
        }
    }

    public async Task<bool> UnregisterClientFromTripAsync(int clientId, int tripId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Sprawdź, czy rejestracja istnieje
        using (var checkCmd = new SqlCommand(
            "SELECT COUNT(1) FROM Client_Trip WHERE IdClient = @clientId AND IdTrip = @tripId", connection))
        {
            checkCmd.Parameters.AddWithValue("@clientId", clientId);
            checkCmd.Parameters.AddWithValue("@tripId", tripId);
            var exists = (int)await checkCmd.ExecuteScalarAsync() > 0;
            if (!exists)
                return false; // Brak rejestracji
        }

        // Usuń rejestrację
        using (var deleteCmd = new SqlCommand(
            "DELETE FROM Client_Trip WHERE IdClient = @clientId AND IdTrip = @tripId", connection))
        {
            deleteCmd.Parameters.AddWithValue("@clientId", clientId);
            deleteCmd.Parameters.AddWithValue("@tripId", tripId);
            int affected = await deleteCmd.ExecuteNonQueryAsync();
            return affected > 0;
        }
    }
}

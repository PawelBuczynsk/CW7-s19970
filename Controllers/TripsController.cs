using Microsoft.AspNetCore.Mvc;
using WebApp.Services;

namespace WebApplication2.Controllers;

[ApiController]
[Route("api/trips")]
public class TripsController : ControllerBase
{
    private readonly ITravelService _service;
    public TripsController(ITravelService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetTrips()
    {
        var trips = await _service.GetTripsAsync();
        return Ok(trips);
    }

    [HttpGet("/api/clients/{id}/trips")]
    public async Task<IActionResult> GetClientTrips(int id)
    {
        var trips = await _service.GetClientTripsAsync(id);
        if (trips == null || !trips.Any()) return NotFound("Client not found or no trips");
        return Ok(trips);
    }
}
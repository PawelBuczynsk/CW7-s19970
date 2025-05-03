using Microsoft.AspNetCore.Mvc;
using WebApp.Models;
using WebApp.Services;

namespace WebApplication2.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly ITravelService _service;
    public ClientsController(ITravelService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> AddClient([FromBody] Client client)
    {
        try
        {
            var id = await _service.AddClientAsync(client);
            return Created($"/api/clients/{id}", new { id });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}/trips/{tripId}")]
    public async Task<IActionResult> RegisterClientToTrip(int id, int tripId)
    {
        var success = await _service.RegisterClientToTripAsync(id, tripId);
        return success ? Ok("Registered") : BadRequest("Registration failed");
    }

    [HttpDelete("{id}/trips/{tripId}")]
    public async Task<IActionResult> UnregisterClientFromTrip(int id, int tripId)
    {
        var success = await _service.UnregisterClientFromTripAsync(id, tripId);
        return success ? Ok("Unregistered") : NotFound("No registration found");
    }
}
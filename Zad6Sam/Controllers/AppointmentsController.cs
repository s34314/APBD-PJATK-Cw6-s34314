using ClinicApi.DTOs;
using ClinicApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClinicApi.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly AppointmentService _service;

    public AppointmentsController(AppointmentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAppointments(
        [FromQuery] string? status,
        [FromQuery] string? patientLastName)
    {
        var appointments = await _service.GetAppointmentsAsync(status, patientLastName);
        return Ok(appointments);
    }

    [HttpGet("{idAppointment:int}")]
    public async Task<IActionResult> GetAppointment(int idAppointment)
    {
        var appointment = await _service.GetAppointmentByIdAsync(idAppointment);
        if (appointment is null)
            return NotFound(new ErrorResponseDto { Message = "Appointment not found." });

        return Ok(appointment);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentRequestDto dto)
    {
        var (id, error, statusCode) = await _service.CreateAppointmentAsync(dto);

        if (error is not null)
        {
            return statusCode switch
            {
                409 => Conflict(new ErrorResponseDto { Message = error }),
                _ => BadRequest(new ErrorResponseDto { Message = error })
            };
        }

        return CreatedAtAction(nameof(GetAppointment), new { idAppointment = id }, new { IdAppointment = id });
    }

    [HttpPut("{idAppointment:int}")]
    public async Task<IActionResult> UpdateAppointment(int idAppointment, [FromBody] UpdateAppointmentRequestDto dto)
    {
        var (found, error, statusCode) = await _service.UpdateAppointmentAsync(idAppointment, dto);

        if (!found)
            return NotFound(new ErrorResponseDto { Message = "Appointment not found." });

        if (error is not null)
        {
            return statusCode switch
            {
                409 => Conflict(new ErrorResponseDto { Message = error }),
                _ => BadRequest(new ErrorResponseDto { Message = error })
            };
        }

        return Ok();
    }

    [HttpDelete("{idAppointment:int}")]
    public async Task<IActionResult> DeleteAppointment(int idAppointment)
    {
        var (found, error, statusCode) = await _service.DeleteAppointmentAsync(idAppointment);

        if (!found)
            return NotFound(new ErrorResponseDto { Message = "Appointment not found." });

        if (error is not null)
            return Conflict(new ErrorResponseDto { Message = error });

        return NoContent();
    }
}
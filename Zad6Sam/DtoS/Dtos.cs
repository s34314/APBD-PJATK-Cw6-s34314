namespace ClinicApi.DTOs;

public class AppointmentListDto
{
    public int IdAppointment { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string PatientFullName { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
}

public class AppointmentDetailsDto
{
    public int IdAppointment { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? InternalNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public PatientDto Patient { get; set; } = null!;
    public DoctorDto Doctor { get; set; } = null!;
}

public class PatientDto
{
    public int IdPatient { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
}

public class DoctorDto
{
    public int IdDoctor { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
}

public class CreateAppointmentRequestDto
{
    public int IdPatient { get; set; }
    public int IdDoctor { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class UpdateAppointmentRequestDto
{
    public int IdPatient { get; set; }
    public int IdDoctor { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? InternalNotes { get; set; }
}

public class ErrorResponseDto
{
    public string Message { get; set; } = string.Empty;
}
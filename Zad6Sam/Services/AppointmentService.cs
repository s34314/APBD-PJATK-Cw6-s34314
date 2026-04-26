using System.Data;
using ClinicApi.DTOs;
using Microsoft.Data.SqlClient;

namespace ClinicApi.Services;

public class AppointmentService
{
    private readonly string _connectionString;

    public AppointmentService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public async Task<List<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName)
    {
        var result = new List<AppointmentListDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("""
            SELECT
                a.IdAppointment,
                a.AppointmentDate,
                a.Status,
                a.Reason,
                p.FirstName + N' ' + p.LastName AS PatientFullName,
                p.Email AS PatientEmail
            FROM dbo.Appointments a
            JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
            WHERE (@Status IS NULL OR a.Status = @Status)
              AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
            ORDER BY a.AppointmentDate;
            """, connection);

        command.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value =
            (object?)status ?? DBNull.Value;
        command.Parameters.Add("@PatientLastName", SqlDbType.NVarChar, 80).Value =
            (object?)patientLastName ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                Reason = reader.GetString(reader.GetOrdinal("Reason")),
                PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail"))
            });
        }

        return result;
    }

    public async Task<AppointmentDetailsDto?> GetAppointmentByIdAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("""
            SELECT
                a.IdAppointment,
                a.AppointmentDate,
                a.Status,
                a.Reason,
                a.InternalNotes,
                a.CreatedAt,
                p.IdPatient,
                p.FirstName AS PatientFirstName,
                p.LastName AS PatientLastName,
                p.Email AS PatientEmail,
                p.PhoneNumber AS PatientPhone,
                p.DateOfBirth AS PatientDob,
                d.IdDoctor,
                d.FirstName AS DoctorFirstName,
                d.LastName AS DoctorLastName,
                d.LicenseNumber,
                s.Name AS Specialization
            FROM dbo.Appointments a
            JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
            JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
            JOIN dbo.Specializations s ON s.IdSpecialization = d.IdSpecialization
            WHERE a.IdAppointment = @IdAppointment;
            """, connection);

        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new AppointmentDetailsDto
        {
            IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
            AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            Reason = reader.GetString(reader.GetOrdinal("Reason")),
            InternalNotes = reader.IsDBNull(reader.GetOrdinal("InternalNotes"))
                ? null
                : reader.GetString(reader.GetOrdinal("InternalNotes")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            Patient = new PatientDto
            {
                IdPatient = reader.GetInt32(reader.GetOrdinal("IdPatient")),
                FirstName = reader.GetString(reader.GetOrdinal("PatientFirstName")),
                LastName = reader.GetString(reader.GetOrdinal("PatientLastName")),
                Email = reader.GetString(reader.GetOrdinal("PatientEmail")),
                PhoneNumber = reader.GetString(reader.GetOrdinal("PatientPhone")),
                DateOfBirth = reader.GetDateTime(reader.GetOrdinal("PatientDob"))
            },
            Doctor = new DoctorDto
            {
                IdDoctor = reader.GetInt32(reader.GetOrdinal("IdDoctor")),
                FirstName = reader.GetString(reader.GetOrdinal("DoctorFirstName")),
                LastName = reader.GetString(reader.GetOrdinal("DoctorLastName")),
                LicenseNumber = reader.GetString(reader.GetOrdinal("LicenseNumber")),
                Specialization = reader.GetString(reader.GetOrdinal("Specialization"))
            }
        };
    }

    public async Task<(int? id, string? error, int statusCode)> CreateAppointmentAsync(CreateAppointmentRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return (null, "Reason cannot be empty.", 400);

        if (dto.Reason.Length > 250)
            return (null, "Reason cannot exceed 250 characters.", 400);

        if (dto.AppointmentDate <= DateTime.UtcNow)
            return (null, "Appointment date must be in the future.", 400);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var patientCmd = new SqlCommand(
            "SELECT COUNT(1) FROM dbo.Patients WHERE IdPatient = @Id AND IsActive = 1;", connection);
        patientCmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.IdPatient;
        var patientExists = (int)(await patientCmd.ExecuteScalarAsync())! > 0;
        if (!patientExists)
            return (null, "Patient does not exist or is inactive.", 400);

        await using var doctorCmd = new SqlCommand(
            "SELECT COUNT(1) FROM dbo.Doctors WHERE IdDoctor = @Id AND IsActive = 1;", connection);
        doctorCmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.IdDoctor;
        var doctorExists = (int)(await doctorCmd.ExecuteScalarAsync())! > 0;
        if (!doctorExists)
            return (null, "Doctor does not exist or is inactive.", 400);

        await using var conflictCmd = new SqlCommand("""
            SELECT COUNT(1) FROM dbo.Appointments
            WHERE IdDoctor = @IdDoctor
              AND AppointmentDate = @AppointmentDate
              AND Status = N'Scheduled';
            """, connection);
        conflictCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
        conflictCmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = dto.AppointmentDate;
        var conflict = (int)(await conflictCmd.ExecuteScalarAsync())! > 0;
        if (conflict)
            return (null, "Doctor already has a scheduled appointment at this time.", 409);

        await using var insertCmd = new SqlCommand("""
            INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
            OUTPUT INSERTED.IdAppointment
            VALUES (@IdPatient, @IdDoctor, @AppointmentDate, N'Scheduled', @Reason);
            """, connection);
        insertCmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;
        insertCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
        insertCmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = dto.AppointmentDate;
        insertCmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = dto.Reason;

        var newId = (int)(await insertCmd.ExecuteScalarAsync())!;
        return (newId, null, 201);
    }

    public async Task<(bool found, string? error, int statusCode)> UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto dto)
    {
        var validStatuses = new[] { "Scheduled", "Completed", "Cancelled" };
        if (!validStatuses.Contains(dto.Status))
            return (true, "Status must be one of: Scheduled, Completed, Cancelled.", 400);

        if (string.IsNullOrWhiteSpace(dto.Reason))
            return (true, "Reason cannot be empty.", 400);

        if (dto.Reason.Length > 250)
            return (true, "Reason cannot exceed 250 characters.", 400);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var existingCmd = new SqlCommand(
            "SELECT Status, AppointmentDate FROM dbo.Appointments WHERE IdAppointment = @Id;", connection);
        existingCmd.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;

        await using var existingReader = await existingCmd.ExecuteReaderAsync();
        if (!await existingReader.ReadAsync())
            return (false, null, 404);

        var currentStatus = existingReader.GetString(existingReader.GetOrdinal("Status"));
        var currentDate = existingReader.GetDateTime(existingReader.GetOrdinal("AppointmentDate"));
        await existingReader.CloseAsync();

        if (currentStatus == "Completed" && dto.AppointmentDate != currentDate)
            return (true, "Cannot change the date of a completed appointment.", 409);

        await using var patientCmd = new SqlCommand(
            "SELECT COUNT(1) FROM dbo.Patients WHERE IdPatient = @Id AND IsActive = 1;", connection);
        patientCmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.IdPatient;
        var patientExists = (int)(await patientCmd.ExecuteScalarAsync())! > 0;
        if (!patientExists)
            return (true, "Patient does not exist or is inactive.", 400);

        await using var doctorCmd = new SqlCommand(
            "SELECT COUNT(1) FROM dbo.Doctors WHERE IdDoctor = @Id AND IsActive = 1;", connection);
        doctorCmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.IdDoctor;
        var doctorExists = (int)(await doctorCmd.ExecuteScalarAsync())! > 0;
        if (!doctorExists)
            return (true, "Doctor does not exist or is inactive.", 400);

        await using var conflictCmd = new SqlCommand("""
            SELECT COUNT(1) FROM dbo.Appointments
            WHERE IdDoctor = @IdDoctor
              AND AppointmentDate = @AppointmentDate
              AND Status = N'Scheduled'
              AND IdAppointment <> @IdAppointment;
            """, connection);
        conflictCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
        conflictCmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = dto.AppointmentDate;
        conflictCmd.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
        var conflict = (int)(await conflictCmd.ExecuteScalarAsync())! > 0;
        if (conflict)
            return (true, "Doctor already has a scheduled appointment at this time.", 409);

        await using var updateCmd = new SqlCommand("""
            UPDATE dbo.Appointments
            SET IdPatient = @IdPatient,
                IdDoctor = @IdDoctor,
                AppointmentDate = @AppointmentDate,
                Status = @Status,
                Reason = @Reason,
                InternalNotes = @InternalNotes
            WHERE IdAppointment = @IdAppointment;
            """, connection);
        updateCmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;
        updateCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
        updateCmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = dto.AppointmentDate;
        updateCmd.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = dto.Status;
        updateCmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = dto.Reason;
        updateCmd.Parameters.Add("@InternalNotes", SqlDbType.NVarChar, 500).Value =
            (object?)dto.InternalNotes ?? DBNull.Value;
        updateCmd.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

        await updateCmd.ExecuteNonQueryAsync();
        return (true, null, 200);
    }

    public async Task<(bool found, string? error, int statusCode)> DeleteAppointmentAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var selectCmd = new SqlCommand(
            "SELECT Status FROM dbo.Appointments WHERE IdAppointment = @Id;", connection);
        selectCmd.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;

        await using var reader = await selectCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return (false, null, 404);

        var status = reader.GetString(reader.GetOrdinal("Status"));
        await reader.CloseAsync();

        if (status == "Completed")
            return (true, "Cannot delete a completed appointment.", 409);

        await using var deleteCmd = new SqlCommand(
            "DELETE FROM dbo.Appointments WHERE IdAppointment = @Id;", connection);
        deleteCmd.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;
        await deleteCmd.ExecuteNonQueryAsync();

        return (true, null, 204);
    }
}
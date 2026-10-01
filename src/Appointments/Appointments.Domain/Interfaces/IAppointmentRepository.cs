using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Appointments.Domain;

namespace Appointments.Application.Interfaces
{
    public interface IAppointmentRepository
    {
        Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<Appointment>> GetByPatientIdAsync(Guid patientId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Appointment>> GetByDoctorIdAsync(Guid doctorId, DateTime date, CancellationToken cancellationToken = default);
        Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default);
        Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default);
        Task DeleteAsync(Appointment appointment, CancellationToken cancellationToken = default);

        // Каскадная отмена: активные записи по специализации/услуге
        Task<IEnumerable<Appointment>> GetActiveBySpecializationIdAsync(string specializationId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Appointment>> GetActiveByServiceIdAsync(string serviceId, CancellationToken cancellationToken = default);

        // US-63: приёмы на указанную дату, которым ещё не отправляли напоминание
        // (не отменённые, с заполненным email). Используется Quartz-задачей.
        Task<IEnumerable<Appointment>> GetForReminderAsync(DateTime date, CancellationToken cancellationToken = default);


        // Методы для работы с заключениями
        Task AddResultAsync(AppointmentResult result, CancellationToken cancellationToken = default);
        Task<bool> HasResultAsync(Guid appointmentId, CancellationToken cancellationToken = default);

        Task<AppointmentResult?> GetResultByAppointmentIdAsync(Guid appointmentId, CancellationToken cancellationToken = default);
        Task UpdateResultAsync(AppointmentResult result, CancellationToken cancellationToken = default);
    }
}

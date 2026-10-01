using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Appointments.Application.Interfaces;
using Appointments.Domain;
using Appointments.Infrastructure.PostgreSql.Data;

namespace Appointments.Infrastructure.PostgreSql.Repositories
{
    public class AppointmentRepository : IAppointmentRepository
    {
        private readonly AppointmentsDataContext _context;

        public AppointmentRepository(AppointmentsDataContext context)
        {
            _context = context;
        }

        public async Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Appointments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<Appointment>> GetByPatientIdAsync(Guid patientId, CancellationToken cancellationToken = default)
        {
            return await _context.Appointments
                .Where(a => a.PatientId == patientId)
                .OrderByDescending(a => a.Date)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
        {
            await _context.Appointments.AddAsync(appointment, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default)
        {
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(Appointment appointment, CancellationToken cancellationToken = default)
        {
            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<Appointment>> GetByDoctorIdAsync(Guid doctorId, DateTime date, CancellationToken cancellationToken = default)
        {
            return await _context.Appointments
                .Where(a => a.DoctorId == doctorId && a.Date.Date == date.Date)
                .ToListAsync(cancellationToken);
        }

        // Каскадная отмена: активные записи по специализации (фильтрация на стороне БД)
        public async Task<IEnumerable<Appointment>> GetActiveBySpecializationIdAsync(string specializationId, CancellationToken cancellationToken = default)
        {
            return await _context.Appointments
                .Where(a => a.SpecializationId == specializationId
                            && a.Status != AppointmentStatuses.Canceled
                            && a.Status != AppointmentStatuses.Completed)
                .ToListAsync(cancellationToken);
        }

        // Каскадная отмена: активные записи по конкретной услуге
        public async Task<IEnumerable<Appointment>> GetActiveByServiceIdAsync(string serviceId, CancellationToken cancellationToken = default)
        {
            return await _context.Appointments
                .Where(a => a.ServiceId == serviceId
                            && a.Status != AppointmentStatuses.Canceled
                            && a.Status != AppointmentStatuses.Completed)
                .ToListAsync(cancellationToken);
        }

        // US-63: приёмы на дату, которым ещё не отправляли напоминание.
        // Отменённые записи не напоминаем, а без email напоминание отправить некуда.
        public async Task<IEnumerable<Appointment>> GetForReminderAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            return await _context.Appointments
                .Where(a => a.Date.Date == date.Date
                            && a.Status != AppointmentStatuses.Canceled
                            && a.ReminderSentAt == null
                            && a.PatientEmail != string.Empty)
                .ToListAsync(cancellationToken);
        }

        public async Task AddResultAsync(AppointmentResult result, CancellationToken cancellationToken = default)
        {            await _context.AppointmentResults.AddAsync(result, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> HasResultAsync(Guid appointmentId, CancellationToken cancellationToken = default)
        {
            return await _context.AppointmentResults.AnyAsync(r => r.AppointmentId == appointmentId, cancellationToken);
        }

        public async Task<AppointmentResult?> GetResultByAppointmentIdAsync(Guid appointmentId, CancellationToken cancellationToken = default)
        {
            return await _context.AppointmentResults.FirstOrDefaultAsync(r => r.AppointmentId == appointmentId, cancellationToken);
        }

        public async Task UpdateResultAsync(AppointmentResult result, CancellationToken cancellationToken = default)
        {
            _context.AppointmentResults.Update(result);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

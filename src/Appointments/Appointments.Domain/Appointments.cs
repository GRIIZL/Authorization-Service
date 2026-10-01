using System;

namespace Appointments.Domain
{
    public class Appointment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        // Внешний ключ пациента из Profiles API
        public Guid PatientId { get; set; }

        // Денормализованный email пациента (вариант A): нужен для отправки писем
        // без синхронного вызова Profiles/Auth на каждом приёме.
        public string PatientEmail { get; set; } = string.Empty;
        
        // Внешние межсервисные ключи связки (AC-3 в ТЗ)
        public string SpecializationId { get; set; } = string.Empty; // Из Services API
        public Guid DoctorId { get; set; }                           // Из Profiles API
        public string ServiceId { get; set; } = string.Empty;        // Из Services API
        public string OfficeId { get; set; } = string.Empty;         // Из Offices API (MongoDB)

        // Дата приема и зарезервированный временной слот (С AC-5 по AC-11)
        public DateTime Date { get; set; }
        public string Timeslot { get; set; } = string.Empty; // Например, "10:30 - 11:00"

        // Статус записи: "Pending", "Approved", "Canceled", "Completed"
        public string Status { get; set; } = AppointmentStatuses.Pending;

        // Момент отправки напоминания за день до приёма (US-63).
        // null — напоминание ещё не отправлялось; защищает от повторной отправки.
        public DateTime? ReminderSentAt { get; set; }


        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

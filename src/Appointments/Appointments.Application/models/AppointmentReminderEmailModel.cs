using System;

namespace Appointments.Application.Models
{
    /// <summary>
    /// Данные письма-напоминания о приёме (US-63, AC-2):
    /// ФИО пациента, дата, время, услуга и ФИО врача.
    /// </summary>
    public class AppointmentReminderEmailModel
    {
        public string PatientFullName { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public string Timeslot { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string DoctorFullName { get; set; } = string.Empty;
    }
}

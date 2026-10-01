using Microsoft.EntityFrameworkCore;
using Appointments.Domain;
using System;

namespace Appointments.Infrastructure.PostgreSql.Data
{
    public class AppointmentsDataContext : DbContext
    {
        public AppointmentsDataContext(DbContextOptions<AppointmentsDataContext> options) : base(options) { }

        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<AppointmentResult> AppointmentResults { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Appointment>().HasKey(a => a.Id);
            modelBuilder.Entity<AppointmentResult>().HasKey(r => r.Id);

            // Явно фиксируем тип колонок дат. Без этого включённый в Program.cs
            // EnableLegacyTimestampBehavior заставляет Npgsql маппить DateTime как
            // "timestamp without time zone", и модель расходится со схемой БД,
            // из-за чего EF генерирует лишние AlterColumn и блокирует миграции.
            modelBuilder.Entity<Appointment>().Property(a => a.Date).HasColumnType("timestamp with time zone");
            modelBuilder.Entity<Appointment>().Property(a => a.CreatedAt).HasColumnType("timestamp with time zone");
            modelBuilder.Entity<Appointment>().Property(a => a.UpdatedAt).HasColumnType("timestamp with time zone");
            modelBuilder.Entity<Appointment>().Property(a => a.ReminderSentAt).HasColumnType("timestamp with time zone");
            modelBuilder.Entity<AppointmentResult>().Property(r => r.CreatedAt).HasColumnType("timestamp with time zone");
        }
    }
}

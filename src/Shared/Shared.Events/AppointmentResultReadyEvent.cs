namespace Shared.Events
{
    /// <summary>
    /// Событие о готовности результата приёма (US-68).
    /// Публикуется Appointments при создании ИЛИ обновлении заключения доктором.
    /// Несёт только идентификатор: консьюмер сам прочитает актуальные данные из БД,
    /// чтобы письмо полностью соответствовало последней версии заключения (AC-2).
    /// </summary>
    public class AppointmentResultReadyEvent
    {
        /// <summary>Идентификатор приёма, к которому относится заключение.</summary>
        public Guid AppointmentId { get; set; }

        /// <summary>Момент создания/обновления заключения (UTC).</summary>
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Services.Application.Interfaces;
using Services.Application.Models;
using Services.Application.Services;
using Services.Domain;
using Shared.Events;
using Xunit;

namespace Services.Application.Tests
{
    public class SpezializationServiceTests
    {
        private const string SpecId = "spec-1";
        private const string ServiceId = "service-1";

        private readonly Mock<ISpecializationRepository> _repository = new();
        private readonly Mock<IEventPublisher> _eventPublisher = new();
        private readonly List<SpecializationChangedEvent> _publishedEvents = new();

        private SpezializationService CreateSut() => new(_repository.Object, _eventPublisher.Object);

        private void SetupEventCapture()
        {
            _publishedEvents.Clear();
            _eventPublisher
                .Setup(p => p.PublishAsync(It.IsAny<SpecializationChangedEvent>(), It.IsAny<CancellationToken>()))
                .Callback<SpecializationChangedEvent, CancellationToken>((e, _) => _publishedEvents.Add(e))
                .Returns(Task.CompletedTask);
        }

        private void VerifyNoEventPublished() =>
            _eventPublisher.Verify(
                p => p.PublishAsync(It.IsAny<SpecializationChangedEvent>(), It.IsAny<CancellationToken>()),
                Times.Never);

        private static Specialization CreateSpecialization(
            string specStatus = ServiceStatuses.Active,
            string serviceStatus = ServiceStatuses.Active)
        {
            var createdAt = DateTime.UtcNow.AddDays(-2);
            return new Specialization
            {
                Id = SpecId,
                Name = "Cardiology",
                Status = specStatus,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                Services = new List<MedicalService>
                {
                    new()
                    {
                        Id = ServiceId,
                        Name = "Consultation",
                        Price = 100m,
                        CategoryName = "Consultations",
                        Status = serviceStatus,
                        CreatedAt = createdAt,
                        UpdatedAt = createdAt
                    }
                }
            };
        }

        private void SetupExistingSpecialization(Specialization specialization) =>
            _repository
                .Setup(r => r.GetByIdAsync(SpecId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(specialization);

        #region GetServiceByIdAsync

        [Fact]
        public async Task GetServiceByIdAsync_ReturnsNull_WhenSpecializationMissing()
        {
            _repository
                .Setup(r => r.GetByIdAsync(SpecId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Specialization?)null);

            var result = await CreateSut().GetServiceByIdAsync(SpecId, ServiceId);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetServiceByIdAsync_ReturnsNull_WhenServiceMissing()
        {
            SetupExistingSpecialization(CreateSpecialization());

            var result = await CreateSut().GetServiceByIdAsync(SpecId, "unknown-service");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetServiceByIdAsync_ReturnsService_WhenFound()
        {
            SetupExistingSpecialization(CreateSpecialization());

            var result = await CreateSut().GetServiceByIdAsync(SpecId, ServiceId);

            Assert.NotNull(result);
            Assert.Equal(ServiceId, result!.Id);
        }

        #endregion

        #region UpdateServiceInSpecializationAsync

        [Fact]
        public async Task UpdateServiceInSpecializationAsync_ReturnsFalse_WhenSpecializationMissing()
        {
            _repository
                .Setup(r => r.GetByIdAsync(SpecId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Specialization?)null);

            var result = await CreateSut().UpdateServiceInSpecializationAsync(SpecId, ServiceId, new UpdateMedicalServiceDto());

            Assert.False(result);
            _repository.Verify(r => r.UpdateAsync(It.IsAny<Specialization>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateServiceInSpecializationAsync_ReturnsFalse_WhenServiceMissing()
        {
            SetupExistingSpecialization(CreateSpecialization());

            var result = await CreateSut().UpdateServiceInSpecializationAsync(SpecId, "unknown-service", new UpdateMedicalServiceDto());

            Assert.False(result);
            _repository.Verify(r => r.UpdateAsync(It.IsAny<Specialization>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateServiceInSpecializationAsync_UpdatesFields_TrimsName_AndPersists()
        {
            var specialization = CreateSpecialization();
            SetupExistingSpecialization(specialization);
            var oldUpdatedAt = specialization.Services[0].UpdatedAt;

            var dto = new UpdateMedicalServiceDto
            {
                Name = "  New Name  ",
                Price = 250m,
                CategoryName = "Diagnostics",
                Status = ServiceStatuses.Active
            };

            var result = await CreateSut().UpdateServiceInSpecializationAsync(SpecId, ServiceId, dto);

            var service = specialization.Services.Single(s => s.Id == ServiceId);
            Assert.True(result);
            Assert.Equal("New Name", service.Name);
            Assert.Equal(250m, service.Price);
            Assert.Equal("Diagnostics", service.CategoryName);
            Assert.Equal(ServiceStatuses.Active, service.Status);
            Assert.True(service.UpdatedAt > oldUpdatedAt);
            _repository.Verify(r => r.UpdateAsync(specialization, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateServiceInSpecializationAsync_PublishesEvent_WhenStatusChanged()
        {
            var specialization = CreateSpecialization(serviceStatus: ServiceStatuses.Active);
            SetupExistingSpecialization(specialization);
            SetupEventCapture();

            var dto = new UpdateMedicalServiceDto
            {
                Name = "Consultation",
                Price = 100m,
                CategoryName = "Consultations",
                Status = ServiceStatuses.Inactive
            };

            await CreateSut().UpdateServiceInSpecializationAsync(SpecId, ServiceId, dto);

            var captured = Assert.Single(_publishedEvents);
            Assert.Equal(SpecId, captured.SpecializationId);
            Assert.Equal("Cardiology", captured.SpecializationName);
            Assert.Equal(ServiceStatuses.Inactive, captured.Status);
            Assert.Equal(ServiceStatuses.Active, captured.OldStatus);
            Assert.Equal(SpecializationChangeTypes.ServiceStatus, captured.ChangeType);
            Assert.Equal(ServiceId, captured.ServiceId);
            Assert.Equal("Consultation", captured.ServiceName);
        }

        [Fact]
        public async Task UpdateServiceInSpecializationAsync_DoesNotPublishEvent_WhenStatusUnchanged()
        {
            var specialization = CreateSpecialization(serviceStatus: ServiceStatuses.Active);
            SetupExistingSpecialization(specialization);

            var dto = new UpdateMedicalServiceDto
            {
                Name = "Consultation",
                Price = 150m,
                CategoryName = "Consultations",
                Status = ServiceStatuses.Active
            };

            await CreateSut().UpdateServiceInSpecializationAsync(SpecId, ServiceId, dto);

            VerifyNoEventPublished();
        }

        #endregion

        #region Read operations

        [Fact]
        public async Task GetSpecializationsListAsync_ReturnsRepositoryData()
        {
            var specializations = new List<Specialization> { CreateSpecialization() };
            _repository
                .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(specializations);

            var result = await CreateSut().GetSpecializationsListAsync();

            Assert.Same(specializations, result);
        }

        [Fact]
        public async Task GetSpecializationByIdAsync_ReturnsRepositoryData()
        {
            var specialization = CreateSpecialization();
            SetupExistingSpecialization(specialization);

            var result = await CreateSut().GetSpecializationByIdAsync(SpecId);

            Assert.Same(specialization, result);
        }

        #endregion

        #region UpdateSpecializationAsync

        [Fact]
        public async Task UpdateSpecializationAsync_ReturnsFalse_WhenMissing()
        {
            _repository
                .Setup(r => r.GetByIdAsync(SpecId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Specialization?)null);

            var result = await CreateSut().UpdateSpecializationAsync(SpecId, new UpdateSpecializationDto { Name = "New" });

            Assert.False(result);
            _repository.Verify(r => r.UpdateAsync(It.IsAny<Specialization>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateSpecializationAsync_TrimsName_AndPersists()
        {
            var specialization = CreateSpecialization();
            SetupExistingSpecialization(specialization);
            var oldUpdatedAt = specialization.UpdatedAt;

            var result = await CreateSut().UpdateSpecializationAsync(SpecId, new UpdateSpecializationDto { Name = "  Neurology  " });

            Assert.True(result);
            Assert.Equal("Neurology", specialization.Name);
            Assert.True(specialization.UpdatedAt > oldUpdatedAt);
            _repository.Verify(r => r.UpdateAsync(specialization, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region AddServiceToSpecializationAsync

        [Fact]
        public async Task AddServiceToSpecializationAsync_ReturnsFalse_WhenMissing()
        {
            _repository
                .Setup(r => r.GetByIdAsync(SpecId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Specialization?)null);

            var result = await CreateSut().AddServiceToSpecializationAsync(SpecId, new CreateMedicalServiceDto());

            Assert.False(result);
        }

        [Fact]
        public async Task AddServiceToSpecializationAsync_AddsService_TrimsName_AndPersists()
        {
            var specialization = CreateSpecialization();
            SetupExistingSpecialization(specialization);

            var dto = new CreateMedicalServiceDto
            {
                Name = "  MRI  ",
                Price = 500m,
                CategoryName = "Diagnostics",
                Status = ServiceStatuses.Active
            };

            var result = await CreateSut().AddServiceToSpecializationAsync(SpecId, dto);

            Assert.True(result);
            Assert.Equal(2, specialization.Services.Count);
            var added = specialization.Services.Last();
            Assert.Equal("MRI", added.Name);
            Assert.Equal(500m, added.Price);
            Assert.Equal("Diagnostics", added.CategoryName);
            _repository.Verify(r => r.UpdateAsync(specialization, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region ChangeStatusAsync

        [Fact]
        public async Task ChangeStatusAsync_ReturnsFalse_WhenMissing()
        {
            _repository
                .Setup(r => r.GetByIdAsync(SpecId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Specialization?)null);

            var result = await CreateSut().ChangeStatusAsync(SpecId, new ChangeSpecializationStatusDto { Status = ServiceStatuses.Inactive });

            Assert.False(result);
            VerifyNoEventPublished();
        }

        [Fact]
        public async Task ChangeStatusAsync_DeactivatesAllServices_WhenInactive_AndPublishesEvent()
        {
            var specialization = CreateSpecialization(specStatus: ServiceStatuses.Active, serviceStatus: ServiceStatuses.Active);
            SetupExistingSpecialization(specialization);
            SetupEventCapture();

            var result = await CreateSut().ChangeStatusAsync(SpecId, new ChangeSpecializationStatusDto { Status = ServiceStatuses.Inactive });

            Assert.True(result);
            Assert.Equal(ServiceStatuses.Inactive, specialization.Status);
            Assert.All(specialization.Services, s => Assert.Equal(ServiceStatuses.Inactive, s.Status));

            var captured = Assert.Single(_publishedEvents);
            Assert.Equal(SpecId, captured.SpecializationId);
            Assert.Equal(ServiceStatuses.Inactive, captured.Status);
            Assert.Equal(SpecializationChangeTypes.SpecializationStatus, captured.ChangeType);
            _repository.Verify(r => r.UpdateAsync(specialization, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ChangeStatusAsync_DoesNotTouchServices_WhenActive()
        {
            var specialization = CreateSpecialization(specStatus: ServiceStatuses.Inactive, serviceStatus: ServiceStatuses.Active);
            SetupExistingSpecialization(specialization);
            SetupEventCapture();

            var result = await CreateSut().ChangeStatusAsync(SpecId, new ChangeSpecializationStatusDto { Status = ServiceStatuses.Active });

            Assert.True(result);
            Assert.Equal(ServiceStatuses.Active, specialization.Status);
            Assert.All(specialization.Services, s => Assert.Equal(ServiceStatuses.Active, s.Status));
            Assert.Equal(SpecializationChangeTypes.SpecializationStatus, Assert.Single(_publishedEvents).ChangeType);
        }

        #endregion

        #region CreateSpecializationAsync

        [Fact]
        public async Task CreateSpecializationAsync_MapsServices_TrimsNames_AndPersists()
        {
            var dto = new CreateSpecializationDto
            {
                Name = "  Cardiology  ",
                Status = ServiceStatuses.Active,
                Services = new List<CreateServiceDto>
                {
                    new() { Name = "  Consultation  ", Price = 100m, CategoryName = "Consultations", Status = ServiceStatuses.Active },
                    new() { Name = "ECG", Price = 200m, CategoryName = "Diagnostics", Status = ServiceStatuses.Inactive }
                }
            };

            var result = await CreateSut().CreateSpecializationAsync(dto);

            Assert.Equal("Cardiology", result.Name);
            Assert.Equal(ServiceStatuses.Active, result.Status);
            Assert.Equal(2, result.Services.Count);
            Assert.Equal("Consultation", result.Services[0].Name);
            Assert.Equal("ECG", result.Services[1].Name);
            Assert.Equal(200m, result.Services[1].Price);
            _repository.Verify(r => r.AddAsync(result, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}

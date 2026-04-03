using BarTasca.Data.Interfaces;
using BarTasca.Models;
using BarTasca.Services.Interfaces;

namespace BarTasca.Services.Services
{
    public class InitialAdminService : IInitialAdminService
    {
        private readonly IStaffUserRepository _staffUserRepository;
        private readonly IStaffAuthService _staffAuthService;

        public InitialAdminService(
            IStaffUserRepository staffUserRepository,
            IStaffAuthService staffAuthService)
        {
            _staffUserRepository = staffUserRepository;
            _staffAuthService = staffAuthService;
        }

        public async Task EnsureInitialAdminAsync(CancellationToken ct = default)
        {
            var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
            var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

            if (string.IsNullOrWhiteSpace(adminEmail))
                throw new InvalidOperationException("ADMIN_EMAIL not set");

            if (string.IsNullOrWhiteSpace(adminPassword))
                throw new InvalidOperationException("ADMIN_PASSWORD not set");

            var existingUser = await _staffUserRepository.GetByEmailAsync(adminEmail, ct);
            if (existingUser is not null)
                return;

            var adminUser = new StaffUser
            {
                Email = adminEmail,
                PasswordHash = _staffAuthService.HashPassword(adminPassword),
                Role = StaffRole.Admin,
                CreatedAt = DateTime.UtcNow
            };

            await _staffUserRepository.AddAsync(adminUser, ct);
            await _staffUserRepository.SaveChangesAsync(ct);
        }
    }
}
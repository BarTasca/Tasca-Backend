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

            var hasEmail = !string.IsNullOrWhiteSpace(adminEmail);
            var hasPassword = !string.IsNullOrWhiteSpace(adminPassword);

            if (!hasEmail && !hasPassword)
                return;

            if (!hasEmail || !hasPassword)
                throw new InvalidOperationException("ADMIN_EMAIL and ADMIN_PASSWORD must both be set together");

            var existingUser = await _staffUserRepository.GetByEmailAsync(adminEmail!, ct);
            if (existingUser is not null)
                return;

            var adminUser = new StaffUser
            {
                Email = adminEmail!,
                PasswordHash = _staffAuthService.HashPassword(adminPassword!),
                Role = StaffRole.Admin,
                CreatedAt = DateTime.UtcNow
            };

            await _staffUserRepository.AddAsync(adminUser, ct);
            await _staffUserRepository.SaveChangesAsync(ct);
        }
    }
}
using AnyDeskMonitor.Application.Interfaces;
using AnyDeskMonitor.Domain.Entities;
using AnyDeskMonitor.Domain.Enums;
using AnyDeskMonitor.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AnyDeskMonitor.Application.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(IApplicationDbContext db, IJwtTokenGenerator tokenGenerator, IPasswordHasher passwordHasher)
    {
        _db = db;
        _tokenGenerator = tokenGenerator;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginDto dto)
    {
        await SeedDefaultUsersAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == dto.Username.Trim().ToLower());
        if (user == null || !_passwordHasher.VerifyPassword(dto.Password, user.PasswordHash))
        {
            return null;
        }

        user.LastLogin = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var (token, expiresAt) = _tokenGenerator.GenerateToken(user);

        return new LoginResponseDto
        {
            Token = token,
            Username = user.Username,
            Role = user.Role.ToString(),
            ExpiresAt = expiresAt
        };
    }

    public async Task SeedDefaultUsersAsync()
    {
        if (!await _db.Users.AnyAsync())
        {
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                PasswordHash = _passwordHasher.HashPassword("admin123"),
                Role = UserRole.Administrator,
                CreatedAt = DateTime.UtcNow
            };

            var operatorUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "operator",
                PasswordHash = _passwordHasher.HashPassword("operator123"),
                Role = UserRole.Operator,
                CreatedAt = DateTime.UtcNow
            };

            var viewer = new User
            {
                Id = Guid.NewGuid(),
                Username = "viewer",
                PasswordHash = _passwordHasher.HashPassword("viewer123"),
                Role = UserRole.Viewer,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.AddRange(admin, operatorUser, viewer);
            await _db.SaveChangesAsync();
        }
    }
}

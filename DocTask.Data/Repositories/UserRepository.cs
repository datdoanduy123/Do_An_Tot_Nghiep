using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Models;
using Microsoft.EntityFrameworkCore;
using DocTask.Core.Dtos.Users;

namespace DocTask.Data.Repositories;

public class UserRepository : IUserRepository
{
    private ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async System.Threading.Tasks.Task<User?> GetByUserNameAsync(string username)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }

    public async System.Threading.Tasks.Task<User> UpdateRefreshToken(User user, string? refreshToken)
    {
        user.Refreshtoken = refreshToken;
        _context.Update(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async System.Threading.Tasks.Task UpdatePasswordAsync(User user, string newHashedPassword)
    {
        user.Password = newHashedPassword;
        // invalidate refresh token on password change
        user.Refreshtoken = null;
        _context.Update(user);
        await _context.SaveChangesAsync();
    }

    public async System.Threading.Tasks.Task<User?> GetByIdAsync(int userId)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
    }

    public async System.Threading.Tasks.Task<CurrentUserDto?> GetCurrentUserAsync(int userId)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.Org)
            .Include(u => u.Unit)
            .Where(u => u.UserId == userId)
            .Select(u => new CurrentUserDto
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Role = u.Role,
                PositionName = u.PositionName ?? (u.Position != null ? u.Position.PositionName : null),
                OrgName = u.Org != null ? u.Org.OrgName : null,
                UnitName = u.Unit != null ? u.Unit.UnitName : null
            })
            .FirstOrDefaultAsync();
    }

    public async System.Threading.Tasks.Task<CurrentUserDto?> UpdateCurrentUserAsync(int userId, DocTask.Core.Dtos.Users.UpdateCurrentUserRequestDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return null;

        // Update allowed fields only
        if (!string.IsNullOrWhiteSpace(dto.FullName)) user.FullName = dto.FullName;
        if (!string.IsNullOrWhiteSpace(dto.Email)) user.Email = dto.Email;
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber)) user.PhoneNumber = dto.PhoneNumber;

        _context.Users.Attach(user);
        await _context.SaveChangesAsync();

        // return updated current user DTO
        return await GetCurrentUserAsync(userId);
    }

    public async System.Threading.Tasks.Task<(System.Collections.Generic.List<UserBasicDto> subordinates, System.Collections.Generic.List<UserBasicDto> peers)> GetSubordinatesAndPeersAsync(int callerId)
    {
        var callerParent = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == callerId)
            .Select(u => u.UserParent)
            .FirstOrDefaultAsync();

        var subordinates = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserParent == callerId)
            .Select(u => new UserBasicDto
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                OrgId = u.OrgId,
                UnitId = u.UnitId,
                Role = u.Role,
            })
            .ToListAsync();

        var peers = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserParent == callerParent && u.UserId != callerId)
            .Select(u => new UserBasicDto
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                OrgId = u.OrgId,
                UnitId = u.UnitId,
                Role = u.Role,
            })
            .ToListAsync();

        return (subordinates, peers);
    }

    public Task<User?> GetByIdWithUnitUserAsync(int userId)
    {
        return _context.Users.Include(u => u.UnitUser)
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    public async Task<List<User>> GetAllByParentUserId(int userParentId)
    {
        return await _context.Users.Where(u => u.UserParent == userParentId).ToListAsync();
    }

    public async Task<int> SaveChanges()
    {
        return await _context.SaveChangesAsync();
    }

    public Task<User?> GetTokenAsync(string token)
    {
        return _context.Users.FirstOrDefaultAsync(u => u.ResetToken == token);
    }
}
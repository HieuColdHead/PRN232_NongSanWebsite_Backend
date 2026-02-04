using DAL.Data;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories;

public class EmailOtpRepository : IEmailOtpRepository
{
    private readonly ApplicationDbContext _dbContext;

    public EmailOtpRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(EmailOtp otp)
    {
        _dbContext.EmailOtps.Add(otp);
        return Task.CompletedTask;
    }

    public Task<EmailOtp?> GetLatestValidAsync(string email, DateTime nowUtc)
    {
        return _dbContext.EmailOtps
            .Where(x => x.Email == email && x.ConsumedAt == null && x.ExpiresAt > nowUtc)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public Task SaveChangesAsync() => _dbContext.SaveChangesAsync();
}

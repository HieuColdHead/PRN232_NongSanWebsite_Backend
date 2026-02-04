using DAL.Entity;

namespace DAL.Repositories.Interfaces;

public interface IEmailOtpRepository
{
    Task AddAsync(EmailOtp otp);
    Task<EmailOtp?> GetLatestValidAsync(string email, DateTime nowUtc);
    Task SaveChangesAsync();
}

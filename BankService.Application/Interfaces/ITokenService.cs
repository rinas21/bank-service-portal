namespace BankService.Application.Interfaces;

public interface ITokenService
{
    string GenerateJwtToken(string userId, string email, string userName, IEnumerable<string> roles);
    DateTime GetTokenExpiry();
}

using BankService.Domain.Entities;

namespace BankService.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(ApplicationUser user, IEnumerable<string> roles);
}

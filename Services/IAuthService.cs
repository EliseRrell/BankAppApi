using BankAppAPI.Dtos;
using Microsoft.AspNetCore.Identity;

namespace BankAppAPI.Services;

/// <summary>
/// Service interface for authentication and user management operations.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticates a user and generates a JWT token.
    /// </summary>
    Task<(bool Success, string Token, DateTime? Expires, IList<string> Roles, string Message)> LoginAsync(LoginDto model);

    /// <summary>
    /// Registers a new customer with user account and disposition.
    /// </summary>
    Task<(bool Success, string UserId, int CustomerId, int AccountId, string Message)> RegisterCustomerAsync(RegisterDto model);

    /// <summary>
    /// Issues a new loan to a customer and credits their account.
    /// </summary>
    Task<(bool Success, int? LoanId, int? AccountId, int? TransactionId, string Message)> IssueLoanAsync(IssueLoanDto model);

    /// <summary>
    /// Updates an existing user's email and/or password.
    /// </summary>
    Task<(bool Success, string Message, IEnumerable<IdentityError>? Errors)> UpdateUserAsync(string userId, UpdateUserDto model);

    /// <summary>
    /// Retrieves a user's information by user ID.
    /// </summary>
    Task<(bool Success, string UserId, string Email, IList<string> Roles, string Message)> GetUserByIdAsync(string userId);

    /// <summary>
    /// Retrieves a user's information by email address.
    /// </summary>
    Task<(bool Success, string UserId, string Email, IList<string> Roles, string Message)> GetUserByEmailAsync(string email);

    /// <summary>
    /// Retrieves complete customer information with accounts, loans, and transactions.
    /// </summary>
    Task<(bool Success, object CustomerData, string Message)> GetCustomerWithAccountsAsync(int customerId);
}
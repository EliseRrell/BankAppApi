using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BankAppAPI.Data;
using BankAppAPI.Dtos;
using BankAppAPI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BankAppAPI.Services;

/// <summary>
/// Service implementation for authentication and user management operations.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly BankAppDataContext _db;

    public AuthService(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        BankAppDataContext db)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _db = db;
    }

    public async Task<(bool Success, string Token, DateTime? Expires, IList<string> Roles, string Message)> LoginAsync(LoginDto model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
            return (false, string.Empty, null, new List<string>(), "Invalid credentials");

        var valid = await _userManager.CheckPasswordAsync(user, model.Password);
        if (!valid)
            return (false, string.Empty, null, new List<string>(), "Invalid credentials");

        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? ""));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Issuer"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return (true, tokenString, token.ValidTo, roles, "Login successful");
    }

    public async Task<(bool Success, string UserId, int CustomerId, int AccountId, string Message)> RegisterCustomerAsync(RegisterDto model)
    {
        if (string.IsNullOrWhiteSpace(model.Email))
            return (false, string.Empty, 0, 0, "Email is required");

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing != null)
            return (false, string.Empty, 0, 0, "User already exists");

        var user = new IdentityUser
        {
            UserName = model.Email,
            Email = model.Email
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
            return (false, string.Empty, 0, 0, string.Join(", ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRolesAsync(user, ["Customer"]);

        var customer = new Customer
        {
            Givenname = model.Givenname ?? "",
            Surname = model.Surname ?? "",
            Gender = string.IsNullOrWhiteSpace(model.Gender) ? "female" : model.Gender,
            Streetaddress = model.Streetaddress ?? "",
            City = model.City ?? "",
            Zipcode = model.Zipcode ?? "",
            Country = model.Country ?? "",
            CountryCode = model.CountryCode ?? "",
            Birthday = null,
            Telephonecountrycode = model.Telephonecountrycode,
            Telephonenumber = model.Telephonenumber,
            Emailaddress = model.Email,
            AspNetUserId = user.Id
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var account = new Account
        {
            Frequency = model.Frequency ?? "Monthly",
            Created = DateOnly.FromDateTime(DateTime.UtcNow),
            Balance = model.InitialBalance ?? 0m,
            AccountTypesId = model.AccountTypeId ?? 1
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        var disposition = new Disposition
        {
            CustomerId = customer.CustomerId,
            AccountId = account.AccountId,
            Type = model.DispositionType ?? "OWNER"
        };

        _db.Dispositions.Add(disposition);
        await _db.SaveChangesAsync();

        return (true, user.Id, customer.CustomerId, account.AccountId, "Customer registered successfully");
    }

    public async Task<(bool Success, int? LoanId, int? AccountId, int? TransactionId, string Message)> IssueLoanAsync(IssueLoanDto model)
    {
        if (model.Amount <= 0)
            return (false, null, null, null, "Amount must be positive");

        if (model.Duration <= 0)
            return (false, null, null, null, "Duration must be positive");

        var customer = await _db.Customers.FindAsync(model.CustomerId);
        if (customer == null)
            return (false, null, null, null, "Customer not found");

        var account = await _db.Accounts.FindAsync(model.AccountId);
        if (account == null)
            return (false, null, null, null, "Account not found");

        var linked = await _db.Dispositions.AnyAsync(d => d.CustomerId == model.CustomerId && d.AccountId == model.AccountId);
        if (!linked)
            return (false, null, null, null, "Account is not linked to the specified customer");

        var monthlyPayment = model.Payments.HasValue && model.Payments.Value > 0
            ? model.Payments.Value
            : Math.Round(model.Amount / model.Duration, 2);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var loan = new Loan
            {
                AccountId = account.AccountId,
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Amount = model.Amount,
                Duration = model.Duration,
                Payments = monthlyPayment,
                Status = model.Status ?? "Active"
            };

            _db.Loans.Add(loan);

            account.Balance += model.Amount;
            _db.Accounts.Update(account);

            var transaction = new Transaction
            {
                AccountId = account.AccountId,
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Type = model.TransactionType ?? "Credit",
                Operation = model.TransactionOperation ?? "Loan disbursement",
                Amount = model.Amount,
                Balance = account.Balance,
                Symbol = model.Symbol ?? "LOAN",
                Bank = null,
                Account = null
            };

            _db.Transactions.Add(transaction);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, loan.LoanId, account.AccountId, transaction.TransactionId, "Loan issued successfully");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return (false, null, null, null, $"Failed to issue loan: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message, IEnumerable<IdentityError>? Errors)> UpdateUserAsync(string userId, UpdateUserDto model)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return (false, "User not found", null);

        if (!string.IsNullOrWhiteSpace(model.Email) && !string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            user.Email = model.Email;
            user.UserName = model.Email;
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return (false, "Failed to update user", updateResult.Errors);

        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (!resetResult.Succeeded)
                return (false, "Failed to update password", resetResult.Errors);
        }

        return (true, "User updated successfully", null);
    }

    public async Task<(bool Success, string UserId, string Email, IList<string> Roles, string Message)> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return (false, string.Empty, string.Empty, new List<string>(), "User not found");

        var roles = await _userManager.GetRolesAsync(user);
        return (true, user.Id, user.Email ?? "", roles, "User retrieved successfully");
    }

    public async Task<(bool Success, string UserId, string Email, IList<string> Roles, string Message)> GetUserByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
            return (false, string.Empty, string.Empty, new List<string>(), "User not found");

        var roles = await _userManager.GetRolesAsync(user);
        return (true, user.Id, user.Email ?? "", roles, "User retrieved successfully");
    }

    public async Task<(bool Success, object CustomerData, string Message)> GetCustomerWithAccountsAsync(int customerId)
    {
        var customer = await _db.Customers
            .Where(c => c.CustomerId == customerId)
            .Select(c => new
            {
                c.CustomerId,
                c.Givenname,
                c.Surname,
                c.Gender,
                c.Streetaddress,
                c.City,
                c.Zipcode,
                c.Country,
                c.CountryCode,
                c.Birthday,
                c.Telephonecountrycode,
                c.Telephonenumber,
                c.Emailaddress,
                Accounts = c.Dispositions.Select(d => new
                {
                    d.Account.AccountId,
                    d.Account.Frequency,
                    d.Account.Created,
                    d.Account.Balance,
                    d.Account.AccountTypesId,
                    AccountType = d.Account.AccountTypes != null
                        ? new
                        {
                            d.Account.AccountTypes.AccountTypeId,
                            d.Account.AccountTypes.TypeName,
                            d.Account.AccountTypes.Description
                        }
                        : null,
                    DispositionType = d.Type,
                    Loans = d.Account.Loans.Select(l => new
                    {
                        l.LoanId,
                        l.Date,
                        l.Amount,
                        l.Duration,
                        l.Payments,
                        l.Status
                    }).ToList(),
                    Transactions = d.Account.Transactions.Select(t => new
                    {
                        t.TransactionId,
                        t.Date,
                        t.Type,
                        t.Operation,
                        t.Amount,
                        t.Balance,
                        t.Symbol,
                        t.Bank,
                        t.Account
                    }).ToList()
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (customer == null)
            return (false, new { }, "Customer not found");

        return (true, customer, "Customer retrieved successfully");
    }

    public async Task<(bool Success, object CustomerData, string Message)> GetMyAccountsAsync(string userId)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.AspNetUserId == userId);

        if (customer == null)
            return (false, new { }, "Customer not found");

        var accounts = await _db.Dispositions
            .Where(d => d.CustomerId == customer.CustomerId)
            .ToListAsync();

        var accountData = accounts.Select(d => new
        {
            d.Account.AccountId,
            d.Account.Frequency,
            d.Account.Created,
            d.Account.Balance,
            d.Account.AccountTypesId,
            AccountType = d.Account.AccountTypes != null
                ? new
                {
                    d.Account.AccountTypes.AccountTypeId,
                    d.Account.AccountTypes.TypeName,
                    d.Account.AccountTypes.Description
                }
                : null,
            DispositionType = d.Type,
            Loans = d.Account.Loans.Select(l => new
            {
                l.LoanId,
                l.Date,
                l.Amount,
                l.Duration,
                l.Payments,
                l.Status
            }).ToList(),
            Transactions = d.Account.Transactions.Select(t => new
            {
                t.TransactionId,
                t.Date,
                t.Type,
                t.Operation,
                t.Amount,
                t.Balance,
                t.Symbol,
                t.Bank,
                t.Account
            }).ToList()
        }).ToList();

        return (true, new { customer, accounts = accountData }, "Accounts retrieved successfully");
    }
}
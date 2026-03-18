using System.Security.Claims;
using BankAppAPI.Data;
using BankAppAPI.Dtos;
using BankAppAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankAppAPI.Controllers;

// Controller for managing customer bank accounts.
// Handles account retrieval, creation, transactions, and money transfers for authenticated customers.
// All endpoints require authentication with the "Customer" role unless otherwise specified.

[ApiController]
[Route("api/[controller]")]
//[Authorize(Roles = "Customer")] // Kräver inloggning av Customer för alla i hela klassen
public class CustomerAccountsController : ControllerBase
{
    private readonly BankAppDataContext _db;

    /// <summary>
    /// Initializes a new instance of the CustomerAccountsController.
    /// </summary>
    /// <param name="db">The database context for bank application data.</param>
    public CustomerAccountsController(BankAppDataContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves all bank accounts for customer.
    /// </summary>
    /// <returns>
    /// An object containing customer information, total account count, total balance,
    /// and a list of all accounts with their details.
    /// </returns>
    // GET: api/CustomerAccounts
    // Hämtar alla konton för den inloggade kunden
    [HttpGet]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetMyAccounts()
    {
        // Fetch the userId from JWT token
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        // Finds the customer linked to the user
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.AspNetUserId == userId);

        if (customer == null)
            return NotFound(new { message = "Customer profile not found" });

        // Fetch all accounts linked to the customer via Dispositions table
        var accounts = await _db.Dispositions
            .Where(d => d.CustomerId == customer.CustomerId) //Every disposition for this customer
            .Select(d => new  //For every disposition, select account details
            {
                AccountId = d.AccountId,
                AccountType = d.Account.AccountTypes != null 
                    ? d.Account.AccountTypes.TypeName 
                    : "Unknown",
                AccountTypeDescription = d.Account.AccountTypes != null 
                    ? d.Account.AccountTypes.Description 
                    : null,
                Balance = d.Account.Balance,
                Frequency = d.Account.Frequency,
                Created = d.Account.Created,
                DispositionType = d.Type // OWNER eller DISPONENT
            })
            .OrderByDescending(a => a.Created)  //Sort by creation date (newest first)
            .ToListAsync();

        return Ok(new
        {
            CustomerId = customer.CustomerId,
            CustomerName = $"{customer.Givenname} {customer.Surname}",      
            TotalAccounts = accounts.Count,
            TotalBalance = accounts.Sum(a => a.Balance),
            Accounts = accounts
        });
    }

    /// <summary>
    /// Retrieves all transactions for a specific account.
    /// </summary>
    /// <param name="accountId">The ID of the account to retrieve transactions for.</param>
    /// <param name="page">The page number (default is 1).</param>
    /// <param name="pageSize">The number of transactions per page (default is 20).</param>
    /// <returns>
    /// Customer account details along with paginated list of transactions.
    /// </returns>
    
    // GET: api/CustomerAccounts/{accountId}/transactions
    [HttpGet("{accountId}/transactions")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetAccountTransactions(int accountId, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20)
    {
        // Fetch the userId from JWT token
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        // Fetch the customer linked to the identityuser
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.AspNetUserId == userId);

        if (customer == null)
            return NotFound(new { message = "Customer profile not found" });

        // Verify that the customer has access to the specified account (owner or disponent)
        var hasAccess = await _db.Dispositions
            .AnyAsync(d => d.CustomerId == customer.CustomerId && d.AccountId == accountId);

        if (!hasAccess)
            return Forbid(); // Customer does not own the account

        // Fetch account details
        var account = await _db.Accounts
            .Where(a => a.AccountId == accountId)
            .Select(a => new
            {
                a.AccountId,
                AccountType = a.AccountTypes != null ? a.AccountTypes.TypeName : "Unknown",
                a.Balance,
                a.Created,
                a.Frequency
            })
            .FirstOrDefaultAsync();

        if (account == null)
            return NotFound(new { message = "Account not found" });

        // Count total transactions for the pagin
        var totalTransactions = await _db.Transactions
            .CountAsync(t => t.AccountId == accountId);

        // Fetch paginated transactions
        var transactions = await _db.Transactions
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.TransactionId)
            .Skip((page - 1) * pageSize)  //Where to start
            .Take(pageSize)
            .Select(t => new
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
            })
            .ToListAsync();

        return Ok(new
        {
            Account = account,
            Pagination = new
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalTransactions = totalTransactions,
                TotalPages = (int)Math.Ceiling(totalTransactions / (double)pageSize)
            },
            Transactions = transactions
        });
    }

    /// <summary>
    /// Retrieves detailed information for a specific account.
    /// </summary>
    /// <param name="accountId">The ID of the account to retrieve details for.</param>
    /// <returns>
    /// Fetches detailed account information including account type, balance, disposition type. 
    /// Also includes total transactions, active loans, and recent transactions.
    /// </returns>
    // GET: api/CustomerAccounts/{accountId}

    [HttpGet("{accountId}")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetAccountDetails(int accountId)
    {
        // Gets the userId from JWT token
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        // Find the customer linked to the user
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.AspNetUserId == userId);

        if (customer == null)
            return NotFound(new { message = "Customer profile not found" });

        // Get account details if the customer has access
        var accountDetails = await _db.Dispositions
            .Where(d => d.CustomerId == customer.CustomerId && d.AccountId == accountId)
            .Select(d => new
            {
                d.AccountId,
                AccountType = d.Account.AccountTypes != null 
                    ? new 
                    {
                        d.Account.AccountTypes.TypeName,
                        d.Account.AccountTypes.Description
                    }
                    : null,
                d.Account.Balance,
                d.Account.Frequency,
                d.Account.Created,
                DispositionType = d.Type,
                TotalTransactions = d.Account.Transactions.Count,
                ActiveLoans = d.Account.Loans
                    .Where(l => l.Status == "Active")
                    .Select(l => new
                    {
                        l.LoanId,
                        l.Amount,
                        l.Duration,
                        l.Payments,
                        l.Date,
                        l.Status
                    })
                    .ToList(),
                RecentTransactions = d.Account.Transactions
                    .OrderByDescending(t => t.Date)
                    .ThenByDescending(t => t.TransactionId)
                    .Take(5)
                    .Select(t => new
                    {
                        t.TransactionId,
                        t.Date,
                        t.Type,
                        t.Operation,
                        t.Amount,
                        t.Balance
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (accountDetails == null)
            return NotFound(new { message = "Account not found or you don't have access" });

        return Ok(accountDetails);
    }

    /// <summary>
    /// Creates a new bank account.
    /// </summary>
    /// <returns>The newly created account details.</returns>
    /// <remarks>
    /// Creates a new account and links it to the customer as the owner with zero initial balance. Also uses a transaction to ensure data consistency.
    /// </remarks>
    // POST: api/CustomerAccounts/create
    [HttpPost("create")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountDto model)
    {
        // Get the userId from JWT token
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        // Find the customer linked to the user
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.AspNetUserId == userId);

        if (customer == null)
            return NotFound(new { message = "Customer profile not found" });

        //Validate account type
        var accountType = await _db.AccountTypes.FindAsync(model.AccountTypeId);
        if (accountType == null)
            return BadRequest(new { message = "Invalid account type" });

        // Use a transaction to ensure data consistency
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Create new account with zero balance for the customer 
            var account = new Account
            {
                Frequency = model.Frequency,
                Created = DateOnly.FromDateTime(DateTime.UtcNow),
                Balance = 0m,
                AccountTypesId = model.AccountTypeId
            };

            _db.Accounts.Add(account);
            await _db.SaveChangesAsync();

            // Link the new account to the customer with owner disposition
            var disposition = new Disposition
            {
                CustomerId = customer.CustomerId,
                AccountId = account.AccountId,
                Type = "OWNER"
            };

            _db.Dispositions.Add(disposition);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return CreatedAtAction(nameof(GetAccountDetails), new { accountId = account.AccountId }, new
            {
                AccountId = account.AccountId,
                AccountType = accountType.TypeName,
                Balance = account.Balance,
                Created = account.Created,
                Frequency = account.Frequency,
                Message = "Account created successfully"
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(500, new { message = "Failed to create account", detail = ex.Message });
        }
    }

    /// <summary>
    /// Transfers money between bank accounts.
    /// </summary>
    /// <remarks>
    /// Endpoint that creates two transaction records: a debit for the source account and a credit for the destination account.
    /// <param>The destination account does not need to be owned by the authenticated customer.</param>
    /// <param>The response indicates whether the transfer is the same customer or a different customer.</param>
    /// </remarks>
    // POST: api/CustomerAccounts/transfer
    [HttpPost("transfer")]
    [Authorize(Roles = "Customer")] // Requires Customer role
    public async Task<IActionResult> TransferMoney([FromBody] TransferDto model)
    {
        // Get the userId from JWT token
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        // Find the customer linked to the user
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.AspNetUserId == userId);

        if (customer == null)
            return NotFound(new { message = "Customer profile not found" });

        // Validate transfer details ( "from" and "to" accounts must be different)
        if (model.FromAccountId == model.ToAccountId)
            return BadRequest(new { message = "Cannot transfer to the same account" });

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Fetch from-account and include dispositions to check ownership
            var fromAccount = await _db.Accounts
                .Include(a => a.Dispositions)
                .FirstOrDefaultAsync(a => a.AccountId == model.FromAccountId);

            if (fromAccount == null)
                return NotFound(new { message = "Source account not found" });

            //checks if the customer owns the from-account to authorize the transfer
            var hasAccessToFrom = fromAccount.Dispositions
                .Any(d => d.CustomerId == customer.CustomerId);

            if (!hasAccessToFrom)
                return Forbid();    // If the customer does not own the from-account

            // Gets the to-account (dosent need to belong to the customer)
            var toAccount = await _db.Accounts.FindAsync(model.ToAccountId);
            if (toAccount == null)
                return NotFound(new { message = "Destination account not found" });

            // Check for sufficient funds in the from-account
            if (fromAccount.Balance < model.Amount)
                return BadRequest(new { message = "Insufficient funds" });

            // Create the transfer by updating balances
            fromAccount.Balance -= model.Amount;
            toAccount.Balance += model.Amount;

            _db.Accounts.Update(fromAccount);
            _db.Accounts.Update(toAccount);

            // Creates transaction for from-account (Debit)
            var fromTransaction = new Transaction
            {
                AccountId = fromAccount.AccountId,
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Type = "Debit",
                Operation = "Transfer out",
                Amount = model.Amount,
                Balance = fromAccount.Balance,
                Symbol = "TRANSFER",
                Bank = null,
                Account = model.ToAccountId.ToString()
            };

            _db.Transactions.Add(fromTransaction); // Adds the debit transaction

            // Creates transaction for to-account (Credit)
            var toTransaction = new Transaction
            {
                AccountId = toAccount.AccountId,
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Type = "Credit",
                Operation = "Transfer in",
                Amount = model.Amount,
                Balance = toAccount.Balance,
                Symbol = "TRANSFER",
                Bank = null,
                Account = model.FromAccountId.ToString()
            };

            _db.Transactions.Add(toTransaction);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // Check if the to-account also belongs to the same customer
            var isOwnAccount = await _db.Dispositions
                .AnyAsync(d => d.CustomerId == customer.CustomerId && d.AccountId == model.ToAccountId);

            return Ok(new
            {
                Message = "Transfer completed successfully",
                FromAccountId = fromAccount.AccountId,
                ToAccountId = toAccount.AccountId,
                Amount = model.Amount,
                NewBalance = fromAccount.Balance,
                TransferType = isOwnAccount ? "Internal" : "External",
                FromTransactionId = fromTransaction.TransactionId,
                ToTransactionId = toTransaction.TransactionId,
                Date = DateTime.UtcNow
            });
        }
        catch (Exception ex) // If any error occurs, rollback the transaction
        {
            await tx.RollbackAsync();
            return StatusCode(500, new { message = "Transfer failed", detail = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves all available account types offered.
    /// </summary>
    /// <returns>A list of all account types with their IDs, names, and descriptions.</returns>
    /// <remarks>
    /// Endpoint that requires authentication but does not require a specific role.
    /// It provides information about bank acount types.
    /// </remarks>
    // GET: api/CustomerAccounts/available-account-types
    [HttpGet("available-account-types")]
    [Authorize]  // Only requires authentication, no specific role
    public async Task<IActionResult> GetAvailableAccountTypes()
    {
        var accountTypes = await _db.AccountTypes
            .Select(at => new
            {
                at.AccountTypeId,
                at.TypeName,
                at.Description
            })
            .ToListAsync();

        return Ok(accountTypes);
    }
}
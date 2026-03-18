using BankAppAPI.Dtos;
using BankAppAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankAppAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Authenticates a user, generates a JWT token.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto model)
    {
        var result = await _authService.LoginAsync(model);

        if (!result.Success)
            return Unauthorized(new { message = result.Message });

        return Ok(new
        {
            token = result.Token,
            expires = result.Expires,
            roles = result.Roles
        });
    }

    /// <summary>
    /// Registers a new customer with user/bank account and disposition.
    /// </summary>
    [HttpPost("register_new_customer")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterDto model)
    {
        var result = await _authService.RegisterCustomerAsync(model);

        if (!result.Success)
        {
            if (result.Message.Contains("already exists"))
                return Conflict(new { message = result.Message });
            return BadRequest(new { message = result.Message });
        }

        return CreatedAtAction(nameof(GetUser), new { id = result.UserId }, new
        {
            UserId = result.UserId,
            CustomerId = result.CustomerId,
            AccountId = result.AccountId,
            Message = result.Message
        });
    }

    /// <summary>
    /// Issues a new loan to a customer.
    /// </summary>
    [HttpPost("issue/loan")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> IssueLoan([FromBody] IssueLoanDto model)
    {
        var result = await _authService.IssueLoanAsync(model);

        if (!result.Success)
        {
            if (result.Message.Contains("not found"))
                return NotFound(new { message = result.Message });
            return BadRequest(new { message = result.Message });
        }

        return Ok(new
        {
            LoanId = result.LoanId,
            AccountId = result.AccountId,
            TransactionId = result.TransactionId,
            Message = result.Message
        });
    }

    /// <summary>
    /// Updates an existing user's email and/or password.
    /// </summary>
    [HttpPut("user/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserDto model)
    {
        var result = await _authService.UpdateUserAsync(id, model);

        if (!result.Success)
        {
            if (result.Message.Contains("not found"))
                return NotFound(new { message = result.Message });
            return BadRequest(new { message = result.Message, errors = result.Errors });
        }

        return NoContent();
    }

    /// <summary>
    /// Retrieves a user's basic information including their role.
    /// </summary>
    [HttpGet("user/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUser(string id)
    {
        var result = await _authService.GetUserByIdAsync(id);

        if (!result.Success)
            return NotFound(new { message = result.Message });

        return Ok(new { result.UserId, result.Email, result.Roles });
    }

    /// <summary>
    /// Retrieves a user's basic information by email.
    /// </summary>
    [HttpGet("userByEmail/{email}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUserByEmail(string email)
    {
        var result = await _authService.GetUserByEmailAsync(email);

        if (!result.Success)
            return NotFound(new { message = result.Message });

        return Ok(new { result.UserId, result.Email, result.Roles });
    }

    /// <summary>
    /// Retrieves customer information with accounts, loans, and transactions.
    /// </summary>
    [HttpGet("customers/{customerId}/full")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetCustomerWithAccounts(int customerId)
    {
        var result = await _authService.GetCustomerWithAccountsAsync(customerId);

        if (!result.Success)
            return NotFound(new { message = result.Message });

        return Ok(result.CustomerData);
    }
}
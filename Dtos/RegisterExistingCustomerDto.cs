using System.ComponentModel.DataAnnotations;

namespace BankAppAPI.Dtos;


// Registering an existing customer with Identity user credentials.
public class RegisterExistingCustomerDto
{
 


    // Email address for the new user account.
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// Password for the new user account.
    [Required]
    [MinLength(4)]
    public string Password { get; set; } = string.Empty;
}
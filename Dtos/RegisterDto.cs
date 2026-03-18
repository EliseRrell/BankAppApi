
namespace BankAppAPI.Dtos;

public class RegisterDto
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;

    // Customer info (minimal; non-nullable Customer fields are provided defaults if omitted)
    public string Givenname { get; set; } = "";
    public string Surname { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Streetaddress { get; set; } = "";
    public string City { get; set; } = "";
    public string Zipcode { get; set; } = "";
    public string Country { get; set; } = "";
    public string CountryCode { get; set; } = "";

    public string? Telephonecountrycode { get; set; }
    public string? Telephonenumber { get; set; }

    // Account choice
    // If null, AccountTypesId will be left null (you may map to a default account type elsewhere)
    public int? AccountTypeId { get; set; }

    // Optional account settings
    public string? Frequency { get; set; } = "Monthly";
    public decimal? InitialBalance { get; set; } = 0m;

    // Disposition type linking customer->account (e.g. "OWNER")
    public string? DispositionType { get; set; } = "OWNER";
}
using System.ComponentModel.DataAnnotations;

namespace BankAppAPI.Dtos;

public class CreateAccountDto
{
    [Required]
    public int AccountTypeId { get; set; }

    [StringLength(50)]
    public string Frequency { get; set; } = "Monthly";

}
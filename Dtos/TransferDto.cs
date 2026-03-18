using System.ComponentModel.DataAnnotations;

namespace BankAppAPI.Dtos;

public class TransferDto
{
    [Required]
    public int FromAccountId { get; set; }

    [Required]
    public int ToAccountId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [StringLength(100)]
    public string? Description { get; set; }
}
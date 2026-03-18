
namespace BankAppAPI.Dtos;

public class IssueLoanDto
{
    // The customer who receives the loan
    public int CustomerId { get; set; }

    // The account to credit with the loan amount (must be linked to the customer)
    public int AccountId { get; set; }

    // Loan amount (positive)
    public decimal Amount { get; set; }

    // Duration in months (positive)
    public int Duration { get; set; }

    // Optional: payments (monthly). If omitted, will be calculated as Amount / Duration.
    public decimal? Payments { get; set; }

    // Optional: initial status (defaults to "Active")
    public string? Status { get; set; } = "Active";

    // Optional transaction metadata
    public string? TransactionType { get; set; } = "Credit";
    public string? TransactionOperation { get; set; } = "Loan disbursement";
    public string? Symbol { get; set; } = "LOAN";
}
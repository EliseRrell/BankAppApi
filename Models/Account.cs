using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BankAppAPI.Models;

public partial class Account
{
    [Key]
    public int AccountId { get; set; }

    [StringLength(50)]
    public string Frequency { get; set; } = null!;

    public DateOnly Created { get; set; }

    [Column(TypeName = "decimal(13, 2)")]
    public decimal Balance { get; set; }

    public int? AccountTypesId { get; set; }

    [ForeignKey("AccountTypesId")]
    [InverseProperty("Accounts")]
    public virtual AccountType? AccountTypes { get; set; }

    [InverseProperty("Account")]
    public virtual ICollection<Disposition> Dispositions { get; set; } = new List<Disposition>();

    [InverseProperty("Account")]
    public virtual ICollection<Loan> Loans { get; set; } = new List<Loan>();

    [InverseProperty("AccountNavigation")]
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

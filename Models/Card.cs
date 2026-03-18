using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BankAppAPI.Models;

public partial class Card
{
    [Key]
    public int CardId { get; set; }

    public int DispositionId { get; set; }

    [StringLength(50)]
    public string Type { get; set; } = null!;

    public DateOnly Issued { get; set; }

    [StringLength(50)]
    public string CCType { get; set; } = null!;

    [StringLength(50)]
    public string CCNumber { get; set; } = null!;

    [StringLength(10)]
    public string CVV2 { get; set; } = null!;

    public int ExpM { get; set; }

    public int ExpY { get; set; }

    [ForeignKey("DispositionId")]
    [InverseProperty("Cards")]
    public virtual Disposition Disposition { get; set; } = null!;
}

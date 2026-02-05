using System.ComponentModel.DataAnnotations;   // ← aggiungi questa riga
using System;
namespace MiniServiceDesk.Api.models;

public enum TicketPriority
{
    Low=0,
    Medium=1,
    High=2,
    Critical=3
}

public enum TicketStatus
{
    Open=0,
    InProgress=1,
    Waiting=2,
    Resolved=3,
    Closed=4
}
    public class Ticket
{
    public int Id{get;set;}

    [Required]
    [MinLength(4)]
    [MaxLength(120)]
    public string Title{get;set;}=String.Empty;

    [Required]
    [MinLength(10)]
    [MaxLength(4000)]
    public string Description{get;set;}=String.Empty;

    [MaxLength(60)]
    public string Category {get;set;}="IT";

    public TicketPriority Priority{get;set;}=TicketPriority.Medium;

    public TicketStatus Status{get;set;}=TicketStatus.Open;

    public DateTime CreatedAt{get;set;}=DateTime.UtcNow;

    public DateTime UpdatedAt{get;set;}=DateTime.UtcNow;
}
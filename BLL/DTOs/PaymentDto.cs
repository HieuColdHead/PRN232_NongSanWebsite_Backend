namespace BLL.DTOs;

public class PaymentDto
{
    public int PaymentId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentStatus { get; set; }
    public DateTime? PaidAt { get; set; }
    public int OrderId { get; set; }
}

public class CreatePaymentRequest
{
    public string? PaymentMethod { get; set; }
    public int OrderId { get; set; }
}

public class TransactionDto
{
    public int TransactionId { get; set; }
    public string? TransactionCode { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Gateway { get; set; }
    public string? Status { get; set; }
    public int PaymentId { get; set; }
}

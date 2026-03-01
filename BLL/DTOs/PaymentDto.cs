namespace BLL.DTOs;

public class PaymentDto
{
    public Guid PaymentId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentStatus { get; set; }
    public DateTime? PaidAt { get; set; }
    public Guid OrderId { get; set; }
}

public class CreatePaymentRequest
{
    public string? PaymentMethod { get; set; }
    public Guid OrderId { get; set; }
}

public class TransactionDto
{
    public Guid TransactionId { get; set; }
    public string? TransactionCode { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Gateway { get; set; }
    public string? Status { get; set; }
    public Guid PaymentId { get; set; }
}

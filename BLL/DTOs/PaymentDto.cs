namespace BLL.DTOs;

public class PaymentDto
{
    public Guid PaymentId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentStatus { get; set; }
    public DateTime? PaidAt { get; set; }
    public decimal? CodAmount { get; set; }
    public Guid OrderId { get; set; }
}

public class CreatePaymentRequest
{
    public string? PaymentMethod { get; set; }
    public decimal? CodAmount { get; set; }
    public Guid OrderId { get; set; }
}

public class CreateVnPayUrlRequest
{
    public Guid OrderId { get; set; }
    public string? ClientIp { get; set; }
}

public class VnPayCreateUrlResponse
{
    public Guid OrderId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string? TxnRef { get; set; }
    public string? PaymentUrl { get; set; }
}

public class VnPayReturnResult
{
    public bool SignatureValid { get; set; }
    public bool PaymentSuccess { get; set; }
    public string? Message { get; set; }
    public string? ResponseCode { get; set; }
    public string? TransactionStatus { get; set; }
    public string? TxnRef { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? PaymentId { get; set; }
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

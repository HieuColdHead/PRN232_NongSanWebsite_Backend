using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public class CreateWishlistRequest
{
    [Required]
    public Guid ProductId { get; set; }
}

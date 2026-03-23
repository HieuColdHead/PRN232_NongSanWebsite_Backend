using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public class CreateWishlistRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid ProductId { get; set; }
}

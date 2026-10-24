using System.ComponentModel.DataAnnotations;

namespace ModelViews.Contact;

public sealed class ContactRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = "";
    [Required, RegularExpression(@"^(0\d{9}|\+84\d{9})$")]
    public string Phone { get; set; } = "";
    [EmailAddress, StringLength(150)]
    public string? Email { get; set; }
    [StringLength(1000)]
    public string Message { get; set; } = "";
    [Range(typeof(bool), "true", "true")]
    public bool Consent { get; set; }
    [StringLength(200)]
    public string? Website { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using GSO_Library.Data;

namespace GSO_Library.Dtos;

public class LoginRequest
{
    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [JsonConverter(typeof(RawStringJsonConverter))]
    public string Password { get; set; } = string.Empty;
}

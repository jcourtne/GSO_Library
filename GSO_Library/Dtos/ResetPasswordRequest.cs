using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using GSO_Library.Data;

namespace GSO_Library.Dtos;

public class ResetPasswordRequest
{
    [Required]
    [JsonConverter(typeof(RawStringJsonConverter))]
    public string NewPassword { get; set; } = string.Empty;
}

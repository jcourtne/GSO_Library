using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using GSO_Library.Data;

namespace GSO_Library.Dtos;

public class UpdateCredentialsRequest
{
    [EmailAddress]
    [StringLength(256)]
    public string? Email { get; set; }

    [StringLength(100)]
    [JsonConverter(typeof(RawStringJsonConverter))]
    public string? CurrentPassword { get; set; }

    [StringLength(100, MinimumLength = 8)]
    [JsonConverter(typeof(RawStringJsonConverter))]
    public string? NewPassword { get; set; }
}

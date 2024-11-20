using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace oed_authz.Models;
public class CloudEvent
{
    /// <summary>
    /// Gets or sets the id of the event.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the specification version of the event.
    /// </summary>
    [JsonPropertyName("specversion")]
    public string SpecVersion { get; set; } = "1.0";

    /// <summary>
    /// Gets or sets the source of the event.
    /// </summary>
    [JsonPropertyName("source")]
    public Uri Source { get; set; } = new Uri("urn:undefined");
    
    /// <summary>
    /// Gets or sets the source of the event.
    /// </summary>
    [JsonPropertyName("time")]
    public DateTimeOffset Time { get; set; }
    
    /// <summary>
    /// Gets or sets the type of the event.
    /// </summary>
    [JsonPropertyName("type")]
    [Required]
    public string Type { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the service resource of the event defining authorization.
    /// </summary>
    [JsonPropertyName("resource")]
    public string? Resource { get; set; } = null!;

    /// <summary>
    /// Gets or sets the service resource instance of the event defining authorization.
    /// </summary>
    [JsonPropertyName("resourceInstance"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ResourceInstance { get; set; } = null;
    
    /// <summary>
    /// Gets or sets the subject of the event.
    /// </summary>
    [JsonPropertyName("subject"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Subject { get; set; } = null;

    /// <summary>
    /// Gets or sets the alternative subject of the event.
    /// </summary>
    [JsonPropertyName("alternativesubject"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? AlternativeSubject { get; set; }

    /// <summary>
    /// Gets or sets the cloudEvent data content. The event payload.
    /// The payload depends on the type and the dataschema.
    /// </summary>
    [JsonPropertyName("data"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object Data { get; set; } = new();

    /// <summary>
    /// Gets or sets the cloudEvent dataschema attribute.
    /// A link to the schema that the data attribute adheres to.
    /// </summary>
    [JsonPropertyName("dataschema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Uri? DataSchema { get; set; }

    /// <summary>
    /// Gets or sets the cloudEvent datacontenttype attribute.
    /// Content type of the data attribute value.
    /// </summary>
    [JsonPropertyName("contenttype"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? DataContentType { get; set; }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("events")]
public class Event
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("creation_timestamp")]
    public DateTime CreationTimestamp { get; set; }

    [Column("event_start_date")]
    public DateTime EventStartDate { get; set; }

    [Column("event_end_date")]
    public DateTime EventEndDate { get; set; }

    [Column("enlisting_start_date")]
    public DateTime EnlistingStartDate { get; set; }

    [Column("enlisting_end_date")]
    public DateTime EnlistingEndDate { get; set; }

    [Column("allowed_anonymous_enlisting")]
    public bool AllowedAnonymousEnlisting { get; set; }

    [Column("allow_anonymous_streaming")]
    public bool AllowAnonymousStreaming { get; set; }

    [Column("searchable")]
    public bool Searchable { get; set; }

    [Column("title")]
    [MaxLength(200)]
    public string? Title { get; set; }

    [Column("description")]
    [MaxLength(2000)]
    public string? Description { get; set; }
}

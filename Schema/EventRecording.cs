using System;
using System.ComponentModel.DataAnnotations.Schema;

[Table("event_recordings")]
public class EventRecording
{
    [Column("id")]
    public int Id { get; set; }

    [Column("event_id")]
    public int EventId { get; set; }

    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;

    [Column("duration_seconds")]
    public int DurationSeconds { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("completed")]
    public bool Completed { get; set; }
}

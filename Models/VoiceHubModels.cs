using System;
using System.Text.Json.Serialization;

namespace VoiceHubLanDesktop.Models;

public sealed class Song
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = string.Empty;

    [JsonPropertyName("requester")]
    public string Requester { get; set; } = string.Empty;

    [JsonPropertyName("voteCount")]
    public int VoteCount { get; set; }

    [JsonPropertyName("cover")]
    public string? Cover { get; set; }
}

public sealed class SongItem
{
    [JsonPropertyName("playDate")]
    public string PlayDate { get; set; } = string.Empty;

    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }

    [JsonPropertyName("song")]
    public Song Song { get; set; } = new Song();

    public DateTime GetPlayDate()
    {
        if (string.IsNullOrWhiteSpace(PlayDate))
        {
            return DateTime.MinValue;
        }

        if (DateTime.TryParseExact(PlayDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime result))
        {
            return result;
        }

        return DateTime.MinValue;
    }
}

public enum ComponentState
{
    Loading,
    Normal,
    NetworkError,
    NoSchedule
}

public sealed class DisplayData
{
    public ComponentState State { get; set; }
    public System.Collections.Generic.List<SongItem> Songs { get; set; } = [];
    public DateTime? DisplayDate { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

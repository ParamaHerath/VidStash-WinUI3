using System.Text.RegularExpressions;
using VidStash.Helpers;
using VidStash.Models;

namespace VidStash.Services;

public partial class ParserService
{
    private static readonly string[] ResolutionPatterns =
        ["2160p", "1080p", "720p", "480p", "4K", "UHD", "HD", "SD"];

    private static readonly string[] SourcePatterns =
        ["BluRay", "BDRip", "BRRip", "WEB-DL", "WEBRip", "HDTV", "DVDRip", "HDRip", "CAM", "TS"];

    private static readonly string[] CodecPatterns =
        ["x264", "x265", "HEVC", "H\\.264", "H\\.265", "AVC", "XviD", "DivX", "10bit"];

    private static readonly string[] AudioPatterns =
        ["AAC", "AC3", "DTS-HD", "DTS", "FLAC", "MP3", "TrueHD", "Atmos", "DD5\\.1", "5\\.1", "7\\.1"];

    private static readonly string[] EditionPatterns =
        ["REMUX", "PROPER", "REPACK", "EXTENDED", "UNRATED", "DIRECTORS\\.?CUT",
         "THEATRICAL", "IMAX", "3D", "HDR10\\+?", "HDR", "DV", "SDR"];

    private static readonly string[] LanguagePatterns =
        ["MULTI", "DUAL", "DUBBED", "SUBBED"];

    private static readonly string[] StreamingPatterns =
        ["NF", "AMZN", "DSNP", "HMAX", "ATVP"];

    private static readonly string[] ReleaseGroupPatterns =
        ["YTS", "YIFY", "RARBG", "EVO", "SPARKS", "GECKOS", "FGT", "ETRG", "PSA",
         "MkvCage", "Tigole", "QxR", "UTR", "ION10", "CMRG", "NTG", "SiGMA",
         "NOGRP", "SHITBOX", "GUACAMOLE", "HQMUX", "DEMAND", "FLUX", "RMTeam"];

    private static readonly Regex YearRegex = GenerateYearRegex();
    private static readonly Regex BracketsRegex = GenerateBracketsRegex();
    private static readonly Regex ParensRegex = GenerateParensRegex();
    private static readonly Regex MultiSpaceRegex = GenerateMultiSpaceRegex();

    private static readonly Regex[] EpisodePatterns =
    [
        EpisodePatternSE(),      // S01E01
        EpisodePatternSdotE(),   // S01.E01
        EpisodePatternXformat(), // 2x05
        EpisodePatternSpelled(), // Season 1 Episode 1
        EpisodePatternSpace()    // S01 E01
    ];

    [GeneratedRegex(@"[\.\s\(\[\-]((?:19|20)\d{2})[\.\s\)\]\-]", RegexOptions.None)]
    private static partial Regex GenerateYearRegex();

    [GeneratedRegex(@"\[.*?\]", RegexOptions.None)]
    private static partial Regex GenerateBracketsRegex();

    [GeneratedRegex(@"\(.*?\)", RegexOptions.None)]
    private static partial Regex GenerateParensRegex();

    [GeneratedRegex(@"\s{2,}", RegexOptions.None)]
    private static partial Regex GenerateMultiSpaceRegex();

    [GeneratedRegex(@"S(\d{1,2})E(\d{1,3})", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodePatternSE();

    [GeneratedRegex(@"S(\d{1,2})\.E(\d{1,3})", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodePatternSdotE();

    [GeneratedRegex(@"(?<=[\.\s\-])(\d{1,2})x(\d{2,3})", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodePatternXformat();

    [GeneratedRegex(@"Season\s*(\d{1,2})\s*Episode\s*(\d{1,3})", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodePatternSpelled();

    [GeneratedRegex(@"S(\d{1,2})\s+E(\d{1,3})", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodePatternSpace();

    public ParseResult Parse(string filePath)
    {
        var filename = Path.GetFileNameWithoutExtension(filePath);
        var result = new ParseResult { OriginalFilename = Path.GetFileName(filePath) };

        // Try TV episode first
        var tvResult = TryParseTvEpisode(filename);
        if (tvResult != null)
        {
            tvResult.OriginalFilename = result.OriginalFilename;
            return tvResult;
        }

        // Fall back to movie
        return ParseMovie(filename, result);
    }

    private ParseResult? TryParseTvEpisode(string filename)
    {
        foreach (var pattern in EpisodePatterns)
        {
            var match = pattern.Match(filename);
            if (!match.Success) continue;

            int season = int.Parse(match.Groups[1].Value);
            int episode = int.Parse(match.Groups[2].Value);

            if (season > 50 || episode > 500) continue;

            var seriesTitle = filename[..match.Index];
            var afterEpisode = filename[(match.Index + match.Length)..];

            var episodeTitle = ExtractEpisodeTitle(afterEpisode);

            seriesTitle = CleanTitle(seriesTitle);
            var year = ExtractYear(ref seriesTitle);

            return new ParseResult
            {
                Type = MediaType.TvEpisode,
                Title = StringHelpers.ToTitleCase(seriesTitle),
                Year = year,
                Season = season,
                Episode = episode,
                EpisodeTitle = string.IsNullOrWhiteSpace(episodeTitle) ? null : StringHelpers.ToTitleCase(episodeTitle)
            };
        }

        return null;
    }

    private ParseResult ParseMovie(string filename, ParseResult result)
    {
        result.Type = MediaType.Movie;
        var title = filename;

        result.Year = ExtractYear(ref title);
        title = CleanTitle(title);
        result.Title = StringHelpers.ToTitleCase(title);

        return result;
    }

    private static int? ExtractYear(ref string title)
    {
        // Pad for regex boundary matching
        var padded = " " + title + " ";
        var match = YearRegex.Match(padded);
        if (match.Success)
        {
            int year = int.Parse(match.Groups[1].Value);
            // Take everything before the year
            int yearPosInOriginal = match.Index - 1; // adjust for padding
            if (yearPosInOriginal >= 0 && yearPosInOriginal <= title.Length)
            {
                title = title[..yearPosInOriginal];
            }
            return year;
        }
        return null;
    }

    private static string CleanTitle(string title)
    {
        title = RemoveJunkPatterns(title);
        title = BracketsRegex.Replace(title, " ");
        title = ParensRegex.Replace(title, " ");
        title = title.Replace('.', ' ').Replace('_', ' ');
        title = title.Replace(" - ", " ");
        title = MultiSpaceRegex.Replace(title, " ");
        return title.Trim();
    }

    private static string RemoveJunkPatterns(string input)
    {
        var allPatterns = ResolutionPatterns
            .Concat(SourcePatterns)
            .Concat(CodecPatterns)
            .Concat(AudioPatterns)
            .Concat(EditionPatterns)
            .Concat(LanguagePatterns)
            .Concat(StreamingPatterns)
            .Concat(ReleaseGroupPatterns);

        foreach (var p in allPatterns)
        {
            input = Regex.Replace(input, @"[\.\s\-\[\(]" + p + @"[\.\s\-\]\)]?", " ", RegexOptions.IgnoreCase);
        }

        // Remove trailing dash-group like "-RARBG"
        input = Regex.Replace(input, @"-\w+$", "", RegexOptions.IgnoreCase);

        return input;
    }

    private static string ExtractEpisodeTitle(string afterEpisode)
    {
        var cleaned = afterEpisode;

        // Remove everything from quality indicators onward
        var qualityRegex = new Regex(
            @"[\.\s](?:2160p|1080p|720p|480p|4K|UHD|HD|BluRay|BDRip|WEB-DL|WEBRip|HDTV|DVDRip|HDRip|x264|x265|HEVC|AAC|AC3|DTS|PROPER|REPACK)",
            RegexOptions.IgnoreCase);

        var match = qualityRegex.Match(cleaned);
        if (match.Success)
        {
            cleaned = cleaned[..match.Index];
        }

        cleaned = cleaned.Replace('.', ' ').Replace('_', ' ');
        cleaned = cleaned.Replace(" - ", " ");
        cleaned = Regex.Replace(cleaned, @"-\w+$", "");
        cleaned = MultiSpaceRegex.Replace(cleaned, " ");
        return cleaned.Trim();
    }
}

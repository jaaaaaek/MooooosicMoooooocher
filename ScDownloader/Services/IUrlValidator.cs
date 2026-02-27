namespace ScDownloader.Services
{
    public interface IUrlValidator
    {
        UrlValidationResult Validate(string url, IReadOnlyCollection<string>? existingUrls = null);
    }

    public readonly record struct UrlValidationResult(bool IsValid, string Message);
}

using System.Threading.Tasks;

namespace NovaForge.Core.Privacy
{
    /// <summary>
    /// A consent platform's "privacy options" entry point (e.g. Google UMP). GDPR requires the user to be able to
    /// change their consent at any time; the settings screen shows the button only when <see cref="IsPrivacyOptionsRequired"/>.
    /// </summary>
    public interface IPrivacyOptionsProvider
    {
        bool IsPrivacyOptionsRequired { get; }
        /// <returns>Error message, or null on success.</returns>
        Task<string> ShowPrivacyOptionsAsync();
    }
}

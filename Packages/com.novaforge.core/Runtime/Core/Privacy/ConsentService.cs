using System;
using NovaForge.Core.Data;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Privacy
{
    public enum ConsentStatus
    {
        /// <summary>Not asked yet. Treated as NOT granted.</summary>
        Unknown,
        Granted,
        Denied,
        /// <summary>Consent not legally required in the user's region (as reported by the consent SDK, e.g. Google UMP).</summary>
        NotRequired,
    }

    /// <summary>Apple App Tracking Transparency status (iOS 14.5+). Other platforms: NotApplicable.</summary>
    public enum TrackingAuthorization
    {
        NotApplicable,
        NotDetermined,
        Restricted,
        Denied,
        Authorized,
    }

    public sealed class ConsentState : ISaveValidatable
    {
        public ConsentStatus Analytics = ConsentStatus.Unknown;
        public ConsentStatus AdPersonalization = ConsentStatus.Unknown;
        public TrackingAuthorization Tracking = TrackingAuthorization.NotApplicable;
        /// <summary>Version string of the Privacy Policy / ToS the user accepted; null = not accepted.</summary>
        public string AcceptedPolicyVersion;
        /// <summary>True if the user declared they are under the age of digital consent (COPPA / GDPR-K).</summary>
        public bool IsChildDirected;
        public long UpdatedUnixMs;
        public string Validate() => null;
    }

    public readonly struct ConsentChanged
    {
        public readonly ConsentState State;
        public ConsentChanged(ConsentState state) { State = state; }
    }

    public interface IConsent
    {
        ConsentState State { get; }
        bool CanCollectAnalytics { get; }
        bool CanShowPersonalizedAds { get; }
        bool IsChildDirected { get; }
        bool HasAcceptedPolicy(string currentPolicyVersion);
    }

    /// <summary>
    /// Single source of truth for privacy decisions (GDPR, KVKK, CCPA, Apple ATT, COPPA).
    /// The actual consent UI / CMP (e.g. Google User Messaging Platform, iOS ATT prompt) lives in the Unity layer
    /// and reports results here; analytics and ads read only from this service.
    /// Defaults are privacy-safe: unknown == not granted.
    /// </summary>
    public sealed class ConsentService : IConsent
    {
        public const int SaveVersion = 1;
        private readonly SaveSlot<ConsentState> _slot;
        private readonly IClock _clock;
        private readonly IEventBus _events;

        public ConsentState State { get; private set; }

        public ConsentService(SaveManager saves, IClock clock, IEventBus events)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _events = events;
            _slot = new SaveSlot<ConsentState>(saves, "consent", SaveVersion, () => new ConsentState());
            State = _slot.Load().Data;
        }

        public bool CanCollectAnalytics => Allowed(State.Analytics);

        public bool CanShowPersonalizedAds =>
            !State.IsChildDirected
            && Allowed(State.AdPersonalization)
            && State.Tracking != TrackingAuthorization.Denied
            && State.Tracking != TrackingAuthorization.Restricted
            && State.Tracking != TrackingAuthorization.NotDetermined;

        public bool IsChildDirected => State.IsChildDirected;

        public bool HasAcceptedPolicy(string currentPolicyVersion) =>
            !string.IsNullOrEmpty(State.AcceptedPolicyVersion) && State.AcceptedPolicyVersion == currentPolicyVersion;

        public void SetAnalytics(ConsentStatus status) => Update(s => s.Analytics = status);
        public void SetAdPersonalization(ConsentStatus status) => Update(s => s.AdPersonalization = status);
        public void SetTracking(TrackingAuthorization tracking) => Update(s => s.Tracking = tracking);
        public void SetChildDirected(bool isChild) => Update(s => s.IsChildDirected = isChild);
        public void AcceptPolicy(string version) => Update(s => s.AcceptedPolicyVersion = version);

        /// <summary>"Delete my data" / withdraw everything (GDPR Art. 7(3)).</summary>
        public void WithdrawAll() => Update(s =>
        {
            s.Analytics = ConsentStatus.Denied;
            s.AdPersonalization = ConsentStatus.Denied;
        });

        private static bool Allowed(ConsentStatus s) => s == ConsentStatus.Granted || s == ConsentStatus.NotRequired;

        private void Update(Action<ConsentState> change)
        {
            change(State);
            State.UpdatedUnixMs = TimeUtil.ToUnixMs(_clock.UtcNow);
            _slot.Save(State);
            _events?.Publish(new ConsentChanged(State));
        }
    }
}

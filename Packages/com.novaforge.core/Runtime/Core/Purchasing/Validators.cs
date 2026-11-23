using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Purchasing
{
    public sealed class HttpResponse
    {
        public int StatusCode;
        public string Body;
        /// <summary>True when the request never got an HTTP response (offline, DNS, timeout).</summary>
        public bool NetworkError;
    }

    /// <summary>Minimal HTTP abstraction. Unity layer implements it with UnityWebRequest.</summary>
    public interface IHttpTransport
    {
        Task<HttpResponse> PostJsonAsync(string url, string jsonBody, TimeSpan timeout);
    }

    /// <summary>
    /// Sends the receipt to YOUR backend, which must verify it with the official store APIs
    /// (Google Play Developer API purchases.products/subscriptionsv2, Apple App Store Server API)
    /// using credentials that never ship in the app.
    ///
    /// Request:  POST {endpoint}  {"gameId","platform","productId","transactionId","receipt"}
    /// Response: 200 {"status":"valid"|"invalid","productId":"...","expiresAtMs":123,"reason":"..."}
    /// 5xx / network error → RetryLater.  4xx → Invalid.
    /// </summary>
    public sealed class ServerPurchaseValidator : IPurchaseValidator
    {
        private readonly IHttpTransport _http;
        private readonly string _endpoint;
        private readonly string _gameId;
        private readonly TimeSpan _timeout;

        public ServerPurchaseValidator(IHttpTransport http, string endpoint, string gameId, TimeSpan? timeout = null)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            if (string.IsNullOrEmpty(endpoint) || !endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Validation endpoint must be an https:// URL.", nameof(endpoint));
            _endpoint = endpoint;
            _gameId = Guard.Id(gameId, nameof(gameId));
            _timeout = timeout ?? TimeSpan.FromSeconds(15);
        }

        public bool IsSecureForProduction => true;

        public async Task<ValidationResult> ValidateAsync(StorePurchase purchase)
        {
            var body = new JObject
            {
                ["gameId"] = _gameId,
                ["platform"] = purchase.Platform,
                ["productId"] = purchase.ProductId,
                ["transactionId"] = purchase.TransactionId,
                ["receipt"] = purchase.Receipt,
            }.ToString(Formatting.None);

            var response = await _http.PostJsonAsync(_endpoint, body, _timeout);
            if (response == null || response.NetworkError) return ValidationResult.Retry("network");
            if (response.StatusCode >= 500 || response.StatusCode == 429) return ValidationResult.Retry("server " + response.StatusCode);
            if (response.StatusCode != 200) return ValidationResult.Invalid("http " + response.StatusCode);

            JObject json;
            try { json = JObject.Parse(response.Body ?? string.Empty); }
            catch (Exception) { return ValidationResult.Retry("malformed response"); }

            var status = json.Value<string>("status");
            if (status == "valid")
            {
                var expires = json.Value<long?>("expiresAtMs");
                return ValidationResult.Valid(json.Value<string>("productId"), expires.HasValue ? TimeUtil.FromUnixMs(expires.Value) : (DateTime?)null);
            }
            if (status == "invalid") return ValidationResult.Invalid(json.Value<string>("reason") ?? "rejected by server");
            return ValidationResult.Retry("unknown status '" + status + "'");
        }
    }

    /// <summary>
    /// INSECURE: accepts every transaction. Only for development / sandbox testing before the validation backend exists.
    /// The Unity bootstrapper rejects it in release builds unless explicitly overridden.
    /// </summary>
    public sealed class DevelopmentAcceptAllValidator : IPurchaseValidator
    {
        public bool IsSecureForProduction => false;
        public Task<ValidationResult> ValidateAsync(StorePurchase purchase) => Task.FromResult(ValidationResult.Valid(purchase.ProductId));
    }
}

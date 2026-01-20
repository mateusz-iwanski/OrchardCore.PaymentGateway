using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Settings;
using OrchardCore.PaymentGateway.Providers.Przelewy24.Objects;
using OrchardCore.PaymentGateway.Providers.Przelewy24.Settings;
using OrchardCore.PaymentGateway.Providers.Przelewy24.Clients;
using System.Net.Http.Headers;

namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Services
{
    /// <summary>
    /// Service for Przelewy24 API integration - handles complete online payment flow.
    /// Implements all key operations: transaction registration, verification, refunds, 
    /// card payments, BLIK payments, and reporting.
    /// </summary>
    /// <remarks>
    /// <para><b>Przelewy24 API Requirements:</b></para>
    /// <list type="bullet">
    ///   <item>Basic Auth authentication (posId:reportKey)</item>
    ///   <item>SHA-384 signature signing for requests (sign parameter)</item>
    ///   <item>Mandatory verification of each payment after receiving notification</item>
    /// </list>
    /// 
    /// <para><b>Standard Payment Flow:</b></para>
    /// <list type="number">
    ///   <item><see cref="CreateTransactionAsync"/> - Register transaction, receive token</item>
    ///   <item>Redirect user to: https://sandbox.przelewy24.pl/trnRequest/{token}</item>
    ///   <item>User completes payment on Przelewy24 page</item>
    ///   <item>Przelewy24 sends notification to urlStatus endpoint</item>
    ///   <item><see cref="VerifyTransactionAsync"/> - CRITICAL: Verify payment (mandatory!)</item>
    ///   <item>Redirect user back to urlReturn (shop confirmation page)</item>
    /// </list>
    /// 
    /// <para><b>Configuration Requirements (appsettings.json):</b></para>
    /// <code>
    /// "Przelewy24": {
    ///   "MerchantId": "12345",
    ///   "PosId": "12345",
    ///   "CrcKey": "xxx",
    ///   "ReportKey": "xxx",
    ///   "BaseUrl": "https://sandbox.przelewy24.pl/api/v1/"
    /// }
    /// </code>
    /// 
    /// <para><b>Security Notes:</b></para>
    /// <list type="bullet">
    ///   <item>All amounts are in grosze (1 PLN = 100 grosze)</item>
    ///   <item>Signatures prevent request tampering</item>
    ///   <item>Verification prevents fake payment confirmations</item>
    ///   <item>Card payments require PCI DSS compliance</item>
    /// </list>
    /// </remarks>
    public class Przelewy24Service
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IPrzelewy24SignatureProvider _signatureProvider;
        private readonly ILogger<Przelewy24Service> _logger;
        private readonly ISiteService? _siteService;

        // Fallbacks for local testing only - REPLACE IN PRODUCTION!
        private const string SandboxCrcKey = "yourSandboxCrcKey";
        private const string SandboxReportKey = "yourSandboxReportKey";
        private const string SandboxBaseUrl = "https://sandbox.przelewy24.pl/api/v1/";

        public Przelewy24Service(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IPrzelewy24SignatureProvider signatureProvider,
            ILogger<Przelewy24Service> logger,
            ISiteService? siteService = null)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _signatureProvider = signatureProvider;
            _logger = logger;
            _siteService = siteService;
        }

        #region Settings helpers

        private async Task<Przelewy24Settings> GetSettingsAsync()
        {
            Przelewy24Settings? siteSettings = null;

            if (_siteService is not null)
            {
                var site = await _siteService.GetSiteSettingsAsync().ConfigureAwait(false);
                siteSettings = site.As<Przelewy24Settings>();
            }

            var merchantId = siteSettings?.MerchantId
                             ?? _configuration.GetValue<int?>("Przelewy24:MerchantId")
                             ?? _configuration.GetValue<int?>("Przelewy24:ClientId");

            var posId = siteSettings?.PosId
                        ?? _configuration.GetValue<int?>("Przelewy24:PosId")
                        ?? merchantId;

            var crc = string.IsNullOrWhiteSpace(siteSettings?.CrcKey)
                ? _configuration["Przelewy24:CrcKey"]
                : siteSettings!.CrcKey;

            var reportKey = string.IsNullOrWhiteSpace(siteSettings?.ReportKey)
                ? _configuration["Przelewy24:ReportKey"]
                : siteSettings!.ReportKey;

            var secretId = string.IsNullOrWhiteSpace(siteSettings?.SecretId)
                ? _configuration["Przelewy24:SecretId"]
                : siteSettings!.SecretId;

            var baseUrl = string.IsNullOrWhiteSpace(siteSettings?.BaseUrl)
                ? _configuration["Przelewy24:BaseUrl"]
                : siteSettings!.BaseUrl;

            var useSandbox = siteSettings?.UseSandboxFallbacks ?? true;

            return new Przelewy24Settings
            {
                ClientId = siteSettings?.ClientId ?? _configuration["Przelewy24:ClientId"],
                MerchantId = merchantId,
                PosId = posId,
                CrcKey = !string.IsNullOrWhiteSpace(crc) ? crc : (useSandbox ? SandboxCrcKey : null),
                ReportKey = !string.IsNullOrWhiteSpace(reportKey) ? reportKey : (useSandbox ? SandboxReportKey : null),
                SecretId = !string.IsNullOrWhiteSpace(secretId) ? secretId : null,
                BaseUrl = !string.IsNullOrWhiteSpace(baseUrl) ? EnsureTrailingSlash(baseUrl) : EnsureTrailingSlash(SandboxBaseUrl),
                UseSandboxFallbacks = useSandbox
            };
        }

        private static string EnsureTrailingSlash(string url) =>
            url.EndsWith('/') ? url : url + "/";

        private async Task<HttpClient> CreateAuthenticatedClientAsync(Przelewy24Settings settings)
        {
            var client = _httpClientFactory.CreateClient("Przelewy24");
            client.BaseAddress ??= new Uri(settings.BaseUrl ?? SandboxBaseUrl);

            var reportKey = settings.SecretId ?? settings.ReportKey ?? throw new InvalidOperationException("Przelewy24:ReportKey/SecretId not configured.");
            var posId = settings.PosId?.ToString() ?? settings.MerchantId?.ToString();

            if (string.IsNullOrWhiteSpace(posId))
            {
                throw new InvalidOperationException("Przelewy24:PosId or MerchantId not configured.");
            }

            var authBytes = Encoding.UTF8.GetBytes($"{posId}:{reportKey}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

            return client;
        }

        #endregion

        #region Test & Basic Operations

        /// <summary>
        /// Tests connection and authentication with Przelewy24 API.
        /// </summary>
        /// <param name="posId">Optional POS ID override (uses configuration if null)</param>
        /// <param name="secretId">Optional secret/report key override (uses configuration if null)</param>
        /// <returns>JSON response from Przelewy24 confirming successful authentication</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when credentials are invalid or API returns error
        /// </exception>
        /// <remarks>
        /// <b>Use Case:</b> Verify credentials before going live or for health checks.
        /// 
        /// <b>HTTP:</b> GET /api/v1/testAccess
        /// 
        /// <b>Auth:</b> Basic Auth (posId:reportKey)
        /// </remarks>
        public async Task<string> TestAccessAsync(string? posId = null, string? secretId = null)
        {
            var settings = await GetSettingsAsync().ConfigureAwait(false);

            var cfgPosId = posId ?? settings.PosId?.ToString() ?? settings.MerchantId?.ToString();
            var cfgSecret = secretId ?? settings.SecretId ?? settings.ReportKey;

            if (string.IsNullOrWhiteSpace(cfgPosId))
                throw new InvalidOperationException("PosId not configured and not provided.");

            if (string.IsNullOrWhiteSpace(cfgSecret))
                throw new InvalidOperationException("SecretId/ReportKey not configured and not provided.");

            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);
            var authBytes = Encoding.UTF8.GetBytes($"{cfgPosId}:{cfgSecret}");
            var authHeader = Convert.ToBase64String(authBytes);

            var request = new HttpRequestMessage(HttpMethod.Get, "testAccess");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            using var resp = await client.SendAsync(request).ConfigureAwait(false);
            var content = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 returned {(int)resp.StatusCode}. Response content: {content}");
            }

            return content;
        }

        #endregion

        #region Transaction Operations

        /// <summary>
        /// Registers a new payment transaction with Przelewy24 and returns a token for payment page.
        /// This is Step 1 in the payment process.
        /// </summary>
        /// <param name="request">Transaction registration request containing payment details</param>
        /// <returns>Response containing payment token used to build redirect URL</returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when API call fails</exception>
        /// <remarks>
        /// <para><b>Payment Flow - Step 1 of 5</b></para>
        /// 
        /// <b>Process:</b>
        /// <list type="number">
        ///   <item>Validates request and generates sessionId if missing</item>
        ///   <item>Computes SHA-384 signature for security</item>
        ///   <item>Sends registration request to Przelewy24</item>
        ///   <item>Returns token for payment redirect</item>
        /// </list>
        /// 
        /// <b>After Registration:</b>
        /// Redirect user to: https://sandbox.przelewy24.pl/trnRequest/{token}
        /// 
        /// <b>HTTP:</b> POST /api/v1/transaction/register
        /// 
        /// <b>Content-Type:</b> application/x-www-form-urlencoded
        /// 
        /// <b>Important:</b> Amount must be in grosze (1 PLN = 100 grosze)
        /// </remarks>
        public async Task<RegisterResponseDto> CreateTransactionAsync(RegisterRequestDto request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var settings = await GetSettingsAsync().ConfigureAwait(false);

            if (settings.MerchantId is null)
                throw new InvalidOperationException("Przelewy24 MerchantId not configured.");
            if (settings.PosId is null)
                throw new InvalidOperationException("Przelewy24 PosId not configured.");
            if (string.IsNullOrWhiteSpace(settings.CrcKey))
                throw new InvalidOperationException("Przelewy24 CRC key not configured.");

            if (string.IsNullOrWhiteSpace(request.SessionId))
            {
                request = request with { SessionId = Guid.NewGuid().ToString("N") };
            }

            var normalizedRequest = request with
            {
                MerchantId = request.MerchantId == 0 ? settings.MerchantId.Value : request.MerchantId,
                PosId = request.PosId == 0 ? settings.PosId.Value : request.PosId
            };

            var sign = await _signatureProvider.CreateRegisterSignatureAsync(normalizedRequest, settings.CrcKey!).ConfigureAwait(false);
            var signedRequest = normalizedRequest with { Sign = sign };

            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var form = new Dictionary<string, string?>
            {
                ["merchantId"] = signedRequest.MerchantId.ToString(),
                ["posId"] = signedRequest.PosId.ToString(),
                ["amount"] = signedRequest.Amount.ToString(),
                ["currency"] = signedRequest.Currency,
                ["description"] = signedRequest.Description,
                ["email"] = signedRequest.Email,
                ["country"] = signedRequest.Country,
                ["language"] = signedRequest.Language,
                ["urlReturn"] = signedRequest.UrlReturn,
                ["urlStatus"] = signedRequest.UrlStatus,
                ["sessionId"] = signedRequest.SessionId,
                ["sign"] = signedRequest.Sign
            };

            using var content = new FormUrlEncodedContent(form);
            var responseMessage = await client.PostAsync("transaction/register", content).ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 returned {(int)responseMessage.StatusCode}. Response content: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<RegisterResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize Przelewy24 response.");
        }

        /// <summary>
        /// Verifies transaction after receiving notification from Przelewy24.
        /// ⚠️ CRITICAL - This method is MANDATORY for payment confirmation!
        /// </summary>
        /// <param name="request">Verification request containing transaction details from notification</param>
        /// <returns>Verification response confirming payment status</returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when verification fails</exception>
        /// <remarks>
        /// <para><b>Payment Flow - Step 5 of 5 (CRITICAL)</b></para>
        /// 
        /// <b>Why This Matters:</b>
        /// <list type="bullet">
        ///   <item>Without verification, Przelewy24 will NOT confirm payment</item>
        ///   <item>Prevents fraudulent payment notifications</item>
        ///   <item>Must be called within 15 minutes of notification</item>
        ///   <item>Cannot fulfill order without successful verification</item>
        /// </list>
        /// 
        /// <b>When to Call:</b>
        /// After receiving POST notification from Przelewy24 to urlStatus endpoint
        /// 
        /// <b>HTTP:</b> PUT /api/v1/transaction/verify
        /// 
        /// <b>Content-Type:</b> application/json
        /// 
        /// <b>If Verification Fails:</b> Do NOT fulfill the order - payment is not confirmed
        /// </remarks>
        public async Task<VerifyResponseDto> VerifyTransactionAsync(VerifyRequestDto request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var settings = await GetSettingsAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(settings.CrcKey))
                throw new InvalidOperationException("Przelewy24 CRC key not configured.");

            var sign = await _signatureProvider.CreateVerifySignatureAsync(request, settings.CrcKey!).ConfigureAwait(false);
            var signedRequest = request with { Sign = sign };

            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var jsonContent = JsonSerializer.Serialize(signedRequest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var responseMessage = await client.PutAsync("transaction/verify", content).ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 verify failed {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<VerifyResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize verify response.");
        }

        /// <summary>
        /// Retrieves transaction details by sessionId.
        /// </summary>
        /// <param name="sessionId">Unique session identifier used during transaction registration</param>
        /// <returns>Transaction details including status, amount, payment method</returns>
        /// <exception cref="ArgumentException">Thrown when sessionId is empty</exception>
        /// <exception cref="InvalidOperationException">Thrown when API call fails</exception>
        /// <remarks>
        /// <b>Use Cases:</b> Check status, get orderId for refunds, retrieve payment method used
        /// 
        /// <b>HTTP:</b> GET /api/v1/transaction/by/sessionId/{sessionId}
        /// 
        /// <b>Returns:</b> orderId, amount, currency, status, methodId, email, description
        /// </remarks>
        public async Task<TransactionDetailsDto> GetTransactionBySessionIdAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("SessionId is required", nameof(sessionId));

            var settings = await GetSettingsAsync().ConfigureAwait(false);
            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var responseMessage = await client.GetAsync($"transaction/by/sessionId/{sessionId}").ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 returned {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<TransactionDetailsDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize transaction details.");
        }

        #endregion

        #region Refund Operations

        /// <summary>
        /// Processes a refund (full or partial) for a completed transaction.
        /// </summary>
        /// <param name="request">Refund request containing orderId, amount, and currency</param>
        /// <returns>Refund response with UUID and status</returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when refund fails</exception>
        /// <remarks>
        /// <para><b>Refund Types:</b></para>
        /// <list type="bullet">
        ///   <item><b>Full:</b> amount = original transaction amount</item>
        ///   <item><b>Partial:</b> amount &lt; original amount</item>
        ///   <item><b>Multiple Partial:</b> Can refund multiple times until full amount</item>
        /// </list>
        /// 
        /// <b>Requirements:</b>
        /// <list type="bullet">
        ///   <item>Transaction must be completed and verified</item>
        ///   <item>Total refunds cannot exceed original amount</item>
        ///   <item>Must be within refund period (usually 365 days)</item>
        /// </list>
        /// 
        /// <b>HTTP:</b> POST /api/v1/transaction/refund
        /// 
        /// <b>Processing Time:</b> Cards 1-5 days, Transfers 1-3 days, BLIK usually instant
        /// </remarks>
        public async Task<RefundResponseDto> RefundTransactionAsync(RefundRequestDto request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var settings = await GetSettingsAsync().ConfigureAwait(false);
            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var responseMessage = await client.PostAsync("transaction/refund", content).ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 refund failed {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<RefundResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize refund response.");
        }

        /// <summary>
        /// Retrieves refund details by orderId.
        /// </summary>
        /// <param name="orderId">Przelewy24 order identifier</param>
        /// <returns>Refund details including status, amount, timestamps</returns>
        /// <exception cref="InvalidOperationException">Thrown when API call fails</exception>
        /// <remarks>
        /// <b>Use Cases:</b> Check refund status, audit operations, customer service inquiries
        /// 
        /// <b>HTTP:</b> GET /api/v1/refund/by/orderId/{orderId}
        /// 
        /// <b>Statuses:</b> pending (processing), completed (money returned), failed (contact support)
        /// </remarks>
        public async Task<RefundDetailsDto> GetRefundByOrderIdAsync(long orderId)
        {
            var settings = await GetSettingsAsync().ConfigureAwait(false);
            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var responseMessage = await client.GetAsync($"refund/by/orderId/{orderId}").ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 returned {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<RefundDetailsDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize refund details.");
        }

        #endregion

        #region Payment Methods

        /// <summary>
        /// Retrieves list of available payment methods for given parameters.
        /// </summary>
        /// <param name="lang">Language code for method names (pl/en)</param>
        /// <param name="amount">Optional amount in grosze to filter methods</param>
        /// <param name="currency">Currency code (default: PLN)</param>
        /// <returns>List of available payment methods with details</returns>
        /// <exception cref="InvalidOperationException">Thrown when API call fails</exception>
        /// <remarks>
        /// <b>Use Cases:</b> Display payment options, build custom UI, check method availability
        /// 
        /// <b>HTTP:</b> GET /api/v1/payment/methods/{lang}?amount=1000&currency=PLN
        /// 
        /// <b>Common Methods:</b> BLIK (181), Credit Card (20), Bank Transfer (25), PayPal (163)
        /// 
        /// <b>Returns:</b> id, name, imgUrl, status, mobile support
        /// </remarks>
        public async Task<PaymentMethodsResponseDto> GetPaymentMethodsAsync(string lang = "pl", int? amount = null, string currency = "PLN")
        {
            var settings = await GetSettingsAsync().ConfigureAwait(false);
            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var queryParams = new List<string>();
            if (amount.HasValue) queryParams.Add($"amount={amount.Value}");
            if (!string.IsNullOrWhiteSpace(currency)) queryParams.Add($"currency={currency}");

            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
            var url = $"payment/methods/{lang}{query}";

            var responseMessage = await client.GetAsync(url).ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 returned {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<PaymentMethodsResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize payment methods.");
        }

        #endregion

        #region Card Operations

        /// <summary>
        /// Retrieves saved card information (reference ID, BIN, masked number) for a transaction.
        /// </summary>
        /// <param name="orderId">Przelewy24 order identifier from completed card payment</param>
        /// <returns>Card information including reference token for recurring payments</returns>
        /// <exception cref="InvalidOperationException">Thrown when API call fails</exception>
        /// <remarks>
        /// <b>Use Cases:</b> Get card token for 1-click payments, display masked card to customer
        /// 
        /// <b>HTTP:</b> GET /api/v1/card/info/{orderId}
        /// 
        /// <b>Returns:</b> refId (STORE SECURELY!), mask (e.g., ************1234), bin (first 6 digits)
        /// 
        /// <b>Security:</b> refId allows charging without PCI DSS, encrypt in database, customer-specific
        /// </remarks>
        public async Task<CardInfoResponseDto> GetCardInfoAsync(long orderId)
        {
            var settings = await GetSettingsAsync().ConfigureAwait(false);
            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var responseMessage = await client.GetAsync($"card/info/{orderId}").ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 returned {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<CardInfoResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize card info.");
        }

        /// <summary>
        /// Charges a saved card with 3D Secure authentication (1-click payment with security).
        /// </summary>
        /// <param name="token">Card reference token (refId) from previous transaction</param>
        /// <returns>Response with redirect URL for 3DS authentication and orderId</returns>
        /// <exception cref="ArgumentException">Thrown when token is empty</exception>
        /// <exception cref="InvalidOperationException">Thrown when charge fails</exception>
        /// <remarks>
        /// <b>Use Case:</b> Recurring payments with customer present (requires 3DS verification)
        /// 
        /// <b>Flow:</b> Call method → Receive redirectUrl → Customer completes 3DS → Verify payment
        /// 
        /// <b>HTTP:</b> POST /api/v1/card/chargeWith3ds
        /// 
        /// <b>When to use:</b> Subscription renewals with customer, one-click checkout, SCA required
        /// </remarks>
        public async Task<CardChargeWith3dsResponseDto> ChargeCardWith3dsAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token is required", nameof(token));

            var settings = await GetSettingsAsync().ConfigureAwait(false);
            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var requestBody = new { token };
            var jsonContent = JsonSerializer.Serialize(requestBody);

            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var responseMessage = await client.PostAsync("card/chargeWith3ds", content).ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 charge failed {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<CardChargeWith3dsResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize charge response.");
        }

        /// <summary>
        /// Charges a saved card without 3D Secure (recurring/subscription payments, customer not present).
        /// </summary>
        /// <param name="token">Card reference token (refId) from initial transaction with recurring consent</param>
        /// <returns>Response with orderId and payment status</returns>
        /// <exception cref="ArgumentException">Thrown when token is empty</exception>
        /// <exception cref="InvalidOperationException">Thrown when charge fails</exception>
        /// <remarks>
        /// <b>Use Case:</b> Automated recurring payments (subscriptions, automatic billing)
        /// 
        /// <b>Requirements:</b>
        /// <list type="bullet">
        ///   <item>Customer must have agreed to recurring charges</item>
        ///   <item>Initial transaction must have been with 3DS</item>
        ///   <item>Merchant must have recurring enabled</item>
        /// </list>
        /// 
        /// <b>HTTP:</b> POST /api/v1/card/charge
        /// 
        /// <b>When to use:</b> Monthly subscriptions, automatic bills, customer not present
        /// 
        /// <b>Important:</b> No 3DS prompt, higher chargeback risk, notify customer before charging
        /// </remarks>
        public async Task<CardChargeResponseDto> ChargeCardAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token is required", nameof(token));

            var settings = await GetSettingsAsync().ConfigureAwait(false);
            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var requestBody = new { token };
            var jsonContent = JsonSerializer.Serialize(requestBody);

            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var responseMessage = await client.PostAsync("card/charge", content).ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 charge failed {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<CardChargeResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize charge response.");
        }

        /// <summary>
        /// Processes direct card payment with card details.
        /// ⚠️ WARNING: Requires PCI DSS compliance! Use hosted payment page instead if possible.
        /// </summary>
        /// <param name="request">Card payment request with card number, expiry, CVV, and transaction details</param>
        /// <returns>Payment response with orderId and status</returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when payment fails</exception>
        /// <remarks>
        /// <para><b>⚠️ PCI DSS COMPLIANCE REQUIRED</b></para>
        /// <para>Only use if you have PCI DSS Level 1/2 certification. Recommended: Use CreateTransactionAsync + redirect instead.</para>
        /// 
        /// <b>HTTP:</b> POST /api/v1/card/pay
        /// 
        /// <b>Security:</b> HTTPS only, never log/store card data, implement rate limiting
        /// 
        /// <b>Penalties:</b> Fines up to $500K, loss of card processing, legal liability
        /// </remarks>
        public async Task<CardPayResponseDto> PayWithCardAsync(CardPayRequestDto request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var settings = await GetSettingsAsync().ConfigureAwait(false);
            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var responseMessage = await client.PostAsync("card/pay", content).ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 card pay failed {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<CardPayResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize card pay response.");
        }

        #endregion

        #region BLIK Operations

        /// <summary>
        /// Processes BLIK payment using 6-digit code (Level 0 - standard BLIK payment).
        /// </summary>
        /// <param name="request">BLIK payment request with 6-digit code and transaction details</param>
        /// <returns>Payment response with orderId and status</returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when payment fails</exception>
        /// <remarks>
        /// <para><b>What is BLIK?</b></para>
        /// <para>Polish instant payment system - customer generates 6-digit code in banking app and enters at checkout.</para>
        /// 
        /// <b>Flow:</b>
        /// <list type="number">
        ///   <item>Customer opens banking app and generates code (valid 2 minutes)</item>
        ///   <item>Customer enters code on checkout page</item>
        ///   <item>You call this method with the code</item>
        ///   <item>Customer confirms in banking app (push notification)</item>
        ///   <item>Payment completed instantly</item>
        /// </list>
        /// 
        /// <b>HTTP:</b> POST /api/v1/paymentMethod/blik/chargeByCode
        /// 
        /// <b>Advantages:</b> Instant, no card needed, ~50% of Polish online payments, low fees
        /// 
        /// <b>Notes:</b> Code expires in 2 min, confirm within 60 sec, Poland only
        /// </remarks>
        public async Task<BlikChargeResponseDto> ChargeByBlikCodeAsync(BlikChargeByCodeRequestDto request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var settings = await GetSettingsAsync().ConfigureAwait(false);
            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var responseMessage = await client.PostAsync("paymentMethod/blik/chargeByCode", content).ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"BLIK charge failed {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<BlikChargeResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize BLIK charge response.");
        }

        /// <summary>
        /// Processes BLIK payment using saved alias (Level 6 - one-click BLIK payment).
        /// </summary>
        /// <param name="request">BLIK alias payment request with alias value/label and transaction details</param>
        /// <returns>Payment response with orderId and status</returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when payment fails</exception>
        /// <remarks>
        /// <para><b>What is BLIK One-Click?</b></para>
        /// <para>Customer registers banking app once, then pays with one click without generating codes.</para>
        /// 
        /// <b>Setup (First Time):</b>
        /// <list type="number">
        ///   <item>Customer makes first BLIK payment with code</item>
        ///   <item>Chooses to save BLIK for future</item>
        ///   <item>Retrieve alias using GetBlikAliasesByEmailAsync</item>
        ///   <item>Store alias for future payments</item>
        /// </list>
        /// 
        /// <b>Subsequent Payments:</b>
        /// <list type="number">
        ///   <item>Customer clicks "Pay with BLIK One-Click"</item>
        ///   <item>Call this method with stored alias</item>
        ///   <item>Customer confirms in banking app (or auto-confirms)</item>
        ///   <item>Payment completed instantly</item>
        /// </list>
        /// 
        /// <b>HTTP:</b> POST /api/v1/paymentMethod/blik/chargeByAlias
        /// 
        /// <b>Advantages:</b> No code needed, faster checkout, higher conversion, works for subscriptions
        /// 
        /// <b>Note:</b> Customer can revoke alias anytime, always have code payment fallback
        /// </remarks>
        public async Task<BlikChargeResponseDto> ChargeByBlikAliasAsync(BlikChargeByAliasRequestDto request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var settings = await GetSettingsAsync().ConfigureAwait(false);
            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var responseMessage = await client.PostAsync("paymentMethod/blik/chargeByAlias", content).ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"BLIK alias charge failed {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<BlikChargeResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize BLIK alias charge response.");
        }

        /// <summary>
        /// Retrieves list of BLIK aliases registered for customer email address.
        /// </summary>
        /// <param name="email">Customer email address used during alias registration</param>
        /// <returns>List of BLIK aliases with values, labels, and types</returns>
        /// <exception cref="ArgumentException">Thrown when email is empty</exception>
        /// <exception cref="InvalidOperationException">Thrown when API call fails</exception>
        /// <remarks>
        /// <b>Use Cases:</b> Display saved BLIK options at checkout, check if one-click enabled, verify alias validity
        /// 
        /// <b>HTTP:</b> GET /api/v1/paymentMethod/blik/getAliasesByEmail/{email}
        /// 
        /// <b>Returns:</b> aliasValue (token for charging), aliasLabel (bank name), type (UID/custom)
        /// 
        /// <b>Workflow:</b>
        /// <list type="number">
        ///   <item>Call this when customer visits checkout</item>
        ///   <item>If aliases found, show "Pay with BLIK One-Click"</item>
        ///   <item>Display labels so customer can choose</item>
        ///   <item>Use selected alias with ChargeByBlikAliasAsync</item>
        ///   <item>If no aliases, fall back to code payment</item>
        /// </list>
        /// 
        /// <b>Note:</b> Empty list = no one-click registered, multiple aliases possible (one per bank)
        /// </remarks>
        public async Task<BlikAliasesResponseDto> GetBlikAliasesByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email is required", nameof(email));

            var settings = await GetSettingsAsync().ConfigureAwait(false);
            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var responseMessage = await client.GetAsync($"paymentMethod/blik/getAliasesByEmail/{email}").ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 returned {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<BlikAliasesResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize BLIK aliases.");
        }

        /// <summary>
        /// Retrieves list of custom BLIK aliases registered for customer email address.
        /// </summary>
        /// <param name="email">Customer email address used during alias registration</param>
        /// <returns>List of custom BLIK aliases with values, labels, and types</returns>
        /// <exception cref="ArgumentException">Thrown when email is empty</exception>
        /// <exception cref="InvalidOperationException">Thrown when API call fails</exception>
        /// <remarks>
        /// <b>Difference from Standard Aliases:</b> Custom aliases allow merchant-specific rules and configurations
        /// 
        /// <b>HTTP:</b> GET /api/v1/paymentMethod/blik/getAliasesByEmail/{email}/custom
        /// 
        /// <b>Use Case:</b> Retrieve merchant-specific BLIK aliases with custom payment rules
        /// 
        /// <b>Note:</b> Requires custom alias configuration in Przelewy24 merchant panel
        /// </remarks>
        public async Task<BlikAliasesResponseDto> GetBlikCustomAliasesByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email is required", nameof(email));

            var settings = await GetSettingsAsync().ConfigureAwait(false);
            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var responseMessage = await client.GetAsync($"paymentMethod/blik/getAliasesByEmail/{email}/custom").ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 returned {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<BlikAliasesResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize BLIK custom aliases.");
        }

        #endregion

        #region Report Operations

        /// <summary>
        /// Retrieves transaction history (batches, transactions, refunds) for given date range.
        /// </summary>
        /// <param name="dateFrom">Start date (format: YYYYMMDD, e.g., "20250101")</param>
        /// <param name="dateTo">End date (format: YYYYMMDD, max 31 days from dateFrom)</param>
        /// <param name="type">Optional filter: "batch", "transaction", "refund" (null = all types)</param>
        /// <returns>Report history with list of items</returns>
        /// <exception cref="ArgumentException">Thrown when dates are invalid</exception>
        /// <exception cref="InvalidOperationException">Thrown when API call fails</exception>
        /// <remarks>
        /// <b>Use Cases:</b> Financial reporting, reconciliation, audit trails, accounting integration
        /// 
        /// <b>HTTP:</b> GET /api/v1/report/history?dateFrom=20250101&dateTo=20250131&type=transaction
        /// 
        /// <b>Date Range:</b> Maximum 31 days between dateFrom and dateTo
        /// 
        /// <b>Item Types:</b>
        /// <list type="bullet">
        ///   <item><b>batch:</b> Settlement batch (money transferred to your account)</item>
        ///   <item><b>transaction:</b> Individual completed payment</item>
        ///   <item><b>refund:</b> Refund processed</item>
        /// </list>
        /// 
        /// <b>Returns:</b> type, id, date, amount, currency, status for each item
        /// 
        /// <b>Tip:</b> Use for daily reconciliation - compare your database with Przelewy24 report
        /// </remarks>
        public async Task<ReportHistoryResponseDto> GetReportHistoryAsync(string dateFrom, string dateTo, string? type = null)
        {
            if (string.IsNullOrWhiteSpace(dateFrom))
                throw new ArgumentException("DateFrom is required", nameof(dateFrom));
            if (string.IsNullOrWhiteSpace(dateTo))
                throw new ArgumentException("DateTo is required", nameof(dateTo));

            var settings = await GetSettingsAsync().ConfigureAwait(false);
            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(type))
                queryParams.Add($"type={type}");

            var query = queryParams.Count > 0 ? "&" + string.Join("&", queryParams) : string.Empty;
            var url = $"report/history?dateFrom={dateFrom}&dateTo={dateTo}{query}";

            var responseMessage = await client.GetAsync(url).ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 returned {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<ReportHistoryResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize report history.");
        }

        /// <summary>
        /// Retrieves detailed information about a settlement batch (all transactions and refunds in batch).
        /// </summary>
        /// <param name="batchId">Batch identifier from report history</param>
        /// <returns>Batch details with all transactions and refunds included</returns>
        /// <exception cref="InvalidOperationException">Thrown when API call fails</exception>
        /// <remarks>
        /// <para><b>What is a Batch?</b></para>
        /// <para>A batch is a group of transactions that Przelewy24 settles together and transfers to your bank account.</para>
        /// 
        /// <b>Use Cases:</b>
        /// <list type="bullet">
        ///   <item>Reconcile bank transfer with individual transactions</item>
        ///   <item>Generate detailed settlement reports</item>
        ///   <item>Verify accounting entries</item>
        ///   <item>Audit financial data</item>
        /// </list>
        /// 
        /// <b>HTTP:</b> GET /api/v1/report/batch/details/{batchId}
        /// 
        /// <b>Returns:</b>
        /// <list type="bullet">
        ///   <item>batchId, date - Batch identifier and settlement date</item>
        ///   <item>transactions - Array of all transactions in batch</item>
        ///   <item>refunds - Array of all refunds in batch</item>
        /// </list>
        /// 
        /// <b>Transaction Details:</b> orderId, sessionId, amount, currency, status
        /// 
        /// <b>Refund Details:</b> orderId, amount, currency, status
        /// 
        /// <b>Workflow:</b>
        /// <list type="number">
        ///   <item>Call GetReportHistoryAsync to get batches</item>
        ///   <item>Find batch matching your bank transfer</item>
        ///   <item>Call this method to get detailed breakdown</item>
        ///   <item>Reconcile individual transactions with your database</item>
        /// </list>
        /// </remarks>
        public async Task<BatchDetailsResponseDto> GetBatchDetailsAsync(int batchId)
        {
            var settings = await GetSettingsAsync().ConfigureAwait(false);
            var client = await CreateAuthenticatedClientAsync(settings).ConfigureAwait(false);

            var responseMessage = await client.GetAsync($"report/batch/details/{batchId}").ConfigureAwait(false);
            var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Przelewy24 returned {(int)responseMessage.StatusCode}. Response: {responseContent}");
            }

            var dto = JsonSerializer.Deserialize<BatchDetailsResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto ?? throw new InvalidOperationException("Failed to deserialize batch details.");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates an authenticated HTTP client with Basic Auth headers for Przelewy24 API calls.
        /// </summary>
        /// <returns>Configured HttpClient with authentication</returns>
        /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
        /// <remarks>
        /// <para><b>Internal helper method</b> - used by all API operations requiring authentication</para>
        /// 
        /// <b>Configuration:</b>
        /// <list type="bullet">
        ///   <item>Gets named HttpClient "Przelewy24" from factory</item>
        ///   <item>Retrieves posId and reportKey from configuration</item>
        ///   <item>Sets Basic Auth header (posId:reportKey)</item>
        /// </list>
        /// 
        /// <b>Security:</b> Basic Auth credentials are Base64-encoded and sent with every request
        /// </remarks>
        private HttpClient CreateAuthenticatedClient()
        {
            var client = _httpClientFactory.CreateClient("Przelewy24");

            var reportKey = _configuration["Przelewy24:ReportKey"] ?? SandboxReportKey;
            var posId = _configuration["Przelewy24:PosId"] ?? _configuration["Przelewy24:MerchantId"];

            if (string.IsNullOrWhiteSpace(posId))
                throw new InvalidOperationException("Przelewy24:PosId or MerchantId not configured.");

            if (string.IsNullOrWhiteSpace(reportKey))
                throw new InvalidOperationException("Przelewy24:ReportKey not configured.");

            var authBytes = Encoding.UTF8.GetBytes($"{posId}:{reportKey}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

            return client;
        }

        #endregion
    }
}
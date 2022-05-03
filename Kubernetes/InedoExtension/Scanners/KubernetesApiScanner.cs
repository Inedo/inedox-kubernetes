using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility.PackageContainerScanners;
using Inedo.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Inedo.Extensions.Kubernetes.Scanners
{
    [DisplayName("Kubernetes API Scanner")]
    [Description("Automatically scans a Kubernetes API for container images and reports back their status.")]
    public sealed class KubernetesApiScanner : PackageContainerScanner
    {
        [Persistent]
        [DisplayName("Kubernetes API Base URL")]
        [PlaceholderText("http://localhost:8080/")]
        [Required]
        public string ApiBaseUrl { get; set; }

        [Persistent]
        [DisplayName("Authorization Type")]
        [Required]
        public AuthenticationType AuthorizationType { get; set; }

        [Persistent]
        [DisplayName("User Name")]
        [Description("Only used if Authentication Type is Basic Authentication")]
        public string UserName { get; set; }

        [DisplayName("Password")]
        [Description("Only used if Authentication Type is Basic Authentication")]
        [Persistent(Encrypted = true)]
        public SecureString Password { get; set; }

        [DisplayName("Bearer Token")]
        [Description("Only used if Authentication Type is Bearer Token")]
        [Persistent(Encrypted = true)]
        public SecureString BearerToken { get; set; }

        public override RichDescription GetDescription() => new RichDescription("Kubernetes API at ", new Hilite(this.ApiBaseUrl));

        public async override Task<ScannerResults> ScanAsync(CancellationToken cancellationToken)
        {
            this.LogInformation($"Scanning Kubernetes API at {this.ApiBaseUrl}");
            var containers = new List<KubernetesContainerUsageData>();
            var httpClient = SDK.CreateHttpClient();
            try
            {
                httpClient.BaseAddress = new Uri(this.ApiBaseUrl);
                if (this.AuthorizationType == AuthenticationType.Basic)
                {
                    if (string.IsNullOrWhiteSpace(this.UserName))
                        throw new ArgumentNullException(nameof(this.UserName));
                    if (this.Password == null)
                        throw new ArgumentNullException(nameof(this.Password));
                    var password = AH.Unprotect(this.Password);
                    if (string.IsNullOrWhiteSpace(password))
                        throw new ArgumentNullException(nameof(this.Password));

                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{this.UserName}:{password}")));
                }
                else if (this.AuthorizationType == AuthenticationType.Bearer)
                {
                    if (this.BearerToken == null)
                        throw new ArgumentNullException(nameof(this.BearerToken));
                    var token = AH.Unprotect(this.BearerToken);
                    if (string.IsNullOrWhiteSpace(token))
                        throw new ArgumentNullException(nameof(this.BearerToken));

                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                containers.AddRange(await this.GetContainersAsync(httpClient, cancellationToken));
            }

            catch (HttpRequestException e)
            {
                this.LogError("An error occurred while calling to the Kubernetes API: " + e.Message, e);
            }
            catch (Exception e)
            {
                this.LogError(e);
            }
            finally
            {
                httpClient.CancelPendingRequests();
            }

            return new ScannerResults(null, containers.Distinct());
        }

        private async Task<IEnumerable<KubernetesContainerUsageData>> GetContainersAsync(HttpClient client, CancellationToken cancellationToken, string continueToken = null)
        {
            var containers = new List<KubernetesContainerUsageData>();
            var response = await client.GetAsync("/api/v1/pods" + (string.IsNullOrWhiteSpace(continueToken) ? string.Empty : $"?continue={continueToken}"), cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync());
                var pods = (JArray)result["items"];
                foreach (var pod in pods)
                {
                    var server = $"{(string)pod["spec"]?["nodeName"]}:{(string)pod["metadata"]?["namespace"]}:{(string)pod["metadata"]?["name"]}";
                    var statuses = (JArray)pod["status"]?["containerStatuses"];
                    if (statuses == null)
                    {
                        this.LogWarning($"Pod {server} is missing a status/containerStatuses node.");
                        continue;
                    }
                    foreach (var containerStatus in statuses)
                    {
                        var state = ((JObject)containerStatus["state"]);

                        ContainerState? stateValue = null;
                        DateTimeOffset? created = DateTimeOffset.UtcNow;


                        if (state?["running"] != null)
                        {
                            stateValue = ContainerState.Running;
                            if (DateTimeOffset.TryParse((string)state["running"]["startedAt"], out var parsedDate))
                                created = parsedDate;
                        }
                        else if (state?["terminated"] != null)
                        {
                            stateValue = ContainerState.Exited;
                            if (DateTimeOffset.TryParse((string)state["terminated"]["finishedAt"], out var parsedDate))
                                created = parsedDate;
                            else if (DateTimeOffset.TryParse((string)state["terminated"]["startedAt"], out var parsedStartedDate))
                                created = parsedStartedDate;
                        }
                        else if (state?["waiting"] != null)
                        {
                            stateValue = ContainerState.Created;
                        }
                        var id = (string)containerStatus["imageID"];
                        if (id?.Contains("@") == true)
                        {
                            containers.Add(new KubernetesContainerUsageData(server, (string)containerStatus["name"], id.Split('@')[1], stateValue, created));
                        }
                    }
                }

                var token = (string)result["metadata"]?["continue"];
                if (!string.IsNullOrWhiteSpace(token))
                    containers.AddRange(await this.GetContainersAsync(client, cancellationToken, token));
            }
            else
            {
                this.LogError($"An error occurred while calling to the Kubernetes API with Response Code: {response.StatusCode} {response.ReasonPhrase}.  {await response.Content.ReadAsStringAsync()}");
                response.EnsureSuccessStatusCode();
            }
            return containers;
        }

        public enum AuthenticationType
        {
            None = 0,
            [Description("Basic Authentication")]
            Basic = 1,
            [Description("Bearer Token")]
            Bearer = 2
        }
    }
}
